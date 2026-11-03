const fs = require('node:fs')
const base = 'http://127.0.0.1:5088/api/v1'
const out = 'specs/053-screen-completeness/evidence/checkpoint20-real-reads.json'
const api = async (who, path, method = 'GET', body) => {
  const r = await fetch(base + path, { method, headers: { 'X-Dev-User': who + '@hub.test', ...(body === undefined ? {} : {'content-type':'application/json'}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await r.text(); let value = null; try { value = text ? JSON.parse(text) : null } catch { value = text }
  return { status: r.status, value }
}
const main = async () => {
  const result = { observedAtUtc: new Date().toISOString(), flags: {}, reads: {}, failures: [] }
  for (const name of ['visual_system','small_ux','work_screens','capacity_screens','project_screens','people_screens','org_structure','task_pickup']) {
    const x = await api('jordan', `/admin/settings/flag.${name}`, 'PUT', { value: true }); result.flags[name] = x.status
    if (x.status >= 300) result.failures.push({ path: `/admin/settings/flag.${name}`, ...x })
  }
  const users = await api('jordan','/admin/users'); result.reads.users = users.value
  let projects = await api('priya','/projects?take=20'); result.reads.projects = projects.value
  // QA20 starts empty; establish one disposable project through ordinary commands so real reads are non-empty.
  if (projects.value?.totalCount === 0) {
    const ref = await api('jordan','/reference'); const uid = name => users.value.find(u => u.email === name+'@hub.test')?.id
    const civil = ref.value.disciplines.find(d => d.code === 'CIV')?.id
    const created = await api('priya','/projects','POST',{ projectNumber:'QA20-REAL-001', name:'Checkpoint 20 real QA project', clientId:ref.value.clients[0].id, officeId:ref.value.offices[0].id, startDate:'2026-10-26', targetCompletionDate:'2026-11-30', disciplines:[{disciplineId:civil,leadUserId:uid('marc')}], members:[{userId:uid('alex'),roles:['TeamMember'],disciplineId:civil},{userId:uid('jill'),roles:['TeamMember'],disciplineId:civil}] })
    result.commands = { projectCreate: created.status, project: created.value }
    if (created.status < 300 && created.value?.id) {
      const detail = await api('priya',`/projects/${created.value.id}`)
      const activated = await api('priya',`/projects/${created.value.id}/transition`,'POST',{toStatus:'Active',rowVersion:detail.value.rowVersion})
      const pdisc = detail.value.disciplines?.[0]?.id
      const deliverable = await api('priya',`/projects/${created.value.id}/deliverables`,'POST',{name:'QA20 real deliverable',projectDisciplineId:pdisc,deliverableTypeId:ref.value.deliverableTypes[0].id,ownerId:uid('alex'),startDate:'2026-10-26',dueDate:'2026-11-15',requiresReview:false})
      const task1 = await api('priya',`/projects/${created.value.id}/tasks`,'POST',{name:'QA20 real task one',projectDisciplineId:pdisc,deliverableId:deliverable.value?.id,assigneeId:uid('priya'),estimatedHours:8,startDate:'2026-10-26',dueDate:'2026-11-05',requiresReview:false})
      const task2 = await api('priya',`/projects/${created.value.id}/tasks`,'POST',{name:'QA20 real task two',projectDisciplineId:pdisc,deliverableId:deliverable.value?.id,assigneeId:uid('alex'),estimatedHours:8,startDate:'2026-10-27',dueDate:'2026-11-06',requiresReview:false})
      result.commands.projectActivate=activated.status; result.commands.deliverableCreate=deliverable.status; result.commands.taskCreates=[task1.status,task2.status]
      const taskId1=task1.value?.id, taskId2=task2.value?.id
      for (const [taskId,date,hours] of [[taskId1,'2026-10-27',2],[taskId2,'2026-10-28',2]]) result.commands['time:'+hours]= (await api('priya','/time','POST',{taskId,workDate:date,hours,note:'QA20 real acceptance entry'})).status
    }
    projects = await api('priya','/projects?take=20'); result.reads.projects = projects.value
  }
  const me = await api('priya','/me'); result.reads.me = me.value
  const today = me.value?.settings?.today
  const monday = today ? (() => { const d = new Date(today+'T00:00:00Z'); d.setUTCDate(d.getUTCDate() - (d.getUTCDay()+6)%7); return d.toISOString().slice(0,10) })() : null
  result.reads.staff = await api('sam','/staff?scope=direct&showInactive=true')
  result.reads.time = await api('priya', `/time?from=${monday}&to=${today}&scope=mine`)
  result.reads.timeWeek = await api('priya', `/time/week?week=${monday}`)
  result.reads.reports = await api('priya','/reports')
  result.reads.reportWorkload = await api('priya','/reports/workload-employee?unit=fte&period=weeks')
  result.reads.hrIdentities = await api('jordan','/planning/hr-identities')
  result.reads.organisation = await api('jordan','/organisation')
  const pRows = Array.isArray(projects.value) ? projects.value : (projects.value?.rows || projects.value?.items || [])
  const project = pRows[0]
  if (project?.id) {
    result.project = { id: project.id, number: project.projectNumber }
    for (const suffix of ['changes/options','handoffs/options','discipline-coordination?projectId='+project.id]) result.reads['project:'+suffix] = await api('priya', `/projects/${project.id}/${suffix}`)
    const taskList = await api('priya', `/projects/${project.id}/tasks`); result.reads.projectTasks = taskList
    const tasks = taskList.value?.rows || taskList.value?.items || []
    if (tasks[0]?.id) {
      result.reads.taskPickup = await api('priya', `/tasks/${tasks[0].id}/pickup-offer`)
      if (result.reads.taskPickup.status === 404) {
        result.commands = result.commands || {}
        result.commands.pickupOffer = (await api('priya', `/tasks/${tasks[0].id}/pickup-offer`, 'POST', {reason:'QA20 real picker notice read',hoursRemaining:4,neededBy:'2026-11-07',note:'Disposable QA20 offer'})).status
        result.reads.taskPickup = await api('priya', `/tasks/${tasks[0].id}/pickup-offer`)
      }
    }
  }
  for (const [key,x] of Object.entries(result.reads)) if (x.status >= 300) result.failures.push({ read:key, status:x.status, value:x.value })
  fs.writeFileSync(out, JSON.stringify(result,null,2)+'\n')
  console.log(JSON.stringify({ flags: result.flags, failures: result.failures.length, staffStatus: result.reads.staff.status, timeStatus: result.reads.time.status, reportStatus: result.reads.reportWorkload.status }))
}
main().catch(e => { console.error(e); process.exitCode=1 })
