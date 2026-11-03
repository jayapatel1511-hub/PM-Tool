// Read-only visual evidence on the actual SPA; every displayed record is synthetic fixture data.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '../../../..')
const { newMockPage, me, project } = require(path.join(root, 'web/tests/platform-fixtures.cjs'))
const { reviewResponseFixture } = require(path.join(root, 'web/tests/review-response.fixture.cjs'))
const base = 'http://127.0.0.1:8856'
;(async () => {
  const saved = { flags: me.flags, capabilities: me.capabilities, settings: me.settings }
  const f = reviewResponseFixture(project, me.id), data = f.fresh(), screenshots = [], writes = [], errors = [], unknown = []
  // Seed a visual-only historical response; this is not a browser command or persistence claim.
  f.apply(data, data.findings[0].id, { requestId: f.id(15900), rowVersion: 7, action: 'Responded',
    reason: 'The proposed clearance would conflict with the accepted access envelope. The current detail is retained.',
    evidenceUrl: 'https://example.test/synthetic-access-review', ownerId: null, responseOutcome: 'Not incorporated' })
  const ui = await newMockPage(base, { events: false }), { page } = ui
  page.setDefaultTimeout(15000)
  const capture = async name => {
    const file = path.join(__dirname, name + '.png'); assert.ok(!fs.existsSync(file), 'Preserve previous visual evidence')
    await page.evaluate(async () => { await document.fonts.ready; await Promise.all(document.getAnimations().filter(animation => Number.isFinite(animation.effect?.getComputedTiming().endTime)).map(animation => animation.finished.catch(() => {}))); await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))) })
    const modal = page.getByRole('dialog').last(), box = await modal.boundingBox()
    assert.ok(box && box.x >= 0 && box.y >= 0 && box.x + box.width <= page.viewportSize().width + 1 && box.height <= page.viewportSize().height + 1)
    await page.screenshot({ path: file, fullPage: false }); screenshots.push(file)
  }
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false]))
    Object.assign(me.flags, { register_screens: true, visual_system: true, people_screens: true, data_entry_safety: true })
    me.capabilities = { ...saved.capabilities, readOnly: false }; me.settings = { ...saved.settings, today: '2026-11-02' }
    page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', route => {
      const request = route.request(), url = new URL(request.url()), p = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (p === 'me/sign-in') return route.fallback()
        writes.push(request.method() + ' ' + p); return route.fulfill({ status: 405, json: {} })
      }
      if (p === `projects/${project.id}/review-findings`) return route.fulfill({ json: f.register(data, url.searchParams) })
      if (p.startsWith(`projects/${project.id}/review-findings/`)) {
        const finding = data.findings.find(row => row.id === p.split('/').at(-1))
        return route.fulfill({ status: finding ? 200 : 404, json: finding ? f.locator(finding, false) : {} })
      }
      if (p === `projects/${project.id}/reviews`) return route.fulfill({ json: f.packages(1) })
      if ([`projects/${project.id}/changes/options`, `projects/${project.id}/reviews/options`].includes(p)) return route.fulfill({ json: f.options })
      if (p === `projects/${project.id}/reviews/${f.package.id}`) return route.fulfill({ json: data })
      if (['config', 'me', 'reference', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count',
        `projects/${project.projectNumber}`, `projects/${project.id}`].includes(p)) return route.fallback()
      unknown.push(p); return route.fulfill({ status: 501, json: {} })
    })
    const url = finding => `${base}/projects/${project.projectNumber}/reviews?panel=ReviewFinding:${finding.id}&round=${finding.roundId}`
    await page.goto(url(data.findings[0]))
    const inspector = () => page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: `${f.package.key} · ${f.package.title}`, exact: true }) })
    const currentResponse = inspector().locator('article > p.whitespace-pre-wrap').filter({ hasText: data.findings[0].response })
    await currentResponse.waitFor(); assert.equal(await currentResponse.count(), 1)
    await currentResponse.scrollIntoViewIfNeeded()
    const history = inspector().locator('#review-finding-history-' + f.package.id + '-' + data.findings[0].id)
    await history.locator('summary').click(); assert.equal(await history.evaluate(node => node.open), true)
    await history.scrollIntoViewIfNeeded()
    await capture('response-history-desktop4')
    await page.goto(url(data.findings[1]))
    await inspector().getByRole('button', { name: 'Record response', exact: true }).click()
    const form = page.locator('[data-review-response-form]'); await form.waitFor()
    await form.getByLabel('Response outcome', { exact: true }).selectOption('Not incorporated')
    await form.getByLabel('Why it was not incorporated', { exact: true }).fill('The revised opening conflicts with the accepted access envelope. Retain the current detail and refer to the marked review.')
    await form.getByLabel('Evidence link', { exact: true }).fill('https://example.test/synthetic-marked-review')
    await capture('response-form-desktop4')
    await page.setViewportSize({ width: 375, height: 900 }); await capture('response-form-phone4')
    assert.deepEqual(writes, []); assert.deepEqual(errors, []); assert.deepEqual(unknown, []); assert.deepEqual(ui.mock.unknownGets, [])
    fs.writeFileSync(path.join(__dirname, 'visual-capture4.json'), JSON.stringify({ status: 'PASS_READ_ONLY_CAPTURE',
      boundary: 'Synthetic data on actual isolated SPA; no POST, live API join, persistence or human acceptance.', screenshots, writes, errors, unknown }, null, 2) + '\n', { flag: 'wx' })
    console.log(JSON.stringify({ status: 'PASS_READ_ONLY_CAPTURE', screenshots, businessWrites: 0 }))
  } finally { me.flags = saved.flags; me.capabilities = saved.capabilities; me.settings = saved.settings; await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
