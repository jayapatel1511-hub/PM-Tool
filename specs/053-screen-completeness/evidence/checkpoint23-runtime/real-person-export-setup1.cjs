// Normal commands against root's new synthetic QA23 database only. No operational target accepted.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const m = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-seen-setup1.json'), 'utf8'))
const seen = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-seen-browser1.json'), 'utf8'))
assert.equal(m.database, 'hub_qa_checkpoint23'); assert.equal(seen.status, 'PASS')
const calls = [], out = { database: m.database, entries: [], calls }
async function api(who, method, route, body, version) {
  const response = await fetch(m.apiBase + '/api/v1/' + route, { method,
    headers: { 'X-Dev-User': who, ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(method === 'POST' ? { 'Idempotency-Key': crypto.randomUUID() } : {}), ...(version == null ? {} : { 'If-Match': String(version) }) },
    ...(body ? { body: JSON.stringify(body) } : {}) })
  const text = await response.text(), data = text ? JSON.parse(text) : null
  calls.push({ method, route, status: response.status })
  assert.ok(response.ok, method + ' ' + route + ': ' + response.status + ' ' + text)
  return data
}
;(async () => {
  await api('jordan@hub.test', 'PUT', 'admin/settings/flag.register_screens', { value: true })
  for (const hours of [1, 2]) out.entries.push(await api('alex@hub.test', 'POST', 'time', { taskId: seen.newTaskId, workDate: m.today, hours, note: 'Synthetic report person row' }))
  const ref = await api('priya@hub.test', 'GET', 'reference')
  const type = ref.deliverableTypes.find(row => row.name === 'Drawing Package')
  const purpose = ref.issuePurposes.find(row => row.name === 'For review'), code = ref.issueReturnCodes.find(row => row.code === 'B')
  assert.ok(type && purpose && code)
  out.deliverable = await api('priya@hub.test', 'POST', 'projects/' + m.projectId + '/deliverables', {
    name: 'Synthetic QA23 client drawing', projectDisciplineId: m.disciplineId, deliverableTypeId: type.id,
    ownerId: m.people.alex.id, reviewerId: m.people.jill.id, clientDrawingNumber: '00123', requiresReview: false,
    description: 'PRIVATE DESCRIPTION QA23', revision: 'A' })
  let detail = await api('priya@hub.test', 'GET', 'deliverables/' + out.deliverable.id)
  await api('marc@hub.test', 'POST', 'deliverables/' + out.deliverable.id + '/issue', { purposeOfIssueId: purpose.id,
    revision: 'A', issuedDate: m.today, issuedTo: 'PRIVATE RECIPIENT QA23', note: 'PRIVATE NOTE QA23', rowVersion: detail.deliverable.rowVersion })
  detail = await api('priya@hub.test', 'GET', 'deliverables/' + out.deliverable.id)
  const issue = detail.deliverable.registerInfo.currentIssue
  await api('priya@hub.test', 'PATCH', 'deliverables/' + out.deliverable.id + '/issues/' + issue.id + '/response', {
    returnCodeId: code.id, responseDate: m.today, rowVersion: detail.deliverable.rowVersion,
    issueRowVersion: detail.issues.find(row => row.id === issue.id).registerInfo.rowVersion })
  detail = await api('priya@hub.test', 'GET', 'deliverables/' + out.deliverable.id)
  await api('priya@hub.test', 'PATCH', 'deliverables/' + out.deliverable.id, { revision: 'PRIVATE FUTURE REVISION QA23' }, detail.deliverable.rowVersion)
  out.detail = await api('priya@hub.test', 'GET', 'deliverables/' + out.deliverable.id)
  out.reportQuery = new URLSearchParams({ projectId: m.projectId, from: m.today, to: m.today, scope: 'mine' }).toString()
  out.report = await api('alex@hub.test', 'GET', 'reports/task-hours?' + out.reportQuery)
  const entries = out.report.rows.filter(row => row.entryId)
  assert.equal(entries.length, 2)
  assert.equal(new Set(entries.map(row => row.userReferences.person.sourceKey)).size, 2)
  assert.ok(entries.every(row => row.userReferences.person.id === m.people.alex.id))
  out.status = 'PASS_NORMAL_COMMAND_SETUP'; fs.writeFileSync(path.join(__dirname, 'real-person-export-setup1.json'), JSON.stringify(out, null, 2) + '\n')
  console.log(JSON.stringify({ status: out.status, calls: calls.length, reportRows: entries.length, deliverableId: out.deliverable.id }))
})().catch(error => { fs.writeFileSync(path.join(__dirname, 'real-person-export-setup1-failure.json'), JSON.stringify({ ...out, error: error.message }, null, 2)); console.error(error); process.exitCode = 1 })
