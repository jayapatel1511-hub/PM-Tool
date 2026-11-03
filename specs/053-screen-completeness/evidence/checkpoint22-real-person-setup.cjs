// SOURCE ONLY until the coordinator runs it against its disposable QA22 API.
// Prerequisites: TUESDAY_QA_DATABASE=hub_qa_checkpoint22; fixed API5088 bound to that DB by root;
// Anonymous GET/config Development then active-only /api/dev/users must confirm every seed email before any identity header; people_screens already ON.
// Optional TUESDAY_QA_REQUIRE_CAPACITY_SCREENS=1 requires that existing flag too. No flag writes here.
// No browser, SQL/ORM, service, schema, auth/role, clock or deletion operation. Root alone drops QA22.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { randomUUID } = require('node:crypto')
const API = 'http://127.0.0.1:5088/api/v1'
const UI = 'http://127.0.0.1:8853'
const OUTPUT = path.join(__dirname, 'checkpoint22-real-person-setup.json')
const EXPECTED_DATABASE = 'hub_qa_checkpoint22'
const ACTORS = ['jordan', 'priya', 'marc', 'alex', 'omar', 'rita', 'lena']
const runId = `${Date.now().toString(36).toUpperCase()}-${randomUUID().slice(0, 8).toUpperCase()}`
const receipt = { status: 'FAIL_OR_UNPROVEN', evidence: 'normal-command local API only; no browser acceptance', runId,
  startedAtUtc: new Date().toISOString(), prerequisites: {}, actors: {}, fixture: {}, commands: {}, reads: {}, joins: {}, http: [], failures: [],
  limits: ['The database env guard is an operator assertion; this script cannot attest the server connection string. Root must bind API5088 to disposable QA22.',
    'No browser/native/deployment/acceptance result is claimed.', 'No facts/certifications or capacity inputs are changed; returned facts/hours may be absent, partial or provisional.'] }
const isId = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) && value !== '00000000-0000-0000-0000-000000000000'
const weekOf = day => { assert.match(day, /^\d{4}-\d{2}-\d{2}$/); const date = new Date(`${day}T00:00:00Z`); assert.ok(Number.isFinite(date.valueOf())); date.setUTCDate(date.getUTCDate() - (date.getUTCDay() + 6) % 7); return date.toISOString().slice(0, 10) }
const problem = value => value && typeof value === 'object' ? Object.fromEntries(['code', 'detail', 'errors', 'currentRowVersion'].filter(key => key in value).map(key => [key, value[key]])) : { detail: 'Non-JSON response' }
let projectId, memberId, writesEnabled = false, confirmedSeedEmails = null

