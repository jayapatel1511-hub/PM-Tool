// S12 real-API smoke. This mutates only an explicitly opted-in Development database.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const ui = process.env.TUESDAY_LIVE_UI_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_LIVE_API_URL ?? 'http://127.0.0.1:18502'
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'set TUESDAY_SYNTHETIC_API=1 for the disposable real-API smoke')
for (const value of [ui, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(value).hostname), `${value} must be local`)

const headers = who => ({ 'X-Dev-User': `${who}@hub.test`, 'X-Hub-Source': 'UI', 'Content-Type': 'application/json' })
async function api(pathname, who = 'jordan', options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { ...headers(who), ...(options.headers ?? {}) } })
  if (!response.ok) throw new Error(`${pathname}: HTTP ${response.status}`)
  return response.status === 204 ? null : response.json()
}
async function setting(key, value) { return api(`admin/settings/${key}`, 'jordan', { method: 'PUT', body: JSON.stringify({ value }) }) }
async function seed() {
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  assert.ok(civil, 'Civil discipline fixture missing')
  const alex = await api('me', 'alex'); const marc = await api('me', 'marc')
  const suffix = `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`.toUpperCase().slice(-8)
  const project = await api('projects', 'priya', { method: 'POST', body: JSON.stringify({
    projectNumber: `L${suffix}`, name: 'Synthetic live stream project', clientId: reference.clients[0].id, officeId: reference.offices[0].id,
    disciplines: [{ disciplineId: civil.id, leadUserId: marc.id }], members: [{ userId: alex.id, roles: ['TeamMember'], disciplineId: civil.id }],
  }) })
  const draft = await api(`projects/${project.id}`, 'priya')
  await api(`projects/${project.id}/transition`, 'priya', { method: 'POST', body: JSON.stringify({ toStatus: 'Active', rowVersion: draft.rowVersion }) })
  const detail = await api(`projects/${project.id}`, 'priya')
  const discipline = detail.disciplines?.[0]?.id ?? detail.projectDisciplines?.[0]?.id
  assert.ok(discipline, 'synthetic project discipline missing')
  return { number: project.projectNumber, id: project.id, discipline }
}
async function signIn(page, who) {
  await page.goto(`${ui}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
  await page.getByRole('button', { name: new RegExp(who === 'alex' ? 'Alex Chen' : 'Priya Nair', 'i') }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/login'), { timeout: 30000 })
}
async function browserWrite(page, pathname, body, who) {
  return page.evaluate(async ({ pathname, body, who }) => {
    const response = await fetch(`/api/v1/${pathname}`, { method: 'POST', headers: { 'X-Dev-User': `${who}@hub.test`, 'X-Hub-Source': 'UI', 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
    return { status: response.status, body: response.ok && response.status !== 204 ? await response.json() : null }
  }, { pathname, body, who })
}
async function waitForText(page, text, timeout = 2000) { await page.locator('button:visible').filter({ hasText: text }).first().waitFor({ state: 'visible', timeout }) }

;(async () => {
  const config = await (await fetch(`${apiBase}/api/v1/config`)).json()
  assert.equal(config.authMode, 'Development', 'the real smoke requires Development authentication')
  const settings = await api('admin/settings')
  const original = Object.fromEntries(settings.filter(x => ['flag.live_events', 'flag.live_project_stamp'].includes(x.key)).map(x => [x.key, x.value]))
  assert.ok(Object.hasOwn(original, 'flag.live_events') && Object.hasOwn(original, 'flag.live_project_stamp'), 'live flags missing from settings')
  let browser
  try {
    const fixture = await seed()
    await setting('flag.live_events', true); await setting('flag.live_project_stamp', true)
    browser = await chromium.launch({ headless: true })
    const contextA = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    const contextB = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    const writerPage = await contextA.newPage(); const viewerPage = await contextB.newPage(); const errors = []
    for (const page of [writerPage, viewerPage]) { page.on('pageerror', e => errors.push(e.message.slice(0, 180))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 180)) }) }
    const streamRequests = []; viewerPage.on('request', request => { if (request.url().includes('/api/v1/events')) streamRequests.push({ url: request.url(), headers: request.headers() }) })
    await Promise.all([signIn(writerPage, 'priya'), signIn(viewerPage, 'alex')])
    await viewerPage.goto(`${ui}/projects/${fixture.number}/risks`, { waitUntil: 'domcontentloaded', timeout: 60000 }); await viewerPage.locator('main').first().waitFor({ timeout: 30000 }); await viewerPage.waitForTimeout(500)
    assert.ok(streamRequests.length > 0, 'viewer did not open the live event stream')
    assert.ok(streamRequests.every(x => new URL(x.url).search === ''), 'event stream URL must not contain a token or query credential')
    assert.ok(streamRequests.every(x => x.headers['x-dev-user'] === 'alex@hub.test'), 'event stream must carry the viewer development header')
    const riskTitle = `Synthetic live risk ${Date.now().toString(36)}`
    const risk = await browserWrite(writerPage, `projects/${fixture.id}/risks`, { title: riskTitle, description: 'live smoke', probability: 2, impact: 2, mitigation: 'observe', triggerIndicator: 'change' }, 'priya')
    assert.equal(risk.status, 201, `risk write failed: HTTP ${risk.status}`); await waitForText(viewerPage, riskTitle); await viewerPage.locator('button:visible').filter({ hasText: riskTitle }).first().click(); await waitForText(viewerPage, riskTitle)
    await viewerPage.goto(`${ui}/projects/${fixture.number}/tasks`, { waitUntil: 'domcontentloaded', timeout: 60000 }); await viewerPage.locator('main').first().waitFor({ timeout: 30000 })
    const taskName = `Synthetic live task ${Date.now().toString(36)}`
    const task = await browserWrite(writerPage, `projects/${fixture.id}/tasks`, { name: taskName, projectDisciplineId: fixture.discipline }, 'priya')
    assert.equal(task.status, 201, `task write failed: HTTP ${task.status}`); await waitForText(viewerPage, taskName)
    assert.equal(errors.length, 0, `browser errors: ${errors.join(' | ')}`)
    console.log(JSON.stringify({ result: 'PASS', project: fixture.number, streamRequests: streamRequests.length, risk: riskTitle, task: taskName, updatesWithinMs: 2000 }))
    await contextA.close(); await contextB.close()
  } finally {
    if (browser) await browser.close()
    for (const [key, value] of Object.entries(original)) await setting(key, value).catch(error => console.error(`could not restore ${key}: ${error.message}`))
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
