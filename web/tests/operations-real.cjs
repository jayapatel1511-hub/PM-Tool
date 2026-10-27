// Packet 044 real-API checks against the local API and PostgreSQL (the mocked-API suite is operations.cjs):
// AC-OPS-08/10/11 the browser reporter reaches the real client-errors endpoint (204, one log line carrying the page's reference,
// no activity row, quoted text removed, cap of 5), AC-OPS-07 Admin > Operations lists, retries and retries all real dead letters,
// AC-OPS-09 a real 500 shows "(ref <the response's traceId>)", AC-OPS-19 the tab title follows real unread counts (3, cleared, 99+),
// AC-OPS-21 density is saved to the real account and a second browser follows it.
// Dead letters are marked in the database (the natural tenth failure is covered by OperationsTests); the render failure is made by
// patching one real response; the 500 comes from a temporary database trigger that this file removes. Synthetic records only.
const assert = require('node:assert/strict')
const { execFileSync, spawnSync } = require('node:child_process')
const fs = require('node:fs')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
const database = process.env.TUESDAY_SYNTHETIC_DB
const apiLog = process.env.TUESDAY_API_LOG
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'this check creates records and adds a temporary trigger, so it requires an explicitly disposable database')
assert.match(database ?? '', /^hub_smoke_/, 'TUESDAY_SYNTHETIC_DB must name the harness-created database')
assert.ok(apiLog && fs.existsSync(apiLog), 'TUESDAY_API_LOG must name the API log')

// CI's PostgreSQL is a service container with a generated name on 55432 (trust auth), so a host psql is used when there is one;
// the Mac has no host psql and reaches its container (TUESDAY_PG_CONTAINER, default pm-tool-db-1) instead.
const [psqlBin, ...psqlArgs] = spawnSync('psql', ['--version']).status === 0
  ? ['psql', '-h', '127.0.0.1', '-p', '55432']
  : ['docker', 'exec', process.env.TUESDAY_PG_CONTAINER ?? 'pm-tool-db-1', 'psql']
