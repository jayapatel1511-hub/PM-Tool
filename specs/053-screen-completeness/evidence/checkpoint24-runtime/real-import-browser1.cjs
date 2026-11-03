// ROOT REVIEW / EXECUTION ONLY. A authored this file without executing it.
// One real UI intent adds40 synthetic QA24 deliverables. Existing progress ALWAYS blocks a rerun.
// Recover an interrupted intent from its original key/order/hash/file; never rerun this producer or mint another key.
const fs = require('node:fs')
const path = require('node:path')
const assert = require('node:assert/strict')
const { createHash, randomUUID } = require('node:crypto')
const { execFileSync } = require('node:child_process')
const buildRoot = path.resolve(__dirname, '../../../..')
const progressFile = path.join(__dirname, 'real-import-browser1-progress.jsonl')
const resultFile = path.join(__dirname, 'real-import-browser1.json')
const screenshotNames = ['invalid-desktop', 'preview-desktop', 'person-desktop', 'preview-375', 'result-375', 'search-desktop', 'files-desktop', 'failure']
const shotPath = name => path.join(__dirname, 'real-ui-import1-' + name + '.png')
// This check precedes dependencies, launch, HTTP, SQL, or any producer. The exclusive claim below closes the race.
assert.ok(!fs.existsSync(progressFile), 'STOP: existing real UI import progress requires original-intent recovery; do not restart this producer')
assert.ok(!fs.existsSync(resultFile) && screenshotNames.every(name => !fs.existsSync(shotPath(name))), 'STOP: preserve existing browser evidence; do not overwrite or restart')
assert.equal(process.env.TUESDAY_REAL_QA, 'isolated-synthetic', 'Root must explicitly select isolated-synthetic QA')
const m = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-qa24-setup1.json'), 'utf8'))
const ownership = JSON.parse(fs.readFileSync(path.join(__dirname, 'qa24-ownership1.json'), 'utf8'))
const prior = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-import-api1.json'), 'utf8'))
assert.equal(m.database, 'hub_qa_checkpoint24'); assert.equal(ownership.database, m.database); assert.equal(prior.database, m.database)
assert.equal(m.status, 'PASS_NORMAL_COMMAND_SETUP'); assert.equal(prior.status, 'PASS_REAL_KESTREL_DATABASE')
assert.equal(m.apiBase, 'http://127.0.0.1:5088'); assert.equal(m.uiBase, 'http://127.0.0.1:8853')
assert.equal(ownership.apiLoopbackPort, 5088); assert.equal(ownership.container, 'codex-pm-052-053-tests')
const guid = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
for (const value of [m.projectId, m.disciplineId, m.typeId, m.sentinel.id, m.task.id, ...Object.values(m.people).map(person => person.id)]) assert.ok(guid(value))
const previousIds = prior.batches.flatMap(batch => batch.created.rows.map(row => row.id))
assert.equal(prior.batches.length, 2); assert.equal(new Set(previousIds).size, 80)
assert.ok(previousIds.every(guid)); assert.ok(prior.batches.every(batch => batch.created.total === 40 && batch.facts.createdLogs === 40))
const hash = value => createHash('sha256').update(value).digest('hex')
const descriptor = name => { const file = path.join(__dirname, name), bytes = fs.readFileSync(file); return { path: file, sha256: hash(bytes), bytes: bytes.length } }
const badFile = descriptor('real-drawings-bad.xlsx'), goodFile = descriptor('real-drawings.xlsx')
const progressFd = fs.openSync(progressFile, 'wx', 0o600), runId = randomUUID()
let sequence = 0
function record(value) { fs.writeSync(progressFd, JSON.stringify({ sequence: ++sequence, atUtc: new Date().toISOString(), runId, ...value }) + '\n'); fs.fsyncSync(progressFd) }
record({ stage: 'CLAIMED_BEFORE_LAUNCH', database: m.database, apiBase: m.apiBase, uiBase: m.uiBase, projectId: m.projectId, actorId: m.people.pm.id,
  source: { bad: badFile, good: goodFile }, recovery: 'No automatic restart/retry/new key. Reconstruct only the saved intent with verified original bytes and wire property order.' })
