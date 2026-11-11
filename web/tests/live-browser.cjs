// Stable S02/S08/S11 browser contract against a self-started production preview and mocked API.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project } = require('./platform-fixtures.cjs')

;(async () => {
  const preview = await startPreview(5191)
  const { browser, page, mock } = await newMockPage(preview.base, { build: 'mock-build-a' })
  const requests = []; const errors = []
  page.on('request', request => { if (request.url().includes('/api/v1/events')) requests.push(request) })
  page.on('pageerror', error => errors.push(error.message))
  try {
    await page.goto(`${preview.base}/projects/${project.projectNumber}/tasks`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('main').waitFor({ state: 'visible', timeout: 20000 })
    const reload = page.getByRole('button', { name: /reload/i })
    const expectsBuildNotice = process.env.VITE_HUB_BUILD && process.env.VITE_HUB_BUILD !== 'dev'
    if (expectsBuildNotice) {
      assert.equal(await reload.count(), 1, 'first distinct server build shows one reload action')
      page.once('framenavigated', () => mock.setBuild('mock-build-b'))
      await reload.click()
      await page.waitForLoadState('domcontentloaded')
      await page.getByRole('button', { name: /reload/i }).waitFor({ state: 'visible', timeout: 10000 })
      assert.equal(await reload.count(), 1, 'second distinct server build shows one reload action')
      page.once('framenavigated', () => mock.setBuild('mock-build-a'))
      await reload.click()
      await page.waitForLoadState('domcontentloaded')
      await page.waitForTimeout(250)
      assert.equal(await page.getByRole('button', { name: /reload/i }).count(), 0, 'A→B→A does not repeat a build notice')
    } else {
      assert.equal(await reload.count(), 0, 'development builds do not show a server-build reload notice')
    }
    await page.getByRole('main').waitFor({ state: 'visible', timeout: 20000 })
    const event = requests.find(request => new URL(request.url()).search === '')
    assert.ok(event, 'live stream opened without query parameters')
    assert.equal(event.headers()['x-dev-user'], 'alex@hub.test', 'stream carries the Development auth header')
    // The first pulse can arrive a few hundred ms after the page shows: wait for it rather than a fixed delay.
    const projectPulse = () => mock.pulseRequests.some(url => new URL(url).searchParams.get('projectId') === project.id)
    for (const deadline = Date.now() + 5000; !projectPulse() && Date.now() < deadline;) await page.waitForTimeout(50)
    assert.ok(projectPulse(), 'project pulse carries the open project id')
    await page.getByRole('button', { name: /reload/i }).count().then(count => assert.equal(count, 0))
    await page.getByRole('main').focus().catch(() => {})
    await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
    const violations = await page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations.filter(x => ['serious', 'critical'].includes(x.impact)))
    assert.deepEqual(violations, [], 'live preview has no serious/critical accessibility violations')
    assert.deepEqual(errors, [], `browser errors: ${errors.join('; ')}`)
    assert.deepEqual(mock.apiErrors, [])
    assert.deepEqual(mock.unknownGets, [], `unhandled mocked GETs: ${mock.unknownGets.join('; ')}`)
    const capped = await newMockPage(preview.base, { build: 'mock-build-a', events: 429 })
    try {
      await capped.page.goto(`${preview.base}/projects/${project.projectNumber}/tasks`, { waitUntil: 'domcontentloaded' })
      await capped.page.getByRole('main').waitFor({ state: 'visible', timeout: 20000 })
      await capped.page.waitForTimeout(4000) // longer than the first two backoff steps (1 s, 2 s)
      assert.equal(capped.mock.eventRequests.length, 1, 'a refused stream (429) is not retried')
      await capped.page.getByRole('main').waitFor({ state: 'visible' })
    } finally { await capped.browser.close() }
    console.log(JSON.stringify({ result: 'PASS', notices: ['mock-build-a', 'mock-build-b'], liveStream: true, projectPulse: true, refusedStreamNotRetried: true, axe: 0 }))
  } finally { await browser.close(); preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
