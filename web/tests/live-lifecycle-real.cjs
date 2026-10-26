// AC-LIVE-05, AC-LIVE-16 and AC-LIVE-18 against the real API. Owns its stack on a fresh synthetic database: the API, a
// pass-through proxy that can stall the event stream (a connection that stays open but carries no bytes), and a preview.
// TUESDAY_LIVE_BEFORE_DIST (optional) is a build of the same SPA without this packet's live client (drop `useLiveEvents()`
// from Shell.tsx and restore useUnread's plain `me/notifications/pulse` query), built with `vite build --outDir <dir>`; the
// candidate's flag-off request log must then equal its log. Takes about five minutes: the silence and outage timings are real.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const http = require('node:http')
const path = require('node:path')
const { spawn } = require('node:child_process')
const { once } = require('node:events')
const { chromium } = require('playwright')

const root = path.resolve(__dirname, '../..')
const [apiPort, proxyPort, uiPort] = (process.env.TUESDAY_LIFECYCLE_PORTS ?? '18562,18563,19562').split(',').map(Number)
const apiBase = `http://127.0.0.1:${apiPort}`, ui = `http://127.0.0.1:${uiPort}`
const database = `hub_live_${process.pid}_${Date.now()}`
const output = path.join(root, 'ci-artifacts/live-lifecycle')
fs.mkdirSync(output, { recursive: true })
const env = {
  ASPNETCORE_ENVIRONMENT: 'Development', Auth__Mode: 'Development', ConnectionStrings__Hub: `Host=localhost;Port=55432;Database=${database};Username=hub`,
  Seed__DevUsers: 'true', Seed__ReviewDemo: 'false', Jobs__Enabled: 'false', Evaluation__Worker: 'true', RateLimit__PerMinute: '100000',
}
const children = new Set()
function start(command, args, label, extra = {}, cwd = root) {
  const log = fs.createWriteStream(path.join(output, `${label}.log`), { flags: 'a' })
  const child = spawn(command, args, { cwd, env: { ...process.env, ...env, ...extra }, detached: true, stdio: ['ignore', 'pipe', 'pipe'] })
  child.stdout.pipe(log, { end: false }); child.stderr.pipe(log, { end: false })
  children.add(child); child.once('exit', () => { children.delete(child); log.end() })
  return child
}
async function ready(url, child) {
  for (const deadline = Date.now() + 120000; Date.now() < deadline;) {
    if (child.exitCode !== null) throw new Error(`${url}: service exited (${child.exitCode}); see ${output}`)
    try { if ((await fetch(url, { signal: AbortSignal.timeout(2000) })).ok) return } catch {}
    await sleep(200)
  }
  throw new Error(`${url} did not become ready`)
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms))
const startApi = () => start('dotnet', ['bin/Release/net10.0/Hub.Api.dll', '--urls', apiBase], 'api', {}, path.join(root, 'src/Hub.Api'))
const startPreview = dist => start(process.execPath, ['web/node_modules/vite/bin/vite.js', 'preview', 'web', '--config', 'web/vite.config.ts', '--host', '127.0.0.1',
  '--port', String(uiPort), '--strictPort', ...(dist ? ['--outDir', dist] : [])], 'preview', { HUB_API_ORIGIN: `http://127.0.0.1:${proxyPort}` })

// Pass-through proxy. With `stall` set, the next event stream gets its opening comment and then nothing.
const proxy = { stall: false, stalled: [], open: 0 }
const proxyServer = http.createServer((req, res) => {
  const events = req.url.startsWith('/api/v1/events'), stalled = events && proxy.stall
  if (events) { proxy.open++; res.once('close', () => proxy.open--) }
  const upstream = http.request(`${apiBase}${req.url}`, { method: req.method, headers: req.headers }, up => {
    res.writeHead(up.statusCode, up.headers)
    if (!stalled) { up.pipe(res); return }
    const entry = { openedAt: Date.now() }; proxy.stalled.push(entry)
    let first = true
    up.on('data', chunk => { if (first) res.write(chunk); first = false })
    res.once('close', () => { entry.closedAt = Date.now(); proxy.stall = false; up.destroy() })
  })
  upstream.on('error', () => { if (!res.headersSent) res.writeHead(502).end(); else res.destroy() })
  res.once('close', () => upstream.destroy())
  req.pipe(upstream)
})

