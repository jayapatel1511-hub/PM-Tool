// Root-owned synthetic setup. The database ownership receipt is mandatory; no operational target is accepted.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const base = 'http://127.0.0.1:5088'
const folder = __dirname
const ownership = JSON.parse(fs.readFileSync(path.join(folder, 'qa23-ownership1.json'), 'utf8'))
assert.equal(ownership.database, 'hub_qa_checkpoint23'); assert.equal(ownership.apiLoopbackPort, 5088)
const calls = []
async function request(who, method, route, body, version) {
  const response = await fetch(base + '/api/v1/' + route, { method,
    headers: { 'X-Dev-User': who, ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(method === 'POST' ? { 'Idempotency-Key': crypto.randomUUID() } : {}),
      ...(version === undefined ? {} : { 'If-Match': String(version) }) },
    ...(body ? { body: JSON.stringify(body) } : {}) })
  const text = await response.text(); const data = text ? JSON.parse(text) : null
  calls.push({ method, route, status: response.status })
  assert.ok(response.ok, method + ' ' + route + ': ' + response.status + ' ' + text)
  return data
}
;(async () => {
  const people = {}
  for (const [key, email] of Object.entries({ admin: 'jordan@hub.test', pm: 'priya@hub.test', alex: 'alex@hub.test', marc: 'marc@hub.test', jill: 'jill@hub.test', readOnly: 'rita@hub.test' }))
    people[key] = await request(email, 'GET', 'me')
  assert.equal(people.admin.capabilities.admin, true)
  for (const flag of ['work_screens', 'people_screens', 'screen_patterns', 'visual_system'])
    await request('jordan@hub.test', 'PUT', 'admin/settings/flag.' + flag, { value: true })
  const reference = await request('jordan@hub.test', 'GET', 'reference')
  const civil = reference.disciplines.find(row => row.name === 'Civil'); assert.ok(civil)
  const number = 'QA23-' + Date.now().toString(36).toUpperCase()
  const created = await request('priya@hub.test', 'POST', 'projects', { projectNumber: number,
    name: 'Synthetic QA23 personal work verification', clientId: reference.clients[0].id, officeId: reference.offices[0].id,
    disciplines: [{ disciplineId: civil.id, leadUserId: people.marc.id }],
    members: [{ userId: people.alex.id, roles: ['TeamMember'], disciplineId: civil.id }] })
  let project = await request('priya@hub.test', 'GET', 'projects/' + created.id)
  await request('priya@hub.test', 'POST', 'projects/' + created.id + '/transition', { toStatus: 'Active', rowVersion: project.rowVersion }, project.rowVersion)
  project = await request('priya@hub.test', 'GET', 'projects/' + created.id)
  const work = await request('alex@hub.test', 'GET', 'me/work')
  assert.match(work.today, /^\d{4}-\d{2}-\d{2}$/)
  const discipline = project.disciplines.find(row => row.disciplineId === civil.id); assert.ok(discipline)
  const old = await request('priya@hub.test', 'POST', 'projects/' + project.id + '/tasks', {
    name: 'Synthetic QA23 pre-visit assignment', projectDisciplineId: discipline.id, assigneeId: people.alex.id,
    dueDate: work.today, requiresReview: false })
  const manifest = { database: ownership.database, apiBase: base, uiBase: 'http://127.0.0.1:8853',
    projectId: project.id, projectNumber: project.projectNumber, disciplineId: discipline.id, today: work.today,
    oldTask: old, people: Object.fromEntries(Object.entries(people).map(([key, me]) => [key, { id: me.id, name: me.displayName }])),
    calls, status: 'SETUP PASS; no private visit/open/mark-all was sent by setup; browser join still unrun' }
  fs.writeFileSync(path.join(folder, 'real-seen-setup1.json'), JSON.stringify(manifest, null, 2) + '\n')
  console.log(JSON.stringify({ status: 'PASS', projectNumber: project.projectNumber, calls: calls.length, privateCommands: 0 }))
})().catch(error => { fs.writeFileSync(path.join(folder, 'real-seen-setup1-failure.json'), JSON.stringify({ calls, error: error.message }, null, 2)); console.error(error); process.exitCode = 1 })
