// Packet 037 acceptance sweep (specs/037-calm-workspace/verification.md): AC-CALM-06 route crawl, two-click reach and
// quick-filter rows, AC-CALM-07 filter behaviour, AC-CALM-08 keyboard and axe, AC-CALM-09 one signal per row, the
// section 6 deep links and the screenshots for the visual review. Read-only against the local Development preview: it
// opens menus and sets filters, and never saves a change.
//   TUESDAY_UI_BASE_URL  the UI under test (default http://localhost:5173), proxying /api to the preview API
//   CALM_BASE_UI_URL     the base commit's UI on the same API; needed by quick, filters and deeplinks, which compare the two
//   CALM_CHECKS          subset of crawl,reach,quick,filters,keyboard,axe,signals,deeplinks,shots (default: all)
//   CALM_PAGES           reach only: comma-separated start pages, for a quick re-run
//   CALM_OUT             directory for calm-acceptance.json and the screenshots (default: a temporary directory)
const assert = require('node:assert/strict')
const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')
const { execFileSync } = require('node:child_process')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://localhost:5173'
const baseUi = process.env.CALM_BASE_UI_URL
for (const u of [base, baseUi].filter(Boolean)) assert.ok(['localhost', '127.0.0.1'].includes(new URL(u).hostname), `${u} must be a local Development preview`)
const CHECKS = (process.env.CALM_CHECKS ?? 'crawl,reach,quick,filters,keyboard,axe,signals,deeplinks,shots').split(',')
const out = process.env.CALM_OUT ?? fs.mkdtempSync(path.join(os.tmpdir(), 'calm-'))
const root = path.resolve(__dirname, '../..')
const axePath = require.resolve('axe-core/axe.min.js')
const P = 'SYN-101'
const report = { base, baseUi: baseUi ?? null, commit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(), at: new Date().toISOString() }
const failures = []
const fail = (check, what) => { failures.push(`${check}: ${what}`); console.log(`  FAIL ${what}`) }

