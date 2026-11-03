// AC-SCR-63/64/84: strict actual-SPA synthetic interactions; provider suites prove real persistence/authority.
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const path = require('node:path')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { referenceData } = require('./me.fixture.cjs')
const { reviewDisciplineFixture } = require('./review-discipline-patterns.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
;(async () => {
  const saved = { flags: { ...me.flags }, capabilities: { ...me.capabilities }, settings: { ...me.settings } }, f = reviewDisciplineFixture(project, me.id)
  // The isolated existing native-visibility helper permits real tab switching; Playwright's default focus emulation would hide this regression.
  const { browser, page, mock } = await newMockPage(base, { events: false, nativeVisibility: true }), unknown = [], writes = [], errors = [], cases = [], screenshots = []
  let settings = f.settings(), armedPut = null, armedPost = false, summaryRefused = false, suggestionMode = 'normal', deferred = null, nextDeferred = false
  const reads = [], settingsReads = [], suggestionReads = []
  let settingsReadFailure = false, catalogueFailure = ''
  const backgroundFailures = []
  page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
  const screenshot = async name => { if (!process.env.TUESDAY_EVIDENCE_DIR) return; await fs.mkdir(process.env.TUESDAY_EVIDENCE_DIR, { recursive: true }); const file = path.join(process.env.TUESDAY_EVIDENCE_DIR, `review-disciplines-${name}.png`); await fs.writeFile(file, await page.screenshot({ fullPage: false }), { flag: 'wx' }); screenshots.push(file) }
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false])); Object.assign(me.flags, { register_screens: true }); me.capabilities = { ...me.capabilities, admin: true }; me.settings.today = '2026-11-02'
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), p = url.pathname.replace('/api/v1/', ''), method = request.method()
      if (method !== 'GET') {
        if (p === 'me/sign-in') return route.fallback()
        const body = request.postDataJSON(); writes.push({ method, path: p, body, version: request.headers()['if-match'] })
        if (method === 'PUT' && p === `admin/deliverableTypes/${f.typeId}/review-defaults` && armedPut) {
          const mode = armedPut; armedPut = null; assert.equal(String(request.headers()['if-match']).replaceAll('"', ''), '7'); assert.equal(body.rowVersion, 7)
          if (mode === 'stale') return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Synthetic stale defaults refusal' } })
          settings = { ...settings, rowVersion: 8, disciplineIds: body.disciplineIds }
          if (mode === 'abort') return route.abort('failed')
          return route.fulfill({ json: settings })
        }
        if (method === 'POST' && p === `projects/${project.id}/reviews` && armedPost) {
          armedPost = false; assert.equal(body.assignments.length, 1); assert.equal(body.assignments[0].projectDisciplineId, f.civil); assert.equal(body.assignments[0].reviewerId, f.reviewer)
          assert.ok(body.requestId); assert.equal(Object.hasOwn(body, 'defaultDisciplineIds'), false)
          return route.fulfill({ status: 409, json: { code: 'synthetic_review_refusal', detail: 'Keep the explicit review choices' } })
        }
        unknown.push(`${method} ${p}`); return route.fulfill({ status: 409, json: { detail: 'Unarmed review-discipline write' } })
      }
      reads.push(p)
      if (p === `projects/${project.id}/reviews/discipline-summary` && !url.search) return route.fulfill(summaryRefused ? { status: 403, json: { detail: 'Summary access refused' } } : { json: f.summary })
      if (p === `projects/${project.id}/reviews/default-disciplines`) {
        assert.deepEqual([...url.searchParams.keys()], ['sourceRevisionIds']); const ids = url.searchParams.get('sourceRevisionIds').split(',')
        assert.ok(ids.length > 0 && ids.every(id => [f.sourceA, f.sourceB].includes(id))); assert.equal(new Set(ids).size, ids.length); suggestionReads.push([...ids])
        const data = f.suggestions(ids)
        if (suggestionMode === 'empty') data.disciplines = []
        if (suggestionMode === 'inactive') data.disciplines[1].state = 'NotInProject'
        if (suggestionMode === 'failure') return route.fulfill({ status: 403, json: { detail: 'Defaults access refused' } })
        if (nextDeferred) { nextDeferred = false; await new Promise(resolve => { deferred = resolve }) }
        return route.fulfill({ json: data })
      }
      if (p === `projects/${project.id}/review-assignment-guidance`) { assert.deepEqual([...url.searchParams.keys()], ['sourceRevisionIds']); return route.fulfill({ json: f.guidance }) }
      if ([`projects/${project.id}/changes/options`, `projects/${project.id}/reviews/options`].includes(p) && !url.search) return route.fulfill({ json: f.options })
      if (p === `projects/${project.id}/reviews`) return route.fulfill({ json: f.packages })
      if (p === `projects/${project.id}/review-findings`) return route.fulfill({ json: f.findings })
      if (p === 'admin/deliverableTypes' && !url.search) {
        if (catalogueFailure === 'types') { backgroundFailures.push(p); return route.fulfill({ status: 503, json: { detail: 'Synthetic type catalogue read failure' } }) }
        return route.fulfill({ json: [{ ...f.type, rowVersion: settings.rowVersion }] })
      }
      if (p === 'admin/disciplines' && !url.search) {
        if (catalogueFailure === 'disciplines') { backgroundFailures.push(p); return route.fulfill({ status: 503, json: { detail: 'Synthetic discipline catalogue read failure' } }) }
        return route.fulfill({ json: f.disciplines })
      }
      if (p === `admin/deliverableTypes/${f.typeId}/review-defaults` && !url.search) {
        if (settingsReadFailure) { backgroundFailures.push(p); return route.fulfill({ status: 503, json: { detail: 'Synthetic settings background read failure' } }) }
        settingsReads.push(settings.rowVersion); return route.fulfill({ json: settings })
      }
      if (p === 'reference') return route.fulfill({ json: referenceData() })
      if (['config', 'me', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count', `projects/${project.id}`, `projects/${project.projectNumber}`].includes(p)) return route.fallback()
      unknown.push(`GET ${p}${url.search}`); return route.fulfill({ status: 501, json: { detail: `Unlisted review-discipline GET ${p}` } })
    })
    const root = `${base}/projects/${project.projectNumber}/reviews`, summary = () => page.getByRole('region', { name: 'Review rounds by discipline' })
    const form = () => page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'New review package', exact: true }) })
    const required = () => form().getByRole('group', { name: 'Required disciplines', exact: true })
    const open = async () => { await page.goto(root); await page.getByRole('button', { name: 'New review package', exact: true }).click(); await form().waitFor() }
    const choose = async source => form().getByRole('checkbox', { name: new RegExp(`^D-${source === f.sourceA ? '1' : '2'}`) }).check()
    const cancel = async () => { const count = writes.length; await form().getByRole('button', { name: 'Cancel', exact: true }).click(); await form().waitFor({ state: 'detached' }); assert.equal(writes.length, count) }
    const backgroundRefetch = async () => {
      const away = await page.context().newPage()
      try {
        await away.goto('about:blank'); await away.bringToFront(); await page.waitForFunction(() => document.visibilityState === 'hidden')
        // Observe the real15s production stale window. No clock/focus/query-cache injection.
        await new Promise(resolve => setTimeout(resolve, 15_100))
        await page.bringToFront(); await page.waitForFunction(() => document.visibilityState === 'visible')
      } finally { await page.bringToFront(); await away.close() }
    }
    await page.goto(`${root}?status=Draft&q=unrelated&page=2`); await summary().getByRole('columnheader', { name: 'Median days to discipline decision', exact: true }).waitFor()
    const civil = summary().getByRole('row').filter({ has: page.getByRole('rowheader', { name: 'Civil', exact: true }) }); assert.ok((await civil.innerText()).includes('3 / 3')); assert.ok((await civil.innerText()).includes('6 / 6'))
    const geo = summary().getByRole('row').filter({ has: page.getByRole('rowheader', { name: 'Geotechnical', exact: true }) }); assert.ok((await geo.innerText()).includes('0 / 0')); assert.ok((await geo.innerText()).includes('—')); await screenshot('desktop-summary')
    cases.push('AC63: discipline-only summary ignores unrelated list/page filters, shows exact denominators and unknown rather than zero metrics')
    await open(); await choose(f.sourceA); await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).waitFor()
    await page.waitForFunction(id => document.querySelector(`input#review-due-${id}`) !== null, f.geo)
    assert.equal(await required().getByRole('checkbox', { name: 'Civil', exact: true }).isChecked(), true); assert.equal(await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), true)
    assert.deepEqual(await required().getByRole('combobox', { name: 'Reviewer', exact: true }).evaluateAll(nodes => nodes.map(node => node.value)), ['', ''])
    await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).uncheck(); await required().getByRole('combobox', { name: 'Reviewer', exact: true }).selectOption(f.reviewer)
    await choose(f.sourceB); await form().getByText('Your assignment choices are preserved. Suggestions will be added only when you choose.', { exact: true }).waitFor()
    assert.equal(await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), false); assert.equal(await required().getByRole('combobox', { name: 'Reviewer', exact: true }).inputValue(), f.reviewer)
    await form().getByLabel('Title', { exact: true }).fill('Synthetic manual review'); await form().getByLabel('Review purpose and scope', { exact: true }).fill('Check explicit discipline scope'); await form().getByRole('combobox', { name: 'Coordinator', exact: true }).selectOption(me.id)
    armedPost = true; await form().getByRole('button', { name: 'Confirm', exact: true }).click(); await form().getByRole('alert').filter({ hasText: 'Keep the explicit review choices' }).waitFor(); await cancel()
    cases.push('AC64: actual two-drawing suggestions deduplicate, choose no reviewer, preserve removal/reviewer through source change and submit only explicit ordinary assignments')
    await open(); nextDeferred = true; const firstRead = page.waitForRequest(request => request.url().includes('/reviews/default-disciplines?')); await choose(f.sourceA); await firstRead
    await required().getByRole('checkbox', { name: 'Civil', exact: true }).check(); await choose(f.sourceB); await form().getByText('Your assignment choices are preserved. Suggestions will be added only when you choose.', { exact: true }).waitFor()
    assert.ok(deferred); const oldResponse = page.waitForResponse(response => { const url = new URL(response.url()); return url.pathname.endsWith('/reviews/default-disciplines') && url.searchParams.get('sourceRevisionIds') === f.sourceA && response.status() === 200 })
    const release = deferred; deferred = null; release(); await oldResponse; await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    assert.equal(await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), false)
    await form().getByRole('button', { name: 'Add suggested disciplines', exact: true }).click(); await page.waitForFunction(id => document.querySelector(`input#review-due-${id}`) !== null, f.geo); await cancel()
    cases.push('AC64: late old-source response cannot overwrite a manual set; explicit Add suggestions unions it')
    suggestionMode = 'inactive'; await open(); await choose(f.sourceA); await form().getByText('Not configured in this project', { exact: true }).waitFor(); assert.equal(await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), false); await cancel()
    suggestionMode = 'empty'; await open(); await choose(f.sourceA); await form().getByText('No default review disciplines are configured for these types.', { exact: true }).waitFor(); await cancel()
    suggestionMode = 'failure'; await open(); await choose(f.sourceA); await form().getByText('Defaults could not be loaded. Retry or explicitly choose the required disciplines manually.', { exact: true }).waitFor(); await required().getByRole('checkbox', { name: 'Civil', exact: true }).check(); await cancel(); suggestionMode = 'normal'
    me.flags.screen_patterns = true; await open(); await choose(f.sourceA); await form().getByText('Reviewer missing', { exact: false }).first().waitFor(); assert.equal(await form().getByRole('button', { name: 'Confirm', exact: true }).isDisabled(), true); await cancel(); me.flags.screen_patterns = false
    await page.setViewportSize({ width: 375, height: 812 }); await open(); await choose(f.sourceA); await page.waitForFunction(id => document.querySelector(`input#review-due-${id}`) !== null, f.geo); await screenshot('375-default-selector'); await cancel(); await page.setViewportSize({ width: 1440, height: 1000 })
    cases.push('AC64: empty/missing mapping/current refusal, independent screen_patterns gate and375px natural form remain honest')
    me.flags.data_entry_safety = true; await open(); await choose(f.sourceA); await page.waitForFunction(id => document.querySelector(`input#review-due-${id}`) !== null, f.geo)
    await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).uncheck(); await required().getByRole('combobox', { name: 'Reviewer', exact: true }).selectOption(f.reviewer); await form().getByLabel('Title', { exact: true }).fill('Restored synthetic scope')
    await page.waitForFunction(projectId => Object.keys(localStorage).some(key => key.includes(`review.new.${projectId}`)), project.id)
    await page.reload(); await page.getByRole('button', { name: 'New review package', exact: true }).click(); await page.waitForFunction(() => document.querySelector('#review-title')?.value === 'Restored synthetic scope')
    await choose(f.sourceB); await form().getByText('Your assignment choices are preserved. Suggestions will be added only when you choose.', { exact: true }).waitFor()
    assert.equal(await required().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), false); assert.equal(await required().getByRole('combobox', { name: 'Reviewer', exact: true }).inputValue(), f.reviewer); await cancel(); me.flags.data_entry_safety = false
    cases.push('AC64: actual browser draft restoration marks the set manual and preserves deliberate removal/reviewer through new source suggestions')
    summaryRefused = true; await page.goto(root); await page.getByRole('alert').filter({ hasText: 'Summary access refused' }).waitFor(); assert.equal(await summary().count(), 0); summaryRefused = false
    me.flags.data_entry_safety = true; await page.goto(`${base}/admin/reference/deliverableTypes`); await page.getByRole('button', { name: 'Review defaults', exact: true }).click()
    const admin = () => page.locator(`[data-review-default-settings="${f.typeId}"]`)
    await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).check(); const dirtySession = await admin().elementHandle(); assert.ok(dirtySession)
    settingsReadFailure = true; await backgroundRefetch(); await admin().getByRole('alert').filter({ hasText: 'Synthetic settings background read failure' }).waitFor()
    assert.equal(await dirtySession.evaluate(node => node.isConnected), true); assert.equal(await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), true)
    assert.equal(await admin().getByRole('button', { name: 'Save', exact: true }).isDisabled(), true)
    const dirtyWrites = writes.length; await admin().getByRole('button', { name: 'Cancel', exact: true }).click()
    const keep = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true }) }); await keep.getByRole('button', { name: 'Keep editing', exact: true }).click()
    assert.equal(await dirtySession.evaluate(node => node.isConnected), true); assert.equal(writes.length, dirtyWrites)
    settingsReadFailure = false; settings = f.settings(9, [f.geoGlobal])
    const recoveredRead = page.waitForResponse(response => response.url().endsWith(`/admin/deliverableTypes/${f.typeId}/review-defaults`) && response.status() === 200)
    await admin().getByRole('alert').filter({ hasText: 'Synthetic settings background read failure' }).getByRole('button', { name: 'Retry', exact: true }).click(); await recoveredRead
    await admin().getByRole('alert').filter({ hasText: 'Synthetic settings background read failure' }).waitFor({ state: 'detached' })
    assert.equal(await admin().getByRole('checkbox', { name: 'Civil', exact: true }).isChecked(), true); assert.equal(await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), true)
    catalogueFailure = 'types'; await backgroundRefetch(); await admin().getByRole('alert').filter({ hasText: 'Synthetic type catalogue read failure' }).waitFor()
    assert.equal(await dirtySession.evaluate(node => node.isConnected), true); assert.equal(await admin().getByRole('button', { name: 'Save', exact: true }).isDisabled(), true)
    catalogueFailure = ''; await admin().getByRole('alert').filter({ hasText: 'Synthetic type catalogue read failure' }).getByRole('button', { name: 'Retry', exact: true }).click()
    await admin().getByRole('alert').filter({ hasText: 'Synthetic type catalogue read failure' }).waitFor({ state: 'detached' })
    assert.equal(await dirtySession.evaluate(node => node.isConnected), true); armedPut = 'stale'; await admin().getByRole('button', { name: 'Save', exact: true }).click(); await admin().getByRole('alert').filter({ hasText: 'Synthetic stale defaults refusal' }).waitFor()
    assert.deepEqual(writes.at(-1).body.disciplineIds.slice().sort(), [f.civilGlobal, f.geoGlobal].sort()); assert.equal(writes.at(-1).body.rowVersion, 7)
    await admin().getByRole('button', { name: 'Cancel', exact: true }).click(); const discardDirty = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true }) }); await discardDirty.getByRole('button', { name: 'Discard', exact: true }).click(); await admin().waitFor({ state: 'detached' })
    cases.push('P2 regression: real background settings and type-catalogue failures/retries retain the same dirty editor, safe close guard and original version7 despite a server version9 and different set')
    settings = f.settings(); await page.goto(`${base}/admin/reference/deliverableTypes`); await page.getByRole('button', { name: 'Review defaults', exact: true }).click(); await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).check()
    armedPut = 'abort'; await admin().getByRole('button', { name: 'Save', exact: true }).click(); await admin().getByRole('button', { name: 'Check saved defaults', exact: true }).waitFor(); const uncertainSession = await admin().elementHandle(); assert.ok(uncertainSession)
    settingsReadFailure = true; catalogueFailure = 'disciplines'; await backgroundRefetch()
    await admin().getByRole('alert').filter({ hasText: 'Synthetic settings background read failure' }).waitFor(); await admin().getByRole('alert').filter({ hasText: 'Synthetic discipline catalogue read failure' }).waitFor()
    assert.equal(await uncertainSession.evaluate(node => node.isConnected), true); assert.equal(await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), true); assert.equal(await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isDisabled(), true)
    const beforeReconcile = writes.length; settingsReadFailure = false; settings = f.settings(9, [f.geoGlobal])
    await admin().getByRole('button', { name: 'Check saved defaults', exact: true }).click(); await admin().getByRole('alert').filter({ hasText: 'The saved defaults do not match this selection.' }).waitFor()
    assert.equal(await uncertainSession.evaluate(node => node.isConnected), true); assert.equal(await admin().getByRole('checkbox', { name: 'Civil', exact: true }).isChecked(), true); assert.equal(await admin().getByRole('button', { name: 'Save', exact: true }).count(), 0); assert.equal(writes.length, beforeReconcile)
    catalogueFailure = ''; settings = f.settings(10, [f.civilGlobal, f.geoGlobal])
    const settingsRetry = admin().getByRole('alert').filter({ hasText: 'Synthetic settings background read failure' }).getByRole('button', { name: 'Retry', exact: true })
    const catalogueRetry = admin().getByRole('alert').filter({ hasText: 'Synthetic discipline catalogue read failure' }).getByRole('button', { name: 'Retry', exact: true })
    assert.equal(await settingsRetry.getAttribute('type'), 'button'); assert.equal(await catalogueRetry.getAttribute('type'), 'button')
    const defaultsPath = `admin/deliverableTypes/${f.typeId}/review-defaults`, beforeRetryReadCount = reads.filter(p => p === defaultsPath).length, beforeRetrySettingsCount = settingsReads.length
    const settingsRecovered = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/${defaultsPath}` && response.status() === 200)
    await settingsRetry.click(); await settingsRecovered
    await admin().getByRole('alert').filter({ hasText: 'Synthetic settings background read failure' }).waitFor({ state: 'detached' })
    assert.equal(await uncertainSession.evaluate(node => node.isConnected), true); assert.equal(await admin().getByRole('button', { name: 'Check saved defaults', exact: true }).isVisible(), true)
    assert.equal(await admin().getByRole('checkbox', { name: 'Civil', exact: true }).isChecked(), true); assert.equal(await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), true)
    assert.equal(await admin().getByRole('button', { name: 'Save', exact: true }).count(), 0); assert.equal(await admin().getByRole('alert').filter({ hasText: 'The saved defaults do not match this selection.' }).count(), 1)
    assert.equal(writes.length, beforeReconcile); assert.equal(reads.filter(p => p === defaultsPath).length, beforeRetryReadCount + 1); assert.equal(settingsReads.length, beforeRetrySettingsCount + 1)
    const cataloguesRecovered = Promise.all(['admin/deliverableTypes', 'admin/disciplines'].map(p => page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/${p}` && response.status() === 200)))
    await catalogueRetry.click(); await cataloguesRecovered
    await admin().getByRole('alert').filter({ hasText: 'Synthetic discipline catalogue read failure' }).waitFor({ state: 'detached' })
    assert.equal(await uncertainSession.evaluate(node => node.isConnected), true); assert.equal(await admin().getByRole('button', { name: 'Check saved defaults', exact: true }).isVisible(), true)
    assert.equal(await admin().getByRole('checkbox', { name: 'Civil', exact: true }).isChecked(), true); assert.equal(await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).isChecked(), true)
    assert.equal(await admin().getByRole('button', { name: 'Save', exact: true }).count(), 0); assert.equal(await admin().getByRole('alert').filter({ hasText: 'The saved defaults do not match this selection.' }).count(), 1)
    assert.equal(writes.length, beforeReconcile); assert.equal(reads.filter(p => p === defaultsPath).length, beforeRetryReadCount + 1); assert.equal(settingsReads.length, beforeRetrySettingsCount + 1)
    const explicitReconciliation = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/${defaultsPath}` && response.status() === 200)
    await admin().getByRole('button', { name: 'Check saved defaults', exact: true }).click(); await explicitReconciliation; await admin().waitFor({ state: 'detached' }); assert.equal(writes.length, beforeReconcile)
    assert.deepEqual(writes.filter(write => write.method === 'PUT').map(write => write.version.replaceAll('"', '')), ['7', '7']); assert.ok(settingsReads.includes(9)); assert.ok(settingsReads.includes(10))
    assert.ok(backgroundFailures.includes('admin/deliverableTypes')); assert.ok(backgroundFailures.includes('admin/disciplines')); assert.ok(backgroundFailures.includes(`admin/deliverableTypes/${f.typeId}/review-defaults`))
    cases.push('P2 regression: uncertain intent survives simultaneous background settings/discipline catalogue failures, mismatching GET and newer successful retries; only the original matching set reconciles, with no extra PUT')
    me.flags.data_entry_safety = true; await page.goto(`${base}/admin/reference/deliverableTypes`); await page.getByRole('button', { name: 'Review defaults', exact: true }).click(); await admin().getByRole('checkbox', { name: 'Geotechnical', exact: true }).uncheck()
    const cancelWrites = writes.length; await admin().getByRole('button', { name: 'Cancel', exact: true }).click(); const question = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true }) }); await question.getByRole('button', { name: 'Discard', exact: true }).click(); await admin().waitFor({ state: 'detached' }); assert.equal(writes.length, cancelWrites)
    cases.push('AC64: dirty settings Cancel uses the real leave guard and Discard sends no PUT')
    me.flags.register_screens = false; const start = reads.length; await page.goto(root); assert.equal(await summary().count(), 0); await page.goto(`${base}/admin/reference/deliverableTypes`); assert.equal(await page.getByRole('button', { name: 'Review defaults', exact: true }).count(), 0)
    assert.equal(reads.slice(start).filter(p => p.endsWith('/discipline-summary') || p.endsWith('/default-disciplines') || p.endsWith('/review-defaults')).length, 0)
    cases.push('AC84: off hides new reads/forms without rewriting configured defaults')
    assert.deepEqual(errors, []); assert.deepEqual(unknown, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS_SYNTHETIC_BROWSER_ONLY', cases, writes: writes.length, suggestionReads, settingsReads, backgroundFailures, screenshots }))
  } finally { if (deferred) deferred(); me.flags = saved.flags; me.capabilities = saved.capabilities; me.settings = saved.settings; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
