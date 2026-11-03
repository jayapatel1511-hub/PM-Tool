// AC-SCR-77 / AC-SCR-84. Strict synthetic shared LinksPanel/AddLink/Editor proof; API tests own permission, comparison and persistence authority.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task } = require('./platform-fixtures.cjs')
const f = require('./document-link-reference.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const start = `${base}/tasks/${task.id}?groupChanges=false&source=files`

;(async () => {
  const saved = { flags: { ...me.flags }, settings: { ...me.settings } }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const commands = [], unexpectedWrites = [], unexpectedGets = [], errors = [], personReads = [], planningReads = [], cases = []
  const receipts = new Map()
  let data = f.fresh(), armed = null, observedCommand = null, mutations = 0, opener = null, stage = 'setup'
  page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
  // Observe the real Copy path action without claiming access to the operating-system clipboard or a file server.
  await page.addInitScript(() => { window.__referenceClipboard = []; Object.defineProperty(navigator, 'clipboard', { configurable: true,
    value: { writeText: async text => { window.__referenceClipboard.push(text) } } }) })
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false]))
    Object.assign(me.flags, { people_screens: true, data_entry_safety: true }); me.settings.today = '2026-11-02'
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', ''), method = request.method()
      if (method !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        const create = method === 'POST' && path === `items/Task/${task.id}/links`
        const patch = method === 'PATCH' && path === `links/${f.id(17001)}`
        const raw = request.postData(), body = raw ? JSON.parse(raw) : null, headers = request.headers()
        if (!armed || armed.method !== method || (!create && !patch)) {
          unexpectedWrites.push({ method, path, body }); return route.fulfill({ status: 409, json: { code: 'unexpected_reference_write' } })
        }
        const mode = armed; armed = null
        const command = { method, path, raw, body, key: headers['idempotency-key'], version: headers['if-match'] }; commands.push(command)
        if (create) {
          assert.ok(Object.keys(body).every(key => ['url', 'title', 'linkType', 'reference'].includes(key)))
          if (me.flags.people_screens) assert.match(command.key, /^[0-9a-f-]{36}$/i)
          else { assert.equal(command.key, undefined); assert.equal('reference' in body, false) }
        } else {
          assert.deepEqual(Object.keys(body), ['reference']); assert.equal(command.version, `"${mode.version ?? 9}"`)
          assert.equal(command.key, undefined, 'PATCH uses original If-Match, not invented POST receipt semantics')
        }
        if (body.reference) assert.deepEqual(Object.keys(body.reference).sort(), ['declaredRevision', 'sourceSystem', 'stableSourceId'])
        let receipt = command.key ? receipts.get(command.key) : null
        if (receipt) { assert.equal(receipt.raw, raw); assert.equal(receipt.path, path) }
        let result = receipt?.result
        if ((mode.commit || mode.kind === 'success') && !receipt) {
          result = create ? f.create(data, body, me.id) : f.replace(data, f.id(17001), body.reference, me.id)
          mutations += 1
          if (command.key) receipts.set(command.key, { raw, path, result })
        }
        try {
          if (mode.kind === 'abort') return await route.abort('failed')
          if (mode.kind === 'bad-reply') return await route.fulfill({ status: 200, json: mode.reply ?? {} })
          if (mode.kind === 'refusal') return await route.fulfill({ status: mode.status, json: { status: mode.status,
            code: mode.code ?? (mode.status === 409 ? 'concurrency_conflict' : 'reference_refused'), detail: `Synthetic reference refusal ${mode.status}`,
            currentRowVersion: 999, errors: { reference: ['Synthetic source reference blocker'] } } })
          return await route.fulfill({ status: create ? 201 : 200, json: result })
        } finally { observedCommand?.(); observedCommand = null }
      }
      if (path === `items/Task/${task.id}/links`) {
        const shown = me.flags.people_screens ? data : { ...data, links: data.links.map(({ referenceContext, ...row }) => row), inherited: data.inherited.map(({ referenceContext, ...row }) => row) }
        return route.fulfill({ json: shown })
      }
      if (path === `people/${f.registrarId}/panel`) { personReads.push(f.registrarId); return route.fulfill({ json: { ...f.person(f.registrarId, 'Synthetic reference registrar'), isActive: false } }) }
      if (path === 'planning/grid') {
        // PersonPanel's usePeopleCapacity reads the actual inspected User for exactly its one displayed week.
        assert.equal(me.flags.people_screens, true)
        const query = Object.fromEntries(url.searchParams)
        assert.deepEqual(query, { personIds: f.registrarId, from: f.person(f.registrarId, 'Synthetic reference registrar').week, weeks: '1' })
        planningReads.push(query); return route.fallback()
      }
      if (path === `items/Task/${task.id}/activity`) return route.fulfill({ json: { items: [], page: 1, pageSize: 100, totalCount: 0 } })
      if (['config', 'me', 'reference', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count',
        `projects/${project.projectNumber}`, `projects/${project.id}`, `projects/${project.id}/tasks`, `projects/${project.id}/milestones`, `projects/${project.id}/deliverables`,
        `tasks/${task.id}`, `tasks/${task.id}/dependencies`, `items/Task/${task.id}/comments`].includes(path)) return route.fallback()
      unexpectedGets.push(path); return route.fulfill({ status: 501, json: { code: 'unexpected_reference_read', detail: path } })
    })
    const row = id => page.locator(`[data-document-link="${id}"]`)
    const inherited = () => page.locator(`[data-document-link-inherited="${f.id(17020)}"]`)
    const links = () => page.locator('section').filter({ has: page.getByRole('heading', { name: 'Links', exact: true }) })
    const editor = () => page.locator('[data-link-reference-editor]')
    const add = () => page.locator('[data-document-link-add]')
    const frames = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    const reset = () => { data = f.fresh(); receipts.clear() }
    const go = async () => { await page.goto(start); await row(f.id(17001)).waitFor() }
    const edit = async () => {
      await go(); const trigger = row(f.id(17001)).getByRole('button', { name: 'Edit source reference', exact: true })
      opener = await trigger.elementHandle(); await trigger.click(); await editor().waitFor()
    }
    const openAdd = async () => { await go(); await links().getByRole('button', { name: 'Add link', exact: true }).click(); await add().waitFor() }
    const fillReference = async container => {
      await container.getByLabel('Source system', { exact: true }).fill('Explicit manual DMS')
      await container.getByLabel('Stable source ID (optional)', { exact: true }).fill('EXPLICIT-17')
      await container.getByLabel('Declared revision (optional)', { exact: true }).fill('D')
    }
    const fillAdd = async (register = true, address = 'https://example.sharepoint.com/sites/synthetic/drawing-D.pdf') => {
      await add().getByLabel('Address or network path', { exact: true }).fill(address)
      if (register) { await add().getByRole('checkbox', { name: 'Register source details manually', exact: true }).check(); await fillReference(add()) }
    }
    const send = async (container, method, mode) => {
      assert.equal(armed, null); armed = { method, ...mode }
      const observed = new Promise(resolve => { observedCommand = resolve })
      await container.getByRole('button', { name: /^(Save|Add|Retry original request)$/ }).click(); await observed
      if (mode.kind === 'success') await container.waitFor({ state: 'hidden' })
      else await container.getByRole('alert').waitFor()
      return commands.at(-1)
    }
    const cancel = async (container, dirty, restore = false) => {
      const before = commands.length
      await container.getByRole('button', { name: 'Cancel', exact: true }).click()
      if (dirty) {
        const question = page.getByRole('dialog', { includeHidden: true }).filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true, includeHidden: true }) })
        await question.getByRole('button', { name: 'Discard', exact: true }).click(); await question.waitFor({ state: 'detached' })
      }
      await container.waitFor({ state: 'hidden' })
      if (restore) await page.waitForFunction(node => node?.isConnected && document.activeElement === node, opener)
      assert.equal(commands.length, before)
    }

    stage = 'summary-states'; await go()
    await row(f.id(17001)).getByRole('status').getByText('Revision differs: Declared C, current D', { exact: true }).waitFor()
    await row(f.id(17001)).getByText(/^Manually registered/).waitFor()
    assert.equal(await row(f.id(17001)).getByText('93', { exact: true }).count(), 0, 'RowVersion is never a drawing revision')
    await row(f.id(17002)).getByText('Declared D, current D', { exact: true }).waitFor()
    assert.equal(await row(f.id(17002)).locator('[class*="text-good"],[class*="bg-good"]').count(), 0)
    for (const [offset, message] of [[2, 'Declared revision not recorded.'], [3, 'No linked deliverable for comparison.'], [4, 'Linked deliverable changed; register the reference again.'],
      [5, 'Comparison target unavailable.'], [6, 'Current registered revision not recorded.']]) await row(f.id(17001 + offset)).getByText(message, { exact: true }).waitFor()
    assert.equal(await inherited().getByRole('button', { name: 'Edit source reference', exact: true }).count(), 0)
    assert.equal(await inherited().locator('[data-person-panel-trigger]').count(), 0, 'A mismatched registrar descriptor is never guessed into a User')
    assert.equal(await inherited().getByText('Mismatched registrar descriptor', { exact: true }).count(), 0)
    const external = row(f.id(17001)).getByRole('link', { name: 'Synthetic Different reference', exact: true })
    assert.equal(await external.getAttribute('href'), data.links[0].url); assert.equal(await external.getAttribute('target'), '_blank')
    await inherited().getByRole('button', { name: 'Copy path', exact: true }).click()
    assert.deepEqual(await page.evaluate(() => window.__referenceClipboard), [data.inherited[0].url])
    cases.push('AC-SCR-77: labelled C/D warning, neutral match, all unknown/changed/unavailable states, manual provenance, actual registrar only, inherited read-only and HTTP/UNC actions')

    stage = 'registrar-source-return'; await page.setViewportSize({ width: 1440, height: 550 }); await go()
    const registrar = row(f.id(17001)).locator(`[data-person-panel-trigger="${f.registrarId}"]`)
    await registrar.scrollIntoViewIfNeeded(); const registrarFocus = await registrar.getAttribute('id')
    for (let step = 0; step < 160 && await page.evaluate(() => document.activeElement?.id) !== registrarFocus; step++) await page.keyboard.press('Tab')
    assert.equal(await page.evaluate(() => document.activeElement?.id), registrarFocus); await frames()
    const origin = page.url(), beforeScroll = await page.locator('#content').evaluate(node => ({ top: node.scrollTop, left: node.scrollLeft }))
    assert.ok(beforeScroll.top > 0)
    const beforePersonWrites = commands.length
    await page.keyboard.press('Enter'); await page.locator(`[data-person-panel="${f.registrarId}"]`).waitFor()
    await page.keyboard.press('Escape'); await page.waitForURL(origin)
    await page.waitForFunction(id => document.activeElement?.id === id, registrarFocus); await frames()
    const returned = await page.locator('#content').evaluate(node => ({ top: node.scrollTop, left: node.scrollLeft }))
    assert.ok(Math.abs(returned.top - beforeScroll.top) <= 2); assert.ok(Math.abs(returned.left - beforeScroll.left) <= 2)
    assert.equal(commands.length, beforePersonWrites); assert.equal(new URL(page.url()).searchParams.get('source'), 'files')
    cases.push('AC-SCR-77: known registrar Person returns to exact task/query/source scroll and natural keyboard focus without writes')

    stage = 'editor-normal'; await page.setViewportSize({ width: 1440, height: 1000 }); reset(); await edit()
    if (process.env.TUESDAY_SCREENSHOT_DIR) {
      await page.evaluate(async () => { await document.fonts.ready; await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))) })
      await page.screenshot({ path: require('node:path').join(process.env.TUESDAY_SCREENSHOT_DIR, 'reference-editor-desktop1.png'), animations: 'disabled' })
    }
    await cancel(editor(), false, true)
    await edit(); await fillReference(editor()); await cancel(editor(), true, true)
    const originalAuthor = { by: data.links[0].addedBy, at: data.links[0].addedAt }
    await edit(); await fillReference(editor()); const replaced = await send(editor(), 'PATCH', { kind: 'success' })
    assert.deepEqual(replaced.body, { reference: { sourceSystem: 'Explicit manual DMS', stableSourceId: 'EXPLICIT-17', declaredRevision: 'D' } })
    await row(f.id(17001)).getByText('Declared D, current D', { exact: true }).waitFor()
    assert.deepEqual({ by: data.links[0].addedBy, at: data.links[0].addedAt }, originalAuthor)
    await edit(); await editor().getByRole('radio', { name: 'Clear registration', exact: true }).check()
    const cleared = await send(editor(), 'PATCH', { kind: 'success', version: 10 })
    assert.deepEqual(cleared.body, { reference: null }); await row(f.id(17001)).getByText('Declared revision not recorded.', { exact: true }).waitFor()
    assert.deepEqual({ by: data.links[0].addedBy, at: data.links[0].addedAt }, originalAuthor)
    cases.push('AC-SCR-77: clean/dirty Cancel zero PATCH and natural editor-opener focus; replace/explicit-null clear only original-version reference fields and preserve original authorship')

    stage = 'editor-refusals'
    for (const status of [400, 403, 404, 409]) {
      reset(); await edit(); await fillReference(editor()); const before = mutations
      await send(editor(), 'PATCH', { kind: 'refusal', status })
      assert.equal(await editor().getByLabel('Declared revision (optional)', { exact: true }).inputValue(), 'D')
      await editor().getByRole('list', { name: 'Reasons this action was refused' }).getByText('Synthetic source reference blocker', { exact: true }).waitFor()
      assert.equal(mutations, before); assert.equal(data.links[0].rowVersion, 9)
      if (status === 409) assert.equal(await editor().getByRole('button', { name: 'Save', exact: true }).isDisabled(), true)
      await cancel(editor(), true, true)
    }
    reset(); await edit(); await fillReference(editor())
    const uncertainPatch = await send(editor(), 'PATCH', { kind: 'abort', commit: true })
    await editor().getByText(/^The request may have saved\./).waitFor()
    const refusedRetry = await send(editor(), 'PATCH', { kind: 'refusal', status: 409 })
    assert.equal(refusedRetry.raw, uncertainPatch.raw); assert.equal(refusedRetry.version, uncertainPatch.version)
    assert.equal(await editor().getByLabel('Source system', { exact: true }).isDisabled(), true)
    assert.equal(data.links[0].rowVersion, 10); await cancel(editor(), true, true)
    reset(); await edit(); await fillReference(editor())
    const malformed = await send(editor(), 'PATCH', { kind: 'bad-reply', reply: { id: f.id(99999), rowVersion: 10 } })
    await editor().getByText(/^The request may have saved\./).waitFor()
    const recovered = await send(editor(), 'PATCH', { kind: 'success' }); assert.equal(recovered.raw, malformed.raw); assert.equal(recovered.version, malformed.version)
    cases.push('AC-SCR-77: named 400/403/404/409 retain input; stale cannot promote version; uncertain PATCH through409 holds original body/If-Match; invalid reply never clears input')

    stage = 'create-normal'; reset(); await openAdd(); await cancel(add(), false)
    await openAdd(); await fillAdd(); await cancel(add(), true)
    await openAdd(); await fillAdd(); const created = await send(add(), 'POST', { kind: 'success' })
    assert.deepEqual(created.body, { url: 'https://example.sharepoint.com/sites/synthetic/drawing-D.pdf', reference: { sourceSystem: 'Explicit manual DMS', stableSourceId: 'EXPLICIT-17', declaredRevision: 'D' } })
    assert.equal(data.links.at(-1).linkType, 'SharePoint'); assert.equal(data.links.at(-1).referenceContext.reference.registeredBy, me.id)
    await openAdd(); await fillAdd(false, '\\\\fileserver\\synthetic\\created')
    const plain = await send(add(), 'POST', { kind: 'success' }); assert.equal('reference' in plain.body, false); assert.equal(data.links.at(-1).linkType, 'Network Folder')
    cases.push('AC-SCR-77: manual registration is optional and explicit; canonical SharePoint/UNC detection persists; clean/dirty Cancel zero create; no actor/time/target in requests')

    reset(); await openAdd(); await fillAdd()
    const beforeRefusedCreate = mutations, refusedCreate = await send(add(), 'POST', { kind: 'refusal', status: 400 })
    await add().getByRole('list', { name: 'Reasons this action was refused' }).getByText('Synthetic source reference blocker', { exact: true }).waitFor()
    assert.equal(await add().getByLabel('Source system', { exact: true }).inputValue(), 'Explicit manual DMS')
    assert.equal(mutations, beforeRefusedCreate)
    await add().getByLabel('Source system', { exact: true }).fill('Corrected explicit manual DMS')
    const correctedCreate = await send(add(), 'POST', { kind: 'success' })
    assert.notEqual(correctedCreate.key, refusedCreate.key); assert.equal(correctedCreate.body.reference.sourceSystem, 'Corrected explicit manual DMS')
    assert.equal(mutations, beforeRefusedCreate + 1)
    cases.push('AC-SCR-77: definite create refusal retains input and named blockers; explicit correction gets a new key before any uncertainty')

    stage = 'create-uncertainty'
    for (const first of [{ kind: 'abort' }, { kind: 'bad-reply', reply: {} }]) {
      reset(); await openAdd(); await fillAdd(); const before = mutations
      const original = await send(add(), 'POST', { ...first, commit: true })
      await add().getByText(/^The request may have saved\./).waitFor()
      for (const status of [403, 409]) {
        assert.equal(await add().getByLabel('Address or network path', { exact: true }).isDisabled(), true)
        const retried = await send(add(), 'POST', { kind: 'refusal', status, code: status === 409 ? 'idempotency_in_progress' : undefined })
        assert.equal(retried.raw, original.raw); assert.equal(retried.key, original.key); assert.equal(retried.path, original.path)
      }
      const replay = await send(add(), 'POST', { kind: 'success' }); assert.equal(replay.key, original.key); assert.equal(replay.raw, original.raw)
      assert.equal(mutations, before + 1); assert.equal(data.links.length, f.fresh().links.length + 1)
    }
    cases.push('AC-SCR-77: lost/malformed create reply locks original intent; retry after403/in-progress409 retains exact body/path/key and resolves one simulated receipt')

    stage = 'phone'; reset(); await page.setViewportSize({ width: 375, height: 812 }); await edit(); await fillReference(editor())
    if (process.env.TUESDAY_SCREENSHOT_DIR) {
      await page.evaluate(async () => { await document.fonts.ready; await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))) })
      await page.screenshot({ path: require('node:path').join(process.env.TUESDAY_SCREENSHOT_DIR, 'reference-editor-phone1.png'), animations: 'disabled' })
    }
    for (const label of ['Source system', 'Stable source ID (optional)', 'Declared revision (optional)']) {
      const box = await editor().getByLabel(label, { exact: true }).boundingBox(); assert.ok(box && box.x >= 0 && box.x + box.width <= 376)
    }
    assert.equal(await editor().evaluate(node => node.scrollWidth <= node.clientWidth + 1), true); await cancel(editor(), true, true)
    await openAdd(); await fillAdd(); assert.equal(await add().evaluate(node => node.scrollWidth <= node.clientWidth + 1), true); await cancel(add(), true)
    cases.push('AC-SCR-77: 375px manual registration/editor fields and Cancel remain reachable without horizontal overflow')

    stage = 'flag-off'; reset(); me.flags.people_screens = false; await page.setViewportSize({ width: 1440, height: 1000 }); await page.goto(start)
    await links().getByRole('link', { name: 'Synthetic Different reference', exact: true }).waitFor()
    assert.equal(await page.locator('[data-link-reference-state],[data-link-reference-editor],[data-document-link]').count(), 0)
    assert.equal(await links().getByRole('button', { name: 'Edit source reference', exact: true }).count(), 0)
    await links().getByRole('button', { name: 'Add link', exact: true }).click()
    const legacy = page.locator('form').filter({ has: page.locator('#link-url') })
    assert.equal(await legacy.getByRole('checkbox', { name: 'Register source details manually', exact: true }).count(), 0)
    await legacy.locator('#link-url').fill('https://example.test/legacy-link'); await legacy.locator('#link-title').fill('Legacy exact body')
    await legacy.locator('#link-type').selectOption('Other')
    const legacyCommand = await send(legacy, 'POST', { kind: 'success' })
    assert.deepEqual(legacyCommand.body, { url: 'https://example.test/legacy-link', title: 'Legacy exact body', linkType: 'Other' })
    assert.equal(legacyCommand.key, undefined)
    cases.push('AC-SCR-84: original form/IDs/request body/no new header remain unchanged off; no new summary/editor/registration or registrar read')

    assert.equal(armed, null); assert.deepEqual(unexpectedWrites, []); assert.deepEqual(unexpectedGets, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, [])
    assert.deepEqual(personReads, [f.registrarId])
    assert.deepEqual(planningReads, [{ personIds: f.registrarId, from: f.person(f.registrarId, 'Synthetic reference registrar').week, weeks: '1' }])
    console.log(JSON.stringify({ status: 'PASS', scope: 'synthetic shared reference UI only; actual API/permission/comparison/persistence verified separately', cases, commands: commands.length, mutations, personReads, planningReads }))
  } catch (error) {
    let state
    try { state = await page.evaluate(() => ({ url: location.href, active: { id: document.activeElement?.id, text: document.activeElement?.textContent?.slice(0, 180) },
      dialogs: [...document.querySelectorAll('[role="dialog"]')].slice(0, 8).map(node => ({ state: node.getAttribute('data-state'), hidden: node.getAttribute('aria-hidden'), title: node.querySelector('h2')?.textContent?.slice(0, 180) })),
      scroll: { top: document.querySelector('#content')?.scrollTop, left: document.querySelector('#content')?.scrollLeft } })) } catch (diagnosticError) { state = { diagnosticError: String(diagnosticError) } }
    console.error(JSON.stringify({ stage, ...state, completedCases: cases, commands: commands.length, mutations, planningReads, unexpectedGets, unexpectedWrites: unexpectedWrites.length, errors }))
    throw error
  } finally { me.flags = saved.flags; me.settings = saved.settings; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
