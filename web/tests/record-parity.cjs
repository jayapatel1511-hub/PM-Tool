// Packet 043 S01 (FR-REC-09): real base-versus-candidate parity for any screen. Both UIs proxy the same
// Development API and database; records are created through the API and nothing is mocked. Ported from the
// spike's harness (043-D3 KILL); `web/scripts/run-record-parity.mjs` builds and serves the two commits.
//   TUESDAY_RECORD_BASE_UI_URL, TUESDAY_RECORD_CANDIDATE_UI_URL, TUESDAY_RECORD_API_URL  local origins
//   TUESDAY_RECORD_SCREENS  space-separated routes, default the Risks register, the Risk panel, the Issues register (plain and
//                           grouped, for the shared DataTable's group rows), the open-roles tab of the Active, Complete
//                           and empty-role projects and the Reviews and Changes registers. {project}, {complete}, {empty} and
//                           {restricted} are synthetic project numbers, {risk} the synthetic risk id
//   TUESDAY_RECORD_FLAGS    flags already merged on those screens: the matrix runs with all off, then all on (§47.6)
//   TUESDAY_RECORD_PARITY_OUT  report path; screenshots of both builds go beside it (evidence, not a pass condition)
//   RECORD_PARITY_SELFCHECK=1  diff logic only; RECORD_PARITY_BROWSER_SELFCHECK=1  identical builds pass, a removed
//                           header fails, a Columns button taken out of the Tab order fails, and a disabled Risk
//                           panel field whose title becomes a permission reason fails
//   After the matrix, when the API declares flag.record_engine_additions, the AC-REC-09 checks run on the candidate with the
//   flag off and then on (the base build has no additions to compare); RECORD_PARITY_ADDITIONS_ONLY=1 runs only those.
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const path = require('node:path')
const { execFileSync } = require('node:child_process')
const { chromium } = require('playwright')

