// AC-VER-02, AC-VER-07, AC-VER-09 and AC-VER-11 against the real API, through the browser. Owns its stack on a fresh
// synthetic database: the release-style migration run that seals the audit chains, the API and a preview of the built SPA.
// Build first (dotnet build Hub.slnx -c Release; npm run build). The database operator's edit in AC-VER-02 and the
// "nothing written" counts in AC-VER-09 use psql inside the PostgreSQL container (TUESDAY_PG_CONTAINER, default pm-tool-db-1).
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { spawn, execFileSync } = require('node:child_process')
const { once } = require('node:events')
const { createServer } = require('node:net')
const { chromium } = require('playwright')

const root = path.resolve(__dirname, '../..')
const [apiPort, uiPort] = (process.env.TUESDAY_VERIFIABLE_PORTS ?? '7236,7237').split(',').map(Number)
const apiBase = `http://127.0.0.1:${apiPort}`, ui = `http://127.0.0.1:${uiPort}`
const database = `hub_verifiable_${process.pid}_${Date.now()}`
const container = process.env.TUESDAY_PG_CONTAINER ?? 'pm-tool-db-1'
const output = path.join(root, 'ci-artifacts/verifiable-records-real')
fs.mkdirSync(output, { recursive: true })
const env = {
  ASPNETCORE_ENVIRONMENT: 'Development', Auth__Mode: 'Development', ConnectionStrings__Hub: `Host=localhost;Port=55432;Database=${database};Username=hub`,
  Seed__DevUsers: 'true', Seed__ReviewDemo: 'false', Jobs__Enabled: 'false', Evaluation__Worker: 'true', RateLimit__PerMinute: '100000',
}
const children = new Set()
function start(command, args, label, extra = {}, cwd = root) {
  const log = fs.createWriteStream(path.join(output, `${label}.log`))
  const child = spawn(command, args, { cwd, env: { ...process.env, ...env, ...extra }, detached: true, stdio: ['ignore', 'pipe', 'pipe'] })
  child.stdout.pipe(log, { end: false }); child.stderr.pipe(log, { end: false })
  children.add(child); child.once('exit', () => { children.delete(child); log.end() })
  return child
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms))
async function ready(url, child) {
  for (const deadline = Date.now() + 120000; Date.now() < deadline;) {
    if (child.exitCode !== null) throw new Error(`${url}: service exited (${child.exitCode}); see ${output}`)
    try { if ((await fetch(url, { signal: AbortSignal.timeout(2000) })).ok) return } catch {}
    await sleep(200)
  }
  throw new Error(`${url} did not become ready`)
}
async function requireFreePort(port) {
  const server = createServer()
  try { server.listen(port, '127.0.0.1'); await once(server, 'listening') }
  catch { throw new Error(`port ${port} is already in use; refusing to attach to another service`) }
  finally { if (server.listening) await new Promise(resolve => server.close(resolve)) }
}
const dll = ['bin/Release/net10.0/Hub.Api.dll'], apiDir = path.join(root, 'src/Hub.Api')

