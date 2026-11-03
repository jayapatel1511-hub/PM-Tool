// S05-A: canonical check ordering, exact source actions/return, inline revision truth and explicit partial capture recovery.
const assert = require('node:assert/strict')
const { newMockPage, me, project, panelIds } = require('./platform-fixtures.cjs')
const { submissionListRow, submissionPage } = require('./submissions.fixture.cjs')
const { coordinationOptions, sourceRevisionRow, reviewPackageRow, reviewRoundRow, reviewFindingRow, reviewDetail, reviewPage, reviewRow } = require('./reviews.fixture.cjs')
const { handoffRow, handoffPage, handoffOptions, handoffDetail } = require('./handoffs.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use local preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const pkg = id(2101), handoff = id(2102), change = id(2103), revisionA = id(2104), revisionB = id(2105), finding = id(2106), review = id(2107), round = id(2108)
const otherDeliverable = id(2109), ownerId = id(2110), receiverId = id(2111), verifierId = id(2112)
const person = (id, name) => ({ id, name, state: 'Active', kind: 'User' })
const coordinator = person(me.id, 'Alex Chen'), checker = person(ownerId, 'Diane Checking Owner'), receiver = person(receiverId, 'Omar Receiving Owner'), verifier = person(verifierId, 'Priya Verifier')
const sourceFact = (kind, id, key, title, nextAction, actor) => ({ kind, id, key, title, neededBy: '2026-11-10', nextAction, nextActors: [actor], actorState: 'Active', primary: false })
const handoffSource = sourceFact('Handoff', handoff, 'P-MOCK-H003', 'Survey handoff', 'accept', receiver)
const changeSource = sourceFact('ChangeAssessment', change, 'P-MOCK-CN001', 'Assess revised electrical source', 'assess_change', checker)
const findingSource = sourceFact('ReviewFinding', finding, 'P-MOCK-RC014', 'Verify corridor comment', 'verify_closure', verifier)
const context = (owner, inputs = [], terminal = false) => ({ owner, nextActors: terminal ? [] : [owner], neededBy: null, nextAction: terminal ? 'none' : inputs.length ? 'resolve_check' : 'not_assessed', terminal,
  actorState: terminal ? 'None' : 'Active', requiresReviewGate: false, nextTransitions: [], inputs })
const stamp = '2026-10-31T12:00:00Z'
let mode = 'full', findingMode = false, issuedMode = false
const packageRow = { id: pkg, projectId: project.id, key: 'P-MOCK-SUB001', title: 'Permit submission', purpose: 'Submit the coordinated electrical issue', recipientReference: 'Municipality', coordinatorId: me.id,
  milestoneId: panelIds.milestone, targetDate: '2026-11-12', status: 'Checking', manifestVersion: 1, rowVersion: 1 }
function fullDetail() {
  const kinds = ['Current Revision', 'Handoff', findingMode ? 'Blocking Findings' : 'Change Assessment', 'Deliverable', 'Independent Review', 'Access', 'Blocking Findings', 'Applicability']
  const states = ['Fail', 'Pending', findingMode ? 'Fail' : 'Pending', 'Pass', 'Pass', 'Pass', 'Pass', 'Pass']
  const source = index => index === 1 ? [handoffSource] : index === 2 ? [findingMode ? findingSource : changeSource] : []
  const checks = kinds.map((kind, i) => ({ id: id(2200 + i), projectId: project.id, packageId: pkg, manifestVersion: 1, kind,
    sourceId: i === 0 ? panelIds.deliverable : null, projectDisciplineId: project.disciplines[0].id, ownerId: i === 7 ? me.id : ownerId, required: i !== 7,
    status: i === 7 ? 'Pass' : 'Pending', effectiveStatus: states[i], evidenceRule: i === 7 ? 'Optional traffic note' : kind,
    evidenceUrl: null, reason: null, approvedBy: null, approvedAt: null, rowVersion: 1, context: context(i === 7 ? coordinator : checker, source(i), states[i] === 'Pass') }))
  const reasons = ['Registered revision changed', 'The handoff is not accepted', findingMode ? 'The responded comment awaits independent verification' : 'The change assessment is pending']
  const checkFacts = checks.map((check, i) => ({ checkId: check.id, state: states[i], known: true, blocking: i < 3, checkingOwner: i === 7 ? coordinator : checker,
    disciplineId: project.disciplines[0].id, disciplineName: 'Civil', reasons: i < 3 ? [{ code: `test.reason.${i}`, message: reasons[i], sourceExpected: true, source: source(i)[0] ?? null }] : [] }))
  const manifest = [{ id: id(2250), packageId: pkg, projectId: project.id, manifestVersion: 1, deliverableId: panelIds.deliverable, sourceRevisionId: revisionA, reviewRoundId: round, required: true, rowVersion: 1 },
    { id: id(2251), packageId: pkg, projectId: project.id, manifestVersion: 1, deliverableId: otherDeliverable, sourceRevisionId: id(2252), reviewRoundId: null, required: true, rowVersion: 1 }]
  const member = (id, deliverableId, key, title, recorded, current) => ({ id, deliverableId, key, title, required: true, status: 'In Progress', available: true,
    recordedRevision: { id: recorded, revision: 'A', url: 'https://example.test/recorded-A.pdf' }, currentRevision: { id: current, revision: current === recorded ? 'A' : 'B', url: 'https://example.test/current.pdf' }, revisionMismatch: current !== recorded })
  return { package: packageRow, effectiveStatus: 'Checking', readiness: { ready: false, fingerprint: 'fixture', blockers: reasons.map((message, i) => ({ kind: kinds[i], sourceId: i === 0 ? panelIds.deliverable : i === 1 ? handoff : change,
    disciplineId: project.disciplines[0].id, ownerId: receiverId, code: `test.reason.${i}`, message, sourcePath: `/projects/${project.projectNumber}/handoffs?panel=Handoff:${handoff}` })) }, checks, manifest, evidence: [], issue: null,
    canCoordinate: false, canAuthorise: false, context: { ...context(coordinator), nextActors: [checker], neededBy: packageRow.targetDate, nextAction: 'resolve_check' },
    delivery: { summary: { available: true, blockingCheckCount: 3, packageProblemCount: 0, unknownCheckCount: 0 }, evaluatedAt: stamp, checksComplete: true, manifestComplete: true, issueAvailability: 'NotIssued',
      checks: checkFacts, packageProblems: [], manifest: [member(manifest[0].id, panelIds.deliverable, 'P-MOCK-D001', 'Electrical issue sheet', revisionA, revisionB), member(manifest[1].id, otherDeliverable, 'P-MOCK-D002', 'Unrelated civil sheet', id(2252), id(2252))],
      statusCounts: [{ status: 'In Progress', count: 2 }], unavailableDeliverables: 0 } }
}
function responseDetail() {
  const d = fullDetail()
  if (issuedMode) {
    d.package = { ...d.package, status: 'Issued' }; d.effectiveStatus = 'Issued'; d.readiness = null
    d.context = { ...context(coordinator, [], true), neededBy: packageRow.targetDate }
    d.checks = d.checks.map(check => ({ ...check, status: 'Pass', effectiveStatus: 'Pass', context: context(checker, [], true) }))
    d.issue = { id: id(2300), projectId: project.id, packageId: pkg, manifestVersion: 1, manifestSnapshot: JSON.stringify(d.manifest), checkSnapshot: JSON.stringify(d.checks),
      authorisedBy: verifierId, authorisedAt: stamp, destination: 'Municipality', transmittalUrl: 'https://example.test/issued-transmittal' }
    d.delivery = { ...d.delivery, summary: { available: false, blockingCheckCount: null, packageProblemCount: 0, unknownCheckCount: 0 }, issueAvailability: 'Available',
      checks: d.delivery.checks.map(check => ({ ...check, state: 'Pass', blocking: false, reasons: [] })) }
  }
  if (mode === 'summary') { delete d.checks; delete d.manifest; delete d.issue; delete d.context; d.delivery = { summary: d.delivery.summary }; return d }
  if (mode === 'partial') { d.checks = d.checks.slice(0, 3); delete d.manifest; delete d.issue; d.delivery.checks = d.delivery.checks.slice(0, 3); d.delivery.checksComplete = false; d.delivery.manifestComplete = false; delete d.delivery.manifest; delete d.delivery.statusCounts; d.delivery.issueAvailability = 'Unavailable' }
  return d
}

;(async () => {
  const flags = { ...me.flags }
  const capabilities = { ...me.capabilities }, originalProjectStatus = project.status, originalCreatePermission = project.permissions.createSubmission
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = []; page.on('pageerror', error => errors.push(error.message))
  const writes = []; page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/') && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method())) writes.push(`${request.method()} ${new URL(request.url()).pathname}`) })
  try {
    me.flags.screen_patterns = true; me.flags.live_events = false
    const options = coordinationOptions({ actorId: me.id, canWrite: false, people: [coordinator, checker, receiver, verifier].map(p => ({ id: p.id, displayName: p.name })), disciplines: [{ id: project.disciplines[0].id, name: 'Civil' }] })
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, route => route.fulfill({ json: options }))
    await page.route(`**/api/v1/projects/${project.id}/submissions**`, route => route.fulfill({ json: new URL(route.request().url()).pathname.endsWith(`/${pkg}`) ? responseDetail()
      : submissionPage([submissionListRow({ id: pkg, key: packageRow.key, title: packageRow.title, status: issuedMode ? 'Issued' : 'Checking', coordinatorId: me.id, targetDate: packageRow.targetDate, blockerCount: issuedMode ? 0 : 7, delivery: responseDetail().delivery.summary })]) }))
    const h = handoffRow({ id: handoff, key: handoffSource.key, title: handoffSource.title, status: 'Submitted', targetDeliverableId: panelIds.deliverable })
    await page.route(`**/api/v1/projects/${project.id}/handoffs**`, route => { const path = new URL(route.request().url()).pathname; return route.fulfill({ json: path.endsWith('/options') ? handoffOptions() : path.endsWith(`/${handoff}`) ? handoffDetail({ row: h }) : handoffPage([h]) }) })
    const revision = sourceRevisionRow({ id: revisionA, projectId: project.id, sourceKey: 'P-MOCK-D001', title: 'Electrical issue sheet', revision: 'A' })
    const reviewPackage = reviewPackageRow({ id: review, projectId: project.id, key: 'P-MOCK-RV001', title: 'Electrical review', currentRoundId: round, status: 'In Review' })
    const reviewFinding = reviewFindingRow({ id: finding, packageId: review, roundId: round, sourceRevisionId: revisionA, projectDisciplineId: project.disciplines[0].id,
      resolverId: receiverId, verifierId, originatorId: verifierId, text: findingSource.title, status: 'Responded', response: 'Corridor updated', evidenceUrl: 'https://example.test/response.pdf' })
    await page.route(`**/api/v1/projects/${project.id}/review-findings/${finding}`, route => route.fulfill({ json: { id: finding, packageId: review, roundId: round, key: findingSource.key, title: findingSource.title,
      context: { ...context(receiver), nextActors: [verifier], nextAction: 'verify_closure' } } }))
    await page.route(`**/api/v1/projects/${project.id}/reviews**`, route => { const path = new URL(route.request().url()).pathname; return route.fulfill({ json: path.endsWith('/options') ? options
      : path.endsWith(`/${review}`) ? reviewDetail({ package: reviewPackage, actorId: me.id, rounds: [reviewRoundRow({ id: round, number: 1, status: 'In Review', startedAt: stamp })], findings: [reviewFinding],
        manifest: [{ id: id(2299), roundId: round, sourceRevisionId: revisionA, deliverableId: panelIds.deliverable, authorIds: [], source: revision }] }) : reviewPage([reviewRow({ id: review, key: reviewPackage.key, title: reviewPackage.title })]) }) })
    const url = `${base}/projects/${project.projectNumber}/submissions?status=Checking&q=permit&sort=targetDate%3Aasc&panel=SubmissionPackage:${pkg}`
    const panel = () => page.getByRole('dialog').last()
    const delivery = () => panel().locator('[data-delivery-presentation]')
    await page.setViewportSize({ width: 1600, height: 900 })
    await page.goto(url, { waitUntil: 'domcontentloaded' })
    await delivery().getByText('3 blockers', { exact: true }).waitFor()
    const states = await delivery().locator('[data-delivery-state]').evaluateAll(nodes => nodes.map(node => node.getAttribute('data-delivery-state')))
    assert.deepEqual(states, ['Fail', 'Pending', 'Pass'])
    assert.equal(await delivery().locator('[data-delivery-state="Fail"] [data-delivery-check]').count(), 1)
    assert.equal(await delivery().locator('[data-delivery-state="Pending"] [data-delivery-check]').count(), 2)
    assert.equal(await delivery().locator('[data-delivery-state="Pass"] [data-delivery-check]').count(), 5)
    await delivery().locator('[data-delivery-state="Pending"]').getByRole('heading').getByText(checker.name, { exact: true }).waitFor()
    assert.equal((await delivery().innerText()).includes('%'), false, 'counts never imply Ready through a percentage')
    const manifest = () => delivery().locator('[data-delivery-manifest]')
    assert.equal(await manifest().getAttribute('open'), null)
    await manifest().locator('summary').focus(); await page.keyboard.press('Enter')
    await manifest().getByText('Manifest revision', { exact: true }).first().waitFor()
    const first = manifest().locator('article').filter({ hasText: 'P-MOCK-D001' })
    const other = manifest().locator('article').filter({ hasText: 'P-MOCK-D002' })
    assert.equal(await first.getByText('Manifest revision', { exact: true }).locator('..').locator('dd').innerText(), 'A')
    assert.equal(await first.getByText('Current registered revision', { exact: true }).locator('..').locator('dd').innerText(), 'B')
    assert.equal(await other.getByText('Manifest revision', { exact: true }).locator('..').locator('dd').innerText(), 'A')
    assert.equal(await other.getByText('Current registered revision', { exact: true }).locator('..').locator('dd').innerText(), 'A')
    assert.equal(await other.locator('.semantic-pill[data-semantic-tone="warn"]').filter({ hasText: /^\s*▲?\s*Revision mismatch\s*$/ }).count(), 0, 'D001 failure never attaches to D002')
    assert.equal(await manifest().locator('.semantic-pill[data-semantic-tone="warn"]').filter({ hasText: /^\s*▲?\s*Revision mismatch\s*$/ }).count(), 1)
    assert.ok(await delivery().evaluate(node => node.querySelector('[data-delivery-checklist]').compareDocumentPosition(node.querySelector('[data-delivery-manifest]')) & Node.DOCUMENT_POSITION_FOLLOWING))

    const returnScroll = await page.locator('main#content').evaluate(node => { node.scrollTop = 100; return node.scrollTop })
    const accept = delivery().getByRole('link', { name: /^Accept.*P-MOCK-H003/ })
    await accept.click(); await page.waitForURL(new RegExp(`panel=Handoff:${handoff}`))
    await panel().getByRole('heading', { name: new RegExp(handoffSource.title) }).waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(url)
    await page.waitForFunction(expected => document.querySelector('main#content').scrollTop === expected, returnScroll)

    findingMode = true; await page.reload()
    const verify = delivery().getByRole('link', { name: /^Verify closure.*P-MOCK-RC014/ })
    await verify.waitFor(); assert.ok((await verify.getAttribute('href')).includes(`ReviewFinding:${finding}`))
    await verify.click(); await page.waitForURL(new RegExp(`panel=ReviewFinding:${finding}`))
    await panel().getByText(findingSource.title, { exact: true }).first().waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(url)

    mode = 'summary'; await page.reload()
    await delivery().getByText('Complete checklist unavailable', { exact: true }).waitFor()
    assert.equal(await delivery().locator('[data-delivery-check]').count(), 0)
    assert.equal(await delivery().locator('[data-delivery-state="Pass"]').count(), 0)
    await delivery().getByText('Manifest status counts unavailable', { exact: true }).waitFor()
    await delivery().getByText('Issue record unavailable', { exact: true }).waitFor()
    await manifest().locator('summary').click(); await manifest().getByText('Complete manifest unavailable', { exact: true }).waitFor()
    assert.equal(await manifest().locator('article').count(), 0)
    mode = 'partial'; await page.reload()
    await delivery().getByText('Complete checklist unavailable', { exact: true }).waitFor()
    assert.equal(await delivery().locator('[data-delivery-state="Pass"]').count(), 0, 'missing passing-check details are not fabricated from totals')

    mode = 'full'; findingMode = false
    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 }); await page.reload(); await delivery().getByText('3 blockers', { exact: true }).waitFor()
      assert.ok(await panel().evaluate(node => node.scrollWidth <= node.clientWidth + 1), `dialog reflows at ${width}`)
      assert.equal(await delivery().locator('[data-delivery-check]').count(), 8)
    }
    me.flags.screen_patterns = false; await page.reload()
    await panel().getByRole('heading', { name: /Permit submission/ }).waitFor()
    assert.equal(await panel().locator('[data-delivery-presentation]').count(), 0)
    assert.ok(await panel().getByRole('heading', { name: 'Required revision manifest', exact: true }).evaluate(node => node.compareDocumentPosition([...node.closest('[role="dialog"]').querySelectorAll('h3')].find(h => h.textContent.startsWith('Readiness blockers'))) & Node.DOCUMENT_POSITION_FOLLOWING), 'off keeps legacy manifest-first order')

    // An issued package may visibly offer a refused command for account/project-status reasons.
    // The data-completeness guard must compose with that permission refusal in both flag states.
    issuedMode = true; mode = 'full'; findingMode = false
    const supersede = () => panel().getByRole('button', { name: 'Prepare superseding issue', exact: true })
    const deniedCases = [
      { name: 'read-only account', readOnly: true, status: 'Active', reason: 'Read-only account cannot prepare a superseding issue' },
      { name: 'complete project', readOnly: false, status: 'Complete', reason: 'Complete project cannot prepare a superseding issue' },
    ]
    for (const enabled of [true, false]) {
      me.flags.screen_patterns = enabled
      for (const denied of deniedCases) {
        me.capabilities.readOnly = denied.readOnly; project.status = denied.status
        project.permissions.createSubmission = { ok: false, reason: denied.reason }
        await page.reload(); await supersede().waitFor()
        assert.equal(await supersede().isDisabled(), true, `${denied.name}, flag ${enabled}: offer refusal remains authoritative`)
        assert.equal(await supersede().getAttribute('title'), denied.reason)
        const beforeWrites = writes.length
        await supersede().evaluate(button => button.click()) // Native activation respects disabled; no force dispatch bypasses it.
        assert.equal(await panel().locator('form').count(), 0, 'denied offer never opens the superseding form')
        assert.equal(await panel().locator('#sub-title').count(), 0)
        assert.equal(writes.length, beforeWrites, 'denied offer sends no mutation')
      }
      // Positive control: composing the refusal guard must preserve an allowed, complete offer.
      me.capabilities.readOnly = false; project.status = 'Active'; project.permissions.createSubmission = { ok: true, reason: null }
      await page.reload(); await supersede().waitFor(); assert.equal(await supersede().isDisabled(), false)
      await supersede().click(); await panel().locator('#sub-title').waitFor()
      await panel().getByRole('button', { name: 'Cancel', exact: true }).click()
      await panel().getByRole('heading', { name: /Permit submission/ }).waitFor()
    }
    // An allowed offer still cannot prefill from an incomplete issued capture when the pattern is enabled.
    me.flags.screen_patterns = true; mode = 'partial'
    await page.reload(); await supersede().waitFor(); assert.equal(await supersede().isDisabled(), true)
    await delivery().getByText('Complete checklist unavailable', { exact: true }).waitFor()
    const beforePartialWrites = writes.length
    await supersede().evaluate(button => button.click())
    assert.equal(await panel().locator('form').count(), 0)
    assert.equal(writes.length, beforePartialWrites)
    assert.deepEqual(writes, [], 'the read/return and permission cases do not write operational records')
    assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: S05-A blocker ordering/count, checking-owner groups, exact Handoff/Finding action and return, manifest isolation/comparison, unavailable captures, keyboard/reflow, issued denied-offer and allowed controls in both flags, partial prefill guard, unchanged-off presentation')
  } finally { me.flags = flags; me.capabilities = capabilities; project.status = originalProjectStatus; project.permissions.createSubmission = originalCreatePermission; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
