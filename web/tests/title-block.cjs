// Packet 052 S05: render the real title block against server asOf and org timezone.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
;
(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  try {
    const page = await browser.newPage({ timezoneId: 'Asia/Tokyo' })
    await page.addInitScript(() => { const FixedDate = class extends Date { constructor(...args) { super(...(args.length ? args : ['2040-01-01T00:00:00Z'])) } static now() { return Date.parse('2040-01-01T00:00:00Z') } }; window.Date = FixedDate })
    await page.route('**/api/**', route => route.fulfill({ json: {} }))
    await page.goto(`${base}/`)
    await page.evaluate(async () => {
      const React = await import('/node_modules/.vite/deps/react.js'); const ReactDOM = await import('/node_modules/.vite/deps/react-dom_client.js')
      const R = React.default ?? React; const client = ReactDOM.default ?? ReactDOM
      const { TitleBlock } = await import('/src/components/hub/title-block.tsx'); const { Ctx } = await import('/src/lib/auth-context.ts')
      const me = { flags: { visual_system: false }, settings: { orgTimeZone: 'America/Halifax' }, capabilities: {}, preferences: {} }
      document.documentElement.dataset.visual = 'v2'; const root = document.createElement('div'); root.id = 'title-fixture'; document.body.append(root); const renderer = client.createRoot(root)
      window.__renderTitle = (on) => renderer.render(R.createElement(Ctx.Provider, { value: { me: { ...me, flags: { visual_system: on } }, signOut: () => {} } }, R.createElement(TitleBlock, { asOf: '2026-10-31T17:24:05Z', references: [{ label: 'Trends compare with', value: '2026-10-24T17:24:05Z' }] })))
      window.__renderTitle(false)
    })
    assert.equal(await page.locator('#title-fixture .visual-title-block').count(), 0, 'flag-off omits title block')
    await page.evaluate(() => window.__renderTitle(true))
    await page.locator('#title-fixture .visual-title-block').waitFor()
    assert.equal(await page.locator('#title-fixture dd').first().textContent(), '2026-10-31 14:24', 'uses org timezone, not browser timezone')
    assert.equal(await page.locator('#title-fixture dd').count(), 2)
    assert.equal(await page.locator('#title-fixture dd').nth(1).textContent(), '2026-10-24 14:24')
    assert.equal(await page.locator('#title-fixture dt').first().textContent(), 'Current values')
    assert.equal(await page.locator('#title-fixture dt').nth(1).textContent(), 'Trends compare with')
    console.log('PASS: real TitleBlock flag-off omission, org-timezone asOf formatting and reference row')
  } finally { await browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
