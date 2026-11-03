// Read-only visual capture of the tested SPA with labelled synthetic fixture data.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '../../../..')
const { newMockPage, me, project } = require(path.join(root, 'web/tests/platform-fixtures.cjs'))
const search = require(path.join(root, 'web/tests/search-screen.fixture.cjs'))
const { reviewRegisterFixture } = require(path.join(root, 'web/tests/review-register.fixture.cjs'))
const base = 'http://127.0.0.1:8854'
const screenshots = [], writes = [], errors = []
async function capture(page, name) {
  const file = path.join(__dirname, name + '.png')
  assert.ok(!fs.existsSync(file), 'Preserve prior visual evidence')
  await page.screenshot({ path: file, fullPage: false }); screenshots.push(file)
}
function observe(page) {
  page.on('pageerror', e => errors.push(e.message))
  page.on('request', r => {
    const p = new URL(r.url()).pathname
    if (p.startsWith('/api/') && !['GET', 'HEAD', 'OPTIONS'].includes(r.method()) && p !== '/api/v1/me/sign-in') writes.push(r.method() + ' ' + p)
  })
}
;(async () => {
  const ui = await newMockPage(base, { events: false }), actor = search.actorFor()
  try {
    actor.flags.visual_system = true; observe(ui.page)
    await ui.page.route('**/api/**', route => {
      const r = route.request(), u = new URL(r.url()), p = u.pathname.replace('/api/v1/', '')
      if (r.method() !== 'GET') return p === 'me/sign-in' ? route.fallback() : route.fulfill({ status: 405, json: {} })
      if (p === 'me') return route.fulfill({ json: actor })
      if (p === 'reference') return route.fulfill({ json: search.reference })
      if (p === 'search') return route.fulfill({ json: search.searchRead(u.searchParams, true) })
      return route.fallback()
    })
    for (const q of ['Civil', 'valve', 'EIT']) {
      await ui.page.goto(base + '/search?q=' + encodeURIComponent(q) + '&type=people')
      await ui.page.locator('#search-tab-people[aria-selected="true"]').waitFor()
      await ui.page.getByRole('region', { name: 'Recent searches', exact: true }).getByRole('button', { name: q, exact: true }).waitFor()
      if (q === 'EIT') await ui.page.locator('[data-search-page-hit]').first().waitFor()
    }
    await capture(ui.page, 'search-desktop1')
    await ui.page.setViewportSize({ width: 375, height: 900 })
    await ui.page.locator('#search-q').waitFor(); await capture(ui.page, 'search-phone1')
    assert.deepEqual(ui.mock.unknownGets, [])
  } finally { await ui.browser.close() }
  const saved = { flags: me.flags, capabilities: me.capabilities, settings: me.settings }
  const review = reviewRegisterFixture(project, me.id), rui = await newMockPage(base, { events: false })
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(k => [k, false]))
    Object.assign(me.flags, { visual_system: true, register_screens: true, people_screens: true })
    me.capabilities = { ...saved.capabilities, workload: false }
    me.settings = { ...saved.settings, today: '2026-11-02', dateFormat: 'yyyy-MM-dd' }
    observe(rui.page)
    await rui.page.route('**/api/**', route => {
      const r = route.request(), u = new URL(r.url()), p = u.pathname.replace('/api/v1/', '')
      if (r.method() !== 'GET') return p === 'me/sign-in' ? route.fallback() : route.fulfill({ status: 405, json: {} })
      if (p === `projects/${project.id}/review-findings`) return route.fulfill({ json: review.page(u.searchParams) })
      if (p === `projects/${project.id}/reviews`) return route.fulfill({ json: review.packages(1) })
      if ([`projects/${project.id}/changes/options`, `projects/${project.id}/reviews/options`].includes(p)) return route.fulfill({ json: review.options })
      return route.fallback()
    })
    await rui.page.goto(base + `/projects/${project.projectNumber}/reviews?q=Retained&status=In+Review&page=1`)
    const widget = rui.page.locator(`[data-review-open-findings="${project.id}"]`)
    await widget.getByRole('table').waitFor(); await widget.locator('h2').scrollIntoViewIfNeeded()
    await capture(rui.page, 'review-findings-desktop1')
    await rui.page.setViewportSize({ width: 375, height: 900 })
    await widget.getByRole('list', { name: 'Open findings', exact: true }).waitFor()
    await widget.locator('h2').scrollIntoViewIfNeeded(); await capture(rui.page, 'review-findings-phone1')
    assert.deepEqual(rui.mock.unknownGets, [])
  } finally { me.flags = saved.flags; me.capabilities = saved.capabilities; me.settings = saved.settings; await rui.browser.close() }
  assert.deepEqual(writes, []); assert.deepEqual(errors, [])
  fs.writeFileSync(path.join(__dirname, 'visual-capture1.json'), JSON.stringify({ status: 'PASS_READ_ONLY_VISUAL_CAPTURE', boundary: 'Synthetic fixture data on the actual isolated SPA; not a live API join or human acceptance.', screenshots, writes, errors }, null, 2) + '\n', { flag: 'wx' })
  console.log(JSON.stringify({ status: 'PASS_READ_ONLY_VISUAL_CAPTURE', screenshots, businessWrites: writes.length }))
})().catch(error => { console.error(error); process.exitCode = 1 })
