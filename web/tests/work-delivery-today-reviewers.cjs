// S04: first useful Today row, device-persisted My Week, independent reviewer guidance and canonical Start refusal.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task } = require('./platform-fixtures.cjs')
const { taskRow, taskState, taskPage, taskDetail, taskPermissions, taskTransition } = require('./tasks.fixture.cjs')
const { myWork, myWorkPerson, myWorkProject } = require('./my-work.fixture.cjs')
const { planningGrid, planningPerson, planningCell } = require('./planning.fixture.cjs')
const { coordinationOptions, sourceRevisionRow, coordinationSource, reviewRow, reviewPage, reviewPackageRow, reviewRoundRow, reviewAssignmentRow, reviewDetail } = require('./reviews.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const reviewer = id(951), reviewId = id(952), roundId = id(953), assignmentId = id(954), revisionId = id(955)
const person = { id: me.id, name: 'Alex Chen', state: 'Active', kind: 'User' }
const independent = { id: reviewer, name: 'Omar Reviewer', state: 'Active', kind: 'User' }
const row = taskRow({ ...task, key: 'P-MOCK-T0005', name: 'Focus survey check', projectDisciplineId: project.disciplines[0].id,
  assigneeId: me.id, assigneeName: person.name, requiresReview: true, reviewerId: null, reviewerName: null, dueDate: '2026-10-03', state: taskState({ isUnassigned: false, isMissingDueDate: false }) })

;(async () => {
  const savedMe = structuredClone(me)
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const pageErrors = []; page.on('pageerror', error => pageErrors.push(error.message))
  let partial = false, provisional = false, selfAllowed = false, sameReviewer = false, validPackage = false, packagePosts = 0, transitionPath = false
  const transitionPosts = []
  const currentTask = () => ({ ...row, ...(transitionPath ? { status: 'In Progress', lane: 'In Progress' } : {}), reviewerId: sameReviewer ? me.id : null, reviewerName: sameReviewer ? person.name : null })
  try {
    await page.setViewportSize({ width: 1600, height: 900 })
    me.flags.visual_system = true; me.flags.screen_patterns = false; me.flags.live_events = false; me.settings.allowSelfReview = false
    await page.route('**/api/v1/me/work*', route => route.fulfill({ json: myWork({ person: myWorkPerson(me.id, person.name), today: '2026-10-03', tasks: [row], projects: [myWorkProject({ id: project.id, projectNumber: project.projectNumber, name: project.name })] }) }))
    await page.route('**/api/v1/planning/grid*', route => route.fulfill({ json: planningGrid({ currentWeek: '2026-09-28', weeks: ['2026-09-28'], partialView: partial,
      people: [planningPerson({ id: me.id, displayName: person.name, partial, cells: [planningCell({ week: '2026-09-28', confirmed: 20, expected: 10, possible: 5, capacity: 40, remaining: 10, provisional })] })] }) }))
    await page.route(`**/api/v1/projects/${project.id}/tasks*`, route => route.fulfill({ json: taskPage([currentTask()]) }))
    await page.route('**/api/v1/tasks?*', route => route.fulfill({ json: taskPage([currentTask()]) }))
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: taskDetail({ task: currentTask(), permissions: taskPermissions({ allowSelfReview: selfAllowed,
      transitions: transitionPath ? [taskTransition('Ready for Review')] : [] }) }) }))
    await page.route(`**/api/v1/tasks/${task.id}/transition`, route => {
      transitionPosts.push(route.request().postDataJSON())
      return route.fulfill({ json: { id: task.id, status: 'Ready for Review', rowVersion: 2, progressPct: 0, reviewRound: 1, warnings: [] } })
    })
    await page.route('**/api/v1/users?*', route => route.fulfill({ json: [
      { id: me.id, displayName: person.name, isActive: true, onProject: true }, { id: reviewer, displayName: independent.name, isActive: true, onProject: true },
    ] }))
    await page.goto(`${base}/my-work`, { waitUntil: 'domcontentloaded' })
    await page.locator('#mw-today').getByRole('button', { name: row.name, exact: true }).waitFor()
    assert.ok(await page.evaluate(() => document.querySelector('#mw-my-week').compareDocumentPosition(document.querySelector('#mw-today')) & Node.DOCUMENT_POSITION_FOLLOWING), 'disabled ordering stays legacy')
    await page.locator('#mw-my-week').getByText(/35 h of 40 h planned/).waitFor()
    const beforeMembers = await page.locator('#mw-today').getByRole('button', { name: row.name, exact: true }).count()
    me.flags.screen_patterns = true
    await page.reload()
    const focus = page.locator('#mw-today').getByRole('button', { name: row.name, exact: true })
    await focus.waitFor()
    assert.equal(await focus.count(), beforeMembers, 'reordering does not change focus membership')
    assert.ok(await page.evaluate(() => document.querySelector('#mw-today').compareDocumentPosition(document.querySelector('#mw-my-week')) & Node.DOCUMENT_POSITION_FOLLOWING), 'focus is before My Week')
    assert.ok(await page.evaluate(() => document.querySelector('#mw-today').compareDocumentPosition(document.querySelector('#mw-attention')) & Node.DOCUMENT_POSITION_FOLLOWING), 'focus is before attention')
    const firstY = (await focus.boundingBox()).y
    assert.ok(firstY <= 405, `first useful Today row ${firstY} must meet AC-CALM-01 at 1600x900`)
    const week = page.locator('#mw-my-week')
    await week.getByText('My Week: 30 h planned, 10 h free', { exact: true }).waitFor()
    assert.equal(await week.getByRole('button', { name: 'Show', exact: true }).getAttribute('aria-expanded'), 'false')
    await week.getByRole('button', { name: 'Show', exact: true }).click()
    assert.equal(await week.getByRole('button', { name: 'Hide', exact: true }).getAttribute('aria-expanded'), 'true')
    await page.reload(); await week.getByRole('button', { name: 'Hide', exact: true }).waitFor()
    await page.goto(`${base}/my-work?tab=overview`)
    await week.getByRole('button', { name: 'Hide', exact: true }).waitFor()
    await page.goto(`${base}/my-work?myWeek=2026-09-28`)
    await week.getByRole('button', { name: 'Hide', exact: true }).waitFor()
    partial = true; await page.reload(); await week.getByText(/at most 10 h/i).waitFor()
    provisional = true; await page.reload(); await week.getByText('My Week: 30 h planned, Free unavailable', { exact: true }).waitFor()
    partial = false; provisional = false

    await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=Task:${task.id}`)
    const panel = () => page.getByRole('dialog').last()
    await panel().getByText('Performer', { exact: true }).waitFor()
    await panel().getByText('Independent reviewer', { exact: true }).waitFor()
    await panel().getByText(/Reviewer missing/).first().waitFor()
    await panel().getByText('The performer is excluded while self-review is disabled', { exact: true }).waitFor()
    await panel().getByText('Independent reviewer', { exact: true }).locator('..').locator('button[data-slot="popover-trigger"]').click()
    await page.getByRole('option', { name: /Omar Reviewer/ }).waitFor()
    assert.equal(await page.getByRole('option', { name: /Alex Chen/ }).count(), 0, 'performer is excluded')
    await page.keyboard.press('Escape')

    // Follow the actual list status-menu path into TransitionDialog, which has its own reviewer picker.
    transitionPath = true
    const openReviewerTransition = async () => {
      await page.goto(`${base}/projects/${project.projectNumber}/tasks`)
      await page.getByRole('button', { name: `Change status of ${row.key}, now In Progress`, exact: true }).click()
      await page.getByRole('menuitem', { name: 'Ready for Review', exact: true }).click()
      await panel().getByRole('heading', { name: `Move ${row.key} to Ready for Review?`, exact: true }).waitFor()
    }
    // The canonical detail permission wins even when /me's cached setting disagrees.
    me.settings.allowSelfReview = true; selfAllowed = false
    await openReviewerTransition()
    await panel().getByText('The performer is excluded while self-review is disabled', { exact: true }).waitFor()
    assert.equal(await panel().getByRole('button', { name: 'Ready for Review', exact: true }).isDisabled(), true)
    await panel().locator('#tt-reviewer').click()
    await page.getByRole('option', { name: /Omar Reviewer/ }).waitFor()
    assert.equal(await page.getByRole('option', { name: /Alex Chen/ }).count(), 0, 'transition picker excludes the canonical performer')
    await page.getByRole('option', { name: /Omar Reviewer/ }).click()
    const transitioned = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/tasks/${task.id}/transition` && response.request().method() === 'POST')
    await panel().getByRole('button', { name: 'Ready for Review', exact: true }).click()
    await transitioned
    assert.equal(transitionPosts.length, 1)
    assert.equal(transitionPosts[0].reviewerId, reviewer)
    assert.equal(transitionPosts[0].toStatus, 'Ready for Review')

    selfAllowed = true; me.settings.allowSelfReview = false
    await openReviewerTransition()
    await panel().locator('#tt-reviewer').waitFor({ state: 'visible' })
    await panel().locator('#tt-reviewer').click()
    await page.getByRole('option', { name: /Alex Chen/ }).click()
    await panel().getByText('Self-review (allowed by setting)', { exact: true }).waitFor()
    await panel().getByRole('button', { name: 'Cancel', exact: true }).click()

    me.flags.screen_patterns = false; selfAllowed = false
    await openReviewerTransition()
    assert.equal(await panel().getByText('The performer is excluded while self-review is disabled', { exact: true }).count(), 0)
    await panel().locator('#tt-reviewer').click()
    await page.getByRole('option', { name: /Alex Chen/ }).waitFor()
    await page.keyboard.press('Escape')
    await panel().getByRole('button', { name: 'Cancel', exact: true }).click()
    me.flags.screen_patterns = true; transitionPath = false
    selfAllowed = true; sameReviewer = true; me.settings.allowSelfReview = true
    await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=Task:${task.id}`)
    await panel().getByText('Self-review (allowed by setting)', { exact: true }).waitFor()
    await panel().getByText('Reviewer', { exact: true }).locator('..').locator('button[data-slot="popover-trigger"]').click()
    await page.getByRole('option', { name: /Alex Chen/ }).waitFor()
    await page.keyboard.press('Escape')

    selfAllowed = false; sameReviewer = false; me.settings.allowSelfReview = false
    await page.goto(`${base}/projects/${project.projectNumber}/board`)
    await page.getByText('Performer', { exact: true }).waitFor()
    await page.getByText('Independent reviewer', { exact: true }).waitFor()
    await page.getByText(/Reviewer missing/).first().waitFor()
    const source = sourceRevisionRow({ id: revisionId, deliverableId: id(956), sourceKey: 'SURVEY-SOURCE', title: 'Survey source', revision: 'A' })
    const options = coordinationOptions({ actorId: me.id, manageDisciplineIds: [project.disciplines[0].id], canCreate: { reviews: true, changes: false },
      people: [{ id: me.id, displayName: person.name }, { id: reviewer, displayName: independent.name }],
      disciplines: [{ id: project.disciplines[0].id, name: 'Civil' }], sources: [coordinationSource({ revision: source })] })
    const guidance = () => ({ allowSelfReview: false, sourcesAvailable: true, authorIds: [me.id], excludedReviewerIds: [me.id], sources: [{ sourceRevisionId: revisionId, sourceKey: source.sourceKey, available: true, performers: [{ taskKey: row.key, person }] }] })
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, route => route.fulfill({ json: options }))
    await page.route(`**/api/v1/projects/${project.id}/review-assignment-guidance*`, route => route.fulfill({ json: guidance() }))
    await page.route(`**/api/v1/projects/${project.id}/reviews**`, route => {
      const path = new URL(route.request().url()).pathname
      if (route.request().method() === 'POST') { packagePosts++; return route.fulfill({ json: { id: reviewId, rowVersion: 1 } }) }
      if (path.endsWith(`/${reviewId}`)) return route.fulfill({ json: reviewDetail({ package: reviewPackageRow({ id: reviewId, key: 'P-MOCK-RV001', title: 'Survey review', currentRoundId: roundId }), actorId: me.id, canCoordinate: true, canManage: true, canWrite: true, eligiblePeople: [me.id, reviewer],
        rounds: [reviewRoundRow({ id: roundId, number: 1 })], assignments: [reviewAssignmentRow({ id: assignmentId, roundId, projectDisciplineId: project.disciplines[0].id, reviewerId: reviewer })],
        presentation: [{ roundId, guidance: guidance(), missingReviewerAssignmentIds: validPackage ? [] : [assignmentId], reviewers: [{ assignmentId, reviewer: validPackage ? independent : { ...independent, state: 'Inactive' } }], startAllowed: validPackage, startReason: validPackage ? null : 'Select an active project person' }] }) })
      return route.fulfill({ json: reviewPage([reviewRow({ id: reviewId, key: 'P-MOCK-RV001', title: 'Survey review' })]) })
    })
    await page.goto(`${base}/projects/${project.projectNumber}/reviews?panel=ReviewPackage:${reviewId}`)
    await panel().getByRole('button', { name: 'Start review', exact: true }).waitFor()
    assert.equal(await panel().getByRole('button', { name: 'Start review', exact: true }).isDisabled(), true)
    await panel().getByRole('status').filter({ hasText: 'Select an active project person' }).waitFor()
    await panel().getByText('Current source performers', { exact: true }).waitFor()
    await panel().getByText(/Reviewer missing/).waitFor()
    validPackage = true; await page.reload()
    await panel().getByRole('button', { name: 'Start review', exact: true }).waitFor()
    assert.equal(await panel().getByRole('button', { name: 'Start review', exact: true }).isDisabled(), false)
    me.flags.screen_patterns = false; validPackage = false; await page.reload()
    await panel().getByRole('button', { name: 'Start review', exact: true }).waitFor()
    assert.equal(await panel().getByRole('button', { name: 'Start review', exact: true }).isDisabled(), false, 'off retains the old start affordance; server guards still apply')
    me.flags.screen_patterns = true
    await page.goto(`${base}/projects/${project.projectNumber}/reviews`)
    await page.getByRole('button', { name: 'New review package', exact: true }).click()
    await panel().getByRole('heading', { name: 'New review package', exact: true }).waitFor()
    const confirm = () => panel().getByRole('button', { name: 'Confirm', exact: true })
    assert.equal(await confirm().isDisabled(), true, 'source checks and required reviewer are incomplete')
    await panel().locator('form').evaluate(form => form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    assert.equal(packagePosts, 0, 'disabled command guard also rejects form submission events')
    await panel().getByLabel(/SURVEY-SOURCE/).check()
    await panel().getByText('Source authors are excluded while self-review is disabled', { exact: true }).waitFor()
    await panel().getByRole('checkbox', { name: 'Civil', exact: true }).check()
    const packageReviewer = panel().getByLabel('Reviewer', { exact: true })
    assert.equal(await packageReviewer.locator(`option[value="${me.id}"]`).count(), 0, 'source author is excluded from the package reviewer choices')
    assert.equal(await packageReviewer.locator(`option[value="${reviewer}"]`).count(), 1)
    assert.equal(await confirm().isDisabled(), true)
    await packageReviewer.selectOption(reviewer)
    assert.equal(await confirm().isDisabled(), false, 'an independent required reviewer completes the presentation gate')
    assert.deepEqual(pageErrors, [])
    assert.deepEqual(mock.apiErrors, [])
    console.log(JSON.stringify({ result: 'PASS', firstUsefulRowY: firstY, checks: ['Today order and 405px', 'focus membership', 'device expansion', 'overview/explicit link', 'partial/provisional free', 'performer exclusion and allowed-self label', 'real status-menu transition picker and independent reviewer command', 'transition canonical setting and off parity', 'board reviewer labels', 'package disabled reason/off parity', 'package candidate exclusion and submit guard'] }))
  } catch (error) {
    console.error(await page.locator('body').innerText(), mock.unknownGets, mock.apiErrors, pageErrors)
    throw error
  } finally {
    Object.assign(me, savedMe)
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
