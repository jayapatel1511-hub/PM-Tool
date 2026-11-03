// Operational record-context coverage for the post-052/053 registers and meeting actions.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, panelIds, links, comments } = require('./platform-fixtures.cjs')
const { riskRow, riskDetail, issueRow, issueDetail } = require('./registers.fixture.cjs')
const { meetingRow, actionRow, actionDetail } = require('./meetings.fixture.cjs')
const { taskDetail, taskRow } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const alex = me.id
const marc = '00000000-0000-0000-0000-000000000013'
const diane = '00000000-0000-0000-0000-000000000007'
const inputTask = '00000000-0000-0000-0000-000000000012'
const civilProjectDiscipline = project.disciplines[0]
const person = (id, name, state = 'Active', kind = 'User') => ({ id, name, state, kind })
const actionId = panelIds.action
const riskId = panelIds.risk
const issueId = panelIds.issue
const meetingId = '00000000-0000-0000-0000-000000000031'

const context = (overrides = {}) => ({
  owner: person(alex, 'Alex Chen'), nextActors: [person(alex, 'Alex Chen')], neededBy: '2026-11-07',
  nextAction: 'complete', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], reviewPackageGate: null, inputs: [],
  ...overrides,
})

const risk = riskRow({ id: riskId, projectId: project.id, key: 'R-001', title: 'Operational risk', ownerId: marc, ownerName: 'Marc Lead', reviewDate: '2026-11-04', status: 'Open' })
const issue = issueRow({ id: issueId, projectId: project.id, key: 'I-001', title: 'Operational issue', ownerId: marc, ownerName: 'Marc Lead', targetResolutionDate: '2026-11-06', status: 'Open' })
const meeting = meetingRow({ id: meetingId, projectId: project.id, title: 'Operational meeting' })
const actionBase = actionRow({ id: actionId, projectId: project.id, meetingId, key: 'A-001', text: 'Operational action', ownerType: 'User', ownerUserId: alex, ownerName: 'Alex Chen', dueDate: '2026-11-07' })
const input = { kind: 'Task', id: inputTask, key: 'P-MOCK-T002', title: 'Survey input', neededBy: null, nextActors: [], nextAction: 'review', primary: true }

