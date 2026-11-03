// SOURCE PREPARATION ONLY. Root is the sole executor against the disposable QA22 API.
// CHECKPOINT22_REAL_BROWSER_EXECUTE=1 TUESDAY_QA_DATABASE=hub_qa_checkpoint22
// node <this file> (final preview-build2-identity.json is required by default).
// All API responses are real. Every API mutation is observed, aborted and fails the run.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { createHash, randomUUID } = require('node:crypto')

const UI = process.env.TUESDAY_UI_BASE_URL || 'http://127.0.0.1:8853'
const API = 'http://127.0.0.1:5088/api/v1'
const DATABASE = 'hub_qa_checkpoint22'
const files = {
  timeline: path.join(__dirname, 'checkpoint22-real-timeline-setup.json'),
  deliverable: path.join(__dirname, 'checkpoint22-real-deliverable-setup.json'),
  person: path.join(__dirname, 'checkpoint22-real-person-setup.json'),
  profile: path.join(__dirname, 'checkpoint22-runtime/qa-runtime-profile1.json'),
}
const report = { kind: 'checkpoint22-real-browser-joins', status: 'NOT_RUN', startedAtUtc: new Date().toISOString(),
  target: { ui: UI, api: API, databaseEnvironment: process.env.TUESDAY_QA_DATABASE || null }, prerequisites: {},
  steps: [], sessions: [], directReads: [], screenshots: [], failures: [], sourceGaps: [], mutations: [],
  limits: ['Local real API and Chrome only; no hosted, native zoom, pilot, human acceptance or Accepted claim.',
    'Database binding is attested by the root runtime profile and required environment; the public API does not expose a connection string.',
    'The browser sends no business, saved-view, settings, authentication or role command. A blocked mutation is a FAIL, never a successful read-only run.',
    'Membership, capacity and date joins use current real responses and fresh setup IDs. Unknown/out-of-scope facts are never replaced with zero.'] }