// UI text comes from the English dictionary, so the checks follow the app's own labels.
const en = fs.readFileSync(path.join(root, 'web/src/i18n/en.ts'), 'utf8')
const T = (k) => {
  const m = en.match(new RegExp(`['"]${k.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}['"]:\\s*(['"])((?:(?!\\1)[^\\\\]|\\\\.)*)\\1`))
  assert.ok(m, `missing text ${k}`)
  return m[2].replace(/\\(['"])/g, '$1')
}

// One shared pace for API requests: the preview API allows 600 a minute per person, and other sessions use it too.
let nextAt = 0
const pace = async () => { const now = Date.now(), at = Math.max(now, nextAt); nextAt = at + 150; if (at > now) await new Promise((r) => setTimeout(r, at - now)) }
const api = async (endpoint, who = 'jordan') => {
  await pace()
  const r = await fetch(`${base}/api/v1/${endpoint}`, { headers: { 'X-Dev-User': `${who}@hub.test` } })
  assert.ok(r.ok, `GET ${endpoint}: ${r.status}`)
  return r.json()
}

let browser
async function newPage(who, viewport = { width: 1440, height: 900 }) {
  const ctx = await browser.newContext({ viewport, reducedMotion: 'reduce' })
  await ctx.route('**/api/v1/**', async (r) => { await pace(); await r.continue() })
  await ctx.addInitScript((u) => { sessionStorage.setItem('hub.devUser', u); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
  const page = await ctx.newPage()
  page.errors = []
  page.gets = []
  page.on('pageerror', (e) => page.errors.push(e.message.slice(0, 200)))
  page.on('console', (m) => { if (m.type() === 'error' && !/status of 403/.test(m.text())) page.errors.push(m.text().slice(0, 200)) }) // a 403 is an access state
  page.on('request', (r) => { if (r.method() === 'GET' && r.url().includes('/api/v1/')) page.gets.push(r.url().slice(r.url().indexOf('/api/v1/') + 8)) })
  return page
}
async function go(page, url, origin = base) {
  page.errors.length = 0
  page.gets.length = 0
  await page.goto(origin + url, { waitUntil: 'domcontentloaded', timeout: 60000 })
  await page.locator('main').first().waitFor({ timeout: 30000 })
  await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => undefined)
  await page.waitForTimeout(500)
}
const settle = async (page) => { await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => undefined); await page.waitForTimeout(400) }
const VIS = `const vis = (e) => { const r = e.getBoundingClientRect(), s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && !e.closest('.sr-only') }`
const evaluate = (page, fn, arg) => page.evaluate(`(${fn.toString().replace('/*VIS*/', VIS)})(${JSON.stringify(arg ?? null)})`)

// The 17 measured screens (verification.md section 2), in census order.
const SCREENS = ['/home', '/my-work', '/boards', '/projects', '/tasks', '/calendar', '/portfolio', '/workload', '/planner', '/reports', '/team',
  ...['dashboard', 'tasks', 'deliverables', 'timeline', 'allocations', 'coordination'].map((x) => `/projects/${P}/${x}`)]
const slug = (u) => u.replace(/^\//, '').replace(/[/?=&]+/g, '_') || 'root'

let ids
async function fixtures() {
  const p = await api(`projects/${P}`)
  const first = async (endpoint) => { const d = await api(endpoint); const list = Array.isArray(d) ? d : d.items ?? d.rows ?? Object.values(d).find(Array.isArray) ?? []; return list[0]?.id }
  const users = await api('users?limit=500')
  const templates = await api('templates')
  ids = {
    project: p.id, alex: users.find((u) => u.email === 'alex@hub.test').id,
    template: templates.templates.find((x) => x.status === 'Published').id,
    task: await first(`projects/${p.id}/tasks?pageSize=1`), deliverable: await first(`projects/${p.id}/deliverables?pageSize=1`),
    milestone: await first(`projects/${p.id}/milestones`), decision: await first(`projects/${p.id}/decisions`),
    risk: await first(`projects/${p.id}/risks`), issue: await first(`projects/${p.id}/issues`), handoff: await first(`projects/${p.id}/handoffs`),
    change: await first(`projects/${p.id}/changes?page=1`), review: await first(`projects/${p.id}/reviews`), submission: await first(`projects/${p.id}/submissions`),
    action: await first(`projects/${p.id}/actions`), allocation: await first(`projects/${p.id}/allocations`), basis: await first(`projects/${p.id}/design-basis?page=1`),
  }
  report.fixtures = ids
}

// ---------- AC-CALM-06 crawl: every route loads for a permitted person and highlights its sidebar item and group ----------
const PRIMARY = [['/my-work'], ['/projects', '/portfolio'], ['/tasks', '/boards', '/calendar', '/timeline'], ['/planner', '/workload'], ['/time'], ['/reports']]
const MORE = ['/home', '/boards', '/calendar', '/portfolio', '/workload', '/team', '/files', '/staff', '/templates', '/admin']
const under = (pathname, p) => pathname === p || pathname.startsWith(`${p}/`)
const sidebarFor = (pathname) => PRIMARY.find((ps) => ps.some((p) => under(pathname, p)))?.[0] ?? MORE.find((p) => under(pathname, p)) ?? null
const GROUP = { dashboard: 'overview', tasks: 'work', board: 'work', timeline: 'work', calendar: 'work', milestones: 'work', deliverables: 'deliverables',
  submissions: 'deliverables', reviews: 'deliverables', readiness: 'deliverables', coordination: 'coordination', handoffs: 'coordination', changes: 'coordination',
  'design-basis': 'coordination', decisions: 'coordination', risks: 'coordination', issues: 'coordination', meetings: 'coordination', team: 'people',
  allocations: 'people', files: 'files', activity: 'more', settings: 'more', vacation: null }

function routeTable() {
  const src = fs.readFileSync(path.join(root, 'web/src/app/routes.tsx'), 'utf8')
  const table = src.slice(src.indexOf('export const routes'))
  const kidsAt = table.indexOf('children: [')
  const kids = table.slice(kidsAt, table.indexOf('...PROJECT_ROUTES', kidsAt))
  const paths = (s) => [...s.matchAll(/path: '([^']+)'/g)].map((m) => m[1])
  const top = paths(table.replace(kids, '')).filter((x) => x !== 'projects/:number')
  return ['', ...top, `projects/${P}`, ...paths(kids).map((x) => `projects/:number/${x}`)]
}
const concrete = (p) => `/${p}`.replace(':number', P).replace('tasks/:id', `tasks/${ids.task}`).replace('templates/:id', `templates/${ids.template}`)
  .replace('people/:id', `people/${ids.alex}`).replace(':code', 'tasks-due-this-week').replace(':kind', 'disciplines').replace(/^\/\*$/, '/no-such-page')

function pageState() {
  /*VIS*/
  const main = document.querySelector('main')
  const tabs = document.querySelector('main nav[aria-label="Project sections"]')
  return {
    pathname: location.pathname, search: location.search,
    sidebar: [...document.querySelectorAll('nav[aria-label="Main navigation"] a[aria-current="page"]')].filter(vis).map((a) => a.getAttribute('href')),
    group: tabs ? [...tabs.querySelectorAll('a[aria-current]')].filter(vis).map((a) => a.textContent.trim()) : null,
    more: tabs ? tabs.querySelector('[aria-haspopup]')?.textContent.trim() ?? null : null,
    groupPage: [...document.querySelectorAll('main nav[aria-label$=" pages"] a[aria-current="page"]')].filter(vis).map((a) => a.textContent.trim()),
    heading: main?.querySelector('h1, h2')?.textContent.trim().slice(0, 80) ?? null,
    alerts: [...(main?.querySelectorAll('[role=alert]') ?? [])].filter(vis).map((e) => e.textContent.trim().slice(0, 120)),
    dialog: [...document.querySelectorAll('[role=dialog]')].filter(vis).map((d) => (d.getAttribute('aria-label') || d.querySelector('h2, h3')?.textContent || '').trim().slice(0, 80))[0] ?? null,
  }
}

async function crawl() {
  console.log('\nAC-CALM-06 crawl')
  const notFound = T('app.notFoundTitle')
  const rows = []
  const pages = {}
  for (const route of routeTable()) {
    const url = concrete(route)
    const who = /^\/(admin|templates|staff)/.test(url) ? 'jordan' : url.startsWith('/people/') ? 'sam' : 'priya' // Sam supervises Alex
    pages[who] ??= await newPage(who)
    const page = pages[who]
    await go(page, url)
    const s = await evaluate(page, pageState)
    const seg = s.pathname.startsWith(`/projects/${P}/`) ? s.pathname.split('/')[3] : null
    const want = { sidebar: sidebarFor(s.pathname), group: seg == null ? undefined : GROUP[seg] }
    const problems = []
    if (route === '*') { if (s.heading !== notFound) problems.push('the unknown route does not show the not-found page') }
    else if (s.heading === notFound) problems.push('not found')
    if (page.errors.length) problems.push(`errors: ${page.errors.join(' | ')}`)
    if (s.alerts.length) problems.push(`alerts: ${s.alerts.join(' | ')}`)
    if (want.sidebar ? !s.sidebar.includes(want.sidebar) : s.sidebar.length) problems.push(`sidebar highlights ${JSON.stringify(s.sidebar)}, expected ${want.sidebar}`)
    if (want.group === 'more') { if (s.more !== T(`ptab.${seg}`) || s.group.length) problems.push(`More shows ${s.more}, groups ${JSON.stringify(s.group)}`) }
    else if (want.group) {
      if (s.group?.length !== 1 || s.group[0] !== T(`calm.group.${want.group}`)) problems.push(`group ${JSON.stringify(s.group)}, expected ${T(`calm.group.${want.group}`)}`)
    } else if (want.group === null && s.group?.length) problems.push(`group ${JSON.stringify(s.group)} for a page that is in no group`)
    const row = { route: `/${route}`, url, who, ...s, expectedSidebar: want.sidebar, expectedGroup: want.group ?? null, problems }
    rows.push(row)
    console.log(`${problems.length ? '✗' : '✓'} ${who} ${url} → ${s.pathname}${s.search} sidebar ${s.sidebar.join(',') || '-'} group ${s.group?.join(',') || s.more || '-'}${problems.length ? ` (${problems.join('; ')})` : ''}`)
    if (problems.length) fail('crawl', `${url}: ${problems.join('; ')}`)
  }
  for (const p of Object.values(pages)) await p.context().close()
  report.crawl = rows
}

// ---------- AC-CALM-06 reach: every former tab, sidebar item, view and quick filter is at most two clicks away ----------
/** Everything a click could act on now: links by target, quick-filter chips (aria-pressed) and menu items by name. */
function reachable() {
  /*VIS*/
  const norm = (s) => s.replace(/\s+/g, ' ').trim().replace(/^✓\s*/, '').replace(/\s*\d+$/, '') // a count badge follows the name
  const keys = []
  for (const e of document.querySelectorAll('a[href], button, [role^=menuitem], [role=option], [role=tab], summary')) {
    if (!vis(e)) continue
    if (e.matches('a[href]')) { const u = new URL(e.href, location.href); if (u.origin === location.origin) keys.push(`link:${u.pathname}${u.search}`) }
    const name = norm(e.getAttribute('aria-label') || e.textContent || '')
    if (e.hasAttribute('aria-pressed')) keys.push(`chip:${name}`)
    if (/^menuitem/.test(e.getAttribute('role') ?? '')) keys.push(`item:${name}`)
    keys.push(`name:${name}`)
  }
  return keys
}
/** Controls that open something without leaving the page area: menus, popovers, disclosures, project groups. */
function openers() {
  /*VIS*/
  const els = [...document.querySelectorAll('[aria-haspopup], button[aria-expanded], main nav[aria-label="Project sections"] a, summary')]
    .filter((e) => vis(e) && !e.closest('table, [role=grid], [role=table], [role=treegrid], [role=dialog], [role=menu], [data-slot=popover-content], [data-slot=dropdown-menu-content]'))
  return els.map((e, i) => { e.setAttribute('data-calm-op', String(i)); return `${(e.getAttribute('aria-label') || e.textContent || '').replace(/\s+/g, ' ').trim()}|${e.getAttribute('href') ?? ''}|${e.hasAttribute('aria-haspopup') ? 'popup' : 'toggle'}` })
}
async function reachMap(page, url) {
  await go(page, url)
  const d1 = new Set(await evaluate(page, reachable))
  const d2 = new Map()
  const list = await evaluate(page, openers)
  let dirty = false
  for (const key of list) {
    if (dirty || !page.url().endsWith(url)) await go(page, url)
    const i = (await evaluate(page, openers)).indexOf(key)
    if (i < 0) { dirty = true; continue }
    await page.locator(`[data-calm-op="${i}"]`).first().click()
    await settle(page)
    for (const k of await evaluate(page, reachable)) if (!d1.has(k) && !d2.has(k)) d2.set(k, key.split('|')[0])
    if (key.endsWith('|popup') && page.url().endsWith(url)) { await page.keyboard.press('Escape'); await page.waitForTimeout(200); dirty = await page.locator('[role=dialog], [role=menu]').filter({ visible: true }).count() > 0 }
    else dirty = true
  }
  return { d1, d2 }
}

async function reach() {
  console.log('\nAC-CALM-06 reach (clicks from the page)')
  const tquick = ['mine', 'overdue', 'blocked', 'blocking', 'dueThisWeek', 'unassigned', 'readyForReview'].map((k) => T(`tquick.${k}`))
  const chips = (...labels) => labels.map((l) => `chip:${l}`)
  const formerTabs = ['dashboard', 'coordination', 'readiness', 'handoffs', 'reviews', 'changes', 'submissions', 'allocations', 'design-basis', 'tasks', 'board',
    'deliverables', 'milestones', 'timeline', 'decisions', 'risks', 'issues', 'meetings', 'files', 'calendar', 'team', 'activity', 'settings'].map((x) => `link:/projects/${P}/${x}`)
  const baseSidebar = ['/home', '/my-work', '/boards', '/projects', '/tasks', '/calendar', '/files', '/time', '/reports', '/team', '/portfolio', '/workload', '/planner', '/staff', '/notifications', '/templates', '/admin']
  const sections = ['planner.myWeek', 'mywork.attention', 'mywork.tasks', 'mywork.reviews', 'mywork.deliverables', 'mywork.waiting', 'mywork.blocking', 'mywork.decisions',
    'mywork.actions', 'mywork.projects', 'mywork.milestones', 'mywork.completed'].map((k) => `item:${T(k)}`)
  // [who, start page, former targets (base 9beace03), what they are]
  const PLAN = [
    ['priya', `/projects/${P}/dashboard`, [...formerTabs, `link:/workload?projectId=${ids.project}`], 'former project tabs'],
    ['priya', '/my-work', baseSidebar.filter((x) => !['/staff', '/templates', '/admin'].includes(x)).map((x) => `link:${x}`), 'former sidebar items (Priya)'],
    ['jordan', '/my-work', baseSidebar.map((x) => `link:${x}`), 'former sidebar items (Jordan)'],
    ['priya', '/my-work', [...['overview', 'today', 'upcoming', 'overdue', 'completed', 'inbox', 'coordination'].map((x) => `item:${T(`mywork.tab.${x}`)}`), ...chips(T('mywork.hideWaiting'))], 'My Work views and quick filter'],
    ['priya', '/my-work?tab=overview', sections, 'My Work sections (from Overview)'],
    ['priya', '/tasks', chips(T('tfilter.open'), ...tquick), 'quick filters'],
    ['priya', '/boards', chips(T('tfilter.open'), ...tquick), 'quick filters'],
    ['priya', `/projects/${P}/tasks`, chips(...tquick), 'quick filters'],
    ['priya', `/projects/${P}/board`, chips(...tquick), 'quick filters'],
    ['priya', '/notifications', chips(T('notif.unreadOnly')), 'quick filters'],
    ['priya', '/notifications?tab=following', chips(T('activity.important')), 'quick filters'],
    ['priya', '/planner', chips(T('planner.indicator.over'), T('planner.indicator.under'), T('planner.indicator.stale'), T('planner.stale'), T('planner.showMyDrafts')), 'quick filters'],
    ['priya', '/workload', chips(...['over', 'under', 'cluster', 'unestimated'].map((x) => T(`workload.ind.${x}`))), 'quick filters'],
    // The base "Any indicator" chip cleared the indicator; now the set chip toggles off, or its active-filter token is removed.
    ['priya', '/workload?indicator=over', [`chip:${T('workload.ind.over')}`, `name:${T('filters.remove').replace('{name}', T('workload.indicator'))}`], 'Any indicator (clears the indicator)'],
    ['priya', `/projects/${P}/changes`, chips(T('coord.mine')), 'quick filters'],
    ['priya', `/projects/${P}/reviews`, chips(T('coord.mine')), 'quick filters'],
    ['priya', `/projects/${P}/decisions`, chips(T('decision.blockingWork')), 'quick filters'],
    ['priya', `/projects/${P}/design-basis`, chips(T('basis.overdueOnly')), 'quick filters'],
    ['priya', `/projects/${P}/handoffs`, chips(T('handoff.overdueOnly')), 'quick filters'],
    ['priya', `/projects/${P}/meetings`, chips(T('ind.overdue')), 'quick filters'],
    ['priya', `/projects/${P}/risks`, chips(T('risk.reviewOverdue')), 'quick filters'],
    ['priya', `/projects/${P}/issues`, chips(T('ind.overdue')), 'quick filters'],
    ['priya', '/calendar', chips(...['Deadline', 'Meeting', 'Site Work', 'Internal Task'].map((x) => T(`calendar.type.${x}`))), 'quick filters (calendar types)'],
  ]
  const rows = []
  const pages = {}
  for (const [who, url, targets, what] of PLAN.filter(([, url]) => !process.env.CALM_PAGES || process.env.CALM_PAGES.split(',').includes(url))) {
    pages[who] ??= await newPage(who)
    const { d1, d2 } = await reachMap(pages[who], url)
    for (const t of targets) {
      const clicks = d1.has(t) ? 1 : d2.has(t) ? 2 : null
      rows.push({ who, page: url, what, target: t, clicks, via: d2.get(t) ?? null })
      if (clicks == null) fail('reach', `${who} ${url}: ${t} is not reachable in 2 clicks`)
    }
    const counts = targets.map((t) => (d1.has(t) ? 1 : d2.has(t) ? 2 : '✗'))
    console.log(`${counts.includes('✗') ? '✗' : '✓'} ${who} ${url} ${what}: ${targets.map((t, i) => `${t.replace(/^\w+:/, '')}=${counts[i]}`).join(', ')}`)
  }
  for (const p of Object.values(pages)) await p.context().close()
  report.reach = rows
}

// ---------- AC-CALM-06 quick-filter rows: each former quick-filter URL shows the same rows as the base UI ----------
/** What a list shows: body rows of its first table (group headers and packet 036's Unfilled roles left out), task keys
 *  in visible text outside the project header and filter fields, list items and calendar entries. */
function rowCount() {
  /*VIS*/
  const main = document.querySelector('main')
  const table = [...main.querySelectorAll('table')].find((t) => vis(t) && !t.closest('#wl-unfilled'))
  let text = ''
  const tw = document.createTreeWalker(main, NodeFilter.SHOW_TEXT)
  while (tw.nextNode()) { const p = tw.currentNode.parentElement; if (p && vis(p) && !p.closest('select, header, nav, [role=dialog]')) text += ` ${tw.currentNode.textContent}` }
  return {
    rows: table ? [...table.querySelectorAll('tbody tr')].filter((r) => vis(r) && !r.querySelector('th[scope=rowgroup]')).length : 0,
    keys: new Set(text.match(/\bSYN-\d{3}-T\d{4}\b/g) ?? []).size,
    items: [...main.querySelectorAll('li')].filter(vis).length,
    entries: [...main.querySelectorAll('[role=region] button, [role=region] a')].filter(vis).length,
  }
}
/** The measure that names a page's rows: list items on My Work (its My Week strip is a table), entries on the calendar,
 *  cards (by key) on boards, table rows elsewhere; item keys always. */
const measure = (url, m) => url.startsWith('/my-work') ? { items: m.items, keys: m.keys } : url.startsWith('/calendar') ? { entries: m.entries }
  : /\/boards?\b/.test(url) ? { keys: m.keys } : { rows: m.rows, keys: m.keys }
async function quick() {
  console.log('\nAC-CALM-06 quick-filter URLs against the base UI')
  assert.ok(baseUi, 'CALM_BASE_UI_URL is needed for the quick-filter comparison')
  const U = [
    ...['open', 'mine', 'overdue', 'blocked', 'blocking', 'dueThisWeek', 'unassigned', 'readyForReview'].flatMap((k) => [`/tasks?${k}=true`, `/boards?${k}=true`]),
    ...['mine', 'overdue', 'blocked', 'blocking', 'dueThisWeek', 'unassigned', 'readyForReview'].flatMap((k) => [`/projects/${P}/tasks?${k}=true`, `/projects/${P}/board?${k}=true`]),
    '/my-work?tab=overview&hideWaiting=1', '/my-work?hideWaiting=1', '/notifications?unread=true', '/notifications?tab=following&important=true',
    '/planner?indicator=OverPlanned', '/planner?indicator=UnderPlanned', '/planner?indicator=StalePlan', '/planner?stale=true', '/planner?includeMyDrafts=false',
    ...['over', 'under', 'cluster', 'unestimated'].map((x) => `/workload?indicator=${x}`),
    `/projects/${P}/changes?mine=true`, `/projects/${P}/reviews?mine=true`, `/projects/${P}/decisions?blocking=true`, `/projects/${P}/design-basis?overdue=true`,
    `/projects/${P}/handoffs?overdue=true`, `/projects/${P}/meetings?indicator=overdue`, `/projects/${P}/risks?indicator=reviewOverdue`, `/projects/${P}/issues?indicator=overdue`,
    '/calendar?hide=Meeting', '/calendar?hide=Deadline',
  ]
  // Differences the packet decided on, reported but not failed.
  const DECIDED = { '/my-work?hideWaiting=1': 'E3: My Work opens on Today; the old default view is ?tab=overview (M1)' }
  const head = await newPage('priya'), old = await newPage('priya')
  const filterGets = (gets, url) => { const keys = [...new URLSearchParams(url.split('?')[1])].map(([k]) => k).filter((k) => k !== 'tab'); return [...new Set(gets.filter((g) => keys.some((k) => new RegExp(`[?&]${k}=`).test(g))))].sort() }
  const rows = []
  for (const url of U) {
    await go(head, url); const h = { shown: measure(url, await evaluate(head, rowCount)), gets: filterGets(head.gets, url), url: new URL(head.url()).search }
    await go(old, url, baseUi); const b = { shown: measure(url, await evaluate(old, rowCount)), gets: filterGets(old.gets, url), url: new URL(old.url()).search }
    const same = JSON.stringify(h.shown) === JSON.stringify(b.shown) && JSON.stringify(h.gets) === JSON.stringify(b.gets)
    rows.push({ url, head: h, base: b, same, decided: DECIDED[url] ?? null })
    console.log(`${same ? '✓' : DECIDED[url] ? '~' : '✗'} ${url}: ${JSON.stringify(b.shown)} → ${JSON.stringify(h.shown)}; filter requests ${same ? `same ${JSON.stringify(h.gets)}` : `${JSON.stringify(b.gets)} → ${JSON.stringify(h.gets)}`}${!same && DECIDED[url] ? ` (${DECIDED[url]})` : ''}`)
    if (!same && !DECIDED[url]) fail('quick', `${url}: base ${JSON.stringify(b.shown)} ${JSON.stringify(b.gets)}, now ${JSON.stringify(h.shown)} ${JSON.stringify(h.gets)}`)
  }
  await head.context().close(); await old.context().close()
  report.quick = rows
}

// ---------- AC-CALM-07 filters: the popover sets the same URL as the base, with chips, a count, Clear and reload ----------
async function filters() {
  console.log('\nAC-CALM-07 filter behaviour')
  assert.ok(baseUi, 'CALM_BASE_UI_URL is needed for the filter comparison')
  const filtersBtn = (page) => page.locator('main button').filter({ hasText: new RegExp(`^${T('calm.filters')}`) }).first()
  const pop = (page) => page.locator('[role=dialog]').filter({ visible: true }).first()
  const params = (page) => Object.fromEntries(new URL(page.url()).searchParams)
  const diff = (a, b) => Object.fromEntries(Object.entries(b).filter(([k, v]) => a[k] !== v).concat(Object.keys(a).filter((k) => !(k in b)).map((k) => [k, null])))
  const tokens = (page) => page.locator(`[role=group][aria-label="${T('filters.active')}"] button[aria-label^="${T('filters.remove').split('{')[0]}"]`)
  // [page, quick chip to also set (its base label)]
  const PAGES = [[`/projects/${P}/tasks`, T('tquick.overdue')], [`/projects/${P}/deliverables`, null], ['/planner', T('planner.indicator.over')], ['/my-work', T('mywork.hideWaiting')], ['/tasks', T('tquick.blocked')]]
  const head = await newPage('priya'), old = await newPage('priya')
  const rows = []
  for (const [url, chip] of PAGES) {
    const r = { url, steps: [] }
    const step = (name, ok, detail) => { r.steps.push({ name, ok, detail }); if (!ok) fail('filters', `${url} ${name}: ${JSON.stringify(detail)}`) }
    await go(head, url)
    const before = params(head)
    await filtersBtn(head).click()
    await pop(head).waitFor()
    // The first select in the popover with a choice, set to its first real option.
    const field = await pop(head).evaluate((d) => {
      const s = [...d.querySelectorAll('select')].find((x) => x.options.length > 1)
      if (!s) return null
      const label = (s.id && d.querySelector(`label[for="${s.id}"]`)?.textContent) || s.getAttribute('aria-label') || s.closest('label')?.textContent || ''
      return { id: s.id, label: label.trim(), value: [...s.options].find((o) => o.value)?.value }
    })
    if (!field) { step('popover has a select field', false, null); rows.push(r); continue }
    await pop(head).locator(`select#${field.id}`).selectOption(field.value)
    await settle(head)
    const setByField = diff(before, params(head))
    // The same field at the base, where it was always visible.
    await go(old, url, baseUi)
    const oldBefore = params(old)
    await old.locator('main').getByLabel(field.label, { exact: true }).first().selectOption(field.value)
    await settle(old)
    const oldSet = diff(oldBefore, params(old))
    step(`"${field.label}" sets the same URL parameter as the base`, JSON.stringify(setByField) === JSON.stringify(oldSet), { now: setByField, base: oldSet })
    await head.keyboard.press('Escape')
    await head.waitForTimeout(200)
    step('a removable chip shows the filter', await tokens(head).count() >= 1, await tokens(head).count())
    const n1 = await filtersBtn(head).innerText()
    step('the Filters button counts it', /\b1\b/.test(n1), n1)
    if (chip) {
      const now = params(head)
      const target = head.locator(`main [aria-pressed]`).filter({ hasText: chip }).first()
      if (!await target.isVisible()) { await filtersBtn(head).click(); await pop(head).waitFor() }
      await head.locator('[aria-pressed]').filter({ hasText: chip }).filter({ visible: true }).first().click()
      await settle(head)
      const chipSet = diff(now, params(head))
      await head.keyboard.press('Escape')
      await go(old, url, baseUi)
      const o0 = params(old)
      await old.locator('main [aria-pressed]').filter({ hasText: chip }).first().click()
      await settle(old)
      step(`chip "${chip}" sets the same URL parameter as the base`, JSON.stringify(chipSet) === JSON.stringify(diff(o0, params(old))), { now: chipSet, base: diff(o0, params(old)) })
      const n2 = await filtersBtn(head).innerText()
      step('the count includes the chip', /\b2\b/.test(n2), n2)
    }
    const set = params(head), chipsShown = await tokens(head).count(), count = await filtersBtn(head).innerText()
    await head.reload(); await head.locator('main').first().waitFor(); await settle(head)
    step('reload keeps the URL', JSON.stringify(params(head)) === JSON.stringify(set), { before: set, after: params(head) })
    step('reload keeps the chips and the count', await tokens(head).count() === chipsShown && await filtersBtn(head).innerText() === count, { chips: [chipsShown, await tokens(head).count()], count: [count, await filtersBtn(head).innerText()] })
    await filtersBtn(head).click(); await pop(head).waitFor()
    step('reload keeps the field value', await pop(head).locator(`select#${field.id}`).inputValue() === field.value, await pop(head).locator(`select#${field.id}`).inputValue())
    await head.keyboard.press('Escape')
    await tokens(head).first().click(); await settle(head)
    step('a chip removes its filter', (await tokens(head).count()) === chipsShown - 1, await tokens(head).count())
    if (chipsShown > 1) {
      await head.locator('main').getByRole('button', { name: T('filters.clear') }).first().click(); await settle(head)
    }
    const left = Object.keys(diff(before, params(head)))
    step('Clear resets every filter', left.length === 0 && await tokens(head).count() === 0 && !/\d/.test(await filtersBtn(head).innerText()), { left, chips: await tokens(head).count(), button: await filtersBtn(head).innerText() })
    rows.push(r)
    console.log(`${r.steps.every((s) => s.ok) ? '✓' : '✗'} ${url}: ${r.steps.map((s) => `${s.ok ? '✓' : '✗'} ${s.name}`).join('; ')}`)
  }
  await head.context().close(); await old.context().close()
  report.filters = rows
}

// ---------- AC-CALM-08 keyboard: Filters popover and menus by keyboard, then axe on the 17 screens ----------
async function keyboard() {
  console.log('\nAC-CALM-08 keyboard')
  const page = await newPage('priya')
  const rows = []
  const active = () => page.evaluate(() => { const a = document.activeElement; return { tag: a?.tagName, role: a?.getAttribute('role'), op: a?.getAttribute('data-calm-k'), inDialog: !!a?.closest('[role=dialog]'), inMenu: !!a?.closest('[role=menu]'), text: (a?.getAttribute('aria-label') || a?.textContent || '').trim().slice(0, 40) } })
  for (const url of SCREENS) {
    await go(page, url)
    const r = { url, filters: null, menus: [] }
    const btn = page.locator('main button').filter({ hasText: new RegExp(`^${T('calm.filters')}`) }).first()
    if (await btn.isVisible().catch(() => false)) {
      await btn.evaluate((e) => e.setAttribute('data-calm-k', 'filters'))
      await btn.focus(); await page.keyboard.press('Enter'); await page.waitForTimeout(300)
      const opened = await active()
      await page.keyboard.press('Escape'); await page.waitForTimeout(300)
      const back = await active()
      r.filters = { opensWithEnter: opened.inDialog, focusInside: opened.inDialog, closesWithEsc: (await page.locator('[role=dialog]').filter({ visible: true }).count()) === 0, focusReturns: back.op === 'filters' }
      if (!Object.values(r.filters).every(Boolean)) fail('keyboard', `${url} Filters popover ${JSON.stringify(r.filters)}`)
    }
    const triggers = await page.evaluate(() => [...document.querySelectorAll('main [aria-haspopup=menu]')].filter((e) => e.getBoundingClientRect().width > 0 && !e.closest('table, [role=grid], [role=table], [role=treegrid]')).map((e, i) => { e.setAttribute('data-calm-m', String(i)); return (e.getAttribute('aria-label') || e.textContent).trim().slice(0, 40) }))
    for (let i = 0; i < triggers.length; i++) {
      const t = page.locator(`[data-calm-m="${i}"]`)
      await t.evaluate((e) => e.setAttribute('data-calm-k', 'menu'))
      await t.focus(); await page.keyboard.press('Enter'); await page.waitForTimeout(300)
      const first = await active()
      await page.keyboard.press('ArrowDown'); await page.waitForTimeout(100)
      const second = await active()
      await page.keyboard.press('Escape'); await page.waitForTimeout(300)
      const back = await active()
      const m = { menu: triggers[i], opensWithEnter: first.inMenu, arrowMoves: second.inMenu && (second.text !== first.text || (await page.locator('[role=menu] [role^=menuitem]').count()) <= 1), closesWithEsc: (await page.locator('[role=menu]').filter({ visible: true }).count()) === 0, focusReturns: back.op === 'menu' }
      await t.evaluate((e) => e.removeAttribute('data-calm-k'))
      r.menus.push(m)
      if (![m.opensWithEnter, m.arrowMoves, m.closesWithEsc, m.focusReturns].every(Boolean)) fail('keyboard', `${url} menu "${m.menu}" ${JSON.stringify(m)}`)
    }
    rows.push(r)
    console.log(`${url}: Filters ${r.filters ? Object.values(r.filters).every(Boolean) ? '✓' : '✗' : '-'}; menus ${r.menus.map((m) => `${m.menu} ${[m.opensWithEnter, m.arrowMoves, m.closesWithEsc, m.focusReturns].every(Boolean) ? '✓' : '✗'}`).join(', ') || '-'}`)
  }
  // The sidebar More and the project More, which every person meets.
  await go(page, '/my-work')
  const more = page.locator('nav[aria-label="Main navigation"] button[aria-expanded]').filter({ hasText: T('nav.more') }).first()
  await more.focus(); await page.keyboard.press('Enter'); await page.waitForTimeout(200)
  const sidebar = { expands: await more.getAttribute('aria-expanded') === 'true' }
  await page.keyboard.press('Tab')
  sidebar.tabIntoItems = await page.evaluate(() => !!document.activeElement?.closest('#nav-more'))
  rows.push({ url: '/my-work sidebar More', sidebar })
  if (!Object.values(sidebar).every(Boolean)) fail('keyboard', `sidebar More ${JSON.stringify(sidebar)}`)
  console.log(`sidebar More: ${JSON.stringify(sidebar)}`)
  await page.context().close()
  report.keyboard = rows
}

async function axe() {
  console.log('\nAC-CALM-08 axe (wcag2a, wcag2aa, wcag21a, wcag21aa)')
  const rows = []
  for (const who of ['priya', 'sam']) {
    const page = await newPage(who)
    for (const url of SCREENS) {
      // Closed and with the Filters popover open count; a menu open is recorded only (an open menu hides the page behind it).
      for (const state of ['closed', 'filters open', 'menu open']) {
        await go(page, url)
        if (state !== 'closed') {
          const btn = state === 'menu open' ? page.locator('main [aria-haspopup=menu]').filter({ visible: true }).first()
            : page.locator('main button').filter({ hasText: new RegExp(`^${T('calm.filters')}`) }).first()
          if (!await btn.isVisible().catch(() => false)) continue
          await btn.click(); await page.waitForTimeout(400)
          if (!await page.locator(state === 'menu open' ? '[role=menu]' : '[role=dialog]').filter({ visible: true }).count()) { fail('axe', `${who} ${url}: ${state} did not open`); continue }
        }
        await page.addScriptTag({ path: axePath })
        const v = await page.evaluate(async () => (await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] }, resultTypes: ['violations'] }))
          .violations.map((x) => ({ id: x.id, impact: x.impact, nodes: x.nodes.length, target: x.nodes[0].target.join(' ') })))
        const bad = v.filter((x) => x.impact === 'serious' || x.impact === 'critical')
        rows.push({ who, url, state, violations: v, seriousOrCritical: bad.length, counted: state !== 'menu open' })
        console.log(`${!bad.length ? '✓' : state === 'menu open' ? '~' : '✗'} ${who} ${url} (${state}): ${v.map((x) => `${x.id}(${x.impact})x${x.nodes}`).join(', ') || 'no violations'}`)
        if (bad.length && state !== 'menu open') fail('axe', `${who} ${url} (${state}): ${bad.map((x) => `${x.id} x${x.nodes} ${x.target}`).join('; ')}`)
      }
    }
    await page.context().close()
  }
  report.axe = rows
}