const { chromium } = require(path.join(buildRoot, 'web/node_modules/playwright'))
const apiRoot = '/api/v1/projects/' + m.projectId + '/deliverables'
const sourcePath = '/projects/' + encodeURIComponent(m.projectNumber) + '/deliverables?group=none&layout=register'
const result = { status: 'RUNNING', database: m.database, projectId: m.projectId, actorId: m.people.pm.id, source: { bad: badFile, good: goodFile },
  checks: [], apiRequests: [], privateProjectOpened: [], bootstrap: [], previews: [], personReads: [], assets: [], screenshots: [], unexpected: [] }
let browser, page, armed = false, intent = null, forwarded = false, fatal = null
const projectTokens = new Set(), pendingResponses = new Set()
const sql = query => {
  assert.ok(query.startsWith('SELECT ') && !query.includes(';'), 'Read-only SELECT only')
  return execFileSync('docker', ['exec', '-e', 'PGOPTIONS=-c default_transaction_read_only=on', ownership.container, 'psql', '-U', 'hub', '-d', m.database,
    '-qAt', '-v', 'ON_ERROR_STOP=1', '-c', query], { encoding: 'utf8', maxBuffer: 4 * 1024 * 1024 }).trim()
}
function databaseState() {
  return JSON.parse(sql(`SELECT jsonb_build_object('count',(SELECT count(*) FROM hub.deliverable WHERE project_id='${m.projectId}'),'ids',(SELECT coalesce(jsonb_agg(id ORDER BY id),'[]'::jsonb) FROM hub.deliverable WHERE project_id='${m.projectId}'),'createdImportLogs',(SELECT count(*) FROM hub.activity_log WHERE item_type='Deliverable' AND action='Created' AND source='Import' AND item_id IN (SELECT id FROM hub.deliverable WHERE project_id='${m.projectId}')),'issues',(SELECT count(*) FROM hub.deliverable_issue WHERE deliverable_id IN (SELECT id FROM hub.deliverable WHERE project_id='${m.projectId}')),'sentinel',(SELECT to_jsonb(d) FROM hub.deliverable d WHERE id='${m.sentinel.id}'))::text`))
}
const businessState = value => ({ count: value.count, ids: value.ids, createdImportLogs: value.createdImportLogs, issues: value.issues, sentinel: value.sentinel })
function refuse(message, request) {
  fatal ??= new Error(message)
  const failure = { method: request.method(), path: new URL(request.url()).pathname, reason: message }
  result.unexpected.push(failure); record({ stage: 'REFUSED_REQUEST', ...failure })
}
function observe(request) {
  const url = new URL(request.url()), method = request.method()
  if (!url.pathname.startsWith('/api/')) return
  result.apiRequests.push({ method, path: url.pathname })
  if (['GET', 'HEAD', 'OPTIONS'].includes(method)) return
  if (method === 'POST' && url.pathname === '/api/v1/me/sign-in') { result.bootstrap.push({ method, path: url.pathname }); return }
  if (method === 'POST' && url.pathname === apiRoot + '/import-preview') return // Explicitly read-only POST, separate from commands.
  if (method === 'POST' && url.pathname === '/api/v1/me/work/opened') return // Gate validates exact owned Project role tokens below.
  if (method !== 'POST' || url.pathname !== apiRoot + '/import') { refuse('Unexpected business method; only the single armed UI import is permitted', request); return }
  try {
    assert.ok(armed && !intent && !forwarded, 'No automatic/second import command or fresh-key retry is permitted')
    const body = request.postDataJSON(), wire = request.postData(), key = request.headers()['idempotency-key']
    assert.ok(guid(key) && typeof wire === 'string')
    assert.equal(body.format, 'xlsx'); assert.equal(body.contentBase64, fs.readFileSync(goodFile.path).toString('base64'))
    assert.equal(hash(fs.readFileSync(goodFile.path)), goodFile.sha256)
    assert.deepEqual(body.defaults, { projectDisciplineId: m.disciplineId, deliverableTypeId: m.typeId })
    assert.ok(typeof body.previewFingerprint === 'string' && Number.isInteger(body.projectRowVersion))
    const { contentBase64, ...logicalBody } = body
    intent = { key, method, path: url.pathname, logicalBody, wirePropertyOrder: Object.keys(body), defaultsPropertyOrder: Object.keys(body.defaults),
      wireSha256: hash(wire), wireBytes: Buffer.byteLength(wire), file: goodFile }
    armed = false
    // Synchronous, fsynced record before the route gate forwards the actual request. No source bytes are persisted.
    record({ stage: 'IMPORT_REQUEST_INTENT', ...intent })
    result.intent = intent
  } catch (error) { refuse(error.message, request) }
}
async function responseFacts(response) {
  const url = new URL(response.url()), method = response.request().method()
  if (url.pathname.startsWith('/assets/') && response.ok()) {
    const bytes = await response.body(); result.assets.push({ path: url.pathname, bytes: bytes.length, sha256: hash(bytes) }); return
  }
  if (url.pathname === apiRoot + '/import' && method === 'POST') {
    let data = null
    try { data = await response.json() } catch {}
    const receipt = { status: response.status(), key: response.request().headers()['idempotency-key'], createdIds: data?.rows?.map(row => row.id) ?? [], total: data?.total ?? null, rows: data?.rows ?? null }
    record({ stage: 'IMPORT_RESPONSE', ...receipt }); result.receipt = receipt
  } else if (url.pathname === apiRoot + '/import-preview' && method === 'POST') {
    const data = await response.json(), request = response.request().postDataJSON()
    assert.equal(response.status(), 200)
    result.previews.push({ creates: data.creates, errors: data.errors, canCommit: data.canCommit, format: data.format, selectedSheet: data.selectedSheet,
      fileSha256: hash(Buffer.from(request.contentBase64, 'base64')), projectRowVersion: data.projectRowVersion, previewFingerprint: data.previewFingerprint,
      row2OwnerId: data.rows.find(row => row.sourceRow === 2)?.values?.ownerId, row22OwnerId: data.rows.find(row => row.sourceRow === 22)?.values?.ownerId,
      invalidSourceRows: data.rows.filter(row => row.errors.length).map(row => row.sourceRow) })
  } else if (/^\/api\/v1\/people\/[^/]+\/panel$/.test(url.pathname)) {
    const data = await response.json(); result.personReads.push({ userId: url.pathname.split('/')[4], status: response.status(), returnedId: data.id })
  } else if (url.pathname === '/api/v1/me/work/opened' && method === 'POST') {
    record({ stage: 'PRIVATE_PROJECT_OPENED_RESPONSE', requestId: response.request().postDataJSON().requestId, status: response.status() })
    assert.equal(response.status(), 200, 'Expected private Project role acknowledgement is separate from business commands')
  }
}
async function drainResponses() { while (pendingResponses.size) await Promise.all([...pendingResponses]); if (fatal) throw fatal }
async function apiRead(route) {
  const response = await page.request.get(m.apiBase + '/api/v1/' + route, { headers: { 'X-Dev-User': 'priya@hub.test' } })
  assert.equal(response.status(), 200, 'Owned QA24 preflight GET ' + route)
  return response.json()
}
async function shot(name) { const file = shotPath(name); assert.ok(!fs.existsSync(file)); await page.screenshot({ path: file, fullPage: false }); result.screenshots.push(file) }
const importDialog = () => page.getByRole('dialog', { name: 'Import drawing list', exact: true })
const personSheet = () => page.getByRole('dialog', { name: 'Person', exact: true })
const leaveDialog = () => page.getByRole('dialog', { name: 'Keep editing or discard?', exact: true })
const row = number => importDialog().locator('[data-drawing-import-row="' + number + '"]')
const sourceFileIdentity = () => page.locator('#drawing-import-file').evaluate(async node => {
  const file = node.files?.[0]; if (!file) return null
  const bytes = await file.arrayBuffer(), digest = await crypto.subtle.digest('SHA-256', bytes)
  return { name: file.name, bytes: file.size, sha256: [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, '0')).join('') }
})
async function personReturn(opener, id, escape = false) {
  const origin = page.url(), focusId = await opener.getAttribute('id')
  assert.ok(focusId); await opener.focus(); await page.keyboard.press('Enter')
  await page.locator('[data-person-panel="' + id + '"]').waitFor()
  assert.equal(await leaveDialog().count(), 0)
  if (escape) await page.keyboard.press('Escape'); else await personSheet().getByRole('button', { name: 'Close', exact: true }).click()
  await page.waitForURL(origin); await page.waitForFunction(id => document.activeElement?.id === id, focusId)
  assert.equal(await leaveDialog().count(), 0)
  await drainResponses(); return focusId
}
;(async () => {
  const chrome = process.env.CHROME_EXECUTABLE_PATH ?? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
  assert.ok(fs.existsSync(chrome), 'Actual Chrome executable is required')
  browser = await chromium.launch({ executablePath: chrome, headless: true })
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
  await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'priya@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
  page = await context.newPage(); page.setDefaultTimeout(20000)
  const errors = []; page.on('pageerror', error => errors.push(error.message)); page.on('request', observe)
  page.on('response', response => { const pending = responseFacts(response).catch(error => { fatal ??= error }).finally(() => pendingResponses.delete(pending)); pendingResponses.add(pending) })
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url()), method = request.method()
    try {
      assert.ok([m.uiBase, m.apiBase].includes(url.origin), 'Only the owned loopback API/proxy is permitted')
      if (['GET', 'HEAD', 'OPTIONS'].includes(method)) return route.continue()
      if (method === 'POST' && ['/api/v1/me/sign-in', apiRoot + '/import-preview'].includes(url.pathname)) return route.continue()
      if (method === 'POST' && url.pathname === '/api/v1/me/work/opened') {
        const body = request.postDataJSON()
        assert.equal(body.itemType, 'Project'); assert.equal(body.itemId.toLowerCase(), m.projectId.toLowerCase())
        assert.ok(guid(body.requestId) && Array.isArray(body.observedTokens) && body.observedTokens.length > 0 && body.observedTokens.length <= 32)
        assert.ok(body.observedTokens.every(token => projectTokens.has(token.toLowerCase())), 'Only preflight-confirmed own ProjectManager tokens')
        result.privateProjectOpened.push({ requestId: body.requestId, itemType: body.itemType, itemId: body.itemId, observedTokens: body.observedTokens })
        return route.continue()
      }
      if (method === 'POST' && url.pathname === apiRoot + '/import') {
        assert.ok(!fatal && intent && !forwarded && !armed)
        assert.equal(request.headers()['idempotency-key'], intent.key); assert.equal(hash(request.postData()), intent.wireSha256)
        forwarded = true; record({ stage: 'IMPORT_FORWARD_ONCE', key: intent.key }); return route.continue()
      }
      throw new Error('Unexpected business request')
    } catch (error) { refuse(error.message, request); return route.abort('blockedbyclient') }
  })
  const me = await apiRead('me'); assert.equal(me.id, m.people.pm.id)
  for (const flag of ['register_screens', 'people_screens', 'work_screens', 'visual_system']) assert.equal(me.flags[flag], true)
  const project = await apiRead('projects/' + m.projectId); assert.equal(project.projectManagerId, m.people.pm.id)
  for (const token of project.seen?.observedTokens ?? []) { assert.equal(token.role, 'ProjectManager'); projectTokens.add(token.token.toLowerCase()) }
  record({ stage: 'PREFLIGHT_READS_CONFIRMED', actorId: me.id, projectId: project.id, allowedPrivateProjectTokens: projectTokens.size })
  const before = databaseState(); assert.ok(before.ids.includes(m.sentinel.id) && previousIds.every(id => before.ids.includes(id)))
  record({ stage: 'BASELINE', ...before }); result.before = { count: before.count, createdImportLogs: before.createdImportLogs, issues: before.issues, sentinelSha256: hash(JSON.stringify(before.sentinel)) }
  await page.goto(m.uiBase + sourcePath, { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Import drawing list', exact: true }).click()
  await importDialog().locator('#drawing-import-discipline').selectOption(m.disciplineId)
  await importDialog().locator('#drawing-import-type').selectOption(m.typeId)
  await importDialog().locator('#drawing-import-file').setInputFiles(badFile.path)
  await importDialog().getByRole('button', { name: 'Preview drawing list', exact: true }).click()
  await importDialog().getByText('39 proposed creates · 1 errors', { exact: true }).waitFor(); await drainResponses()
  assert.equal(await importDialog().getByRole('button', { name: 'Create 39 deliverables', exact: true }).isDisabled(), true)
  assert.equal(await importDialog().locator('[data-drawing-import-row]').count(), 40)
  assert.ok(result.previews.at(-1).invalidSourceRows.includes(24)); assert.equal(intent, null)
  assert.deepEqual(businessState(databaseState()), businessState(before), 'Bad real workbook preview creates no DB records/logs/issues and preserves sentinel')
  await row(24).scrollIntoViewIfNeeded(); await shot('invalid-desktop')
  result.checks.push('Real40-row bad XLSX: row24 error,39/1, disabled Create, unchanged SQL business baseline')
  await importDialog().locator('#drawing-import-file').setInputFiles(goodFile.path)
  await importDialog().getByRole('region', { name: 'Preview drawing list', exact: true }).waitFor({ state: 'detached' })
  await importDialog().getByRole('button', { name: 'Preview drawing list', exact: true }).click()
  await importDialog().getByText('40 proposed creates · 0 errors', { exact: true }).waitFor(); await drainResponses()
  assert.equal(result.previews.length, 2, 'Only the two explicit bad/corrected previews occurred before the producer')
  const preview = result.previews.at(-1); assert.equal(preview.fileSha256, goodFile.sha256); assert.equal(preview.row2OwnerId, m.people.marc.id)
  await row(2).locator('summary').click(); await shot('preview-desktop')
  assert.equal(preview.row22OwnerId, m.people.marc.id)
  await row(22).locator('summary').click()
  const owner = row(22).locator('[data-person-panel-trigger="' + m.people.marc.id + '"]')
  const fileBeforePerson = await sourceFileIdentity(), body = importDialog().locator('[data-slot="dialog-body"]')
  const rowList = importDialog().getByRole('list', { name: 'Drawing list rows', exact: true })
  assert.equal(fileBeforePerson.sha256, goodFile.sha256)
  await owner.scrollIntoViewIfNeeded(); await owner.focus()
  const scroll = await body.evaluate(node => node.scrollTop), rowsScroll = await rowList.evaluate(node => node.scrollTop)
  assert.ok(rowsScroll > 0, 'The actual overflowing preview row list has genuine nonzero source scroll')
  const focusId = await owner.getAttribute('id'), origin = page.url()
  await page.keyboard.press('Enter'); await page.locator('[data-person-panel="' + m.people.marc.id + '"]').waitFor()
  assert.equal(await leaveDialog().count(), 0); assert.deepEqual(await sourceFileIdentity(), fileBeforePerson); await shot('person-desktop')
  await page.keyboard.press('Escape'); await page.waitForURL(origin); await page.waitForFunction(id => document.activeElement?.id === id, focusId)
  assert.equal(await leaveDialog().count(), 0); assert.deepEqual(await sourceFileIdentity(), fileBeforePerson)
  assert.equal(await row(22).locator('details').evaluate(node => node.open), true)
  assert.equal(await row(2).locator('details').evaluate(node => node.open), true)
  assert.ok(Math.abs(await body.evaluate(node => node.scrollTop) - scroll) <= 2, 'Measured outer dialog scroll is preserved, including zero')
  assert.ok(Math.abs(await rowList.evaluate(node => node.scrollTop) - rowsScroll) <= 2, 'Genuinely nonzero preview-list scroll stays mounted'); await drainResponses()
  assert.deepEqual(businessState(databaseState()), businessState(before))
  result.checks.push('Actual row22 Marc Person Enter/Escape naturally retains source file/hash, row2/22 disclosures, genuine nonzero preview-list and measured outer scroll, full opener focus and no discard prompt')
  await page.setViewportSize({ width: 375, height: 900 }); await row(22).scrollIntoViewIfNeeded()
  const bounds = await importDialog().boundingBox(); assert.ok(bounds && bounds.x >= -1 && bounds.x + bounds.width <= 376)
  assert.equal(await body.evaluate(node => node.scrollWidth <= node.clientWidth + 1), true); await shot('preview-375')
  // The sole producer action. No retry path exists in this driver, even after a transport failure.
  const answer = page.waitForResponse(response => new URL(response.url()).pathname === apiRoot + '/import' && response.request().method() === 'POST')
  armed = true; await importDialog().getByRole('button', { name: 'Create 40 deliverables', exact: true }).click()
  const response = await answer; await drainResponses(); assert.equal(response.status(), 200)
  await importDialog().getByRole('region', { name: 'Import result', exact: true }).getByText('40 deliverables created.', { exact: true }).waitFor()
  assert.ok(intent && forwarded && !armed); assert.equal(result.receipt.total, 40)
  const madeIds = result.receipt.createdIds; assert.equal(new Set(madeIds).size, 40); assert.ok(madeIds.every(id => guid(id) && !before.ids.includes(id)))
  const quotedIds = madeIds.map(id => "'" + id + "'").join(',')
  const imported = JSON.parse(sql(`SELECT jsonb_build_object('records',(SELECT count(*) FROM hub.deliverable WHERE id IN (${quotedIds}) AND project_id='${m.projectId}' AND status='Not Started'),'issues',(SELECT count(*) FROM hub.deliverable_issue WHERE deliverable_id IN (${quotedIds})),'createdImportLogs',(SELECT count(*) FROM hub.activity_log WHERE item_type='Deliverable' AND item_id IN (${quotedIds}) AND action='Created' AND source='Import'),'sourceRows',(SELECT count(DISTINCT snapshot->>'sourceRow') FROM hub.activity_log WHERE item_type='Deliverable' AND item_id IN (${quotedIds}) AND action='Created' AND source='Import'),'batches',(SELECT count(DISTINCT snapshot->>'batchId') FROM hub.activity_log WHERE item_type='Deliverable' AND item_id IN (${quotedIds}) AND action='Created' AND source='Import'))::text`))
  assert.deepEqual(imported, { records: 40, issues: 0, createdImportLogs: 40, sourceRows: 40, batches: 1 })
  const after = databaseState(); assert.equal(after.count - before.count, 40); assert.equal(after.createdImportLogs - before.createdImportLogs, 40)
  assert.equal(after.issues, before.issues); assert.deepEqual(after.sentinel, before.sentinel)
  assert.deepEqual(after.ids.filter(id => !before.ids.includes(id)).sort(), [...madeIds].sort())
  result.createdFacts = imported; result.after = { count: after.count, createdImportLogs: after.createdImportLogs, issues: after.issues, sentinelSha256: hash(JSON.stringify(after.sentinel)) }
  record({ stage: 'SQL_JOIN_CONFIRMED', createdIds: madeIds, ...result.createdFacts, beforeCount: before.count, afterCount: after.count }); await shot('result-375')
  await importDialog().getByRole('button', { name: 'Close', exact: true }).first().click(); await importDialog().waitFor({ state: 'detached' }); assert.equal(await leaveDialog().count(), 0)
  result.checks.push('One explicit Chrome Create40 joins exact successful response IDs to SQL delta40,40Created/Import logs,40source rows,1batch,0new issues and unchanged sentinel')
  await page.setViewportSize({ width: 1440, height: 1000 })
  const searchPath = '/search?q=' + encodeURIComponent(m.projectNumber) + '&type=tasks'
  const searchResponse = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/search' && new URL(response.url()).searchParams.get('type') === 'tasks')
  await page.goto(m.uiBase + searchPath, { waitUntil: 'domcontentloaded' })
  const searchData = await (await searchResponse).json(), actualTask = searchData.groups.tasks.find(task => task.id === m.task.id)
  assert.equal(actualTask.person.id, m.people.alex.id); assert.equal(actualTask.person.role, 'assignee')
  const taskRow = page.locator('[data-search-page-hit="tasks:' + m.task.id + '"]'); await taskRow.waitFor()
  assert.equal(await page.locator('.visual-sidebar a[aria-current="page"]').count(), 0, 'Search utility selects no sidebar rail item')
  await personReturn(taskRow.locator('[data-person-panel-trigger="' + m.people.alex.id + '"]'), m.people.alex.id)
  assert.equal(page.url(), m.uiBase + searchPath); assert.equal(await page.locator('.visual-sidebar a[aria-current="page"]').count(), 0); await shot('search-desktop')
  const filesPath = '/files?projects=' + m.projectId
  const filesResponse = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/files' && new URL(response.url()).searchParams.get('projects') === m.projectId)
  await page.goto(m.uiBase + filesPath, { waitUntil: 'domcontentloaded' })
  const filesData = await (await filesResponse).json(), links = filesData.projects.flatMap(project => project.items.flatMap(item => item.links))
  assert.deepEqual(links.map(link => link.id).sort(), m.links.map(link => link.id).sort())
  assert.equal(links.find(link => link.id === m.links[0].id).addedByUser.id, m.people.pm.id)
  assert.equal(links.find(link => link.id === m.links[1].id).addedByUser.id, m.people.jill.id)
  const authorRow = page.getByRole('link', { name: m.links[1].title, exact: true }).locator('xpath=ancestor::li[1]')
  await personReturn(authorRow.locator('[data-person-panel-trigger="' + m.people.jill.id + '"]'), m.people.jill.id)
  assert.equal(page.url(), m.uiBase + filesPath); await shot('files-desktop'); await drainResponses()
  assert.equal(result.apiRequests.filter(request => request.method === 'POST' && request.path === apiRoot + '/import').length, 1)
  assert.ok(result.personReads.some(read => read.userId === m.people.marc.id) && result.personReads.some(read => read.userId === m.people.alex.id) && result.personReads.some(read => read.userId === m.people.jill.id))
  assert.ok(result.personReads.every(read => read.status === 200 && read.returnedId === read.userId))
  assert.deepEqual(businessState(databaseState()), businessState(after), 'Search/Files/Person add no further business commands or records')
  assert.deepEqual(result.unexpected, []); assert.deepEqual(errors, [])
  result.checks.push('Actual Search task metadata Alex Person and Files two typed authors/Jill Person naturally return; no Search rail selection or additional business command')
  result.status = 'PASS_REAL_CHROME_API_SQL_JOIN'
  result.boundary = 'Owned synthetic QA24 only. UI/API/SQL join with actual workbooks; API atomicity proof is real-import-api1.json. Asset samples are observed served resources, not complete parity. No pilot, deployment or human acceptance.'
  record({ stage: 'PASS', status: result.status, checks: result.checks })
  fs.writeFileSync(resultFile, JSON.stringify(result, null, 2) + '\n', { flag: 'wx' })
  console.log(JSON.stringify({ status: result.status, created: result.receipt.total, screenshots: result.screenshots.length, privateProjectOpened: result.privateProjectOpened.length, progressFile, resultFile }))
})().catch(async error => {
  result.status = 'FAIL_REQUIRES_ORIGINAL_INTENT_REVIEW'; result.error = error.message
  record({ stage: 'FAILED_DO_NOT_RESTART', error: error.message, intentKey: intent?.key ?? null, forwarded, responseStatus: result.receipt?.status ?? null, createdIds: result.receipt?.createdIds ?? [] })
  if (page && !fs.existsSync(shotPath('failure'))) { try { await shot('failure') } catch {} }
  if (!fs.existsSync(resultFile)) fs.writeFileSync(resultFile, JSON.stringify(result, null, 2) + '\n', { flag: 'wx' })
  console.error(JSON.stringify({ status: result.status, error: error.message, progressFile, recovery: 'Inspect saved original key/body order/file hash/response IDs. Do not rerun this producer or create a fresh intent.' })); process.exitCode = 1
}).finally(async () => { try { if (browser) await browser.close() } finally { fs.closeSync(progressFd) } })