const headers = who => ({ 'X-Dev-User': `${who}@hub.test`, 'X-Hub-Source': 'UI', 'Content-Type': 'application/json' })
async function api(pathname, who = 'jordan', options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { ...headers(who), ...(options.headers ?? {}) } })
  if (!response.ok) throw new Error(`${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const flags = value => Promise.all(['flag.live_events', 'flag.live_project_stamp'].map(key => api(`admin/settings/${key}`, 'jordan', { method: 'PUT', body: JSON.stringify({ value }) })))
async function seed() {
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  const marc = await api('me', 'marc')
  const project = await api('projects', 'priya', { method: 'POST', body: JSON.stringify({
    projectNumber: `LL${Date.now().toString(36).toUpperCase().slice(-6)}`, name: 'Synthetic live lifecycle project', clientId: reference.clients[0].id,
    officeId: reference.offices[0].id, disciplines: [{ disciplineId: civil.id, leadUserId: marc.id }], members: [],
  }) })
  const draft = await api(`projects/${project.id}`, 'priya')
  await api(`projects/${project.id}/transition`, 'priya', { method: 'POST', body: JSON.stringify({ toStatus: 'Active', rowVersion: draft.rowVersion }) })
  const detail = await api(`projects/${project.id}`, 'priya')
  const name = 'Synthetic lifecycle task'
  const task = await api(`projects/${project.id}/tasks`, 'priya', { method: 'POST', body: JSON.stringify({ name, projectDisciplineId: (detail.disciplines?.[0] ?? detail.projectDisciplines[0]).id }) })
  return { number: project.projectNumber, id: project.id, task: { ...task, name } }
}
/** Hold one stream straight to the API; `frames` records when each comment or event arrived. */
function stream(who) {
  const controller = new AbortController(); const frames = []
  const done = (async () => {
    const response = await fetch(`${apiBase}/api/v1/events`, { headers: headers(who), signal: controller.signal })
    frames.status = response.status
    if (!response.ok) return
    const decoder = new TextDecoder()
    for await (const chunk of response.body) frames.push({ at: Date.now(), text: decoder.decode(chunk) })
    frames.endedAt = Date.now()
  })().catch(error => { frames.endedAt = Date.now(); frames.error = error.name })
  return { controller, frames, done, opened: (async () => { while (frames.status === undefined && frames.endedAt === undefined) await sleep(20); return frames.status })() }
}

const uuid = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/g
function recorder(page, fixture) {
  const log = []; let phase = 'start'
  page.on('request', request => {
    const url = new URL(request.url())
    if (!url.pathname.startsWith('/api/') && url.pathname !== '/health') return
    const at = Date.now()
    const entry = { at, phase, method: request.method(), path: `${url.pathname}${url.search}`.replace('/api/v1/', '').replace(uuid, ':id').replaceAll(fixture.number, ':number') }
    log.push(entry)
    request.response().then(response => { entry.respondedAt = Date.now(); entry.status = response?.status() }).catch(() => {})
  })
  return { log, phase: name => { phase = name } }
}
const isPulse = entry => entry.path.startsWith('me/notifications/pulse')
const isProject = entry => entry.method === 'GET' && entry.path === 'projects/:number'
const isEvents = entry => entry.path === 'events'
const within = (log, test, from, to) => log.filter(entry => test(entry) && entry.at >= from && entry.at <= to)
function pulseGaps(log, phase) {
  const times = log.filter(entry => isPulse(entry) && entry.phase === phase).map(entry => entry.at)
  return times.slice(1).map((at, i) => at - times[i])
}
async function signIn(page, name) {
  await page.goto(`${ui}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
  const signedIn = page.waitForResponse(response => response.url().endsWith('/api/v1/me/sign-in'), { timeout: 30000 })
  await page.getByRole('button', { name }).click()
  await signedIn // once per tab; settle it before any request log starts
}