const headers = who => ({ 'X-Dev-User': `${who}@hub.test`, 'X-Hub-Source': 'UI', 'Content-Type': 'application/json' })
async function api(pathname, who, options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { ...headers(who), ...(options.headers ?? {}) } })
  if (!response.ok) throw new Error(`${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const send = (method, pathname, who, body, extra = {}) => api(pathname, who, { method, body: JSON.stringify(body), ...extra })
const rid = () => crypto.randomUUID()
/** The database operator's view: psql in the PostgreSQL container, against this run's synthetic database only. */
const sql = query => execFileSync('docker', ['exec', container, 'psql', '-U', 'hub', '-d', database, '-v', 'ON_ERROR_STOP=1', '-Atc', query], { encoding: 'utf8' }).trim()
const count = query => Number(sql(`select count(*) from ${query}`))
const monday = (offsetWeeks = 0) => { const d = new Date(); d.setUTCDate(d.getUTCDate() - (d.getUTCDay() + 6) % 7 + offsetWeeks * 7); return d.toISOString().slice(0, 10) }
const addDays = (date, n) => { const d = new Date(`${date}T00:00:00Z`); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10) }

let reference, people
async function project(name) {
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  const created = await send('POST', 'projects', 'priya', { projectNumber: `VR${Date.now().toString(36).toUpperCase().slice(-6)}`, name,
    clientId: reference.clients[0].id, officeId: reference.offices[0].id, disciplines: [{ disciplineId: civil.id, leadUserId: people.marc.id }], members: [] })
  const draft = await api(`projects/${created.id}`, 'priya')
  await send('POST', `projects/${created.id}/transition`, 'priya', { toStatus: 'Active', rowVersion: draft.rowVersion })
  const detail = await api(`projects/${created.id}`, 'priya')
  return { id: created.id, number: created.projectNumber, discipline: detail.disciplines[0].id }
}
const task = (p, name, assigneeId) => send('POST', `projects/${p.id}/tasks`, 'priya', { name, projectDisciplineId: p.discipline, assigneeId })
const taskVersion = async (id, who) => (await api(`tasks/${id}`, who)).task.rowVersion

async function axe(page) {
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  return page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations
    .filter(x => ['serious', 'critical'].includes(x.impact)).map(x => x.id))
}
let browser
const pageErrors = []
async function signIn(name) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
  const page = await context.newPage()
  page.on('pageerror', error => pageErrors.push(`${name}: ${error.message}`))
  await page.goto(`${ui}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
  const signedIn = page.waitForResponse(response => response.url().endsWith('/api/v1/me/sign-in'), { timeout: 30000 })
  await page.getByRole('button', { name: new RegExp(name, 'i') }).click()
  await signedIn
  await page.waitForURL(url => !url.pathname.startsWith('/login'), { timeout: 30000 })
  return page
}
const evidence = { database, checks: [] }
function pass(ac, detail) { evidence.checks.push({ ac, ...detail }); console.log(JSON.stringify({ ac, result: 'PASS', ...detail })) }

/** AC-VER-02: an operator edit is caught at its sequence; Activity History says so; one attention item and one email; no repair. */
async function chain(jordan, priya) {
  const p = await project('Synthetic 048 audit chain')
  const created = await task(p, 'Synthetic chain task', null)
  for (const name of ['Synthetic chain task, renamed', 'Synthetic chain task, final']) {
    await send('PATCH', `tasks/${created.id}`, 'priya', { name }, { headers: { 'If-Match': `"${await taskVersion(created.id, 'priya')}"` } })
  }
  const row = jordan.getByRole('row').filter({ hasText: p.id })
  const verify = async () => {
    const done = jordan.waitForResponse(r => r.url().includes(`/admin/audit-chains/${p.id}/verify`))
    await row.getByRole('button', { name: 'Verify' }).click()
    assert.equal((await done).status(), 200)
  }
  await jordan.goto(`${ui}/admin/audit-chains`, { waitUntil: 'domcontentloaded' })
  await row.waitFor({ timeout: 30000 })
  await verify()
  await jordan.getByRole('status').filter({ hasText: 'Chain verified' }).waitFor()
  await priya.goto(`${ui}/projects/${p.number}/activity`, { waitUntil: 'domcontentloaded' })
  const line = priya.getByRole('status').filter({ hasText: /^Log / })
  await line.waitFor({ timeout: 30000 })
  const head = Number(sql(`select head_seq from hub.activity_chain_head where chain_id = '${p.id}'`))
  assert.match(await line.innerText(), new RegExp(`^Log verified · ${head} entries · checked `))

  // The operator edits one stored `changes` value of the task's creation entry, bypassing the application.
  const seq = Number(sql(`select chain_seq from hub.activity_log where chain_id = '${p.id}' and item_type = 'Task' and action = 'Created'`))
  assert.ok(seq > 1 && seq < head, `tampered entry ${seq} sits inside the chain of ${head}`)
  sql(`alter table hub.activity_log disable trigger activity_log_no_update;
    update hub.activity_log set changes = jsonb_set(changes, '{0,new}', '"Tampered by operator"') where chain_id = '${p.id}' and chain_seq = ${seq};
    alter table hub.activity_log enable trigger activity_log_no_update`)
  const tampered = sql(`select changes->0->>'new' from hub.activity_log where chain_id = '${p.id}' and chain_seq = ${seq}`)
  assert.equal(tampered, 'Tampered by operator')
  const admins = sql(`select u.id || ' ' || u.email from hub.app_user u join hub.user_system_role r on r.user_id = u.id where r.role = 'Admin' and u.is_active`).split('\n')
  const alerts = () => count(`hub.notification where event_type = 'AuditChainBroken' and item_id = '${p.id}'`)
  const mails = () => count(`hub.email_message where dedup_key like 'audit-chain:${p.id}:%'`)
  assert.equal(alerts(), 0); assert.equal(mails(), 0)

  await verify()
  await jordan.getByRole('status').filter({ hasText: `Chain has a broken record at sequence ${seq}` }).waitFor()
  await priya.reload({ waitUntil: 'domcontentloaded' })
  await line.waitFor()
  assert.equal(await line.innerText(), `Log check failed at entry ${seq}`)
  assert.equal(alerts(), admins.length, 'one attention item per Admin'); assert.equal(mails(), admins.length, 'one email per Admin')
  for (const admin of admins) {
    const [id, email] = admin.split(' ')
    assert.equal(count(`hub.email_message where user_id = '${id}' and to_address = '${email}' and dedup_key = 'audit-chain:${p.id}:${seq}:${id}'`), 1)
  }
  await jordan.goto(`${ui}/notifications`, { waitUntil: 'domcontentloaded' })
  await jordan.getByText(`Log check failed at entry ${seq}`, { exact: true }).first().waitFor({ timeout: 30000 })
  assert.equal(await jordan.getByText(`Log check failed at entry ${seq}`, { exact: true }).count(), 1, 'one attention item for this break')
  const notifications = await axe(jordan)

  // Verifying again finds the same break and alerts nobody twice; the row stays as the operator left it.
  await jordan.goto(`${ui}/admin/audit-chains`, { waitUntil: 'domcontentloaded' })
  await row.waitFor()
  await verify()
  await jordan.getByRole('status').filter({ hasText: `Chain has a broken record at sequence ${seq}` }).waitFor()
  assert.equal(alerts(), admins.length); assert.equal(mails(), admins.length)
  assert.equal(sql(`select changes->0->>'new' from hub.activity_log where chain_id = '${p.id}' and chain_seq = ${seq}`), 'Tampered by operator', 'nothing is repaired')
  assert.equal(Number(sql(`select first_broken_seq from hub.activity_chain_head where chain_id = '${p.id}'`)), seq)
  const audit = await axe(jordan)
  await priya.reload({ waitUntil: 'domcontentloaded' }); await line.waitFor()
  const activity = await axe(priya)
  assert.deepEqual({ notifications, audit, activity }, { notifications: [], audit: [], activity: [] })
  pass('AC-VER-02', { project: p.number, head, brokenSeq: seq, admins: admins.length, alerts: alerts(), emails: mails() })
}

