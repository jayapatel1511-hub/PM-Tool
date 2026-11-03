// S33: synthetic transport/UI evidence only. Real commands, authority, immutable issuance and dual versions have API cases.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { referenceData, referenceDeliverableType } = require('./me.fixture.cjs')
const { deliverableRow, deliverableDetail } = require('./deliverables.fixture.cjs')
const { registerInfo, issueRegister, issuedRow } = require('./deliverable-register.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0033-${String(n).padStart(12, '0')}`
const purpose = { id: id(101), name: 'For review', isActive: true, sortOrder: 0 }
const construction = { id: id(102), name: 'For construction', isActive: true, sortOrder: 1 }
const codes = [ ['A', 'Accepted'], ['B', 'Accepted with comments'], ['C', 'Revise and resubmit'] ].map(([code, name], i) => ({ id: id(110 + i), code, name, isActive: true, sortOrder: i }))
const make = (n, status = 'Not Started', dueDate = null) => deliverableRow({ id: id(n), projectId: project.id, projectDisciplineId: project.disciplines[0].id,
  key: `D-${String(n).padStart(3, '0')}`, name: `Synthetic register drawing ${n}`, status, dueDate, registerInfo: registerInfo() })
const rows = [make(1, 'Revision Required'), make(2), make(3, 'In Progress'), make(4, 'On Hold'),
  make(5, 'Issued'), make(6, 'Accepted'), make(7, 'Cancelled'), ...Array.from({ length: 24 }, (_, i) => make(20 + i, 'In Progress', `2026-11-${String(i + 3).padStart(2, '0')}`))]
const issues = new Map([[id(1), [
  issuedRow({ id: id(201), issuedDate: '2026-10-30', revision: 'A', issuedBy: 'Synthetic PM', registerInfo: issueRegister({ purposeOfIssueId: purpose.id, purpose: purpose.name,
    returnCodeId: codes[1].id, returnCode: 'B', returnLabel: codes[1].name, responseDate: '2026-11-01' }) }),
  issuedRow({ id: id(202), issuedDate: '2026-10-29', revision: 'B', issuedBy: 'Synthetic PM', registerInfo: issueRegister({ purposeOfIssueId: construction.id, purpose: construction.name }) }),
]]])
const actualCurrent = new Map([[id(1), id(202)]])
rows[0].registerInfo = registerInfo({ clientDrawingNumber: 'C12', numberPattern: 'C-###', numberMatchesPattern: false })
let numbering = { pattern: 'C-###', rowVersion: 1, canEdit: true }, purposes = [purpose, construction], returnCodes = codes, responseAllowed = true
const listReads = [], responseWrites = [], numberWrites = [], issueWrites = [], clientWrites = [], viewWrites = [], exportReads = [], views = [], errors = []
const apiMutations = [], unexpectedMutations = [], rejectedMutations = []
// Exact intended commands only. A transition, different deliverable/issue, bulk operation or new verb must fail this harness.
const allowedMutations = new Set([
  'POST /api/v1/me/sign-in',
  `POST /api/v1/projects/${project.id}/deliverables`,
  `PATCH /api/v1/deliverables/${id(1)}`,
  `POST /api/v1/deliverables/${id(2)}/issue`,
  `PATCH /api/v1/deliverables/${id(1)}/issues/${id(202)}/response`,
  `PUT /api/v1/projects/${project.id}/deliverable-numbering`,
  'POST /api/v1/views',
])
const isMutation = method => !['GET', 'HEAD', 'OPTIONS'].includes(method)
const open = row => !['Issued', 'Accepted', 'Cancelled'].includes(row.status)
const params = url => Object.fromEntries([...url.searchParams].map(([key, value]) => [key[0].toLowerCase() + key.slice(1), value]))
function presented(row) {
  const result = structuredClone(row)
  if (!me.flags.register_screens) { delete result.registerInfo; return result }
  const current = (issues.get(row.id) ?? []).find(issue => issue.id === actualCurrent.get(row.id))
  result.registerInfo.currentIssue = current ? { id: current.id, revision: current.revision, issuedDate: current.issuedDate,
    ...Object.fromEntries(Object.entries(current.registerInfo).filter(([key]) => key !== 'rowVersion')) } : null
  return result
}
function filtered(query) {
  assert.equal(query.layout, undefined, 'presentation must not reach canonical list/export')
  return rows.map(presented).filter(row => (!query.status || query.status.split(',').includes(row.status)) && (!query.disciplineId || row.projectDisciplineId === query.disciplineId)
    && (!query.noDueDate || query.noDueDate === 'false' || !row.dueDate && open(row)) && (!query.dueFrom || row.dueDate && row.dueDate >= query.dueFrom) && (!query.dueTo || row.dueDate && row.dueDate <= query.dueTo)
    && (!query.indicator || query.indicator.split(',').every(indicator => indicator !== 'open' || open(row)))
    && (!query.purposeOfIssueId || row.registerInfo?.currentIssue?.purposeOfIssueId === query.purposeOfIssueId) && (!query.returnCodeId || row.registerInfo?.currentIssue?.returnCodeId === query.returnCodeId))
}
;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  const path = `/projects/${project.projectNumber}/deliverables`
  const go = async query => {
    const read = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/projects/${project.id}/deliverables` && response.request().method() === 'GET')
    await page.goto(`${base}${path}${query ? `?${query}` : ''}`); await read
    const panel = new URLSearchParams(query || '').get('panel')
    if (panel) {
      assert.ok(panel.startsWith('Deliverable:'), `Unexpected panel in register harness: ${panel}`)
      const row = rows.find(value => value.id === panel.slice('Deliverable:'.length)); assert.ok(row, `Missing exact panel fixture ${panel}`)
      // A modal Sheet hides the background register from the accessibility tree. Wait for the actual foreground record.
      const sheet = page.getByRole('dialog', { name: 'Deliverable', exact: true })
      await sheet.getByRole('heading', { name: row.name, exact: true }).waitFor()
    } else await page.getByRole('heading', { name: 'Deliverables', exact: true }).waitFor()
  }
  const dialog = () => page.getByRole('dialog').last()
  const history = () => page.locator('[data-register-issue-id]')
  const closePanel = () => page.getByRole('button', { name: 'Close', exact: true }).first().click()
  const ids = locator => locator.evaluateAll(nodes => nodes.map(node => node.dataset.registerDateId).sort())
  const response = issueId => page.locator(`[data-register-issue-id="${issueId}"]`).getByRole('button', { name: /^Record recipient response/ }).click()
  const waitForNormalQueryExpiry = async () => {
    // Call after a bound field has rendered its GET result. main.tsx keeps reads fresh for 15s.
    // Start the full real interval now, after that committed result; read Date.now without replacing the clock or query policy.
    const observedAt = await page.evaluate(() => Date.now())
    await page.waitForFunction(({ since, staleMs }) => Date.now() - since > staleMs, { since: observedAt, staleMs: 15_000 }, { timeout: 20_000 })
  }
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', error => errors.push(error.message))
    // Observe actual outgoing requests, independently of what any route handler decides to fulfil.
    page.on('request', request => {
      const method = request.method(), pathname = new URL(request.url()).pathname
      if (!pathname.startsWith('/api/') || !isMutation(method)) return
      const command = `${method} ${pathname}`
      apiMutations.push(command)
      if (!allowedMutations.has(command)) unexpectedMutations.push(command)
    })
    me.flags = { ...me.flags, register_screens: true, visual_system: false, revision_check: false, delivery_screens: false, work_screens: false,
      screen_patterns: false, small_ux: false, org_structure: false, live_events: false, data_entry_safety: false }
    me.settings.today = '2026-11-02'
    project.permissions.createDeliverableIn = [project.disciplines[0].id]
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ deliverableTypes: [referenceDeliverableType(id(130), 'Drawing')],
      ...(me.flags.register_screens ? { issuePurposes: purposes, issueReturnCodes: returnCodes } : {}) }) }))
    await page.route('**/api/v1/projects/*/deliverables**', route => {
      const request = route.request(), url = new URL(request.url()), query = params(url)
      if (url.pathname.endsWith('/export')) { exportReads.push(query); return route.fulfill({ contentType: 'text/csv', body: 'Key,Name\nD-001,Synthetic register drawing 1\n' }) }
      if (request.method() === 'POST') {
        const body = request.postDataJSON(); clientWrites.push(body); const row = make(90); row.registerInfo = registerInfo({ clientDrawingNumber: body.clientDrawingNumber, numberPattern: numbering.pattern, numberMatchesPattern: false }); rows.push(row)
        return route.fulfill({ status: 201, json: { id: row.id, key: row.key, rowVersion: row.rowVersion, dueDate: null, warnings: ['Does not match C-###'] } })
      }
      listReads.push(query); return route.fulfill({ json: filtered(query) })
    })
    await page.route('**/api/v1/deliverables/**', route => {
      const request = route.request(), url = new URL(request.url()), parts = url.pathname.split('/'), row = rows.find(row => row.id === parts[4])
      if (!row) return route.fallback()
      if (parts[5] === 'issues' && parts[7] === 'response') {
        const body = request.postDataJSON(), issue = (issues.get(row.id) ?? []).find(issue => issue.id === parts[6]); responseWrites.push({ id: row.id, issueId: parts[6], body })
        assert.ok(issue); assert.deepEqual(Object.keys(body).sort(), ['issueRowVersion', 'reason', 'responseDate', 'returnCodeId', 'rowVersion'])
        if (body.rowVersion !== row.rowVersion || body.issueRowVersion !== issue.registerInfo.rowVersion) return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Synthetic issue changed after opening.' } })
        const code = returnCodes.find(code => code.id === body.returnCodeId)
        Object.assign(issue.registerInfo, { returnCodeId: code?.id ?? null, returnCode: code?.code ?? null, returnLabel: code?.name ?? null, responseDate: body.responseDate, rowVersion: issue.registerInfo.rowVersion + 1 }); row.rowVersion++
        return route.fulfill({ json: { id: row.id, issueId: issue.id, rowVersion: row.rowVersion, issueRowVersion: issue.registerInfo.rowVersion } })
      }
      if (parts[5] === 'issue') {
        const body = request.postDataJSON(); issueWrites.push(body); const chosen = purposes.find(value => value.id === body.purposeOfIssueId && value.isActive); assert.ok(chosen, 'purpose must be explicitly chosen from active choices')
        const issue = issuedRow({ id: id(203), issuedDate: body.issuedDate, revision: body.revision, registerInfo: issueRegister({ purposeOfIssueId: chosen.id, purpose: chosen.name }) })
        issues.set(row.id, [...(issues.get(row.id) ?? []), issue]); actualCurrent.set(row.id, issue.id); row.status = 'Issued'; row.rowVersion++
        return route.fulfill({ json: { id: row.id, status: row.status, rowVersion: row.rowVersion, openTasks: [] } })
      }
      if (request.method() === 'PATCH') {
        const body = request.postDataJSON(); clientWrites.push(body); const version = Number(request.headers()['if-match'].replaceAll('"', '')); assert.equal(version, row.rowVersion)
        assert.deepEqual(Object.keys(body).sort(), ['clientDrawingNumber', 'reason'], 'number edit must not carry lifecycle or issue changes')
        row.registerInfo.clientDrawingNumber = body.clientDrawingNumber; row.registerInfo.numberMatchesPattern = body.clientDrawingNumber === 'C-123'; row.rowVersion++
        return route.fulfill({ json: { id: row.id, rowVersion: row.rowVersion, warnings: row.registerInfo.numberMatchesPattern ? [] : ['Does not match C-###'] } })
      }
      if (parts.length > 5) return route.fallback()
      const currentIssues = structuredClone(issues.get(row.id) ?? [])
      if (!me.flags.register_screens) currentIssues.forEach(issue => delete issue.registerInfo)
      return route.fulfill({ json: deliverableDetail({ deliverable: presented(row), project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status }, issues: currentIssues,
        permissions: { edit: { ok: true, reason: null }, issue: row.status === 'Not Started', recordIssueResponse: me.flags.register_screens ? responseAllowed : undefined } }) })
    })
    await page.route('**/api/v1/projects/*/deliverable-numbering', route => {
      if (route.request().method() === 'GET') return route.fulfill({ json: numbering })
      const body = route.request().postDataJSON(); numberWrites.push(body)
      if (body.rowVersion !== numbering.rowVersion) return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Synthetic project changed after opening.' } })
      numbering = { ...numbering, pattern: body.pattern, rowVersion: numbering.rowVersion + 1 }
      return route.fulfill({ json: { id: project.id, pattern: numbering.pattern, rowVersion: numbering.rowVersion } })
    })
    await page.route('**/api/v1/views**', route => {
      if (route.request().method() === 'GET') return route.fulfill({ json: viewList(views) })
      const body = route.request().postDataJSON(); viewWrites.push(body); const value = savedView({ id: id(300 + views.length), name: body.name, params: body.params, projectId: project.id }); views.push(value)
      return route.fulfill({ status: 201, json: viewSaved(value.id) })
    })
    // Registered last, so it runs before every endpoint mock and the platform fixture's permissive non-GET fallback.
    await page.route('**/api/**', async route => {
      const request = route.request(), method = request.method(), pathname = new URL(request.url()).pathname
      if (!isMutation(method)) return route.fallback()
      const command = `${method} ${pathname}`
      if (allowedMutations.has(command)) return route.fallback()
      rejectedMutations.push(command)
      return route.fulfill({ status: 501, json: { code: 'unexpected_test_mutation', detail: `Unexpected synthetic API mutation: ${command}` } })
    })

    // Independent register flag: the exact four open undated statuses partition the same response as the dated list.
    await go('layout=dates&group=status')
    await page.getByRole('heading', { name: 'No due date (4)', exact: true }).waitFor()
    assert.deepEqual(await ids(page.locator('[data-register-undated] [data-register-date-id]')), rows.slice(0, 4).map(row => row.id).sort())
    assert.equal(await page.locator('[data-register-dated] [data-register-date-id]').count(), 24)
    assert.equal(listReads.at(-1).indicator, 'open')
    const target = page.locator('[data-register-dated] [data-register-date-id]').last().getByRole('button')
    await target.scrollIntoViewIfNeeded(); await target.focus()
    const sourceUrl = page.url(), sourceScroll = await page.locator('#content').evaluate(element => element.scrollTop), focusId = await target.getAttribute('id')
    assert.ok(sourceScroll > 0, 'return evidence needs real scroll, not zero')
    await target.press('Enter'); await page.getByRole('button', { name: 'Close', exact: true }).first().waitFor(); await closePanel()
    await page.waitForFunction(({ url, scroll, focus }) => location.href === url && Math.abs(document.getElementById('content').scrollTop - scroll) <= 1 && document.activeElement.id === focus, { url: sourceUrl, scroll: sourceScroll, focus: focusId })
    const undatedRead = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/projects/${project.id}/deliverables` && params(new URL(response.url())).noDueDate === 'true')
    await page.getByRole('button', { name: 'Show no due date in the register', exact: true }).click(); await undatedRead
    await page.waitForFunction(() => new URL(location.href).searchParams.get('noDueDate') === 'true')
    assert.equal(new URL(page.url()).searchParams.get('group'), 'status'); assert.equal(new URL(page.url()).searchParams.get('layout'), 'register')
    assert.deepEqual(filtered(listReads.at(-1)).map(row => row.id).sort(), rows.slice(0, 4).map(row => row.id).sort())
    await page.getByRole('checkbox', { name: 'Select D-001', exact: true }).click()
    await page.getByRole('button', { name: 'Dates', exact: true }).click(); await page.getByRole('heading', { name: 'No due date (4)', exact: true }).waitFor()
    await page.getByRole('button', { name: 'Register', exact: true }).click(); assert.equal(await page.getByRole('checkbox', { name: 'Select D-001', exact: true }).isChecked(), true)
    await go('layout=dates&indicator=open&status=In%20Progress&dueFrom=2026-11-01')
    await page.getByText('A due-date range excludes undated deliverables. Remove that range explicitly to include them.', { exact: true }).waitFor()
    await page.getByRole('button', { name: 'Remove due-date range', exact: true }).click(); await page.getByRole('heading', { name: 'No due date (1)', exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('status'), 'In Progress'); assert.equal(new URL(page.url()).searchParams.get('indicator'), 'open')
    assert.equal(new URL(page.url()).searchParams.has('dueFrom'), false)

    // Explicit current identity marks the backdated second issue; recipient date starts blank, with no status mutation.
    await go(`panel=Deliverable:${id(1)}`)
    await page.getByRole('tab', { name: /^Issues/ }).click(); await history().first().waitFor()
    assert.equal(await page.locator(`[data-register-issue-id="${id(202)}"]`).getByText('Latest issue', { exact: true }).count(), 1)
    assert.equal(await page.locator(`[data-register-issue-id="${id(201)}"]`).getByText('Latest issue', { exact: true }).count(), 0)
    await response(id(202)); assert.equal(await dialog().getByLabel('Response date', { exact: true }).inputValue(), ''); assert.equal(await dialog().getByLabel('Return code', { exact: true }).inputValue(), '')
    await dialog().getByLabel('Return code', { exact: true }).selectOption(codes[1].id); await dialog().getByLabel('Response date', { exact: true }).fill('2026-11-01'); await dialog().getByRole('button', { name: 'Save', exact: true }).click()
    await page.waitForFunction(() => !document.getElementById('issue-response-date')); assert.equal(rows[0].status, 'Revision Required'); assert.equal(issues.get(id(1)).length, 2)
    await page.locator(`[data-register-issue-id="${id(202)}"]`).getByText(/^B\s*· Accepted with comments$/).waitFor()
    assert.equal(responseWrites.at(-1).body.issueRowVersion, 0, 'zero is a real required version')
    // A stale dialog retains both baselines and typed values, then requires explicit close/reopen.
    await response(id(202)); const captured = { parent: rows[0].rowVersion, issue: issues.get(id(1))[1].registerInfo.rowVersion }
    await dialog().getByLabel('Return code', { exact: true }).selectOption(codes[2].id); await dialog().getByLabel('Response date', { exact: true }).fill('2026-11-02'); await dialog().getByLabel('Correction reason', { exact: true }).fill('Synthetic corrected response letter')
    rows[0].rowVersion++; issues.get(id(1))[1].registerInfo.rowVersion++
    await waitForNormalQueryExpiry()
    const responseRefetch = page.waitForResponse(response => response.url().endsWith(`/api/v1/deliverables/${id(1)}`) && response.request().method() === 'GET')
    await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange'))); await responseRefetch
    assert.equal(await dialog().getByLabel('Response date', { exact: true }).inputValue(), '2026-11-02')
    await dialog().getByRole('button', { name: 'Save', exact: true }).click(); await dialog().getByText(/Your entries and original versions are retained/).waitFor()
    assert.equal(responseWrites.at(-1).body.rowVersion, captured.parent); assert.equal(responseWrites.at(-1).body.issueRowVersion, captured.issue)
    assert.equal(await dialog().getByLabel('Response date', { exact: true }).inputValue(), '2026-11-02'); assert.equal(await dialog().getByRole('button', { name: 'Save', exact: true }).isDisabled(), true)
    const afterRefusal = responseWrites.length
    const afterCancel = page.waitForResponse(response => response.url().endsWith(`/api/v1/deliverables/${id(1)}`) && response.request().method() === 'GET')
    await dialog().getByRole('button', { name: 'Cancel', exact: true }).click(); await afterCancel; assert.equal(responseWrites.length, afterRefusal)
    await response(id(201)); assert.equal(await dialog().getByLabel('Response date', { exact: true }).inputValue(), '2026-11-01')
    await dialog().getByRole('button', { name: 'Cancel', exact: true }).click(); await closePanel()

    // Warning-only client number edit and creation use normal writes; the pattern cannot block either.
    await go(`panel=Deliverable:${id(1)}`); await page.getByRole('button', { name: 'Client drawing number', exact: true }).click()
    const editedNumber = dialog().getByLabel('Client drawing number (optional)', { exact: true })
    assert.equal(await editedNumber.getAttribute('id'), 'edit-client-number', 'the optional label must resolve to the actual number editor input')
    await editedNumber.fill('C-123'); await dialog().getByRole('button', { name: 'Save', exact: true }).click()
    await page.waitForFunction(() => !document.getElementById('edit-client-number')); assert.equal(clientWrites.at(-1).clientDrawingNumber, 'C-123'); assert.equal(rows[0].registerInfo.numberMatchesPattern, true); await closePanel()
    await page.getByRole('button', { name: 'New deliverable', exact: true }).click(); await dialog().getByLabel('Deliverable', { exact: true }).fill('Synthetic warning-only new drawing')
    const types = await dialog().getByLabel('Type', { exact: true }).locator('option').evaluateAll(options => options.map(option => option.value).filter(Boolean)); await dialog().getByLabel('Type', { exact: true }).selectOption(types[0])
    const createdNumber = dialog().getByLabel('Client drawing number (optional)', { exact: true })
    assert.equal(await createdNumber.getAttribute('id'), 'd-client-number', 'the create label must resolve to its own associated input')
    await createdNumber.fill('C12'); await dialog().getByRole('button', { name: 'Save', exact: true }).click(); await page.getByText('Does not match C-###', { exact: true }).first().waitFor()
    assert.equal(clientWrites.at(-1).clientDrawingNumber, 'C12')
    // No purpose is preselected. Only active choices may be issued; no fake history is inserted by viewing a form.
    await go(`panel=Deliverable:${id(2)}`); await page.getByRole('button', { name: 'Issue deliverable', exact: true }).click()
    assert.equal(await dialog().getByLabel('Purpose of issue', { exact: true }).inputValue(), ''); assert.equal(await dialog().getByRole('button', { name: 'Issue deliverable', exact: true }).isDisabled(), true)
    await dialog().getByLabel('Purpose of issue', { exact: true }).selectOption(purpose.id); await dialog().getByRole('button', { name: 'Issue deliverable', exact: true }).click(); await page.waitForFunction(() => !document.getElementById('i-purpose'))
    assert.equal(issueWrites.at(-1).purposeOfIssueId, purpose.id); await closePanel()
    purposes = purposes.map(value => ({ ...value, isActive: false })); returnCodes = returnCodes.map(value => ({ ...value, isActive: false }))
    await go(`panel=Deliverable:${id(90)}`); await page.getByRole('button', { name: 'Issue deliverable', exact: true }).click(); await dialog().getByText(/Administrator must configure an active purpose/).waitFor()
    assert.equal(await dialog().getByRole('button', { name: 'Issue deliverable', exact: true }).isDisabled(), true); await dialog().getByRole('button', { name: 'Cancel', exact: true }).click(); await closePanel()
    await go(`panel=Deliverable:${id(2)}`); await page.getByRole('tab', { name: /^Issues/ }).click(); await response(id(203)); await dialog().getByText(/Administrator must configure an active return code/).waitFor()
    assert.equal(await dialog().getByRole('button', { name: 'Save', exact: true }).isDisabled(), true); await dialog().getByRole('button', { name: 'Cancel', exact: true }).click(); await closePanel()
    await go(`panel=Deliverable:${id(1)}`); await page.getByRole('tab', { name: /^Issues/ }).click(); await response(id(201)); assert.match(await dialog().getByLabel('Return code', { exact: true }).locator('option:checked').textContent(), /B · Accepted with comments \(inactive\)/)
    await dialog().getByRole('button', { name: 'Cancel', exact: true }).click(); await closePanel()

    // Settings hold their dirty baseline through stale refusal; only an explicit reload adopts the new project version.
    await page.goto(`${base}/projects/${project.projectNumber}/settings`); const settings = page.getByRole('region', { name: 'Client drawing numbers', exact: true })
    await settings.getByLabel('Numbering pattern', { exact: true }).fill('D-###'); numbering.rowVersion = 9
    await waitForNormalQueryExpiry()
    const numberingRefetch = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/projects/${project.id}/deliverable-numbering` && response.request().method() === 'GET')
    await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange'))); await numberingRefetch
    assert.equal(await settings.getByLabel('Numbering pattern', { exact: true }).inputValue(), 'D-###')
    await settings.getByRole('button', { name: 'Save', exact: true }).click(); await settings.getByText(/Your pattern and original version are retained/).waitFor()
    assert.equal(numberWrites.at(-1).rowVersion, 1); assert.equal(await settings.getByLabel('Numbering pattern', { exact: true }).inputValue(), 'D-###'); assert.equal(numberWrites.length, 1)
    await settings.getByRole('button', { name: 'Reload', exact: true }).click(); await page.waitForFunction(() => document.getElementById('deliverable-number-pattern').value === 'C-###')
    await settings.getByLabel('Numbering pattern', { exact: true }).fill('D-###'); await settings.getByRole('button', { name: 'Save', exact: true }).click(); await page.getByText('Numbering pattern saved', { exact: true }).waitFor(); assert.equal(numberWrites.at(-1).rowVersion, 9)
    numbering.canEdit = false; await page.reload(); assert.equal(await settings.getByLabel('Numbering pattern', { exact: true }).isDisabled(), true); assert.equal(await settings.getByRole('button', { name: 'Save', exact: true }).count(), 0)

    // Saved view/Copy preserve the real UUID marker separately from presentation; exports use exact data filters only.
    purposes = [purpose, construction]; returnCodes = codes
    await go(`layout=dates&noDueDate=true&purposeOfIssueId=${construction.id}&returnCodeId=${codes[1].id}&group=status`)
    await page.getByRole('heading', { name: 'No due date (1)', exact: true }).waitFor(); await page.getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    await dialog().getByLabel('Name', { exact: true }).fill('Synthetic returned review'); await dialog().getByRole('button', { name: 'Save', exact: true }).click(); await page.getByRole('button', { name: 'Synthetic returned review', exact: true }).waitFor()
    const saved = viewWrites.at(-1); assert.equal(saved.params.layout, 'dates'); assert.equal(saved.params.noDueDate, 'true'); assert.equal(saved.params.purposeOfIssueId, construction.id); assert.equal(saved.params.returnCodeId, codes[1].id)
    for (const key of ['clientDrawingNumber', 'purposeOfIssue', 'returnCode', 'responseDate']) assert.ok(saved.params.cols.split(',').includes(key)); assert.equal(saved.params.view, undefined)
    me.flags.small_ux = true; await page.reload(); await page.getByRole('button', { name: 'Synthetic returned review', exact: true }).click(); await page.getByRole('menuitem', { name: 'Copy as personal view', exact: true }).click(); await dialog().getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('button', { name: 'Synthetic returned review (copy)', exact: true }).waitFor(); assert.deepEqual(viewWrites.at(-1).params, saved.params)
    await page.getByRole('button', { name: 'Export', exact: true }).click()
    const exportRead = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/projects/${project.id}/deliverables/export`)
    await page.getByRole('menuitem', { name: 'CSV (.csv)', exact: true }).click(); await exportRead
    assert.equal(exportReads.at(-1).layout, undefined); assert.equal(exportReads.at(-1).indicator, 'open'); assert.equal(exportReads.at(-1).purposeOfIssueId, construction.id); assert.equal(exportReads.at(-1).returnCodeId, codes[1].id); assert.equal(exportReads.at(-1).noDueDate, 'true')
    await page.setViewportSize({ width: 390, height: 844 }); await go('layout=dates'); await page.getByRole('heading', { name: /^No due date \(/ }).waitFor()
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true)
    responseAllowed = false; await go(`panel=Deliverable:${id(1)}`); await page.getByRole('tab', { name: /^Issues/ }).click(); assert.equal(await page.getByRole('button', { name: /^Record recipient response/ }).count(), 0)
    await closePanel(); me.flags.register_screens = false; await go(`layout=dates&purposeOfIssueId=${purpose.id}&returnCodeId=${codes[1].id}&noDueDate=true`)
    assert.equal(await page.getByRole('group', { name: 'Deliverable view', exact: true }).count(), 0); assert.equal(await page.locator('[data-register-undated]').count(), 0)
    for (const key of ['purposeOfIssueId', 'returnCodeId', 'noDueDate', 'layout']) assert.equal(listReads.at(-1)[key], undefined)
    assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, [])
    assert.deepEqual(unexpectedMutations, [], 'every actual mutation must match an exact intended METHOD + PATH')
    assert.deepEqual(rejectedMutations, [], 'the top interceptor must never have to refuse an app mutation')
    const signInBaselineWrites = apiMutations.filter(command => command === 'POST /api/v1/me/sign-in').length
    assert.equal(apiMutations.length - signInBaselineWrites, clientWrites.length + numberWrites.length + issueWrites.length + responseWrites.length + viewWrites.length,
      'each observed product mutation must reach one intended handler with its existing body/version assertions')
    console.log(JSON.stringify({ pass: true, evidence: 'synthetic UI only', cases: ['independent flag', 'four-status tray parity', 'AND date range', 'real scroll/focus return', 'current backdated identity', 'explicit response/date', 'dual stale baselines', 'warning-only number', 'required purpose', 'inactive/empty refs', 'PM pattern conflict', 'view Copy/export', 'phone', 'permission and dark controls', 'exact mutation allowlist'], reads: listReads.length, responseWrites: responseWrites.length, numberWrites: numberWrites.length, observedMutations: apiMutations.length, signInBaselineWrites }))
  } catch (error) {
    console.error(JSON.stringify({ diagnostic: 'deliverable-register failure', url: page.url(), visibleHeadings: await page.getByRole('heading').allTextContents().catch(() => []),
      visibleAlerts: await page.getByRole('alert').allTextContents().catch(() => []), pageErrors: errors, unknownGets: mock.unknownGets, apiErrors: mock.apiErrors,
      foregroundControls: await page.getByRole('dialog').last().locator('input,select,textarea').evaluateAll(elements => elements.map(element => ({ id: element.id, labels: Array.from(element.labels || []).map(label => label.textContent), type: element.getAttribute('type') }))).catch(() => []),
      lastListRead: listReads.at(-1) || null, recentMutations: apiMutations.slice(-10), unexpectedMutations, rejectedMutations }))
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
