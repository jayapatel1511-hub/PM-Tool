// S14 real API preparation runner. Authoring this file is not execution evidence.
// Root alone configures the frozen final build, QA22 database/API and feature flags.
// Run once: TUESDAY_QA_DATABASE=hub_qa_checkpoint22 node <this-file>
// Fixed loopback API, Development personas, ordinary commands, no flag/auth/clock/schema writes.
// A prior receipt is never overwritten: root must preserve/rename it before a deliberate retry.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { randomBytes } = require('node:crypto')

const API = 'http://127.0.0.1:5088/api/v1'
const UI = 'http://127.0.0.1:8853'
const RECEIPT = path.join(__dirname, 'checkpoint22-real-timeline-setup.json')
const run = Date.now().toString(36) + '-' + randomBytes(3).toString('hex')
const emails = Object.fromEntries(['jordan', 'priya', 'marc', 'alex', 'omar'].map(name => [name, name + '@hub.test']))
const result = {
  kind: 'checkpoint22-real-timeline-api-setup', run, status: 'FAIL_OR_UNPROVEN',
  startedAtUtc: new Date().toISOString(), finishedAtUtc: null,
  target: { api: API, preview: UI, databaseFromRootEnvironment: process.env.TUESDAY_QA_DATABASE || null, databaseIndependentlyVerified: false },
  fixture: { projects: [], tasks: [], views: [] }, partialIds: {}, journal: [], checks: [], failures: [],
  reads: {}, derivedCurrentFacts: {}, links: {}, browser: { status: 'NOT_RUN', screenshots: [], observations: [] },
  limits: [
    'API setup and response joins only; no browser, visual, print, phone, human acceptance or deployment claim.',
    'Database identity is a required root environment assertion, not independently observable through /config.',
    'Root owns flags, services, final-build verification and restoration. This runner never writes settings.',
    'Optional baseline/handoff rendering, inactive discipline and all-off retention require separate root execution.',
    'Fresh synthetic records are intentionally retained in the disposable QA22 database for browser follow-up.'
  ]
}
let receiptOwned = false
let seedPersonasVerified = false

function persist() {
  if (!receiptOwned) return
  const temporary = RECEIPT + '.tmp-' + run
  fs.writeFileSync(temporary, JSON.stringify(result, null, 2) + '\n', { mode: 0o600 })
  fs.renameSync(temporary, RECEIPT)
}
function pass(name, facts) { result.checks.push({ name, status: 'PASS', facts }); persist() }
function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical).sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b)))
  if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map(key => [key, canonical(value[key])]))
  return value
}
function same(actual, expected, message) { assert.deepEqual(canonical(actual), canonical(expected), message) }
function set(value) { return String(value).split(',').filter(Boolean).sort() }
function row(list, id) {
  assert.ok(Array.isArray(list.views), 'saved-view response must expose views')
  const found = list.views.find(item => item.id === id)
  assert.ok(found, 'saved view ' + id + ' must exist')
  return found
}
function offset(today, days) {
  assert.match(today, /^\d{4}-\d{2}-\d{2}$/, 'organisation today must be an ISO calendar date')
  const date = new Date(today + 'T12:00:00Z')
  assert.equal(date.toISOString().slice(0, 10), today, 'organisation today must be a real calendar date')
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}
function uiLink(route, params) {
  const query = new URLSearchParams(params)
  return UI + route + (query.size ? '?' + query : '')
}