const candidate = process.env.TUESDAY_RECORD_CANDIDATE_UI_URL ?? 'http://127.0.0.1:19502'
const base = process.env.TUESDAY_RECORD_BASE_UI_URL ?? 'http://127.0.0.1:19503'
const apiBase = process.env.TUESDAY_RECORD_API_URL ?? 'http://127.0.0.1:18502'
for (const url of [candidate, base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
const output = process.env.TUESDAY_RECORD_PARITY_OUT ?? path.resolve(__dirname, '../../ci-artifacts/record-parity/report.json')
const runId = process.env.TUESDAY_RECORD_PARITY_RUN ?? crypto.randomUUID()
const SCREENS = (process.env.TUESDAY_RECORD_SCREENS ?? [
  '/projects/{project}/risks', '/projects/{project}/risks?panel=Risk:{risk}', '/projects/{project}/issues', '/projects/{project}/issues?group=owner',
  '/projects/{project}/allocations?tab=roles', '/projects/{complete}/allocations?tab=roles', '/projects/{empty}/allocations?tab=roles',
  '/projects/{project}/reviews', '/projects/{project}/changes',
].join(' ')).split(/\s+/).filter(Boolean)
const FLAGS = (process.env.TUESDAY_RECORD_FLAGS ?? '').split(/[\s,]+/).filter(Boolean)
// Project Manager, Discipline Lead, Team Member, Read Only, and a user outside the Restricted project.
const PERSONAS = { priya: 'Priya Nair', marc: 'Marc Dubois', alex: 'Alex Chen', rita: 'Rita Gomez', jill: 'Jill Martin' }
const ADDITIONS = 'flag.record_engine_additions' // §47.5 FR-REC-12, kept on the restored screens after the KILL (FR-REC-14)
const WIDTHS = [1600, 375]
const launch = () => chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
// Both builds read the same API-created records. Only the local origin is volatile;
// IDs and timestamps are evidence, not noise to erase from a parity report.
const clean = (value) => JSON.parse(JSON.stringify(value, (_, v) => typeof v === 'string' ? v.replace(/^https?:\/\/[^/]+/, '') : v))
const slug = route => route.replace(/\{(\w+)\}/g, '$1').replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '')

function diff(a, b, at = '$', out = []) {
  if (JSON.stringify(a) === JSON.stringify(b)) return out
  if (Array.isArray(a) && Array.isArray(b)) {
    if (a.length !== b.length) out.push(`${at}: length ${a.length} != ${b.length}`)
    for (let i = 0; i < Math.min(a.length, b.length); i++) diff(a[i], b[i], `${at}[${i}]`, out)
    return out
  }
  if (a && b && typeof a === 'object' && typeof b === 'object') {
    for (const key of new Set([...Object.keys(a), ...Object.keys(b)])) {
      if (!(key in a) || !(key in b)) out.push(`${at}.${key}: missing`)
      else diff(a[key], b[key], `${at}.${key}`, out)
    }
    return out
  }
  out.push(`${at}: ${JSON.stringify(a)} != ${JSON.stringify(b)}`)
  return out
}

function selfCheck() {
  const same = { screens: { risks: { controls: [{ tag: 'TH', text: 'Key' }, { tag: 'TH', text: 'Title' }], requests: ['projects/P/risks'] } } }
  assert.deepEqual(diff(same, structuredClone(same)), [])
  const changed = structuredClone(same); changed.screens.risks.controls.pop()
  assert.ok(diff(same, changed).some(x => x.includes('controls')), 'removed header must fail parity')
  assert.equal(expand('/projects/{project}/risks?panel=Risk:{risk}', { project: 'P1', risk: 'r' }), '/projects/P1/risks?panel=Risk:r')
  assert.throws(() => expand('/projects/{nope}', {}), /unknown placeholder/)
  console.log('record parity selfcheck: PASS (identical passes; removed header fails)')
}

function expand(route, ids) {
  return route.replace(/\{(\w+)\}/g, (match, key) => { if (!(key in ids)) throw new Error(`unknown placeholder ${match} in ${route}`); return ids[key] })
}

const auth = who => ({ 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json' })
async function api(pathname, who = 'jordan', options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { ...auth(who), ...(options.headers ?? {}) } })
  if (!response.ok) throw new Error(`${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const setting = (key, value) => api(`admin/settings/${key}`, 'jordan', { method: 'PUT', body: JSON.stringify({ value }) })

async function createProject(reference, prefix, name, members = true) {
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  assert.ok(civil, 'Civil discipline fixture missing')
  const body = {
    projectNumber: `${prefix}${runId.replace(/[^A-Z0-9]/gi, '').slice(-7).toUpperCase()}`, name, clientId: reference.clients[0].id, officeId: reference.offices[0].id,
    disciplines: [{ disciplineId: civil.id, leadUserId: (await api('me', 'marc')).id }],
    members: members ? [{ userId: (await api('me', 'alex')).id, roles: ['TeamMember'], disciplineId: civil.id }] : [],
  }
  const project = await api('projects', 'priya', { method: 'POST', body: JSON.stringify(body) })
  const draft = await api(`projects/${project.id}`, 'priya')
  await api(`projects/${project.id}/transition`, 'priya', { method: 'POST', body: JSON.stringify({ toStatus: 'Active', rowVersion: draft.rowVersion }) })
  const detail = await api(`projects/${project.id}`, 'priya')
  const discipline = detail.disciplines?.[0]?.id ?? detail.projectDisciplines?.[0]?.id
  assert.ok(discipline, `${name}: project discipline missing`)
  return { ...project, discipline }
}

// A deliverable's source with a started review package on revision A and a draft change notice for revision B, so the
// Reviews and Changes registers each list a row.
async function coordination(reference, project) {
  const [alex, marc, priya] = await Promise.all(['alex', 'marc', 'priya'].map(who => api('me', who).then(x => x.id)))
  const type = reference.deliverableTypes.find(x => x.name === 'Drawing Package')
  assert.ok(type, 'Drawing Package deliverable type fixture missing')
  const root = `projects/${project.id}`, post = (who, pathname, body) => api(`${root}/${pathname}`, who, { method: 'POST', body: JSON.stringify({ requestId: crypto.randomUUID(), ...body }) })
  const deliverable = await post('marc', 'deliverables', { name: 'Synthetic parity survey', projectDisciplineId: project.discipline, deliverableTypeId: type.id, ownerId: alex, revision: 'A', requiresReview: false })
  const register = async (revision, old) => {
    const options = await api(`${root}/changes/options`, 'alex')
    return post('alex', 'source-revisions', { deliverableId: deliverable.id, deliverableRowVersion: options.deliverables.find(x => x.id === deliverable.id).rowVersion, projectDisciplineId: project.discipline, ownerId: alex,
      sourceSystem: 'Deliverable', externalIdentifier: 'survey', title: 'Synthetic parity survey', revision, url: `https://example.test/${revision}.pdf`, issuer: 'Survey team', scope: 'East corridor',
      ...(old ? { supersedesId: old.id, headRowVersion: options.heads[0].rowVersion, description: 'Changed corridor alignment', effectiveDate: '2026-09-14', assessmentDueDate: '2026-09-18' } : {}) })
  }
  const first = await register('A')
  const review = await post('marc', 'reviews', { title: 'Synthetic parity review', purpose: 'Parity fixture', projectDisciplineId: project.discipline, coordinatorId: marc, sourceRevisionIds: [first.id],
    assignments: [{ projectDisciplineId: project.discipline, reviewerId: priya, dueDate: '2026-09-18' }], requiredForIssue: true })
  const draft = await api(`${root}/reviews/${review.id}`, 'marc')
  await post('marc', `reviews/${review.id}/action`, { rowVersion: draft.package.rowVersion, action: 'start', reason: 'Parity fixture' })
  await register('B', first)
}

// An Active project with a risk and an open role, a Complete project whose open role needs a correction reason,
// an Active project with no open roles, and a Restricted project the outsider cannot see.
async function fixtures() {
  const reference = await api('reference')
  const role = (project, label) => api(`projects/${project.id}/open-roles`, 'priya', { method: 'POST', body: JSON.stringify({
    roleLabel: label, fromDate: '2026-10-19', throughDate: '2026-10-23', plannedHours: 20, projectDisciplineId: project.discipline,
  }) })
  const project = await createProject(reference, 'P', 'Synthetic record parity project')
  const risk = await api(`projects/${project.id}/risks`, 'priya', { method: 'POST', body: JSON.stringify({
    title: 'Synthetic parity risk', description: 'Parity fixture', probability: 2, impact: 2, mitigation: 'Monitor', triggerIndicator: 'Review',
  }) })
  await api(`projects/${project.id}/issues`, 'priya', { method: 'POST', body: JSON.stringify({
    title: 'Synthetic parity issue', description: 'Parity fixture', severity: 'Medium', issueType: 'General', locations: [], documents: [],
  }) })
  const openRole = await role(project, 'Synthetic parity role')
  await coordination(reference, project)
  const complete = await createProject(reference, 'C', 'Synthetic complete parity project')
  await role(complete, 'Synthetic complete role')
  const beforeComplete = await api(`projects/${complete.id}`, 'priya')
  await api(`projects/${complete.id}/transition`, 'priya', { method: 'POST', body: JSON.stringify({
    toStatus: 'Complete', reason: 'Synthetic parity closeout', closeout: { tasks: 'cancel' }, rowVersion: beforeComplete.rowVersion,
  }) })
  const empty = await createProject(reference, 'E', 'Synthetic empty-role parity project')
  await setting('restricted_projects_enabled', true)
  const restricted = await createProject(reference, 'R', 'Synthetic restricted parity project', false)
  const restrictedDraft = await api(`projects/${restricted.id}`, 'priya')
  await api(`projects/${restricted.id}`, 'priya', { method: 'PATCH', headers: { 'If-Match': `"${restrictedDraft.rowVersion}"` }, body: JSON.stringify({ visibility: 'Restricted', rowVersion: restrictedDraft.rowVersion }) })
  return { project: project.projectNumber, projectId: project.id, risk: risk.id, openRole: openRole.id, complete: complete.projectNumber, empty: empty.projectNumber,
    restricted: restricted.projectNumber, restrictedId: restricted.id }
}