let directory, browser, receipts, activeSeeds, screenshotSequence = 0
const sha = text => createHash('sha256').update(text).digest('hex')
const load = file => { assert.ok(fs.existsSync(file), `Missing prerequisite receipt: ${file}`); return JSON.parse(fs.readFileSync(file, 'utf8')) }
const sorted = values => [...values].sort()
const sameIds = (actual, expected, label) => assert.deepEqual(sorted(actual), sorted(expected), label)
const text = value => String(value).replace(/\s+/g, ' ').trim()
const equalText = (actual, expected, label) => assert.equal(text(actual), text(expected), label)
const contains = (actual, expected, label) => assert.ok(text(actual).includes(text(expected)), `${label}: ${JSON.stringify(expected)} absent from ${JSON.stringify(actual)}`)
const isGuid = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
function observedResponse(page, predicate) {
  const promise = page.waitForResponse(predicate)
  promise.catch(() => {}) // Preserve durable evidence if its triggering UI action fails first.
  return promise
}
const hours = value => { assert.ok(Number.isFinite(value), 'Canonical hours must be finite, never defaulted'); const n = Math.round(value * 100) / 100; return `${n < 0 ? '−' : ''}${Math.abs(n).toFixed(2).replace(/\.?0+$/, '')} h` }
const dateText = (value, format) => {
  assert.match(value || '', /^\d{4}-\d{2}-\d{2}$/)
  const [year, month, day] = value.split('-')
  return format === 'd MMM yyyy' ? `${Number(day)} ${['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][Number(month) - 1]} ${year}` : value
}
function persist() {
  if (!directory) return
  const temporary = path.join(directory, 'report.tmp')
  fs.writeFileSync(temporary, JSON.stringify(report, null, 2) + '\n', { mode: 0o600 })
  fs.renameSync(temporary, path.join(directory, 'report.json'))
}
function uiLink(value) { const url = new URL(value, UI); assert.equal(url.origin, UI); return url.href }
function sourceGap(message, facts) { report.sourceGaps.push({ message, facts }); persist(); return { status: 'UNPROVEN_SOURCE_GAP', message, facts } }
async function get(route, actor) {
  assert.ok(route.startsWith('/') && !route.startsWith('//') && !route.includes('://'))
  if (actor) assert.ok(activeSeeds.has(actor.email.toLowerCase()), 'No identity header for an unverified seed')
  const url = route === '/api/dev/users' ? 'http://127.0.0.1:5088/api/dev/users' : API + route
  const response = await fetch(url, { method: 'GET', redirect: 'error', signal: AbortSignal.timeout(15000), headers: actor ? { 'X-Dev-User': actor.email } : {} })
  const entry = { actor: actor?.email || 'anonymous', url, status: response.status }; report.directReads.push(entry)
  const body = await response.json(); entry.body = body; persist()
  assert.equal(response.status, 200, `Real read failed: ${url}`)
  return body
}
async function allPerson(actor, id) {
  const first = await get(`/people/${id}/panel?projectsPage=1&projectsPageSize=100`, actor)
  assert.equal(first.id, id); assert.ok(Array.isArray(first.projects?.items))
  const items = [...first.projects.items]
  for (let page = 2; items.length < first.projects.total && page <= 20; page++) {
    const read = await get(`/people/${id}/panel?projectsPage=${page}&projectsPageSize=100`, actor)
    assert.equal(read.projects.total, first.projects.total, 'Real membership population changed during paging')
    assert.ok(read.projects.items.length); items.push(...read.projects.items)
  }
  assert.equal(items.length, first.projects.total, 'Full current person population must fit the bounded read')
  assert.equal(new Set(items.map(row => row.memberId)).size, items.length)
  return { ...first, projects: { ...first.projects, items } }
}
async function allVisibleProjects(actor) {
  const items = []; let total
  for (let page = 1; page <= 10; page++) {
    const read = await get(`/projects?includeArchived=true&pageSize=200&page=${page}`, actor)
    assert.ok(Array.isArray(read.items)); assert.ok(Number.isInteger(read.totalCount))
    if (total == null) total = read.totalCount
    assert.equal(read.totalCount, total, 'Actual visible-project population changed during paging')
    items.push(...read.items)
    if (items.length >= total) { assert.equal(items.length, total); return items }
    assert.ok(read.items.length, 'Visible-project paging ended before its declared total')
  }
  throw new Error('Actual visible-project population exceeds the bounded 2000-row evidence budget')
}
async function screenshot(page, label) {
  const file = path.join(directory, `${String(++screenshotSequence).padStart(3, '0')}-${label.replace(/[^a-z0-9-]/gi, '-')}.png`)
  assert.ok(!fs.existsSync(file), 'Never overwrite an existing screenshot')
  await page.screenshot({ path: file, fullPage: true })
  report.screenshots.push(file); persist(); return file
}
async function step(session, name, fn) {
  const item = { name, actor: session.actor.email, width: session.width, status: 'RUNNING', startedAtUtc: new Date().toISOString() }
  report.steps.push(item); persist()
  try {
    const result = await fn()
    item.status = result?.status === 'UNPROVEN_SOURCE_GAP' ? result.status : 'PASS'; item.facts = result
    item.screenshot = await screenshot(session.page, `${session.actor.email.split('@')[0]}-${session.width}-${name}`)
  } catch (error) {
    item.status = 'FAIL'; item.error = error.stack || String(error); item.url = session.page.url()
    try { item.screenshot = await screenshot(session.page, `${session.actor.email.split('@')[0]}-${session.width}-${name}-FAIL`) } catch (captureError) { item.captureError = captureError.message }
    report.failures.push({ step: name, actor: session.actor.email, width: session.width, message: error.message })
  } finally {
    item.finishedAtUtc = new Date().toISOString(); persist()
    fs.writeFileSync(path.join(directory, `${String(report.steps.length).padStart(3, '0')}-${name}-${session.width}-${session.actor.email.split('@')[0]}.json`), JSON.stringify(item, null, 2) + '\n', { flag: 'wx', mode: 0o600 })
  }
}
async function settle(page) {
  await page.evaluate(async () => {
    await document.fonts.ready
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))
    const nodes = [...document.querySelectorAll('.visual-page,.visual-page-band,.visual-page-content')]
    const animations = [...new Set(nodes.flatMap(node => node.getAnimations()))].filter(animation => animation.pending || animation.playState === 'running')
    let timer
    try { await Promise.race([Promise.allSettled(animations.map(animation => animation.finished)), new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Layout animation readiness timeout')), 2000) })]) }
    finally { clearTimeout(timer) }
  })
}
async function containment(page) {
  await settle(page)
  const result = await page.evaluate(() => ({ viewport: innerWidth, document: document.documentElement.scrollWidth,
    content: document.getElementById('content') ? { client: document.getElementById('content').clientWidth, scroll: document.getElementById('content').scrollWidth } : null }))
  assert.ok(result.document <= result.viewport + 1, `Actual document overflow: ${JSON.stringify(result)}`)
  return result
}
async function go(session, destination) {
  const url = uiLink(destination)
  const identity = observedResponse(session.page, response => response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/v1/me')
  const navigation = await session.page.goto(url, { waitUntil: 'domcontentloaded' })
  assert.equal(navigation?.status(), 200)
  const answer = await identity; assert.equal(answer.status(), 200)
  const me = await answer.json(); assert.equal(me.id, session.actor.id); assert.equal(me.email.toLowerCase(), session.actor.email.toLowerCase())
  for (const [flag, enabled] of Object.entries(receipts.profile.flagsAfter)) assert.equal(me.flags?.[flag], enabled, `Root profile drift: ${flag}`)
  assert.equal(me.settings.today, receipts.profile.today)
  session.me = me; session.identities.push({ url, id: me.id, email: me.email, flags: me.flags, settings: me.settings })
  await session.page.locator('main').waitFor(); return me
}
async function withActor(actor, width, body) {
  assert.ok(isGuid(actor.id) && activeSeeds.has(actor.email.toLowerCase()))
  const context = await browser.newContext({ viewport: { width, height: width === 375 ? 812 : 1000 }, reducedMotion: 'reduce' })
  await context.addInitScript(email => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1') }, actor.email)
  const page = await context.newPage(); page.setDefaultTimeout(15000)
  const session = { actor, width, page, identities: [], requests: [], responses: [], errors: [], pending: new Set() }
  page.on('request', request => {
    const url = new URL(request.url())
    if (url.pathname.startsWith('/api/')) session.requests.push({ method: request.method(), url: request.url() })
  })
  page.on('pageerror', error => session.errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') session.errors.push(message.text()) })
  page.on('response', response => {
    const url = new URL(response.url())
    if (!url.pathname.startsWith('/api/')) return
    const promise = (async () => {
      const entry = { method: response.request().method(), url: response.url(), status: response.status() }; session.responses.push(entry)
      if (/\/me$|\/timeline$|\/people\/[^/]+\/panel$|\/planning\/grid$|\/deliverables(?:\/[^/]+)?$|\/views$|\/team$/.test(url.pathname)) {
        try { entry.body = await response.json() } catch (error) { entry.bodyReadError = error.message }
      }
    })().catch(error => session.errors.push(error.message)).finally(() => session.pending.delete(promise))
    session.pending.add(promise)
  })
  await context.route('**/*', route => {
    const request = route.request(), url = new URL(request.url())
    if (['http:', 'https:'].includes(url.protocol) && ![UI, 'http://127.0.0.1:5088'].includes(url.origin)) {
      session.errors.push(`Unexpected external request: ${url.origin}${url.pathname}`); return route.abort('blockedbyclient')
    }
    if (url.pathname.startsWith('/api/') && !['GET', 'HEAD'].includes(request.method())) {
      report.mutations.push({ actor: actor.email, width, method: request.method(), url: request.url(), blocked: true }); persist()
      return route.abort('blockedbyclient')
    }
    return route.continue()
  })
  try { await body(session) } catch (error) { report.failures.push({ actor: actor.email, width, message: error.stack || String(error) }) }
  finally {
    await Promise.allSettled([...session.pending])
    const failedReads = session.responses.filter(response => response.status >= 400)
    if (session.errors.length || failedReads.length) report.failures.push({ actor: actor.email, width, errors: session.errors, failedReads })
    report.sessions.push({ actor, width, identities: session.identities, requests: session.requests, responses: session.responses, errors: session.errors })
    await context.close(); persist()
  }
}
async function openAndReturn(session, opener, kind, id) {
  const { page } = session
  await opener.scrollIntoViewIfNeeded(); await opener.focus()
  const element = await opener.elementHandle()
  const origin = await opener.evaluate(node => ({ url: location.href, top: document.getElementById('content').scrollTop, left: document.getElementById('content').scrollLeft, focusId: node.id || null }))
  await opener.press('Enter'); await page.waitForURL(url => url.searchParams.get('panel') === `${kind}:${id}`)
  const panel = page.getByRole('dialog', { name: kind, exact: true }); await panel.waitFor()
  return { panel, origin, close: async () => {
    await panel.getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(origin.url)
    await page.waitForFunction(({ top, left }) => Math.abs(document.getElementById('content').scrollTop - top) <= 1 && Math.abs(document.getElementById('content').scrollLeft - left) <= 1, origin)
    if (origin.focusId) await page.waitForFunction(id => document.getElementById(id) === document.activeElement, origin.focusId)
    else await page.waitForFunction(el => el === document.activeElement, element)
    return { origin, returnedUrl: page.url(), focusReturned: true, scrollReturnedWithinPx: 1 }
  } }
}
async function timelineCase(session) {
  const r = receipts.timeline, p = r.fixture.projects[0], view = r.fixture.views.find(item => item.label === 'shared-view')
  assert.ok(view && p && r.followUp.expectedCivilTaskIds.length === 2)
  const tasks = r.fixture.tasks.filter(task => r.followUp.expectedCivilTaskIds.includes(task.id))
  await step(session, 'timeline-shared-definition-render', async () => {
    await go(session, `/projects/${encodeURIComponent(p.projectNumber)}/timeline`)
    const current = await get(`/views?listType=timeline&projectId=${p.id}`, session.actor)
    const actual = current.views.find(item => item.id === view.id); assert.ok(actual)
    for (const key of ['disciplineId', 'zoom', 'layers', 'group', 'cols', 'expanded', 'collapsed', 'from', 'to']) equalText(actual.params[key], view.params[key], `Saved definition ${key}`)
    const canonical = await get(`/projects/${p.id}/timeline`, session.actor)
    sameIds(canonical.tasks.map(task => task.id), r.fixture.tasks.filter(task => task.projectId === p.id).map(task => task.id), 'Actual canonical task IDs')
    const edge = canonical.dependencies.find(item => item.id === r.fixture.dependency.id)
    assert.ok(edge); assert.equal(edge.predecessorTaskId, r.fixture.dependency.predecessorTaskId); assert.equal(edge.successorTaskId, r.fixture.dependency.successorTaskId)
    await session.page.getByRole('button', { name: 'Views', exact: true }).click()
    await session.page.getByRole('menuitem', { name: view.name, exact: true }).click()
    await session.page.waitForURL(url => url.searchParams.get('view') === view.id && url.searchParams.get('expanded') === view.params.expanded)
    for (const [key, value] of Object.entries(view.params)) assert.equal(new URL(session.page.url()).searchParams.get(key), value)
    await session.page.getByRole('button', { name: 'Zoom: Month', exact: true }).waitFor()
    await session.page.getByRole('button', { name: /^Filters/ }).click()
    assert.equal(await session.page.locator('#tl-discipline').inputValue(), p.civil)
    const controls = session.page.locator('[data-timeline-layer-controls]:visible')
    sameIds(await controls.locator('[role="checkbox"][aria-checked="true"]').evaluateAll(elements => elements.map(el => el.closest('label').textContent.trim())), ['Dependencies', 'Milestones'], 'Effective shown layers')
    await session.page.keyboard.press('Escape')
    const region = session.page.getByRole('region', { name: 'Timeline', exact: true })
    for (const task of tasks) await region.locator(`[data-visual-timeline-bar="${task.id}"]`).waitFor()
    assert.equal(await region.locator(`[data-visual-timeline-bar="${r.followUp.excludedRenderedTaskId}"]`).count(), 0)
    sameIds(await region.locator('[data-visual-timeline-bar]').evaluateAll(elements => elements.map(el => el.getAttribute('data-visual-timeline-bar'))), r.followUp.expectedCivilTaskIds, 'Exact rendered Civil task IDs')
    await region.locator('svg[data-timeline-layer="dependencies"]').waitFor()
    const observed = session.responses.filter(response => new URL(response.url).pathname === `/api/v1/projects/${p.id}/timeline`)
    assert.ok(observed.length && observed.every(response => new URL(response.url).search === ''), 'UI uses its unchanged canonical project read')
    return { viewId: view.id, canonicalDependency: edge, renderedTaskIds: r.followUp.expectedCivilTaskIds, excludedTaskId: r.followUp.excludedRenderedTaskId, params: Object.fromEntries(new URL(session.page.url()).searchParams), contained: await containment(session.page) }
  })
  await step(session, 'timeline-task-keyboard-return', async () => {
    const task = tasks[0], opener = session.page.locator('[role="rowheader"] button').filter({ hasText: task.name })
    const read = observedResponse(session.page, response => response.request().method() === 'GET' && new URL(response.url()).pathname === `/api/v1/tasks/${task.id}`)
    const opened = await openAndReturn(session, opener, 'Task', task.id)
    const answer = await read; assert.equal(answer.status(), 200); const detail = await answer.json(); assert.equal(detail.task.id, task.id)
    await opened.panel.getByRole('heading', { name: task.name, exact: true }).waitFor()
    const openScreenshot = await screenshot(session.page, `timeline-task-open-${session.actor.email.split('@')[0]}-${session.width}`)
    return { taskId: detail.task.id, openScreenshot, ...await opened.close(), contained: await containment(session.page) }
  })
}
function fixtureDeliverable(id) { const row = receipts.deliverable.fixture.deliverables.find(item => item.id === id); assert.ok(row); return row }
async function deliverablePanel(session, link, id) {
  const answer = observedResponse(session.page, response => response.request().method() === 'GET' && new URL(response.url()).pathname === `/api/v1/deliverables/${id}`)
  await go(session, link)
  const response = await answer; assert.equal(response.status(), 200); const detail = await response.json(); assert.equal(detail.deliverable.id, id)
  const panel = session.page.getByRole('dialog', { name: 'Deliverable', exact: true }); await panel.waitFor()
  await panel.getByRole('tab', { name: /^Issues/ }).click()
  return { panel, detail }
}
async function deliverableCase(session) {
  const r = receipts.deliverable, p = r.fixture.project
  await step(session, 'deliverable-dates-exact-open-partition', async () => {
    await go(session, r.links.dates)
    const canonical = await get(`/projects/${p.id}/deliverables?indicator=open`, session.actor)
    sameIds(canonical.map(row => row.id), r.facts.undated.openIds, 'Real open cohort matches setup')
    await session.page.getByRole('heading', { name: `No due date (${r.facts.undated.expectedIds.length})`, exact: true }).waitFor()
    const trayIds = await session.page.locator('[data-register-undated] [data-register-date-id]').evaluateAll(elements => elements.map(el => el.dataset.registerDateId))
    const datedIds = await session.page.locator('[data-register-dated] [data-register-date-id]').evaluateAll(elements => elements.map(el => el.dataset.registerDateId))
    sameIds(trayIds, r.facts.undated.expectedIds, 'Four exact open undated IDs'); sameIds(datedIds, r.facts.undated.datedIds, 'Dated exact IDs')
    sameIds([...trayIds, ...datedIds], canonical.map(row => row.id), 'UI partition equals the same real open cohort')
    const statuses = Object.fromEntries(trayIds.map(id => [id, canonical.find(row => row.id === id).status]))
    sameIds(Object.values(statuses), Object.keys(r.facts.undated.expectedByStatus), 'Retained open statuses')
    return { projectId: p.id, trayIds, datedIds, statuses, contained: await containment(session.page) }
  })
  await step(session, 'deliverable-date-panel-return', async () => {
    const id = r.facts.undated.expectedIds[0], opener = session.page.locator(`[data-register-date-id="${id}"] button`)
    const read = observedResponse(session.page, response => response.request().method() === 'GET' && new URL(response.url()).pathname === `/api/v1/deliverables/${id}`)
    const opened = await openAndReturn(session, opener, 'Deliverable', id)
    const answer = await read; assert.equal(answer.status(), 200); const detail = await answer.json(); assert.equal(detail.deliverable.id, id)
    await opened.panel.getByRole('heading', { name: detail.deliverable.name, exact: true }).waitFor()
    return { deliverableId: id, ...await opened.close() }
  })
  await step(session, 'deliverable-current-filter-warning-only-number', async () => {
    await go(session, r.links.reviewB)
    const fact = r.facts.reviewResponse, row = fixtureDeliverable(fact.deliverableId)
    const current = await get(`/projects/${p.id}/deliverables?${new URLSearchParams(fact.filter)}`, session.actor)
    sameIds(current.map(item => item.id), fact.filteredIds, 'Real current-purpose/B filter IDs')
    const visible = session.page.locator(session.width < 768 ? 'main li:visible' : 'main tr:visible').filter({ has: session.page.getByText(row.key, { exact: true }) }).first()
    await visible.waitFor(); const content = await visible.innerText(), actual = current[0]
    assert.equal(actual.id, fact.deliverableId); assert.equal(actual.status, fact.status)
    assert.equal(actual.registerInfo.currentIssue.id, fact.issueId)
    assert.equal(actual.registerInfo.clientDrawingNumber, 'C12'); assert.equal(actual.registerInfo.numberMatchesPattern, false)
    contains(content, 'C12', 'Retained mismatched number'); contains(content, 'Does not match C-###', 'Warning, not refusal')
    contains(content, actual.registerInfo.currentIssue.purpose, 'Current purpose'); contains(content, actual.registerInfo.currentIssue.returnCode, 'Current return code')
    contains(content, dateText(actual.registerInfo.currentIssue.responseDate, session.me.settings.dateFormat), 'Actual response date')
    return { deliverableId: actual.id, status: actual.status, currentIssue: actual.registerInfo.currentIssue, registerInfo: actual.registerInfo, visibleText: content, contained: await containment(session.page) }
  })
  await step(session, 'deliverable-backdated-current-history-response-view', async () => {
    const fact = r.facts.backdatedReissue, { panel, detail } = await deliverablePanel(session, r.links.backdatedHistory, fact.deliverableId)
    assert.equal(detail.deliverable.registerInfo.currentIssue.id, fact.currentIssueId)
    const current = panel.locator(`[data-register-issue-id="${fact.currentIssueId}"]`), old = panel.locator(`[data-register-issue-id="${fact.olderIssueId}"]`)
    await current.waitFor(); await old.waitFor()
    assert.equal(await current.getByText('Latest issue', { exact: true }).count(), 1); assert.equal(await old.getByText('Latest issue', { exact: true }).count(), 0)
    const latest = detail.issues.find(issue => issue.id === fact.currentIssueId), older = detail.issues.find(issue => issue.id === fact.olderIssueId)
    assert.ok(latest && older); assert.ok(latest.issuedDate < older.issuedDate, 'Backdated issue is still current by actual identity')
    assert.equal(latest.registerInfo.returnCodeId, null); assert.equal(older.registerInfo.returnCodeId, r.reference.returnC.id)
    contains(await current.innerText(), latest.registerInfo.purpose, 'Current history purpose'); contains(await old.innerText(), older.registerInfo.returnCode, 'Older response isolated to older history')
    equalText(await current.locator('td').nth(7).innerText(), '— Unavailable', 'Unknown current recipient response retains its accessible missing-value label')
    await current.getByRole('button', { name: /^Record recipient response/ }).click()
    const response = session.page.getByRole('dialog').last(); await response.locator('#issue-response-date').waitFor()
    assert.equal(await response.locator('#issue-response-date').inputValue(), '')
    assert.equal(await response.getByLabel('Return code', { exact: true }).inputValue(), '')
    const responseView = await screenshot(session.page, `response-view-only-${session.width}`)
    await response.getByRole('button', { name: 'Cancel', exact: true }).click()
    return { deliverableId: fact.deliverableId, currentIssueId: latest.id, olderIssueId: older.id, currentResponse: latest.registerInfo, olderResponse: older.registerInfo, responseView, fieldsOpenedWithoutEditing: true, contained: await containment(session.page) }
  })
  await step(session, 'deliverable-returned-response-history', async () => {
    const fact = r.facts.reviewResponse, { panel, detail } = await deliverablePanel(session, r.links.reviewHistory, fact.deliverableId)
    const current = panel.locator(`[data-register-issue-id="${fact.issueId}"]`); await current.waitFor()
    const issue = detail.issues.find(item => item.id === fact.issueId); assert.ok(issue)
    assert.equal(issue.registerInfo.returnCodeId, r.reference.returnB.id)
    contains(await current.innerText(), issue.registerInfo.returnCode, 'B response displayed on actual issue')
    contains(await current.innerText(), dateText(issue.registerInfo.responseDate, session.me.settings.dateFormat), 'Historical response date')
    assert.equal(detail.deliverable.status, fact.status)
    return { deliverableId: fact.deliverableId, issueId: issue.id, registerCurrent: detail.deliverable.registerInfo.currentIssue, historyResponse: issue.registerInfo, status: detail.deliverable.status }
  })
}
async function personCase(session, actorName) {
  const r = receipts.person, personId = r.fixture.personId
  await step(session, 'person-team-keyboard-facts-note-project-capacity', async () => {
    await go(session, r.links.projectTeam)
    // Later QA fixtures may add legitimate memberships. Fresh reads, not an old
    // receipt's global total, define this actor's current authorized population.
    const expected = await allPerson(session.actor, personId), visibleProjects = await allVisibleProjects(session.actor)
    const team = await get(`/projects/${r.fixture.projectId}/team`, session.actor)
    const membership = team.members.find(row => row.id === r.fixture.memberId); assert.ok(membership)
    assert.equal(membership.userId, personId); assert.equal(membership.responsibility.note, r.fixture.finalResponsibilityNote)
    const opener = session.page.locator(`${r.selectors.personTrigger}:visible`).first(); await opener.waitFor()
    const actualRead = observedResponse(session.page, response => response.request().method() === 'GET' && new URL(response.url()).pathname === `/api/v1/people/${personId}/panel`)
    const gridAnswer = actorName === 'lena' ? observedResponse(session.page, answer => {
      const url = new URL(answer.url()); return answer.request().method() === 'GET' && url.pathname === '/api/v1/planning/grid' && url.searchParams.get('personIds') === personId && url.searchParams.get('from') === expected.week && url.searchParams.get('weeks') === '1'
    }) : null
    const opened = await openAndReturn(session, opener, 'Person', personId)
    const response = await actualRead; assert.equal(response.status(), 200); const actual = await response.json()
    assert.equal(actual.id, personId); assert.equal(actual.week, expected.week); assert.equal(actual.today, expected.today)
    const panel = opened.panel.locator(r.selectors.personPanel); await panel.waitFor()
    await panel.getByRole('heading', { name: actual.displayName, exact: true }).waitFor()
    assert.deepEqual(actual.personFacts, expected.personFacts, 'Real panel recorded facts match current same-actor read')
    assert.deepEqual(actual.organisationFacts, expected.organisationFacts, 'Real panel organisation matches current same-actor read')
    const facets = ['homeDiscipline', 'office', 'supervisor', ...(session.me.flags.org_structure ? ['unit'] : [])]
    const organisation = await panel.locator('[aria-labelledby="person-panel-organisation"] dl > div dd').allTextContents()
    assert.equal(organisation.length, facets.length, 'Actual organisation row count')
    facets.forEach((facet, index) => equalText(organisation[index], !actual.organisationFacts ? 'Unavailable' : actual.organisationFacts[facet] ? actual.organisationFacts[facet].name || `Unavailable · ${actual.organisationFacts[facet].id}` : 'Not set', `Organisation ${facet}`))
    if (actual.personFacts) {
      const facts = actual.personFacts, recorded = panel.locator('[aria-labelledby="person-panel-recorded"]'); await recorded.waitFor()
      const fields = await recorded.locator('dl dd').allTextContents()
      const values = [facts.notices.personType || 'Not recorded', facts.notices.startDate ? dateText(facts.notices.startDate, session.me.settings.dateFormat) : 'Not recorded', facts.notices.endDate ? dateText(facts.notices.endDate, session.me.settings.dateFormat) : 'Not recorded']
      assert.deepEqual(fields.map(text), values.map(text))
      sameIds(await recorded.locator('[data-person-certification]').evaluateAll(elements => elements.map(el => el.dataset.personCertification)), facts.certifications.map(record => record.tagId), 'Exact recorded certification IDs, including empty')
      if (!facts.certifications.length) await recorded.getByText('No certification records yet.', { exact: true }).waitFor()
    } else assert.equal(await panel.locator('[aria-labelledby="person-panel-recorded"]').count(), 0)
    const visible = new Set(visibleProjects.map(row => row.id))
    assert.equal(actual.projects.total, expected.projects.total, 'Current membership total agrees across fresh reads')
    let size = actual.projects.pageSize
    if (actual.projects.total > 25) {
      await panel.locator('#person-project-page-size').selectOption('100')
      await session.page.waitForURL(url => url.searchParams.get('personProjectsPageSize') === '100'); size = 100
    }
    const waitMembershipPage = async pageNumber => {
      const ids = expected.projects.items.slice((pageNumber - 1) * size, pageNumber * size).map(row => row.projectId)
      await session.page.waitForFunction(({ personId, ids }) => {
        const node = document.querySelector(`[data-person-panel="${personId}"]`)
        const actual = node ? [...node.querySelectorAll('[data-person-project]')].map(el => el.dataset.personProject).sort() : null
        return actual && JSON.stringify(actual) === JSON.stringify([...ids].sort())
      }, { personId, ids })
      return ids
    }
    const rendered = new Set()
    let currentPage = 1
    for (let pageNumber = 1; pageNumber <= 20; pageNumber++) {
      await waitMembershipPage(pageNumber); currentPage = pageNumber
      const current = await panel.locator('[data-person-project]').evaluateAll(elements => elements.map(el => el.dataset.personProject))
      for (const id of current) { assert.ok(visible.has(id), 'Every shown membership is in the current same-actor visible-project read'); rendered.add(id) }
      const next = panel.getByRole('button', { name: 'Next', exact: true })
      if (await next.isDisabled()) break
      await next.click(); await session.page.waitForURL(url => url.searchParams.get('personProjectsPage') === String(pageNumber + 1))
      if (pageNumber === 20) throw new Error('Actual person pagination exceeds the bounded evidence budget')
    }
    sameIds([...rendered], expected.projects.items.map(row => row.projectId), 'All actual paged UI memberships equal current same-actor read')
    // Reopen the first page if the created membership is on an earlier page.
    if (!(await panel.locator(r.selectors.panelVisibleProject).count())) {
      for (let n = 0; !(await panel.locator(r.selectors.panelVisibleProject).count()) && n < 20; n++) {
        const previous = panel.getByRole('button', { name: 'Previous', exact: true }); assert.equal(await previous.isDisabled(), false)
        await previous.click(); currentPage--; await waitMembershipPage(currentPage)
      }
    }
    const projectRow = panel.locator(r.selectors.panelVisibleProject); await projectRow.waitFor()
    contains(await projectRow.innerText(), r.fixture.finalResponsibilityNote, 'Exact real membership note')
    const href = await projectRow.getByRole('link').getAttribute('href'); assert.equal(href, `/projects/${r.fixture.projectNumber}/team`)
    let capacity
    if (actorName === 'rita') {
      assert.equal(session.me.capabilities.workload, false)
      assert.equal(session.requests.filter(request => new URL(request.url).pathname === '/api/v1/planning/grid').length, 0, 'Rita sends zero planner-grid requests')
      assert.equal(await panel.locator('[data-capacity]').count(), 0, 'Rita sees zero hour/availability figures')
      capacity = { observedUiGridRequests: 0, observedUiCapacityFigures: 0, workload: false }
    } else {
      assert.equal(session.me.capabilities.workload, true)
      const answer = await gridAnswer; assert.equal(answer.status(), 200); const grid = await answer.json()
      const freshGrid = await get(`/planning/grid?personIds=${personId}&from=${actual.week}&weeks=1`, session.actor)
      assert.ok(Array.isArray(grid?.people)); assert.ok(grid.people.every(person => person.id === personId), 'Exact candidate scope only')
      sameIds(grid.people.map(person => person.id), freshGrid.people.map(person => person.id), 'Actual UI and fresh grid return the same candidate cohort')
      const person = grid.people.find(row => row.id === personId)
      if (!person) {
        assert.equal(await panel.locator('[data-capacity]').count(), 0)
        capacity = sourceGap('Lena actual scoped grid omits the person; numeric availability join is unproven', { requestedPersonId: personId, week: actual.week, returnedPersonIds: grid.people.map(row => row.id), receipt: r.joins.lenaCapacity })
      } else {
        const cell = person.cells.find(row => row.week === actual.week); assert.ok(cell)
        assert.equal(typeof person.partial, 'boolean'); assert.equal(typeof cell.provisional, 'boolean')
        const currentPerson = freshGrid.people.find(row => row.id === personId), currentCell = currentPerson.cells.find(row => row.week === actual.week); assert.ok(currentCell)
        for (const field of ['remaining', 'timeAway', 'capacity', 'provisional']) assert.equal(cell[field], currentCell[field], `Canonical ${field} vs current same-actor grid`)
        assert.equal(person.partial, currentPerson.partial)
        const figure = panel.locator('[data-capacity]'); await figure.waitFor()
        const spoken = await figure.innerText()
        if (person.partial) contains(spoken, 'Partial view', 'Partial figures suppressed')
        else if (cell.provisional) equalText(spoken, 'Provisional availability', 'Provisional figures suppressed')
        else {
          const expectedText = `${cell.remaining < 0 ? hours(-cell.remaining) + ' over' : hours(cell.remaining) + ' free'}${cell.timeAway > 0 ? ` · ${hours(cell.timeAway)} away` : ''}`
          contains(spoken, expectedText, 'Canonical visible free/away figure')
        }
        if (person.partial || cell.provisional) assert.equal(/\d(?:[.,]\d+)?\s*h\b/.test(spoken), false, 'No misleading numeric hours when partial/provisional')
        capacity = { status: 'ACTUAL_SCOPED_API_VALUE', week: actual.week, personId, cell, partial: person.partial, visible: spoken }
      }
    }
    const panelScreenshot = await screenshot(session.page, `person-open-${actorName}-${session.width}`)
    const returned = await opened.close()
    return { ...(capacity.status === 'UNPROVEN_SOURCE_GAP' ? { status: 'UNPROVEN_SOURCE_GAP' } : {}), personId, memberId: membership.id, note: membership.responsibility.note,
      actualFacts: actual.personFacts, actualOrganisation: actual.organisationFacts, renderedProjectIds: [...rendered], capacity, panelScreenshot, ...returned, contained: await containment(session.page) }
  })
}
async function prerequisites() {
  assert.equal(process.env.CHECKPOINT22_REAL_BROWSER_EXECUTE, '1', 'Execution guard closed; source preparation is not browser permission')
  assert.equal(process.env.TUESDAY_QA_DATABASE, DATABASE)
  const url = new URL(UI); assert.equal(url.origin, 'http://127.0.0.1:8853'); assert.equal(url.href, 'http://127.0.0.1:8853/')
  assert.equal(UI, url.origin, 'Use the exact loopback preview origin without path/query/fragment/trailing slash')
  receipts = Object.fromEntries(Object.entries(files).map(([name, file]) => [name, load(file)]))
  assert.equal(receipts.timeline.status, 'PASS_API_SETUP_AND_JOINS'); assert.equal(receipts.deliverable.status, 'PASS_API_SETUP'); assert.equal(receipts.person.status, 'PASS_API_ONLY')
  for (const name of ['timeline', 'deliverable', 'person']) { assert.deepEqual(receipts[name].failures, [], `${name} setup must have no failures`); assert.ok(receipts[name].finishedAtUtc) }
  assert.equal(receipts.timeline.target.databaseFromRootEnvironment, DATABASE); assert.equal(receipts.timeline.target.api, API)
  assert.equal(receipts.deliverable.guard.databaseSelector, DATABASE); assert.equal(receipts.deliverable.api, API)
  assert.equal(receipts.person.prerequisites.databaseRequested, DATABASE); assert.equal(receipts.person.prerequisites.api, API)
  assert.equal(receipts.profile.database, DATABASE); assert.equal(receipts.profile.api, 'http://127.0.0.1:5088')
  for (const flag of ['visual_system', 'small_ux', 'project_screens', 'work_screens', 'register_screens', 'people_screens', 'capacity_screens']) assert.equal(receipts.profile.flagsAfter[flag], true)
  for (const who of ['priya', 'marc']) assert.equal(receipts.timeline.fixture.actors[who].id, receipts.person.actors[who].id)
  assert.equal(receipts.deliverable.personas.pm.id, receipts.person.actors.priya.id, 'This joined run requires actual Priya PM setup')
  assert.equal(receipts.deliverable.personas.pm.email.toLowerCase(), receipts.person.actors.priya.email.toLowerCase())
  for (const project of [...receipts.timeline.fixture.projects, receipts.deliverable.fixture.project, { id: receipts.person.fixture.projectId, projectNumber: receipts.person.fixture.projectNumber }]) {
    assert.ok(isGuid(project.id)); assert.ok(project.projectNumber.startsWith('QA22-'), 'Fresh QA22 fixture only')
  }
  const buildFile = path.resolve(process.env.CHECKPOINT22_BUILD_IDENTITY_RECEIPT || path.join(__dirname, 'checkpoint22-runtime/preview-build2-identity.json'))
  assert.equal(path.dirname(buildFile), path.join(__dirname, 'checkpoint22-runtime'), 'Build identity overrides must stay in this checkpoint runtime evidence directory')
  const build = load(buildFile); assert.equal(build.url, UI); assert.match(build.indexSha256 || '', /^[0-9a-f]{64}$/)
  assert.ok(Array.isArray(build.assetsChecked) && build.assetsChecked.length > 0); assert.match(build.manifestSha256 || '', /^[0-9a-f]{64}$/)
  assert.equal(build.sourceManifest, 'web-freeze2.json', 'This joined runner is prepared for root final build2')
  const index = await fetch(UI, { method: 'GET', redirect: 'error', signal: AbortSignal.timeout(15000) }); assert.equal(index.status, 200)
  assert.equal(sha(Buffer.from(await index.arrayBuffer())), build.indexSha256, 'Preview bytes differ from the final root build identity')
  const dist = path.resolve(__dirname, '../../../src/Hub.Api/wwwroot')
  const manifestFile = path.join(dist, '.vite/manifest.json')
  assert.equal(sha(fs.readFileSync(manifestFile)), build.manifestSha256, 'Local manifest differs from the final root build identity')
  const manifest = JSON.parse(fs.readFileSync(manifestFile, 'utf8'))
  const manifestAssets = [...new Set(Object.values(manifest).flatMap(entry => [entry.file, ...(entry.css || []), ...(entry.assets || [])]).map(file => `/${file}`))]
  assert.ok(build.assetsChecked.every(asset => manifestAssets.includes(asset)), 'Every previously verified asset belongs to the final manifest')
  const assetsVerified = []
  for (const asset of manifestAssets) {
    assert.match(asset, /^\/assets\/[A-Za-z0-9_.-]+$/, 'Only final Vite asset filenames are eligible')
    const localFile = path.join(dist, asset.slice(1)), expectedSha256 = sha(fs.readFileSync(localFile))
    const served = await fetch(new URL(asset, UI), { method: 'GET', redirect: 'error', signal: AbortSignal.timeout(15000) })
    assert.equal(served.status, 200, `Final asset unavailable: ${asset}`)
    assert.equal(sha(Buffer.from(await served.arrayBuffer())), expectedSha256, `Served final asset differs: ${asset}`)
    assetsVerified.push({ asset, sha256: expectedSha256 })
  }
  const config = await get('/config'); assert.equal(config.authMode, 'Development')
  const seeds = await get('/api/dev/users'); assert.ok(Array.isArray(seeds)); activeSeeds = new Set(seeds.map(seed => seed.email.toLowerCase()))
  for (const who of ['priya', 'marc', 'lena', 'rita']) assert.equal(seeds.filter(seed => seed.email.toLowerCase() === receipts.person.actors[who].email.toLowerCase()).length, 1, `Existing active ${who} seed required before identity use`)
  report.prerequisites = { receipts: Object.fromEntries(Object.entries(files).map(([name, file]) => [name, { file, sha256: sha(fs.readFileSync(file)), status: receipts[name].status || 'ROOT_RUNTIME_PROFILE' }])),
    buildIdentity: { file: path.resolve(buildFile), indexSha256: build.indexSha256, manifestSha256: build.manifestSha256, assetsVerified }, activeSeedEmailsVerified: ['priya', 'marc', 'lena', 'rita'].map(who => receipts.person.actors[who].email), authMode: config.authMode }
}
async function main() {
  directory = path.join(__dirname, `checkpoint22-real-browser-${new Date().toISOString().replace(/[:.]/g, '-')}-${randomUUID().slice(0, 8)}`)
  fs.mkdirSync(directory, { mode: 0o700 }); persist()
  try {
    await prerequisites(); report.status = 'RUNNING'; persist()
    const { chromium } = require('../../../web/node_modules/playwright')
    browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true })
    for (const width of [1440, 375]) {
      for (const actor of ['priya', 'marc']) await withActor(receipts.person.actors[actor], width, timelineCase)
      await withActor(receipts.person.actors.priya, width, deliverableCase)
      for (const actor of ['lena', 'rita']) await withActor(receipts.person.actors[actor], width, session => personCase(session, actor))
    }
    assert.deepEqual(report.mutations, [], 'Any attempted mutation fails even when the guard aborted it')
    assert.deepEqual(report.failures, [], 'One or more joined browser checks failed')
    assert.deepEqual(report.sourceGaps, [], 'A required join remains unproven because source data/scope is unavailable')
    assert.equal(report.steps.length, 22, 'All planned actor/width joins must execute')
    assert.ok(report.steps.every(item => item.status === 'PASS'), 'Every planned join must pass explicitly')
    report.status = 'PASS_REAL_API_BROWSER_JOINS'
  } catch (error) {
    report.status = 'FAIL_OR_UNPROVEN'; report.failures.push({ fatal: error.stack || String(error) }); process.exitCode = 1
  } finally {
    if (browser) await browser.close()
    report.finishedAtUtc = new Date().toISOString(); persist()
    console.log(JSON.stringify({ status: report.status, report: path.join(directory, 'report.json'), steps: report.steps.length, screenshots: report.screenshots.length, mutations: report.mutations.length, failures: report.failures.length, sourceGaps: report.sourceGaps.length }))
  }
}
if (require.main === module) main().catch(error => { console.error(JSON.stringify({ status: 'FAIL_OR_UNPROVEN', message: error.message, evidenceDirectory: directory || null })); process.exitCode = 1 })