/** A task commitment for this week, signed by its performer and captured in the week's snapshot, all through the API. */
async function capturedCommitment(p, performer) {
  const work = await task(p, 'Synthetic commitment task', people[performer].id)
  const base = `projects/${p.id}/readiness/Task/${work.id}`
  await send('POST', base, performer, { requestId: rid(), targetRowVersion: await taskVersion(work.id, performer),
    intendedOutput: 'Synthetic sheet set', completionCriteria: 'Sheets checked' })
  for (const code of ['Handoff', 'Predecessor', 'Decision', 'Basis', 'Production Capacity', 'Review Capacity', 'Review Gate', 'Submission Gate']) {
    const readiness = await api(base, 'marc')
    await send('POST', `${base}/checks/${encodeURIComponent(code)}/applicability`, 'marc', { requestId: rid(), assessmentRowVersion: readiness.assessment.rowVersion,
      checkRowVersion: readiness.checks.find(c => c.code === code).rowVersion, applies: false, reason: 'Synthetic: not applicable' })
  }
  assert.equal((await api(base, 'marc')).assessment.state, 'Ready')
  const week = monday()
  const proposed = await send('POST', `projects/${p.id}/weekly-commitments/Task/${work.id}`, performer, { requestId: rid(),
    targetRowVersion: await taskVersion(work.id, performer), weekStart: week, targetDate: week,
    intendedOutput: 'Synthetic sheet set', completionCriteria: 'Sheets checked' })
  await send('POST', `projects/${p.id}/weekly-commitments/${proposed.id}/transition`, performer, { requestId: rid(), rowVersion: proposed.rowVersion,
    toState: 'Committed', reason: 'Synthetic signature' })
  await send('POST', `projects/${p.id}/weekly-commitments/snapshot`, 'priya', { requestId: rid(), weekStart: week, reason: 'Synthetic capture' })
  return proposed.id
}

