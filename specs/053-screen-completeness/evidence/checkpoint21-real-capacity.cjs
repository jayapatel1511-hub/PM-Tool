// Root-only disposable QA21 join. Normal API commands, never direct database edits.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('../../../web/node_modules/playwright')
const apiBase = 'http://127.0.0.1:5088/api/v1'
const uiBase = 'http://127.0.0.1:8853'
const evidence = { commands: [], reads: [], browser: [], limits: [] }
const save = () => fs.writeFileSync(path.join(__dirname, 'checkpoint21-real-capacity.json'), JSON.stringify(evidence, null, 2) + '\n')
async function api(user, route, method = 'GET', body) {
  const response = await fetch(apiBase + route, { method, headers: { 'X-Dev-User': user, ...(body === undefined ? {} : { 'content-type': 'application/json' }) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text(), value = text ? JSON.parse(text) : null
  const record = { method, route, status: response.status, value }
  evidence[method === 'GET' ? 'reads' : 'commands'].push(record)
  assert.ok(response.ok, `${method} ${route}: ${response.status} ${text}`)
  return value
}
;(async () => {
  if (process.env.CHECKPOINT21_REAL_EXECUTE !== '1') return
  try {
    const admin = 'jordan@hub.test', email = 'qa21-capacity-person@hub.test'
    const me = await api(admin, '/me'), reference = await api(admin, '/reference')
    const date = new Date(`${me.settings.today}T00:00:00Z`)
    date.setUTCDate(date.getUTCDate() - (date.getUTCDay() + 6) % 7)
    const week = date.toISOString().slice(0, 10)
    const civil = reference.disciplines.find(row => row.code === 'CIV').id
    const person = await api(email, '/me')
    const users = await api(admin, '/admin/users')
    const manager = users.find(row => row.email === 'lena@hub.test')
    const current = users.find(row => row.id === person.id)
    assert.ok(manager && current)
    if (process.env.CHECKPOINT21_REAL_REUSE_FIXTURE !== '1') {
      await api(admin, `/admin/users/${person.id}`, 'PATCH', { displayName: 'Synthetic QA21 capacity person', supervisorId: manager.id, homeDisciplineId: civil, weeklyCapacityHours: 40, rowVersion: current.rowVersion })
      for (const [confidence, hoursPerWeek] of [['Confirmed', 45], ['Expected', 5], ['Possible', 20]]) {
        await api(admin, '/planning/entries', 'POST', { personId: person.id, label: `Synthetic QA21 ${confidence} capacity input`, sourceCategory: 'Admin', confidence, visibility: 'Published', hoursPerWeek, startWeek: week, endWeek: week })
      }
    }
    const base = { supervisorId: manager.id, from: week, weeks: '1', includeMyDrafts: 'false' }
    const hoursParams = { ...base, unit: 'hours', period: 'weeks' }
    const fteParams = { ...base, unit: 'fte', period: 'months' }
    const hours = await api(admin, '/reports/team-capacity?' + new URLSearchParams(hoursParams))
    const fte = await api(admin, '/reports/team-capacity?' + new URLSearchParams(fteParams))
    const find = report => report.capacityComparison.rows.find(row => row.disciplineId === civil)
    assert.deepEqual(['capacity', 'demand', 'deficit', 'possible'].map(key => find(hours)[key].value), [40, 50, 10, 20])
    assert.deepEqual(['capacity', 'demand', 'deficit'].map(key => find(fte)[key].value), [1, 1.25, .25])
    assert.equal(fte.capacityComparison.periods[0].denominatorHours, 40)
    assert.ok(!JSON.stringify(hours.capacityComparison).includes(person.id))
    evidence.fixture = { personId: person.id, managerId: manager.id, civilId: civil, week }
    const browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true })
    try {
      for (const [name, params, expected] of [['hours', hoursParams, [40, 50, 10]], ['fte-month', fteParams, [1, 1.25, .25]]]) {
        for (const width of [1440, 375]) {
          const context = await browser.newContext({ viewport: { width, height: 1000 }, reducedMotion: 'reduce' })
          await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'jordan@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
          const page = await context.newPage(), responses = [], errors = []
          page.on('pageerror', error => errors.push(error.message))
          page.on('response', async response => {
            if (new URL(response.url()).pathname === '/api/v1/reports/team-capacity' && response.ok()) responses.push(await response.json())
          })
          try {
            await page.goto(uiBase + '/reports/team-capacity?' + new URLSearchParams(params), { waitUntil: 'domcontentloaded' })
            const key = `discipline:${civil}`
            const row = page.locator(`[data-report-comparison-row="${key}"]`)
            await row.locator(`[data-report-comparison-cell="deficit"][data-value="${expected[2]}"]`).waitFor()
            const source = responses.find(report => find(report)?.deficit.value === expected[2])
            assert.ok(source, 'Rendered values must join the actual matching report response')
            for (const [index, measure] of ['capacity', 'demand', 'deficit'].entries()) {
              assert.equal(find(source)[measure].value, expected[index])
              assert.equal(await row.locator(`[data-report-comparison-cell="${measure}"]`).getAttribute('data-value'), String(expected[index]))
              assert.equal(await page.locator(`[data-report-chart-group="${key}"] [data-report-bar="${measure}"]`).getAttribute('data-value'), String(expected[index]))
            }
            await page.locator(`[data-report-chart-group="${key}"]`).getByText(/short by 10 h/).waitFor()
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true)
            await page.evaluate(async () => { await document.fonts.ready; document.querySelector('main#content').scrollTo({ top: 0, left: 0, behavior: 'auto' }) })
            const first = page.locator('table[data-report-table="team-capacity"] tbody tr').first()
            await first.waitFor()
            const matrix = await first.boundingBox()
            if (width === 1440) assert.ok(matrix && matrix.y <= 405, `Actual matrix first row ${matrix?.y} must be <=405`)
            assert.deepEqual(errors, [])
            const screenshot = path.join(__dirname, `checkpoint21-real-capacity-${name}-${width}.png`)
            await page.screenshot({ path: screenshot, fullPage: true })
            let chartScreenshot
            if (width === 375) {
              await page.locator('[data-report-capacity-chart]').scrollIntoViewIfNeeded()
              chartScreenshot = path.join(__dirname, `checkpoint21-real-capacity-chart-${name}-${width}.png`)
              await page.screenshot({ path: chartScreenshot, fullPage: true })
            }
            evidence.browser.push({ name, width, url: page.url(), matrix, values: expected, actualResponse: source, screenshot, chartScreenshot, errors })
          } finally { await context.close() }
        }
      }
    } finally { await browser.close() }
    evidence.status = 'PASS'
    evidence.limits.push('Disposable local QA21 only; not hosted, pilot, or human acceptance. One displayed ISO week uses a 40-hour standard denominator.')
  } catch (error) { evidence.status = 'FAIL'; evidence.error = error.stack; process.exitCode = 1 }
  finally { save() }
})()
