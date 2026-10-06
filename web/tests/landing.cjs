// Run after npm run build, against a local preview with a synthetic authentication config.
// Example: LANDING_URL=http://127.0.0.1:4196 node web/tests/landing.cjs
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('playwright')
const url = process.env.LANDING_URL || 'http://127.0.0.1:4196'
assert(['localhost', '127.0.0.1'].includes(new URL(url).hostname), 'Only run this test against an isolated local preview')
const evidence = process.env.LANDING_EVIDENCE || '/private/tmp/tuesday-landing-qa'
fs.mkdirSync(evidence, { recursive: true })

;(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' })
  const page = await browser.newPage()
  const errors = []
  const failed = []
  const consoleFindings = []
  const accessibility = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => {
    if (!['error', 'warning'].includes(message.type())) return
    if (message.location().url.includes('/api/v1/me') && message.text().includes('401')) return
    consoleFindings.push(message.text())
  })
  page.on('response', response => { if (response.status() >= 400 && !response.url().includes('/api/v1/me')) failed.push(`${response.status()} ${response.url()}`) })
  await page.addInitScript(() => {
    window.landingMetrics = { lcp: 0, cls: 0 }
    new PerformanceObserver(list => { for (const entry of list.getEntries()) window.landingMetrics.lcp = entry.startTime }).observe({ type: 'largest-contentful-paint', buffered: true })
    new PerformanceObserver(list => { for (const entry of list.getEntries()) if (!entry.hadRecentInput) window.landingMetrics.cls += entry.value }).observe({ type: 'layout-shift', buffered: true })
  })
  async function audit(label) {
    await page.evaluate(fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8'))
    const violations = await page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } })).violations.map(item => ({ id: item.id, impact: item.impact, nodes: item.nodes.map(node => node.target) })))
    accessibility.push({ label, violations })
    assert.deepEqual(violations, [], `Accessibility findings in ${label}`)
  }
  const geometry = []
  for (const width of [1440, 1024, 768, 390, 375]) {
    await page.setViewportSize({ width, height: width >= 768 ? 1000 : 844 })
    await page.goto(url)
    await page.locator('#hero-title').waitFor()
    await page.evaluate(() => document.fonts.ready)
    assert.match(await page.title(), /Project and resource planning/)
    assert.equal(await page.locator('h1').count(), 1)
    assert(await page.getByRole('link', { name: 'Sign in', exact: true }).first().isVisible())
    const bounds = await page.evaluate(() => ({ width: innerWidth, document: document.documentElement.scrollWidth, heading: document.querySelector('h1').getBoundingClientRect().toJSON(), cta: document.querySelector('.hero .primary').getBoundingClientRect().toJSON() }))
    assert(bounds.document <= width, `Document overflow at ${width}: ${bounds.document}`)
    assert(bounds.heading.left >= 0 && bounds.heading.right <= width, `Hero heading clips at ${width}`)
    assert(bounds.cta.left >= 0 && bounds.cta.right <= width, `Hero CTA clips at ${width}`)
    assert.equal(await page.locator('.thought-sketches svg').count(), 5, 'Keep the original raw-thought drawings')
    {
      for (const table of ['.plan-grid', '.effort-table', ...(width >= 768 ? ['.forecast-grid'] : [])]) {
        const fits = await page.locator(table).evaluate(element => element.parentElement.scrollWidth <= element.parentElement.clientWidth)
        assert(fits, `${table} should show every column at ${width}`)
      }
    }
    await audit(`${width}px`)
    geometry.push({ ...bounds, metrics: await page.evaluate(() => window.landingMetrics) })
    await page.screenshot({ path: path.join(evidence, `hero-${width}.png`) })
    if (width === 1440 || width === 390) {
      for (const section of ['workspace', 'effort-title', 'approach', 'teams', 'data', 'final-title']) {
        await page.locator(`#${section}`).scrollIntoViewIfNeeded()
        await page.screenshot({ path: path.join(evidence, `${section}-${width}.png`) })
      }
      await page.screenshot({ path: path.join(evidence, `full-${width}.png`), fullPage: true })
    }
  }
  await page.setViewportSize({ width: 1440, height: 1000 })
  await page.goto(url)
  await page.getByLabel('Filter the example by discipline').selectOption('civilDesign')
  assert.equal(await page.locator('.forecast-grid tbody tr').count(), 1)
  assert.match(await page.locator('.preview-note').innerText(), /^1 person/)
  await page.getByLabel('Filter the example by discipline').selectOption('all')
  assert.equal(await page.locator('.forecast-grid tbody tr').count(), 3)
  await page.getByRole('link', { name: 'Explore the platform' }).click()
  assert.equal(new URL(page.url()).hash, '#workspace')
  await page.getByRole('tab', { name: 'Week of Oct 5' }).focus()
  await page.keyboard.press('ArrowRight')
  assert.equal(await page.locator('#week-2').getAttribute('aria-selected'), 'true')
  assert.equal(await page.locator('#plan-panel').getAttribute('aria-labelledby'), 'week-2')
  assert.equal(await page.locator('#plan-summary').innerText(), '23.5 h available across the team')
  await page.keyboard.press('Home')
  assert.equal(await page.locator('#plan-summary').innerText(), '20.5 h available across the team')
  for (const week of [0, 1]) {
    await page.locator(`#week-${week + 1}`).click()
    const totals = await page.locator('.plan-grid tbody tr').evaluateAll(rows => rows.map(row => [...row.querySelectorAll('td')].reduce((sum, cell) => sum + parseFloat(cell.textContent), 0)))
    assert.deepEqual(totals, [37.5, 37.5, 37.5], 'Each allocation must reconcile with weekly capacity')
  }
  const effort = await page.locator('.effort-table').evaluate(table => {
    const rows = [...table.querySelectorAll('tbody tr')].map(row => [...row.querySelectorAll('td')].map(cell => parseFloat(cell.textContent)))
    return { sums: rows.reduce((sum, row) => row.map((value, i) => value + sum[i]), [0, 0, 0]), footer: [...table.querySelectorAll('tfoot td')].map(cell => parseFloat(cell.textContent)) }
  })
  assert.deepEqual(effort.sums, effort.footer, 'Effort totals must reconcile with visible rows')
  await page.getByRole('button', { name: 'See how it works' }).click()
  assert(await page.getByRole('dialog').isVisible())
  assert.equal(await page.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Close workspace preview')
  await audit('desktop dialog')
  await page.keyboard.press('Shift+Tab')
  assert.equal(await page.evaluate(() => document.activeElement.textContent.trim()), 'Enter Tuesday')
  await page.keyboard.press('Escape')
  assert.equal(await page.getByRole('dialog').isVisible(), false)
  assert.equal(await page.evaluate(() => document.activeElement.textContent.trim()), 'See how it works')
  await page.getByRole('button', { name: 'See how it works' }).click()
  await page.getByRole('button', { name: 'Close workspace preview' }).click()
  assert.equal(await page.getByRole('dialog').isVisible(), false)
  for (const [name, hash] of [['Teams', '#teams'], ['Data', '#data'], ['See the platform', '#workspace']]) {
    await page.getByRole('link', { name, exact: true }).click()
    assert.equal(new URL(page.url()).hash, hash)
  }
  fs.writeFileSync(path.join(evidence, 'accessibility.json'), JSON.stringify(accessibility, null, 2))
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto(url)
  await page.evaluate(() => document.fonts.ready)
  await page.getByRole('link', { name: 'Sign in', exact: true }).first().click()
  assert.equal(new URL(page.url()).pathname, '/login')
  await page.getByLabel('User ID', { exact: true }).waitFor()
  assert(await page.getByLabel('Password', { exact: true }).isVisible())
  await page.screenshot({ path: path.join(evidence, 'login-390.png') })
  await page.getByRole('link', { name: 'Home', exact: true }).click()
  await page.locator('#hero-title').waitFor()
  await page.waitForLoadState('networkidle')
  await page.keyboard.press('Tab')
  assert.equal(await page.evaluate(() => document.activeElement.textContent.trim()), 'Skip to content')
  await page.keyboard.press('Enter')
  assert.equal(await page.evaluate(() => document.activeElement.id), 'main')
  await page.emulateMedia({ reducedMotion: 'reduce' })
  const duration = await page.locator('.primary').first().evaluate(element => getComputedStyle(element).transitionDuration)
  assert.equal(duration, '0s')
  const metrics = await page.evaluate(() => window.landingMetrics)
  assert.deepEqual(errors, [], 'No runtime errors')
  assert.deepEqual(failed, [], 'No failing asset requests')
  assert.deepEqual(consoleFindings, [], 'No unexpected console errors or warnings')
  fs.writeFileSync(path.join(evidence, 'results.json'), JSON.stringify({ geometry, metrics, accessibility, errors, failed, consoleFindings }, null, 2))
  console.log(JSON.stringify({ status: 'PASS', viewports: geometry.map(item => item.width), metrics, checks: 'navigation, filters, allocation totals, effort totals, keyboard tabs, modal focus, Escape, close, mobile login, skip link, reduced motion, axe AA at all widths and in dialog, assets, console and runtime errors', evidence }, null, 2))
  await browser.close()
})().catch(error => { console.error(error); process.exit(1) })
