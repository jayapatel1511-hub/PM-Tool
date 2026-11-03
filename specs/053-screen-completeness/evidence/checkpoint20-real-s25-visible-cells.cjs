const assert = require('node:assert/strict')
const fs = require('node:fs')
const { chromium } = require('../../../web/node_modules/playwright')
const seed = require('./checkpoint20-real-s25-month.json')
const root = 'specs/053-screen-completeness/evidence/'
;(async () => {
  const project = seed.project.id
  const query = `projectId=${project}&unit=fte&period=months`
  const response = await fetch(`http://127.0.0.1:5088/api/v1/reports/workload-employee?${query}`, { headers: { 'X-Dev-User': 'sam@hub.test' } })
  assert.equal(response.status, 200)
  const data = await response.json(), month = data.unitView.periods.findIndex(period => period.key === '2026-11'), path = `u${month}`
  assert.ok(month >= 0); assert.equal(data.unitView.periods[month].weekCount, 4); assert.equal(data.unitView.periods[month].denominatorHours, 160)
  const browser = await chromium.launch({ headless: true, executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' })
  const evidence = { observedAtUtc: new Date().toISOString(), path, period: data.unitView.periods[month], rows: [], errors: [] }
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'sam@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    const page = await context.newPage(); page.on('pageerror', error => evidence.errors.push(error.message))
    await page.goto(`http://127.0.0.1:8853/reports/workload-employee?${query}`)
    await page.locator('table[data-report-table="workload-employee"] tbody tr').first().waitFor()
    await page.evaluate(async () => { await document.fonts.ready })
    for (const [person, expected] of [['qa20-report-a', 1], ['qa20-report-b', .75]]) {
      const rowIndex = data.rows.findIndex(row => row.person === person), row = page.locator('table[data-report-table="workload-employee"] tbody tr').filter({ hasText: person })
      assert.ok(rowIndex >= 0); assert.equal(data.unitView.rows.find(row => row.rowIndex === rowIndex).cells.find(cell => cell.path === path).value, expected)
      const cell = row.locator(`[data-report-unit-cell="${path}"]`); await cell.scrollIntoViewIfNeeded()
      assert.equal(Number(await cell.getAttribute('data-unit-value')), expected)
      const bounds = await cell.boundingBox(); assert.ok(bounds.x >= 240 && bounds.x + bounds.width <= 1441)
      evidence.rows.push({ person, rowIndex, path, value: expected, text: await cell.innerText(), bounds })
    }
    const total = page.locator(`[data-report-unit-total] [data-report-unit-cell="${path}"]`)
    assert.equal(Number(await total.getAttribute('data-unit-value')), 1.75)
    evidence.total = { value: 1.75, text: await total.innerText(), bounds: await total.boundingBox() }
    evidence.url = page.url(); evidence.documentOverflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)
    assert.equal(evidence.documentOverflow, false); assert.deepEqual(evidence.errors, [])
    await page.screenshot({ path: root + 'checkpoint20-real-s25-visible-november.png', fullPage: true })
    await context.close()
  } finally { await browser.close(); fs.writeFileSync(root + 'checkpoint20-real-s25-visible-cells.json', JSON.stringify(evidence, null, 2) + '\n') }
  console.log('PASS: actual November cells visible in the matrix, exact1/.75/1.75, denominator160; inner horizontal scroll retained.')
})().catch(error => { console.error(error); process.exitCode = 1 })
