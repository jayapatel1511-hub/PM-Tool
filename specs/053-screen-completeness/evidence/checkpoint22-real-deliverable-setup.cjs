// SOURCE-ONLY PREPARATION: root executes later, exclusively against its disposable QA22 API.
// Prerequisites: TUESDAY_QA_DATABASE=hub_qa_checkpoint22, the API already using that database,
// /config authMode Development, seeded EXISTING personas, register_screens already enabled,
// and root-controlled services/clock/mail/jobs configured. This runner does not configure any of them.
// Optional EXISTING DEVELOPMENT-persona selectors: TUESDAY_QA_PM_EMAIL, TUESDAY_QA_LEAD_EMAIL, TUESDAY_QA_OWNER_EMAIL.
// Defaults name the current Seed.DevUsers personas; IDs always come from fresh /me and /reference reads.
// No SQL/ORM, old evidence IDs, sign-in command, role override, auth/clock/flag/schema change or browser.
// A prior receipt is never overwritten: root must preserve/rename it before a deliberate retry.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { randomUUID } = require('node:crypto')

const API = 'http://127.0.0.1:5088/api/v1'
const UI = 'http://127.0.0.1:8853'
const RECEIPT = path.join(__dirname, 'checkpoint22-real-deliverable-setup.json')
const emails = { pm: process.env.TUESDAY_QA_PM_EMAIL || 'priya@hub.test', lead: process.env.TUESDAY_QA_LEAD_EMAIL || 'marc@hub.test', owner: process.env.TUESDAY_QA_OWNER_EMAIL || 'alex@hub.test' }
const out = { schemaVersion: 1, status: 'FAIL_OR_UNPROVEN', startedAtUtc: new Date().toISOString(), api: API,
  guard: { requiredDatabase: 'hub_qa_checkpoint22', databaseSelector: process.env.TUESDAY_QA_DATABASE || null, databaseIdentity: 'Operator-attested selector; the public API does not expose its actual database name.' },
  personas: {}, reference: {}, fixture: { project: null, deliverables: [] }, requests: [], assertions: [], facts: {}, links: {}, selectors: {}, failures: [],
  limits: ['API setup/assertions only; no UI, browser, human, pilot, deployment or Accepted claim.', 'All IDs are discovered or returned during this invocation; no prior receipt is used to select records.',
    'No retry, cleanup, deletion, auth/role/flag/clock/schema change or external connection. Partial synthetic records remain available for inspection on failure.'] }
let projectId = null, projectNumber = null, receiptOwned = false
const ids = rows => rows.map(row => row.id).sort()
const open = row => !['Issued', 'Accepted', 'Cancelled'].includes(row.status)
const check = (label, fn) => { fn(); out.assertions.push(label) }
const guid = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
const version = (value, label) => { assert.ok(Number.isInteger(value) && value >= 0, `${label}: a real nonnegative version is required`); return value }
const date = value => { assert.match(value || '', /^\d{4}-\d{2}-\d{2}$/); const parsed = new Date(`${value}T00:00:00Z`); assert.equal(parsed.toISOString().slice(0, 10), value); return value }
const offset = (today, days) => { const value = new Date(`${date(today)}T00:00:00Z`); value.setUTCDate(value.getUTCDate() + days); return value.toISOString().slice(0, 10) }
const query = values => new URLSearchParams(Object.entries(values).filter(([, value]) => value !== null && value !== undefined).map(([key, value]) => [key, String(value)])).toString()

function allowedMutation(method, pathname) {
  if (method === 'POST' && pathname === '/projects') return true
  if (!projectId) return false
  if (method === 'POST' && [ `/projects/${projectId}/transition`, `/projects/${projectId}/deliverables` ].includes(pathname)) return true
  if (method === 'PUT' && pathname === `/projects/${projectId}/deliverable-numbering`) return true
  const deliverable = out.fixture.deliverables.find(row => pathname.startsWith(`/deliverables/${row.id}/`))
  if (!deliverable) return false
  if (method === 'POST' && [`/deliverables/${deliverable.id}/transition`, `/deliverables/${deliverable.id}/issue`].includes(pathname)) return true
  return method === 'PATCH' && (deliverable.issueIds || []).some(issueId => pathname === `/deliverables/${deliverable.id}/issues/${issueId}/response`)
}

