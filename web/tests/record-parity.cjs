// Packet 043 S01 (FR-REC-09): real base-versus-candidate parity for any screen. Both UIs proxy the same
// Development API and database; records are created through the API and nothing is mocked. Ported from the
// spike's harness (043-D3 KILL); `web/scripts/run-record-parity.mjs` builds and serves the two commits.
//   TUESDAY_RECORD_BASE_UI_URL, TUESDAY_RECORD_CANDIDATE_UI_URL, TUESDAY_RECORD_API_URL  local origins
//   TUESDAY_RECORD_SCREENS  space-separated routes, default the Risks register and the open-roles tab. {project},
//                           {complete}, {empty} and {restricted} are synthetic project numbers, {risk} the synthetic
//                           risk id, e.g. /projects/{project}/risks?panel=Risk:{risk} for a panel
//   TUESDAY_RECORD_FLAGS    flags already merged on those screens: the matrix runs with all off, then all on (§47.6)
//   TUESDAY_RECORD_PARITY_OUT  report path; screenshots of both builds go beside it (evidence, not a pass condition)
//   RECORD_PARITY_SELFCHECK=1  diff logic only; RECORD_PARITY_BROWSER_SELFCHECK=1  identical builds pass, a removed header fails
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
const SCREENS = (process.env.TUESDAY_RECORD_SCREENS ?? '/projects/{project}/risks /projects/{project}/allocations?tab=roles').split(/\s+/).filter(Boolean)
const FLAGS = (process.env.TUESDAY_RECORD_FLAGS ?? '').split(/[\s,]+/).filter(Boolean)
// Project Manager, Discipline Lead, Team Member, Read Only, and a user outside the Restricted project.
const PERSONAS = { priya: 'Priya Nair', marc: 'Marc Dubois', alex: 'Alex Chen', rita: 'Rita Gomez', jill: 'Jill Martin' }
const WIDTHS = [1600, 375]
const launch = () => chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
// Both builds read the same API-created records. Only the local origin is volatile;
// IDs and timestamps are evidence, not noise to erase from a parity report.
const clean = (value) => JSON.parse(JSON.stringify(value, (_, v) => typeof v === 'string' ? v.replace(/^https?:\/\/[^/]+/, '') : v))
const slug = route => route.replace(/\{\w+\}/g, '_').replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '')

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
  await role(project, 'Synthetic parity role')
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
  return { project: project.projectNumber, projectId: project.id, risk: risk.id, complete: complete.projectNumber, empty: empty.projectNumber, restricted: restricted.projectNumber }
}

async function browserSnapshot(origin, who, width, ids, { removeHeader = false, shots = true } = {}) {
  const browser = await launch()
  const createdViewIds = []
  try {
    const context = await browser.newContext({ viewport: { width, height: width < 500 ? 900 : 1000 }, reducedMotion: 'reduce', acceptDownloads: true })
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
    // Headings, caption, column headers, link and button names, disabled states and their titles, aria-sort, aria-current.
    const controls = () => page.evaluate(() => {
      const root = [...document.querySelectorAll('[role=dialog]')].filter(e => e.getClientRects().length).pop() ?? document.querySelector('main')
      return [...root.querySelectorAll('h1,h2,h3,caption,th,a,button,[role=tab],[role=menuitem],[role=option],[role=alert]')].filter(e => e.getClientRects().length)
        .map(e => ({ tag: e.tagName, text: (e.getAttribute('aria-label') || e.textContent || '').replace(/\s+/g, ' ').trim(), disabled: e.disabled || e.getAttribute('aria-disabled') === 'true', title: e.getAttribute('title'), sort: e.getAttribute('aria-sort'), current: e.getAttribute('aria-current'), selected: e.getAttribute('aria-selected') }))
    })
    // Each field's value, editability and the reason given for a disabled one.
    const fields = () => page.evaluate(() => {
      const root = [...document.querySelectorAll('[role=dialog]')].filter(e => e.getClientRects().length).pop() ?? document.querySelector('main')
      return [...root.querySelectorAll('input,textarea,select')].filter(e => e.getClientRects().length)
        .map(e => ({ tag: e.tagName, type: e.getAttribute('type'), label: e.getAttribute('aria-label') || e.labels?.[0]?.textContent?.trim() || e.getAttribute('placeholder'), value: e.value, disabled: e.disabled, readOnly: !!e.readOnly, required: e.required, title: e.getAttribute('title') }))
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
        const bytes = await fs.readFile(await file.path())
        const format = path.extname(file.suggestedFilename()).toLowerCase()
        const data = format === '.xlsx' ? JSON.parse(execFileSync('python3', ['-c', `import io,json,sys,zipfile,xml.etree.ElementTree as E
z=zipfile.ZipFile(io.BytesIO(sys.stdin.buffer.read()))
n={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
root=E.fromstring(z.read('xl/worksheets/sheet1.xml'))
print(json.dumps([[''.join(c.itertext()) for c in row.findall('s:c',n)] for row in root.findall('.//s:sheetData/s:row',n)]))`], { input: bytes }).toString()) : bytes.toString('utf8').replace(/\r\n/g, '\n')
        downloads.push({ item, format, rows: data })
      }
      return { items, downloads }
    }
    // The keyboard path: Tab order from the top of the page, then Enter on the first row control and Esc back.
    const keyboardEvidence = async url => {
      await go(url)
      const order = []
      for (let i = 0; i < 15; i++) { await page.keyboard.press('Tab'); order.push(await focused()) }
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
      else Object.assign(state, { filters: await filterEvidence(url), sort: await sortEvidence(url), columns: await columnEvidence(), views: await viewEvidence(url), exports: await exportEvidence(), keyboard: await keyboardEvidence(url) })
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

;(async () => {
  selfCheck()
  if (process.env.RECORD_PARITY_SELFCHECK === '1') return
  assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'set TUESDAY_SYNTHETIC_API=1 for the disposable Development API')
  const config = await (await fetch(`${apiBase}/api/v1/config`)).json()
  assert.equal(config.authMode, 'Development', 'record parity requires Development authentication')
  const settings = await api('admin/settings')
  const original = Object.fromEntries(['restricted_projects_enabled', ...FLAGS].map(key => [key, settings.find(x => x.key === key)?.value]))
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
      }
      console.log(`record parity browser selfcheck: PASS (identical builds pass at ${WIDTHS.join(' and ')} px; removed header fails) on ${SCREENS.join(' ')}`)
      return
    }
    const report = { candidate, base, apiBase, screens: SCREENS, flags: FLAGS, ids, runs: {}, at: new Date().toISOString() }
    for (const flagged of FLAGS.length ? [false, true] : [false]) {
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
    await fs.writeFile(output, JSON.stringify(report, null, 2))
    const failed = Object.values(report.runs).filter(x => !x.pass).length
    console.log(JSON.stringify({ result: failed ? 'FAIL' : 'PASS', checked: Object.keys(report.runs).length, failed, screens: SCREENS, report: output }))
    if (failed) process.exitCode = 1
  } finally {
    for (const [key, value] of Object.entries(original)) await setting(key, value).catch(error => console.error(`could not restore ${key}: ${error.message}`))
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