async function api(name, who, route, method = 'GET', body, expected = 200) {
  const anonymous = who === null
  if (anonymous) {
    assert.ok(route === '/config' || route === '/api/dev/users', 'only configuration and the Development picker may be anonymous')
    assert.equal(method, 'GET'); assert.equal(body, undefined)
  } else {
    assert.ok(emails[who], 'only the known synthetic Development personas are allowed')
    assert.ok(seedPersonasVerified, 'verify every active seed email anonymously before sending any identity header')
  }
  assert.ok(route.startsWith('/') && !route.startsWith('//') && !route.includes('#'), 'API route must be relative')
  const devPicker = route === '/api/dev/users'
  assert.ok(!devPicker || anonymous, 'the Development picker must be read without identity headers')
  const destination = devPicker ? new URL('/api/dev/users', API) : new URL(API + route)
  assert.equal(destination.origin, 'http://127.0.0.1:5088', 'fixed disposable loopback API only')
  assert.ok(destination.pathname.startsWith('/api/v1/') || devPicker, 'only the maintained API and Development picker are allowed')
  assert.ok(['GET', 'POST', 'PATCH', 'DELETE'].includes(method), 'unsupported command method')
  if (method !== 'GET') {
    const allowed = method === 'POST'
      ? /^\/(projects|workspaces|views)$/.test(route)
        || /^\/projects\/[0-9a-f-]+\/(transition|tasks|milestones)$/.test(route)
        || /^\/tasks\/[0-9a-f-]+\/dependencies$/.test(route)
      : /^\/views\/[0-9a-f-]+$/.test(route)
    assert.ok(allowed, 'runner refuses writes outside the bounded fixture/view commands')
  }
  const entry = { name, actor: anonymous ? 'anonymous' : emails[who], method, route, expectedStatus: expected, status: null }
  if (body !== undefined) entry.requestBody = body
  result.journal.push(entry)
  persist()
  const response = await fetch(destination, {
    method, redirect: 'error', signal: AbortSignal.timeout(15000),
    headers: { ...(anonymous ? {} : { 'X-Dev-User': emails[who] }), ...(body === undefined ? {} : { 'content-type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body)
  })
  entry.status = response.status
  const text = await response.text()
  let value
  try { value = text ? JSON.parse(text) : null } catch { throw new Error(name + ': response was not JSON') }
  if (value && typeof value === 'object') {
    entry.answer = Object.fromEntries(['id', 'key', 'rowVersion', 'code', 'errors', 'warnings'].filter(key => value[key] !== undefined).map(key => [key, value[key]]))
    if (method === 'POST' && value.id) result.partialIds[name] = value.id
  }
  persist()
  assert.equal(response.status, expected, name + ': HTTP status')
  return value
}

async function createProject(suffix, twoDisciplines, refs, actors, dates) {
  const body = {
    projectNumber: 'QA22-TL-' + run + '-' + suffix, name: 'QA22 synthetic Timeline ' + run + ' ' + suffix,
    clientId: refs.client.id, officeId: refs.office.id, startDate: dates.from, targetCompletionDate: dates.to,
    disciplines: [{ disciplineId: refs.civil.id, leadUserId: twoDisciplines ? actors.marc.id : actors.priya.id }],
    members: twoDisciplines ? [{ userId: actors.alex.id, roles: ['TeamMember'], disciplineId: refs.civil.id }] : []
  }
  if (twoDisciplines) body.disciplines.push({ disciplineId: refs.other.id, leadUserId: actors.omar.id })
  const created = await api('project-' + suffix + '-create', 'priya', '/projects', 'POST', body, 201)
  const saved = { id: created.id, projectNumber: created.projectNumber, name: body.name }
  result.fixture.projects.push(saved); persist()
  let detail = await api('project-' + suffix + '-detail', 'priya', '/projects/' + created.id)
  assert.equal(detail.projectManagerId, actors.priya.id, 'normal create assigns Priya as PM')
  if (detail.status !== 'Active') {
    await api('project-' + suffix + '-activate', 'priya', '/projects/' + created.id + '/transition', 'POST', { toStatus: 'Active', rowVersion: detail.rowVersion })
    detail = await api('project-' + suffix + '-active-detail', 'priya', '/projects/' + created.id)
  }
  assert.equal(detail.status, 'Active')
  saved.civil = detail.disciplines.find(item => item.disciplineId === refs.civil.id)?.id
  saved.other = twoDisciplines ? detail.disciplines.find(item => item.disciplineId === refs.other.id)?.id : null
  assert.ok(saved.civil && (!twoDisciplines || saved.other), 'resolve actual project-discipline IDs')
  assert.notEqual(saved.civil, refs.civil.id, 'global and project discipline IDs are distinct')
  result.reads['project' + suffix] = detail
  persist()
  return saved
}

async function createTask(project, suffix, discipline, assignee, startDate, dueDate) {
  const body = { name: 'QA22 ' + run + ' ' + suffix, projectDisciplineId: discipline, assigneeId: assignee, requiresReview: false, startDate, dueDate }
  const created = await api('task-' + suffix, 'priya', '/projects/' + project.id + '/tasks', 'POST', body, 201)
  const task = { id: created.id, key: created.key, rowVersion: created.rowVersion, projectId: project.id, ...body }
  result.fixture.tasks.push(task); persist()
  return task
}

async function saveView(label, body) {
  const created = await api(label, 'priya', '/views', 'POST', body, 201)
  result.fixture.views.push({ label, id: created.id, rowVersion: created.rowVersion, ...body })
  persist()
  return created
}

async function snapshot(project, who, label) {
  const value = await api(label, who, '/projects/' + project.id + '/timeline')
  return recordTimelineRead(label, value)
}
function pickFields(value, keys) {
  return Object.fromEntries(keys.filter(key => value[key] !== undefined).map(key => [key, value[key]]))
}
function derivedFacts(timeline) {
  const fields = {
    tasks: ['id', 'isOverdue', 'slipShown', 'affectedMilestoneIds', 'lane'],
    deliverables: ['id', 'progressPct', 'isOverdue', 'slipShown', 'baselineSlipShown'],
    milestones: ['id', 'status', 'slipShown', 'baselineSlipShown'],
    dependencies: ['id', 'highlighted'],
    deliverableLinks: ['from', 'to', 'highlighted'],
    handoffs: ['id', 'dateMismatch', 'isOverdue', 'daysOverdue']
  }
  const facts = Object.fromEntries(Object.entries(fields).filter(([key]) => Array.isArray(timeline[key]))
    .map(([key, keys]) => [key, timeline[key].map(value => pickFields(value, keys))]))
  if (timeline.latestPlanned !== undefined) facts.latestPlanned = timeline.latestPlanned
  return facts
}
function recordTimelineRead(label, value) {
  result.reads[label] = value
  // Retain the actual evaluator/presentation answers independently, including before a later join can fail.
  result.derivedCurrentFacts[label] = Array.isArray(value.projects)
    ? { projects: value.projects.map(group => ({ projectId: group.project.id, facts: derivedFacts(group.data) })) }
    : derivedFacts(value)
  persist()
  return value
}
function sourceFacts(timeline) {
  // Timeline.Build reads task status/progress from Tasks, and deliverable status from Deliverables.
  // Deliverable progress comes from DeliverableStates; milestone status comes from MilestoneStates.
  // EvaluationWorker may legitimately refresh the latter while these ordinary reads run.
  const fields = {
    tasks: ['id', 'key', 'name', 'projectDisciplineId', 'deliverableId', 'milestoneId', 'startDate', 'dueDate',
      'originalStartDate', 'originalDueDate', 'status', 'progressPct', 'assignee', 'rowVersion', 'createdOn', 'canEditDates', 'dueNeedsReason'],
    deliverables: ['id', 'key', 'name', 'startDate', 'dueDate', 'originalStartDate', 'originalDueDate', 'status',
      'rowVersion', 'createdOn', 'canEditDates', 'baselineStartDate', 'baselineDueDate'],
    milestones: ['id', 'key', 'name', 'date', 'isComplete', 'originalDate', 'baselineDate'],
    dependencies: ['id', 'predecessorTaskId', 'successorTaskId', 'type', 'lagDays'],
    // The derived marker identifies task-derived graph topology, not the evaluator's highlighted state.
    deliverableLinks: ['from', 'to', 'type', 'lagDays', 'derived'],
    handoffs: ['id', 'key', 'title', 'status', 'sendingDisciplineId', 'receivingDisciplineId', 'neededBy', 'promisedBy']
  }
  return {
    permissions: timeline.permissions,
    ...Object.fromEntries(Object.entries(fields).map(([key, keys]) => [key, (timeline[key] || []).map(value => pickFields(value, keys))]))
  }
}

async function main() {
  assert.ok(!fs.existsSync(RECEIPT), 'prior receipt exists; root must preserve/rename it before retrying')
  fs.writeFileSync(RECEIPT, JSON.stringify(result, null, 2) + '\n', { flag: 'wx', mode: 0o600 })
  receiptOwned = true
  assert.equal(process.env.TUESDAY_QA_DATABASE, 'hub_qa_checkpoint22', 'refuse any database environment other than disposable QA22')
  const config = await api('config', null, '/config')
  result.reads.config = { authMode: config.authMode }
  assert.equal(config.authMode, 'Development', 'refuse non-Development authentication')
  // This endpoint lists only active, existing users and does not invoke Development provisioning.
  // Check the entire cohort before the first X-Dev-User request, not one persona at a time.
  const existingPersonas = await api('existing-active-seed-personas', null, '/api/dev/users')
  assert.ok(Array.isArray(existingPersonas), 'anonymous Development picker must list existing active personas')
  result.reads.existingActiveSeedPersonas = {}
  for (const [who, email] of Object.entries(emails)) {
    const matches = existingPersonas.filter(person => typeof person.email === 'string' && person.email.toLowerCase() === email)
    assert.equal(matches.length, 1, 'root must preseed exactly one active Development persona: ' + who)
    result.reads.existingActiveSeedPersonas[who] = { email: matches[0].email, displayName: matches[0].displayName }
  }
  seedPersonasVerified = true
  result.target.personasPreexistInDevelopmentPicker = true
  persist()

  const actors = {}
  for (const who of Object.keys(emails)) {
    const me = await api('identity-' + who, who, '/me')
    assert.equal(me.email.toLowerCase(), emails[who])
    assert.ok(me.id && me.capabilities && me.settings && me.flags, 'current identity contract')
    assert.equal(me.flags.work_screens, true, 'root must enable work_screens before this runner')
    actors[who] = { id: me.id, email: me.email, name: me.displayName, capabilities: me.capabilities }
    result.reads['me-' + who] = { ...actors[who], roles: me.roles, systemRoles: me.systemRoles, settings: me.settings, flags: me.flags }
  }
  assert.equal(actors.jordan.capabilities.admin, true, 'Jordan supplies only the normal admin reference read')
  assert.equal(actors.priya.capabilities.createProject, true)
  for (const who of ['priya', 'marc', 'alex']) assert.equal(actors[who].capabilities.readOnly, false)
  const users = await api('seed-users', 'jordan', '/admin/users')
  assert.ok(Array.isArray(users), 'admin users must be an array')
  for (const [who, actor] of Object.entries(actors)) {
    const user = users.find(person => person.email?.toLowerCase() === emails[who])
    assert.ok(user && user.isActive, 'required synthetic seed persona is active: ' + who)
    assert.equal(user.id, actor.id)
  }
  result.fixture.actors = actors
  const reference = await api('reference', 'priya', '/reference')
  const refs = {
    civil: reference.disciplines?.find(item => item.isActive && item.code === 'CIV'),
    other: reference.disciplines?.find(item => item.isActive && item.code !== 'CIV'),
    client: reference.clients?.find(item => item.isActive), office: reference.offices?.find(item => item.isActive)
  }
  for (const [key, value] of Object.entries(refs)) assert.ok(value?.id, 'active reference required: ' + key)
  result.fixture.reference = refs
  const settings = result.reads['me-priya'].settings, today = settings.today
  const dates = { today, orgTimeZone: settings.orgTimeZone, dateFormat: settings.dateFormat,
    a: offset(today, -7), b: offset(today, 2), c: offset(today, 3), d: offset(today, 12), from: offset(today, -14), to: offset(today, 42) }
  result.fixture.dates = dates
  pass('Development identities, work_screens and fresh reference prerequisites', { actors, dates, references: refs })

  const p1 = await createProject('A', true, refs, actors, dates)
  const predecessor = await createTask(p1, 'Civil predecessor', p1.civil, actors.alex.id, dates.a, dates.b)
  const successor = await createTask(p1, 'Civil successor', p1.civil, actors.alex.id, dates.c, dates.d)
  const other = await createTask(p1, 'Other discipline', p1.other, actors.omar.id, dates.a, dates.d)
  const edge = await api('dependency', 'priya', '/tasks/' + successor.id + '/dependencies', 'POST',
    { predecessorTaskId: predecessor.id, type: 'FinishToStart', lagDays: 0, note: 'QA22 synthetic Timeline dependency' }, 201)
  result.fixture.dependency = { id: edge.id, predecessorTaskId: predecessor.id, successorTaskId: successor.id, type: 'FinishToStart', lagDays: 0 }
  const milestone = await api('milestone', 'priya', '/projects/' + p1.id + '/milestones', 'POST',
    { name: 'QA22 ' + run + ' Timeline milestone', milestoneType: 'Design Submission', date: dates.d, projectDisciplineId: p1.civil, isClientFacing: true }, 201)
  result.fixture.milestone = milestone
  const p2 = await createProject('B', false, refs, actors, dates)
  await createTask(p2, 'Second project Civil work', p2.civil, actors.priya.id, dates.a, dates.d)
  const workspace = await api('workspace', 'priya', '/workspaces', 'POST', { name: 'QA22 ' + run + ' Timeline scope', projectIds: [p1.id, p2.id] }, 201)
  result.fixture.workspace = workspace
  same(workspace.projectIds, [p1.id, p2.id], 'named workspace retains the exact explicit fixture scope')
  const marcWorkspaces = await api('marc-workspaces', 'marc', '/workspaces')
  assert.ok(Array.isArray(marcWorkspaces))
  assert.ok(!marcWorkspaces.some(item => item.id === workspace.id), 'Priya named workspace is not Marc workspace')
  persist()

  const beforePriya = await snapshot(p1, 'priya', 'canonical-before-priya')
  const beforeMarc = await snapshot(p1, 'marc', 'canonical-before-marc')
  for (const timeline of [beforePriya, beforeMarc]) {
    same(timeline.tasks.map(item => item.id), [predecessor.id, successor.id, other.id], 'canonical Timeline retains all three source tasks')
    for (const task of [predecessor, successor, other]) {
      const actual = timeline.tasks.find(item => item.id === task.id)
      assert.equal(actual.projectDisciplineId, task.projectDisciplineId)
      assert.equal(actual.startDate, task.startDate); assert.equal(actual.dueDate, task.dueDate)
    }
    const dependency = timeline.dependencies.find(item => item.id === edge.id)
    assert.ok(dependency, 'canonical Timeline exposes the created dependency ID')
    same({ id: dependency.id, predecessorTaskId: dependency.predecessorTaskId, successorTaskId: dependency.successorTaskId, type: dependency.type, lagDays: dependency.lagDays }, result.fixture.dependency)
    assert.equal(timeline.milestones.find(item => item.id === milestone.id)?.date, dates.d)
  }
  pass('canonical task, discipline, dependency and milestone ID joins', { projectId: p1.id, taskIds: [predecessor.id, successor.id, other.id], dependency: result.fixture.dependency, milestoneId: milestone.id })

  const common = { zoom: 'month', group: 'discipline', cols: 'items,dates', layers: 'dependencies,milestones',
    hideCompleted: 'false', collapsed: 'none', expanded: 'loose:' + p1.civil }
  const projectParams = { ...common, disciplineId: p1.civil, from: dates.from, to: dates.to }
  const projectViews = '/views?listType=timeline&projectId=' + p1.id
  const sharedName = 'QA22 ' + run + ' Civil Month dependencies'
  const shared = await saveView('shared-view', { name: sharedName, listType: 'timeline', projectId: p1.id, scope: 'Project', params: projectParams, isDefault: false })
  for (const who of ['priya', 'marc', 'alex']) {
    const list = await api('shared-list-' + who, who, projectViews)
    result.reads['shared-list-' + who] = list
    const view = row(list, shared.id)
    assert.equal(view.scope, 'Project'); assert.equal(view.params.disciplineId, p1.civil); assert.equal(view.params.zoom, 'month')
    assert.equal(view.params.group, 'discipline'); assert.equal(view.params.cols, 'items,dates')
    assert.equal(view.params.expanded, 'loose:' + p1.civil); assert.equal(view.params.collapsed, 'none')
    assert.equal(view.params.from, dates.from); assert.equal(view.params.to, dates.to)
    same(set(view.params.layers), set(projectParams.layers))
    assert.equal(view.canEdit, who !== 'alex'); assert.equal(list.canShare, who !== 'alex')
    assert.ok(!view.dropped.includes('disciplineId'))
  }
  pass('Priya shared Civil/Month/dependency definition visible to Marc and Alex with authoritative edit rights', { viewId: shared.id, params: projectParams })

  const workspaceViews = '/views?listType=workspace-timeline'
  const workspaceParams = { projects: [p1.id, p2.id].join(','), zoom: 'quarter', start: dates.from, group: 'project', cols: 'items,dates',
    layers: 'dependencies,originalDates', hideCompleted: 'false', collapsed: 'none' }
  const explicitName = 'QA22 ' + run + ' private two-project default'
  const explicit = await saveView('workspace-explicit-default', { name: explicitName, listType: 'workspace-timeline', scope: 'Personal', params: workspaceParams, isDefault: true })
  const mineParams = { ...workspaceParams, projects: 'mine', zoom: 'week' }
  const mine = await saveView('workspace-mine', { name: 'QA22 ' + run + ' private My projects', listType: 'workspace-timeline', scope: 'Personal', params: mineParams, isDefault: false })
  const namedParams = { ...workspaceParams, ws: workspace.id }; delete namedParams.projects
  const named = await saveView('workspace-named', { name: 'QA22 ' + run + ' private named scope', listType: 'workspace-timeline', scope: 'Personal', params: namedParams, isDefault: false })
  const privateList = await api('workspace-list-priya', 'priya', workspaceViews)
  const marcPrivateList = await api('workspace-list-marc', 'marc', workspaceViews)
  result.reads['workspace-list-priya'] = privateList; result.reads['workspace-list-marc'] = marcPrivateList
  assert.equal(privateList.canShare, false); assert.equal(marcPrivateList.canShare, false)
  for (const viewId of [explicit.id, mine.id, named.id]) {
    assert.equal(row(privateList, viewId).scope, 'Personal')
    assert.ok(!marcPrivateList.views.some(item => item.id === viewId), 'other user cannot read Priya private definition')
  }
  const explicitRow = row(privateList, explicit.id)
  assert.equal(explicitRow.isDefault, true)
  same(privateList.views.filter(item => item.isDefault).map(item => item.id), [explicit.id], 'one workspace Timeline default')
  same(set(explicitRow.params.projects), [p1.id, p2.id])
  for (const key of ['zoom', 'start', 'group', 'cols', 'hideCompleted', 'collapsed']) assert.equal(explicitRow.params[key], workspaceParams[key])
  same(set(explicitRow.params.layers), set(workspaceParams.layers))
  assert.equal(row(privateList, mine.id).params.projects, 'mine'); assert.equal(row(privateList, mine.id).params.zoom, 'week')
  assert.equal(row(privateList, named.id).params.ws, workspace.id); assert.equal(row(privateList, named.id).params.projects, undefined)

  const explicitTimeline = recordTimelineRead('workspace-canonical-explicit',
    await api('workspace-canonical-explicit', 'priya', '/timeline?projects=' + encodeURIComponent(workspaceParams.projects)))
  assert.equal(explicitTimeline.truncated, false, 'two-project fixture must not be truncated')
  same(explicitTimeline.projects.map(item => item.project.id), [p1.id, p2.id])
  same(sourceFacts(explicitTimeline.projects.find(item => item.project.id === p1.id).data), sourceFacts(beforePriya))
  const mineTimeline = recordTimelineRead('workspace-canonical-mine', await api('workspace-canonical-mine', 'priya', '/timeline?projects=mine'))
  for (const project of [p1, p2]) assert.ok(mineTimeline.projects.some(item => item.project.id === project.id), 'actual mine scope includes Priya managed fixture project')
  pass('private explicit/mine/named/default definitions and canonical workspace joins', {
    explicitViewId: explicit.id, mineViewId: mine.id, namedViewId: named.id, workspaceId: workspace.id,
    explicitProjectIds: explicitTimeline.projects.map(item => item.project.id), mineProjectIds: mineTimeline.projects.map(item => item.project.id), mineTruncated: mineTimeline.truncated
  })

  await api('member-cannot-share', 'alex', '/views', 'POST', { name: 'QA22 ' + run + ' forbidden member share', listType: 'timeline', projectId: p1.id, scope: 'Project', params: projectParams }, 403)
  await api('member-cannot-edit-shared', 'alex', '/views/' + shared.id, 'PATCH', { name: 'Forbidden edit', rowVersion: shared.rowVersion }, 403)
  const changed = await api('lead-edit-shared', 'marc', '/views/' + shared.id, 'PATCH', { params: { ...projectParams, zoom: 'week', layers: 'none' }, rowVersion: shared.rowVersion })
  assert.notEqual(changed.rowVersion, shared.rowVersion, 'real edited view advances version')
  await api('stale-shared-edit', 'priya', '/views/' + shared.id, 'PATCH', { name: 'Stale edit', rowVersion: shared.rowVersion }, 409)
  const afterLead = row(await api('shared-after-stale', 'priya', projectViews), shared.id)
  assert.equal(afterLead.params.zoom, 'week'); assert.equal(afterLead.params.layers, 'none'); assert.equal(afterLead.name, sharedName)
  const restored = await api('restore-shared-view', 'priya', '/views/' + shared.id, 'PATCH', { params: projectParams, rowVersion: afterLead.rowVersion })
  result.fixture.views.find(item => item.id === shared.id).rowVersion = restored.rowVersion
  await api('foreign-private-patch', 'marc', '/views/' + explicit.id, 'PATCH', { name: 'Forbidden private edit', rowVersion: explicit.rowVersion }, 404)
  await api('foreign-private-delete', 'marc', '/views/' + explicit.id, 'DELETE', undefined, 404)
  await api('workspace-cannot-share', 'priya', '/views', 'POST', { name: 'QA22 ' + run + ' invalid workspace sharing', listType: 'workspace-timeline', scope: 'Project', params: workspaceParams }, 400)
  await api('project-id-required', 'priya', '/views', 'POST', { name: 'QA22 ' + run + ' missing project', listType: 'timeline', scope: 'Personal', params: projectParams }, 400)
  await api('invalid-known-zoom', 'priya', '/views', 'POST', { name: 'QA22 ' + run + ' invalid zoom', listType: 'timeline', projectId: p1.id, params: { ...projectParams, zoom: 'year' } }, 400)
  await api('invalid-calendar-date', 'priya', '/views', 'POST', { name: 'QA22 ' + run + ' invalid date', listType: 'timeline', projectId: p1.id, params: { ...projectParams, from: today.slice(0, 4) + '-02-30' } }, 400)
  const finalProjectViews = await api('final-project-views', 'priya', projectViews)
  const finalPrivateViews = await api('final-workspace-views', 'priya', workspaceViews)
  const finalShared = row(finalProjectViews, shared.id)
  same(finalShared.params, projectParams, 'shared definition restored using the latest normal version')
  assert.equal(finalShared.name, sharedName)
  same(finalProjectViews.views.map(item => item.id), [shared.id], 'refused project commands created no view')
  same(finalPrivateViews, privateList, 'refused private/workspace commands changed no definition')
  pass('403 member, 404 private ownership, 409 stale version and 400 validation refusals', {
    sharedViewId: shared.id, restoredVersion: restored.rowVersion, refusedCommands: result.journal.filter(item => item.expectedStatus >= 400).map(item => ({ name: item.name, status: item.status }))
  })

  for (const [who, before] of [['priya', beforePriya], ['marc', beforeMarc]]) {
    const after = await snapshot(p1, who, 'canonical-after-' + who)
    same(sourceFacts(after), sourceFacts(before), 'view operations preserve canonical Timeline source IDs/dates/statuses/versions for ' + who)
  }
  const afterWorkspace = recordTimelineRead('workspace-canonical-after',
    await api('workspace-canonical-after', 'priya', '/timeline?projects=' + encodeURIComponent(workspaceParams.projects)))
  same(afterWorkspace.projects.map(item => item.project.id), explicitTimeline.projects.map(item => item.project.id))
  for (const project of explicitTimeline.projects) same(sourceFacts(afterWorkspace.projects.find(item => item.project.id === project.project.id).data), sourceFacts(project.data))
  const finalMe = await api('final-me-priya', 'priya', '/me')
  same(finalMe.flags, result.reads['me-priya'].flags, 'root flag profile remained stable throughout this runner')
  result.reads['final-me-priya'] = { id: finalMe.id, flags: finalMe.flags, settings: finalMe.settings }
  pass('saved-view commands preserve canonical project/workspace source facts', {
    projectIds: [p1.id, p2.id],
    sourceFieldsCompared: 'IDs, graph topology, source dates, persisted task/deliverable/handoff statuses, task progress, source versions, milestone completion and same-actor rights',
    derivedFactsRecordedSeparately: 'milestone health status, deliverable progress, overdue/highlighted/affected-chain state, lane/slip presentation and latestPlanned'
  })

  result.links = {
    projectPriyaAndMarc: uiLink('/projects/' + p1.projectNumber + '/timeline', { view: shared.id, ...projectParams }),
    bareWorkspaceDefault: UI + '/timeline',
    explicitWorkspace: uiLink('/timeline', { view: explicit.id, ...workspaceParams }),
    explicitMyProjectsBeatsDefault: uiLink('/timeline', { projects: 'mine', zoom: 'week' }),
    namedWorkspace: uiLink('/timeline', { view: named.id, ...namedParams }),
    projectCanonicalApi: API + '/projects/' + p1.id + '/timeline',
    projectViewsApi: API + projectViews, workspaceViewsApi: API + workspaceViews
  }
  result.followUp = {
    status: 'BROWSER_AND_FLAGS_NOT_RUN',
    sharedViewName: sharedName, privateDefaultName: explicitName,
    expectedCivilTaskIds: [predecessor.id, successor.id], excludedRenderedTaskId: other.id,
    expansion: 'loose:' + p1.civil, dependencyId: edge.id, milestoneId: milestone.id,
    instruction: 'Root uses actual actor contexts and these links for real UI joins, panel return, print, phone containment, layer masks and later flag/off-restoration checks.'
  }
  result.status = 'PASS_API_SETUP_AND_JOINS'
}

main().catch(error => {
  result.status = 'FAIL_OR_UNPROVEN'
  result.failures.push({ message: error.message, lastRequest: result.journal.at(-1)?.name || null })
  process.exitCode = 1
  console.error('QA22 Timeline setup FAIL_OR_UNPROVEN: ' + error.message)
}).finally(() => {
  result.finishedAtUtc = new Date().toISOString()
  persist()
  if (receiptOwned) console.log(JSON.stringify({ status: result.status, receipt: RECEIPT, run, browser: 'NOT_RUN', checks: result.checks.length, failures: result.failures.length }))
})
