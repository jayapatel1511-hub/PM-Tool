// Packet 052 S01: the visual system is dark behind one root switch.
// Browser plugin unavailable in this environment; this is the repo's regular Playwright path.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')

;(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' })
  const errors = []
  const requests = [], writes = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  page.on('request', request => { if (request.resourceType() === 'font') requests.push(request.url()) })
  let enabled = false
  await page.addInitScript(() => {
    sessionStorage.setItem('hub.devUser', 'sam@hub.test')
    sessionStorage.setItem('hub.signedIn', '1')
  })
  // Signed's normal telemetry call is a POST; keep this visual smoke test
  // read-only against the coordinated preview.
  await page.route('**/api/**', async route => {
    const request = route.request()
    const pathname = new URL(request.url()).pathname.replace('/api/v1/', '')
    if (pathname === 'config') return route.fulfill({ json: clientConfig() })
    if (pathname === 'me') return route.fulfill({ json: meView({
      id: '00000000-0000-0000-0000-000000000002', displayName: 'Alex Chen', email: 'alex@hub.test',
      capabilities: { admin: true, createTask: true, workload: true }, flags: { visual_system: enabled },
    }) })
    if (pathname === 'admin/settings') return route.fulfill({ json: [{ key: 'flag.visual_system', kind: 'Bool', group: 'flags', default: false, value: enabled, packet: '052' }] })
    if (pathname === 'admin/settings/flag.visual_system' && request.method() === 'PUT') {
      enabled = request.postDataJSON().value
      writes.push(enabled)
      return route.fulfill({ status: 204, body: '' })
    }
    if (pathname === 'reference' || pathname === 'workspaces' || pathname === 'views') return route.fulfill({ json: pathname === 'reference' ? referenceData() : [] })
    if (pathname === 'me/sign-in') return route.fulfill({ status: 204, body: '' })
    if (pathname === 'me/notifications/pulse' || pathname === 'me/notifications/unread-count') return route.fulfill({ json: {} })
    if (request.method() !== 'GET') return route.fulfill({ status: 204, body: '' })
    return route.fulfill({ json: {} })
  })

  const origin = new URL(base).origin
  await page.goto(`${base}/admin/settings`)
  await page.getByRole('heading', { name: 'Administration', exact: true }).waitFor()
  assert.equal(await page.locator('html').getAttribute('data-visual'), null, 'flag-off keeps the root unchanged')
  const offFontRequests = requests.length
  assert.equal(offFontRequests, 0, 'flag-off does not request IBM fonts')
  await page.getByRole('button', { name: 'Account', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Sign out', exact: true }).waitFor()

  const toggle = page.locator('[id="set-flag.visual_system"]')
  await page.keyboard.press('Escape')
  await page.evaluate(() => { window.__visualDocumentProbe = 'same-document' })
  requests.length = 0
  await toggle.click()
  await page.locator('html[data-visual="v2"]').waitFor()
  assert.equal(await page.locator('html').getAttribute('data-visual'), 'v2', 'flag-on sets the v2 root attribute')
  assert.match(await page.locator('body').evaluate(el => getComputedStyle(el).fontFamily), /IBM Plex Sans/)
  assert.match(await page.locator('body').evaluate(el => getComputedStyle(el).getPropertyValue('--ink-3')), /#646d81|100, 109, 129/i)
  assert.match(await page.locator('body').evaluate(el => getComputedStyle(el).getPropertyValue('--page')), /#f4f5f8|244, 245, 248/i)
  const families = await page.evaluate(() => ['font-sans', 'font-mono', 'key'].map(className => {
    const el = document.createElement('code'); el.className = className; el.textContent = '250101-T0002'
    document.getElementById('content').append(el)
    const family = getComputedStyle(el).fontFamily; el.remove(); return family
  }))
  assert.match(families[0], /IBM Plex Sans/)
  assert.match(families[1], /IBM Plex Mono/)
  assert.match(families[2], /IBM Plex Mono/)
  await page.evaluate(async () => { await document.fonts.ready; await document.fonts.load('14px "IBM Plex Sans"'); await document.fonts.load('14px "IBM Plex Mono"') })
  assert.ok(await page.evaluate(() => document.fonts.check('14px "IBM Plex Sans"')), 'IBM Plex Sans is loaded')
  assert.ok(await page.evaluate(() => document.fonts.check('14px "IBM Plex Mono"')), 'IBM Plex Mono is loaded')
  assert.ok(requests.length > 0, 'flag-on requests IBM fonts')
  for (const url of requests) assert.equal(new URL(url).origin, origin, `font request stays same-origin: ${url}`)

  await page.emulateMedia({ contrast: 'more' })
  const contrast = await page.locator('html').evaluate(el => {
    const style = getComputedStyle(el)
    return [style.getPropertyValue('--border').trim(), style.getPropertyValue('--muted-foreground').trim()]
  })
  assert.deepEqual(contrast, ['#7c8491', '#161a23'])
  await page.emulateMedia({ contrast: 'no-preference' })
  await toggle.click()
  await page.locator('html:not([data-visual])').waitFor()
  assert.equal(await page.evaluate(() => window.__visualDocumentProbe), 'same-document')
  assert.deepEqual(writes, [true, false])
  assert.equal(await page.locator('html').getAttribute('data-visual'), null, 'toggle-off removes the root attribute')
  assert.doesNotMatch(await page.locator('body').evaluate(el => getComputedStyle(el).fontFamily), /IBM Plex/)
  assert.equal(await page.locator('body').evaluate(el => getComputedStyle(el).getPropertyValue('--page')), '', 'flag-off restores legacy token surface')
  assert.equal(errors.length, 0, errors.map(error => error.stack ?? error.message).join('\n'))
  console.log(`PASS: same-document Admin off/on/off, high contrast, font utilities and legacy restoration, ${requests.length} same-origin font requests, no browser errors.`)
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
