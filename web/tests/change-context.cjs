const assert = require('node:assert/strict')
const { newMockPage, me, project, task } = require('./platform-fixtures.cjs')
const { taskDetail, taskRow } = require('./tasks.fixture.cjs')
const { changeRow, changePage, changeNoticeRow, changeAssessmentRow, changeDetail, sourceRevisionRow, coordinationSource, coordinationOptions, coordinationTask } = require('./reviews.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const alex = me.id, diane = '00000000-0000-0000-0000-000000000007', marc = '00000000-0000-0000-0000-000000000013'
const noticeId = '00000000-0000-0000-0000-000000000051', assessmentId = '00000000-0000-0000-0000-000000000052'
const missingId = '00000000-0000-0000-0000-000000000099', secondTask = '00000000-0000-0000-0000-000000000056'
const secondAssessment = '00000000-0000-0000-0000-000000000055', unknownAssessment = '00000000-0000-0000-0000-000000000057'
const oldId = '00000000-0000-0000-0000-000000000053', newId = '00000000-0000-0000-0000-000000000054'
const person = (id, name) => ({ id, name, state: 'Active', kind: 'User' })
const date = '2026-11-10'
const context = (overrides = {}) => ({ owner: person(diane, 'Diane Review'), nextActors: [person(diane, 'Diane Review')], neededBy: date,
  nextAction: 'assess_change', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [], ...overrides })
let mode = 'pending', parentGets = 0, childGets = 0
const status = () => mode === 'pending' ? 'Pending Assessment' : mode === 'resolved' ? 'Resolved' : 'Update Required'
const correctionInput = () => ({ kind: 'Task', id: task.id, key: task.key, title: task.name, neededBy: null,
  nextActors: mode === 'verify' || mode === 'resolved' ? [] : [person(marc, 'Marc Lead')],
  nextAction: mode === 'verify' || mode === 'resolved' ? 'none' : 'review', primary: mode === 'follow', actorState: mode === 'verify' || mode === 'resolved' ? 'None' : 'Active' })
const childContext = () => context(mode === 'pending' ? {} : mode === 'retention'
  ? { nextActors: [], actorState: 'Unassigned', nextAction: 'not_assessed', inputs: [correctionInput()] }
  : mode === 'follow' ? { nextActors: [person(marc, 'Marc Lead')], nextAction: 'follow_linked_task', inputs: [correctionInput()] }
  : mode === 'verify' ? { nextActors: [person(marc, 'Marc Lead')], nextAction: 'verify_change_update', inputs: [correctionInput()] }
  : { nextActors: [], actorState: 'None', nextAction: 'none', terminal: true, inputs: [correctionInput()] })
const parentContext = () => context({ owner: person(alex, 'Alex Chen'), nextActors: mode === 'pending' ? [person(marc, 'Marc Lead'), person(diane, 'Diane Review')] : [person(marc, 'Marc Lead')],
  actorState: 'Partial', nextAction: 'not_assessed', inputs: [
    { kind: 'ChangeAssessment', id: assessmentId, key: task.key, title: task.name, neededBy: date, nextActors: childContext().nextActors, nextAction: childContext().nextAction, actorState: childContext().actorState, primary: false },
    { kind: 'ChangeAssessment', id: secondAssessment, key: 'P-MOCK-T002', title: 'Second target', neededBy: date, nextActors: [person(marc, 'Marc Lead')], nextAction: 'assess_change', actorState: 'Active', primary: false },
    { kind: 'ChangeAssessment', id: unknownAssessment, key: 'CN-001', title: 'Corridor alignment update', neededBy: date, nextActors: [], nextAction: 'not_assessed', actorState: 'Unknown', primary: false },
  ] })
const oldRevision = sourceRevisionRow({ id: oldId, projectId: project.id, sourceKey: 'D-001', title: 'Corridor', revision: 'A' })
const newRevision = sourceRevisionRow({ id: newId, projectId: project.id, sourceKey: 'D-001', title: 'Corridor', revision: 'B' })
const notice = changeNoticeRow({ id: noticeId, projectId: project.id, key: 'CN-001', title: 'Corridor alignment update', ownerId: alex, assessmentDueDate: date, status: 'Open' })
const assessment = () => changeAssessmentRow({ id: assessmentId, projectId: project.id, changeNoticeId: noticeId, targetType: 'Task', targetId: task.id,
  ownerId: diane, reviewerId: marc, revisionUsedId: oldId, status: status(), retainOldRevision: true,
  retentionApprovedBy: mode === 'pending' || mode === 'retention' ? null : marc, correctionTaskId: mode === 'pending' ? null : task.id,
  rationale: mode === 'pending' ? null : 'Recorded correction', evidenceUrl: mode === 'pending' ? null : 'https://example.test/evidence',
  verifiedBy: mode === 'resolved' ? marc : null, verifiedAt: mode === 'resolved' ? '2026-11-09T12:00:00Z' : null })

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui, errors = []
  page.setDefaultTimeout(12000)
  page.on('pageerror', e => errors.push(e.message))
  page.on('console', m => { if (m.type() === 'error' && !/server responded with a status of 404/.test(m.text())) errors.push(m.text()) })
  try {
    const options = coordinationOptions({ actorId: alex, canWrite: false, people: [{ id: alex, displayName: 'Alex Chen' }, { id: diane, displayName: 'Diane Review' }, { id: marc, displayName: 'Marc Lead' }],
      sources: [coordinationSource({ revision: oldRevision }), coordinationSource({ revision: newRevision, isCurrent: true })],
      tasks: [coordinationTask({ id: task.id, key: task.key, name: task.name, projectDisciplineId: project.disciplines[0].id, ownerId: diane }),
        coordinationTask({ id: secondTask, key: 'P-MOCK-T002', name: 'Second target', projectDisciplineId: project.disciplines[0].id, ownerId: marc })] })
    await page.route(`**/api/v1/projects/${project.id}/changes**`, route => {
      const path = new URL(route.request().url()).pathname
      if (path.endsWith('/options')) return route.fulfill({ json: options })
      if (path.endsWith(`/${noticeId}`)) { parentGets++; return route.fulfill({ json: changeDetail({ notice, oldRevision, newRevision, context: parentContext(), uses: [], canWrite: false,
        completeAssessmentIds: mode === 'resolved' ? [assessmentId] : [], assessments: [assessment(),
          changeAssessmentRow({ id: secondAssessment, projectId: project.id, changeNoticeId: noticeId, targetType: 'Task', targetId: secondTask, ownerId: marc, revisionUsedId: oldId }),
          changeAssessmentRow({ id: unknownAssessment, projectId: project.id, changeNoticeId: noticeId, targetType: 'Task', targetId: missingId, ownerId: alex, revisionUsedId: oldId })] }) }) }
      return route.fulfill({ json: changePage([changeRow({ ...notice }), ...Array.from({ length: 50 }, (_, i) => changeRow({ ...notice,
        id: `00000000-0000-0000-0001-${String(i + 1).padStart(12, '0')}`, key: `CN-${i + 2}`, title: `Scrollable notice ${i + 1}` }))]) })
    })
    await page.route(`**/api/v1/projects/${project.id}/input-uses*`, route => route.fulfill({ json: { items: [], page: 1, pageSize: 50, totalCount: 0, assessments: [], history: [] } }))
    await page.route(`**/api/v1/projects/${project.id}/change-assessments/${assessmentId}`, route => { childGets++; return route.fulfill({ json: { id: assessmentId, noticeId, key: task.key, title: task.name, context: childContext() } }) })
    await page.route(`**/api/v1/projects/${project.id}/change-assessments/${missingId}`, route => route.fulfill({ status: 404, json: { error: 'not_found', message: 'Not found' } }))
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: taskDetail({ task: taskRow({ ...task, assigneeId: diane, reviewerId: marc, status: mode === 'verify' || mode === 'resolved' ? 'Complete' : 'In Review', requiresReview: true }) }) }))
    const origin = `${base}/projects/${project.projectNumber}/changes?status=Open&sort=assessmentDueDate:asc&panel=ChangeNotice:${noticeId}`
    const exact = `${base}/projects/${project.projectNumber}/changes?status=Open&sort=assessmentDueDate:asc&panel=ChangeAssessment:${assessmentId}`
    const atExact = url => url.pathname.endsWith('/changes') && url.searchParams.get('panel') === `ChangeAssessment:${assessmentId}` && url.searchParams.get('status') === 'Open' && url.searchParams.get('sort') === 'assessmentDueDate:asc'
    const section = () => page.getByRole('dialog').locator('section[aria-label="Record context"]')
    const fact = (name) => section().locator('dt').filter({ hasText: new RegExp(`^${name}$`) }).locator('..')
    me.flags = { ...me.flags, screen_patterns: false }
    await page.goto(origin); await page.getByRole('dialog').getByRole('heading', { name: /CN-001/ }).waitFor(); assert.equal(await section().count(), 0)
    await page.goto(exact); assert.equal(childGets, 0); assert.equal(await page.getByRole('dialog').count(), 0, 'new child route stays dark')
    me.flags.screen_patterns = true
    await page.goto(origin); await fact('Owner').getByText('You', { exact: true }).waitFor(); await fact('Next actor').getByText(/Other next actors not assessed/).waitFor()
    await fact('Next action').getByText('Not assessed', { exact: true }).waitFor(); await section().getByText(date, { exact: false }).first().waitFor()
    // A copied child URL resolves the exact child, not a parent fallback or a different assessment.
    await page.goto(exact); await page.getByRole('dialog').locator('article').first().waitFor(); assert.equal(await page.getByRole('dialog').locator('article').count(), 1)
    await fact('Owner').getByText('Diane Review', { exact: true }).waitFor(); await fact('Next actor').getByText('Diane Review', { exact: true }).waitFor()
    await fact('Next action').getByText('Assess change', { exact: true }).waitFor(); await section().getByText(/Notice assessment due/).waitFor()
    for (const [state, actor, action] of [['follow', 'Marc Lead', 'Continue in the linked task'], ['retention', 'Unassigned', 'Not assessed'], ['verify', 'Marc Lead', 'Verify the update']]) {
      mode = state; await page.reload(); await fact('Next actor').getByText(actor, { exact: true }).waitFor(); await fact('Next action').getByText(action, { exact: true }).waitFor()
      await fact('Owner').getByText('Diane Review', { exact: true }).waitFor()
      if (state === 'retention') {
        await page.goto(origin)
        const row = section().getByRole('link', { name: new RegExp(`Assessment.*${task.key}`) }).locator('..').locator('..')
        await row.getByText('Unassigned', { exact: true }).waitFor()
        await page.goto(exact); await fact('Next actor').getByText('Unassigned', { exact: true }).waitFor()
      }
    }
    mode = 'resolved'; await page.reload(); await fact('Next action').getByText('No next action', { exact: true }).waitFor(); assert.equal(await fact('Next actor').count(), 0)
    await fact('Owner').getByText('Diane Review', { exact: true }).waitFor(); await section().getByText(date, { exact: false }).first().waitFor()
    const before = parentGets
    await page.goto(exact.replace(assessmentId, missingId)); await page.getByRole('dialog').getByRole('heading', { name: 'Assessment', exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('alert').waitFor(); assert.equal(parentGets, before); assert.equal(await section().count(), 0)
    // Nested return chain: notice -> exact assessment -> correction -> assessment -> notice.
    mode = 'follow'; await page.goto(origin); await fact('Owner').getByText('You', { exact: true }).waitFor()
    const scroll = await page.locator('main#content').evaluate(el => { el.scrollTop = 160; return el.scrollTop }); assert.equal(scroll, 160)
    await section().getByRole('link', { name: new RegExp(`Assessment.*${task.key}`) }).click(); await page.waitForURL(atExact); await fact('Owner').getByText('Diane Review', { exact: true }).waitFor()
    await section().getByRole('button', { name: new RegExp(task.key) }).click(); await page.getByRole('dialog').getByRole('heading', { name: task.name, exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(atExact); await fact('Next action').getByText('Continue in the linked task', { exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected, scroll)
    assert.equal(new URL(page.url()).searchParams.get('sort'), 'assessmentDueDate:asc'); assert.equal(new URL(page.url()).searchParams.get('status'), 'Open')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: change notice, exact assessment, canonical steps and nested correction return')
  } finally { await ui.browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
