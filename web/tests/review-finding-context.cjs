const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { coordinationOptions, coordinationSource, reviewRow, reviewPage, reviewPackageRow, reviewRoundRow, reviewAssignmentRow, reviewFindingRow, reviewFindingEventRow, reviewManifestRow, reviewDetail, sourceRevisionRow } = require('./reviews.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const alex = me.id, marc = '00000000-0000-0000-0000-000000000013', diane = '00000000-0000-0000-0000-000000000007'
const pkgId = '00000000-0000-0000-0000-000000000061', pkgBId = '00000000-0000-0000-0000-000000000081'
const oldRound = '00000000-0000-0000-0000-000000000062', currentRound = '00000000-0000-0000-0000-000000000063', roundB = '00000000-0000-0000-0000-000000000082'
const findingId = '00000000-0000-0000-0000-000000000064', historyId = '00000000-0000-0000-0000-000000000071', otherId = '00000000-0000-0000-0000-000000000067', missingId = '00000000-0000-0000-0000-000000000099'
const oldSourceId = '00000000-0000-0000-0000-000000000065', sourceId = '00000000-0000-0000-0000-000000000075'
const text = 'Confirm the corridor clearance', historicalText = 'Earlier clearance comment'
const person = (id, name, state = 'Active') => ({ id, name, state, kind: 'User' })
const context = (overrides = {}) => ({ owner: person(alex, 'Alex Chen'), nextActors: [person(alex, 'Alex Chen')], neededBy: null,
  nextAction: 'respond_to_comment', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [], ...overrides })
let mode = 'open', parentGets = 0, childGets = 0
const commentContext = () => context(mode === 'open' ? {} : mode === 'responded'
  ? { nextActors: [person(marc, 'Marc Lead')], nextAction: 'verify_closure' }
  : mode === 'inactive' ? { nextActors: [person(marc, 'Marc Lead', 'Inactive')], actorState: 'Inactive', nextAction: 'not_assessed' }
  : mode === 'withdrawn' ? { nextActors: [person(diane, 'Diane Review')], nextAction: 'acknowledge_withdrawal' }
  : { nextActors: [], actorState: 'None', nextAction: 'none', terminal: true })
const historyContext = () => context({ nextActors: [], actorState: 'Unknown', nextAction: 'not_assessed' })
const oldSource = sourceRevisionRow({ id: oldSourceId, projectId: project.id, sourceKey: 'D-001', title: 'Old survey source', revision: 'A', authorIds: [alex] })
const source = sourceRevisionRow({ id: sourceId, projectId: project.id, sourceKey: 'D-001', title: 'Current survey source', revision: 'B', authorIds: [alex] })
const pkg = reviewPackageRow({ id: pkgId, projectId: project.id, key: 'RV-001', title: 'Corridor review', purpose: 'Check corridor interfaces', coordinatorId: diane, status: 'Draft', currentRoundId: currentRound, roundNumber: 2 })
const pkgB = reviewPackageRow({ id: pkgBId, projectId: project.id, key: 'RV-002', title: 'Drainage review', coordinatorId: diane, status: 'Draft', currentRoundId: roundB })
const rounds = [reviewRoundRow({ id: currentRound, packageId: pkgId, number: 2, status: 'Draft', startedAt: null }), reviewRoundRow({ id: oldRound, packageId: pkgId, number: 1, status: 'Superseded', startedAt: '2026-10-01T12:00:00Z' })]
const comment = () => reviewFindingRow({ id: findingId, projectId: project.id, packageId: pkgId, roundId: currentRound, carriedFromId: historyId,
  sourceRevisionId: oldSourceId, projectDisciplineId: project.disciplines[0].id, originatorId: diane, resolverId: alex, verifierId: marc,
  severity: 'Blocking', status: mode === 'open' ? 'Open' : mode === 'responded' || mode === 'inactive' ? 'Responded' : 'Withdrawn', text,
  response: mode === 'responded' || mode === 'inactive' ? 'Clearance confirmed' : null, evidenceUrl: mode === 'responded' || mode === 'inactive' ? 'https://example.test/clearance' : null,
  withdrawalAcknowledgedBy: mode === 'acknowledged' ? diane : null, context: commentContext() })
const historical = () => reviewFindingRow({ id: historyId, projectId: project.id, packageId: pkgId, roundId: oldRound, sourceRevisionId: oldSourceId,
  projectDisciplineId: project.disciplines[0].id, originatorId: diane, resolverId: alex, verifierId: marc, severity: 'Blocking', status: 'Open', text: historicalText, context: historyContext() })

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui, errors = []
  page.setDefaultTimeout(12000)
  page.on('pageerror', e => errors.push(e.message))
  page.on('console', m => { if (m.type() === 'error' && !/server responded with a status of 404/.test(m.text())) errors.push(m.text()) })
  try {
    const options = coordinationOptions({ actorId: alex, canWrite: true,
      people: [{ id: alex, displayName: 'Alex Chen' }, { id: marc, displayName: 'Marc Lead' }, { id: diane, displayName: 'Diane Review' }],
      sources: [coordinationSource({ revision: oldSource }), coordinationSource({ revision: source, isCurrent: true })] })
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, r => r.fulfill({ json: options }))
    await page.route(`**/api/v1/projects/${project.id}/reviews**`, r => {
      const path = new URL(r.request().url()).pathname
      if (path.endsWith(`/${pkgBId}`)) return r.fulfill({ json: reviewDetail({ package: pkgB, rounds: [reviewRoundRow({ id: roundB, packageId: pkgBId, status: 'Draft' })], assignments: [], findings: [], manifest: [], events: [], canWrite: false }) })
      if (path.endsWith(`/${pkgId}`)) { parentGets++; return r.fulfill({ json: reviewDetail({ package: pkg, rounds,
        actorId: alex, canWrite: true, canCoordinate: false, canManage: false, canReassignVerifier: false,
        assignments: [reviewAssignmentRow({ id: '00000000-0000-0000-0000-000000000066', roundId: currentRound, projectDisciplineId: project.disciplines[0].id, reviewerId: diane, dueDate: '2026-12-15' })],
        findings: [comment(), historical(), reviewFindingRow({ id: otherId, projectId: project.id, packageId: pkgId, roundId: currentRound, sourceRevisionId: sourceId, projectDisciplineId: project.disciplines[0].id,
          originatorId: diane, resolverId: alex, verifierId: marc, severity: 'Advisory', status: 'Withdrawn', text: 'Separate advisory comment', context: context({ nextActors: [], actorState: 'None', nextAction: 'none', terminal: true }) })],
        manifest: [reviewManifestRow({ id: '00000000-0000-0000-0000-000000000068', roundId: oldRound, sourceRevisionId: oldSourceId, authorIds: [alex], source: oldSource }),
          reviewManifestRow({ id: '00000000-0000-0000-0000-000000000069', roundId: currentRound, sourceRevisionId: sourceId, authorIds: [alex], source })],
        events: [reviewFindingEventRow({ id: '00000000-0000-0000-0000-000000000070', findingId: historyId, action: 'Open', reason: 'Historical event only' }),
          reviewFindingEventRow({ id: '00000000-0000-0000-0000-000000000072', findingId, action: 'Open', reason: 'Current event only' })] }) }) }
      return r.fulfill({ json: reviewPage([reviewRow({ id: pkgId, key: pkg.key, title: pkg.title, status: 'Draft', coordinatorId: diane, roundNumber: 2 }),
        reviewRow({ id: pkgBId, key: pkgB.key, title: pkgB.title, status: 'Draft', coordinatorId: diane, roundNumber: 1 }),
        ...Array.from({ length: 50 }, (_, i) => reviewRow({ id: `00000000-0000-0000-0002-${String(i + 1).padStart(12, '0')}`, key: `RV-${i + 3}`, title: `Scrollable review ${i + 1}`, status: 'Draft', coordinatorId: diane }))]) })
    })
    await page.route(`**/api/v1/projects/${project.id}/review-findings/${findingId}`, r => { childGets++; return r.fulfill({ json: { id: findingId, packageId: pkgId, roundId: currentRound, key: pkg.key, title: text, context: commentContext() } }) })
    await page.route(`**/api/v1/projects/${project.id}/review-findings/${historyId}`, r => { childGets++; return r.fulfill({ json: { id: historyId, packageId: pkgId, roundId: oldRound, key: pkg.key, title: historicalText, context: historyContext() } }) })
    await page.route(`**/api/v1/projects/${project.id}/review-findings/${missingId}`, r => r.fulfill({ status: 404, json: { error: 'not_found', message: 'Not found' } }))
    const parentUrl = `${base}/projects/${project.projectNumber}/reviews?status=Draft&sort=round:asc&panel=ReviewPackage:${pkgId}`
    const childUrl = `${base}/projects/${project.projectNumber}/reviews?status=Draft&sort=round:asc&panel=ReviewFinding:${findingId}`
    const historicalUrl = childUrl.replace(findingId, historyId)
    const dialog = () => page.getByRole('dialog')
    const exactSection = () => dialog().locator('section[aria-label="Record context"]')
    const article = () => dialog().locator('article').filter({ has: page.getByText(text, { exact: true }) })
    const fact = (section, label) => section.locator('dt').filter({ hasText: new RegExp(`^${label}$`) }).locator('..')
    const atChild = id => url => url.searchParams.get('panel') === `ReviewFinding:${id}`
    me.flags = { ...me.flags, screen_patterns: false }
    await page.goto(parentUrl); await dialog().getByRole('heading', { name: /RV-001/ }).waitFor(); assert.equal(await exactSection().count(), 0)
    await page.goto(childUrl); await page.getByRole('button', { name: /RV-001.*Corridor review/ }).waitFor(); assert.equal(childGets, 0); assert.equal(await dialog().count(), 0)
    me.flags.screen_patterns = true
    await page.goto(parentUrl); const inline = article().locator('section[aria-label="Record context"]')
    await fact(inline, 'Owner').getByText('You', { exact: true }).waitFor(); await fact(inline, 'Next actor').getByText('You', { exact: true }).waitFor()
    await fact(inline, 'Next action').getByText('Respond', { exact: true }).waitFor(); await inline.getByText(/No date/).waitFor(); assert.equal(await inline.getByText('2026-12-15').count(), 0)
    await page.goto(childUrl); await fact(exactSection(), 'Next action').getByText('Respond', { exact: true }).waitFor(); assert.equal(await dialog().locator('article').count(), 1)
    assert.equal(await dialog().getByLabel('Review round and history').count(), 0)
    for (const [state, actor, action] of [['responded', 'Marc Lead', 'Verify closure'], ['inactive', 'Marc Lead (Inactive)', 'Not assessed'], ['withdrawn', 'Diane Review', 'Acknowledge withdrawal']]) {
      mode = state; await page.reload(); await fact(exactSection(), 'Next actor').getByText(actor, { exact: true }).waitFor()
      await fact(exactSection(), 'Next action').getByText(action, { exact: true }).waitFor(); await fact(exactSection(), 'Owner').getByText('You', { exact: true }).waitFor()
    }
    mode = 'acknowledged'; await page.reload(); await fact(exactSection(), 'Next action').getByText('No next action', { exact: true }).waitFor(); assert.equal(await fact(exactSection(), 'Next actor').count(), 0)
    await exactSection().getByText(/No date/).waitFor()
    const beforeMissing = parentGets
    await page.goto(childUrl.replace(findingId, missingId)); await dialog().getByRole('heading', { name: 'Review comment', exact: true }).waitFor(); await dialog().getByRole('alert').waitFor()
    assert.equal(parentGets, beforeMissing); assert.equal(await exactSection().count(), 0)
    // Copied historical comment selects its actual round and history; no current-round mutation is offered.
    await page.goto(historicalUrl); await fact(exactSection(), 'Next action').getByText('Not assessed', { exact: true }).waitFor()
    await fact(exactSection(), 'Next actor').getByText('Unknown', { exact: true }).waitFor(); assert.equal(await dialog().locator('article').count(), 1)
    await dialog().getByText('This earlier round is preserved and cannot be edited.', { exact: true }).waitFor(); assert.equal(await dialog().getByLabel('Review round and history').count(), 0)
    assert.equal(await dialog().getByRole('button', { name: 'Respond', exact: true }).count(), 0)
    await dialog().locator('article details summary').click(); await dialog().getByText('Historical event only', { exact: true }).waitFor(); assert.equal(await dialog().getByText('Current event only', { exact: true }).count(), 0)
    // Opening a historical article returns to its selected package round and real scroll position.
    mode = 'open'; await page.goto(parentUrl); await dialog().getByLabel('Review round and history').selectOption(oldRound)
    const parentHistory = page.url(); await dialog().getByText(historicalText, { exact: true }).waitFor()
    const scroll = await page.locator('main#content').evaluate(el => { el.scrollTop = 160; return el.scrollTop }); assert.equal(scroll, 160)
    await dialog().locator('article').filter({ has: page.getByText(historicalText, { exact: true }) }).getByRole('link', { name: 'Open comment', exact: true }).click()
    await page.waitForURL(atChild(historyId)); await fact(exactSection(), 'Next action').getByText('Not assessed', { exact: true }).waitFor()
    await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(parentHistory)
    await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected, scroll)
    assert.equal(new URL(page.url()).searchParams.get('status'), 'Draft'); assert.equal(new URL(page.url()).searchParams.get('sort'), 'round:asc')
    // Ordinary close/open clears A's round so package B opens its own current round.
    await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel'))
    assert.equal(new URL(page.url()).searchParams.has('round'), false)
    await page.getByRole('button', { name: /RV-002.*Drainage review/ }).click(); await dialog().getByRole('heading', { name: /RV-002.*Drainage review/ }).waitFor()
    assert.equal(await dialog().getByRole('alert').count(), 0); assert.equal(await dialog().getByLabel('Review round and history').inputValue(), roundB)
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: review comment context, exact historical round, withdrawal, return scroll and package isolation')
  } finally { await ui.browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
