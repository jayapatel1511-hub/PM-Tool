// AC-SCR-71: strict synthetic SPA proof for full resolved counts, exact drilldown and explicit versioned classification.
// AC-SCR-84: flag-off form/body/reads retain the prior issue contract. Provider/API tests prove real authority/persistence.
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const path = require('node:path')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { referenceData } = require('./me.fixture.cjs')
const { issueRootCauseFixture } = require('./issue-root-cause.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
;(async () => {
  const saved = { flags: { ...me.flags }, settings: { ...me.settings } }, f = issueRootCauseFixture(project, me.id)
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], unexpectedGets = [], unexpectedWrites = [], commands = [], reads = [], cases = [], receipts = new Map()
  const screenshots = []
  const capture = async name => {
    if (!process.env.TUESDAY_EVIDENCE_DIR) return
    await fs.mkdir(process.env.TUESDAY_EVIDENCE_DIR, { recursive: true })
    const file = path.join(process.env.TUESDAY_EVIDENCE_DIR, `issue-root-cause-${name}.png`)
    await fs.writeFile(file, await page.screenshot({ fullPage: false }), { flag: 'wx' }); screenshots.push(file)
  }
  let rows = f.fresh(), armed = null, seen = null, summaryRefused = false, choicesEmpty = false
  page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false])); Object.assign(me.flags, { register_screens: true, data_entry_safety: true }); me.settings.today = '2026-11-02'
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        if (request.method() !== 'POST' || path !== `issues/${f.target}/transition` || !armed) {
          unexpectedWrites.push({ method: request.method(), path }); return route.fulfill({ status: 409, json: { detail: 'Unarmed issue mutation' } })
        }
        const mode = armed; armed = null; const raw = request.postData(), body = JSON.parse(raw), headers = request.headers(), key = headers['idempotency-key']
        const command = { raw, body, key, version: headers['if-match'], path }; commands.push(command)
        assert.equal(body.toStatus, 'Resolved'); assert.equal(command.version.replaceAll('"', ''), '7')
        if (me.flags.register_screens) { assert.equal(body.rootCauseCategoryId, f.category); assert.ok(key) } else assert.ok(!Object.hasOwn(body, 'rootCauseCategoryId'))
        let receipt = key ? receipts.get(key) : null
        if (receipt) assert.equal(receipt.raw, raw, 'An uncertain retry must preserve the exact body')
        if (mode.commit || mode.kind === 'success') {
          if (!receipt) {
            const issue = rows.find(row => row.id === f.target); issue.status = 'Resolved'; issue.resolution = body.resolution; issue.resolvedDate = body.resolvedDate; issue.rowVersion = 8
            if (body.rootCauseCategoryId) issue.rootCause = { categoryId: f.category, recordedName: 'Coordination gap' }
            receipt = { raw, result: { id: f.target, status: 'Resolved', rowVersion: 8 } }; if (key) receipts.set(key, receipt)
          }
        }
        try {
          if (mode.kind === 'abort') return await route.abort('failed')
          if (mode.kind === 'malformed') return await route.fulfill({ json: { id: 'wrong', status: 'Resolved', rowVersion: 8 } })
          if (mode.kind === 'refusal') return await route.fulfill({ status: mode.status, json: { code: 'synthetic_refusal', detail: 'Synthetic issue refusal', errors: { rootCauseCategoryId: ['Retain the selected cause and original version'] } } })
          return await route.fulfill({ json: receipt.result })
        } finally { if (seen) { const done = seen; seen = null; done(command) } }
      }
      reads.push(path)
      if (path === 'reference') return route.fulfill({ json: referenceData(me.flags.register_screens ? { issueRootCauses: choicesEmpty ? [] : f.choices } : {}) })
      // IssueMetadata's real request has this exact project and no query. Other projects/queries/origins stay unlisted.
      if (path === `projects/${f.projectId}/changes/options` && url.origin === new URL(base).origin && url.search === '')
        return route.fulfill({ json: f.coordinationOptions })
      if (path === `projects/${project.id}/issues/root-causes`) return route.fulfill(summaryRefused ? { status: 403, json: { detail: 'Summary access refused' } } : { json: f.summary(rows) })
      if (path === `projects/${project.id}/issues`) return route.fulfill({ json: f.list(rows, url.searchParams).map(row => { if (me.flags.register_screens) return row; const copy = { ...row }; delete copy.rootCause; return copy }) })
      const detail = /^issues\/([^/]+)$/.exec(path)
      if (detail) return route.fulfill({ json: f.detail(rows.map(row => { if (me.flags.register_screens) return row; const copy = { ...row }; delete copy.rootCause; return copy }), detail[1]) })
      if ([`issues/${f.target}/locations`, `issues/${f.target}/documents`, `issues/${f.target}/verification`, `issues/${f.target}/reference-impacts`].includes(path)) return route.fulfill({ json: [] })
      if (path === `items/Issue/${f.target}/links` || path === `items/Issue/${f.target}/comments`) return route.fallback()
      if (['config', 'me', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count', `projects/${project.id}`, `projects/${project.projectNumber}`].includes(path)) return route.fallback()
      unexpectedGets.push(`${path}${url.search}`); return route.fulfill({ status: 501, json: { detail: `Unlisted issue GET ${path}${url.search}` } })
    })
    const root = `${base}/projects/${project.projectNumber}/issues`, form = () => page.locator(`[data-issue-root-cause-form="${f.target}"]`)
    const open = async () => { await page.goto(`${root}?status=Open&panel=Issue:${f.target}`); await page.getByRole('button', { name: 'Resolve', exact: true }).click(); await form().waitFor() }
    const cancel = async dirty => {
      const count = commands.length; await form().getByRole('button', { name: 'Cancel', exact: true }).click()
      if (dirty) { const question = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true }) }); await question.getByRole('button', { name: 'Discard', exact: true }).click(); await question.waitFor({ state: 'detached' }) }
      await form().waitFor({ state: 'detached' }); assert.equal(commands.length, count)
    }
    const fill = async () => { await form().getByLabel('Root-cause category', { exact: true }).selectOption(f.category); await form().getByLabel('Resolution', { exact: true }).fill('Checked synthetic resolution') }
    const send = async mode => { armed = mode; const observed = new Promise(resolve => { seen = resolve }); await form().getByRole('button', { name: /^(Resolve|Retry same resolution)$/ }).click(); const command = await observed;
      if (mode.kind === 'success') await form().waitFor({ state: 'detached' }); else await form().getByRole('alert').first().waitFor(); return command }
    await page.goto(`${root}?status=Open&severity=High&ownerId=${me.id}&location=old&document=old&verification=Rejected&view=old&page=2&group=location`)
    const summary = page.getByRole('region', { name: 'Resolved issues by root cause' })
    await summary.getByRole('button', { name: 'Coordination gap 60', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('rootCause') === f.category && url.searchParams.get('status') === 'Resolved')
    const params = new URL(page.url()).searchParams; assert.deepEqual([...params.keys()].sort(), ['group', 'rootCause', 'status']); assert.equal(params.get('group'), 'location')
    await page.getByRole('button', { name: 'Next', exact: true }).waitFor(); cases.push('AC71: 60 unique resolved issues, independent of conflicting filters/page and two visual location groups; exact cleared drilldown URL')
    await capture('desktop-project-summary')
    await summary.getByRole('button', { name: 'Not recorded 3', exact: true }).click(); await page.waitForURL(url => url.searchParams.get('rootCause') === 'not-recorded')
    await page.getByRole('button', { name: 'Legacy unknown 1', exact: true }).waitFor(); cases.push('AC71: unknown is a separate exact resolved cohort')
    await open(); assert.equal(await form().getByLabel('Root-cause category', { exact: true }).inputValue(), ''); await cancel(false)
    await open(); await fill(); const refused = await send({ kind: 'refusal', status: 409 }); assert.equal(refused.version.replaceAll('"', ''), '7'); assert.equal(await form().getByLabel('Root-cause category', { exact: true }).inputValue(), f.category); await cancel(true)
    cases.push('AC71: no default, blank Cancel zero writes, stale refusal retains selection/text/shown version')
    await open(); await fill(); const uncertain = await send({ kind: 'abort', commit: true }); assert.equal(await form().getByLabel('Root-cause category', { exact: true }).isDisabled(), true)
    const later = await send({ kind: 'refusal', status: 403 }); assert.equal(later.key, uncertain.key); assert.equal(later.raw, uncertain.raw)
    const retried = await send({ kind: 'success' }); assert.equal(retried.key, uncertain.key); assert.equal(retried.version, uncertain.version); assert.equal(rows.find(row => row.id === f.target).rowVersion, 8)
    cases.push('AC71: uncertain committed reply stays immutable through later refusal, then exact receipt returns one simulated resolution')
    rows = f.fresh(); await open(); await fill(); const malformed = await send({ kind: 'malformed', commit: true })
    assert.equal(await form().getByLabel('Root-cause category', { exact: true }).isDisabled(), true)
    const recovered = await send({ kind: 'success' }); assert.equal(recovered.key, malformed.key); assert.equal(recovered.raw, malformed.raw)
    cases.push('AC71: wrong-issue successful reply does not close or discard the command; exact receipt recovery preserves its key/body')
    rows = f.fresh(); choicesEmpty = true; await open(); await form().getByText('No active root-cause categories. Ask an administrator to configure the list.', { exact: true }).waitFor(); assert.equal(await form().getByRole('button', { name: 'Resolve', exact: true }).isDisabled(), true); await cancel(false); choicesEmpty = false
    await page.setViewportSize({ width: 375, height: 812 }); await open(); await fill(); assert.ok(await form().getByLabel('Root-cause category', { exact: true }).isVisible()); await capture('375-resolution-selector'); await cancel(true)
    cases.push('AC71: empty catalogue is honest; 375px form retains guarded Cancel')
    summaryRefused = true; await page.goto(root); await page.getByRole('alert').filter({ hasText: 'Summary access refused' }).waitFor(); assert.equal(await page.getByRole('region', { name: 'Resolved issues by root cause' }).count(), 0); summaryRefused = false
    me.flags.register_screens = false; rows = f.fresh(); const readStart = reads.length; await page.goto(`${root}?rootCause=malformed`)
    assert.equal(await page.getByRole('region', { name: 'Resolved issues by root cause' }).count(), 0)
    await page.goto(`${root}?status=Open&panel=Issue:${f.target}`); await page.getByRole('button', { name: 'Resolve', exact: true }).click(); const old = page.getByRole('dialog').last()
    assert.equal(await old.getByLabel('Root-cause category', { exact: true }).count(), 0)
    await old.getByLabel('Resolution', { exact: true }).fill('Legacy resolution body'); armed = { kind: 'success' }; const observed = new Promise(resolve => { seen = resolve }); await old.getByRole('button', { name: 'Resolve', exact: true }).click(); await observed
    assert.equal(reads.slice(readStart).filter(path => path.endsWith('/issues/root-causes')).length, 0)
    cases.push('AC84: off keeps old form/body, ignores malformed new URL control, and sends no summary read')
    assert.deepEqual(errors, []); assert.deepEqual(unexpectedGets, []); assert.deepEqual(unexpectedWrites, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS_SYNTHETIC_BROWSER_ONLY', cases, reads: reads.length, commands: commands.length, screenshots }))
  } finally { me.flags = saved.flags; me.settings = saved.settings; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