/** AC-VER-07: Not Met needs a miss category and a reason; renaming the category relabels the history and keeps the count. */
async function missCategory(alex, jordan) {
  const p = await project('Synthetic 048 commitment misses')
  const id = await capturedCommitment(p, 'alex')
  const category = (await api('commitment-miss-categories', 'alex')).find(c => c.name === 'Missing input')
  assert.ok(category, 'default miss categories are seeded')
  const posts = []
  alex.on('request', r => { if (r.method() === 'POST' && r.url().includes(`/weekly-commitments/${id}/transition`)) posts.push(r.url()) })
  await alex.goto(`${ui}/projects/${p.number}/readiness?promise=${id}`, { waitUntil: 'domcontentloaded' })
  await alex.getByRole('button', { name: 'Record Not Met' }).click()
  const form = alex.getByRole('dialog').filter({ has: alex.getByRole('heading', { name: 'Record Not Met' }) })
  const reason = form.getByLabel('Rationale'), select = form.getByLabel('Miss category'), submit = form.getByRole('button', { name: 'Record Not Met' })
  await select.locator('option', { hasText: 'Other' }).waitFor({ state: 'attached' }) // the list loads when the form opens
  assert.deepEqual(await select.locator('option').allTextContents(), ['Choose…', ...(await api('commitment-miss-categories', 'alex')).filter(c => c.isActive).map(c => c.name)])
  await reason.fill('Synthetic: the survey arrived on Friday')
  await submit.click()
  assert.equal(await select.evaluate(el => el.validity.valueMissing), true, 'the category is required')
  await select.selectOption(category.id)
  await reason.fill('')
  await submit.click()
  assert.equal(await reason.evaluate(el => el.validity.valueMissing), true, 'the reason is required')
  await sleep(300)
  assert.equal(posts.length, 0, 'nothing is sent without both')
  await reason.fill('Synthetic: the survey arrived on Friday')
  const saved = alex.waitForResponse(r => r.request().method() === 'POST' && r.url().includes(`/weekly-commitments/${id}/transition`))
  await submit.click()
  assert.equal((await saved).status(), 200)
  assert.equal(sql(`select miss_category_id from hub.output_commitment_event where commitment_id = '${id}' and to_state = 'Not Met'`), category.id)
  const promise = alex.getByRole('dialog').filter({ has: alex.getByRole('heading', { name: 'Review promise' }) })
  await promise.getByText('Committed → Not Met').waitFor({ timeout: 30000 })
  await alex.keyboard.press('Escape')
  await promise.waitFor({ state: 'detached' })

  const misses = alex.getByRole('heading', { name: 'Why commitments were missed' }).locator('xpath=ancestor::section[1]')
  const missRow = name => misses.getByRole('listitem').filter({ hasText: name })
  await alex.getByRole('heading', { name: 'Commitment reliability' }).waitFor()
  await missRow('Missing input').waitFor({ timeout: 10000 }) // the open screen refreshes after the save
  assert.match(await missRow('Missing input').innerText(), /^Missing input\s+1\b/)
  assert.match(await alex.getByText(/^Met \d+ of \d+/).innerText(), /^Met 0 of 1 \(0%\)$/)
  const readiness = await axe(alex)

  // An Admin renames the category (reference data, by ID); history and counts follow the ID.
  const admin = (await api('admin/commitmentMissCategories', 'jordan')).find(c => c.id === category.id)
  await send('PATCH', `admin/commitmentMissCategories/${category.id}`, 'jordan', { name: 'Missing design input' }, { headers: { 'If-Match': `"${admin.rowVersion}"` } })
  await alex.reload({ waitUntil: 'domcontentloaded' })
  await missRow('Missing design input').waitFor({ timeout: 30000 })
  assert.match(await missRow('Missing design input').innerText(), /^Missing design input\s+1\b/)
  assert.equal(await missRow('Missing input').count(), 0)
  assert.equal(sql(`select miss_category_id from hub.output_commitment_event where commitment_id = '${id}' and to_state = 'Not Met'`), category.id, 'history keeps the ID')
  await jordan.goto(`${ui}/reports/commitment-misses`, { waitUntil: 'domcontentloaded' })
  await jordan.getByRole('heading', { name: 'Why commitments were missed', level: 1 }).waitFor({ timeout: 30000 })
  const reportRow = jordan.getByRole('row').filter({ hasText: p.number })
  await reportRow.waitFor({ timeout: 30000 })
  assert.deepEqual((await reportRow.getByRole('cell').allInnerTexts()).map(x => x.trim()), [p.number, 'Civil', 'Missing design input', '1'])
  const report = await axe(jordan)
  assert.deepEqual({ readiness, report }, { readiness: [], report: [] })
  pass('AC-VER-07', { project: p.number, commitment: id, category: category.id, renamed: 'Missing design input', blockedSubmits: 2 })
}