let riskTerminal = false
let issueTerminal = false
let actionMode = 'user'
const actionForMode = () => {
  if (actionMode === 'discipline') return actionRow({ ...actionBase, ownerType: 'Discipline', ownerTypeLabel: 'Discipline', ownerUserId: null, ownerDisciplineId: civilProjectDiscipline.id, ownerName: 'Civil', leadId: marc, leadName: 'Marc Lead', noLead: false })
  if (actionMode === 'missing-lead') return actionRow({ ...actionBase, ownerType: 'Discipline', ownerTypeLabel: 'Discipline', ownerUserId: null, ownerDisciplineId: civilProjectDiscipline.id, ownerName: 'Civil', leadId: null, leadName: null, noLead: true })
  if (actionMode === 'inactive-lead') return actionRow({ ...actionBase, ownerType: 'Discipline', ownerTypeLabel: 'Discipline', ownerUserId: null, ownerDisciplineId: civilProjectDiscipline.id, ownerName: 'Civil', leadId: marc, leadName: 'Inactive lead', noLead: false })
  if (actionMode === 'external') return actionRow({ ...actionBase, ownerType: 'External Party', ownerTypeLabel: 'External party', ownerUserId: null, ownerDisciplineId: null, ownerExternalPartyId: '00000000-0000-0000-0000-000000000088', ownerName: 'Vendor', partyIsClient: false })
  if (actionMode === 'converted') return actionRow({ ...actionBase, ownerType: 'Discipline', ownerTypeLabel: 'Discipline', ownerUserId: null, ownerDisciplineId: civilProjectDiscipline.id, ownerName: 'Civil', leadId: diane, leadName: 'Diane Review', converted: true, relatedTaskId: inputTask, taskKey: input.key })
  return actionBase
}
const actionContext = () => {
  if (actionMode === 'discipline') return context({ owner: { id: civilProjectDiscipline.id, name: 'Civil', kind: 'Discipline', state: 'Active' }, nextActors: [person(marc, 'Marc Lead')], actorState: 'Active' })
  if (actionMode === 'missing-lead') return context({ owner: { id: civilProjectDiscipline.id, name: 'Civil', kind: 'Discipline', state: 'Active' }, nextActors: [], actorState: 'Unassigned' })
  if (actionMode === 'inactive-lead') return context({ owner: { id: civilProjectDiscipline.id, name: 'Civil', kind: 'Discipline', state: 'Active' }, nextActors: [person(marc, 'Inactive lead', 'Inactive')], actorState: 'Inactive' })
  if (actionMode === 'external') return context({ owner: person('00000000-0000-0000-0000-000000000088', 'Vendor', 'Inactive', 'ExternalParty'), nextActors: [person('00000000-0000-0000-0000-000000000088', 'Vendor', 'Inactive', 'ExternalParty')], actorState: 'Inactive' })
  if (actionMode === 'converted') return context({ owner: { id: civilProjectDiscipline.id, name: 'Civil', kind: 'Discipline', state: 'Active' }, nextActors: [person(diane, 'Diane Review')], neededBy: actionBase.dueDate, nextAction: 'follow_linked_task', inputs: [input] })
  return context({ owner: person(alex, 'Alex Chen'), nextActors: [person(alex, 'Alex Chen')] })
}

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error' && !/server responded with a status of 422/.test(message.text())) errors.push(new Error(message.text())) })
  try {
    page.setDefaultTimeout(12000)
    await page.route(`**/api/v1/projects/${project.id}/risks**`, route => route.fulfill({ json: [risk] }))
    await page.route(`**/api/v1/projects/${project.id}/issues**`, route => route.fulfill({ json: [issue] }))
    await page.route(`**/api/v1/projects/${project.id}/meetings**`, route => route.fulfill({ json: [meeting] }))
    await page.route(`**/api/v1/projects/${project.id}/actions**`, route => route.fulfill({ json: [actionForMode()] }))
    await page.route(`**/api/v1/tasks/${inputTask}`, route => route.fulfill({ json: taskDetail({ task: taskRow({ ...task, id: inputTask, key: input.key, name: input.title, status: 'In Review', reviewerId: diane }) }) }))
    await page.route(`**/api/v1/tasks/${inputTask}/dependencies`, route => route.fulfill({ json: taskDependencies() }))
    await page.route(`**/api/v1/items/Task/${inputTask}/links`, route => route.fulfill({ json: links }))
    await page.route(`**/api/v1/items/Task/${inputTask}/comments`, route => route.fulfill({ json: comments }))
    await page.route(`**/api/v1/risks/${riskId}`, route => route.fulfill({ json: riskDetail({ risk: { ...risk, status: riskTerminal ? 'Realised' : 'Open' }, context: context({ owner: person(marc, 'Marc Lead'), nextActors: riskTerminal ? [] : [person(marc, 'Marc Lead')], terminal: riskTerminal, actorState: riskTerminal ? 'None' : 'Active', nextAction: riskTerminal ? 'none' : 'review', neededBy: risk.reviewDate }) }) }))
    await page.route(`**/api/v1/issues/${issueId}`, route => route.fulfill({ json: issueDetail({ issue: { ...issue, status: issueTerminal ? 'Resolved' : 'Open', resolvedDate: issueTerminal ? '2026-11-08' : null }, context: context({ owner: person(marc, 'Marc Lead'), nextActors: issueTerminal ? [] : [person(marc, 'Marc Lead')], terminal: issueTerminal, actorState: issueTerminal ? 'None' : 'Active', nextAction: issueTerminal ? 'none' : 'review_resolve_complete', neededBy: issue.targetResolutionDate }) }) }))
    await page.route(`**/api/v1/actions/${actionId}`, route => route.fulfill({ json: actionDetail({ action: actionForMode(), context: actionContext(), task: actionMode === 'converted' ? { id: inputTask, key: input.key, name: input.title, status: 'In Review' } : null, project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status }, permissions: { edit: { ok: false, reason: null }, transitions: [], convert: false, comment: true } }) }))

    me.flags = { ...me.flags, screen_patterns: false }
    for (const [kind, id, title, route] of [['Risk', riskId, risk.title, 'risks'], ['Issue', issueId, issue.title, 'issues'], ['Action', actionId, actionBase.text, 'meetings']]) {
      await page.goto(`${base}/projects/${project.projectNumber}/${route}?q=keep&status=Open&panel=${kind}:${id}`, { waitUntil: 'domcontentloaded' })
      const dialog = page.getByRole('dialog'); await dialog.getByRole('heading', { name: title }).waitFor()
      assert.equal(await dialog.locator('section[aria-label="Record context"]').count(), 0, `${kind}: flag-off hides stale context after headline loaded`)
    }

    me.flags.screen_patterns = true
    await page.goto(`${base}/projects/${project.projectNumber}/risks?q=keep&status=Open&panel=Risk:${riskId}`, { waitUntil: 'domcontentloaded' })
    let dialog = page.getByRole('dialog'); let section = dialog.locator('section[aria-label="Record context"]'); await section.getByText('Marc Lead', { exact: true }).first().waitFor(); await section.locator('dt').filter({ hasText: 'Next actor' }).locator('..').getByText('Marc Lead', { exact: true }).waitFor(); await section.getByText('Review', { exact: true }).waitFor(); await section.getByText(/2026-11-04/).waitFor()
    riskTerminal = true; await page.reload(); section = page.getByRole('dialog').locator('section[aria-label="Record context"]'); await section.getByText('No next action', { exact: true }).waitFor(); assert.equal(await section.getByText('Next actor', { exact: true }).count(), 0)

    issueTerminal = false; await page.goto(`${base}/projects/${project.projectNumber}/issues?q=keep&status=Open&panel=Issue:${issueId}`, { waitUntil: 'domcontentloaded' }); dialog = page.getByRole('dialog'); section = dialog.locator('section[aria-label="Record context"]'); await section.getByText(/Review, resolve or complete/i).waitFor(); await section.getByText(/2026-11-06/).waitFor()
    issueTerminal = true; await page.reload(); section = page.getByRole('dialog').locator('section[aria-label="Record context"]'); await section.getByText('No next action', { exact: true }).waitFor(); assert.equal(await section.getByText('Next actor', { exact: true }).count(), 0)

    const actionUrl = `${base}/projects/${project.projectNumber}/meetings?status=Open&sort=dueDate:asc&panel=Action:${actionId}`
    for (const [mode, ownerText, actorText] of [['user', 'You', 'You'], ['discipline', 'Civil', 'Marc Lead'], ['missing-lead', 'Civil', 'Unassigned'], ['inactive-lead', 'Civil', 'Inactive lead (Inactive)'], ['external', 'Vendor (Inactive · No account)', 'Vendor (Inactive · No account)']]) {
      actionMode = mode; await page.goto(actionUrl, { waitUntil: 'domcontentloaded' }); dialog = page.getByRole('dialog'); section = dialog.locator('section[aria-label="Record context"]'); await section.getByText(ownerText, { exact: true }).first().waitFor(); await section.getByText(actorText, { exact: true }).last().waitFor(); await section.getByText('Complete', { exact: true }).waitFor()
      if (mode === 'missing-lead') assert.equal(await section.getByText('Alex Chen', { exact: true }).count(), 0, 'missing discipline lead does not guess the PM')
    }
    actionMode = 'converted'; await page.goto(actionUrl, { waitUntil: 'domcontentloaded' }); dialog = page.getByRole('dialog'); section = dialog.locator('section[aria-label="Record context"]'); await section.getByText('Civil', { exact: true }).first().waitFor(); await section.getByText('Diane Review', { exact: true }).waitFor(); await section.getByText('Continue in the linked task', { exact: true }).waitFor(); await section.getByText(/2026-11-07/).waitFor(); assert.equal(await section.getByRole('button').count(), 1); await section.getByRole('button', { name: /P-MOCK-T002 · Survey input/ }).click(); await page.waitForFunction((id) => new URL(location.href).searchParams.get('panel') === `Task:${id}`, inputTask); await page.getByRole('dialog').getByRole('heading', { name: input.title, exact: true }).waitFor(); assert.equal(new URL(page.url()).searchParams.get('status'), 'Open'); assert.equal(new URL(page.url()).searchParams.get('sort'), 'dueDate:asc')
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click(); await page.waitForFunction(id => new URL(location.href).searchParams.get('panel') === `Action:${id}`, actionId); await page.getByRole('dialog').getByRole('heading', { name: actionBase.text, exact: true }).waitFor(); await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel')); assert.equal(new URL(page.url()).searchParams.get('status'), 'Open'); assert.equal(new URL(page.url()).searchParams.get('sort'), 'dueDate:asc')

    assert.deepEqual(mock.unknownGets, [], `unmocked GETs: ${mock.unknownGets.join('; ')}`); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [], errors.map(error => error.stack ?? error.message).join('\n')); console.log('PASS: operational record context')
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
