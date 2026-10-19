// Production route/chunk contract against a self-started preview and mocked API.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, mockPlatformApi } = require('./platform-fixtures.cjs')
;(async () => {
  const preview = await startPreview(5190)
  const session = await newMockPage(preview.base)
  let { browser, page, mock } = session
  const mocks = [mock]
  const failedChunks = new Set(); const responses = []; const errors = []
  const expected = { '/my-work': /My Work|Today/i, '/projects': /Projects/i, '/admin/users': /Users|Administration/i }
  try {
    page.on('pageerror', error => errors.push(error.message))
    page.on('response', response => { if (response.url().includes('/assets/')) responses.push(response) })
    if (process.env.ROUTE_SPLIT_BREAK_CHUNK === '1') {
      // Warm the shell and discover the route-specific asset before intercepting it.
      await page.goto(`${preview.base}/my-work`, { waitUntil: 'networkidle' })
      const before = new Set(responses.map(response => response.url()))
      await page.goto(`${preview.base}/projects`, { waitUntil: 'networkidle' })
      const target = responses.map(response => response.url()).find(url => !before.has(url) && !url.includes('index'))
      assert.ok(target, 'projects loaded a route-specific chunk to target')
      await page.close()
      page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
      await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'alex@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
      mock = mockPlatformApi(page); mocks.push(mock)
      await page.route(target, async route => { failedChunks.add(target); await route.abort() })
      await page.goto(`${preview.base}/my-work`, { waitUntil: 'networkidle' })
      await page.goto(`${preview.base}/projects`, { waitUntil: 'domcontentloaded' })
      await page.getByRole('button', { name: /reload/i }).waitFor({ state: 'visible', timeout: 10000 })
      assert.ok(failedChunks.size === 1, 'exactly one route chunk was aborted')
    } else {
      for (const path of Object.keys(expected)) {
        await page.goto(`${preview.base}${path}`, { waitUntil: 'domcontentloaded' })
        await page.getByRole('main').waitFor({ state: 'visible', timeout: 20000 })
        await page.getByText(expected[path]).first().waitFor({ timeout: 20000 })
        assert.equal(errors.length, 0, `${path}: ${errors.join('; ')}`)
      }
      assert.ok(responses.some(response => response.url().includes('/assets/')), 'routes loaded hashed production assets')
      assert.ok(responses.every(response => response.ok()), 'all route chunks returned successfully')
    }
    assert.deepEqual(mocks.flatMap(x => x.unknownGets), [], 'all mocked GETs must have an explicit response shape')
    console.log(JSON.stringify({ result: 'PASS', routes: Object.keys(expected), chunks: responses.length, failedChunks: failedChunks.size }))
  } finally { await browser.close(); preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