/** AC-LIVE-18's scripted session: open My Work, open the project's Tasks tab, change a task. Returns the change time. */
async function session(page, fixture, rec, beforeChange = () => {}) {
  rec.phase('my-work')
  await page.goto(`${ui}/my-work`, { waitUntil: 'domcontentloaded' })
  await page.getByRole('heading', { name: 'My Work', level: 1 }).waitFor({ timeout: 30000 })
  await page.waitForTimeout(9000)
  rec.phase('tasks')
  await beforeChange()
  await page.goto(`${ui}/projects/${fixture.number}/tasks`, { waitUntil: 'domcontentloaded' })
  const select = page.getByRole('combobox', { name: `Priority of ${fixture.task.key}` })
  await select.waitFor({ timeout: 30000 })
  await page.waitForTimeout(9000)
  // Change right after a pulse, so the next pulse (and any project-stamp refresh it brings) lands after the follow-up window.
  await page.waitForResponse(response => response.url().includes('/me/notifications/pulse'))
  rec.phase('change')
  const patched = page.waitForResponse(response => response.request().method() === 'PATCH' && response.url().includes(`/tasks/${fixture.task.id}`))
  await select.selectOption((await select.inputValue()) === 'High' ? 'Low' : 'High')
  assert.equal((await patched).status(), 200, 'task change saved')
  const changedAt = Date.now()
  await page.waitForTimeout(6000)
  rec.phase('after')
  return changedAt
}
function checkSession(rec, changedAt, label) {
  const gaps = [...pulseGaps(rec.log, 'my-work'), ...pulseGaps(rec.log, 'tasks')]
  assert.ok(gaps.length >= 3 && gaps.every(gap => gap >= 3500 && gap <= 4700), `${label}: 4-second pulse, gaps ${gaps}`)
  const followUp = within(rec.log, isProject, changedAt + 1300, changedAt + 2400)
  assert.equal(followUp.length, 1, `${label}: one 1.5-second follow-up read of the project after the change`)
  return { pulseGaps: gaps, followUpMs: followUp[0].at - changedAt }
}
const signature = (log, phases) => log.filter(entry => phases.includes(entry.phase) && !isPulse(entry)).map(entry => `${entry.phase} ${entry.method} ${entry.path}`).sort()