async function request(actor, route, { method = 'GET', body, version } = {}) {
  assert.equal(process.env.TUESDAY_QA_DATABASE, EXPECTED_DATABASE, 'QA database guard must stay exact')
  assert.ok(route.startsWith('/') && !route.startsWith('//') && !route.includes('://'), 'Only a fixed API-relative route is allowed')
  const devPicker = route === '/api/dev/users'
  if (devPicker) { assert.equal(actor, null); assert.equal(method, 'GET'); assert.equal(body, undefined); assert.equal(version, undefined) }
  const destination = devPicker ? new URL('/api/dev/users', API) : new URL(API + route)
  assert.equal(destination.origin, 'http://127.0.0.1:5088'); assert.ok(devPicker || destination.pathname.startsWith('/api/v1/'))
  if (actor) assert.ok(confirmedSeedEmails?.has(`${actor}@hub.test`), 'No identity header before ALL selected seeds are verified in the anonymous active-only picker')
  if (method !== 'GET') {
    assert.ok(writesEnabled, 'No command before Development/identity/flag/reference guards')
    const allowed = method === 'POST' && route === '/projects'
      || method === 'POST' && projectId && route === `/projects/${projectId}/transition`
      || method === 'PATCH' && projectId && memberId && route === `/projects/${projectId}/members/${memberId}/responsibility`
    assert.ok(allowed, `Command outside this runner's exact project/note scope: ${method} ${route}`)
  }
  const response = await fetch(destination, { method, redirect: 'error', signal: AbortSignal.timeout(20000),
    headers: { ...(actor ? { 'X-Dev-User': `${actor}@hub.test` } : {}), ...(body === undefined ? {} : { 'Content-Type': 'application/json' }), ...(version == null ? {} : { 'If-Match': `"${version}"` }) },
    body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text(); let value = null
  try { value = text ? JSON.parse(text) : null } catch { /* A non-JSON response is recorded as an unavailable result, never guessed. */ }
  const result = { status: response.status, value }
  receipt.http.push({ actor: actor ?? 'anonymous', method, route, status: response.status, ...(!response.ok ? { problem: problem(value) } : {}) })
  return result
}
async function expect(actor, route, options, status = 200) {
  const result = await request(actor, route, options)
  assert.equal(result.status, status, `${actor ?? 'anonymous'} ${options?.method ?? 'GET'} ${route}: expected${status}, received${result.status}`)
  assert.ok(result.value && typeof result.value === 'object', `JSON body required: ${route}`)
  return result.value
}
async function visibleProjects(actor) {
  const all = []; let total
  for (let page = 1; page <= 10; page++) {
    const read = await expect(actor, `/projects?includeArchived=true&pageSize=200&page=${page}`)
    assert.ok(Array.isArray(read.items) && Number.isInteger(read.totalCount), 'Actual visible-project paging envelope required')
    if (total == null) total = read.totalCount
    assert.equal(read.totalCount, total, 'Visible project population changed during setup; rerun only after preserving partial receipt')
    all.push(...read.items.map(row => ({ id: row.id, projectNumber: row.projectNumber })))
    if (all.length >= total) { assert.equal(all.length, total); assert.equal(new Set(all.map(row => row.id)).size, total); return all }
    assert.ok(read.items.length > 0, 'Visible project paging ended before its reported total')
  }
  throw new Error('Visible project population exceeds this bounded2000-row receipt; completeness remains unproven')
}
async function person(actor, userId) {
  const first = await expect(actor, `/people/${userId}/panel?projectsPage=1&projectsPageSize=100`)
  assert.equal(first.id, userId); assert.ok(Array.isArray(first.projects?.items) && Number.isInteger(first.projects.total))
  const items = [...first.projects.items]
  for (let page = 2; items.length < first.projects.total && page <= 20; page++) {
    const read = await expect(actor, `/people/${userId}/panel?projectsPage=${page}&projectsPageSize=100`)
    assert.equal(read.id, userId); assert.equal(read.projects.total, first.projects.total, 'Membership population changed while paging')
    assert.ok(Array.isArray(read.projects.items) && read.projects.items.length); items.push(...read.projects.items)
  }
  assert.equal(items.length, first.projects.total, 'Person membership paging must be complete within its bound')
  assert.equal(new Set(items.map(row => row.memberId)).size, items.length)
  for (const key of ['email', 'allocations', 'approvedLeave', 'weeklyCapacityHours', 'tasks']) assert.equal(key in first, false, `Narrow panel must omit ${key}`)
  return { ...first, projects: { ...first.projects, items } }
}
async function team(actor = 'priya') { return expect(actor, `/projects/${projectId}/team`) }
function member(read) { const rows = read.members.filter(row => row.id === memberId); assert.equal(rows.length, 1); return rows[0] }
async function note(actor, text) {
  const before = member(await team()); assert.ok(Number.isInteger(before.rowVersion)); assert.equal(text.length, 80)
  const result = await expect(actor, `/projects/${projectId}/members/${memberId}/responsibility`, { method: 'PATCH', body: { responsibilityNote: text, rowVersion: before.rowVersion }, version: before.rowVersion })
  receipt.commands[`${actor}Note`] = { id: result.id, rowVersion: result.rowVersion, responsibilityNote: result.responsibilityNote, priorRowVersion: before.rowVersion }
  assert.equal(result.id, memberId); assert.equal(result.rowVersion, before.rowVersion + 1); assert.equal(result.responsibilityNote, text)
  const after = member(await team()); assert.equal(after.responsibility.note, text); assert.equal(after.rowVersion, result.rowVersion)
  assert.deepEqual(after.roles, before.roles); assert.equal(after.primaryDisciplineId, before.primaryDisciplineId)
  return after
}
function fields(row, keys) {
  for (const key of keys) assert.ok(Object.hasOwn(row, key), `Canonical refusal-proof field is missing: ${key}`)
  return Object.fromEntries(keys.map(key => [key, row[key]]))
}
const canonicalProject = row => fields(row, ['id', 'projectNumber', 'name', 'rowVersion', 'status', 'projectManagerId', 'visibility'])
const canonicalMember = row => ({ ...fields(row, ['id', 'userId', 'rowVersion', 'roles', 'primaryDisciplineId', 'leadOf', 'isPrimaryPm']), responsibilityNote: fields(row.responsibility, ['note']).note })
const canonicalTeam = read => ({ members: read.members.map(canonicalMember).sort((a, b) => a.id.localeCompare(b.id)),
  disciplines: read.disciplines.map(row => fields(row, ['id', 'disciplineId', 'rowVersion', 'leadUserId', 'isActive'])).sort((a, b) => a.id.localeCompare(b.id)) })
async function scopedMemberAudit() {
  // ProjectMember's registered audit type is Member; evaluator activity uses other item types.
  const read = await request('priya', `/projects/${projectId}/activity?itemType=Member&pageSize=200&page=1`)
  if (read.status !== 200) return { status: 'UNPROVEN', httpStatus: read.status, problem: problem(read.value), rows: null }
  assert.ok(Array.isArray(read.value?.items) && Number.isInteger(read.value.totalCount), 'Member activity envelope is required')
  if (read.value.totalCount > read.value.items.length) return { status: 'UNPROVEN', reason: 'Member activity exceeds bounded first200 rows', rows: null }
  const rows = read.value.items.filter(row => row.itemId === memberId).map(row => fields(row, ['id', 'itemId', 'itemType', 'action', 'actorId', 'occurredAt', 'changes', 'reason', 'correlationId'])).sort((a, b) => a.id.localeCompare(b.id))
  return rows.length ? { status: 'AVAILABLE', rows } : { status: 'UNPROVEN', reason: 'No target member activity is returned by the scoped API', rows: null }
}
async function refusal(actor) {
  const beforeTeam = await team(), beforeProject = await expect('priya', `/projects/${projectId}`), before = member(beforeTeam)
  const beforeCanonical = { project: canonicalProject(beforeProject), team: canonicalTeam(beforeTeam) }, beforeAudit = await scopedMemberAudit()
  const ownView = member(await team(actor)); assert.equal(ownView.responsibility.canEdit, false)
  const result = await request(actor, `/projects/${projectId}/members/${memberId}/responsibility`, { method: 'PATCH', body: { responsibilityNote: `Synthetic refused ${actor}`, rowVersion: before.rowVersion }, version: before.rowVersion })
  const afterTeam = await team(), afterProject = await expect('priya', `/projects/${projectId}`)
  const afterCanonical = { project: canonicalProject(afterProject), team: canonicalTeam(afterTeam) }, afterAudit = await scopedMemberAudit()
  const auditAvailable = beforeAudit.status === 'AVAILABLE' && afterAudit.status === 'AVAILABLE'
  receipt.commands[`${actor}Refusal`] = { status: result.status, problem: problem(result.value), beforeMember: canonicalMember(before), afterMember: canonicalMember(member(afterTeam)),
    beforeCanonical, afterCanonical, scopedMemberAudit: { before: beforeAudit, after: afterAudit },
    observedUnchanged: { canonicalProject: JSON.stringify(beforeCanonical.project) === JSON.stringify(afterCanonical.project),
      canonicalTeamMembershipsAndDisciplineLeads: JSON.stringify(beforeCanonical.team) === JSON.stringify(afterCanonical.team),
      scopedMemberAudit: auditAvailable ? JSON.stringify(beforeAudit.rows) === JSON.stringify(afterAudit.rows) : null } }
  assert.equal(result.status, 403); assert.deepEqual(afterCanonical, beforeCanonical)
  if (auditAvailable) assert.deepEqual(afterAudit.rows, beforeAudit.rows, 'A refused note must not append/change the target membership activity')
  else receipt.limits.push(`${actor} refusal: canonical project/member state is checked; scoped membership activity is unproven.`)
}

async function main() {
  // Preserve an existing receipt and its IDs. The coordinator must archive it before a deliberate rerun.
  if (fs.existsSync(OUTPUT)) { console.error('FAIL_OR_UNPROVEN: existing QA22 person receipt preserved; no network/command executed.'); process.exitCode = 1; return }
  try {
    assert.equal(process.env.TUESDAY_QA_DATABASE, EXPECTED_DATABASE, 'Set TUESDAY_QA_DATABASE exactly hub_qa_checkpoint22 before execution')
    receipt.prerequisites.databaseRequested = process.env.TUESDAY_QA_DATABASE
    receipt.prerequisites.api = API
    const config = await expect(null, '/config'); receipt.prerequisites.authMode = config.authMode
    assert.equal(config.authMode, 'Development', 'Only the coordinator disposable Development API is allowed')
    const existingUsers = await expect(null, '/api/dev/users'); assert.ok(Array.isArray(existingUsers), 'Anonymous Development picker must return its active-user array')
    // Program.cs filters this anonymous endpoint by IsActive; it intentionally exposes neither IDs nor an isActive field.
    const selectedSeeds = ACTORS.map(actor => {
      const email = `${actor}@hub.test`, matches = existingUsers.filter(user => typeof user.email === 'string' && user.email.toLowerCase() === email)
      assert.equal(matches.length, 1, `Root must preseed exactly one active ${email}; this runner must never auto-provision it`)
      return { email, displayName: matches[0].displayName, presenceInActiveOnlyPicker: true }
    })
    confirmedSeedEmails = new Set(selectedSeeds.map(seed => seed.email)); receipt.prerequisites.existingActiveSeeds = selectedSeeds
    for (const actor of ACTORS) {
      const me = await expect(actor, '/me'); assert.ok(isId(me.id)); assert.equal(me.email.toLowerCase(), `${actor}@hub.test`)
      receipt.actors[actor] = { id: me.id, displayName: me.displayName, email: me.email, systemRoles: me.systemRoles, capabilities: me.capabilities, flags: me.flags,
        today: me.settings?.today, orgTimeZone: me.settings?.orgTimeZone }
      assert.equal(me.flags?.people_screens, true, `${actor}: people_screens must already be enabled by root`)
      if (process.env.TUESDAY_QA_REQUIRE_CAPACITY_SCREENS === '1') assert.equal(me.flags?.capacity_screens, true, 'Optional capacity_screens prerequisite is not satisfied')
    }
    const { priya, marc, alex, omar, rita, lena } = receipt.actors
    assert.equal(new Set(Object.values(receipt.actors).map(actor => actor.id)).size, 7, 'Fresh seed identities must be distinct')
    assert.equal(priya.capabilities.createProject, true); assert.equal(rita.capabilities.readOnly, true); assert.equal(rita.capabilities.workload, false); assert.equal(lena.capabilities.workload, true)
    const reference = await expect('priya', '/reference')
    const active = rows => rows.filter(row => row.isActive === true)
    const civil = active(reference.disciplines).find(row => row.code === 'CIV'), electrical = active(reference.disciplines).find(row => row.code === 'ELE')
    const client = active(reference.clients)[0], office = active(reference.offices)[0]
    assert.ok(civil && electrical && client && office, 'Current active Civil/Electrical/client/office references are required')
    const projectNumber = `QA22-PER-${runId}`
    writesEnabled = true
    const created = await expect('priya', '/projects', { method: 'POST', body: { projectNumber, name: `Synthetic QA22 person panel ${runId}`, clientId: client.id, officeId: office.id,
      disciplines: [{ disciplineId: civil.id, leadUserId: marc.id }, { disciplineId: electrical.id, leadUserId: omar.id }],
      members: [{ userId: alex.id, roles: ['TeamMember'], disciplineId: civil.id }] } }, 201)
    assert.ok(isId(created.id)); projectId = created.id
    receipt.fixture = { projectId, projectNumber: created.projectNumber, personId: alex.id, civilReferenceId: civil.id, electricalReferenceId: electrical.id, clientId: client.id, officeId: office.id }
    const project = await expect('priya', `/projects/${projectId}`); assert.equal(project.projectManagerId, priya.id)
    receipt.commands.activate = await expect('priya', `/projects/${projectId}/transition`, { method: 'POST', body: { toStatus: 'Active', rowVersion: project.rowVersion }, version: project.rowVersion })
    const initialTeam = await team(); const alexMember = initialTeam.members.filter(row => row.userId === alex.id); assert.equal(alexMember.length, 1)
    memberId = alexMember[0].id; assert.ok(isId(memberId)); receipt.fixture.memberId = memberId
    const primary = initialTeam.disciplines.find(row => row.disciplineId === civil.id), other = initialTeam.disciplines.find(row => row.disciplineId === electrical.id)
    assert.ok(primary?.isActive && other?.isActive); assert.equal(primary.leadUserId, marc.id); assert.equal(other.leadUserId, omar.id)
    assert.equal(alexMember[0].primaryDisciplineId, primary.id); assert.equal(alexMember[0].responsibility.canEdit, true)
    assert.equal(member(await team('marc')).responsibility.canEdit, true)
    receipt.fixture.primaryProjectDisciplineId = primary.id; receipt.fixture.wrongLeadProjectDisciplineId = other.id
    const pmText = 'Synthetic QA22 PM Civil responsibility note '.padEnd(80, 'x'), leadText = 'Synthetic QA22 own Civil lead responsibility note '.padEnd(80, 'y')
    await note('priya', pmText); const current = await note('marc', leadText)
    await refusal('omar'); await refusal('rita')
    receipt.fixture.finalResponsibilityNote = current.responsibility.note; receipt.fixture.finalMemberRowVersion = current.rowVersion
    for (const actor of ['priya', 'rita', 'lena']) {
      const read = await person(actor, alex.id), visible = await visibleProjects(actor), ids = new Set(visible.map(row => row.id))
      for (const membership of read.projects.items) assert.ok(ids.has(membership.projectId), `${actor}: panel project must be in that same actor's actual visible-project API read`)
      const own = read.projects.items.filter(row => row.projectId === projectId); assert.equal(own.length, 1)
      assert.equal(own[0].memberId, memberId); assert.equal(own[0].responsibilityNote, leadText); assert.deepEqual(own[0].roles, current.roles)
      assert.equal(read.week, weekOf(read.today)); receipt.reads[`${actor}Person`] = read
      receipt.joins[`${actor}VisibleProjects`] = { visibleProjects: visible, panelProjectIds: read.projects.items.map(row => row.projectId), actualPanelMembershipTotal: read.projects.total, allPanelProjectsVisible: true, createdMembershipNoteAndRolesMatch: true }
    }
    receipt.reads.finalProjectTeam = await team()
    const workspace = await expect('priya', `/team?projects=${projectId}`); receipt.reads.workspaceTeam = workspace
    const workspaceAlex = workspace.people.find(row => row.userId === alex.id), workspaceMembership = workspaceAlex?.projects.find(row => row.projectId === projectId)
    assert.equal(workspaceMembership?.responsibilityNote, leadText); receipt.joins.workspaceNoteMatches = true
    const deniedGrid = await request('rita', `/planning/grid?personIds=${alex.id}&from=${receipt.reads.ritaPerson.week}&weeks=1`)
    receipt.reads.ritaPlannerProbe = { status: deniedGrid.status, problem: problem(deniedGrid.value) }; assert.equal(deniedGrid.status, 403)
    const gridRoute = `/planning/grid?personIds=${alex.id}&from=${receipt.reads.lenaPerson.week}&weeks=1`, grid = await expect('lena', gridRoute)
    assert.ok(Array.isArray(grid.people)); assert.ok(grid.people.every(row => row.id === alex.id), 'Candidate read must never fill from a wider directory')
    const scoped = grid.people.find(row => row.id === alex.id)
    if (!scoped) {
      receipt.joins.lenaCapacity = { status: 'OUTSIDE_ACTUAL_SCOPE', requestedPersonId: alex.id, week: receipt.reads.lenaPerson.week, returnedPersonIds: [], figure: null }
      receipt.limits.push('Lena candidate grid omitted Alex; free/away figures are unproven and must remain absent. No scope or capacity data was changed.')
    } else {
      const cell = scoped.cells.find(row => row.week === receipt.reads.lenaPerson.week); assert.ok(cell, 'In-scope current-week cell required')
      for (const field of ['remaining', 'timeAway', 'capacity']) assert.ok(typeof cell[field] === 'number' && Number.isFinite(cell[field]), `Canonical ${field} must be recorded, not defaulted`)
      assert.equal(typeof cell.provisional, 'boolean'); assert.equal(typeof scoped.partial, 'boolean')
      receipt.joins.lenaCapacity = { status: 'ACTUAL_SCOPED_API_VALUE', requestedPersonId: alex.id, week: cell.week, gridWeeks: grid.weeks, currentWeek: grid.currentWeek,
        evaluatedAt: grid.evaluatedAt, partial: scoped.partial, provisional: cell.provisional, canonicalCell: cell,
        displayState: scoped.partial ? 'Partial view; numeric free figure suppressed' : cell.provisional ? 'Provisional availability; numeric free figure suppressed' : 'Canonical free/away values available' }
    }
    receipt.links = { projectTeam: `${UI}/projects/${encodeURIComponent(created.projectNumber)}/team`, workspaceTeam: `${UI}/team?projects=${projectId}`,
      priyaPerson: `${UI}/projects/${encodeURIComponent(created.projectNumber)}/team?panel=Person:${alex.id}`, apiPerson: `${API}/people/${alex.id}/panel`, lenaCurrentWeekGrid: API + gridRoute }
    if (lena.capabilities.allStaff === true) {
      receipt.reads.lenaAllStaffPerson = await expect('lena', `/people/${alex.id}/panel?staffScope=all`)
      assert.equal(receipt.reads.lenaAllStaffPerson.id, alex.id)
      receipt.links.lenaStaffPerson = `${UI}/staff?scope=all&panel=Person:${alex.id}&personStaffScope=all`
    }
    receipt.selectors = { personTrigger: `[data-person-panel-trigger="${alex.id}"]`, personPanel: `[data-person-panel="${alex.id}"]`, membershipNote: `[data-member-responsibility="${memberId}"]`,
      editResponsibilityButtonName: `Edit responsibility for ${alex.displayName}`, panelVisibleProject: `[data-person-project="${projectId}"]`, personName: alex.displayName, finalNoteText: leadText }
    receipt.status = 'PASS_API_ONLY'
  } catch (error) { receipt.failures.push({ status: 'FAIL_OR_UNPROVEN', message: error instanceof Error ? error.message : String(error) }); process.exitCode = 1 }
  finally {
    receipt.finishedAtUtc = new Date().toISOString(); receipt.limits.push('Refusal no-change assertions cover canonical project identity/name/version/status/manager/visibility and exact Team membership roles/primary/note/versions/discipline leads, plus target-member activity when available. Evaluator timestamps/health materialization are excluded; no all-database or audit-chain attestation is claimed.')
    fs.writeFileSync(OUTPUT, JSON.stringify(receipt, null, 2) + '\n', { flag: 'wx' })
    console.log(JSON.stringify({ status: receipt.status, receipt: OUTPUT, projectId: receipt.fixture.projectId ?? null, memberId: receipt.fixture.memberId ?? null, failures: receipt.failures }))
  }
}
if (require.main === module) main().catch(error => { console.error(JSON.stringify({ status: 'FAIL_OR_UNPROVEN', message: error.message, receiptWriteMayHaveFailed: true })); process.exitCode = 1 })