// ---------- AC-CALM-09: at most one indicator per row; extras as +n with a label; week indicators name their week ----------
function signalRows(weekLabels) {
  /*VIS*/
  const chipSel = 'span.rounded-md.py-px, button.rounded-md.py-px'
  const rows = [...document.querySelectorAll('main tr, main [role=row]')].filter(vis)
  const bad = [], more = [], weeks = []
  for (const r of rows) {
    const chips = [...r.querySelectorAll(chipSel)].filter(vis)
    const name = (r.querySelector('th, [role=rowheader], td') ?? r).textContent.replace(/\s+/g, ' ').trim().slice(0, 50)
    if (chips.length > 1) bad.push({ row: name, chips: chips.map((c) => c.textContent.trim()) })
    for (const g of r.querySelectorAll('[data-signals]')) {
      const n = Number(g.getAttribute('data-signals')), plus = [...g.querySelectorAll('button[aria-label]')].find((b) => /^\+\d+$/.test(b.textContent.trim()))
      if (n > 1) more.push({ row: name, signals: n, plus: plus?.textContent.trim() ?? null, label: plus?.getAttribute('aria-label') ?? null })
    }
    for (const c of chips) { const text = c.textContent.trim(); if (weekLabels.some((w) => text.includes(w))) weeks.push({ row: name, chip: text, namesWeek: /\b[A-Z][a-z]{2} \d{1,2}\b/.test(text) }) }
  }
  return { rows: rows.length, bad, more, weeks }
}
async function signals() {
  console.log('\nAC-CALM-09 one signal per row')
  const weekLabels = [T('planner.indicator.over'), T('planner.indicator.under'), T('planner.indicator.stale'), T('workload.ind.over')]
  const PLAN = [['priya', '/planner'], ['lena', '/planner'], ['priya', '/workload'], ['sam', '/workload'], ['priya', '/tasks'], ['priya', '/my-work?tab=overdue'],
    ['priya', `/projects/${P}/tasks`], ['priya', `/projects/${P}/deliverables`], ['priya', '/projects/SYN-105/tasks'], ['priya', '/projects/SYN-105/deliverables']]
  const rows = []
  const pages = {}
  for (const [who, url] of PLAN) {
    pages[who] ??= await newPage(who)
    await go(pages[who], url)
    const r = await evaluate(pages[who], signalRows, weekLabels)
    const plusOk = r.more.every((m) => m.plus === `+${m.signals - 1}` && m.label)
    const weekOk = r.weeks.filter((w) => !w.chip.startsWith(T('workload.ind.over'))).every((w) => w.namesWeek)
    rows.push({ who, url, ...r, plusOk, weekOk })
    console.log(`${!r.bad.length && plusOk && weekOk ? '✓' : '✗'} ${who} ${url}: ${r.rows} rows, ${r.bad.length} with two or more chips, ${r.more.length} with +n${plusOk ? '' : ' (bad +n)'}, week chips ${r.weeks.map((w) => w.chip).join(' | ') || '-'}`)
    if (r.bad.length) fail('signals', `${who} ${url}: ${JSON.stringify(r.bad.slice(0, 3))}`)
    if (!plusOk) fail('signals', `${who} ${url}: +n ${JSON.stringify(r.more.filter((m) => m.plus !== `+${m.signals - 1}` || !m.label).slice(0, 3))}`)
    if (!weekOk) fail('signals', `${who} ${url}: week chip without its week ${JSON.stringify(r.weeks.filter((w) => !w.namesWeek))}`)
  }
  for (const p of Object.values(pages)) await p.context().close()
  report.signals = rows
}

