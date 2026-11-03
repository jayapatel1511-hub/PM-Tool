// Actual SPA, file inputs and navigation. Mock API DTOs do not prove parser/transaction/authority correctness.
// DrawingImportTests and DrawingImportFormatTests own actual 40-row CSV/XLSX and atomic receipt evidence.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./deliverable-import.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the coordinator local preview')
const root = '/api/v1/projects/' + f.ids.project + '/deliverables'
const sourcePath = '/projects/' + f.project.projectNumber + '/deliverables?group=none&layout=register'
const uncertainText = 'The result has not been confirmed. Your file and original request are kept here. Retry to check the same import before changing the list.'
const definiteText = 'The list was not created. Review the error and preview again before committing.'
const keys = body => Object.keys(body).sort()
const previewKeys = ['contentBase64', 'defaults', 'format', 'reason', 'sourceReference', 'worksheet'].sort()
const commitKeys = [...previewKeys, 'previewFingerprint', 'projectRowVersion'].sort()

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const pageErrors = [], unexpected = [], observedUnexpected = [], previews = [], commands = [], personReads = [], forbidden = [], listReads = [], projectReads = [], optionReads = [], observations = []
  const receipts = new Map()
  const requestTimeline = [], startedAt = Date.now()
  let commitMode = 'refuse', armed = 0, version = 1, batch = 0, createdRows = [], successfulReplies = 0
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', error => pageErrors.push(error.message))
    page.on('request', request => {
      const url = new URL(request.url()), method = request.method()
      if ([root, root + '/import', root + '/import-preview', '/api/v1/projects/' + f.project.projectNumber].includes(url.pathname))
        requestTimeline.push({ elapsedMs: Date.now() - startedAt, method, path: url.pathname, mode: commitMode, successfulReplies, listReads: listReads.length })
      if (!url.pathname.startsWith('/api/') || ['GET', 'HEAD', 'OPTIONS'].includes(method)) return
      if (method === 'POST' && [root + '/import-preview', root + '/import', '/api/v1/me/sign-in'].includes(url.pathname)) return
      observedUnexpected.push(method + ' ' + url.pathname)
    })
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname, method = request.method()
      if (method === 'POST' && path === '/api/v1/me/sign-in') return route.fallback()
      if (method === 'POST' && path === root + '/import-preview') {
        const body = request.postDataJSON()
        assert.deepEqual(keys(body), previewKeys)
        assert.equal(request.headers()['idempotency-key'], undefined, 'Read-only preview never creates a command receipt')
        assert.ok([f.files.bad.buffer.toString('base64'), f.files.good.buffer.toString('base64'), f.files.xlsx.buffer.toString('base64')].includes(body.contentBase64), 'Preview sends exactly the selected synthetic bytes')
        assert.ok(body.format === 'xlsx' ? body.contentBase64 === f.files.xlsx.buffer.toString('base64') : body.format === 'csv', 'Format follows selected file')
        const result = f.preview(body, version)
        previews.push({ body, result })
        return route.fulfill({ json: result })
      }
      if (method === 'POST' && path === root + '/import') {
        const body = request.postDataJSON(), key = request.headers()['idempotency-key'], wire = request.postData()
        assert.ok(armed > 0, 'An import commit must follow an explicit test button action; no automatic commands')
        armed--
        assert.match(key ?? '', /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i)
        assert.deepEqual(keys(body), commitKeys)
        assert.ok(body.previewFingerprint && body.projectRowVersion > 0)
        const prior = receipts.get(key)
        if (prior) assert.equal(wire, prior.wire, 'An immutable retry keeps the exact wire body, not just semantically equal fields')
        commands.push({ body, key, wire, mode: commitMode })
        const problem = (status, code, detail) => route.fulfill({ status, contentType: 'application/problem+json', body: JSON.stringify({ status, code, title: 'Synthetic refusal', detail }) })
        if (commitMode === 'conflict') { version++; return problem(409, 'drawing_import_changed', 'Synthetic reference facts changed. Preview again.') }
        if (commitMode === '403') return problem(403, 'forbidden', 'Synthetic authority temporarily refused replay.')
        if (commitMode === '404') return problem(404, 'not_found', 'Synthetic flag or project scope temporarily unavailable.')
        if (!['lost', 'success'].includes(commitMode)) return problem(405, 'unexpected_test_command', 'No import is currently authorized by this UI scenario.')
        let receipt = prior
        if (!receipt) {
          const last = previews.at(-1)
          assert.ok(last && last.result.canCommit)
          assert.equal(body.previewFingerprint, last.result.previewFingerprint)
          assert.equal(body.projectRowVersion, last.result.projectRowVersion)
          const { previewFingerprint, projectRowVersion, ...input } = body
          assert.deepEqual(input, last.body, 'Commit is the exact reviewed source/default/reason selection')
          const result = f.created(batch++, body.defaults?.projectDisciplineId ?? f.ids.discipline)
          receipt = { wire, result }
          receipts.set(key, receipt)
          createdRows.push(...f.registerRows(result, input)); version++
        }
        if (commitMode === 'lost') return route.abort('failed') // Mock committed outcome, response lost; real receipt evidence is in API tests.
        successfulReplies++
        return route.fulfill({ json: receipt.result })
      }
      if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
        unexpected.push(method + ' ' + path)
        return route.fulfill({ status: 405, json: { detail: 'This suite allows only explicit import commits, read-only previews and session bootstrap.' } })
      }
      if (path === '/api/v1/me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: f.actor })
      // This scenario has no unrelated project changes. The generic platform fixture
      // increments projectStamp on every poll, which would request independent refreshes.
      if (path === '/api/v1/me/notifications/pulse') return route.fulfill({ json: { stamp: 'synthetic-import-idle' } })
      if (path === '/api/v1/reference') return route.fulfill({ json: f.reference })
      if (path === '/api/v1/projects/' + f.ids.project || path === '/api/v1/projects/' + f.project.projectNumber) {
        projectReads.push(url.href); return route.fulfill({ json: { ...f.project, rowVersion: version } })
      }
      if (path === root) { listReads.push(url.href); return route.fulfill({ json: createdRows }) }
      if (path === root + '/import-options') { optionReads.push(url.href); return route.fulfill({ json: { ...f.options, projectRowVersion: version } }) }
      if (path === '/api/v1/projects/' + f.ids.project + '/milestones') return route.fulfill({ json: [f.milestone] })
      if (/^\/api\/v1\/people\/[^/]+\/panel$/.test(path)) {
        const userId = path.split('/')[4]
        personReads.push({ userId, query: Object.fromEntries(url.searchParams) })
        return route.fulfill({ json: f.personRead(userId) })
      }
      if (/^\/api\/v1\/(planning|workload|staff|users)(\/|$)/.test(path)) {
        forbidden.push(url.href); return route.fulfill({ status: 403, json: { detail: 'No directory or hours read belongs to normalized Person inspection' } })
      }
      return route.fallback()
    })
    const dialog = () => page.getByRole('dialog', { name: 'Import drawing list', exact: true })
    const leaveDialog = () => page.getByRole('dialog', { name: 'Keep editing or discard?', exact: true })
    const personSheet = () => page.getByRole('dialog', { name: 'Person', exact: true })
    const fileInput = () => dialog().locator('#drawing-import-file')
    const previewButton = () => dialog().getByRole('button', { name: 'Preview drawing list', exact: true })
    const previewSection = () => dialog().getByRole('region', { name: 'Preview drawing list', exact: true })
    const createButton = () => dialog().getByRole('button', { name: /^Create \d+ deliverables$/ })
    const row = number => dialog().locator('[data-drawing-import-row="' + number + '"]')
    const currentFile = () => fileInput().evaluate(async node => { const file = node.files?.[0]; return file ? { name: file.name, size: file.size, text: await file.text() } : null })
    const expectNoStoredBytes = async () => {
      const leak = await page.evaluate(({ marker, bytes }) => [localStorage, sessionStorage].some(storage => Object.keys(storage).some(key => {
        const value = storage.getItem(key) ?? ''; return value.includes(marker) || bytes.some(body => value.includes(body))
      })), { marker: f.marker, bytes: [f.files.good.buffer.toString('base64'), f.files.bad.buffer.toString('base64'), f.files.xlsx.buffer.toString('base64')] })
      assert.equal(leak, false, 'Source bytes are absent from local/session draft storage')
    }
    const open = async () => {
      await page.getByRole('button', { name: 'Import drawing list', exact: true }).click()
      await fileInput().waitFor(); await dialog().locator('#drawing-import-discipline').waitFor()
    }
    const defaults = async (discipline = f.ids.discipline, type = f.ids.type) => {
      await dialog().locator('#drawing-import-discipline').selectOption(discipline)
      await dialog().locator('#drawing-import-type').selectOption(type)
    }
    const preview = async (creates = 40, errors = 0, keyboard = false) => {
      const count = previews.length
      const answered = page.waitForResponse(response => new URL(response.url()).pathname === root + '/import-preview' && response.request().method() === 'POST')
      if (keyboard) { await previewButton().focus(); await page.keyboard.press('Enter') } else await previewButton().click()
      await answered
      await previewSection().getByText(creates + ' proposed creates · ' + errors + ' errors', { exact: true }).waitFor()
      assert.equal(previews.length, count + 1, 'Only the explicit preview action sends one read-only preview')
      return previews.at(-1)
    }
    const chooseSource = async value => {
      const details = dialog().locator('details:has(#drawing-import-source)')
      if (!(await details.evaluate(node => node.open))) await details.locator('summary').click()
      await dialog().locator('#drawing-import-source').fill(value)
    }
    const closeAndDiscard = async () => {
      await dialog().locator('[data-slot="dialog-close"]').click()
      await leaveDialog().waitFor(); await leaveDialog().getByRole('button', { name: 'Discard', exact: true }).click()
      await dialog().waitFor({ state: 'detached' })
    }
    const commit = async mode => {
      commitMode = mode; armed++
      const button = mode === '403' || mode === '404' || mode === 'recover' ? dialog().getByRole('button', { name: 'Retry same import', exact: true }) : createButton()
      if (mode === 'recover') commitMode = 'success'
      const answered = mode === 'lost'
        ? page.waitForEvent('requestfailed', { predicate: request => new URL(request.url()).pathname === root + '/import' && request.method() === 'POST' })
        : page.waitForResponse(response => new URL(response.url()).pathname === root + '/import' && response.request().method() === 'POST')
      await button.click()
      await answered
    }
    await page.goto(base + sourcePath)
    await page.getByRole('button', { name: 'Import drawing list', exact: true }).waitFor()
    assert.equal(commands.length, 0); assert.equal(previews.length, 0)
    await open(); await defaults(); await fileInput().setInputFiles(f.files.bad)
    assert.equal(previews.length, 0, 'Selecting a file never automatically previews or commits')
    await preview(39, 1)
    assert.equal(await dialog().locator('[data-drawing-import-row]').count(), 40)
    assert.equal(await createButton().isDisabled(), true, 'One blank title blocks the complete import')
    assert.ok((await row(14).innerText()).includes('Synthetic title is required.'))
    assert.ok((await row(14).locator('summary').innerText()).includes('Unresolved row'), 'Unavailable normalized values are labelled neutrally')
    assert.ok((await row(14).innerText()).includes('SRC-013'), 'Error row retains source key even without normalized values')
    assert.equal(commands.length, 0)
    await fileInput().setInputFiles(f.files.good)
    await previewSection().waitFor({ state: 'detached' })
    assert.equal(await createButton().isDisabled(), true, 'A corrected file requires a new preview')
    const corrected = await preview()
    await dialog().locator('#drawing-import-type').selectOption(f.ids.alternateType)
    await previewSection().waitFor({ state: 'detached' }); assert.equal(await createButton().isDisabled(), true)
    const changedType = await preview()
    assert.notEqual(changedType.result.previewFingerprint, corrected.result.previewFingerprint)
    await dialog().locator('#drawing-import-discipline').selectOption(f.ids.alternateDiscipline)
    await previewSection().waitFor({ state: 'detached' }); assert.equal(await createButton().isDisabled(), true)
    const reviewed = await preview()
    assert.equal(reviewed.body.defaults.projectDisciplineId, f.ids.alternateDiscipline)
    assert.equal(reviewed.body.defaults.deliverableTypeId, f.ids.alternateType)
    await row(22).locator('summary').click()
    const normalized = await row(22).innerText()
    for (const text of ['SRC-021', 'Synthetic alternate discipline', 'Synthetic drawing package', 'Synthetic drawing owner', 'Synthetic drawing checker',
      'P-MOCK-M0001', '2026-11-02', '2026-11-20', 'SYN-021', 'High', 'Review required', '(discipline lead)', '(milestone date)', f.marker]) assert.ok(normalized.includes(text), 'Normalized preview shows ' + text)
    assert.equal(await row(22).getByText('SYN-021 · A', { exact: true }).count(), 1, 'Working revision is shown with the client drawing number')
    const personTriggers = await dialog().locator('[data-person-panel-trigger]').evaluateAll(nodes => ({ ids: nodes.map(node => node.id),
      nested: nodes.filter(node => node.parentElement?.closest('a,button,summary,[role="option"]')).map(node => node.id) }))
    assert.equal(new Set(personTriggers.ids).size, 80, 'Forty rows keep distinct owner/reviewer opener IDs')
    assert.deepEqual(personTriggers.nested, [], 'Person names are separate from row disclosure/navigation controls')
    assert.equal(commands.length, 0, 'Forty valid proposed creates do not auto-commit')
    observations.push('actual 40-row CSV bytes, one blank-title error, source key without values, corrected file/default invalidation, full normalized row')

    // A source form remains mounted through Person; opening another route still asks before abandoning the file.
    const sourceFile = await currentFile(), owner = row(22).locator('[data-person-panel-trigger="' + f.ids.owner + '"]')
    const focusId = await owner.getAttribute('id'), bodyScroll = dialog().locator('[data-slot="dialog-body"]')
    await owner.scrollIntoViewIfNeeded(); await owner.focus()
    const scroll = await bodyScroll.evaluate(node => node.scrollTop), rowsScroll = await dialog().getByRole('list', { name: 'Drawing list rows', exact: true }).evaluate(node => node.scrollTop)
    assert.ok(rowsScroll > 0, 'The actual overflowing row list has nonzero source scroll')
    await page.keyboard.press('Enter'); await page.locator('[data-person-panel="' + f.ids.owner + '"]').waitFor()
    assert.equal(await leaveDialog().count(), 0, 'Person open does not ask to discard a mounted file')
    assert.equal(await page.locator('[data-drawing-import]').count(), 1, 'Source form stays mounted below Person')
    assert.deepEqual(await page.locator('#drawing-import-file').evaluate(async node => { const file = node.files?.[0]; return file ? { name: file.name, size: file.size, text: await file.text() } : null }), sourceFile)
    await expectNoStoredBytes()
    const personUrl = page.url()
    await personSheet().getByRole('link', { name: f.project.projectNumber + ' · ' + f.project.name, exact: true }).click()
    await leaveDialog().waitFor(); await leaveDialog().getByRole('button', { name: 'Keep editing', exact: true }).click()
    await leaveDialog().waitFor({ state: 'detached' }); assert.equal(page.url(), personUrl, 'Other route remains blocked after Keep editing')
    await page.keyboard.press('Escape'); await page.waitForURL(base + sourcePath); await dialog().waitFor()
    assert.equal(await leaveDialog().count(), 0, 'Actual Person Escape uses preserved-source return without a leave prompt')
    await page.waitForFunction(id => document.activeElement?.id === id, focusId)
    assert.deepEqual(await currentFile(), sourceFile)
    assert.equal(await row(22).locator('details').evaluate(node => node.open), true, 'Expanded row disclosure survives return')
    assert.ok(Math.abs(await bodyScroll.evaluate(node => node.scrollTop) - scroll) <= 2, 'Source dialog scroll returns naturally')
    assert.ok(Math.abs(await dialog().getByRole('list', { name: 'Drawing list rows', exact: true }).evaluate(node => node.scrollTop) - rowsScroll) <= 2, 'Nested row-list scroll stays mounted')
    await dialog().locator('[data-slot="dialog-close"]').click(); await leaveDialog().waitFor()
    await leaveDialog().getByRole('button', { name: 'Keep editing', exact: true }).click(); await leaveDialog().waitFor({ state: 'detached' })
    assert.deepEqual(await currentFile(), sourceFile, 'Source Close retains its explicit dirty guard')
    await closeAndDiscard(); assert.equal(commands.length, 0)
    observations.push('mounted Person open/Escape, exact source URL/opener focus/scroll/fold/file return, guarded other route and source Close, no stored source bytes')

    // MOCK XLSX transport only: exact bytes/format and explicit worksheet choice, without pretending these bytes are a workbook.
    await page.setViewportSize({ width: 375, height: 900 }); await open(); await defaults(); await fileInput().setInputFiles(f.files.xlsx)
    await preview(0, 1, true)
    assert.equal(previews.at(-1).body.format, 'xlsx'); assert.equal(previews.at(-1).body.worksheet, null)
    await dialog().locator('#drawing-import-sheet').selectOption('Drawings')
    await previewSection().waitFor({ state: 'detached' }); assert.equal(await createButton().isDisabled(), true)
    const workbook = await preview(40, 0, true)
    assert.equal(workbook.body.worksheet, 'Drawings')
    assert.equal(workbook.body.contentBase64, f.files.xlsx.buffer.toString('base64'))
    const bounds = await dialog().boundingBox()
    assert.ok(bounds && bounds.x >= -1 && bounds.x + bounds.width <= 376, 'Import dialog fits375px')
    assert.equal(await dialog().locator('[data-slot="dialog-body"]').evaluate(node => node.scrollWidth <= node.clientWidth + 1), true)
    await fileInput().focus(); await page.keyboard.press('Tab')
    assert.equal(await page.evaluate(() => document.activeElement?.textContent?.trim()), 'Download CSV template', 'File control keeps a natural next keyboard target')
    await row(2).locator('summary').click()
    const phoneReviewer = row(2).locator('[data-person-panel-trigger="' + f.ids.reviewer + '"]'), phoneFocus = await phoneReviewer.getAttribute('id')
    await phoneReviewer.focus(); await page.keyboard.press('Enter'); await page.locator('[data-person-panel="' + f.ids.reviewer + '"]').waitFor()
    await page.keyboard.press('Escape'); await page.waitForURL(base + sourcePath)
    await page.waitForFunction(id => document.activeElement?.id === id, phoneFocus)
    assert.equal((await currentFile()).name, f.files.xlsx.name)
    assert.equal(await leaveDialog().count(), 0); await expectNoStoredBytes(); await closeAndDiscard()
    await page.setViewportSize({ width: 1440, height: 1000 })
    observations.push('mock-only XLSX bytes/format, explicit worksheet preview,375px containment and keyboard/Person return')

    // Options may become stale while the project changes status; the fresh preview owns the reason requirement.
    await open(); await defaults(); await fileInput().setInputFiles(f.files.good); await chooseSource('requires-reason')
    assert.equal(await dialog().locator('#drawing-import-source').getAttribute('maxlength'), '200', 'UI source reference limit matches the public API')
    assert.equal(await dialog().locator('#drawing-import-reason').count(), 0, 'Initial active-project options require no reason')
    await preview(40, 1)
    await dialog().locator('#drawing-import-reason').fill('  Synthetic completed-project reason  ')
    await previewSection().waitFor({ state: 'detached' })
    assert.equal(await dialog().locator('#drawing-import-reason').inputValue(), '  Synthetic completed-project reason  ', 'Reason field stays present after typing invalidates preview')
    const withReason = await preview()
    assert.equal(withReason.body.reason, 'Synthetic completed-project reason')
    const oldKeyIndex = commands.length
    await commit('conflict'); await dialog().getByText(definiteText, { exact: true }).waitFor()
    assert.equal(await createButton().isDisabled(), true, 'Definite409 requires another preview')
    assert.equal(await fileInput().isDisabled(), false); assert.equal((await currentFile()).name, f.files.good.name)
    const oldKey = commands[oldKeyIndex].key, fresh = await preview()
    assert.notEqual(fresh.result.previewFingerprint, withReason.result.previewFingerprint)
    const listBefore = listReads.length, projectBefore = projectReads.length
    // One existing refreshProject cycle has overlapping ['p',id]/['p'] immediate invalidations
    // (two observable register reads) and one1500ms pass; the header is read once per pass.
    const refreshList = page.waitForResponse(response => response.url().split('?')[0] === base + root && response.request().method() === 'GET' && listReads.length >= listBefore + 3)
    const refreshHeader = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/projects/' + f.project.projectNumber && response.request().method() === 'GET' && projectReads.length >= projectBefore + 2)
    await commit('success'); await dialog().getByRole('region', { name: 'Import result', exact: true }).getByText('40 deliverables created.', { exact: true }).waitFor(); await Promise.all([refreshList, refreshHeader])
    assert.notEqual(commands.at(-1).key, oldKey, 'Only a new preview after a definite failure receives a fresh command key')
    assert.equal(createdRows.length, 40); assert.equal(successfulReplies, 1)
    assert.equal(listReads.length, listBefore + 3, 'One success uses exactly one existing register refresh cycle')
    assert.equal(projectReads.length, projectBefore + 2, 'One success uses the established two-pass project refresh cycle')
    assert.equal(await dialog().getByRole('region', { name: 'Import result', exact: true }).locator('li').count(), 40)
    await dialog().getByRole('button', { name: 'Close', exact: true }).first().click(); await dialog().waitFor({ state: 'detached' })
    assert.equal(await leaveDialog().count(), 0, 'Confirmed result closes without treating saved rows as dirty')
    observations.push('fresh-preview reason requirement survives edits, definite409 retains inputs, new preview/new key,40-row result and one established two-pass refresh cycle')

    // A committed result whose reply was lost stays one immutable intent across later replay denials.
    await open(); await defaults(f.ids.alternateDiscipline, f.ids.alternateType); await fileInput().setInputFiles(f.files.good)
    await chooseSource('Synthetic retained import batch'); await preview()
    const start = commands.length, unconfirmedList = listReads.length
    await commit('lost'); await dialog().getByText(uncertainText, { exact: true }).waitFor()
    assert.equal(createdRows.length, 80, 'Mock transport ledger applied this intent once before losing its reply')
    const first = commands[start]
    for (const mode of ['403', '404']) {
      await commit(mode); await dialog().getByRole('alert').waitFor()
      await dialog().getByText(uncertainText, { exact: true }).waitFor()
      assert.equal(await dialog().getByText(definiteText, { exact: true }).count(), 0, 'Replay denial cannot claim the earlier intent was not created')
      assert.equal(await fileInput().isDisabled(), true)
      assert.equal(await dialog().locator('#drawing-import-discipline').isDisabled(), true)
      assert.equal(await dialog().locator('#drawing-import-type').isDisabled(), true)
      assert.equal(await previewButton().isDisabled(), true, 'Uncertain result cannot be replaced by a fresh preview/key')
      assert.equal(commands.at(-1).key, first.key); assert.equal(commands.at(-1).wire, first.wire)
      assert.equal((await currentFile()).name, f.files.good.name)
      assert.equal(listReads.length, unconfirmedList, 'An uncertain result or replay denial never reports a successful import refresh')
    }
    const finalList = listReads.length, finalProject = projectReads.length
    const retryRefresh = page.waitForResponse(response => response.url().split('?')[0] === base + root && response.request().method() === 'GET' && listReads.length >= finalList + 3)
    const retryHeader = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/projects/' + f.project.projectNumber && response.request().method() === 'GET' && projectReads.length >= finalProject + 2)
    await commit('recover'); await dialog().getByRole('region', { name: 'Import result', exact: true }).getByText('40 deliverables created.', { exact: true }).waitFor(); await Promise.all([retryRefresh, retryHeader])
    assert.equal(commands.at(-1).key, first.key); assert.equal(commands.at(-1).wire, first.wire, 'Authority returning never starts a fresh import intent')
    assert.equal(new Set(commands.slice(start).map(command => command.key)).size, 1)
    assert.equal(createdRows.length, 80, 'Mock ledger did not duplicate40 rows on retry')
    assert.equal(successfulReplies, 2)
    assert.equal(listReads.length, finalList + 3); assert.equal(projectReads.length, finalProject + 2)
    assert.equal(await dialog().getByRole('region', { name: 'Import result', exact: true }).locator('li').count(), 40)
    assert.equal(await dialog().getByRole('button', { name: 'Retry same import', exact: true }).count(), 0)
    await expectNoStoredBytes(); await dialog().getByRole('button', { name: 'Close', exact: true }).first().click(); await dialog().waitFor({ state: 'detached' })
    observations.push('response-loss then403 then404 remains uncertain/locked, exact immutable key+wire retries, recovered40-row result without duplicate intent')

    const optionsBefore = optionReads.length, previewsBefore = previews.length, commandsBefore = commands.length
    f.actor.flags.register_screens = false
    await page.goto(base + sourcePath); await page.getByRole('heading', { name: 'Deliverables', exact: true }).waitFor()
    assert.equal(await page.getByRole('button', { name: 'Import drawing list', exact: true }).count(), 0)
    assert.equal(optionReads.length, optionsBefore); assert.equal(previews.length, previewsBefore); assert.equal(commands.length, commandsBefore)
    assert.equal(armed, 0)
    assert.deepEqual([...new Set(personReads.map(read => read.userId))].sort(), [f.ids.owner, f.ids.reviewer].sort(), 'Desktop owner and phone reviewer are distinct actual normalized User reads')
    assert.ok(personReads.every(read => !read.query.staffScope), 'Person inspection adds no scope')
    assert.deepEqual(forbidden, []); assert.deepEqual(unexpected, []); assert.deepEqual(observedUnexpected, []); assert.deepEqual(pageErrors, []); assert.deepEqual(ui.mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS', suite: 'deliverable-import', evidence: 'synthetic browser transport/UI only; real parser/atomicity/authority remain API-owned',
      xlsx: 'MOCK BYTES, UI worksheet/format checks only', observations, previews: previews.length, explicitCommitRequests: commands.length, distinctKeys: new Set(commands.map(command => command.key)).size,
      mockCreatedRows: createdRows.length, successfulReplies, personReads: personReads.length, unexpectedBusinessMethods: unexpected.length + observedUnexpected.length }))
  } catch (error) { console.error(JSON.stringify({ requestTimeline, listReads, projectReads })); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
