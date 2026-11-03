const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('../../../web/node_modules/playwright')
const fixture = require('./checkpoint21-real-summary.json').fixture
const output = { browser: [], limits: [] }
;(async () => {
  const response = await fetch(`http://127.0.0.1:5088/api/v1/projects/${fixture.projectId}/tasks`, { headers: { 'X-Dev-User': 'priya@hub.test' } })
  assert.equal(response.status, 200)
  output.api = await response.json()
  const cards = output.api.items
  assert.equal(cards.length, 5)
  assert.equal(cards.reduce((n, card) => n + card.estimatedHours, 0), 12)
  assert.equal(cards.filter(card => card.state?.isOverdue).length, 1)
  assert.ok(cards.every(card => card.state && card.board.projectManager.id === fixture.personIds.priya && card.board.disciplineLead.id === fixture.personIds.marc))
  const browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true })
  try {
    for (const [name, route, lead] of [
      ['project-discipline', `/projects/${fixture.projectNumber}/board?swim=discipline&sort=dueDate`, 'Discipline Lead: Marc Dubois'],
      ['workspace-project', `/boards?projects=${fixture.projectId}&swim=project&sort=dueDate`, 'Project Manager: Priya Nair'],
    ]) for (const width of [1440, 375]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 }, reducedMotion: 'reduce' })
      await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'priya@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
      const page = await context.newPage(), errors = []
      page.on('pageerror', error => errors.push(error.message))
      try {
        await page.goto('http://127.0.0.1:8853' + route, { waitUntil: 'domcontentloaded' })
        const facts = page.locator('[data-board-swimlane-facts]')
        await facts.getByText(lead, { exact: true }).waitFor()
        await facts.getByText('12 h estimated · 0 unestimated', { exact: true }).waitFor()
        await facts.getByText('1 overdue', { exact: true }).waitFor()
        assert.equal(await page.locator('[data-board-review-waiting]').count(), 2)
        assert.deepEqual(await page.locator('[data-board-review-waiting]').allTextContents(), ['0 days waiting', '0 days waiting'])
        assert.deepEqual(await page.locator('[data-board-reviewer]').allTextContents(), ['Marc Dubois', 'Marc Dubois'])
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true)
        assert.deepEqual(errors, [])
        const screenshot = path.join(__dirname, `checkpoint21-real-board-final-${name}-${width}.png`)
        await page.screenshot({ path: screenshot, fullPage: true })
        output.browser.push({ route, width, lead, figures: await facts.innerText(), screenshot, errors })
      } finally { await context.close() }
    }
    output.status = 'PASS'
    output.limits.push('Evaluation worker enabled only for disposable QA21; scheduled jobs and email dispatch remain disabled. Six-day travel is covered by controlled API tests, not this real browser fixture.')
  } catch (error) { output.status = 'FAIL'; output.error = error.stack; process.exitCode = 1 }
  finally {
    await browser.close()
    fs.writeFileSync(path.join(__dirname, 'checkpoint21-real-board-final.json'), JSON.stringify(output, null, 2) + '\n')
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
