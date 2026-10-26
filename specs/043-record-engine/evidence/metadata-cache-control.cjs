// Read-only route/cache proof against the parity runner's owned synthetic API.
const assert = require('node:assert/strict')
const { chromium } = require('../../../web/node_modules/playwright')
const api = 'http://127.0.0.1:18502', ui = 'http://127.0.0.1:19502'
;(async () => {
  assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1')
  assert.equal((await (await fetch(`${api}/api/v1/config`)).json()).authMode, 'Development')
  const projects = await (await fetch(`${api}/api/v1/projects?q=Synthetic%20record%20parity%20project`, { headers: { 'X-Dev-User': 'priya@hub.test' } })).json()
  assert.equal(projects.items.length, 1)
  const browser = await chromium.launch({ headless: true })
  try {
    const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, reducedMotion: 'reduce' })
    await page.goto(`${ui}/login`)
    await page.getByRole('button', { name: /Priya Nair/i }).click()
    await page.waitForURL(u => !u.pathname.startsWith('/login'))
    let metaReads = 0
    const recordWrites = []
    page.on('request', r => {
      const pathname = new URL(r.url()).pathname
      if (pathname === '/api/v1/meta') metaReads++
      if (pathname.startsWith('/api/v1/') && pathname !== '/api/v1/me/sign-in' && !['GET', 'HEAD', 'OPTIONS'].includes(r.method())) recordWrites.push({ method: r.method(), pathname })
    })
    await page.goto(`${ui}/projects/${projects.items[0].projectNumber}/risks`)
    await page.getByRole('button', { name: 'Synthetic parity risk', exact: true }).first().waitFor()
    await page.evaluate(() => { window.__recordCacheControl = 'same-application' })
    await page.getByRole('link', { name: 'My Work', exact: true }).first().click()
    await page.waitForURL(u => u.pathname === '/my-work')
    assert.equal(await page.evaluate(() => window.__recordCacheControl), 'same-application', 'My Work must use SPA navigation before any history restoration')
    await page.goBack()
    await page.getByRole('button', { name: 'Synthetic parity risk', exact: true }).first().waitFor()
    assert.equal(await page.evaluate(() => window.__recordCacheControl), 'same-application', 'route navigation must preserve the application')
    assert.equal(metaReads, 1, 'the remounted record list must serve metadata from cache')
    assert.deepEqual(recordWrites, [], 'navigation must issue no record/API commands; auth sign-in is excluded')
    console.log(JSON.stringify({ result: 'PASS', path: ['Risks', 'My Work', 'Risks'], sameApplication: true, metaReads, recordDataWrites: recordWrites.length, excludedAuthPath: '/api/v1/me/sign-in' }, null, 2))
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
