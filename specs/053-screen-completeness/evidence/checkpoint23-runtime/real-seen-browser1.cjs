// Actual built SPA + owned real QA23 API/database. Setup producers are separate from browser actions.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { execFileSync } = require('node:child_process')
const root = path.resolve(__dirname, '../../../..')
const { chromium } = require(path.join(root, 'web/node_modules/playwright'))
const m = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-seen-setup1.json'), 'utf8'))
assert.equal(m.database, 'hub_qa_checkpoint23')
const checks = [], setupCalls = [], browserBusiness = [], personal = [], errors = []
function sql(query) {
  return execFileSync('docker', ['exec', 'codex-pm-052-053-tests', 'psql', '-U', 'hub', '-d', m.database, '-At', '-v', 'ON_ERROR_STOP=1', '-c', query], { encoding: 'utf8' }).trim()
}
async function api(who, method, route, body) {
  const response = await fetch(m.apiBase + '/api/v1/' + route, { method,
    headers: { 'X-Dev-User': who, ...(body ? { 'Content-Type': 'application/json' } : {}), ...(method === 'POST' ? { 'Idempotency-Key': crypto.randomUUID() } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}) })
  const text = await response.text(), data = text ? JSON.parse(text) : null
  if (method !== 'GET') setupCalls.push({ method, route, status: response.status })
  assert.ok(response.ok, method + ' ' + route + ': ' + response.status + ' ' + text)
  return data
}
function businessFingerprint() {
  const tables = ['task', 'deliverable', 'decision', 'meeting_action', 'risk', 'issue', 'handoff', 'change_notice', 'project']
  return sql('SELECT jsonb_build_object(' + tables.map(table => `'${table}',(SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM hub.${table} r)`).join(',') + ',\'events\',(SELECT count(*) FROM hub.activity_log WHERE item_type IN (\'Task\',\'Deliverable\',\'Decision\',\'Action\',\'Risk\',\'Issue\',\'Handoff\',\'ChangeNotice\',\'Project\')))::text')
}
function observe(page, foreignPosts) {
  page.on('pageerror', error => errors.push(error.message))
  page.on('request', request => {
    const url = new URL(request.url())
    if (!url.pathname.startsWith('/api/') || ['GET', 'HEAD', 'OPTIONS'].includes(request.method())) return
    if (url.pathname === '/api/v1/me/sign-in') return
    if (request.method() === 'POST' && /^\/api\/v1\/me\/work\/(visits|opened|seen-all)$/.test(url.pathname)) {
      personal.push({ path: url.pathname, body: request.postDataJSON() }); foreignPosts?.push(request.url())
    } else browserBusiness.push({ method: request.method(), path: url.pathname })
  })
}
;(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_EXECUTABLE_PATH ?? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' })
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'alex@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    const page = await context.newPage(); page.setDefaultTimeout(15000)
    observe(page)
    const own = m.uiBase + '/my-work?tab=overview&projects=' + m.projectId
    await page.goto(own); await page.locator('[data-work-seen-all]:not([disabled])').waitFor()
    assert.equal(personal.filter(row => row.path.endsWith('/visits')).length, 1)
    assert.equal(sql(`SELECT count(*) FROM hub.work_seen_acknowledgement a JOIN hub.work_assignment_occurrence o USING(token,recipient_id) WHERE o.item_id='${m.oldTask.id}' AND o.recipient_id='${m.people.alex.id}' AND a.provenance='InitialBaseline'`), '1')
    checks.push('Actual first visible visit baselines the existing assignment')
    const newTask = await api('priya@hub.test', 'POST', 'projects/' + m.projectId + '/tasks', { name: 'Synthetic QA23 new exact assignment', projectDisciplineId: m.disciplineId,
      assigneeId: m.people.alex.id, dueDate: m.today, requiresReview: false })
    const reference = await api('jordan@hub.test', 'GET', 'reference'), civil = reference.disciplines.find(row => row.name === 'Civil')
    const second = await api('priya@hub.test', 'POST', 'projects', { projectNumber: m.projectNumber + '-B', name: 'Synthetic QA23 outside visible scope',
      clientId: reference.clients[0].id, officeId: reference.offices[0].id, disciplines: [{ disciplineId: civil.id, leadUserId: m.people.marc.id }],
      members: [{ userId: m.people.alex.id, roles: ['TeamMember'], disciplineId: civil.id }] })
    let secondDetail = await api('priya@hub.test', 'GET', 'projects/' + second.id)
    await api('priya@hub.test', 'POST', 'projects/' + second.id + '/transition', { toStatus: 'Active', rowVersion: secondDetail.rowVersion })
    secondDetail = await api('priya@hub.test', 'GET', 'projects/' + second.id)
    const outside = await api('priya@hub.test', 'POST', 'projects/' + second.id + '/tasks', { name: 'Synthetic QA23 filtered-out assignment',
      projectDisciplineId: secondDetail.disciplines[0].id, assigneeId: m.people.alex.id, dueDate: m.today, requiresReview: false })
    const fingerprint = businessFingerprint()
    await page.reload(); await page.locator('[data-work-seen-all]:not([disabled])').waitFor()
    const row = page.locator('#mw-tasks li').filter({ has: page.getByRole('button', { name: 'Synthetic QA23 new exact assignment', exact: true }) })
    await row.locator('[data-work-new]').waitFor()
    assert.equal(await page.getByText('Synthetic QA23 filtered-out assignment', { exact: true }).count(), 0)
    checks.push('Second visit preserves New; actual selected-project scope hides the other new assignment')
    const opened = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/me/work/opened' && response.ok())
    const displayed = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/tasks/' + newTask.id && response.request().method() === 'GET' && response.ok())
    await row.getByRole('button', { name: 'Synthetic QA23 new exact assignment', exact: true }).click()
    const detail = await (await displayed).json(), openedResponse = await opened
    const command = openedResponse.request().postDataJSON(), receipt = await openedResponse.json()
    const observedTokens = detail.seen.observedTokens.map(row => row.token.toLowerCase()).sort()
    assert.ok(observedTokens.length > 0)
    assert.equal(command.itemType, 'Task'); assert.equal(command.itemId, newTask.id)
    assert.deepEqual(command.observedTokens.map(token => token.toLowerCase()).sort(), observedTokens)
    assert.equal(receipt.requestId, command.requestId)
    const acknowledged = JSON.parse(sql(`SELECT coalesce(jsonb_agg(a.token ORDER BY a.token),'[]'::jsonb)::text FROM hub.work_seen_acknowledgement a JOIN hub.work_assignment_occurrence o USING(token,recipient_id) WHERE o.item_id='${newTask.id}' AND o.recipient_id='${m.people.alex.id}' AND a.provenance='Opened' AND a.request_id='${command.requestId}'`))
    assert.deepEqual(acknowledged, observedTokens, 'Exactly the displayed token set is acknowledged by this request')
    await page.screenshot({ path: path.join(__dirname, 'real-seen-opened-desktop.png'), fullPage: true })
    await page.getByRole('dialog', { name: 'Task', exact: true }).getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(own)
    const marked = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/me/work/seen-all' && response.ok())
    await page.locator('[data-work-seen-all]').click(); await marked
    assert.equal(sql(`SELECT count(*) FROM hub.work_seen_acknowledgement a JOIN hub.work_assignment_occurrence o USING(token,recipient_id) WHERE o.item_id='${outside.id}' AND o.recipient_id='${m.people.alex.id}' AND a.provenance='MarkAll'`), '1')
    checks.push('Actual detail acknowledges its exact token; Mark all covers a filtered-out project assignment')
    assert.equal(fingerprint, businessFingerprint(), 'Personal UI commands leave canonical business rows/versions/events unchanged')
    const foreignContext = await browser.newContext({ viewport: { width: 375, height: 900 }, reducedMotion: 'reduce' })
    await foreignContext.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'priya@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    const foreign = await foreignContext.newPage(), foreignPosts = []
    observe(foreign, foreignPosts)
    const foreignRead = foreign.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/me/work' && new URL(response.url()).searchParams.get('userId') === m.people.alex.id)
    await foreign.goto(m.uiBase + '/my-work?userId=' + m.people.alex.id); const foreignResponse = await foreignRead
    assert.ok(foreignResponse.ok()); const foreignData = await foreignResponse.json()
    assert.equal(JSON.stringify(foreignData).includes('"seen":'), false)
    await foreign.getByRole('heading', { level: 1 }).filter({ hasText: m.people.alex.name }).waitFor()
    assert.equal(await foreign.locator('[data-work-seen-all],[data-work-new]').count(), 0)
    await foreign.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    await foreign.screenshot({ path: path.join(__dirname, 'real-seen-foreign-phone.png'), fullPage: true })
    assert.deepEqual(foreignPosts, [])
    checks.push('Actual foreign-person response/UI at375 omits private Seen state and personal commands')
    assert.deepEqual(browserBusiness, []); assert.deepEqual(errors, [])
    const result = { status: 'PASS', checks, setupCalls, personal, businessWritesFromBrowser: browserBusiness,
      canonicalFingerprintUnchanged: true, newTaskId: newTask.id, outsideTaskId: outside.id,
      boundary: 'Actual synthetic QA23 API/database +build2 SPA; no lost-TCP or human/pilot acceptance claim' }
    fs.writeFileSync(path.join(__dirname, 'real-seen-browser1.json'), JSON.stringify(result, null, 2) + '\n')
    console.log(JSON.stringify({ status: result.status, checks, browserBusinessWrites: 0, personalCommands: personal.length }))
  } finally { await browser.close() }
})().catch(error => { fs.writeFileSync(path.join(__dirname, 'real-seen-browser1-failure.json'), JSON.stringify({ error: error.message, checks, setupCalls, personal, browserBusiness, errors }, null, 2)); console.error(error); process.exitCode = 1 })
