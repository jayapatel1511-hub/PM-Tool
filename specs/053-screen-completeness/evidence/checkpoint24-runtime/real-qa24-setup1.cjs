// Root-owned synthetic normal-command setup. No operational target is accepted.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const ownership = JSON.parse(fs.readFileSync(path.join(__dirname, 'qa24-ownership1.json'), 'utf8'))
assert.equal(ownership.database, 'hub_qa_checkpoint24'); assert.equal(ownership.apiLoopbackPort, 5088)
const result = { database: ownership.database, apiBase: 'http://127.0.0.1:5088', uiBase: 'http://127.0.0.1:8853', people: {}, calls: [] }
async function api(who, method, route, body, version) {
  const response = await fetch(result.apiBase + '/api/v1/' + route, { method, headers: { 'X-Dev-User': who,
    ...(body ? { 'Content-Type': 'application/json' } : {}), ...(method === 'POST' ? { 'Idempotency-Key': crypto.randomUUID() } : {}),
    ...(version == null ? {} : { 'If-Match': '"' + version + '"' }) }, ...(body ? { body: JSON.stringify(body) } : {}) })
  const text = await response.text(), data = text ? JSON.parse(text) : null
  result.calls.push({ method, route, status: response.status })
  assert.ok(response.ok, method + ' ' + route + ': ' + response.status + ' ' + text)
  return data
}
;(async () => {
  for (const [key, email] of Object.entries({ admin: 'jordan@hub.test', pm: 'priya@hub.test', alex: 'alex@hub.test', marc: 'marc@hub.test', jill: 'jill@hub.test', readOnly: 'rita@hub.test' })) {
    const me = await api(email, 'GET', 'me'); result.people[key] = { id: me.id, name: me.displayName }
  }
  for (const flag of ['register_screens', 'people_screens', 'work_screens', 'visual_system']) await api('jordan@hub.test', 'PUT', 'admin/settings/flag.' + flag, { value: true })
  const reference = await api('priya@hub.test', 'GET', 'reference')
  const civil = reference.disciplines.find(row => row.name === 'Civil'), type = reference.deliverableTypes.find(row => row.name === 'Drawing Package')
  assert.ok(civil && type)
  const number = 'QA24-' + Date.now().toString(36).toUpperCase()
  result.createdProject = await api('priya@hub.test', 'POST', 'projects', { projectNumber: number, name: 'Synthetic QA24 drawing intake and people',
    clientId: reference.clients[0].id, officeId: reference.offices[0].id, disciplines: [{ disciplineId: civil.id, leadUserId: result.people.marc.id }],
    members: ['alex', 'jill', 'marc'].map(key => ({ userId: result.people[key].id, roles: ['TeamMember'], disciplineId: civil.id })) })
  result.projectId = result.createdProject.id; result.projectNumber = number; result.typeId = type.id
  let project = await api('priya@hub.test', 'GET', 'projects/' + result.projectId)
  await api('priya@hub.test', 'POST', 'projects/' + result.projectId + '/transition', { toStatus: 'Active', rowVersion: project.rowVersion }, project.rowVersion)
  project = await api('priya@hub.test', 'GET', 'projects/' + result.projectId)
  await api('priya@hub.test', 'PUT', 'projects/' + result.projectId + '/deliverable-numbering', { pattern: 'C-###', rowVersion: project.rowVersion }, project.rowVersion)
  project = await api('priya@hub.test', 'GET', 'projects/' + result.projectId)
  result.disciplineId = project.disciplines.find(row => row.disciplineId === civil.id).id
  result.today = (await api('alex@hub.test', 'GET', 'me/work')).today
  result.task = await api('priya@hub.test', 'POST', 'projects/' + result.projectId + '/tasks', { name: 'Synthetic QA24 original task',
    projectDisciplineId: result.disciplineId, assigneeId: result.people.alex.id, reviewerId: result.people.jill.id, dueDate: result.today, requiresReview: false })
  result.sentinel = await api('priya@hub.test', 'POST', 'projects/' + result.projectId + '/deliverables', { name: 'Synthetic QA24 existing sentinel',
    projectDisciplineId: result.disciplineId, deliverableTypeId: result.typeId, ownerId: result.people.alex.id, reviewerId: result.people.jill.id,
    clientDrawingNumber: '00123', revision: 'A', description: 'Synthetic unchanged existing record', requiresReview: false })
  result.links = []
  result.links.push(await api('priya@hub.test', 'POST', 'items/Project/' + result.projectId + '/links', { title: 'Synthetic QA24 PM source pointer', url: 'https://example.test/qa24/project', linkType: 'Other' }))
  result.links.push(await api('jill@hub.test', 'POST', 'items/Task/' + result.task.id + '/links', { title: 'Synthetic QA24 task author pointer', url: 'https://example.test/qa24/task', linkType: 'Other' }))
  result.options = await api('priya@hub.test', 'GET', 'projects/' + result.projectId + '/deliverables/import-options')
  assert.equal(result.options.canImport, true)
  const search = await api('priya@hub.test', 'GET', 'search?q=' + encodeURIComponent(number))
  assert.equal(search.groups.projects.find(row => row.id === result.projectId).person.id, result.people.pm.id)
  assert.equal(search.groups.tasks.find(row => row.id === result.task.id).person.id, result.people.alex.id)
  const files = await api('priya@hub.test', 'GET', 'files?projects=' + result.projectId)
  const links = files.projects.flatMap(project => project.items.flatMap(item => item.links))
  assert.equal(links.find(link => link.id === result.links[0].id).addedByUser.id, result.people.pm.id)
  assert.equal(links.find(link => link.id === result.links[1].id).addedByUser.id, result.people.jill.id)
  result.status = 'PASS_NORMAL_COMMAND_SETUP'; result.boundary = 'Only new synthetic QA24 database flags/data; no import command yet'
  fs.writeFileSync(path.join(__dirname, 'real-qa24-setup1.json'), JSON.stringify(result, null, 2) + '\n')
  console.log(JSON.stringify({ status: result.status, calls: result.calls.length, projectId: result.projectId, realAuthorIds: [result.people.pm.id, result.people.jill.id] }))
})().catch(error => { fs.writeFileSync(path.join(__dirname, 'real-qa24-setup1-failure.json'), JSON.stringify({ ...result, error: error.message }, null, 2)); console.error(error); process.exitCode = 1 })
