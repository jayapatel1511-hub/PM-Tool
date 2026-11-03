// AC-SCR-69 / AC-SCR-84: synthetic actual-SPA UI only; provider tests own canonical maths/authority/receipts/persistence.
const assert = require('node:assert/strict')
const { join } = require('node:path')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./decision-metadata.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the coordinator-owned local preview')

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui, source = f.state()
  const writes = [], unknownGets = [], unexpected = [], errors = [], screenReads = [], listReads = [], detailReads = [], correctionRefusals = [], observations = []
  let actor = f.actorFor(), refuseScreen = false, outcomeReceipt = null, createReceipt = null, completeReceipt = null
  const path = '/projects/P-MOCK/decisions', createdId = '00000000-0000-0000-0000-000000007999'
  try {
    page.setDefaultTimeout(15000)
    page.on('pageerror', error => errors.push(error.message))
    page.on('request', request => { const name = new URL(request.url()).pathname; if (name.startsWith('/api/') && !['GET', 'HEAD', 'OPTIONS'].includes(request.method()) && !(request.method() === 'POST' && name === '/api/v1/me/sign-in')) writes.push({ method: request.method(), name, body: request.postData(), key: request.headers()['idempotency-key'], version: request.headers()['if-match'] }) })
    const shell = new Set(['config', 'reference', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events', 'projects/' + f.ids.project + '/tasks', 'projects/' + f.ids.project + '/deliverables', 'projects/' + f.ids.project + '/milestones'])
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), name = url.pathname.replace('/api/v1/', ''), method = request.method()
      const problem = (status, detail, code = 'invalid') => route.fulfill({ status, contentType: 'application/problem+json', json: { status, code, detail } })
      if (method === 'POST' && name === 'me/sign-in') return route.fallback()
      if (method === 'GET') {
        if (name === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: actor })
        if (name === 'reference') return route.fulfill({ json: f.reference })
        if (name === 'projects') return route.fulfill({ json: f.projects })
        if (name === 'projects/P-MOCK' || name === 'projects/' + f.ids.project) return route.fulfill({ json: { ...f.project, status: source.complete ? 'Complete' : 'Active', permissions: { ...f.project.permissions, needsReason: source.complete } } })
        if (name === 'projects/' + f.ids.project + '/external-parties') return route.fulfill({ json: f.parties })
        if (name === 'projects/' + f.ids.project + '/decisions/screen') {
          screenReads.push({ query: Object.fromEntries(url.searchParams), enabled: actor.flags.register_screens === true, refused: refuseScreen })
          if (!actor.flags.register_screens) return problem(404, 'Off')
          if (url.searchParams.size) return problem(400, 'No per-person or row filter is allowed')
          if (refuseScreen) return problem(400, 'Synthetic metrics read refused')
          return route.fulfill({ json: f.screen(source) })
        }
        if (name === 'projects/' + f.ids.project + '/decisions') { listReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: f.rows(source, url.searchParams, actor.flags.register_screens === true) }) }
        if (name.startsWith('decisions/') && source.rows.some(row => 'decisions/' + row.id === name)) { detailReads.push(name); return route.fulfill({ json: f.detail(source, name.split('/')[1], actor) }) }
        if (name === 'people/' + f.ids.requester + '/panel' || name === 'people/' + f.ids.owner + '/panel') return route.fulfill({ json: f.person(name.split('/')[1]) })
        if (shell.has(name)) return route.fallback()
        unknownGets.push(name); return problem(501, 'Unexpected GET: ' + name)
      }
      if (!['POST', 'PATCH'].includes(method)) { unexpected.push(method + ' ' + name); return problem(405, 'Unexpected method') }
      const body = request.postDataJSON(), key = request.headers()['idempotency-key'], version = request.headers()['if-match']
      const correction = operation => {
        correctionRefusals.push({ operation, version, body })
        return route.fulfill({ status: 400, contentType: 'application/problem+json', json: { status: 400, code: 'validation', detail: 'Synthetic Complete project correction refused', errors: { reason: ['Give a reason of at least 5 characters.'] } } })
      }
      if (method === 'PATCH' && name === 'decisions/' + createdId && source.complete) {
        const row = source.rows.find(row => row.id === createdId), metadata = source.metadata.get(createdId)
        if (version !== '"' + row.rowVersion + '"') { unexpected.push('Complete context version promoted or stale'); return problem(409, 'Wrong context version') }
        if (!body.reason || body.reason.trim().length < 5) return correction('context')
        if ('rationale' in body) { unexpected.push('Pending Complete rationale must not be sent'); return problem(403, 'Forbidden rationale') }
        metadata.categoryId = body.categoryId; metadata.optionsConsidered = body.optionsConsidered; row.rowVersion++; source.applied++
        return route.fulfill({ json: { id: row.id, rowVersion: row.rowVersion } })
      }
      if (method === 'POST' && name === 'decisions/' + createdId + '/transition' && source.complete && body.toStatus === 'Decided') {
        if (completeReceipt) {
          if (key !== completeReceipt.key || version !== completeReceipt.version || JSON.stringify(body) !== completeReceipt.body) { unexpected.push('Complete retry changed reason/body/key/version'); return problem(409, 'Changed Complete intent') }
          return route.fulfill({ headers: { 'Idempotent-Replayed': 'true' }, json: completeReceipt.result })
        }
        const row = source.rows.find(row => row.id === createdId), metadata = source.metadata.get(createdId)
        if (!key || body.rowVersion !== row.rowVersion || version !== '"' + row.rowVersion + '"') { unexpected.push('Complete outcome version promoted or stale'); return problem(409, 'Wrong Complete version') }
        if (!body.reason || body.reason.trim().length < 5) return correction('outcome')
        row.status = 'Decided'; row.decisionText = body.decisionText; row.decisionDate = body.decisionDate; row.rowVersion++; metadata.rationale = body.rationale; source.applied++
        completeReceipt = { body: JSON.stringify(body), key, version, result: { id: row.id, rowVersion: row.rowVersion, status: row.status } }
        return problem(503, 'Synthetic Complete outcome reply lost after commit')
      }
      if (method === 'PATCH' && name === 'decisions/' + f.ids.pending) {
        if ('rationale' in body) { unexpected.push('Pending rationale must not be sent'); return problem(403, 'Forbidden rationale') }
        return problem(409, 'Synthetic stale version', 'concurrency_conflict')
      }
      if (method === 'POST' && name === 'decisions/' + f.ids.pending + '/transition' && body.toStatus === 'Decided') {
        if (!outcomeReceipt) {
          if (!key || body.rowVersion !== 1 || version !== '"1"' || !body.rationale || !body.decisionText) { unexpected.push('Invalid original outcome intent'); return problem(400, 'Bad outcome intent') }
          const row = source.rows.find(row => row.id === f.ids.pending), metadata = source.metadata.get(f.ids.pending)
          row.status = 'Decided'; row.decisionText = body.decisionText; row.decisionDate = body.decisionDate; row.rowVersion = 2; metadata.rationale = body.rationale; source.applied++
          outcomeReceipt = { body: JSON.stringify(body), key, version, result: { id: row.id, rowVersion: row.rowVersion, status: row.status } }
          return problem(503, 'Synthetic outcome reply lost after commit')
        }
        if (key !== outcomeReceipt.key || version !== outcomeReceipt.version || JSON.stringify(body) !== outcomeReceipt.body) { unexpected.push('Outcome retry changed original intent/version/key'); return problem(409, 'Changed intent') }
        return route.fulfill({ headers: { 'Idempotent-Replayed': 'true' }, json: outcomeReceipt.result })
      }
      if (method === 'POST' && name === 'projects/' + f.ids.project + '/decisions') {
        if (!createReceipt) {
          if (!key || body.subject !== 'Synthetic created decision' || body.ownerExternalPartyId !== f.ids.party || body.ownerUserId !== null || body.categoryId !== f.ids.category || body.optionsConsidered?.length !== 2) { unexpected.push('Invalid original create intent'); return problem(400, 'Bad create intent') }
          const row = { ...source.rows[0], id: createdId, key: 'DEC-CREATED', subject: body.subject, status: 'Pending', decisionText: null, decisionDate: null, rowVersion: 1, ownerExternalPartyId: body.ownerExternalPartyId, ownerUserId: null,
            requestedById: body.requestedById ?? actor.id, requestedByName: actor.displayName, dateRequested: body.dateRequested, requiredByDate: body.requiredByDate, originalRequiredByDate: body.requiredByDate, daysUntil: 6 }
          source.rows.push(row); source.metadata.set(createdId, { categoryId: body.categoryId, optionsConsidered: body.optionsConsidered, rationale: null, canEditRationale: false, correctionReasonRequired: source.complete }); source.applied++
          createReceipt = { body: JSON.stringify(body), key, result: { id: row.id, key: row.key, rowVersion: row.rowVersion } }
          return problem(503, 'Synthetic create reply lost after commit')
        }
        if (key !== createReceipt.key || JSON.stringify(body) !== createReceipt.body) { unexpected.push('Create retry changed original intent/key'); return problem(409, 'Changed intent') }
        return route.fulfill({ status: 201, headers: { 'Idempotent-Replayed': 'true' }, json: createReceipt.result })
      }
      unexpected.push(method + ' ' + name); return problem(405, 'Unexpected business command')
    })
    const goto = value => page.goto(base + value), lag = () => page.locator('#decision-organisation-lag')
    const screenshot = async name => { if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: join(process.env.TUESDAY_EVIDENCE_DIR, name + '.png') }) }
    const client = () => lag().locator('[data-decision-lag-kind="External"][data-decision-lag-organisation="Synthetic client organisation"]')
    const filterFields = '#decision-status,#decision-owner-type,#decision-owner,#decision-impact,#decision-required-from,#decision-required-to'
    const openFilters = async () => {
      const phone = await page.evaluate(() => matchMedia('(max-width: 1023.98px)').matches)
      const trigger = page.locator('#content [data-slot="' + (phone ? 'sheet-trigger' : 'popover-trigger') + '"]').filter({ hasText: /^Filters/ })
      await trigger.waitFor({ state: 'visible' }); if (await trigger.getAttribute('aria-expanded') !== 'true') await trigger.click()
      await page.locator('[data-slot="' + (phone ? 'sheet-content' : 'popover-content') + '"][data-state="open"]').filter({ has: page.locator('#decision-status') }).waitFor({ state: 'visible' })
    }
    const closeFilters = async () => { const fields = await page.locator(filterFields).all(); await page.keyboard.press('Escape'); await Promise.all(fields.map(field => field.waitFor({ state: 'hidden' }))) }
    const cancelEditor = async editor => {
      const question = page.getByRole('dialog', { name: 'Keep editing or discard?', exact: true })
      await editor.getByRole('button', { name: 'Cancel', exact: true }).click()
      // useSafeForm closes directly with data_entry_safety off; only its actual leave dialog authorizes Discard.
      await Promise.race([editor.waitFor({ state: 'hidden' }), question.waitFor({ state: 'visible' })])
      const confirmed = await question.isVisible()
      if (confirmed) { await question.getByRole('button', { name: 'Discard', exact: true }).click(); await question.waitFor({ state: 'hidden' }) }
      await editor.waitFor({ state: 'hidden' })
      return confirmed ? 'confirmed discard' : 'direct explicit Cancel'
    }
    await goto(path); await client().getByText('Median: 10 days', { exact: true }).waitFor()
    await lag().getByText('Median: 7.5 days', { exact: true }).waitFor(); await lag().getByText('7 decided; 5 attributed. Excluded for dates: 1; for organisation: 1.', { exact: true }).waitFor()
    assert.equal(await lag().locator('[data-person-panel-trigger]').count(), 0); assert.equal(await lag().getByText('Synthetic recorder', { exact: false }).count(), 0); assert.equal(await lag().getByText('Synthetic contact A', { exact: false }).count(), 0)
    await screenshot('decision-metadata-desktop-lag')
    await page.setViewportSize({ width: 375, height: 800 })
    const person = page.locator('#content [data-person-panel-trigger="' + f.ids.requester + '"]:visible').last()
    await person.scrollIntoViewIfNeeded(); await person.focus()
    const personOrigin = page.url(), personFocus = await person.getAttribute('id'), scrollBefore = await page.locator('#content').evaluate(node => node.scrollTop)
    assert.ok(scrollBefore > 0, 'Person return starts from actual nonzero phone card scroll')
    await page.keyboard.press('Enter'); await page.locator('[data-person-panel="' + f.ids.requester + '"]').waitFor()
    await page.keyboard.press('Escape'); await page.waitForURL(personOrigin); await page.waitForFunction(id => document.activeElement?.id === id, personFocus)
    assert.ok(Math.abs(await page.locator('#content').evaluate(node => node.scrollTop) - scrollBefore) <= 2)
    await page.setViewportSize({ width: 1440, height: 1000 })
    await openFilters(); await page.locator('#decision-status').selectOption('Pending'); await page.waitForURL(url => url.searchParams.get('status') === 'Pending'); await closeFilters()
    await page.getByRole('button', { name: 'Synthetic pending decision', exact: true }).waitFor(); await client().getByText('Median: 10 days', { exact: true }).waitFor()
    assert.ok(screenReads.every(read => Object.keys(read.query).length === 0))
    observations.push('AC69: client4/10/12→10, internal4/11→7.5, full-project basis/denominator and no contact/recorder/person figures despite row filter')

    const opener = page.getByRole('button', { name: 'Synthetic pending decision', exact: true }), original = page.url()
    await opener.click(); await page.waitForURL(url => url.searchParams.get('panel') === 'Decision:' + f.ids.pending)
    const panel = page.getByRole('dialog', { name: 'Decision', exact: true })
    await panel.getByRole('heading', { name: 'Decision context', exact: true }).waitFor()
    await panel.getByText('Synthetic retired category (Inactive)', { exact: true }).waitFor()
    await screenshot('decision-metadata-desktop-context')
    await panel.getByRole('button', { name: 'Edit decision context', exact: true }).click()
    const editor = page.getByRole('dialog', { name: 'Edit decision context', exact: true })
    assert.equal(await editor.getByRole('textbox', { name: 'Rationale', exact: true }).count(), 0)
    await editor.getByRole('textbox', { name: 'Option 1', exact: true }).fill('Changed draft option')
    await editor.getByRole('button', { name: 'Save', exact: true }).click(); await editor.getByText('This decision changed. Close this form and review it before starting another save.', { exact: true }).waitFor()
    assert.equal(await editor.getByRole('textbox', { name: 'Option 1', exact: true }).inputValue(), 'Changed draft option')
    assert.equal(await editor.getByRole('button', { name: 'Save', exact: true }).isDisabled(), true)
    const beforeStaleCancel = writes.length, stalePatch = writes.at(-1)
    assert.equal(stalePatch.method, 'PATCH'); assert.equal(stalePatch.name, '/api/v1/decisions/' + f.ids.pending); assert.equal(stalePatch.version, '"1"')
    assert.equal(JSON.parse(stalePatch.body).optionsConsidered[0], 'Changed draft option')
    const cancelMode = await cancelEditor(editor)
    assert.equal(writes.length, beforeStaleCancel, 'Stale Cancel/departure makes no extra command')
    assert.equal(source.rows.find(row => row.id === f.ids.pending).rowVersion, 1); assert.equal(source.applied, 0)
    assert.equal(source.metadata.get(f.ids.pending).optionsConsidered[0], 'Synthetic option A')
    observations.push('AC69: inactive category retained, genuine Person return, pending rationale hidden, stale refusal retains draft and original version; ' + cancelMode + ' makes no extra command')

    await page.setViewportSize({ width: 375, height: 800 })
    await panel.getByRole('button', { name: 'Record decision', exact: true }).click()
    const outcome = page.locator('[data-decision-outcome]')
    await outcome.locator('#decision-outcome-text').fill('Synthetic chosen outcome'); await outcome.locator('#decision-outcome-rationale').fill('Synthetic reason for this outcome')
    await outcome.locator('#decision-outcome-date').fill('2026-11-14'); await screenshot('decision-metadata-phone-outcome'); await outcome.getByRole('button', { name: 'Record decision', exact: true }).click()
    await outcome.getByText('This save may have completed. Retry checks the same request; your entries and original version stay locked.', { exact: true }).waitFor()
    assert.equal(await outcome.locator('#decision-outcome-text').isDisabled(), true); assert.equal(await outcome.locator('#decision-outcome-rationale').inputValue(), 'Synthetic reason for this outcome')
    const beforeRefusedScreen = screenReads.length
    const refusedRead = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/projects/' + f.ids.project + '/decisions/screen' && response.request().method() === 'GET' && response.status() === 400)
    refuseScreen = true
    await outcome.getByRole('button', { name: 'Check the same save', exact: true }).click(); await outcome.waitFor({ state: 'hidden' })
    await refusedRead
    await panel.getByText('Synthetic reason for this outcome', { exact: true }).waitFor(); assert.equal(source.applied, 1)
    assert.ok(screenReads.slice(beforeRefusedScreen).some(read => read.enabled && read.refused && Object.keys(read.query).length === 0), 'Successful same-save recovery causes an actual refused current screen read')
    const beforePanelClose = writes.length
    // The phone sheet keeps the list aria-hidden while open. Close it naturally before reading/retrying its ErrorBanner.
    await panel.getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(original); await panel.waitFor({ state: 'hidden' })
    assert.equal(writes.length, beforePanelClose, 'Closing the saved decision panel makes no command')
    await lag().getByRole('alert').getByText('Synthetic metrics read refused', { exact: true }).waitFor()
    assert.equal(await lag().locator('[data-decision-lag-kind]').count(), 0, 'A refused current-key read cannot display old cached medians')
    refuseScreen = false; await lag().getByRole('button', { name: 'Retry', exact: true }).click(); await client().getByText('Median: 10 days', { exact: true }).waitFor()
    assert.equal(source.applied, 1); assert.equal(writes.length, beforePanelClose)
    observations.push('AC69/84:375px distinct outcome/rationale, lost committed reply retries same body/key/version once; actual current-key refusal withholds cached figures, natural panel Close exposes the canonical ErrorBanner and its real Retry recovers')

    await page.getByRole('button', { name: 'Raise decision', exact: true }).click()
    const create = page.locator('[data-decision-create]')
    await create.locator('[id$="-subject"]').fill('Synthetic created decision'); await create.getByRole('textbox', { name: 'Description', exact: true }).fill('Synthetic creation context'); await create.locator('[id$="-required"]').fill('2026-11-20'); await create.locator('[id$="-impact-description"]').fill('Synthetic impact')
    await create.getByRole('radio', { name: 'An external party', exact: true }).check(); await create.getByRole('combobox', { name: 'An external party', exact: true }).selectOption(f.ids.party)
    await create.getByRole('combobox', { name: 'Category (optional)', exact: true }).selectOption(f.ids.category)
    assert.equal(await create.locator('option[value="' + f.ids.inactive + '"]').count(), 0, 'Inactive category is not a new choice')
    for (let index = 0; index < 2; index++) { await create.getByRole('button', { name: 'Add an option', exact: true }).click(); await create.getByRole('textbox', { name: 'Option ' + (index + 1), exact: true }).fill('Synthetic new option ' + (index + 1)) }
    await create.getByRole('button', { name: 'Raise decision', exact: true }).click(); await create.getByText('This save may have completed. Retry checks the same request; your entries and original version stay locked.', { exact: true }).waitFor()
    assert.equal(await create.locator('[id$="-subject"]').isDisabled(), true)
    await create.getByRole('button', { name: 'Check the same save', exact: true }).click(); await page.waitForURL(url => url.searchParams.get('panel') === 'Decision:' + createdId)
    await page.getByRole('dialog', { name: 'Decision', exact: true }).getByText('Synthetic category', { exact: true }).waitFor()
    assert.equal(source.applied, 2); assert.equal(source.rows.filter(row => row.id === createdId).length, 1)
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    observations.push('AC69:375px typed create uses existing owner picker, mounts link picker, active category and ordered options; lost committed create reply returns one actual ID with immutable intent')
    assert.equal(writes.filter(write => write.method === 'PATCH').length, 1); assert.equal(writes.filter(write => write.name.endsWith('/transition')).length, 2); assert.equal(writes.filter(write => write.name.endsWith('/decisions')).length, 2)
    assert.equal(writes.length, 5, 'Original strict five-attempt scenario is unchanged')

    // Controlled API-fixture races: the project completes after the inspector read. No app/cache/DOM state injection.
    source.complete = true
    const createdPanel = page.getByRole('dialog', { name: 'Decision', exact: true })
    await createdPanel.getByRole('button', { name: 'Edit decision context', exact: true }).click()
    const completeEditor = page.getByRole('dialog', { name: 'Edit decision context', exact: true })
    assert.equal(await completeEditor.getByRole('textbox', { name: 'Reason', exact: true }).count(), 0, 'The shown inspector predated Complete')
    await completeEditor.getByRole('textbox', { name: 'Option 1', exact: true }).fill('Complete context correction')
    const readsBeforeContextRefusal = detailReads.length
    await completeEditor.getByRole('button', { name: 'Save', exact: true }).click()
    await completeEditor.getByText('Give a reason of at least 5 characters.', { exact: true }).waitFor()
    const contextReason = completeEditor.getByRole('textbox', { name: 'Reason', exact: true })
    await contextReason.waitFor(); assert.equal(await contextReason.getAttribute('maxlength'), null)
    assert.equal(detailReads.length, readsBeforeContextRefusal, 'Definite reason refusal does not fetch/promote a new version')
    assert.equal(source.rows.find(row => row.id === createdId).rowVersion, 1)
    await contextReason.fill('Complete project context correction'); await completeEditor.getByRole('button', { name: 'Save', exact: true }).click(); await completeEditor.waitFor({ state: 'hidden' })
    await createdPanel.getByText('Complete context correction', { exact: true }).waitFor(); assert.equal(source.rows.find(row => row.id === createdId).rowVersion, 2)
    await createdPanel.getByRole('button', { name: 'Edit decision context', exact: true }).click()
    const hintedEditor = page.getByRole('dialog', { name: 'Edit decision context', exact: true }), hintedReason = hintedEditor.getByRole('textbox', { name: 'Reason', exact: true })
    await hintedReason.waitFor(); assert.notEqual(await hintedReason.getAttribute('required'), null); assert.equal(await hintedReason.getAttribute('minlength'), '5'); assert.equal(await hintedReason.getAttribute('maxlength'), null)
    const beforeRequiredValidation = writes.length
    await hintedEditor.getByRole('button', { name: 'Save', exact: true }).click(); assert.equal(writes.length, beforeRequiredValidation, 'Known Complete hint requires reason before a command')
    await hintedEditor.getByRole('button', { name: 'Cancel', exact: true }).click(); await hintedEditor.waitFor({ state: 'hidden' })
    // Reopen the fixture's project, read it, then complete it again before recording the outcome.
    source.complete = false; await page.reload(); await createdPanel.getByRole('heading', { name: 'Decision context', exact: true }).waitFor()
    await createdPanel.getByRole('button', { name: 'Record decision', exact: true }).click()
    const completeOutcome = page.locator('[data-decision-outcome]')
    await completeOutcome.locator('#decision-outcome-text').fill('Complete chosen outcome'); await completeOutcome.locator('#decision-outcome-rationale').fill('Complete outcome rationale'); await completeOutcome.locator('#decision-outcome-date').fill('2026-11-14')
    assert.equal(await completeOutcome.getByRole('textbox', { name: 'Reason', exact: true }).count(), 0)
    source.complete = true; const readsBeforeOutcomeRefusal = detailReads.length
    await completeOutcome.getByRole('button', { name: 'Record decision', exact: true }).click(); await completeOutcome.getByText('Give a reason of at least 5 characters.', { exact: true }).waitFor()
    assert.equal(detailReads.length, readsBeforeOutcomeRefusal); assert.equal(source.rows.find(row => row.id === createdId).rowVersion, 2)
    const outcomeReason = completeOutcome.getByRole('textbox', { name: 'Reason', exact: true })
    await outcomeReason.waitFor(); assert.equal(await outcomeReason.getAttribute('maxlength'), null)
    await outcomeReason.fill('Complete project outcome correction'); await completeOutcome.getByRole('button', { name: 'Record decision', exact: true }).click()
    await completeOutcome.getByText('This save may have completed. Retry checks the same request; your entries and original version stay locked.', { exact: true }).waitFor()
    assert.equal(await outcomeReason.isDisabled(), true); assert.equal(await outcomeReason.inputValue(), 'Complete project outcome correction')
    await completeOutcome.getByRole('button', { name: 'Check the same save', exact: true }).click(); await completeOutcome.waitFor({ state: 'hidden' })
    await createdPanel.getByText('Complete outcome rationale', { exact: true }).waitFor()
    assert.deepEqual(correctionRefusals.map(refusal => refusal.operation), ['context', 'outcome'])
    assert.equal(correctionRefusals[0].version, '"1"'); assert.equal(correctionRefusals[1].version, '"2"'); assert.equal(completeReceipt.version, '"2"')
    assert.equal(source.applied, 4)
    observations.push('Complete: definite missing-reason context/outcome refusals retain original versions; supplied canonical reason succeeds, no new maxlength, and lost committed outcome retries identical reason/body/key/version')

    const beforeOff = screenReads.length
    actor = f.actorFor(false); await goto(path)
    await page.getByRole('heading', { name: 'Decisions', exact: true }).waitFor()
    assert.equal(await lag().count(), 0); assert.equal(screenReads.length, beforeOff)
    await page.getByRole('button', { name: 'Synthetic client choice 0', exact: true }).click()
    assert.equal(await page.getByRole('heading', { name: 'Decision context', exact: true }).count(), 0)
    const legacyPanel = page.getByRole('dialog', { name: 'Decision', exact: true })
    await legacyPanel.getByRole('button', { name: 'Close', exact: true }).click(); await legacyPanel.waitFor({ state: 'hidden' }); await page.waitForURL(url => !url.searchParams.has('panel'))
    await page.getByRole('button', { name: 'Raise decision', exact: true }).click()
    const old = page.getByRole('dialog', { name: 'Raise decision', exact: true })
    assert.equal(await old.getByRole('combobox', { name: 'Category', exact: true }).count(), 0); assert.equal(await old.getByRole('button', { name: 'Add an option', exact: true }).count(), 0)
    await old.getByRole('button', { name: 'Cancel', exact: true }).click()
    assert.deepEqual(unknownGets, []); assert.deepEqual(ui.mock.unknownGets, []); assert.deepEqual(unexpected, []); assert.deepEqual(errors, [])
    assert.equal(writes.filter(write => write.method === 'PATCH').length, 3); assert.equal(writes.filter(write => write.name.endsWith('/transition')).length, 5); assert.equal(writes.filter(write => write.name.endsWith('/decisions')).length, 2)
    assert.equal(writes.length, 10); assert.ok(screenReads.every(read => Object.keys(read.query).length === 0)); assert.ok(listReads.some(query => query.status === 'Pending'))
    observations.push('AC84: real flag-off replies/requests/forms keep legacy arrays/actions; no enhanced read/metadata/rationale inputs or unexpected commands')
    console.log(JSON.stringify({ status: 'PASS', suite: 'decision-metadata', evidence: 'synthetic actual-SPA only; provider tests own canonical authority/math/replay/persistence', observations, attempts: writes.length, logicalMutations: source.applied, screenReads: screenReads.length, unexpectedGets: unknownGets.length }))
  } catch (error) {
    try { console.error(JSON.stringify({ status: 'FAIL', suite: 'decision-metadata', url: page.url().slice(0, 2000), recentScreens: screenReads.slice(-8), recentLists: listReads.slice(-8), attempts: writes.slice(-6).map(write => ({ ...write, body: write.body?.slice(0, 1500) })), unexpected, unknownGets, observations })) } catch {}
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
