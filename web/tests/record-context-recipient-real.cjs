// Real API acceptance: use only a separately created, disposable synthetic QA database.
// Never point this script at a review, pilot or operational database: setup changes flags.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_RECIPIENT_API_BASE
assert.equal(process.env.TUESDAY_REAL_QA, 'isolated-synthetic', 'Explicit isolated synthetic database acknowledgement required')
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'A localhost real API URL is required')
const admin = 'jordan@hub.test', pm = 'priya@hub.test', recipient = 'alex@hub.test', denied = 'rita@hub.test'

async function request(user, path, method = 'GET', body, status = 200) {
  const response = await fetch(`${base}/api/v1${path}`, { method, headers: { 'X-Dev-User': user, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const raw = await response.text()
  assert.equal(response.status, status, `${method} ${path}: ${raw}`)
  return raw ? JSON.parse(raw) : null
}

;(async () => {
  const ref = await request(admin, '/reference')
  const users = await request(admin, '/admin/users')
  const userId = email => users.find(u => u.email === email).id
  const civil = ref.disciplines.find(d => d.name === 'Civil').id
  await request(admin, '/admin/settings/restricted_projects_enabled', 'PUT', { value: true })
  await request(admin, '/admin/settings/flag.screen_patterns', 'PUT', { value: true })
  const project = await request(pm, '/projects', 'POST', {
    projectNumber: `QA${Date.now()}`, name: 'Synthetic recipient browser QA', clientId: ref.clients[0].id, officeId: ref.offices[0].id,
    disciplines: [{ disciplineId: civil, leadUserId: userId('marc@hub.test') }],
    members: [{ userId: userId(recipient), roles: ['TeamMember'], disciplineId: civil }],
  }, 201)
  let detail = await request(pm, `/projects/${project.id}`)
  await request(pm, `/projects/${project.id}`, 'PATCH', { rowVersion: detail.rowVersion, visibility: 'Restricted' })
  detail = await request(pm, `/projects/${project.id}`)
  await request(pm, `/projects/${project.id}/transition`, 'POST', { rowVersion: detail.rowVersion, toStatus: 'Active' })
  detail = await request(pm, `/projects/${project.id}`)
  const task = await request(pm, `/projects/${project.id}/tasks`, 'POST', {
    name: 'Synthetic copied-link recipient task', projectDisciplineId: detail.disciplines[0].id,
    assigneeId: userId(recipient), dueDate: '2026-11-12', requiresReview: false,
  }, 201)
  const apiDetail = await request(recipient, `/tasks/${task.id}`)
  assert.equal(apiDetail.context.owner.id, userId(recipient))
  assert.equal(apiDetail.context.neededBy, '2026-11-12')
  await request(denied, `/tasks/${task.id}`, 'GET', undefined, 404)

  const browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH, headless: true })
  const errors = []
  const makePage = async email => {
    const context = await browser.newContext()
    await context.addInitScript(email => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1') }, email)
    const page = await context.newPage()
    page.setDefaultTimeout(15000)
    page.on('pageerror', error => errors.push(error.message))
    return page
  }
  try {
    const sender = await makePage(pm)
    await sender.goto(`${base}/tasks/${task.id}`, { waitUntil: 'domcontentloaded' })
    const senderFacts = sender.getByRole('region', { name: 'Record context' })
    await senderFacts.getByText('Alex Chen', { exact: true }).first().waitFor()
    const copiedUrl = sender.url()
    const reader = await makePage(recipient)
    await reader.goto(copiedUrl, { waitUntil: 'domcontentloaded' })
    const facts = reader.getByRole('region', { name: 'Record context' })
    await facts.getByText('You', { exact: true }).first().waitFor()
    for (const label of ['Owner', 'Next actor', 'Next action']) await facts.locator('dt').getByText(label, { exact: true }).waitFor()
    await facts.getByText(/Needed by:.*2026-11-12/).waitFor()
    assert.equal(await facts.locator('dd').first().innerText(), 'You', 'Recipient resolves the stored owner in their own session')
    assert.equal(new URL(reader.url()).pathname, `/tasks/${task.id}`, 'The copied URL identifies the exact task')
    await request(admin, '/admin/settings/flag.screen_patterns', 'PUT', { value: false })
    await reader.reload({ waitUntil: 'domcontentloaded' })
    await reader.getByText('Synthetic copied-link recipient task', { exact: true }).first().waitFor()
    await reader.waitForFunction(() => !document.querySelector('section[aria-label="Record context"]'))
    await request(admin, '/admin/settings/flag.screen_patterns', 'PUT', { value: true })
    const stranger = await makePage(denied)
    const deniedResponse = stranger.waitForResponse(r => r.url().endsWith(`/api/v1/tasks/${task.id}`))
    await stranger.goto(copiedUrl, { waitUntil: 'domcontentloaded' })
    assert.equal((await deniedResponse).status(), 404)
    await stranger.getByText('This page or item does not exist, or you cannot view it.', { exact: true }).waitFor()
    assert.equal(await stranger.getByText('Synthetic copied-link recipient task', { exact: true }).count(), 0)
    assert.equal(await stranger.getByRole('region', { name: 'Record context' }).count(), 0)
    assert.deepEqual(errors, [])
    console.log('PASS: real API copied task URL resolves owner/date/four facts for a separate recipient; disabled context absent; denied recipient receives 404 without record facts')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
