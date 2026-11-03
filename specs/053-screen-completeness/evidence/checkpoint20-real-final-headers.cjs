const assert = require('node:assert/strict')
const fs = require('node:fs')
const { chromium } = require('../../../web/node_modules/playwright')
const seed = require('./checkpoint20-real-s25-month.json')
const root = 'specs/053-screen-completeness/evidence/'
;(async () => {
  const evidence = { observedAtUtc: new Date().toISOString(), reports: {}, errors: [] }
  const read = async path => { const r = await fetch('http://127.0.0.1:5088/api/v1/' + path, { headers: { 'X-Dev-User': 'sam@hub.test' } }); assert.equal(r.status, 200); return r }
  for (const [code, scope] of [['workload-employee', `projectId=${seed.project.id}`], ['workload-discipline', ''], ['team-capacity', 'from=2026-11-02&weeks=4'], ['allocations-by-source', 'from=2026-11-02&weeks=4&show=hours-by-source']]) {
    const query = `${scope}${scope ? '&' : ''}unit=fte&period=months`, data = await (await read(`reports/${code}?${query}`)).json()
    const headers = data.unitView.columns.filter(column => /^u\d/.test(column.path)); assert.ok(headers.length)
    for (const column of headers) { assert.ok(column.header.includes('FTE')); assert.ok(!column.header.includes('(h)') && !column.header.includes('Hours (FTE') && !column.header.startsWith('reportUnits.')) }
    const csv = await (await read(`reports/${code}/export?${query}&format=csv`)).text()
    for (const column of headers) assert.ok(csv.includes(column.header), `${code} CSV missing ${column.header}`)
    evidence.reports[code] = { headers, periods: data.unitView.periods, contextHeaders: data.unitView.columns.filter(column => ['capacity', 'noDueDate'].includes(column.path)), csvHeadersMatch: true }
    if (code === 'workload-employee') {
      const m = data.unitView.periods.findIndex(period => period.key === '2026-11'); assert.equal(data.unitView.periods[m].denominatorHours, 160)
      for (const [name, value, capacity] of [['qa20-report-a', 1, 40], ['qa20-report-b', .75, 32]]) {
        const i = data.rows.findIndex(row => row.person === name); assert.ok(i >= 0); assert.equal(data.rows[i].capacity, capacity)
        assert.equal(data.unitView.rows.find(row => row.rowIndex === i).cells.find(cell => cell.path === `u${m}`).value, value)
      }
      assert.equal(data.unitView.rows.find(row => row.rowIndex === -1).cells.find(cell => cell.path === `u${m}`).value, 1.75)
    }
  }
  const catalogue = await (await read('reports')).json(), pickup = catalogue.find(report => report.code === 'pickup-offers')
  assert.equal(pickup.title, 'Task pickup offers'); assert.ok(!pickup.description.startsWith('report.')); evidence.pickup = pickup
  const browser = await chromium.launch({ headless: true, executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' })
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'sam@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    const page = await context.newPage(); page.on('pageerror', error => evidence.errors.push(error.message))
    await page.goto(`http://127.0.0.1:8853/reports/workload-employee?projectId=${seed.project.id}&unit=fte&period=months`)
    const table = page.locator('table[data-report-table="workload-employee"]'); await table.locator('tbody tr').first().waitFor(); await page.evaluate(async () => { await document.fonts.ready })
    const pickupLink = page.getByRole('link').filter({ has: page.getByText('Task pickup offers', { exact: true }) }); await pickupLink.waitFor(); assert.equal(await pickupLink.count(), 1)
    const headerText = await table.locator('thead').innerText(); assert.ok(headerText.includes('Available (FTE;')); assert.ok(!headerText.includes('Available (h) (FTE;'))
    const cells = table.locator('[data-report-unit-cell="u1"]'); assert.equal(await cells.count(), 3); assert.deepEqual((await cells.evaluateAll(nodes => nodes.map(node => Number(node.dataset.unitValue)))), [1, .75, 1.75])
    await cells.first().scrollIntoViewIfNeeded(); evidence.rendered = { headerText, values: [1, .75, 1.75], firstDataY: (await table.locator('tbody tr').first().boundingBox()).y, overflow: await page.evaluate(() => document.documentElement.scrollWidth > innerWidth) }
    assert.ok(evidence.rendered.firstDataY <= 405); assert.equal(evidence.rendered.overflow, false); assert.deepEqual(evidence.errors, [])
    await page.screenshot({ path: root + 'checkpoint20-real-final-november.png', fullPage: true }); await context.close()
  } finally { await browser.close(); fs.writeFileSync(root + 'checkpoint20-real-final-headers.json', JSON.stringify(evidence, null, 2) + '\n') }
  console.log('PASS: refreshed API/UI/export FTE headers across four forms, retained context units, exact1/.75/1.75, translated pickup catalogue.')
})().catch(error => { console.error(error); process.exitCode = 1 })