// An export download as rows: the CSV lines, or the first sheet's cells (ZIP metadata and file names are not record data).
async function exportRows(file) {
  const bytes = await fs.readFile(await file.path())
  const format = path.extname(file.suggestedFilename()).toLowerCase()
  const rows = format === '.xlsx' ? JSON.parse(execFileSync('python3', ['-c', `import io,json,sys,zipfile,xml.etree.ElementTree as E
z=zipfile.ZipFile(io.BytesIO(sys.stdin.buffer.read()))
n={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
root=E.fromstring(z.read('xl/worksheets/sheet1.xml'))
print(json.dumps([[''.join(c.itertext()) for c in row.findall('s:c',n)] for row in root.findall('.//s:sheetData/s:row',n)]))`], { input: bytes }).toString()) : bytes.toString('utf8').replace(/\r\n/g, '\n')
  return { format, rows }
}

async function browserSnapshot(origin, who, width, ids, { removeHeader = false, untabColumns = false, retitle = false, shots = true } = {}) {
  const browser = await launch()
  const createdViewIds = []
  try {
    const context = await browser.newContext({ viewport: { width, height: width < 500 ? 900 : 1000 }, reducedMotion: 'reduce', acceptDownloads: true })
    // Negative control: the Columns button stays clickable and focusable by script but leaves the Tab order.
    if (untabColumns) await context.addInitScript(() => new MutationObserver(() => {
      for (const b of document.querySelectorAll('main button')) if (b.textContent.trim() === 'Columns' && b.tabIndex !== -1) b.tabIndex = -1
    }).observe(document, { childList: true, subtree: true }))
    // Negative control: what the spike's KILL measured, a disabled field's label title replaced by a permission reason.
    if (retitle) await context.addInitScript(() => new MutationObserver(() => {
      for (const e of document.querySelectorAll('[role=dialog] div[title="Probability"]')) e.title = 'Only the owner or the Project Manager can do this.'
    }).observe(document, { childList: true, subtree: true }))
    const page = await context.newPage()
    const gets = [], errors = [], wrongPersona = [], pending = new Set()
    let signedIn = false
    page.on('request', request => {
      const url = request.url()
      if (!url.includes('/api/v1/')) return
      if (request.method() === 'GET') gets.push(url.split('/api/v1/')[1])
      if (!/\/api\/v1\/events(\?|$)/.test(url)) pending.add(request) // the live stream never finishes
      if (signedIn && !url.endsWith('/config') && request.headers()['x-dev-user'] !== `${who}@hub.test`) wrongPersona.push(url)
    })
    for (const event of ['requestfinished', 'requestfailed']) page.on(event, request => pending.delete(request))
    page.on('pageerror', error => errors.push(error.message.slice(0, 180)))
    // Waits for the API to go quiet (400 ms without a pending request, at most 10 s) instead of fixed sleeps.
    const settle = async () => { for (let quiet = 0, i = 0; quiet < 8 && i < 200; i++) { quiet = pending.size ? 0 : quiet + 1; await page.waitForTimeout(50) } }
    const here = () => { const url = new URL(page.url()); return url.pathname + url.search }
    const visible = locator => locator.isVisible().catch(() => false)
    const dialog = () => page.locator('[role=dialog]').filter({ visible: true }).last()
    const dialogOpen = async () => await page.locator('[role=dialog]').filter({ visible: true }).count() > 0
    const go = async url => {
      await settle(); gets.length = 0 // a request still leaving the previous page is not this screen's
      await page.goto(`${origin}${url}`, { waitUntil: 'domcontentloaded', timeout: 60000 })
      await page.locator('main').first().waitFor({ timeout: 30000 }); await settle()
    }
    const tree = async () => (await dialogOpen() ? dialog() : page.locator('main').first()).ariaSnapshot()
    // Headings, caption, column headers, link and button names, disabled states, every title (a disabled inline field is a
    // titled div, which the aria tree leaves out), aria-sort, aria-current.
    const controls = () => page.evaluate(() => {
      const root = [...document.querySelectorAll('[role=dialog]')].filter(e => e.getClientRects().length).pop() ?? document.querySelector('main')
      return [...root.querySelectorAll('h1,h2,h3,caption,th,a,button,[role=tab],[role=menuitem],[role=option],[role=alert],[title]')].filter(e => e.getClientRects().length)
        .map(e => ({ tag: e.tagName, text: (e.getAttribute('aria-label') || e.textContent || '').replace(/\s+/g, ' ').trim(), disabled: e.disabled || e.getAttribute('aria-disabled') === 'true', title: e.getAttribute('title'), sort: e.getAttribute('aria-sort'), current: e.getAttribute('aria-current'), selected: e.getAttribute('aria-selected') }))
    })
    // Each field's value, editability and the reason given for a disabled one.
    const fields = () => page.evaluate(() => {
      const root = [...document.querySelectorAll('[role=dialog]')].filter(e => e.getClientRects().length).pop() ?? document.querySelector('main')
      return [...root.querySelectorAll('input,textarea,select')].filter(e => e.getClientRects().length)
        .map(e => ({ tag: e.tagName, type: e.getAttribute('type'), label: e.getAttribute('aria-label') || e.labels?.[0]?.textContent?.trim() || e.getAttribute('placeholder'), value: e.value, disabled: e.disabled, readOnly: !!e.readOnly, required: e.required, minLength: e.minLength ?? null, title: e.getAttribute('title') }))
    })
    const rows = async () => { const body = page.locator('main tbody'); return (await body.count() ? body : page.locator('main')).allInnerTexts() }
    const scan = async () => {
      await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
      return page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations.filter(x => ['serious', 'critical'].includes(x.impact)).map(x => x.id))
    }
    const focused = () => page.evaluate(() => { const e = document.activeElement; return e && e !== document.body ? `${e.tagName} ${(e.getAttribute('aria-label') || e.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 60)}` : null })

    // Every filter field: the URL it writes and the rows returned, opened and closed by keyboard.
    const filterEvidence = async url => {
      const out = { search: null, popover: null, quick: [] }
      const search = page.locator('main input[type=search]').filter({ visible: true }).first()
      if (await visible(search)) {
        await search.fill('Synthetic'); await page.waitForTimeout(400); await settle()
        out.search = { url: here(), rows: await rows() }
        await search.fill(''); await page.waitForTimeout(400); await settle()
      }
      const trigger = page.locator('main button').filter({ hasText: /^Filters/ }).first()
      if (await visible(trigger)) {
        await trigger.focus(); await page.keyboard.press('Enter'); await dialog().waitFor()
        const changes = []
        const selects = await dialog().locator('select').evaluateAll(es => es.map(e => ({ id: e.id, value: e.value, option: [...e.options].find(o => o.value && o.value !== e.value)?.value })))
        for (const [i, select] of selects.entries()) {
          if (!select.option) continue
          await dialog().locator('select').nth(i).selectOption(select.option); await settle()
          changes.push({ field: select.id || i, value: select.option, url: here(), rows: await rows() })
          await dialog().locator('select').nth(i).selectOption(select.value); await settle()
        }
        const popoverFields = await fields()
        await page.keyboard.press('Escape'); await settle()
        out.popover = { fields: popoverFields, changes, closed: !await dialogOpen(), focusReturned: await trigger.evaluate(e => document.activeElement === e) }
      }
      const chips = await page.locator('main button[aria-pressed]').filter({ visible: true }).count()
      for (let i = 0; i < chips; i++) {
        const chip = page.locator('main button[aria-pressed]').filter({ visible: true }).nth(i)
        const name = (await chip.textContent())?.trim()
        await chip.click(); await settle()
        out.quick.push({ name, url: here(), rows: await rows() })
        await go(url)
      }
      return out
    }
    // Each column sort, with the aria-sort state and row order it produces.
    const sortEvidence = async url => {
      const out = []
      const headers = await page.locator('main th button').filter({ visible: true }).allTextContents()
      for (const [i, header] of headers.entries()) {
        await page.locator('main th button').filter({ visible: true }).nth(i).click(); await settle()
        out.push({ header: header.trim(), url: here(), state: await page.locator('main th[aria-sort]').evaluateAll(es => es.map(e => [e.textContent?.trim(), e.getAttribute('aria-sort')])), rows: await rows() })
      }
      if (out.length) await go(url)
      return out
    }
    // The column chooser: arrow keys move in the menu, each column toggles, Esc returns focus.
    const columnEvidence = async () => {
      const trigger = page.locator('main button').filter({ hasText: /^Columns$/ }).first()
      if (!await visible(trigger)) return null
      await trigger.focus(); await page.keyboard.press('Enter')
      const menu = page.getByRole('menu'); await menu.waitFor()
      const items = menu.getByRole('menuitemcheckbox'), choices = []
      await page.waitForFunction(() => !!document.activeElement?.matches('[role^=menuitem]'))
      const firstFocus = await focused(); await page.keyboard.press('ArrowDown')
      await page.waitForFunction(before => document.activeElement?.textContent !== before, await page.evaluate(() => document.activeElement?.textContent), { timeout: 2000 }).catch(() => {})
      const arrowFocus = await focused()
      for (let i = 0; i < await items.count(); i++) {
        const checked = await items.nth(i).getAttribute('aria-checked')
        await items.nth(i).click(); await settle()
        choices.push({ choice: (await items.nth(i).textContent())?.trim(), checked: await items.nth(i).getAttribute('aria-checked'), headers: await page.locator('main th').allTextContents() })
        await items.nth(i).click(); await settle()
        assert.equal(await items.nth(i).getAttribute('aria-checked'), checked, 'column chooser must restore each column')
      }
      await page.keyboard.press('Escape'); await settle()
      return { firstFocus, arrowFocus, choices, focusReturned: await trigger.evaluate(e => document.activeElement === e) }
    }
    // The saved-view menu: saving the current view and applying it. Views made here are deleted afterwards.
    const viewEvidence = async url => {
      const trigger = page.locator('main button[aria-haspopup=menu]').filter({ hasText: /^(Views|Parity )/ }).first()
      if (!await visible(trigger)) return null
      const menu = page.locator('[role=menu]').filter({ visible: true }).first()
      const menuItems = async () => (await menu.locator('[role^=menuitem]').allTextContents()).map(x => x.trim()).filter(Boolean)
      await trigger.click(); await menu.waitFor()
      let items = await menuItems(), saved = false, applied = false
      const save = menu.getByText(/^Save current view/i).first()
      if (await visible(save)) {
        const name = `Parity ${runId.slice(0, 8)}-${who}-${width}`
        await save.click(); await dialog().waitFor(); await dialog().locator('#view-name').fill(name)
        const created = page.waitForResponse(response => response.request().method() === 'POST' && /\/api\/v1\/views$/.test(response.url()), { timeout: 10000 }).catch(() => null)
        await dialog().getByRole('button', { name: /^Save(?: view)?$/i }).click()
        const response = await created
        if (response?.ok()) { const body = await response.json(); if (body.id) createdViewIds.push(body.id) }
        saved = !!response?.ok(); await settle()
        await go(url); await trigger.click(); await menu.waitFor()
        items = await menuItems()
        const savedItem = menu.getByText(name, { exact: true }).first()
        if (await visible(savedItem)) { await savedItem.click(); await settle(); applied = new URL(page.url()).searchParams.has('view') } else await page.keyboard.press('Escape')
        await go(url)
      } else await page.keyboard.press('Escape')
      return { items, saved, applied }
    }
    // The export file in CSV and Excel: header row and data rows (ZIP metadata and file names are not record data).
    const exportEvidence = async () => {
      const trigger = page.locator('main button').filter({ hasText: /^Export/ }).first()
      if (!await visible(trigger)) return null
      const menu = page.locator('[role=menu]').filter({ visible: true }).first()
      await trigger.click(); await menu.waitFor()
      const items = (await menu.locator('[role^=menuitem]').allTextContents()).map(x => x.trim()).filter(Boolean)
      await page.keyboard.press('Escape')
      const downloads = []
      for (const item of items.filter(x => /csv|excel/i.test(x))) {
        await trigger.click(); await menu.waitFor()
        const download = page.waitForEvent('download', { timeout: 10000 }).catch(() => null)
        await menu.getByText(item, { exact: true }).first().click()
        const file = await download
        if (!file) { downloads.push({ item, rows: null }); continue }
        downloads.push({ item, ...await exportRows(file) })
      }
      return { items, downloads }
    }
    // The screen's create action (Raise risk, Add open role): the form it opens, such as the required correction reason
    // on a Complete project, then Esc without saving.
    const createEvidence = async () => {
      const trigger = page.locator('main button:enabled').filter({ visible: true, hasText: /^(Add|Raise|New) / }).first()
      if (!await visible(trigger)) return null
      await trigger.click()
      if (!await dialog().waitFor({ timeout: 5000 }).then(() => true, () => false)) return { opened: false }
      const form = { opened: true, tree: await tree(), fields: await fields() }
      await page.keyboard.press('Escape'); await settle()
      return { ...form, closed: !await dialogOpen() }
    }
    // The keyboard path: the screen's whole Tab order from the main landmark (the skip link's target) until focus
    // leaves it, so the toolbar and table controls are in it; then Enter on the first row control and Esc back.
    const keyboardEvidence = async url => {
      await go(url)
      const order = []
      await page.locator('main').first().focus()
      // ponytail: 150 stops guards a focus trap; raise it if a parity screen ever has more tabbable controls.
      for (let i = 0; i < 150; i++) {
        await page.keyboard.press('Tab')
        if (!await page.evaluate(() => !!document.activeElement?.closest('main'))) break
        order.push(await focused())
      }
      const row = page.locator('main tbody').locator('button,a').filter({ visible: true }).first()
      if (!await visible(row)) return { order, row: null }
      await row.focus(); await page.keyboard.press('Enter'); await settle()
      const opened = { url: here(), dialog: await dialogOpen(), tree: await dialogOpen() ? await tree() : null }
      if (opened.dialog) {
        await page.keyboard.press('Escape'); await settle()
        Object.assign(opened, { closed: !await dialogOpen(), focusReturned: await row.evaluate(e => document.activeElement === e).catch(() => false) })
      }
      await go(url)
      return { order, row: opened }
    }
    // A panel opened by its route: each tab's content, then Esc closes it.
    const panelEvidence = async () => {
      const tabs = [], tabList = dialog().getByRole('tab')
      for (let i = 0; i < await tabList.count(); i++) {
        await tabList.nth(i).click(); await settle()
        tabs.push({ tab: (await tabList.nth(i).textContent())?.trim(), tree: await tree(), controls: await controls(), fields: await fields() })
      }
      await page.keyboard.press('Escape'); await settle()
      return { tabs, closed: !await dialogOpen(), url: here() }
    }

    await page.goto(`${origin}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
    await page.getByRole('button', { name: new RegExp(PERSONAS[who], 'i') }).click()
    await page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 30000 })
    signedIn = true; await settle()
    const identity = await page.evaluate(async () => (await fetch('/api/v1/me', { headers: { 'X-Dev-User': sessionStorage.getItem('hub.devUser') ?? '' } })).json())
    assert.equal(identity.email, `${who}@hub.test`, 'Development picker must select the requested persona')
    const snapshot = { identity: identity.email, screens: {}, problems: [] }
    for (const route of SCREENS) {
      const url = expand(route, ids)
      await go(url)
      if (removeHeader) await page.locator('main th').first().evaluate(element => element.remove()).catch(() => {})
      const state = { url: here(), tree: await tree(), controls: await controls(), fields: await fields(), requests: [...new Set(gets.filter(x => !x.startsWith('meta')))].sort(), axe: await scan() }
      if (shots) await page.screenshot({ path: path.join(path.dirname(output), `${origin === base ? 'base' : 'candidate'}-${who}-${width}-${slug(route)}.png`), fullPage: true })
      if (await dialogOpen()) state.panel = await panelEvidence()
      else Object.assign(state, { filters: await filterEvidence(url), sort: await sortEvidence(url), columns: await columnEvidence(), views: await viewEvidence(url), exports: await exportEvidence(), create: await createEvidence(), keyboard: await keyboardEvidence(url) })
      if (state.axe.length) snapshot.problems.push(`${route}: serious/critical accessibility issues ${state.axe.join(', ')}`)
      if (who === 'jill' && route.includes('{project}')) {
        await go(expand(route, { ...ids, project: ids.restricted }))
        state.restricted = { url: here(), tree: await tree() }
        if (state.restricted.tree.includes(ids.restricted)) snapshot.problems.push(`${route}: restricted outsider saw ${ids.restricted}`)
      }
      snapshot.screens[route] = state
    }
    if (errors.length) snapshot.problems.push(`browser errors: ${errors.join(' | ')}`)
    if (wrongPersona.length) snapshot.problems.push(`requests without the persona header: ${wrongPersona.join(', ')}`)
    return clean(snapshot)
  } finally {
    try { for (const id of createdViewIds) await api(`views/${id}`, who, { method: 'DELETE' }) } finally { await browser.close() }
  }
}

// AC-REC-09 on the candidate. Off: Risks has no Views menu; open roles have no sort buttons, no panel link and no open-roles
// export entries; ?panel=OpenRole:<id> opens nothing. On: a Risks saved view re-applies its filters, sort and columns; open-role
// headers sort with aria-sort; the role label opens a panel whose History lists the role's activity entries; the Export menu's
// open-roles entries download the roles with the list's columns; a viewer who cannot view the project gets none of them.
async function additionsCheck(ids) {
  const failures = [], check = (ok, message) => { if (!ok) failures.push(message) }
  const browser = await launch()
  const createdViews = []
  let step = 'off' // names the check that stopped, if one throws
  try {
    const signIn = async who => {
      const page = await (await browser.newContext({ viewport: { width: 1600, height: 1000 }, reducedMotion: 'reduce', acceptDownloads: true })).newPage()
      page.on('pageerror', error => failures.push(`${who} browser error: ${error.message.slice(0, 180)}`))
      await page.goto(`${candidate}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
      await page.getByRole('button', { name: new RegExp(PERSONAS[who], 'i') }).click()
      await page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 30000 })
      return page
    }
    const go = async (page, url, ready = 'main') => {
      await page.goto(`${candidate}${url}`, { waitUntil: 'domcontentloaded', timeout: 60000 })
      await page.locator(ready).first().waitFor({ timeout: 30000 }); await page.waitForLoadState('networkidle').catch(() => {})
    }
    // A closing menu stays in the DOM while it animates out, so only the open one is read or clicked.
    const openMenu = page => page.locator('[role=menu][data-state=open]')
    const menuItems = async (page, trigger) => {
      await trigger.click(); const menu = openMenu(page); await menu.waitFor()
      const items = (await menu.locator('[role^=menuitem]').allTextContents()).map(x => x.trim()).filter(Boolean)
      await page.keyboard.press('Escape'); await menu.waitFor({ state: 'detached' }); return items
    }
    const viewsButton = page => page.locator('main button[aria-haspopup=menu]').filter({ hasText: /^Views$/ })
    const exportButton = page => page.locator('main button').filter({ hasText: /^Export$/ }).first()
    const risks = `/projects/${ids.project}/risks`, roles = `/projects/${ids.project}/allocations?tab=roles`
    const roleLink = page => page.locator('main tbody button').filter({ hasText: /^Synthetic parity role$/ })
    const listStatus = async (who, id) => (await fetch(`${apiBase}/api/v1/projects/${id}/open-roles`, { headers: auth(who) })).status
    const exportStatus = async (who, id) => (await fetch(`${apiBase}/api/v1/lists/open-roles/export?projectId=${id}&format=csv`, { headers: auth(who) })).status

    await setting(ADDITIONS, false)
    let page = await signIn('priya')
    await go(page, risks, 'main table')
    check(await viewsButton(page).count() === 0, 'off: the Risks toolbar shows a Views menu')
    await go(page, roles, 'main table')
    check(await page.locator('main th button').count() === 0 && await page.locator('main th[aria-sort]').count() === 0, 'off: open-role headers sort')
    check(await roleLink(page).count() === 0, 'off: the role label opens a panel')
    check(!(await menuItems(page, exportButton(page))).some(x => x.startsWith('Open roles')), 'off: the Export menu offers open roles')
    check(await exportStatus('priya', ids.projectId) === 404, 'off: the open-roles export answers')
    await go(page, `${roles}&panel=OpenRole:${ids.openRole}`, 'main table')
    check(await page.locator('[role=dialog]').count() === 0, 'off: ?panel=OpenRole opens a panel')

    await setting(ADDITIONS, true)
    step = 'on: Risks saved view'
    page = await signIn('priya')
    // A saved view keeps the filters, the sort and the columns, and applying it from the plain register restores them.
    await go(page, `${risks}?status=Open,Monitoring&severity=Medium&sort=severity:desc&cols=severity,status`, 'main table')
    await viewsButton(page).click()
    await openMenu(page).getByText(/^Save current view/).click()
    const name = `Parity additions ${runId.slice(0, 8)}`
    await page.locator('[role=dialog] #view-name').fill(name)
    const created = page.waitForResponse(r => r.request().method() === 'POST' && /\/api\/v1\/views$/.test(r.url()))
    await page.locator('[role=dialog]').getByRole('button', { name: /^Save$/ }).click()
    const saved = await created
    if (saved.ok()) createdViews.push((await saved.json()).id); else failures.push(`on: saving a Risks view answered ${saved.status()}`)
    await go(page, risks, 'main table')
    await viewsButton(page).click()
    await openMenu(page).getByText(name, { exact: true }).click()
    await page.waitForFunction(() => new URL(location.href).searchParams.has('view'))
    const applied = new URL(page.url()).searchParams
    check(applied.get('status') === 'Open,Monitoring' && applied.get('severity') === 'Medium' && applied.get('sort') === 'severity:desc', `on: the view re-applied ${applied}`)
    await page.locator('main th[aria-sort=descending]').waitFor({ timeout: 10000 }).catch(() => {}) // the filtered rows have loaded
    const headers = (await page.locator('main th').allTextContents()).map(x => x.trim())
    check(JSON.stringify(headers) === JSON.stringify(['Key', 'Title', 'Severity', 'Status']), `on: the view's columns are ${headers.join(', ')}`)
    check(await page.locator('main th[aria-sort=descending]').textContent().then(x => x.trim(), () => null) === 'Severity', 'on: the view did not re-apply its sort')
    check((await page.locator('main tbody').allInnerTexts()).join(' ').includes('Synthetic parity risk'), 'on: the view lost the matching risk')

    // Open-role headers sort with aria-sort and keep it in the URL.
    step = 'on: open-role sorting'
    await go(page, roles, 'main table')
    const role = page.locator('main th').filter({ hasText: /^Role$/ })
    check(await role.getAttribute('aria-sort') === 'none', 'on: the Role header is not sortable')
    await role.locator('button').click(); await page.waitForFunction(() => document.querySelector('main th[aria-sort=ascending]'))
    check(await role.getAttribute('aria-sort') === 'ascending' && new URL(page.url()).searchParams.get('sort') === 'role', 'on: Role does not sort ascending')
    await role.locator('button').click(); await page.waitForFunction(() => document.querySelector('main th[aria-sort=descending]'))
    check(await role.getAttribute('aria-sort') === 'descending', 'on: Role does not sort descending')

    // The role label opens the read-only panel; its History lists the role's activity entries.
    step = 'on: open-role panel'
    await go(page, roles, 'main table')
    await roleLink(page).click()
    const dialog = page.locator('[role=dialog]').filter({ visible: true })
    await dialog.getByRole('heading', { name: 'Synthetic parity role' }).waitFor({ timeout: 10000 }).catch(() => failures.push('on: the role label opened no panel'))
    check(new URL(page.url()).searchParams.get('panel') === `OpenRole:${ids.openRole}`, 'on: the panel is not in the URL')
    check(await dialog.getByRole('tab', { name: 'History' }).getAttribute('aria-selected').catch(() => null) === 'true', 'on: the panel has no History tab')
    check(await dialog.locator('input,textarea,select').count() === 0, 'on: the open-role panel is editable')
    const activity = (await api(`items/OpenRole/${ids.openRole}/activity?pageSize=100`, 'priya')).items
    await dialog.locator('ol > li').first().waitFor({ timeout: 10000 }).catch(() => {})
    check(activity.length > 0 && await dialog.locator('ol > li').count() === activity.length, `on: History lists ${await dialog.locator('ol > li').count()} of ${activity.length} entries`)
    await page.keyboard.press('Escape'); await dialog.waitFor({ state: 'detached' }).catch(() => {})

    // The Export menu's open-roles entries come after the page's own and download the roles with the list's columns.
    step = 'on: open-role export'
    step = 'on: open-role export, reading the Export menu'
    const items = await menuItems(page, exportButton(page))
    check(JSON.stringify(items) === JSON.stringify(['Excel (.xlsx)', 'CSV (.csv)', 'Open roles · Excel (.xlsx)', 'Open roles · CSV (.csv)']), `on: the Export menu is ${items.join(' | ')}`)
    for (const item of items.slice(2)) {
      step = `on: open-role export, opening the menu for ${item}`
      await exportButton(page).click()
      const menu = openMenu(page); await menu.waitFor()
      const download = page.waitForEvent('download', { timeout: 10000 }).catch(() => null)
      await menu.getByText(item, { exact: true }).click()
      const file = await download
      await menu.waitFor({ state: 'detached' })
      if (!file) { failures.push(`on: ${item} downloaded nothing`); continue }
      const { format, rows } = await exportRows(file)
      const table = format === '.xlsx' ? rows : rows.trim().replace(/^\uFEFF/, '').split('\n').map(line => line.split(','))
      check(table[0]?.join(',') === 'Role,Discipline,From,Through,Hours,Status,Filled by,Note', `on: ${item} header is ${table[0]}`)
      check(table.length === 2 && table[1][0] === 'Synthetic parity role', `on: ${item} rows are ${JSON.stringify(table.slice(1))}`)
    }

    // FR-ROLE-05: a viewer who cannot view the project gets none of them, and the export answers as the list does.
    step = 'on: outsider'
    page = await signIn('jill')
    await go(page, `/projects/${ids.restricted}/allocations?tab=roles`)
    check(await page.locator('main table').count() === 0 && await exportButton(page).count() === 0, 'on: the outsider sees the restricted open roles')
    check(await exportStatus('jill', ids.restrictedId) === await listStatus('jill', ids.restrictedId), 'on: the export and the list answer the outsider differently')
    check((await fetch(`${apiBase}/api/v1/views?listType=risks&projectId=${ids.restrictedId}`, { headers: auth('jill') })).status === 404, 'on: the outsider reads restricted Risks views')
  } catch (error) {
    failures.push(`${step} stopped: ${error.message.split('\n')[0]}`)
  } finally {
    try { for (const id of createdViews) await api(`views/${id}`, 'priya', { method: 'DELETE' }) } finally { await browser.close() }
  }
  return failures
}

