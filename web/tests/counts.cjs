// SC-002 count reconciliation (packet 036 verification.md section 7), read-only against the local Development preview
// with the section 8 fixtures loaded. For Jordan, Priya and Sam (signed in through the development picker), and Omar as a
// Discipline Lead (packet 045 AC-RCV-08), every linked count on Home, the SYN-101..103 Project Dashboards (also scoped to each
// discipline) and Milestones views they can see and Portfolio (each row's counts, AC-RCV-10) is compared with the total of the
// list request its destination page sends, as the same person. With flag.status_rollups on, the discipline status segments
// (AC-RCV-18), the Overdue deliverables column and Portfolio grouped by client, office and project type (AC-RCV-19) are
// checked too; a summed figure (a group subtotal, "n of m issued") is compared with the sum of the figures it adds up.
// Fails on any difference, on a list that drops the link's filters, and on a missing agreed tile.
//   TUESDAY_UI_BASE_URL (default http://localhost:5173), COUNTS_REPORT (JSON report path),
//   TUESDAY_COUNTS_PROJECTS (space-separated fixture project numbers; defaults SYN-101..103).
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://localhost:5173'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'TUESDAY_UI_BASE_URL must be a local Development preview')
const reportPath = process.env.COUNTS_REPORT ?? require('node:path').join(require('node:os').tmpdir(), 'tuesday-counts-report.json')
const PERSONAS = [['jordan', 'Jordan Lee'], ['priya', 'Priya Nair'], ['sam', 'Sam Patel'], ['omar', 'Omar Haddad']]
const PROJECTS = (process.env.TUESDAY_COUNTS_PROJECTS ?? 'SYN-101 SYN-102 SYN-103').split(/[\s,]+/).filter(Boolean)
assert.ok(PROJECTS.length > 0, 'at least one count-reconciliation project is required')
// Agreed tile list (section 7). Lanes, project segments and discipline rows come from the page's own data.
const AGREED = {
  home: ['Active Projects', 'Open Tasks', 'Overdue Tasks', 'On Track · Green', 'On Track · evaluated', 'Team Members'],
  dashboard: [...['Total', 'Complete', 'In progress', 'In review', 'Overdue', 'Blocked', 'Waiting', 'Unassigned'].map((x) => `Tasks · ${x}`),
    ...['Total', 'Due ≤ 14 d', 'At risk', 'Issued'].map((x) => `Deliverables · ${x}`), 'Decisions · Open', 'Decisions · Overdue',
    'Issues and risks · Open issues', 'Issues and risks · High issues', 'Issues and risks · High risks', 'Due this week', 'Blocked'],
  portfolio: ['Red (computed)', 'Yellow (computed)', 'Green (computed)', 'Active projects', 'Submissions in 14 days'],
}
const VIEW = new Set(['panel', 'group', 'sort', 'view', 'tab', 'layout', 'page', 'side', 'swim', 'ws']) // link parameters that are layout, not filters
const BUDGET = 300 // API requests per person per minute: half the API's 600, so other sessions as the same person keep working
const PLUMBING = new Set(['page', 'pagesize', 'sort', 'includetasks', 'projects', 'mine']) // request parameters that are not filters

// The generated client preserves the contract's PascalCase parameter names while links use the
// product's lower-case URL names. Matching names case-insensitively keeps this reconciliation
// about filter semantics; values are still compared exactly below.
const queryValue = (params, key) => params.get([...params.keys()].find((k) => k.toLowerCase() === key.toLowerCase()) ?? key)

const api = async (who, endpoint) => fetch(`${base}/api/v1/${endpoint}`, { headers: { 'X-Dev-User': `${who}@hub.test` } })

