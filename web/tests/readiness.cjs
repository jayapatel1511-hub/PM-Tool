// 040-S15-Readiness: the readiness window, the inspector's reads and the constraint lookup go through the typed client. The window is read from
// its contract path with its page query, the inspector reads the assessment, its constraints and its submission prerequisites from theirs and
// shows each answer (the unassessed checks, the sources, the linked record, the superseding package, the exception), choosing a linked
// type reads the link options, and a search link to a constraint reads the lookup and opens its work's inspector. Every read carries
// X-Hub-Source: UI. Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, me, project } = require('./platform-fixtures.cjs')
const { coordinationOptions, coordinationTask } = require('./reviews.fixture.cjs')
const { basisPage } = require('./design-basis.fixture.cjs')
const { commitmentList } = require('./weekly-commitments.fixture.cjs')
const { readinessWindow, windowConstraint, readyOutput, readinessAssessment, readinessCheck, readinessException, linkedRecord, readinessDetail,
  readinessConstraintRow, readinessPackageRef, readinessPrerequisiteRow } = require('./readiness.fixture.cjs')

const port = Number(process.env.READINESS_TEST_PORT ?? 7392)
const civil = project.projectDisciplines[0]
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const taskId = id(500), assessmentId = id(501), constraintId = id(502), linkId = id(503), basisId = id(504), decisionId = id(505), packageId = id(506), successorId = id(507)
const source = (request) => request.headers()['x-hub-source']
const where = (request) => new URL(request.url()).pathname + new URL(request.url()).search
const root = `/api/v1/projects/${project.id}/readiness`
const work = `${root}/Task/${taskId}`

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const reads = []
    const read = (route, json) => { const request = route.request(); reads.push({ path: where(request), source: source(request) }); return route.fulfill({ json }) }
    const detail = () => readinessDetail({
      assessment: readinessAssessment({ id: assessmentId, targetId: taskId, ownerId: me.id, rowVersion: 2, state: 'Not Ready' }),
      checks: [
        readinessCheck({ id: id(511), assessmentId, code: 'Production Owner', applies: true, satisfied: true, reason: 'The linked production owner is current source evidence.' }),
        readinessCheck({ id: id(512), assessmentId, code: 'Submission Gate', applies: true, satisfied: false, reason: 'A linked submission package is not Issued.' }),
        readinessCheck({ id: id(513), assessmentId, code: 'Handoff' }),
      ],
      sources: [{ code: 'Submission Gate', record: linkedRecord({ type: 'SubmissionPackage', id: successorId, key: 'P-MOCK-SUB002', title: 'Permit package', status: 'Draft' }) }],
      unknown: ['Handoff'], blocked: ['Submission Gate'],
      exceptions: [readinessException({ id: id(520), assessmentId, basisVersionId: basisId, approvedBy: me.id, verifierId: me.id, limitedWork: 'Only the preliminary grading layout' })],
    })
    const live = { ...project, permissions: { ...project.permissions, captureWeeklyPlan: { ok: false } } }

    await page.route(`**/api/v1/projects/${project.projectNumber}`, route => route.fulfill({ json: live }))
    await page.route(`**/api/v1/projects/${project.id}`, route => route.fulfill({ json: live }))
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, route => route.fulfill({ json: coordinationOptions({ actorId: me.id, canWrite: true,
      people: [{ id: me.id, displayName: 'Alex Chen' }], disciplines: [{ id: civil.id, name: 'Civil' }],
      tasks: [coordinationTask({ id: taskId, key: 'P-MOCK-T0001', name: 'Grading layout', projectDisciplineId: civil.id, ownerId: me.id, canManage: true, canCommit: true })] }) }))
    await page.route(/\/api\/v1\/projects\/[^/]+\/weekly-commitments(\?.*)?$/, route => route.fulfill({ json: commitmentList([]) }))
    await page.route(/\/api\/v1\/projects\/[^/]+\/design-basis(\?.*)?$/, route => route.fulfill({ json: basisPage([]) }))
    await page.route(/\/api\/v1\/projects\/[^/]+\/readiness\/window(\?.*)?$/, route => read(route, readinessWindow({
      constraints: [windowConstraint({ id: constraintId, targetId: taskId, description: 'Await the outfall decision' })], constraintsTotal: 1,
      readyOutputs: [readyOutput({ id: id(530), targetId: taskId, key: 'P-MOCK-T0002', name: 'Watermain layout', intendedOutput: 'Synthetic layout', dueDate: '2026-10-06' })], readyOutputsTotal: 1 })))
    await page.route(`**${root}/constraints/${constraintId}`, route => read(route, { id: constraintId, key: 'P-MOCK-CT001', targetType: 'Task', targetId: taskId }))
    await page.route(`**${work}`, route => read(route, detail()))
    await page.route(`**${work}/constraints`, route => read(route, [
      readinessConstraintRow({ id: constraintId, key: 'P-MOCK-CT001', targetId: taskId, description: 'Await the outfall decision', removalOwnerId: me.id, affectedOwnerId: me.id,
        linkedType: 'Decision', linkedId: decisionId, linked: linkedRecord({ type: 'Decision', id: decisionId, key: 'P-MOCK-DEC001', title: 'Confirm drainage outfall', status: 'Pending' }) }),
      readinessConstraintRow({ id: id(508), key: 'P-MOCK-CT002', targetId: taskId, description: 'Await the permit', linkedType: 'Issue', linkedId: id(509) }),
    ]))
    await page.route(`**${work}/submission-prerequisites`, route => read(route, [
      readinessPrerequisiteRow({ id: linkId, packageId, package: readinessPackageRef({ id: packageId, key: 'P-MOCK-SUB001', title: 'Permit set', status: 'Superseded' }),
        effective: readinessPackageRef({ id: successorId, key: 'P-MOCK-SUB002', title: 'Permit set rev B', status: 'Issued' }), listsOutput: true, reason: 'Permit set must be issued before grading' }),
    ]))
    await page.route(`**${root}/link-options?*`, route => read(route, [linkedRecord({ type: 'Decision', id: decisionId, key: 'P-MOCK-DEC001', title: 'Confirm drainage outfall', status: 'Pending' })]))

    // The window: its contract path with the page query, both lists rendered from the typed answer.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/readiness?from=2026-10-05&to=2026-10-11`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Watermain layout').first().waitFor({ state: 'visible', timeout: 20000 })
    await page.getByText('Await the outfall decision').first().waitFor({ state: 'visible' })
    const windowRead = reads.find(x => x.path.startsWith(`${root}/window?`))
    assert.ok(windowRead, `the window is read from its contract path: ${JSON.stringify(reads)}`)
    assert.deepEqual([...new URL(windowRead.path, 'http://x').searchParams], [['from', '2026-10-05'], ['to', '2026-10-11'], ['constraintsPage', '1'], ['readyPage', '1'], ['pageSize', '50']])
    assert.equal(windowRead.source, 'UI')

    // The inspector: the work chosen, the assessment, constraints and prerequisites each read from their contract path.
    await page.getByRole('button', { name: 'Inspect readiness' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Linked work').selectOption(`Task:${taskId}`)
    await dialog.getByRole('heading', { name: 'Submission Gate' }).waitFor({ state: 'visible', timeout: 20000 })
    for (const path of [work, `${work}/constraints`, `${work}/submission-prerequisites`]) {
      const one = reads.find(x => x.path === path)
      assert.ok(one, `${path} is read from its contract path`)
      assert.equal(one.source, 'UI')
    }
    // An unassessed check stays visibly unassessed, an unsatisfied one says so, and the source behind the gate is a link.
    await dialog.getByText('Unassessed').first().waitFor({ state: 'visible' })
    await dialog.getByText('A linked submission package is not Issued.').waitFor({ state: 'visible' })
    await dialog.getByRole('list', { name: 'Contributing records' }).getByText('Permit package').waitFor({ state: 'visible' })
    await dialog.getByText('Only the preliminary grading layout').waitFor({ state: 'visible' })
    // A constraint shows the record it links; one whose record is gone reads as unavailable.
    const constraints = dialog.getByRole('region', { name: 'Constraints' })
    await constraints.getByText('Confirm drainage outfall').waitFor({ state: 'visible' })
    await constraints.getByText('P-MOCK-CT001').waitFor({ state: 'visible' })
    await constraints.getByText('Await the permit').waitFor({ state: 'visible' })
    assert.ok(await constraints.getByText('Unavailable or no longer a participant').count() >= 1, 'a link to a record that is gone reads as unavailable')
    // A prerequisite names its package, the package that now governs it and that the package lists the work's own output.
    const prerequisites = dialog.locator('section[aria-labelledby="readiness-prerequisites"]')
    await prerequisites.getByText('P-MOCK-SUB001').waitFor({ state: 'visible' })
    await prerequisites.getByText('P-MOCK-SUB002').waitFor({ state: 'visible' })
    await prerequisites.getByText('Permit set must be issued before grading').waitFor({ state: 'visible' })

    // Raising a constraint: choosing a linked type reads the link options for that type.
    await constraints.getByRole('button', { name: 'Record constraint' }).click()
    const raise = page.getByRole('dialog')
    await raise.getByLabel('Existing record this constraint represents (optional)').selectOption('Decision')
    await raise.getByLabel('Linked record').waitFor({ state: 'visible', timeout: 20000 })
    const options = reads.find(x => x.path.startsWith(`${root}/link-options`))
    assert.ok(options, 'the link options are read from their contract path')
    assert.deepEqual([...new URL(options.path, 'http://x').searchParams], [['type', 'Decision']])
    assert.equal(options.source, 'UI')

    // A search link to a constraint: the lookup is read, then its work's inspector opens on the same assessment.
    reads.length = 0
    await page.goto(`${preview.base}/projects/${project.projectNumber}/readiness?from=2026-10-05&to=2026-10-11&panel=WorkConstraint:${constraintId}`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Submission Gate' }).waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.some(x => x.path === `${root}/constraints/${constraintId}` && x.source === 'UI'), 'the constraint lookup is read from its contract path')
    assert.ok(reads.some(x => x.path === work), 'its work is opened')
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