async function req(persona, route, method = 'GET', body, expected = 200) {
  assert.ok(route.startsWith('/') && !route.startsWith('//') && !route.includes('\\'), 'only a local API-relative route is allowed')
  // The single anonymous Development picker read verifies the selected users already exist before any identity header is sent.
  const devPicker = route === '/api/dev/users'
  if (devPicker) { assert.equal(method, 'GET'); assert.equal(persona, null); assert.equal(body, undefined) }
  const destination = devPicker ? new URL('/api/dev/users', API) : new URL(API + route)
  assert.equal(destination.origin, 'http://127.0.0.1:5088'); assert.ok(destination.pathname.startsWith('/api/v1/') || devPicker)
  const pathname = devPicker ? destination.pathname : destination.pathname.slice('/api/v1'.length)
  if (method !== 'GET') assert.ok(allowedMutation(method, pathname), `Refused unlisted mutation ${method} ${pathname}`)
  assert.ok(out.requests.length < 180, 'bounded setup request budget exceeded')
  const entry = { persona, method, route, status: null }; out.requests.push(entry)
  const response = await fetch(destination, { method, redirect: 'error', signal: AbortSignal.timeout(15000),
    headers: { ...(persona ? { 'X-Dev-User': emails[persona] } : {}), ...(body === undefined ? {} : { 'content-type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body) })
  entry.status = response.status
  const text = await response.text(); let value = null
  try { value = text ? JSON.parse(text) : null } catch { throw new Error(`${method} ${pathname}: expected JSON, HTTP ${response.status}`) }
  if (response.status !== expected) {
    entry.problem = { code: value?.code || null, detail: String(value?.detail || value?.title || 'Unexpected response').slice(0, 500) }
    throw new Error(`${method} ${pathname}: HTTP ${response.status}, expected ${expected}; ${entry.problem.code || entry.problem.detail}`)
  }
  return value
}

async function detail(id, persona = 'pm') {
  const value = await req(persona, `/deliverables/${id}`)
  assert.equal(value.deliverable.id, id); assert.equal(value.deliverable.projectId, projectId)
  version(value.deliverable.rowVersion, 'deliverable'); assert.ok(value.deliverable.registerInfo, 'register envelope is required')
  const fixture = out.fixture.deliverables.find(row => row.id === id)
  Object.assign(fixture, { status: value.deliverable.status, rowVersion: value.deliverable.rowVersion, dueDate: value.deliverable.dueDate,
    issueIds: value.issues.map(issue => issue.id), currentIssueId: value.deliverable.registerInfo.currentIssue?.id || null })
  return value
}
async function list(filters = {}) {
  const result = await req('pm', `/projects/${projectId}/deliverables?${query(filters)}`)
  assert.ok(Array.isArray(result), 'canonical register must be an array'); assert.ok(result.every(row => row.projectId === projectId), 'register scope must stay in the fresh project')
  return result
}
async function create(label, extra = {}) {
  const made = await req('pm', `/projects/${projectId}/deliverables`, 'POST', { name: `Synthetic QA22 ${label}`, projectDisciplineId: out.reference.projectDisciplineId,
    deliverableTypeId: out.reference.deliverableTypeId, ownerId: out.personas.owner.id, reviewerId: null, requiresReview: false, priority: 'Medium', ...extra }, 201)
  assert.ok(guid(made.id), 'create must return a fresh deliverable ID')
  out.fixture.deliverables.push({ label, id: made.id, key: made.key, creationWarnings: made.warnings, issueIds: [],
    href: `/projects/${encodeURIComponent(projectNumber)}/deliverables?panel=Deliverable:${made.id}` })
  const value = await detail(made.id)
  check(`${label}: normal create is Not Started with no fabricated issue`, () => { assert.equal(value.deliverable.status, 'Not Started'); assert.equal(value.issues.length, 0) })
  return value
}
async function step(id, toStatus) {
  const before = await detail(id)
  await req('pm', `/deliverables/${id}/transition`, 'POST', { toStatus, rowVersion: before.deliverable.rowVersion,
    reason: 'Synthetic QA22 documented lifecycle setup', comment: 'Synthetic QA22 recipient requested a revised issue.' })
  const after = await detail(id); assert.equal(after.deliverable.status, toStatus); return after
}
async function issue(id, purpose, revision, issuedDate) {
  const before = await detail(id)
  assert.equal(before.permissions.issue, true, 'normal issue authority/guard must permit this fixture')
  await req('pm', `/deliverables/${id}/issue`, 'POST', { rowVersion: before.deliverable.rowVersion, purposeOfIssueId: purpose.id, issuedDate, revision,
    issuedTo: 'Synthetic QA22 recipient', transmittalUrl: null, note: 'Synthetic QA22 actual issue through the canonical command.', confirmOpenTasks: false })
  const after = await detail(id), added = after.issues.filter(row => !before.issues.some(old => old.id === row.id))
  assert.equal(added.length, 1); assert.equal(after.issues.length, before.issues.length + 1); assert.equal(after.deliverable.status, 'Issued')
  assert.equal(after.deliverable.registerInfo.currentIssue.id, added[0].id)
  assert.equal(added[0].registerInfo.purposeOfIssueId, purpose.id); assert.equal(added[0].registerInfo.purpose, purpose.name)
  return { detail: after, issueId: added[0].id }
}
function immutableIssue(value) {
  return { id: value.id, issuedDate: value.issuedDate, revision: value.revision, issuedTo: value.issuedTo, transmittalUrl: value.transmittalUrl,
    note: value.note, issuedBy: value.issuedBy, purposeOfIssueId: value.registerInfo.purposeOfIssueId, purpose: value.registerInfo.purpose }
}
async function respond(id, issueId, code, responseDate, reason = null) {
  const before = await detail(id), recorded = before.issues.find(row => row.id === issueId)
  assert.ok(recorded && recorded.registerInfo); assert.equal(before.permissions.recordIssueResponse, true)
  const beforeParent = version(before.deliverable.rowVersion, 'parent baseline'), beforeChild = version(recorded.registerInfo.rowVersion, 'issue baseline')
  const saved = await req('pm', `/deliverables/${id}/issues/${issueId}/response`, 'PATCH', { returnCodeId: code.id, responseDate, rowVersion: beforeParent,
    issueRowVersion: beforeChild, reason })
  const after = await detail(id), changed = after.issues.find(row => row.id === issueId)
  check(`response ${issueId}: immutable issuance, same status/count, explicit code/date and both versions`, () => {
    assert.deepEqual(before.issues.map(immutableIssue), after.issues.map(immutableIssue)); assert.equal(after.deliverable.status, before.deliverable.status)
    assert.equal(after.issues.length, before.issues.length); assert.equal(changed.registerInfo.returnCodeId, code.id); assert.equal(changed.registerInfo.returnCode, code.code)
    assert.equal(changed.registerInfo.returnLabel, code.name); assert.equal(changed.registerInfo.responseDate, responseDate)
    assert.ok(saved.rowVersion > beforeParent); assert.ok(saved.issueRowVersion > beforeChild)
  })
  return after
}
function equalCurrent(label, row, value) {
  check(label, () => {
    const current = value.deliverable.registerInfo.currentIssue, listed = row.registerInfo.currentIssue
    assert.deepEqual(listed, current)
    const history = value.issues.find(issue => issue.id === current.id)
    for (const key of ['purposeOfIssueId', 'purpose', 'returnCodeId', 'returnCode', 'returnLabel', 'responseDate']) assert.equal(current[key], history.registerInfo[key])
    assert.equal(current.revision, history.revision); assert.equal(current.issuedDate, history.issuedDate)
  })
}

async function main() {
  try {
    // Exclusive reservation precedes guards and every request. EEXIST must stop without network or fixture creation.
    fs.writeFileSync(RECEIPT, JSON.stringify(out, null, 2) + '\n', { flag: 'wx', mode: 0o600 })
    receiptOwned = true
    assert.equal(process.env.TUESDAY_QA_DATABASE, 'hub_qa_checkpoint22', 'Root must select the disposable QA22 database before execution')
    const config = await req(null, '/config'); assert.equal(config.authMode, 'Development', '/config must already use Development auth')
    out.guard.authMode = config.authMode
    const existingPersonas = await req(null, '/api/dev/users'); assert.ok(Array.isArray(existingPersonas), 'Development picker must list existing active personas')
    for (const [persona, email] of Object.entries(emails)) assert.equal(existingPersonas.filter(value => value.email.toLowerCase() === email.toLowerCase()).length, 1,
      `Root must preseed one active Development ${persona} persona; this runner will not provision an unknown email`)
    out.guard.personasPreexistInDevelopmentPicker = true
    for (const persona of ['pm', 'lead', 'owner']) {
      const current = await req(persona, '/me')
      assert.equal(current.email.toLowerCase(), emails[persona].toLowerCase(), `Root must preseed the existing ${persona} persona`); assert.ok(guid(current.id))
      out.personas[persona] = { id: current.id, email: current.email, displayName: current.displayName }
      if (persona === 'pm') {
        assert.equal(current.flags.register_screens, true, 'Root must enable register_screens before this runner')
        assert.equal(current.capabilities.createProject, true, 'Existing PM must already have create-project authority')
        out.today = date(current.settings.today); out.guard.registerScreens = true
      }
    }
    assert.equal(new Set(Object.values(out.personas).map(row => row.id)).size, 3, 'Use distinct current PM, lead and owner personas')
    const ref = await req('pm', '/reference')
    const active = (values, match, label) => { const choices = (values || []).filter(value => value.isActive && match(value)); assert.ok(choices.length, `Missing active ${label}; root configures vocabulary separately`); return choices.sort((a, b) => a.id.localeCompare(b.id))[0] }
    const civil = active(ref.disciplines, value => value.code === 'CIV' || value.name === 'Civil', 'Civil discipline')
    const client = active(ref.clients, () => true, 'client'), office = active(ref.offices, () => true, 'office')
    const type = active(ref.deliverableTypes, value => value.name === 'Drawing Package', 'Drawing Package deliverable type')
    const review = active(ref.issuePurposes, value => value.name === 'For review', 'For review purpose')
    const construction = active(ref.issuePurposes, value => value.name === 'For construction', 'For construction purpose')
    const b = active(ref.issueReturnCodes, value => value.code === 'B', 'B return code'), c = active(ref.issueReturnCodes, value => value.code === 'C', 'C return code')
    out.reference = { disciplineId: civil.id, clientId: client.id, officeId: office.id, deliverableTypeId: type.id, reviewPurpose: review, constructionPurpose: construction, returnB: b, returnC: c }
    projectNumber = `QA22-DLV-${Date.now().toString(36).toUpperCase()}-${randomUUID().slice(0, 6).toUpperCase()}`
    const project = await req('pm', '/projects', 'POST', { projectNumber, name: `Synthetic QA22 deliverable register ${projectNumber}`, clientId: client.id, officeId: office.id,
      description: 'Disposable synthetic QA22 fixture. No client facts, fees, rates or operational records.', disciplines: [{ disciplineId: civil.id, leadUserId: out.personas.lead.id }],
      members: [{ userId: out.personas.owner.id, roles: ['TeamMember'], disciplineId: civil.id }] }, 201)
    assert.ok(guid(project.id)); projectId = project.id; projectNumber = project.projectNumber
    out.fixture.project = { id: projectId, projectNumber, href: `/projects/${encodeURIComponent(projectNumber)}/deliverables` }
    let currentProject = await req('pm', `/projects/${projectId}`)
    await req('pm', `/projects/${projectId}/transition`, 'POST', { toStatus: 'Active', rowVersion: version(currentProject.rowVersion, 'new project') })
    currentProject = await req('pm', `/projects/${projectId}`); assert.equal(currentProject.status, 'Active')
    const projectCivil = currentProject.disciplines.find(row => row.disciplineId === civil.id && row.isActive); assert.ok(projectCivil)
    out.reference.projectDisciplineId = projectCivil.id
    const pattern = await req('pm', `/projects/${projectId}/deliverable-numbering`); assert.equal(pattern.canEdit, true)
    await req('pm', `/projects/${projectId}/deliverable-numbering`, 'PUT', { pattern: 'C-###', rowVersion: version(pattern.rowVersion, 'project pattern'), reason: null })
    out.facts.numbering = await req('pm', `/projects/${projectId}/deliverable-numbering`); assert.equal(out.facts.numbering.pattern, 'C-###')
    const yesterday = offset(out.today, -1), backdated = offset(out.today, -2), future = offset(out.today, 7)

    const warning = await create('For review and B recipient response', { clientDrawingNumber: 'C12' }), warningId = warning.deliverable.id
    check('C12 is saved with a warning, not a status/issue change', () => {
      assert.equal(warning.deliverable.registerInfo.clientDrawingNumber, 'C12'); assert.equal(warning.deliverable.registerInfo.numberMatchesPattern, false)
      assert.ok(out.fixture.deliverables.find(row => row.id === warningId).creationWarnings.some(value => value.includes('C-###')))
    })
    const first = await issue(warningId, review, 'A', yesterday)
    const returned = await respond(warningId, first.issueId, b, out.today)
    const filteredB = await list({ purposeOfIssueId: review.id, returnCodeId: b.id })
    check('For review + B filter returns the actual responded deliverable', () => assert.deepEqual(ids(filteredB), [warningId]))
    equalCurrent('B response register/current/history equality', filteredB[0], returned)
    out.facts.reviewResponse = { deliverableId: warningId, issueId: first.issueId, status: returned.deliverable.status, currentIssue: returned.deliverable.registerInfo.currentIssue,
      warnings: out.fixture.deliverables.find(row => row.id === warningId).creationWarnings, filter: { purposeOfIssueId: review.id, returnCodeId: b.id }, filteredIds: ids(filteredB) }

    const revision = await create('Backdated reissue and older response isolation', { clientDrawingNumber: 'C-102' }), revisionId = revision.deliverable.id
    const old = await issue(revisionId, review, 'A', yesterday); await respond(revisionId, old.issueId, b, out.today)
    await step(revisionId, 'Revision Required'); await step(revisionId, 'In Progress'); await step(revisionId, 'Ready to Issue')
    const newer = await issue(revisionId, construction, 'B', backdated)
    const corrected = await respond(revisionId, old.issueId, c, out.today, 'Synthetic QA22 correction of an older recipient response transcription')
    check('backdated actual B identity stays current after older A response correction', () => {
      assert.notEqual(newer.issueId, old.issueId); assert.equal(corrected.deliverable.registerInfo.currentIssue.id, newer.issueId)
      assert.equal(corrected.deliverable.registerInfo.currentIssue.purposeOfIssueId, construction.id); assert.equal(corrected.deliverable.registerInfo.currentIssue.returnCodeId, null)
      assert.equal(corrected.issues.find(row => row.id === old.issueId).registerInfo.returnCodeId, c.id); assert.equal(corrected.deliverable.status, 'Issued')
      assert.ok(backdated < yesterday)
    })
    const filteredC = await list({ returnCodeId: c.id }); check('older C response never leaks into the current C filter', () => assert.deepEqual(ids(filteredC), []))
    const currentConstruction = await list({ purposeOfIssueId: construction.id }); assert.equal(currentConstruction.length, 1)
    equalCurrent('backdated current register/history equality', currentConstruction[0], corrected)
    out.facts.backdatedReissue = { deliverableId: revisionId, olderIssueId: old.issueId, currentIssueId: newer.issueId, oldIssuedDate: yesterday, currentIssuedDate: backdated,
      currentIssue: corrected.deliverable.registerInfo.currentIssue, olderResponse: corrected.issues.find(row => row.id === old.issueId).registerInfo, currentCFilterIds: ids(filteredC) }

    const cohort = {}
    for (const [index, state] of ['Not Started', 'In Progress', 'On Hold', 'Revision Required'].entries()) {
      const made = await create(`Undated ${state}`, { clientDrawingNumber: `C-${201 + index}` }), id = made.deliverable.id
      if (state === 'In Progress' || state === 'On Hold') await step(id, state)
      if (state === 'Revision Required') { await issue(id, review, 'A', yesterday); await step(id, state) }
      cohort[state] = id
    }
    for (const state of ['Issued', 'Accepted', 'Cancelled']) {
      const made = await create(`Excluded undated ${state}`), id = made.deliverable.id
      if (state !== 'Cancelled') await issue(id, review, 'A', yesterday)
      if (state !== 'Issued') await step(id, state)
    }
    const dated = await create('Dated open control', { dueDate: future, clientDrawingNumber: 'C-301' })
    const openRows = await list({ indicator: 'open' }), noDue = await list({ noDueDate: true }), expected = Object.values(cohort).sort()
    const tray = openRows.filter(row => !row.dueDate), datedRows = openRows.filter(row => row.dueDate)
    check('exact four open undated statuses equal No due date filter and canonical dates/tray partition', () => {
      assert.deepEqual(ids(noDue), expected); assert.deepEqual(ids(tray), expected); assert.equal(openRows.length, 5)
      assert.deepEqual(noDue.map(row => row.status).sort(), Object.keys(cohort).sort()); assert.ok(noDue.every(row => row.dueDate === null && open(row)))
      assert.deepEqual(ids(datedRows), [dated.deliverable.id]); assert.equal(openRows.length, tray.length + datedRows.length)
    })
    const withRange = await list({ noDueDate: true, dueFrom: out.today }), withTerminal = await list({ noDueDate: true, status: 'Issued' })
    const withCivil = await list({ noDueDate: true, disciplineId: projectCivil.id }), withProgress = await list({ noDueDate: true, status: 'In Progress' })
    check('No due date preserves AND date/status/discipline scope', () => {
      assert.deepEqual(ids(withRange), []); assert.deepEqual(ids(withTerminal), []); assert.deepEqual(ids(withCivil), expected); assert.deepEqual(ids(withProgress), [cohort['In Progress']])
    })
    out.facts.undated = { expectedByStatus: cohort, expectedIds: expected, openIds: ids(openRows), trayIds: ids(tray), noDueFilterIds: ids(noDue), datedIds: ids(datedRows), datedDueDate: future,
      queries: { open: { indicator: 'open' }, noDueDate: { noDueDate: true }, rangeAND: { noDueDate: true, dueFrom: out.today }, terminalAND: { noDueDate: true, status: 'Issued' }, disciplineAND: { noDueDate: true, disciplineId: projectCivil.id }, statusAND: { noDueDate: true, status: 'In Progress' } },
      rangeANDIds: ids(withRange), terminalANDIds: ids(withTerminal), disciplineANDIds: ids(withCivil), statusANDIds: ids(withProgress) }
    const finalRows = await list(); assert.equal(finalRows.length, 10)
    const finalMe = await req('pm', '/me')
    check('root-controlled register flag and organisation date remain unchanged during setup', () => {
      assert.equal(finalMe.flags.register_screens, true); assert.equal(finalMe.settings.today, out.today)
    })
    for (const row of finalRows) Object.assign(out.fixture.deliverables.find(item => item.id === row.id), { status: row.status, dueDate: row.dueDate, rowVersion: row.rowVersion,
      currentIssueId: row.registerInfo.currentIssue?.id || null, registerInfo: row.registerInfo })
    const root = `/projects/${encodeURIComponent(projectNumber)}/deliverables`
    out.links = { register: UI + root, dates: `${UI}${root}?layout=dates`, noDueDate: `${UI}${root}?layout=register&noDueDate=true`,
      reviewB: `${UI}${root}?${query({ purposeOfIssueId: review.id, returnCodeId: b.id })}`, reviewHistory: `${UI}${root}?${query({ panel: `Deliverable:${warningId}`, deliverableTab: `${warningId}:issues` })}`,
      backdatedHistory: `${UI}${root}?${query({ panel: `Deliverable:${revisionId}`, deliverableTab: `${revisionId}:issues` })}`, numbering: `${UI}/projects/${encodeURIComponent(projectNumber)}/settings`,
      rangeAND: `${UI}${root}?${query({ layout: 'dates', noDueDate: true, dueFrom: out.today })}` }
    out.selectors = { dates: { role: 'button', name: 'Dates' }, undatedHeading: { role: 'heading', name: 'No due date (4)' },
      tray: '[data-register-undated] [data-register-date-id]', dated: '[data-register-dated] [data-register-date-id]',
      trayRecordIds: expected, currentHistoryRow: `[data-register-issue-id="${newer.issueId}"]`, oldHistoryRow: `[data-register-issue-id="${old.issueId}"]`,
      returnedHistoryRow: `[data-register-issue-id="${first.issueId}"]`, issuesTab: { role: 'tab', nameStartsWith: 'Issues' },
      recordResponse: { role: 'button', nameStartsWith: 'Record recipient response' }, responseCodeLabel: 'Return code', responseDateLabel: 'Response date',
      numberPatternLabel: 'Numbering pattern', clientNumberLabel: 'Client drawing number', warningText: 'Does not match C-###',
      rangeAction: { role: 'button', name: 'Remove due-date range' }, trayToRegister: { role: 'button', name: 'Show no due date in the register' },
      note: 'deliverableTab restoration requires screen_patterns; with that independent flag off, select the Issues tab explicitly. Links/selectors are prepared, not browser-verified.' }
    out.status = 'PASS_API_SETUP'
  } catch (error) {
    out.status = 'FAIL_OR_UNPROVEN'; out.failures.push({ message: String(error.message || error).slice(0, 1500), lastRequest: out.requests.at(-1) || null }); process.exitCode = 1
  } finally {
    out.finishedAtUtc = new Date().toISOString()
    if (receiptOwned) fs.writeFileSync(RECEIPT, JSON.stringify(out, null, 2) + '\n')
    console.log(JSON.stringify({ status: out.status, receiptOwned, receipt: receiptOwned ? RECEIPT : null, refusedReceipt: receiptOwned ? undefined : RECEIPT,
      project: out.fixture.project, requests: out.requests.length, assertions: out.assertions.length, failures: out.failures }))
  }
}
main().catch(error => { process.exitCode = 1; console.error(`QA22 receipt could not be completed: ${error.message}`) })