;(async () => {
  selfCheck()
  if (process.env.RECORD_PARITY_SELFCHECK === '1') return
  assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'set TUESDAY_SYNTHETIC_API=1 for the disposable Development API')
  const config = await (await fetch(`${apiBase}/api/v1/config`)).json()
  assert.equal(config.authMode, 'Development', 'record parity requires Development authentication')
  const settings = await api('admin/settings')
  const additions = settings.some(x => x.key === ADDITIONS)
  const original = Object.fromEntries(['restricted_projects_enabled', ...FLAGS, ...(additions ? [ADDITIONS] : [])].map(key => [key, settings.find(x => x.key === key)?.value]))
  for (const [key, value] of Object.entries(original)) assert.equal(typeof value, 'boolean', `setting ${key} missing`)
  await fs.mkdir(path.dirname(output), { recursive: true })
  try {
    const ids = await fixtures()
    for (const origin of [base, candidate]) {
      const response = await fetch(`${origin}/api/v1/projects/${ids.projectId}`, { headers: auth('priya') })
      assert.ok(response.ok, `${origin} must proxy the owned synthetic API`)
      assert.equal((await response.json()).id, ids.projectId, `${origin} must use the same synthetic database`)
    }
    if (process.env.RECORD_PARITY_BROWSER_SELFCHECK === '1') {
      for (const width of WIDTHS) {
        const same = await browserSnapshot(base, 'priya', width, ids, { shots: false })
        const repeat = await browserSnapshot(base, 'priya', width, ids, { shots: false })
        assert.deepEqual(diff(same, repeat), [], `identical builds must have identical state at ${width}px`)
        if (width !== 1600) continue
        const changed = await browserSnapshot(base, 'priya', width, ids, { removeHeader: true, shots: false })
        assert.ok(diff(same, changed).some(x => x.includes('controls')), 'removed header must fail browser parity')
        const untabbed = await browserSnapshot(base, 'priya', width, ids, { untabColumns: true, shots: false })
        assert.ok(diff(same, untabbed).some(x => x.includes('keyboard.order')), 'a toolbar control out of the Tab order must fail browser parity')
        // The Discipline Lead sees the Risk panel's fields disabled, as the spike's eight failing pairs did.
        if (!SCREENS.some(x => x.includes('panel=Risk:'))) continue
        const lead = await browserSnapshot(base, 'marc', width, ids, { shots: false })
        const retitled = await browserSnapshot(base, 'marc', width, ids, { retitle: true, shots: false })
        assert.ok(diff(lead, retitled).some(x => /panel=Risk:.*\.title: "Probability"/.test(x)), 'a changed disabled-field title must fail browser parity')
      }
      console.log(`record parity browser selfcheck: PASS (identical builds pass at ${WIDTHS.join(' and ')} px; removed header, untabbed Columns and a retitled disabled field fail) on ${SCREENS.join(' ')}`)
      return
    }
    const report = { candidate, base, apiBase, screens: SCREENS, flags: FLAGS, ids, runs: {}, at: new Date().toISOString() }
    for (const flagged of process.env.RECORD_PARITY_ADDITIONS_ONLY === '1' ? [] : FLAGS.length ? [false, true] : [false]) {
      for (const key of FLAGS) await setting(key, flagged)
      for (const who of Object.keys(PERSONAS)) for (const width of WIDTHS) {
        const key = `${FLAGS.length ? `flags ${flagged ? 'on' : 'off'} ` : ''}${who}/${width}`
        const before = await browserSnapshot(base, who, width, ids)
        const after = await browserSnapshot(candidate, who, width, ids)
        const differences = diff(before, after)
        const problems = [...before.problems.map(x => `base ${x}`), ...after.problems.map(x => `candidate ${x}`)]
        report.runs[key] = { pass: !differences.length && !problems.length, differences, problems, base: before, candidate: after }
        console.log(`${report.runs[key].pass ? 'PASS' : 'FAIL'} ${key}${report.runs[key].pass ? '' : ` (${[...problems, ...differences].slice(0, 4).join('; ')})`}`)
      }
    }
    if (additions) {
      report.additions = await additionsCheck(ids)
      console.log(`${report.additions.length ? 'FAIL' : 'PASS'} AC-REC-09 ${ADDITIONS} off and on${report.additions.length ? ` (${report.additions.slice(0, 4).join('; ')})` : ''}`)
    }
    await fs.writeFile(output, JSON.stringify(report, null, 2))
    const failed = Object.values(report.runs).filter(x => !x.pass).length + (report.additions?.length ? 1 : 0)
    console.log(JSON.stringify({ result: failed ? 'FAIL' : 'PASS', checked: Object.keys(report.runs).length + (additions ? 1 : 0), failed, screens: SCREENS, report: output }))
    if (failed) process.exitCode = 1
  } finally {
    for (const [key, value] of Object.entries(original)) await setting(key, value).catch(error => console.error(`could not restore ${key}: ${error.message}`))
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