// ---------- Section 6 deep links: the links in the docs and in notification and digest code open the same page as at the base ----------
async function deeplinks() {
  console.log('\nSection 6 deep links (base UI vs this UI, same API)')
  assert.ok(baseUi, 'CALM_BASE_UI_URL is needed for the deep-link comparison')
  const grep = (args) => { try { return execFileSync('git', ['grep', '-ohE', ...args], { cwd: root, encoding: 'utf8' }).split('\n').filter(Boolean) } catch { return [] } }
  const pid = ids.project, monday = (() => { const d = new Date(); d.setDate(d.getDate() - ((d.getDay() + 6) % 7)); return d.toISOString().slice(0, 10) })()
  const panel = { Task: ids.task, Deliverable: ids.deliverable, Milestone: ids.milestone, Decision: ids.decision, Risk: ids.risk, Issue: ids.issue, Handoff: ids.handoff,
    ChangeNotice: ids.change, ReviewPackage: ids.review, SubmissionPackage: ids.submission, Action: ids.action }
  // Notification, digest and attention links built by the API (interpolated strings), then the app paths named in the docs.
  const code = grep(['\\$"/(projects|my-work|time|tasks|planner|workload|notifications)[^"]*"', '--', 'src/*.cs', ':!src/**/*Tests*']).map((s) => s.slice(2, -1))
  const docs = grep(['/projects/(\\{id\\}|1234|SYN-[0-9]+)(/[a-z-]+)?(\\?[^ )"\'`<>]*)?', '--', 'docs', '*.md', ':!specs', ':!spec-parts', ':!Engineering-Project-Coordination-Hub-Specification.md'])
  const fill = (s) => s.replace(/panel=\{type\}:\{id\}|panel=\{target\.Type\}:\{target\.Id\}/, `panel=Task:${ids.task}`)
    .replace(/panel=([A-Za-z]+):\{[^}]+\}/g, (_, type) => `panel=${type}:${panel[type] ?? 'unknown'}`)
    .replace(/allocation=\{[^}]+\}/, `allocation=${ids.allocation}`).replace(/basis=\{[^}]+\}/, `basis=${ids.basis}`)
    .replace(/\{(await )?Num\([^)]*\)\)?\}|\{[a-z]*\.?(Project)?Number\}|\{id\}|1234/g, P).replace('{page}', 'tasks').replace('{tab}', 'tasks')
    .replace(/\{p\.Id\}/, pid).replace(/\{[a-zA-Z.]+:yyyy-MM-dd\}/, monday).replace(/\{a\.RuleId\}/, 'A-01').replace(/\?\{qs\}|\{\(qs\.Length.*$/, '')
  const skip = (s) => /\{/.test(s) || /\/(members|disciplines|template-packs|decision-log|actions)(\b|$)/.test(s) // API paths and unfilled templates
  const links = [...new Set([...code, ...docs].map(fill))].filter((s) => !skip(s)).sort()
  report.deeplinksSkipped = [...new Set([...code, ...docs].map(fill))].filter(skip)
  const head = await newPage('priya'), old = await newPage('priya')
  const rows = []
  for (const url of links) {
    await go(head, url); const h = await evaluate(head, pageState)
    await go(old, url, baseUi); const b = await evaluate(old, pageState)
    const same = h.pathname === b.pathname && h.heading === b.heading && !!h.dialog === !!b.dialog
    const notFound = h.heading === T('app.notFoundTitle')
    rows.push({ url, head: { path: h.pathname + h.search, heading: h.heading, dialog: h.dialog, group: h.group }, base: { path: b.pathname + b.search, heading: b.heading, dialog: b.dialog }, same, notFound })
    console.log(`${same && !notFound ? '✓' : '✗'} ${url} → ${h.pathname}${h.search} "${h.heading}"${h.dialog ? ` panel "${h.dialog}"` : ''}${same ? '' : ` (base ${b.pathname}${b.search} "${b.heading}"${b.dialog ? ` panel "${b.dialog}"` : ''})`}`)
    if (!same || notFound) fail('deeplinks', `${url}: now ${h.pathname}${h.search} "${h.heading}" ${h.dialog ?? ''}, base ${b.pathname}${b.search} "${b.heading}" ${b.dialog ?? ''}`)
  }
  await head.context().close(); await old.context().close()
  report.deeplinks = rows
}

// ---------- Screenshots of the 17 screens at 1440 and 1024 wide, for Jay's review (SC-CALM-02) ----------
async function shots() {
  console.log('\nScreenshots')
  for (const width of [1440, 1024]) {
    const page = await newPage('priya', { width, height: 900 })
    fs.mkdirSync(path.join(out, `screens-${width}`), { recursive: true })
    for (const [i, url] of SCREENS.entries()) {
      await go(page, url)
      await page.waitForTimeout(800)
      const file = path.join(out, `screens-${width}`, `${String(i + 1).padStart(2, '0')}-${slug(url)}.jpg`)
      await page.screenshot({ path: file, type: 'jpeg', quality: 80 })
      console.log(`  ${path.relative(out, file)}`)
    }
    await page.context().close()
  }
}

;(async () => {
  assert.equal((await (await fetch(`${base}/api/v1/config`)).json()).authMode, 'Development')
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  try {
    await fixtures()
    for (const [name, fn] of Object.entries({ crawl, reach, quick, filters, keyboard, axe, signals, deeplinks, shots })) if (CHECKS.includes(name)) await fn()
  } finally {
    await browser.close()
    report.failures = failures
    fs.mkdirSync(out, { recursive: true })
    fs.writeFileSync(path.join(out, `calm-acceptance${process.env.CALM_CHECKS ? `-${CHECKS.join('-')}` : ''}.json`), JSON.stringify(report, null, 2))
  }
  if (failures.length) { console.error(`\nFAIL: ${failures.length}\n${failures.join('\n')}`); process.exitCode = 1 }
  else console.log(`\nPASS: ${CHECKS.join(', ')}. Report in ${out}`)
})().catch((e) => { console.error(e); process.exitCode = 1 })