/** Runs in the page: every count tile and linked count, as { name, shown, href } (href null for a clickable tile). */
function extract() {
  const own = (el) => { const c = el.cloneNode(true); c.querySelectorAll('button, .sr-only, [aria-hidden="true"]').forEach((x) => x.remove()); return c.textContent.replace(/\s+/g, ' ').trim() }
  const num = (s) => { const m = (s ?? '').replace(/,/g, '').match(/\d+/); return m ? Number(m[0]) : null }
  const linked = (a, prefix) => {
    const label = [...a.querySelectorAll('span')].map(own).find((s) => /[a-z]/i.test(s) && !/^\d/.test(s)) ?? own(a).replace(/\d+/g, '').trim()
    return { name: prefix ? `${prefix} · ${label}` : label, shown: num(own(a).replace(label, '')), href: a.getAttribute('href') }
  }
  const main = document.querySelector('main')
  const out = []
  // Summary tiles: a label span, then a value span. A tile is itself a link, a filter button, or holds links.
  for (const tile of main.querySelectorAll('a[data-accent], button[data-accent], div[data-accent]')) {
    // A linked tile that also holds links is a box whose first child is the link with the label and the value.
    const head = tile.tagName === 'DIV' && tile.firstElementChild?.tagName === 'A' ? tile.firstElementChild : null
    const [label, value] = (head ?? tile).children
    if (label?.tagName !== 'SPAN' || value?.tagName !== 'SPAN') continue
    const title = own(label).replace(/Whole project$/, '') // a scoped dashboard marks the tiles that stay project-wide
    if (head) { const unit = own(value).replace(/[\d,]+/g, '').trim(); out.push({ name: unit ? `${title} · ${unit}` : title, shown: num(own(value)), href: head.getAttribute('href') }) }
    if (tile.tagName !== 'DIV') out.push({ name: title, shown: num(own(value)), href: tile.tagName === 'A' ? tile.getAttribute('href') : null })
    else tile.querySelectorAll('a[href]').forEach((a) => { if (a !== head) out.push(linked(a, title)) })
  }
  // Home: lanes and the On hold count, then project segments (their count is in the title) and project totals.
  const lanes = new Map() // a lane's bar and number share one link; keep the one that shows the number
  for (const a of main.querySelectorAll('#h-status a[href]')) if (!lanes.has(a.getAttribute('href')) || /\d/.test(own(a))) lanes.set(a.getAttribute('href'), a)
  for (const [href, a] of lanes) {
    const row = a.closest('li') ?? a.parentElement
    out.push({ name: `Tasks by Status · ${own(row.querySelector('span') ?? row).replace(/\d+$/, '').trim()}`, shown: num(own(a)) ?? num((a.getAttribute('aria-label') ?? '').split(':').pop()), href })
  }
  for (const a of main.querySelectorAll('#h-projects a[href]')) {
    const segment = a.getAttribute('aria-label')
    out.push(segment ? { name: `Tasks by Project · ${segment.split(':')[0]}`, shown: num((a.title || segment).split(':').pop()), href: a.getAttribute('href') }
      : { name: `Tasks by Project · ${own(a.closest('li').querySelector('span')).trim()} total`, shown: num(own(a)), href: a.getAttribute('href') })
  }
  // Dashboard: the discipline table, then the Due this week and Blocked cards (their link appears only beyond the shown rows).
  for (const tr of main.querySelectorAll('#disciplines tbody tr')) {
    const discipline = own(tr.cells[0])
    for (const [flag, label] of [['open=true', 'Open'], ['overdue=true', 'Overdue'], ['blocked=true', 'Blocked'], ['dueTo=', 'Due ≤ 14 d']]) {
      const a = tr.querySelector(`a[href*="${flag}"]`)
      if (a) out.push({ name: `${discipline} · ${label}`, shown: num(own(a)), href: a.getAttribute('href') })
    }
    // Packet 045 status bar: each segment names its group and count; "n of m issued" adds them up.
    const segments = [...tr.querySelectorAll('a[aria-label][href*="/deliverables?"]')].map((a) => {
      const [label, n] = a.getAttribute('aria-label').split(': ')
      return { name: `${discipline} · ${label}`, shown: num(n), href: a.getAttribute('href') }
    })
    if (segments.length) {
      const [issued, total] = (own(tr.querySelector('a[aria-label][href*="/deliverables?"]').closest('td').querySelector(':scope > div > span')).match(/\d+/g) ?? []).map(Number)
      out.push(...segments, { name: `${discipline} · issued of`, shown: total, sum: segments.reduce((n, x) => n + x.shown, 0) },
        { name: `${discipline} · issued`, shown: issued, sum: segments.at(2).shown })
    }
  }
  // Milestones view (packet 045 FR-RCV-09): each figure is a link naming itself in data-count, in a row led by the milestone key.
  for (const a of main.querySelectorAll('table a[data-count]')) {
    const key = own(a.closest('tr').cells[1])
    out.push({ name: `${key} · ${a.dataset.count}`, shown: num(own(a)), href: a.getAttribute('href') })
  }
  // Portfolio (packet 045 FR-RCV-09, FR-RCV-15): every row count, then each group header's links and subtotals.
  if (location.pathname === '/portfolio') {
    const heads = [...main.querySelectorAll('table thead th')].map(own)
    const groups = []
    for (const tr of main.querySelectorAll('table tbody tr')) {
      if (tr.dataset.portfolioGroup !== undefined) { groups.push({ tr, rows: [] }); continue }
      const project = decodeURIComponent(tr.cells[0].querySelector('a[href$="/dashboard"]').getAttribute('href').split('/')[2])
      const values = {}
      ;[...tr.cells].forEach((td, i) => td.querySelectorAll('a[href]').forEach((a) => {
        if (!/^\d+$/.test(own(a))) return
        const label = [heads[i], a.querySelector('.sr-only')?.textContent.trim()].filter(Boolean).join(' · ')
        values[label] = num(own(a))
        out.push({ name: `${project} · ${label}`, shown: values[label], href: a.getAttribute('href') })
      }))
      groups.at(-1)?.rows.push(values)
    }
    for (const { tr, rows } of groups) {
      const [th, ...cells] = tr.cells
      const name = own(th).split(' · ')[0]
      th.querySelectorAll('a[href]').forEach((a, i) => out.push({ name: `${name} · ${i ? own(a).replace(/[\d\s]+$/, '') : 'Projects'}`, shown: num(own(a)), href: a.getAttribute('href') }))
      const sum = (label) => rows.reduce((n, r) => n + (r[label] ?? 0), 0)
      cells.slice(0, 5).forEach((td, j) => out.push({ name: `${name} · ${heads[th.colSpan + j]} (sum)`, shown: num(own(td)), sum: sum(heads[th.colSpan + j]) }))
      const [critical, warning] = (own(cells[5]).match(/\d+/g) ?? []).map(Number), attention = heads[th.colSpan + 5]
      out.push({ name: `${name} · ${attention} Critical (sum)`, shown: critical, sum: sum(`${attention} · Critical`) },
        { name: `${name} · ${attention} Warning (sum)`, shown: warning, sum: sum(`${attention} · Warning`) })
    }
  }
  for (const section of main.querySelectorAll('section')) {
    const heading = section.querySelector('h2, h3')
    const title = heading && own(heading).replace(/\d+$/, '').trim()
    if (title === 'Due this week' || title === 'Blocked') {
      const all = [...section.querySelectorAll('a[href]')].find((a) => own(a).startsWith('Show all'))
      out.push({ name: title, shown: num(own(heading)), href: all?.getAttribute('href') ?? null, card: true })
    }
  }
  return out
}

