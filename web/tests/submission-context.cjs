// Submission-package and exact submission-check record-context coverage.
const assert = require('node:assert/strict')
const { newMockPage, me, project, panelIds } = require('./platform-fixtures.cjs')
const { submissionListRow, submissionPage } = require('./submissions.fixture.cjs')
const { coordinationOptions, coordinationDeliverable, coordinationTask } = require('./reviews.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const packageId = '00000000-0000-0000-0000-000000000071'
const checkId = '00000000-0000-0000-0000-000000000072'
const missingCheckId = '00000000-0000-0000-0000-000000000099'
const milestoneId = '00000000-0000-0000-0000-000000000073'
const coordinator = me.id
const reviewer = '00000000-0000-0000-0000-000000000007'
const sourceDeliverable = panelIds.deliverable
const sourceTask = '00000000-0000-0000-0000-000000000074'
const person = (id, name, state = 'Active', kind = 'User') => ({ id, name, state, kind })
const parentContext = { owner: person(coordinator, 'Alex Chen'), nextActors: [person(reviewer, 'Diane Review')], neededBy: '2026-11-12', nextAction: 'resolve_check', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], reviewPackageGate: null, inputs: [{ kind: 'SubmissionCheck', id: checkId, key: 'SUB-001', title: 'Current permit input', neededBy: null, nextActors: [person(reviewer, 'Diane Review')], nextAction: 'resolve_check', primary: true }] }
let checkContext = { owner: person(reviewer, 'Diane Review'), nextActors: [person(reviewer, 'Diane Review')], neededBy: null, nextAction: 'resolve_check', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], reviewPackageGate: null, inputs: [{ kind: 'Deliverable', id: sourceDeliverable, key: 'D-001', title: 'Synthetic deliverable', neededBy: null, nextActors: [], nextAction: 'review', primary: true }] }
const pkg = { id: packageId, projectId: project.id, key: 'SUB-001', title: 'Permit submission', purpose: 'Permit review\n' + 'Supporting source notes\n'.repeat(80), recipientReference: 'Authority', coordinatorId: coordinator, milestoneId, targetDate: '2026-11-12', status: 'Checking', manifestVersion: 2, rowVersion: 1, seq: 1, supersedesPackageId: null, auditType: 'SubmissionPackage', auditProjectId: null, auditKey: null, auditName: null, createdAt: '2026-11-01T00:00:00Z', createdBy: coordinator, updatedAt: '2026-11-01T00:00:00Z', updatedBy: coordinator, deletedAt: null, deletedBy: null }
const check = (id = checkId, effectiveStatus = 'Fail', context = checkContext) => ({ id, packageId, projectId: project.id, manifestVersion: 2, kind: 'Independent Review', evidenceRule: 'Independent Review', ownerId: reviewer, projectDisciplineId: project.disciplines[0].id, status: 'Pending', effectiveStatus, required: true, sourceId: sourceDeliverable, evidenceUrl: null, reason: 'Input has not been confirmed.', approvedAt: null, approvedBy: null, rowVersion: 1, auditType: 'SubmissionCheck', auditProjectId: null, auditKey: null, auditName: null, createdAt: '2026-11-01T00:00:00Z', createdBy: coordinator, updatedAt: '2026-11-01T00:00:00Z', updatedBy: coordinator, context })
let detailChecks = [check(checkId, 'Fail'), check('00000000-0000-0000-0000-000000000075', 'Pending')]
// S05 (FR-SCR-05): with screen_patterns on the panel shows the server's delivery presentation. A readiness blocker
// that names no check (MissingInput below) is a package problem there (DeliveryScreenReads.Problems).
const delivery = () => ({ summary: { available: true, blockingCheckCount: 2, packageProblemCount: 1, unknownCheckCount: 0 }, evaluatedAt: '2026-11-01T00:00:00Z',
  checksComplete: true, manifestComplete: true, issueAvailability: 'NotIssued', packageProblems: [{ code: 'MissingInput', message: 'Required input is unavailable.' }],
  checks: detailChecks.map(row => ({ checkId: row.id, state: row.effectiveStatus, known: true, blocking: true, checkingOwner: row.context.owner, disciplineId: project.disciplines[0].id, disciplineName: 'Civil', reasons: [] })),
  manifest: [], statusCounts: [], unavailableDeliverables: 0 })
