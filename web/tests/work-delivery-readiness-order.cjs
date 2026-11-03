// S05-B actual-app/mock-API acceptance; canonical state/source fixtures, independent paging and exact inspector return.
const assert = require('node:assert/strict')
const { newMockPage, me, project, panelIds } = require('./platform-fixtures.cjs')
const { coordinationOptions, coordinationTask } = require('./reviews.fixture.cjs')
const { readinessWindow, readinessAssessment, readinessCheck, readinessDetail, readinessConstraintRow, readyOutput } = require('./readiness.fixture.cjs')
const { commitmentList } = require('./weekly-commitments.fixture.cjs')
const { decisionDetail, decisionRow } = require('./decisions.fixture.cjs')
const { handoffRow, handoffDetail, handoffOptions, handoffPage } = require('./handoffs.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use a local preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const stamp = '2026-10-31T12:00:00Z', decision = id(4100), handoff = id(4101), constraint = id(4102)
const owner = { id: id(4103), name: 'Diane Production Owner', state: 'Active', kind: 'User' }
const decisionOwner = { id: id(4104), name: 'Omar Decision Owner', state: 'Active', kind: 'User' }
const removalOwner = { id: id(4105), name: 'Priya Removal Owner', state: 'Active', kind: 'User' }
const pm = { id: id(4106), name: 'Priya Current PM', state: 'Active', kind: 'User' }
const lead = { id: id(4107), name: 'Marc Current Civil Lead', state: 'Active', kind: 'User' }
const codes = ['Handoff', 'Predecessor', 'Decision', 'Basis', 'Production Owner', 'Production Capacity', 'Review Capacity', 'Review Gate', 'Submission Gate']
let mode = 'full', proposed = false, external = false
const fact = (kind, id, key, title, nextAction, actor) => ({ kind, id, key, title, neededBy: '2026-11-06', nextAction, nextActors: [actor], actorState: 'Active', primary: false })
const decisionFact = () => fact('Decision', decision, 'P-MOCK-DEC001', 'Confirm the exact corridor', 'decide', external ? { id: id(4199), name: 'Municipal Approver', state: 'External', kind: 'ExternalParty' } : decisionOwner)
const handoffFact = () => fact('Handoff', handoff, 'P-MOCK-H003', 'Survey handoff for receiving work', 'accept', owner)
const constraintFact = () => fact('WorkConstraint', constraint, 'P-MOCK-CT001', 'Verify the survey independently', proposed ? 'verify_removal' : 'remove', proposed ? owner : removalOwner)
const input = (code, source, relationship = 'Source', linkedSource = null) => ({ code, checkApplies: true, checkSatisfied: false, source, status: source.kind === 'Decision' ? 'Pending' : source.kind === 'Handoff' ? 'Submitted' : proposed ? 'Resolution Proposed' : 'Open', relationship, contextAvailable: true,
  linkedSource, linkedStatus: linkedSource ? 'Submitted' : null, linkedAvailable: true })
function presentation(state, hasAssessment = true, first = false) {
  const checks = codes.map(code => ({ code, applies: code === 'Production Owner' ? true : false, satisfied: code === 'Production Owner' ? true : null, reason: null, unknown: false, blocking: false }))
  const blocked = state === 'Not Ready' ? first ? ['Decision', 'Handoff', 'Constraint'] : ['Decision'] : state === 'Proceed under Assumption' ? ['Basis'] : []
  const unknown = state === 'Needs Assessment' ? codes.filter(code => code !== 'Production Owner') : []
  for (const check of checks) {
    if (blocked.includes(check.code)) Object.assign(check, { applies: true, satisfied: false, blocking: true, reason: 'This aggregate check has an unresolved source' })
    if (unknown.includes(check.code)) Object.assign(check, { applies: null, satisfied: null, unknown: true })
  }
  return { hasAssessment, available: true, state, evaluatedAt: stamp, checks, unknown, blocked,
    inputs: state === 'Not Ready' ? [input('Decision', decisionFact()), ...(first ? [input('Handoff', handoffFact(), 'DirectTarget'), input('Constraint', constraintFact(), 'Constraint', handoffFact())] : [])] : [],
    applicability: { role: 'PMOrDisciplineLead', state: mode === 'refused' ? 'Refused' : 'Multiple', candidates: [pm, lead].map(person => ({ person: mode === 'refused' ? { ...person, state: 'Inactive' } : person, allowed: mode !== 'refused', refusal: mode === 'refused' ? 'This project actor cannot currently write' : null })) },
    constraintOwners: first ? [{ id: constraint, removalOwner, affectedOwner: owner }] : [] }
}
function rows() {
  return [...Array.from({ length: 49 }, (_, i) => ({ targetType: 'Task', targetId: id(4200 + i), key: `P-MOCK-T${String(i + 1).padStart(3, '0')}`, name: i === 0 ? 'Decision and handoff receiving work' : `Shared decision work ${i + 1}`,
    dueDate: '2026-11-10', ownerId: owner.id, disciplineId: project.disciplines[0].id, state: 'Not Ready', hasAssessment: true, available: true, presentation: presentation('Not Ready', true, i === 0) })),
  { targetType: 'Task', targetId: id(4249), key: 'P-MOCK-T050', name: 'Work without an assessment', dueDate: '2026-11-04', ownerId: owner.id, disciplineId: project.disciplines[0].id, state: 'Needs Assessment', hasAssessment: false, available: true, presentation: presentation('Needs Assessment', false) },
  { targetType: 'Task', targetId: id(4250), key: 'P-MOCK-T051', name: 'Limited assumption work', dueDate: '2026-11-03', ownerId: owner.id, disciplineId: project.disciplines[0].id, state: 'Proceed under Assumption', hasAssessment: true, available: true, presentation: presentation('Proceed under Assumption') },
  { targetType: 'Task', targetId: id(4251), key: 'P-MOCK-T052', name: 'Early dated Ready work', dueDate: '2026-11-02', ownerId: owner.id, disciplineId: project.disciplines[0].id, state: 'Ready', hasAssessment: true, available: true, presentation: presentation('Ready') }]
}

;(async () => {
  const flags = { ...me.flags }, { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], writes = [], windowRequests = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/') && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method())) writes.push(`${request.method()} ${new URL(request.url()).pathname}`) })
  try {
    me.flags.screen_patterns = true; me.flags.live_events = false; me.flags.commitment_reliability = false
    const options = coordinationOptions({ actorId: me.id, canWrite: false, people: [owner, decisionOwner, removalOwner, pm, lead].map(p => ({ id: p.id, displayName: p.name })),
      disciplines: [{ id: project.disciplines[0].id, name: 'Civil' }], tasks: rows().map(row => coordinationTask({ id: row.targetId, key: row.key, name: row.name, ownerId: owner.id, projectDisciplineId: row.disciplineId, canManage: false })) })
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, route => route.fulfill({ json: options }))
    await page.route(`**/api/v1/projects/${project.id}/weekly-commitments**`, route => route.fulfill({ json: commitmentList([]) }))
    await page.route(`**/api/v1/projects/${project.id}/design-basis*`, route => route.fulfill({ json: { items: [], page: 1, pageSize: 200, totalCount: 0 } }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/**`, route => {
      const url = new URL(route.request().url()), path = url.pathname
      if (path.endsWith('/window')) {
        windowRequests.push(Object.fromEntries(url.searchParams))
        const page = Math.max(1, Number(url.searchParams.get('workPage')) || 1), size = 50
        const work = me.flags.screen_patterns && mode !== 'partial' ? { scope: 'DatedOpenWork', evaluatedAt: stamp, items: rows().slice((page - 1) * size, page * size), total: 52, page, pageSize: size } : undefined
        return route.fulfill({ json: readinessWindow({ from: '2026-11-01', to: '2026-11-14', readyOutputs: [readyOutput({ id: id(4399), targetId: id(4251), key: 'P-MOCK-T052', name: 'Early dated Ready work' })], readyOutputsTotal: 1,
          ...(work ? { work } : {}) }) })
      }
      const targetId = path.split('/Task/')[1]?.split('/')[0], row = rows().find(r => r.targetId === targetId)
      if (path.endsWith('/submission-prerequisites')) return route.fulfill({ json: [] })
      if (path.endsWith('/constraints')) return route.fulfill({ json: targetId === id(4200) ? [readinessConstraintRow({ id: constraint, key: 'P-MOCK-CT001', targetId, category: 'Handoff', description: 'Verify the survey independently', removalOwnerId: removalOwner.id, affectedOwnerId: owner.id, state: proposed ? 'Resolution Proposed' : 'Open' })] : [] })
      if (!row || !row.hasAssessment) return route.fulfill({ status: 404, json: { code: 'not_found', message: 'No assessment exists' } })
      const p = row.presentation
      return route.fulfill({ json: readinessDetail({ assessment: readinessAssessment({ id: id(4400), targetId, ownerId: owner.id, state: row.state, evaluatedAt: stamp }), unknown: p.unknown, blocked: p.blocked,
        checks: p.checks.map((c, i) => readinessCheck({ id: id(4410 + i), assessmentId: id(4400), code: c.code, applies: c.applies, satisfied: c.satisfied, reason: c.reason, recordedBy: id(4499) })),
        sources: p.inputs.map(i => ({ code: i.code, record: { type: i.source.kind, id: i.source.id, key: i.source.key, title: i.source.title, status: i.status } })),
        ...(me.flags.screen_patterns && mode !== 'partialDetail' ? { presentation: p } : {}) }) })
    })
    await page.route(`**/api/v1/decisions/${decision}`, route => route.fulfill({ json: { ...decisionDetail, decision: { ...decisionRow, id: decision, projectId: project.id, key: 'P-MOCK-DEC001', subject: 'Confirm the exact corridor', ownerUserId: decisionOwner.id, ownerExternalPartyId: null, ownerName: decisionOwner.name }, project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: 'Active' } } }))
    const h = handoffRow({ id: handoff, key: 'P-MOCK-H003', title: 'Survey handoff for receiving work', targetTaskId: id(4200) })
    await page.route(`**/api/v1/projects/${project.id}/handoffs**`, route => { const path = new URL(route.request().url()).pathname; return route.fulfill({ json: path.endsWith('/options') ? handoffOptions() : path.endsWith(`/${handoff}`) ? handoffDetail({ row: h }) : handoffPage([h]) }) })

    const url = `${base}/projects/${project.projectNumber}/readiness?from=2026-11-01&to=2026-11-14&constraintsPage=1&readyPage=1&promisePage=1`
    const list = () => page.locator('#work-readiness'), dialog = () => page.getByRole('dialog').last()
    const first = () => list().locator(`[data-readiness-work="Task:${id(4200)}"]`)
    await page.goto(url); await first().waitFor()
    assert.deepEqual(await list().locator('[data-readiness-work]').evaluateAll(nodes => nodes.map(n => n.dataset.readinessState)), [...Array(49).fill('Not Ready'), 'Needs Assessment'])
    await list().getByText(/Excludes held, terminal and undated work/).waitFor()
    await first().getByRole('heading', { name: /Omar Decision Owner/ }).waitFor()
    await first().getByRole('heading', { name: /Diane Production Owner/ }).waitFor()
    await first().getByText(/Linked record — the readiness constraint remains independent/).waitFor()
    await first().locator('[data-readiness-constraint-owners]').getByText(removalOwner.name, { exact: true }).waitFor()
    assert.equal((await list().innerText()).includes('%'), false)
    const pager = list().getByRole('navigation', { name: 'Work readiness', exact: true })
    await pager.getByRole('button', { name: 'Next', exact: true }).click()
    await page.waitForURL(/workPage=2/); await list().getByText('Limited assumption work', { exact: false }).waitFor()
    assert.deepEqual(await list().locator('[data-readiness-work]').evaluateAll(nodes => nodes.map(n => n.dataset.readinessState)), ['Proceed under Assumption', 'Ready'])
    for (const key of ['constraintsPage', 'readyPage', 'promisePage']) assert.equal(new URL(page.url()).searchParams.get(key), '1')
    await pager.getByRole('button', { name: 'Previous', exact: true }).click(); await first().waitFor()

    const unassessed = list().locator(`[data-readiness-work="Task:${id(4249)}"]`)
    await unassessed.getByRole('button', { name: 'Inspect applicability · Handoff', exact: true }).click()
    await dialog().getByText('Readiness has not been assessed. The named performer must first define the intended output and completion criteria.', { exact: true }).waitFor()
    assert.equal(await dialog().getByRole('button', { name: 'Define output', exact: true }).count(), 0)
    assert.equal(await dialog().locator('form').count(), 0)
    await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await first().waitFor()

    // A real keyboard activation selects the exact canonical check, with readonly commands still absent.
    const inspect = first().getByRole('button', { name: 'Inspect check · Decision', exact: true })
    await inspect.focus(); await page.keyboard.press('Enter')
    await page.waitForURL(/inspectCheck=Decision/); await dialog().locator('[data-readiness-check="Decision"]').waitFor()
    await page.waitForFunction(() => document.activeElement?.getAttribute('data-readiness-check') === 'Decision')
    assert.equal(await dialog().getByRole('button', { name: /^Record applicability/ }).count(), 0)
    const originUrl = page.url()
    const decide = dialog().locator('[data-readiness-next-steps]').getByRole('button', { name: /^Decide.*P-MOCK-DEC001/ })
    await decide.scrollIntoViewIfNeeded()
    await decide.evaluate(node => {
      document.querySelector('main#content').scrollTop = 120
      node.addEventListener('click', () => { const surface = node.closest('[data-slot="dialog-body"], [data-slot="dialog-content"]'); window.__readinessScroll = { main: document.querySelector('main#content').scrollTop, panel: surface.scrollTop, slot: surface.dataset.slot } }, { once: true, capture: true })
    })
    await decide.click(); await page.waitForURL(target => target.pathname === `/projects/${project.projectNumber}/readiness`
      && target.searchParams.get('panel') === `Decision:${decision}` && target.searchParams.get('inspectCheck') === 'Decision')
    const captured = await page.evaluate(() => window.__readinessScroll)
    assert.ok(captured.main > 0, 'the source work list really scrolls'); assert.ok(captured.panel > 0, 'the inspector body really scrolls')
    await dialog().getByRole('heading', { name: 'Confirm the exact corridor', exact: true }).waitFor()
    await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(originUrl)
    await dialog().locator('[data-readiness-check="Decision"]').waitFor()
    await page.waitForFunction(() => { const expected = window.__readinessScroll, panel = document.querySelector(`[data-slot="${expected.slot}"]`); return panel && panel.scrollTop === expected.panel && document.querySelector('main#content').scrollTop === expected.main })
    const accept = dialog().locator('[data-readiness-next-steps]').getByRole('link', { name: /^Accept.*P-MOCK-H003/ })
    await accept.click(); await page.waitForURL(target => target.pathname === `/projects/${project.projectNumber}/handoffs`
      && target.searchParams.get('panel') === `Handoff:${handoff}`)
    await dialog().getByRole('heading', { name: `${h.key} · ${h.title}`, exact: true }).waitFor(); await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(originUrl)
    await dialog().locator('[data-readiness-check="Decision"]').waitFor()
    await dialog().getByRole('button', { name: 'Close', exact: true }).click()
    proposed = true; external = true; await page.reload(); await first().getByRole('heading', { name: /Municipal Approver.*external|Municipal Approver.*account/i }).waitFor()
    await first().getByRole('link', { name: /^Verify removal.*P-MOCK-CT001/ }).waitFor()
    await first().locator('[data-readiness-constraint-owners]').getByText(removalOwner.name, { exact: true }).waitFor()
    await first().locator('[data-readiness-constraint-owners]').getByText(owner.name, { exact: true }).waitFor()

    mode = 'refused'; await page.reload(); await list().locator('[data-readiness-applicability="Refused"]').waitFor()
    await list().getByText('No current candidate can perform this action', { exact: true }).waitFor()
    mode = 'partial'; await page.reload(); await list().getByText('Complete work readiness list unavailable', { exact: true }).waitFor()
    assert.equal(await list().locator('[data-readiness-work]').count(), 0)
    mode = 'partialDetail'; await page.goto(originUrl); await dialog().getByText('Complete readiness check details unavailable', { exact: true }).waitFor()
    mode = 'full'
    const invalidSelection = new URL(originUrl); invalidSelection.searchParams.set('inspectCheck', 'Decision-extra')
    await page.goto(invalidSelection.toString()); await dialog().locator('[data-readiness-check="Decision"]').waitFor()
    assert.equal(await dialog().locator('[data-readiness-check].ring-2').count(), 0, 'only an exact canonical check code selects a card')
    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 }); await page.goto(originUrl); await dialog().locator('[data-readiness-check="Decision"]').waitFor()
      assert.ok(await dialog().evaluate(node => [node, node.querySelector('[data-slot="dialog-body"]')].filter(Boolean).every(surface => surface.scrollWidth <= surface.clientWidth + 1)), `inspector reflows at ${width}`)
      await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await first().waitFor()
      assert.ok(await list().evaluate(node => node.scrollWidth <= node.clientWidth + 1), `work list reflows at ${width}`)
    }
    await pager.getByRole('button', { name: 'Next', exact: true }).click(); await page.waitForURL(/workPage=2/)
    await page.getByRole('button', { name: /^Filters/ }).click()
    await page.getByLabel('From', { exact: true }).fill('2026-11-02')
    await page.waitForFunction(() => ['workPage', 'constraintsPage', 'readyPage', 'promisePage'].every(key => !new URL(location.href).searchParams.has(key)))
    await first().waitFor(); assert.equal(windowRequests.at(-1).workPage, '1')
    me.flags.screen_patterns = false; await page.goto(url)
    await page.locator('#ready-outputs').getByRole('heading').waitFor()
    assert.match(await page.locator('#ready-outputs').getByRole('heading').innerText(), /^Ready outputs(?:\s*\d+)?$/)
    assert.equal(await list().count(), 0); assert.equal('workPage' in windowRequests.at(-1), false)
    assert.deepEqual(writes, []); assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: all canonical readiness states across independent work pages; exact Decision/Handoff and selected-check return; current/refused/external actors; partial-data recovery; readonly no-write; keyboard, 375/768/1280 reflow and exact flag-off query/layout')
  } finally { me.flags = flags; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