/** Opens the link as the signed-in person and reads the total of the list request that carries exactly its filters.
 *  It navigates inside the app first (fewer requests); a cached list sends nothing, so it then reloads the page. */
async function listTotal(page, href, attempt = 0) {
  const u = new URL(href, base)
  // A Portfolio Attention figure opens the dashboard's attention panel at that severity; its list is the attention request.
  const list = u.pathname.endsWith('/dashboard') && u.searchParams.has('severity') ? u.pathname.replace(/dashboard$/, 'attention') : u.pathname
  const endpoint = new RegExp(`^${list.slice(1).replace(/^projects\/[^/]+\//, 'projects/[^/]+/')}$`)
  const want = [...u.searchParams].filter(([k]) => !VIEW.has(k)).map(([k, v]) => [k.toLowerCase(), v])
  const seen = []
  const match = (r) => {
    const a = new URL(r.url())
    if (!a.pathname.startsWith('/api/v1/') || r.request().method() !== 'GET' || !endpoint.test(a.pathname.slice(8))) return false
    seen.push(a.search)
    const extra = [...a.searchParams.keys()].filter((k) => !PLUMBING.has(k.toLowerCase()) && !want.some(([w]) => w === k.toLowerCase()))
    // The app's query helper leaves out false flags, the API's default, so mine=false may travel as nothing.
    return want.every(([k, v]) => queryValue(a.searchParams, k) === v || (v === 'false' && queryValue(a.searchParams, k) == null)) && extra.length === 0 && (queryValue(a.searchParams, 'page') ?? '1') === '1'
  }
  const response = page.waitForResponse(match, { timeout: attempt ? 30000 : 10000 })
  if (attempt) await page.goto(u.href)
  else await page.evaluate((to) => { history.pushState(null, '', to); dispatchEvent(new PopStateEvent('popstate')) }, u.pathname + u.search)
  const r = await response.catch(() => null)
  if (!r) return attempt ? { total: null, api: seen.length ? `no request with exactly the link's filters; seen ${seen.join(' | ')}` : 'no list request' } : listTotal(page, href, 1)
  if (r.status() === 429 && attempt < 3) { // the API's per-person minute budget, shared with anyone else signed in as this person
    await page.waitForTimeout(Math.min(60, Number(r.headers()['retry-after'] ?? 30)) * 1000)
    return listTotal(page, href, attempt + 1)
  }
  const body = r.status() === 200 ? await r.json().catch(() => null) : null
  if (!body) return { total: null, api: `HTTP ${r.status()} ${r.url().replace(base, '')}` }
  return { total: Array.isArray(body) ? body.length : body.totalCount ?? body.total ?? (body.people ?? body.projects).length, api: r.url().replace(base, '') }
}