let parentGetCount = 0, childVersion = 2

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error' && !/server responded with a status of 404/.test(message.text())) errors.push(new Error(message.text())) })
  try {
    page.setDefaultTimeout(12000)
    const options = coordinationOptions({ actorId: coordinator, canWrite: false, people: [{ id: coordinator, displayName: 'Alex Chen' }, { id: reviewer, displayName: 'Diane Review' }], deliverables: [coordinationDeliverable({ id: sourceDeliverable, key: 'D-001', name: 'Synthetic deliverable', projectDisciplineId: project.disciplines[0].id, ownerId: coordinator })], tasks: [coordinationTask({ id: sourceTask, key: 'T-074', name: 'Source task', projectDisciplineId: project.disciplines[0].id, ownerId: coordinator })] })
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, route => route.fulfill({ json: options }))
    await page.route(`**/api/v1/projects/${project.id}/milestones*`, route => route.fulfill({ json: [{ id: milestoneId, key: 'M-001', name: 'Permit milestone', date: '2026-11-20', isCancelled: false }] }))
    await page.route(`**/api/v1/projects/${project.id}/submissions**`, async route => {
      const path = new URL(route.request().url()).pathname
      if (path.endsWith(`/submissions/${packageId}`)) { parentGetCount++; return route.fulfill({ json: { package: pkg, checks: detailChecks, manifest: [], effectiveStatus: 'Checking', evidence: [], issue: null, readiness: { ready: false, fingerprint: 'fp', blockers: [{ code: 'MissingInput', kind: 'Input', disciplineId: null, ownerId: null, sourceId: null, sourcePath: null, message: 'Required input is unavailable.' }], }, context: parentContext, delivery: delivery(), canAuthorise: false, canCoordinate: false } }) }
      return route.fulfill({ json: submissionPage([submissionListRow({ id: packageId, projectId: project.id, key: pkg.key, title: pkg.title, status: pkg.status, coordinatorId: coordinator, targetDate: pkg.targetDate, blockerCount: 1 }), ...Array.from({ length: 30 }, (_, i) => submissionListRow({ id: `00000000-0000-0000-0000-${String(i + 200).padStart(12, '0')}`, projectId: project.id, key: `SUB-${String(i + 2).padStart(3, '0')}`, title: `Other submission ${i + 1}`, status: 'Draft', coordinatorId: coordinator, targetDate: '2026-12-01', blockerCount: 0 }))]) })
    })
    await page.route(`**/api/v1/projects/${project.id}/submission-checks/${checkId}`, route => route.fulfill({ json: { id: checkId, packageId, manifestVersion: childVersion, key: pkg.key, title: 'Current Revision', context: checkContext } }))
    await page.route(`**/api/v1/projects/${project.id}/submission-checks/${missingCheckId}`, route => route.fulfill({ status: 404, contentType: 'application/json', json: { error: 'Not found' } }))

    const parentUrl = `${base}/projects/${project.projectNumber}/submissions?status=Checking&q=permit&sort=targetDate:asc&panel=SubmissionPackage:${packageId}`
    me.flags = { ...me.flags, screen_patterns: false }; await page.goto(parentUrl, { waitUntil: 'domcontentloaded' }); let dialog = page.getByRole('dialog'); await dialog.getByRole('heading', { name: /SUB-001 · Permit submission/ }).waitFor(); assert.equal(await dialog.locator('section[aria-label="Record context"]').count(), 0, 'flag-off leaves legacy submission context hidden')
    me.flags.screen_patterns = true; await page.reload(); dialog = page.getByRole('dialog'); let sections = dialog.locator('section[aria-label="Record context"]'); let section = sections.first(); await section.locator('dt').filter({ hasText: 'Owner' }).locator('..').getByText('You', { exact: true }).waitFor(); await section.locator('dt').filter({ hasText: 'Next actor' }).locator('..').getByText('Diane Review', { exact: true }).waitFor(); await section.locator('dt').filter({ hasText: 'Next action' }).locator('..').getByText('Resolve the check', { exact: true }).waitFor(); await section.getByText(/2026-11-12/).first().waitFor(); await dialog.locator('.semantic-pill').filter({ hasText: /Fail$/ }).waitFor(); await dialog.locator('.semantic-pill').filter({ hasText: /Pending$/ }).waitFor(); await dialog.getByText('Required input is unavailable.', { exact: true }).waitFor(); assert.equal(await sections.count(), 3, 'parent and both check contexts render without a duplicate child context'); assert.equal(await dialog.getByText('Needed by: No date', { exact: true }).count(), 2, 'each check context has no per-check date')

    await page.goto(`${base}/projects/${project.projectNumber}/submissions?status=Checking&q=permit&sort=targetDate:asc&panel=SubmissionCheck:${checkId}`, { waitUntil: 'domcontentloaded' }); dialog = page.getByRole('dialog'); await dialog.getByRole('heading', { name: /SUB-001 · Permit submission/ }).waitFor(); section = dialog.locator('section[aria-label="Record context"]'); await section.locator('dt').filter({ hasText: 'Owner' }).locator('..').getByText('Diane Review', { exact: true }).waitFor(); await section.locator('dt').filter({ hasText: 'Next actor' }).locator('..').getByText('Diane Review', { exact: true }).waitFor(); assert.equal(await dialog.locator('section[aria-label="Record context"]').count(), 1, 'exact check context appears once at the top')
    const origin = page.url(); const scroll = await page.locator('main#content').evaluate(el => { el.scrollTop = 160; return el.scrollTop }); assert.equal(scroll, 160); const dialogScroll = await dialog.locator('[data-slot="dialog-body"]').evaluate(el => { el.scrollTop = 80; return el.scrollTop }); assert.equal(dialogScroll, 80); await dialog.locator('section[aria-label="Record context"]').getByRole('link', { name: /D-001 · Synthetic deliverable/ }).click(); await page.waitForURL(new RegExp(`panel=Deliverable:${sourceDeliverable}$`)); await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click(); await page.waitForURL(origin); await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected, scroll); await page.waitForFunction(expected => document.querySelector('[role="dialog"] [data-slot="dialog-body"]')?.scrollTop === expected, dialogScroll); assert.equal(new URL(page.url()).searchParams.get('status'), 'Checking'); assert.equal(new URL(page.url()).searchParams.get('q'), 'permit'); assert.equal(new URL(page.url()).searchParams.get('sort'), 'targetDate:asc')

    checkContext = { ...checkContext, inputs: [], nextAction: 'not_assessed', unavailableInputCount: 1 }; await page.reload(); await page.getByRole('dialog').getByText('1 input details unavailable; their next steps are not assessed.', { exact: true }).waitFor();
    childVersion = 1; await page.reload(); await page.getByRole('dialog').getByRole('alert').waitFor(); assert.equal(await page.getByRole('dialog').locator('section[aria-label="Record context"]').count(), 0); childVersion = 2;
    const beforeMissing = parentGetCount; await page.goto(`${base}/projects/${project.projectNumber}/submissions?status=Checking&q=permit&sort=targetDate:asc&panel=SubmissionCheck:${missingCheckId}`, { waitUntil: 'domcontentloaded' }); dialog = page.getByRole('dialog'); await dialog.getByRole('heading', { name: 'Check' }).waitFor(); await dialog.getByRole('alert').waitFor(); assert.equal(parentGetCount, beforeMissing, 'missing check does not fall back to parent submission GET')
    assert.deepEqual(mock.unknownGets, [], `unmocked GETs: ${mock.unknownGets.join('; ')}`); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [], errors.map(error => error.stack ?? error.message).join('\n')); console.log('PASS: submission package and check context')
  } catch (error) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw error } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
