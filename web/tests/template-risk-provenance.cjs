// AC-SCR-70/81/84 actual-SPA synthetic proof; real authority/persistence/rollback remain provider-test owned.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./template-risk-provenance.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use only the root-owned local preview')
;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], unknown = [], writes = [], reads = [], observations = [], templateReplies = [], previewReplies = [], provenanceReplies = []
  let actor = f.actorFor(), saveMode = 'stale', refuseProvenance = false
  const templateIds = f.templateIds
  const shell = new Set(['config', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events'])
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() === 'POST' && path === 'me/sign-in') return route.fallback()
      if (request.method() === 'POST' && templateIds.some(id => path === `templates/${id}/preview`)) {
        const body = request.postDataJSON()
        assert.ok(Object.keys(body).every(key => ['officeId', 'startDate', 'milestoneDates', 'disciplineIds'].includes(key)), 'Old preview accepts no new feature input')
        const reply = f.preview(path.split('/')[1], body)
        assert.ok(!Object.hasOwn(reply, 'risks') && !Object.hasOwn(reply, 'screen') && !Object.hasOwn(reply.counts, 'risks'))
        previewReplies.push({ path, body: structuredClone(body), reply: structuredClone(reply) })
        return route.fulfill({ json: reply })
      }
      if (request.method() === 'PUT' && path === `templates/${f.ids.draft}/structure`) {
        const body = request.postDataJSON(); writes.push({ method: request.method(), path, body: structuredClone(body) })
        assert.ok(Object.keys(body).every(key => ['rowVersion', 'header', 'disciplines', 'milestones', 'deliverables', 'tasks', 'dependencies', 'roles', 'risks'].includes(key)))
        assert.ok(body.risks.every(row => Object.keys(row).every(key => ['ref', 'title', 'probability', 'impact', 'mitigation', 'triggerIndicator'].includes(key))))
        if (saveMode === 'stale') {
          if (writes.length === 1) f.advanceVersion()
          return route.fulfill({ status: 409, contentType: 'application/problem+json', json: { status: 409, code: 'concurrency_conflict', detail: 'Synthetic version conflict; keep your edits.', currentRowVersion: 8 } })
        }
        return route.fulfill({ json: f.save(body, actor.flags.register_screens === true) })
      }
      if (request.method() !== 'GET') { unknown.push(request.method() + ' ' + path); return route.fulfill({ status: 405, json: { detail: 'Unexpected business command in template proof' } }) }
      reads.push({ path, query: Object.fromEntries(url.searchParams) })
      if (path === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: actor })
      if (path === 'reference') return route.fulfill({ json: f.reference })
      if (path === 'templates') return route.fulfill({ json: f.list() })
      if (templateIds.some(id => path === 'templates/' + id)) {
        assert.ok([...url.searchParams.keys()].every(key => ['changePage', 'changePageSize'].includes(key)))
        assert.ok(['1', '2'].includes(url.searchParams.get('changePage') ?? '1'))
        assert.equal(url.searchParams.get('changePageSize') ?? '50', '50')
        const reply = f.read(path.split('/')[1], actor.flags.register_screens === true, actor.flags.people_screens === true, Number(url.searchParams.get('changePage') ?? 1))
        templateReplies.push({ path, risk: actor.flags.register_screens === true, people: actor.flags.people_screens === true, reply: structuredClone(reply) })
        return route.fulfill({ json: reply })
      }
      if (templateIds.some(id => path === 'templates/' + id + '/projects')) {
        assert.equal(actor.flags.people_screens, true, 'No enhanced version-project GET while off')
        assert.ok([...url.searchParams.keys()].every(key => ['page', 'pageSize'].includes(key)))
        assert.ok(['1', '2'].includes(url.searchParams.get('page') ?? '1'))
        assert.equal(url.searchParams.get('pageSize'), '20')
        return route.fulfill({ json: f.projects(path.split('/')[1], Number(url.searchParams.get('page') ?? 1), Number(url.searchParams.get('pageSize') ?? 50)) })
      }
      if (path === 'projects') {
        assert.ok([...url.searchParams.keys()].every(key => ['mine', 'pagesize', 'sort', 'includearchived'].includes(key.toLowerCase())), 'Finite project-list and copy-source reads only')
        return route.fulfill({ json: f.projectRows })
      }
      const project = f.projectReads.find(project => path === 'projects/' + project.id || path === 'projects/' + project.projectNumber)
      if (project) return route.fulfill({ json: project })
      const numberingProject = f.projectReads.find(project => path === `projects/${project.id}/deliverable-numbering`)
      if (numberingProject) {
        assert.equal(actor.flags.register_screens, true, 'No numbering read while register screens are off')
        assert.equal([...url.searchParams.keys()].length, 0, 'Numbering read has no query parameters')
        return route.fulfill({ json: f.numberingFor(numberingProject.id) })
      }
      const provenanceProject = f.projectReads.find(project => path === `projects/${project.id}/template-provenance`)
      if (provenanceProject) {
        assert.equal(actor.flags.people_screens, true, 'No enhanced project-provenance GET while off')
        if (refuseProvenance && provenanceProject.id === f.ids.project) return route.fulfill({ status: 404, json: { detail: 'Synthetic current project provenance refused.' } })
        const reply = f.provenanceFor(provenanceProject.id); provenanceReplies.push({ projectId: provenanceProject.id, reply: structuredClone(reply) })
        return route.fulfill({ json: reply })
      }
      if (shell.has(path)) return route.fallback()
      unknown.push('GET ' + path); return route.fulfill({ status: 501, json: { detail: 'Unexpected finite GET: ' + path } })
    })
    await page.goto(base + '/templates/' + f.ids.draft)
    await page.getByLabel('Risk title', { exact: true }).first().waitFor({ state: 'visible' })
    assert.equal(await page.getByLabel('Risk title', { exact: true }).count(), 2)
    await page.locator('[data-template-diff]').getByText(/1 added.*1 removed.*49 changed/).waitFor({ state: 'visible' })
    await page.locator('[data-template-diff] dt').filter({ hasText: /^Name$/ }).first().waitFor({ state: 'visible' })
    const diff = page.getByRole('heading', { name: 'Changes from copied version', exact: true }).locator('..').locator('..')
    await diff.getByRole('button', { name: 'Next', exact: true }).click()
    await page.locator('[data-template-diff]').getByText('Changed synthetic paged task 47', { exact: false }).first().waitFor({ state: 'visible' })
    assert.ok(reads.some(read => read.query.changePage === '2'))
    if (process.env.TUESDAY_EVIDENCE_DIR) {
      await page.getByLabel('Risk title', { exact: true }).first().locator('xpath=ancestor::fieldset').screenshot({ path: process.env.TUESDAY_EVIDENCE_DIR + '/template-risk-editor-desktop.png' })
      await page.locator('[data-template-diff]').screenshot({ path: process.env.TUESDAY_EVIDENCE_DIR + '/template-diff-desktop.png' })
    }
    observations.push('actual generated risk editor and added/removed/changed paged baseline read')

    const title = page.getByLabel('Risk title', { exact: true }).first()
    await title.fill('Keep this original intent')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByText('Synthetic version conflict; keep your edits.', { exact: false }).waitFor({ state: 'visible' })
    assert.equal(await title.inputValue(), 'Keep this original intent')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await page.waitForFunction(() => [...document.querySelectorAll('button')].some(button => button.textContent?.trim() === 'Save' && !button.disabled))
    assert.equal(writes.length, 2); assert.equal(writes[0].body.rowVersion, writes[1].body.rowVersion); assert.deepEqual(writes[0].body, writes[1].body)
    const beforeCancel = writes.length
    await page.getByRole('button', { name: 'Cancel', exact: true }).click()
    await page.waitForFunction(() => document.querySelector('input[id$="-title"]')?.value === 'Synthetic first risk')
    await page.locator('[data-template-draft-version="8"]').waitFor({ state: 'visible' })
    assert.equal(writes.length, beforeCancel)
    saveMode = 'success'; await title.fill('Deliberate edit after Cancel')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('button', { name: 'Save', exact: true }).waitFor({ state: 'visible' })
    await page.waitForFunction(() => [...document.querySelectorAll('button')].some(button => button.textContent?.trim() === 'Save' && button.disabled))
    assert.equal(writes.length, 3); assert.equal(writes[2].body.rowVersion, 8)
    observations.push('409 keeps exact original version/body/input; explicit Cancel writes nothing; deliberate later save uses its actually loaded version')

    await page.getByRole('link', { name: /v3.*Retired/ }).click()
    await page.getByRole('link', { name: '250101 · Synthetic created project 0', exact: true }).waitFor({ state: 'visible' })
    const usage = page.getByRole('heading', { name: 'Projects created from this version', exact: true }).locator('..').locator('..')
    await usage.getByText('21 projects you can view', { exact: true }).waitFor({ state: 'visible' })
    await usage.getByRole('button', { name: 'Next', exact: true }).click()
    await page.getByRole('link', { name: /P-SYN-20/ }).waitFor({ state: 'visible' })
    await page.goto(base + '/projects/' + f.ids.project + '/settings')
    await page.locator('[data-template-provenance]').getByText(/Created from Synthetic source v3.*v5 exists/).waitFor({ state: 'visible' })
    await page.setViewportSize({ width: 375, height: 900 })
    await page.locator('[data-template-provenance]').getByText(/Created from Synthetic source v3.*v5 exists/).waitFor({ state: 'visible' })
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Settings provenance must wrap at375px')
    if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: process.env.TUESDAY_EVIDENCE_DIR + '/template-provenance-phone.png', fullPage: true })
    assert.equal(writes.length, 3)
    observations.push('version-specific21-project pagination and v3/v5 information at375px with no project command')
    const viewedAt = Date.now()
    await page.setViewportSize({ width: 1440, height: 1000 })
    const moreNavigation = page.locator('button[aria-controls="nav-more"]')
    await moreNavigation.waitFor({ state: 'visible' })
    if (await moreNavigation.getAttribute('aria-expanded') === 'false') await moreNavigation.click()
    await page.locator('#nav-more a[href="/templates"]').click()
    await page.waitForURL('**/templates')
    refuseProvenance = true
    // Wait for the established15s QueryClient stale window; do not inject cache/focus/history state.
    await page.waitForFunction(start => Date.now() >= start + 15050, viewedAt, { timeout: 20000 })
    const currentRefusal = page.waitForResponse(response => response.request().method() === 'GET'
      && new URL(response.url()).pathname === `/api/v1/projects/${f.ids.project}/template-provenance` && response.status() === 404)
    await page.goBack()
    const refusedResponse = await currentRefusal
    assert.equal(refusedResponse.status(), 404); assert.equal(new URL(refusedResponse.url()).search, '')
    assert.ok(Date.now() >= viewedAt + 15050, 'Observe the current refused read after the real stale window')
    const refusedSection = page.getByRole('heading', { name: 'Template', exact: true }).locator('..').locator('..')
    await refusedSection.getByRole('alert').getByText('This page or item does not exist, or you cannot view it.', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await page.locator('[data-template-provenance]').count(), 0)
    assert.equal(await refusedSection.getByText(/Created from .*v3|v5 exists/).count(), 0)
    observations.push('actual current provenance GET404 after the real15s stale window shows canonical nondisclosing error and withholds all cached source/newer facts')
    await page.setViewportSize({ width: 375, height: 900 })
    await page.goto(base + '/templates/' + f.ids.draft)
    await page.getByText(/desktop|larger screen|phone/i).first().waitFor({ state: 'visible' })
    assert.equal(await page.getByLabel('Risk title', { exact: true }).count(), 0)
    actor = f.actorFor(false, false); await page.setViewportSize({ width: 1440, height: 1000 }); await page.goto(base + '/templates/' + f.ids.draft)
    await page.getByLabel('Name', { exact: true }).first().waitFor({ state: 'visible' })
    assert.equal(await page.getByLabel('Risk title', { exact: true }).count(), 0)
    assert.equal(await page.locator('[data-template-diff]').count(), 0)

    // Revisit stored-on data with only risk editing enabled: values remain, no S48 request/control is smuggled in.
    const stored = f.read(f.ids.draft, true, true)
    assert.equal(stored.risks.length, 2); assert.equal(stored.risks[0].title, 'Deliberate edit after Cancel')
    const riskOnlyReads = reads.length
    actor = f.actorFor(true, false); refuseProvenance = false
    await page.goto(base + '/templates/' + f.ids.draft)
    await page.locator(`[data-template-draft-version="${stored.rowVersion}"]`).waitFor({ state: 'visible' })
    assert.equal(await page.getByLabel('Risk title', { exact: true }).first().inputValue(), stored.risks[0].title)
    assert.equal(await page.getByLabel('Risk title', { exact: true }).count(), 2)
    assert.equal(await page.getByRole('heading', { name: 'Changes from copied version', exact: true }).count(), 0)
    assert.equal(await page.getByRole('heading', { name: 'Projects created from this version', exact: true }).count(), 0)
    assert.ok(reads.slice(riskOnlyReads).every(read => !read.path.endsWith('/projects') && !read.path.endsWith('/template-provenance')))
    const riskOnlyReply = templateReplies.at(-1)
    assert.equal(riskOnlyReply.risk, true); assert.equal(riskOnlyReply.people, false)
    assert.ok(!Object.hasOwn(riskOnlyReply.reply, 'screen')); assert.deepEqual(riskOnlyReply.reply.risks, stored.risks)
    observations.push('riskON/provenanceOFF is nonempty and preserves recorded seed IDs/values/version without S48 reads or controls')

    // Actual create dialog, source selection and summary use immutable Published GET, not a new preview POST payload.
    const beforeWizardWrites = writes.length, beforeWizardGet = templateReplies.length, beforeWizardPreview = previewReplies.length
    await page.goto(base + '/projects')
    await page.getByRole('button', { name: 'Create project', exact: true }).first().click()
    const wizard = page.getByRole('dialog', { name: 'Create project', exact: true })
    await wizard.locator('#p-num').fill('P-SYN-WIZ')
    await wizard.locator('#p-name').fill('Synthetic source risk preview')
    await wizard.locator('#p-client').selectOption(f.ids.client)
    await wizard.locator('#p-office').selectOption(f.ids.office)
    await wizard.locator('#p-start').fill('2026-11-02')
    await wizard.locator('#p-template').selectOption(f.ids.latest)
    await page.waitForFunction(id => document.querySelector('#p-template')?.value === id, f.ids.latest)
    await wizard.getByRole('button', { name: 'Next', exact: true }).click()
    await wizard.locator('[data-template-risk-preview]').getByText("2 risks will be created as Open, owned by the project's PM.", { exact: true }).waitFor({ state: 'visible' })
    await wizard.locator('[data-template-risk-preview]').getByText('Synthetic first risk', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await wizard.locator('[data-template-risk-preview]').getByText('Deliberate edit after Cancel', { exact: true }).count(), 0, 'Published preview must not read the edited Draft')
    const sourceReads = templateReplies.slice(beforeWizardGet).filter(entry => entry.path === `templates/${f.ids.latest}`)
    assert.ok(sourceReads.length > 0); assert.ok(sourceReads.every(entry => entry.reply.id === f.ids.latest && entry.reply.version === 5 && entry.reply.status === 'Published' && entry.reply.risks.length === 2))
    const wizardPreviews = previewReplies.slice(beforeWizardPreview).filter(entry => entry.path === `templates/${f.ids.latest}/preview`)
    assert.ok(wizardPreviews.length > 0)
    for (const entry of wizardPreviews) {
      assert.equal(entry.body.officeId, f.ids.office); assert.equal(entry.body.startDate, '2026-11-02')
      assert.deepEqual(entry.body.disciplineIds, [f.ids.civil]); assert.deepEqual(entry.body.milestoneDates, {})
      assert.deepEqual(entry.reply.counts, { milestones: 1, deliverables: 0, tasks: 0, dependencies: 0 })
      assert.equal(entry.reply.milestones[0].id, f.ids.latestMilestone); assert.equal(entry.reply.milestones[0].date, '2026-11-02')
      assert.ok(!Object.hasOwn(entry.reply, 'risks'))
    }
    await wizard.getByRole('button', { name: 'Cancel', exact: true }).click()
    await wizard.waitFor({ state: 'hidden' })
    assert.equal(writes.length, beforeWizardWrites, 'Wizard inspection and Cancel issue no project-create command')
    observations.push('actual FromTemplate create-wizard sourceGET supplies two immutable Published risks; legacy preview has real milestone counts and no new fields; Cancel makes no project command')

    // Inverse independent combination: real visible task changes survive while risk values are hidden.
    actor = f.actorFor(false, true)
    await page.goto(base + '/templates/' + f.ids.draft)
    await page.locator('[data-template-diff]').getByText(/0 added.*0 removed.*49 changed/).waitFor({ state: 'visible' })
    await page.getByText('Some template sections are hidden by current feature settings. These counts cover only the displayed sections.', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await page.getByLabel('Risk title', { exact: true }).count(), 0)
    assert.equal(await page.locator('[data-template-diff]').getByText('Deliberate edit after Cancel', { exact: false }).count(), 0)
    const provenanceOnlyReply = templateReplies.at(-1)
    assert.equal(provenanceOnlyReply.risk, false); assert.equal(provenanceOnlyReply.people, true)
    assert.ok(!Object.hasOwn(provenanceOnlyReply.reply, 'risks')); assert.equal(provenanceOnlyReply.reply.screen.draftDiff.totalCount, 49)
    assert.ok(provenanceOnlyReply.reply.screen.draftDiff.incompleteSections.includes('Risk'))
    assert.ok(provenanceOnlyReply.reply.screen.draftDiff.changes.every(change => change.kind === 'Task'))
    await page.goto(base + '/projects/' + f.ids.project + '/settings')
    await page.locator('[data-template-provenance]').getByText(/Created from Synthetic source v3.*v5 exists/).waitFor({ state: 'visible' })
    observations.push('riskOFF/provenanceON retains49 actual visible changes with incompleteSections notice and actual v3/v5 facts, omitting all risk fields rather than faking empty history')

    // Stored-on→bothOFF→bothON affects only projection: verify nonempty IDs/content/source/version return unchanged.
    actor = f.actorFor(false, false)
    const offReads = reads.length
    await page.goto(base + '/templates/' + f.ids.draft)
    await page.getByLabel('Name', { exact: true }).first().waitFor({ state: 'visible' })
    const offReply = templateReplies.at(-1).reply
    assert.ok(!Object.hasOwn(offReply, 'risks') && !Object.hasOwn(offReply, 'screen'))
    assert.equal(await page.getByLabel('Risk title', { exact: true }).count(), 0)
    assert.ok(reads.slice(offReads).every(read => !read.path.endsWith('/projects') && !read.path.endsWith('/template-provenance')))
    actor = f.actorFor(true, true)
    await page.goto(base + '/templates/' + f.ids.draft)
    await page.locator(`[data-template-draft-version="${stored.rowVersion}"]`).waitFor({ state: 'visible' })
    assert.equal(await page.getByLabel('Risk title', { exact: true }).first().inputValue(), stored.risks[0].title)
    const restored = templateReplies.at(-1).reply
    assert.deepEqual(restored.risks, stored.risks); assert.equal(restored.rowVersion, stored.rowVersion)
    assert.deepEqual(restored.screen.draftDiff.source, stored.screen.draftDiff.source)
    assert.equal(restored.screen.draftDiff.totalCount, stored.screen.draftDiff.totalCount)
    assert.ok(restored.screen.draftDiff.totalCount > 0); assert.equal(writes.length, beforeWizardWrites)
    observations.push('stored-on→off→on restores exact nonempty risk IDs/content, version and source-v3 change total without extra mutation')

    // Baseline absence/corruption are real nullable unknown responses, never zero-count successful diffs.
    for (const scenario of [{ id: f.ids.legacyDraft, state: 'NotRecorded' }, { id: f.ids.unavailableDraft, state: 'Unavailable' }]) {
      const state = scenario.state
      await page.goto(base + '/templates/' + scenario.id)
      const section = page.getByRole('heading', { name: 'Changes from copied version', exact: true }).locator('..').locator('..')
      await section.getByText(state === 'NotRecorded' ? 'The copied-from version was not recorded. No comparison is available.' : 'The recorded comparison baseline is unavailable.', { exact: true }).waitFor({ state: 'visible' })
      assert.equal(await section.locator('[data-template-diff]').count(), 0)
      assert.equal(await section.getByText(/\d+ added.*\d+ removed.*\d+ changed/).count(), 0)
      assert.equal(await section.getByText('No changes in the displayed sections.', { exact: true }).count(), 0)
      assert.equal(await section.getByRole('button', { name: 'Next', exact: true }).count(), 0)
      const unknownDiff = templateReplies.at(-1).reply.screen.draftDiff
      assert.equal(unknownDiff.state, state); assert.equal(unknownDiff.counts, null); assert.equal(unknownDiff.totalCount, null); assert.equal(unknownDiff.source, null)
      assert.equal(writes.length, beforeWizardWrites)
    }
    await page.goto(base + '/projects/' + f.ids.unavailableProject + '/settings')
    await page.locator('[data-template-provenance]').getByText('Source template information is incomplete or unavailable.', { exact: true }).waitFor({ state: 'visible' })
    const unavailable = provenanceReplies.at(-1)
    assert.equal(unavailable.projectId, f.ids.unavailableProject); assert.equal(unavailable.reply.state, 'Unavailable')
    assert.equal(unavailable.reply.source, null); assert.equal(unavailable.reply.newer, null); assert.equal(unavailable.reply.currentPublished, null)
    assert.equal(await page.locator('[data-template-provenance]').getByText(/Created from .*v3|v5 exists/).count(), 0)
    const beforeNoSource = provenanceReplies.length, beforeNoSourceRead = reads.length
    await page.goto(base + '/projects/' + f.ids.noSourceProject + '/settings')
    await page.getByRole('heading', { name: /Synthetic no-source project/ }).waitFor({ state: 'visible' })
    assert.equal(await page.locator('[data-template-provenance]').count(), 0)
    assert.equal(await page.getByRole('heading', { name: 'Template', exact: true }).count(), 0)
    assert.equal(provenanceReplies.length, beforeNoSource)
    assert.ok(reads.slice(beforeNoSourceRead).every(read => !read.path.endsWith('/template-provenance')))
    observations.push('visible NotRecorded/Unavailable draft baselines use null unknown counts, partial-source project has real Unavailable facts, and genuine no-source project naturally issues no provenance GET/section')
    assert.deepEqual(errors, []); assert.deepEqual(unknown, [])
    console.log(JSON.stringify({ status: 'PASS', suite: 'template-risk-provenance', evidence: 'synthetic actual-SPA contracts; provider checks own persistence/authority', observations, businessWrites: writes.length, reads: reads.length }))
  } catch (error) {
    console.error(JSON.stringify({ status: 'FAIL', suite: 'template-risk-provenance', url: page.url(), errors, unknown, writes, reads: reads.slice(-12), templateStates: templateReplies.slice(-4).map(entry => ({ path: entry.path, risk: entry.risk, people: entry.people, version: entry.reply.rowVersion, state: entry.reply.screen?.draftDiff?.state, total: entry.reply.screen?.draftDiff?.totalCount })), observations }))
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
