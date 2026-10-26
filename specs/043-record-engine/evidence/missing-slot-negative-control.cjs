// AC-REC-08 negative control: add one unknown slot to the real candidate metadata.
// The API/database remain untouched; only the browser's metadata response is cloned.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('playwright')

const root = path.resolve(__dirname, '../../..')
const reportPath = process.env.PARITY_REPORT ?? path.join(root, 'ci-artifacts/platform-recovery/parity-measured/report.json')
const report = JSON.parse(fs.readFileSync(reportPath, 'utf8'))
const candidate = process.env.TUESDAY_RECORD_CANDIDATE_UI_URL ?? report.candidate
const projectNumber = report.ids.number
const projectUrl = `${candidate}/projects/${projectNumber}/risks`

assert.ok(['localhost', '127.0.0.1'].includes(new URL(candidate).hostname), `candidate must be local: ${candidate}`)
;(async () => {
const browser = await chromium.launch({ headless: true })
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, reducedMotion: 'reduce' })
const requests = []
page.on('request', request => requests.push({ method: request.method(), url: request.url() }))

let modified = false
await page.route('**/api/v1/meta', async route => {
  const response = await route.fetch()
  const body = await response.json()
  const risks = body.lists?.find(list => list.code === 'risks')
  assert.ok(risks, 'real metadata response has no Risks definition')
  assert.ok(!risks.slots?.includes('Cancel'), 'control requires Cancel to be absent before injection')
  risks.slots = [...(risks.slots ?? []), 'Cancel']
  modified = true
  await route.fulfill({ response, body: JSON.stringify(body) })
})

try {
  // Reuse the existing Development identity without POST /me/sign-in; this
  // negative control must not mutate the candidate database or sign-in audit.
  await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'priya@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
  await page.goto(projectUrl, { waitUntil: 'domcontentloaded', timeout: 60000 })
  await page.locator('main').first().waitFor({ timeout: 30000 })
  await page.waitForTimeout(500)
  const bodyText = await page.locator('body').innerText()
  const missingSlot = /Missing record slots[^\n]*Cancel/i.test(bodyText)
  const tableCount = await page.getByRole('table', { name: 'Risks' }).count()
  const columnButtonCount = await page.locator('main button').filter({ hasText: /^(Key|Title|Severity|Status|Owner|Discipline|Review date|Mitigation|Realised)$/ }).count()
  const writes = requests.filter(request => request.method !== 'GET')
  assert.equal(modified, true, 'metadata response was not intercepted and modified')
  assert.equal(missingSlot, true, `candidate did not report the injected Cancel slot; body=${bodyText.slice(0, 1200)}`)
  assert.equal(tableCount, 0, 'Risks table rendered despite missing record slot')
  assert.equal(columnButtonCount, 0, 'Risks columns rendered despite missing record slot')
  assert.deepEqual(writes, [], `negative control issued API writes: ${JSON.stringify(writes)}`)
  console.log(JSON.stringify({ result: 'PASS', candidate, projectNumber, injectedSlot: 'Cancel', missingSlotError: true, risksTableCount: tableCount, riskColumnButtonCount: columnButtonCount, apiWrites: writes.length }))
} finally {
  await page.close()
  await browser.close()
}
})().catch(error => { console.error(error); process.exitCode = 1 })