/** AC-VER-09: editing 32 h to 46 h previews "46 of 40 h" Over without writing, logging or notifying; the save matches it. */
async function allocationPreview(priya) {
  const p = await project('Synthetic 048 allocation preview')
  const work = await task(p, 'Synthetic allocation task', people.alex.id)
  const from = monday(1), through = addDays(from, 4)
  const created = await send('POST', `projects/${p.id}/allocations`, 'priya', { requestId: rid(), personId: people.alex.id, purpose: 'Production',
    fromDate: from, throughDate: through, plannedHours: 32, days: [], links: [{ workType: 'Task', workId: work.id, workDate: from, reviewHours: null }], reason: null, openRoleId: null })
  const dated = await api(`projects/${p.id}/allocations/${created.id}/confirmation-preview`, 'sam')
  await send('POST', `projects/${p.id}/allocations/${created.id}/confirm`, 'sam', { requestId: rid(), rowVersion: dated.rowVersion,
    dateVersions: dated.days.map(d => ({ workDate: d.date, rowVersion: d.dateVersion })), overCapacityReason: null })
  const cell = (await api(`workload?from=${from}&to=${through}`, 'priya')).people.find(x => x.id === people.alex.id).cells[0]
  assert.deepEqual([cell.committed, cell.available], [32, 40], 'the person-week starts at 32 of 40 h')

  const state = () => ({
    allocation: sql(`select planned_hours || ' ' || status || ' ' || row_version from hub.resource_allocation where id = '${created.id}'`),
    allocations: count('hub.resource_allocation'), days: count('hub.allocation_day_override'), links: count('hub.allocation_work_link'),
    // Repeat notices collapse into one row (count, updated_at), so the notice figures include both.
    log: count('hub.activity_log'), notifications: sql(`select count(*) || ' ' || coalesce(sum(count), 0) || ' ' || coalesce(max(updated_at)::text, '') from hub.notification`),
    emails: sql(`select count(*) || ' ' || coalesce(max(created_at)::text, '') from hub.email_message`),
    chain: sql(`select head_seq || ' ' || head_hash from hub.activity_chain_head where chain_id = '${p.id}'`),
  })
  const before = state()
  const previews = []
  priya.on('response', r => { if (r.url().includes(`/allocations/${created.id}/edit?preview=true`)) previews.push(r) })
  await priya.goto(`${ui}/projects/${p.number}/allocations?allocation=${created.id}`, { waitUntil: 'domcontentloaded' })
  await priya.getByRole('button', { name: 'Edit allocation' }).click()
  const form = priya.getByRole('dialog').filter({ has: priya.getByRole('heading', { name: 'Edit allocation' }) })
  const line = form.locator('section[aria-live="polite"]').getByRole('listitem')
  await form.getByLabel('Reason').fill('Synthetic: the survey scope grew')
  await line.filter({ hasText: 'After this change: 32 of 40 h, within capacity' }).waitFor({ timeout: 30000 })
  await form.getByLabel('Planned hours').fill('46')
  await line.filter({ hasText: 'After this change: 46 of 40 h, over capacity' }).waitFor({ timeout: 30000 })
  assert.deepEqual(await line.allInnerTexts(), [`After this change: 46 of 40 h, over capacity\nOver capacity on at least one day`])
  assert.ok(previews.length >= 2 && previews.every(r => r.status() === 200), `previews ${previews.map(r => r.status())}`)
  const preview = await previews.at(-1).json() // what the form shows
  assert.deepEqual(state(), before, 'a preview writes, logs and notifies nothing')
  const dialog = await axe(priya)

  const saved = priya.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith(`/allocations/${created.id}/edit`))
  await form.getByRole('button', { name: 'Save changes' }).click()
  assert.equal((await saved).status(), 200)
  await form.waitFor({ state: 'detached' })
  const after = state()
  assert.ok(after.log > before.log && after.notifications !== before.notifications, 'the save itself logs and notifies')
  // The saved allocation, read back as its confirmer sees it, gives the previewed week.
  const read = await api(`projects/${p.id}/allocations/${created.id}/confirmation-preview`, 'sam')
  const week = read.days.filter(d => d.date >= from && d.date <= through)
  const savedWeek = { capacity: week.reduce((n, d) => n + d.availableHours, 0), planned: week.reduce((n, d) => n + d.resultingHours, 0), over: week.some(d => d.overByHours > 0) }
  const [previewed] = preview.weeks
  assert.deepEqual(savedWeek, { capacity: previewed.capacity, planned: previewed.planned, over: previewed.indicators.includes('overCapacity') })
  assert.deepEqual([previewed.planned, previewed.capacity, previewed.band], [46, 40, 'over'])
  assert.deepEqual(dialog, [])
  pass('AC-VER-09', { project: p.number, allocation: created.id, previews: previews.length, savedWeek })
}