;(async () => {
  let browser
  const evidence = { database, ports: [apiPort, proxyPort, uiPort] }
  const errors = []
  try {
    const init = start('dotnet', ['run', '--project', 'src/Hub.Api', '-c', 'Release', '--no-launch-profile'], 'initialize', { Db__MigrateOnly: 'true', Db__InitializeAuditChains: 'true' })
    const [code] = await once(init, 'exit'); assert.equal(code, 0, `migration initializer failed; see ${output}/initialize.log`)
    let backend = startApi(); await ready(`${apiBase}/health`, backend)
    proxyServer.listen(proxyPort, '127.0.0.1'); await once(proxyServer, 'listening')
    let preview = startPreview(process.env.TUESDAY_LIVE_BEFORE_DIST); await ready(ui, preview)
    assert.equal((await api('config')).authMode, 'Development')
    const fixture = await seed()
    await flags(false)
    browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
    const newContext = () => browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    const watch = page => page.on('pageerror', error => errors.push(error.message.slice(0, 200)))

    // AC-LIVE-18 before: the SPA without this packet's live client, flags off.
    let beforeLog
    if (process.env.TUESDAY_LIVE_BEFORE_DIST) {
      const context = await newContext(); const page = await context.newPage(); watch(page)
      await signIn(page, /Priya Nair/i)
      const rec = recorder(page, fixture)
      evidence.before = checkSession(rec, await session(page, fixture, rec), 'before')
      assert.equal(rec.log.filter(isEvents).length, 0)
      beforeLog = rec.log
      await context.close()
      process.kill(-preview.pid, 'SIGTERM'); await once(preview, 'exit')
      preview = startPreview(); await ready(ui, preview)
    }

    // AC-LIVE-05: the deep link in a new tab opens the Task panel over My Work; "Open full page" goes to /tasks/<id>.
    const context = await newContext(); const signedIn = await context.newPage(); watch(signedIn)
    await signIn(signedIn, /Priya Nair/i)
    const tab = await context.newPage(); watch(tab)
    await tab.goto(`${ui}/my-work?panel=Task:${fixture.task.id}`, { waitUntil: 'domcontentloaded' })
    const dialog = tab.getByRole('dialog')
    // Development sign-in lives in each tab's sessionStorage, so the new tab signs in and the gate returns it to the link.
    await tab.getByRole('button', { name: /Priya Nair/i }).click()
    await tab.waitForURL(url => url.pathname === '/my-work' && url.searchParams.get('panel') === `Task:${fixture.task.id}`, { timeout: 30000 })
    await dialog.getByRole('heading', { name: fixture.task.name }).waitFor({ timeout: 30000 })
    // The modal panel hides the page from the accessibility tree, so look past aria-hidden for the page underneath.
    await tab.locator('main').getByRole('heading', { name: 'My Work', level: 1, includeHidden: true }).waitFor()
    assert.equal(new URL(tab.url()).pathname, '/my-work')
    await dialog.getByRole('button', { name: 'Open full page' }).click()
    await tab.waitForURL(url => url.pathname === `/tasks/${fixture.task.id}`, { timeout: 30000 })
    await tab.getByRole('main').getByRole('heading', { name: fixture.task.name }).first().waitFor({ timeout: 30000 })
    evidence.deepLink = { from: `/my-work?panel=Task:${fixture.task.id}`, fullPage: new URL(tab.url()).pathname }
    await tab.close()

    // AC-LIVE-18 flags off: no stream, the pulse without a project id, the follow-up and the same requests as before.
    const page = signedIn
    let rec = recorder(page, fixture)
    const offChange = await session(page, fixture, rec)
    evidence.flagsOff = checkSession(rec, offChange, 'flags off')
    assert.equal(rec.log.filter(isEvents).length, 0, 'flags off: no request to /api/v1/events')
    assert.ok(rec.log.filter(isPulse).every(entry => entry.path === 'me/notifications/pulse'), 'flags off: pulse unchanged')
    if (beforeLog) {
      const phases = ['my-work', 'tasks', 'change']
      assert.deepEqual(signature(rec.log, phases), signature(beforeLog, phases), 'flags off: same requests as before this packet')
      evidence.sameAsBefore = signature(rec.log, phases).length
    }

    // AC-LIVE-18 flags on, AC-LIVE-16 silence: the Tasks-tab stream is stalled, so the follow-up and pulse stand alone.
    await flags(true)
    const heartbeat = stream('marc')
    rec = recorder(page, fixture)
    const onChange = await session(page, fixture, rec, () => { proxy.stall = true })
    evidence.flagsOn = checkSession(rec, onChange, 'flags on')
    assert.ok(rec.log.some(entry => entry.path === `me/notifications/pulse?projectId=:id`), 'flags on: project pulse')
    assert.ok(rec.log.some(isEvents), 'flags on: stream opened')
    for (let i = 0; i < 700 && !proxy.stalled[0]?.closedAt; i++) await sleep(100)
    const silent = proxy.stalled[0]
    assert.ok(silent?.closedAt, 'the client closed the silent stream')
    assert.ok(silent.closedAt - silent.openedAt >= 60000 && silent.closedAt - silent.openedAt <= 62500, `silent stream closed after ${silent.closedAt - silent.openedAt} ms`)
    await sleep(28000) // past the reconnected stream's first heartbeat, so a refresh per message would show
    const reopen = rec.log.find(entry => isEvents(entry) && entry.at > silent.closedAt)
    assert.ok(reopen?.respondedAt, 'reconnected after silence')
    assert.ok(reopen.at - silent.closedAt >= 900 && reopen.at - silent.closedAt <= 1800, `first backoff step: ${reopen.at - silent.closedAt} ms`)
    const afterSilence = within(rec.log, isProject, reopen.respondedAt, reopen.respondedAt + 27000)
    assert.equal(afterSilence.length, 2, `one project refresh (two passes) after the reopen, got ${afterSilence.map(x => x.at - reopen.respondedAt)}`)
    heartbeat.controller.abort()
    // A heartbeat follows 25 s after the last thing written, whether that was the opening comment, a beat or an event.
    const beatGaps = heartbeat.frames.flatMap((frame, i) => i && frame.text.startsWith(': heartbeat') ? [frame.at - heartbeat.frames[i - 1].at] : [])
    assert.ok(beatGaps.length >= 2 && beatGaps.every(gap => gap >= 24500 && gap <= 26500), `heartbeat every 25 s: ${beatGaps}`)
    evidence.silence = { closedAfterMs: silent.closedAt - silent.openedAt, reconnectMs: reopen.at - silent.closedAt, projectReadsAfterReopen: afterSilence.length, heartbeatGapsMs: beatGaps }

    // AC-LIVE-16 hidden tab: the stream closes while hidden and reopens, with one project refresh, when visible.
    const setHidden = hidden => page.evaluate(value => {
      for (const [name, read] of [['hidden', () => value], ['visibilityState', () => value ? 'hidden' : 'visible']]) Object.defineProperty(document, name, { configurable: true, get: read })
      document.dispatchEvent(new Event('visibilitychange'))
    }, hidden)
    assert.equal(proxy.open, 1, 'one browser stream before hiding')
    const hiddenAt = Date.now(); await setHidden(true)
    for (let i = 0; i < 30 && proxy.open; i++) await sleep(100)
    const closeMs = Date.now() - hiddenAt
    assert.equal(proxy.open, 0, 'hidden tab closed its stream')
    const hiddenStreams = rec.log.filter(isEvents).length
    await sleep(6000)
    assert.equal(rec.log.filter(isEvents).length, hiddenStreams, 'no stream while hidden')
    const visibleAt = Date.now(); await setHidden(false); await sleep(5000)
    const reopened = rec.log.filter(entry => isEvents(entry) && entry.at >= visibleAt)
    assert.equal(reopened.length, 1, 'one stream when visible again'); assert.ok(reopened[0].at - visibleAt < 1000, 'reopened at once')
    const followUps = within(rec.log, isProject, visibleAt + 1300, visibleAt + 2400)
    assert.equal(followUps.length, 1, 'one project refresh on becoming visible')
    evidence.hidden = { closeMs, hiddenMs: visibleAt - hiddenAt, reopenMs: reopened[0].at - visibleAt }
    assert.deepEqual(errors, [], `browser errors: ${errors.join(' | ')}`)

    // AC-LIVE-16 server stopping and outage: streams end, the host exits promptly, the client backs off 1, 2, 4, 8, 16, 30 s.
    const held = stream('marc'); assert.equal(await held.opened, 200)
    await sleep(500)
    const stopAt = Date.now(); process.kill(-backend.pid, 'SIGTERM'); const [exitCode, exitSignal] = await once(backend, 'exit')
    const exitMs = Date.now() - stopAt
    await held.done
    assert.ok(exitMs < 10000, `host stopped in ${exitMs} ms`); assert.ok(held.frames.endedAt - stopAt < exitMs + 100, 'held stream ended with the host')
    const attempts = () => rec.log.filter(entry => isEvents(entry) && entry.at > stopAt)
    for (let i = 0; i < 400 && attempts().length < 5; i++) await sleep(100)
    backend = startApi(); await ready(`${apiBase}/health`, backend)
    for (let i = 0; i < 400 && !(attempts().length >= 6 && attempts()[5].respondedAt); i++) await sleep(100)
    const tries = attempts().slice(0, 6)
    const outageGaps = [tries[0].at - stopAt, ...tries.slice(1).map((entry, i) => entry.at - tries[i].at)]
    ;[1000, 2000, 4000, 8000, 16000, 30000].forEach((expected, i) => assert.ok(outageGaps[i] >= expected - 100 && outageGaps[i] <= expected + 1500, `backoff step ${i + 1}: ${outageGaps[i]} ms (${outageGaps})`))
    assert.equal(tries[5].status, 200, 'reconnected after the outage')
    await sleep(6000)
    const afterOutage = within(rec.log, isProject, tries[5].respondedAt + 1300, tries[5].respondedAt + 2400)
    assert.equal(afterOutage.length, 1, 'one project refresh after the outage reopen')
    evidence.outage = { hostExitMs: exitMs, exitCode, exitSignal, heldStreamEndedMs: held.frames.endedAt - stopAt, backoffMs: outageGaps }

    // AC-LIVE-16 cap: a sixth stream for one user gets 429; closing one frees its place.
    const five = Array.from({ length: 5 }, () => stream('jordan'))
    assert.deepEqual(await Promise.all(five.map(s => s.opened)), [200, 200, 200, 200, 200])
    const sixth = stream('jordan'); assert.equal(await sixth.opened, 429, 'sixth stream refused')
    five[0].controller.abort(); await sleep(500)
    const again = stream('jordan'); assert.equal(await again.opened, 200, 'a closed stream frees its place')
    for (const s of [...five, again]) s.controller.abort()
    evidence.cap = { sixth: 429, afterClose: 200 }
    assert.deepEqual(errors, [], `browser errors: ${errors.join(' | ')}`)
    fs.writeFileSync(path.join(output, 'evidence.json'), JSON.stringify(evidence, null, 2))
    console.log(JSON.stringify({ result: 'PASS', ...evidence }))
    await context.close()
  } finally {
    if (browser) await browser.close()
    proxyServer.close()
    for (const child of children) { try { process.kill(-child.pid, 'SIGTERM') } catch {} }
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