// The local PostgreSQL is shared with other runs and can be at max_connections for a moment, so a refused connection is retried.
function psql(statement) {
  for (let attempt = 0; ; attempt++) {
    try { return execFileSync(psqlBin, [...psqlArgs, '-U', 'hub', '-d', database, '-Atc', statement], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim() }
    catch (e) { if (attempt >= 30 || !/too many clients/.test(String(e.stderr))) throw e; Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000) }
  }
}
async function raw(pathname, who, options = {}) {
  return fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json' } })
}
async function api(pathname, who, options = {}) {
  const response = await raw(pathname, who, options)
  if (!response.ok) throw new Error(`${options.method ?? 'GET'} ${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const send = (pathname, who, body, method = 'POST') => api(pathname, who, { method, body: JSON.stringify(body) })
const addDays = (iso, n) => new Date(Date.parse(`${iso}T00:00:00Z`) + n * 86400000).toISOString().slice(0, 10)
const suffix = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`.toUpperCase().slice(-7)
const until = async (what, check, ms = 30000) => {
  const deadline = Date.now() + ms
  while (Date.now() < deadline) { const value = await check(); if (value) return value; await new Promise(r => setTimeout(r, 250)) }
  throw new Error(`timed out waiting for ${what}`)
}
const logText = () => fs.readFileSync(apiLog, 'utf8')

;(async () => {
  const jordan = await api('me', 'jordan')
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  const people = {}
  for (const who of ['alex', 'jill', 'marc', 'priya', 'sam']) people[who] = await api('me', who)
  const today = jordan.settings.today
  const project = async name => {
    const created = await send('projects', 'priya', { projectNumber: `K${suffix()}`, name, clientId: reference.clients[0].id, officeId: reference.offices[0].id,
      disciplines: [{ disciplineId: civil.id, leadUserId: people.marc.id }], members: ['alex', 'jill'].map(who => ({ userId: people[who].id, roles: ['TeamMember'], disciplineId: civil.id })) })
    const draft = await api(`projects/${created.id}`, 'priya')
    await send(`projects/${created.id}/transition`, 'priya', { toStatus: 'Active', rowVersion: draft.rowVersion })
    const detail = await api(`projects/${created.id}`, 'priya')
    return { id: created.id, number: created.projectNumber, discipline: (detail.disciplines ?? detail.projectDisciplines)[0].id }
  }
  const task = (p, name, assignee) => send(`projects/${p.id}/tasks`, 'priya', { name, projectDisciplineId: p.discipline, assigneeId: people[assignee].id, startDate: addDays(today, -1), dueDate: addDays(today, 9) })

  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const results = {}
  const session = async (who, init) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    await context.addInitScript(([email, extra]) => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1'); if (extra) localStorage.setItem(extra[0], extra[1]) }, [`${who}@hub.test`, init ?? null])
    const page = await context.newPage()
    page.reports = []; page.pageErrors = []
    page.on('pageerror', e => page.pageErrors.push(e.message))
    page.on('response', async response => {
      if (response.url().endsWith('/api/v1/client-errors')) page.reports.push({ status: response.status(), body: response.request().postDataJSON() })
    })
    return page
  }
  try {
    const p = await project(`Synthetic 044 project ${suffix()}`)

    // AC-OPS-08, AC-OPS-10: a screen that throws while rendering shows the reference, and exactly one report with that reference reaches the real endpoint.
    const activityBefore = psql('SELECT count(*) FROM hub.activity_log')
    const admin = await session('jordan')
    await admin.route('**/api/v1/admin/operations', async route => { const response = await route.fetch(); const body = await response.json(); await route.fulfill({ response, json: { ...body, jobs: {} } }) })
    await admin.goto(`${base}/admin/operations`, { waitUntil: 'networkidle', timeout: 60000 })
    await admin.getByRole('heading', { name: 'Tuesday hit a snag' }).waitFor({ timeout: 15000 })
    const shown = (await admin.getByText(/^Reference:/).innerText()).replace('Reference: ', '')
    assert.match(shown, /admin\/operations.*[A-Z2-9]{6}$/)
    await until('the render report', () => admin.reports.length >= 1)
    await admin.waitForTimeout(500)
    assert.equal(admin.reports.length, 1, 'one report for the shown reference')
    assert.equal(admin.reports[0].status, 204)
    assert.equal(admin.reports[0].body.Reference, shown)
    const line = await until('the API log line for the reference', () => logText().split('\n').find(l => l.includes(`Client error ${shown} `)))
    assert.match(line, new RegExp(`kind=render route=admin/operations build=\\S* user=${jordan.id} name=TypeError`))
    assert.ok(!(await admin.locator('body').innerText()).match(/at \w+ \(|\.tsx?:\d+/), 'no stack on the page')
    assert.equal(psql('SELECT count(*) FROM hub.activity_log'), activityBefore, 'a client error writes no activity row')
    results['AC-OPS-08/10'] = { reference: shown, status: 204, logLine: line.trim().slice(0, 160), activityRows: 'unchanged' }

    // AC-OPS-11: an uncaught error and an unhandled rejection are each reported once with the quoted text removed; 7 distinct errors send 5.
    const reporter = await session('priya')
    await reporter.goto(`${base}/my-work`, { waitUntil: 'networkidle', timeout: 60000 })
    await reporter.getByRole('main').first().waitFor()
    await reporter.evaluate(() => { setTimeout(() => { throw new Error('Synthetic uncaught "private name"') }, 0); Promise.reject(new Error('Synthetic unhandled "private name" rejection')) })
    await until('two reports', () => reporter.reports.length >= 2)
    await reporter.waitForTimeout(500)
    assert.equal(reporter.reports.length, 2)
    assert.deepEqual(reporter.reports.map(r => r.status), [204, 204])
    assert.deepEqual(reporter.reports.map(r => r.body.Kind).sort(), ['error', 'unhandled rejection'])
    assert.ok(!JSON.stringify(reporter.reports).includes('private name'), 'quoted text removed before sending')
    const capped = await session('marc')
    await capped.goto(`${base}/my-work`, { waitUntil: 'networkidle', timeout: 60000 })
    await capped.getByRole('main').first().waitFor()
    await capped.evaluate(() => { for (let i = 0; i < 7; i++) setTimeout(() => { throw new Error(`Distinct synthetic error ${String.fromCharCode(65 + i)}`) }, i * 20) })
    await until('five reports', () => capped.reports.length >= 5)
    await capped.waitForTimeout(1500)
    assert.equal(capped.reports.length, 5, 'seven distinct errors on one page send five reports')
    assert.ok(capped.reports.every(r => r.status === 204))
    results['AC-OPS-11'] = { uncaughtAndRejection: 'one report each, 204, quotes removed', sevenErrors: capped.reports.length }

    // AC-OPS-07 and AC-OPS-09: real dead letters on Admin > Operations; a real 500 on Retry ends "(ref traceId)"; Retry and Retry all then work.
    await task(p, 'Synthetic 044 dead-letter task A', 'alex'); await task(p, 'Synthetic 044 dead-letter task B', 'jill')
    await until('the outbox to settle', () => psql('SELECT count(*) FROM hub.outbox_event WHERE processed_at IS NULL') === '0')
    const ids = psql(`SELECT id FROM hub.outbox_event WHERE project_id = '${p.id}' ORDER BY created_at DESC LIMIT 2`).split('\n')
    assert.equal(ids.length, 2, 'two events to dead-letter')
    psql(`UPDATE hub.outbox_event SET processed_at = NULL, attempts = 10, dead_lettered_at = now(), last_error = 'SyntheticException' WHERE id IN ('${ids.join("','")}')`)
    const ops = await session('jordan')
    await ops.goto(`${base}/admin/operations`, { waitUntil: 'networkidle', timeout: 60000 })
    const rows = ops.getByRole('listitem').filter({ hasText: 'SyntheticException' })
    await rows.first().waitFor({ timeout: 15000 })
    assert.equal(await rows.count(), 2)
    for (const text of await rows.allInnerTexts()) for (const part of [p.number, '10', 'SyntheticException']) assert.ok(text.includes(part), `row shows ${part}: ${text}`)
    assert.equal((await api('admin/operations', 'jordan')).problems.filter(x => x.kind === 'OutboxDeadLettered').length, 1)
    await ops.getByText(/failed 10 times/).first().waitFor({ timeout: 15000 })

    psql(`CREATE OR REPLACE FUNCTION hub.gap044_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic gap044 failure'; END $$`)
    psql('CREATE TRIGGER gap044_fail BEFORE UPDATE ON hub.outbox_event FOR EACH ROW WHEN (OLD.dead_lettered_at IS NOT NULL) EXECUTE FUNCTION hub.gap044_fail()')
    let failure
    try {
      const failed = ops.waitForResponse(r => r.url().includes('/admin/operations/outbox/retry'))
      await rows.first().getByRole('button', { name: 'Retry', exact: true }).click()
      const response = await failed
      assert.equal(response.status(), 500)
      const traceId = (await response.json()).traceId
      assert.ok(traceId, 'the real 500 carries a traceId')
      const banner = ops.getByRole('alert').filter({ hasText: '(ref ' })
      await banner.first().waitFor({ timeout: 15000 })
      const message = (await banner.first().innerText()).trim()
      assert.ok(message.endsWith(`(ref ${traceId})`), `message ends with the response's trace id: ${message}`)
      assert.ok(!/HTTP 500/.test(message))
      failure = { traceId, message }
    } finally { psql('DROP TRIGGER IF EXISTS gap044_fail ON hub.outbox_event; DROP FUNCTION IF EXISTS hub.gap044_fail()') }
    assert.equal(psql(`SELECT count(*) FROM hub.outbox_event WHERE dead_lettered_at IS NOT NULL AND id IN ('${ids.join("','")}')`), '2', 'the refused retry changed nothing')
    results['AC-OPS-09'] = failure

    await ops.reload({ waitUntil: 'networkidle' })
    await rows.first().waitFor({ timeout: 15000 })
    await rows.first().getByRole('button', { name: 'Retry', exact: true }).click()
    await until('one dead letter left', async () => (await rows.count()) === 1)
    const retried = psql(`SELECT count(*) FROM hub.outbox_event WHERE id IN ('${ids.join("','")}') AND dead_lettered_at IS NULL AND (attempts = 0 OR processed_at IS NOT NULL)`)
    assert.equal(retried, '1', 'one event back with attempts reset (or already processed)')
    await ops.getByRole('button', { name: 'Retry all' }).click()
    await until('no dead letters', async () => (await rows.count()) === 0)
    await until('both events processed by the worker', () => psql(`SELECT count(*) FROM hub.outbox_event WHERE id IN ('${ids.join("','")}') AND processed_at IS NOT NULL AND dead_lettered_at IS NULL`) === '2')
    assert.equal((await api('admin/operations', 'jordan')).deadLetterCount, 0)
    assert.equal((await raw('admin/operations/outbox/retry', 'priya', { method: 'POST', body: '{}' })).status, 403)
    results['AC-OPS-07'] = { listed: 2, retryOne: 'attempts reset, leaves the list', retryAll: 'empties it', worker: 'processed both', projectManager: 403 }

    // AC-OPS-19: the tab title follows the real unread count: 3, cleared by Mark all read, 99+; a signed-out landing page keeps its own title.
    await send('me/notifications/read', 'jill', { all: true })
    for (let i = 0; i < 3; i++) await task(p, `Synthetic 044 title task ${i}`, 'jill')
    const jill = await session('jill')
    await jill.goto(`${base}/my-work`, { waitUntil: 'networkidle', timeout: 60000 })
    assert.equal((await api('me/notifications/unread-count', 'jill')).notifications, 3)
    await jill.waitForFunction(() => document.title === '(3) Tuesday', null, { timeout: 15000 })
    await jill.getByRole('button', { name: /^Notifications|unread/i }).first().click()
    await jill.getByRole('button', { name: 'Mark all read' }).click()
    await jill.waitForFunction(() => document.title === 'Tuesday', null, { timeout: 15000 })
    for (let i = 0; i < 12; i++) await Promise.all(Array.from({ length: 10 }, (_, n) => task(p, `Synthetic 044 bulk ${i}-${n}`, 'jill')))
    assert.equal((await api('me/notifications/unread-count', 'jill')).notifications, 120)
    await jill.reload({ waitUntil: 'networkidle' })
    await jill.waitForFunction(() => document.title === '(99+) Tuesday', null, { timeout: 15000 })
    const landing = await browser.newPage()
    await landing.goto(`${base}/`, { waitUntil: 'networkidle', timeout: 60000 })
    const landingTitle = await landing.title()
    assert.ok(landingTitle && !/^\(/.test(landingTitle), `landing keeps its own title: ${landingTitle}`)
    results['AC-OPS-19'] = { three: '(3) Tuesday', cleared: 'Tuesday', many: '(99+) Tuesday', landing: landingTitle }

    // AC-OPS-21: Comfortable chosen in one browser is saved to the account and followed by a second browser; a stored Compact choice wins; a new person is compact.
    assert.notEqual((await api('me', 'sam')).preferences?.denseRows, false, 'sam starts compact')
    const first = await session('sam')
    await first.goto(`${base}/my-work`, { waitUntil: 'networkidle', timeout: 60000 })
    await first.waitForFunction(() => document.documentElement.dataset.density === 'compact', null, { timeout: 15000 })
    await first.getByRole('button', { name: 'Account' }).click()
    await first.getByRole('menuitemradio', { name: 'Comfortable' }).click()
    await until('the account to store the choice', async () => (await api('me', 'sam')).preferences?.denseRows === false)
    const second = await session('sam')
    await second.goto(`${base}/my-work`, { waitUntil: 'networkidle', timeout: 60000 })
    await second.waitForFunction(() => document.documentElement.dataset.density === 'comfortable', null, { timeout: 15000 })
    const own = await session('sam', ['hub.density', 'compact'])
    await own.goto(`${base}/my-work`, { waitUntil: 'networkidle', timeout: 60000 })
    await own.waitForFunction(() => document.documentElement.dataset.density === 'compact', null, { timeout: 15000 })
    const fresh = await session('alex')
    await fresh.goto(`${base}/my-work`, { waitUntil: 'networkidle', timeout: 60000 })
    await fresh.waitForFunction(() => document.documentElement.dataset.density === 'compact', null, { timeout: 15000 })
    results['AC-OPS-21'] = { account: 'dense_rows false after Comfortable', secondBrowser: 'comfortable', storedCompact: 'kept', neverChose: 'compact' }

    for (const page of [admin, reporter, capped, ops, jill, first, second, own, fresh]) {
      const unexpected = page.pageErrors.filter(m => !/synthetic|jobs\.map is not a function/i.test(m))
      assert.deepEqual(unexpected, [], 'no unexpected page errors')
    }
  } finally {
    psql('DROP TRIGGER IF EXISTS gap044_fail ON hub.outbox_event; DROP FUNCTION IF EXISTS hub.gap044_fail()')
    await browser.close()
  }
  console.log(JSON.stringify({ scope: 'Chromium against the local API and PostgreSQL', results }))
})().catch(e => { console.error(e); process.exitCode = 1 })