/** AC-VER-11: with the A, B, C convention, "Rev 1" and B after C are saved and marked; status, health and attention ignore marks. */
async function revisionCheck(priya) {
  const p = await project('Synthetic 048 revision check')
  const deliverable = await send('POST', `projects/${p.id}/deliverables`, 'priya', { name: 'Synthetic drawing set', projectDisciplineId: p.discipline,
    deliverableTypeId: reference.deliverableTypes[0].id, requiresReview: false })
  await priya.goto(`${ui}/projects/${p.number}/settings`, { waitUntil: 'domcontentloaded' })
  const settings = priya.getByRole('heading', { name: 'Revision check' }).locator('xpath=ancestor::section[1]')
  await settings.getByLabel('Revision convention').selectOption('letters')
  const put = priya.waitForResponse(r => r.request().method() === 'PUT' && r.url().endsWith(`/projects/${p.id}/revision-check`))
  await settings.getByRole('button', { name: 'Save' }).click()
  assert.equal((await put).status(), 200)
  const settingsPage = await axe(priya)

  const panel = `${ui}/projects/${p.number}/deliverables?panel=Deliverable:${deliverable.id}`
  async function issue(revision) {
    await priya.goto(panel, { waitUntil: 'domcontentloaded' })
    await priya.getByRole('button', { name: 'Issue deliverable' }).first().click()
    const dialog = priya.getByRole('dialog').filter({ has: priya.locator('#i-rev') })
    await dialog.locator('#i-rev').fill(revision)
    await dialog.locator('#i-to').fill('Synthetic recipient')
    const done = priya.waitForResponse(r => r.url().endsWith(`/deliverables/${deliverable.id}/issue`))
    await dialog.getByRole('button', { name: 'Issue deliverable' }).click()
    assert.equal((await done).status(), 200, `issue ${revision} is saved`)
    await dialog.waitFor({ state: 'detached' })
  }
  async function reopen() { // Issued → Revision Required → In Progress, so the next revision can be issued
    for (const toStatus of ['Revision Required', 'In Progress']) {
      const row = await api(`deliverables/${deliverable.id}`, 'priya')
      await send('POST', `deliverables/${deliverable.id}/transition`, 'priya', { toStatus, reason: 'Synthetic re-issue', comment: 'Synthetic re-issue', rowVersion: row.deliverable.rowVersion })
    }
  }
  const nonStandard = 'Non-standard revision: expected A, B, C …'
  await issue('Rev 1')
  await reopen(); await issue('C')
  await reopen(); await issue('B')

  await priya.goto(panel, { waitUntil: 'domcontentloaded' })
  await priya.getByRole('tab', { name: /^Issues/ }).click()
  const issueRow = revision => priya.getByRole('dialog').getByRole('row').filter({ has: priya.getByRole('cell', { name: new RegExp(`^${revision}\\b`) }) })
  await issueRow('Rev 1').waitFor({ timeout: 30000 })
  const marksOf = async revision => (await issueRow(revision).getByRole('cell').nth(1).innerText()).split('\n').slice(1).join('; ')
  assert.equal(await marksOf('Rev 1'), nonStandard)
  assert.equal(await marksOf('C'), '')
  assert.equal(await marksOf('B'), 'Out of sequence')
  const detail = await axe(priya)

  await priya.goto(`${ui}/projects/${p.number}/deliverables?revisionWarning=true`, { waitUntil: 'domcontentloaded' })
  await priya.getByRole('button', { name: 'Synthetic drawing set' }).waitFor({ timeout: 30000 })
  const register = await axe(priya)
  await priya.goto(`${ui}/projects/${p.number}/settings`, { waitUntil: 'domcontentloaded' })
  await settings.getByText(`Rev 1: ${nonStandard}`).waitFor({ timeout: 30000 })
  await settings.getByText('B: Out of sequence').waitFor()

  // Marks are computed on read: the same reads with the convention cleared show status, health and attention unchanged.
  await sleep(2000) // the evaluation worker settles the last issue
  const observed = async () => {
    const row = (await api(`deliverables/${deliverable.id}`, 'priya')).deliverable
    const proj = await api(`projects/${p.id}`, 'priya')
    return { status: row.status, state: row.state, health: proj.health, attention: await api(`projects/${p.id}/attention`, 'priya') }
  }
  const marked = await observed()
  assert.equal(marked.status, 'Issued')
  const issues = (await api(`projects/${p.id}/revision-check`, 'priya')).deliverables[0].issues.map(i => [i.revision, i.marks])
  assert.deepEqual(issues, [['Rev 1', [nonStandard]], ['C', []], ['B', ['Out of sequence']]])
  const current = await api(`projects/${p.id}`, 'priya')
  await send('PUT', `projects/${p.id}/revision-check`, 'priya', { rowVersion: current.rowVersion, preset: null })
  assert.deepEqual((await api(`projects/${p.id}/revision-check`, 'priya')).deliverables, [])
  assert.deepEqual(await observed(), marked)
  assert.deepEqual({ settingsPage, detail, register }, { settingsPage: [], detail: [], register: [] })
  pass('AC-VER-11', { project: p.number, deliverable: deliverable.id, issues })
}