/** Opens a source page and waits for its counts to render. */
async function open(page, source, ready, rendered = 'main [data-accent] span') {
  const loaded = page.waitForResponse((r) => new URL(r.url()).pathname.slice(8) === ready && r.request().method() === 'GET', { timeout: 20000 })
  await page.goto(`${base}${source}`)
  const data = await (await loaded).json()
  if (!Array.isArray(data) || data.length) await page.waitForFunction((sel) => document.querySelector(sel), rendered, { timeout: 15000 })
  await page.waitForTimeout(300)
  return data
}

;(async () => {
  assert.equal((await (await fetch(`${base}/api/v1/config`)).json()).authMode, 'Development')
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const rows = [], missing = [], skipped = []
  try {
    for (const [who, displayName] of PERSONAS) {
      const page = await (await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })).newPage()
      const stamps = [], totals = new Map()
      page.on('request', (r) => { if (r.url().includes('/api/v1/')) stamps.push(Date.now()) })
      const pace = async () => { while (stamps.filter((t) => t > Date.now() - 60000).length >= BUDGET) await page.waitForTimeout(1000) }
      await page.goto(`${base}/login`)
      await page.getByRole('button', { name: new RegExp(displayName) }).click()
      await page.waitForURL((u) => !u.pathname.startsWith('/login'), { timeout: 20000 })
      const me = await (await api(who, 'me')).json()
      const sources = [['Home', '/home', 'home', 'home']]
      for (const number of PROJECTS) {
        const p = await api(who, `projects/${number}`)
        if (p.ok) {
          const project = await p.json(), id = project.id
          sources.push([`Dashboard ${number}`, `/projects/${number}/dashboard`, `projects/${id}/dashboard`, 'dashboard'])
          for (const d of project.disciplines.filter((x) => x.isActive))
            sources.push([`Dashboard ${number} · ${d.name}`, `/projects/${number}/dashboard?discipline=${d.id}`, `projects/${id}/dashboard`, 'dashboard'])
          sources.push([`Milestones ${number}`, `/projects/${number}/milestones`, `projects/${id}/milestones`, 'milestones'])
        }
        else skipped.push(`${who}: ${number} not visible (HTTP ${p.status})`)
      }
      if (me.capabilities.portfolio) sources.push(['Portfolio', '/portfolio', 'portfolio', 'portfolio'],
        ...(me.flags?.status_rollups ? ['client', 'office', 'projectType'].map((g) => [`Portfolio by ${g}`, `/portfolio?group=${g}`, 'portfolio', 'portfolio']) : []))
      else skipped.push(`${who}: Portfolio not permitted`)
      sources.push(['Home (all projects)', '/home?projects=all', 'home', 'home']) // last: the app remembers the chosen scope
      for (const [label, source, ready, kind] of sources) {
        await pace()
        const data = await open(page, source, ready, kind === 'milestones' ? 'main table a[data-count]' : undefined)
        const tiles = await page.evaluate(extract)
        const required = (AGREED[kind] ?? []).filter((name) => kind !== 'home' || !name.startsWith('On Track') || data.metrics.onTrack.evaluated > 0)
        if (kind === 'home') {
          // Lanes, On hold and segments are named by their link, so a renamed lane still counts as present.
          const status = (t) => new URL(t.href, base).searchParams.get('status')
          const lanes = tiles.filter((t) => t.name.startsWith('Tasks by Status · '))
          for (const l of [...data.tasksByStatus, ...(data.onHold ? [{ lane: 'On hold', statuses: data.onHold.status }] : [{ lane: 'On hold', statuses: 'On Hold' }])])
            if (!lanes.some((t) => status(t) === l.statuses)) missing.push(`${who} ${label}: Tasks by Status lane ${l.lane} (status=${l.statuses})`)
          for (const p of data.tasksByProject.slice(0, 8)) for (const l of p.lanes.filter((x) => x.count > 0))
            if (!tiles.some((t) => t.href && new URL(t.href, base).searchParams.get('projectId') === p.projectId && status(t) === l.statuses))
              missing.push(`${who} ${label}: Tasks by Project segment ${p.projectNumber} ${l.lane}`)
        }
        if (kind === 'dashboard') {
          required.push(...data.disciplines.flatMap((d) => ['Open', 'Overdue', 'Blocked', 'Due ≤ 14 d'].map((x) => `${d.name} · ${x}`)))
          // Packet 045 status bar: four named segments wherever a discipline has a deliverable outside Cancelled.
          required.push(...data.disciplines.filter((d) => Object.values(data.disciplineStatus?.[d.disciplineId] ?? {}).some((x) => x.value > 0))
            .flatMap((d) => ['Not started', 'In progress', 'Issued', 'On hold'].map((x) => `${d.name} · ${x}`)))
          for (const card of tiles.filter((t) => t.card && !t.href)) card.href = `${source.split('/dashboard')[0]}/tasks?${data[card.name === 'Blocked' ? 'blocked' : 'dueThisWeek'].link}`
        }
        if (kind === 'milestones') required.push(...data.flatMap((m) => ['Issued', 'Deliverables', 'Complete', 'Tasks',
          ...(m.taskOverdue > 0 ? ['Overdue'] : []), ...(m.taskBlocked > 0 ? ['Blocked'] : [])].map((x) => `${m.key} · ${x}`)))
        if (kind === 'portfolio') for (const p of data.projects) { // each row: Overdue, Blocked, Decisions, High issues, Critical, Warning (+ Overdue deliverables)
          const figures = tiles.filter((t) => t.href && t.name.startsWith(`${p.projectNumber} · `)).length
          if (figures !== ('overdueDeliverables' in p ? 7 : 6)) missing.push(`${who} ${label}: ${p.projectNumber} row has ${figures} linked figures`)
        }
        for (const name of required) if (!tiles.some((t) => t.name === name && (t.href || kind === 'portfolio'))) missing.push(`${who} ${label}: ${name}`)
        if (kind === 'portfolio') for (const tile of tiles.filter((t) => !t.href && AGREED.portfolio.includes(t.name))) {
          // A filter tile has no link of its own: clicking it sets the page's filters, and that URL is its list.
          await page.locator('main button[data-accent]', { hasText: tile.name }).first().click()
          await page.waitForURL((u) => u.search.length > 1, { timeout: 5000 }).catch(() => undefined)
          tile.href = new URL(page.url()).pathname + new URL(page.url()).search
          await open(page, source, ready)
        }
        const decisions = kind === 'portfolio' && tiles.find((t) => t.name === 'Overdue decisions')
        if (decisions) { // AC-RCV-10: the tile filters to the projects with an overdue decision and keeps its value, the column's sum
          const filtered = page.waitForResponse((r) => new URL(r.url()).pathname === '/api/v1/portfolio' && new URL(r.url()).searchParams.get('overdueDecisions') === 'true')
          await page.locator('main button[data-accent]', { hasText: decisions.name }).first().click()
          const shown = (await (await filtered).json()).projects.length
          await page.waitForFunction((n) => document.querySelectorAll('main table tbody tr:not([data-portfolio-group])').length === n, shown)
          const column = (await page.evaluate(extract)).filter((t) => t.name.endsWith(' · Decisions overdue'))
          if (column.some((t) => !(t.shown > 0))) missing.push(`${who} ${label}: Overdue decisions filter shows a project without one`)
          decisions.sum = column.reduce((n, t) => n + t.shown, 0)
          decisions.name += ' (sum of the rows it filters to)'
        }
        for (const tile of tiles.filter((t) => t.sum !== undefined))
          rows.push({ persona: who, page: label, tile: tile.name, shown: tile.shown, total: tile.sum, ok: tile.shown != null && tile.shown === tile.sum, href: null, request: 'sum of the figures shown' })
        for (const tile of tiles.filter((t) => t.href)) {
          if (!totals.has(tile.href)) { await pace(); totals.set(tile.href, await listTotal(page, tile.href)) }
          const { total, api: request } = totals.get(tile.href)
          rows.push({ persona: who, page: label, tile: tile.name, shown: tile.shown, total, ok: tile.shown != null && tile.shown === total, href: tile.href, request })
        }
      }
      await page.context().close()
    }
  } finally {
    await browser.close()
  }
  const failures = rows.filter((r) => !r.ok)
  console.table(rows.map(({ persona, page, tile, shown, total, ok }) => ({ persona, page, tile, shown, total, ok })))
  for (const s of skipped) console.log(`SKIPPED ${s}`)
  for (const m of missing) console.log(`MISSING ${m}`)
  for (const f of failures) console.log(`MISMATCH ${f.persona} ${f.page} · ${f.tile}: shown ${f.shown}, list ${f.total} (${f.href} → ${f.request})`)
  fs.mkdirSync(path.dirname(reportPath), { recursive: true })
  fs.writeFileSync(reportPath, JSON.stringify({ base, generatedAt: new Date().toISOString(), checked: rows.length, mismatches: failures.length, missing, skipped, rows }, null, 2))
  console.log(`${rows.length} counts checked, ${failures.length} mismatches, ${missing.length} missing agreed tiles. Report: ${reportPath}`)
  if (failures.length || missing.length) process.exitCode = 1
})().catch((e) => { console.error(e); process.exitCode = 1 })