;(async () => {
  await requireFreePort(apiPort); await requireFreePort(uiPort)
  for (const file of ['src/Hub.Api/bin/Release/net10.0/Hub.Api.dll', 'src/Hub.Api/wwwroot/index.html'])
    assert.ok(fs.existsSync(path.join(root, file)), `${file} is missing: build first`)
  // The release step that seals each chain under the migration identity before the flag can be switched on.
  const initializer = start('dotnet', dll, 'initialize', { Db__MigrateOnly: 'true', Db__InitializeAuditChains: 'true' }, apiDir)
  const [initialized] = await once(initializer, 'exit')
  assert.equal(initialized, 0, `migration initializer failed; see ${output}/initialize.log`)
  const backend = start('dotnet', [...dll, '--urls', apiBase], 'api', {}, apiDir)
  await ready(`${apiBase}/health`, backend)
  const preview = start(process.execPath, ['web/node_modules/vite/bin/vite.js', 'preview', 'web', '--config', 'web/vite.config.ts', '--host', '127.0.0.1',
    '--port', String(uiPort), '--strictPort'], 'preview', { HUB_API_ORIGIN: apiBase })
  await ready(ui, preview)
  assert.equal((await api('config', 'priya')).authMode, 'Development')
  for (const flag of ['verifiable_log', 'commitment_reliability', 'allocation_preview', 'revision_check'])
    await send('PUT', `admin/settings/flag.${flag}`, 'jordan', { value: true })
  reference = await api('reference', 'priya')
  people = Object.fromEntries(await Promise.all(['alex', 'marc', 'sam'].map(async who => [who, await api('me', who)])))
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  try {
    const [jordan, priya, alex] = [await signIn('Jordan Lee'), await signIn('Priya Nair'), await signIn('Alex Chen')]
    await chain(jordan, priya)
    await missCategory(alex, jordan)
    await allocationPreview(priya)
    await revisionCheck(priya)
    assert.deepEqual(pageErrors, [])
    fs.writeFileSync(path.join(output, 'evidence.json'), JSON.stringify(evidence, null, 2) + '\n')
    console.log(`PASS: AC-VER-02, AC-VER-07, AC-VER-09 and AC-VER-11 in Chromium against the real API; evidence ${output}/evidence.json`)
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 }).finally(() => {
  for (const child of children) { try { process.kill(-child.pid, 'SIGTERM') } catch {} }
  // A passing run removes its synthetic database; a failing one keeps it for inspection.
  if (!process.exitCode) execFileSync('docker', ['exec', container, 'psql', '-U', 'hub', '-d', 'postgres', '-Atc', `drop database if exists ${database} with (force)`])
})
