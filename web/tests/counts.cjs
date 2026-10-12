// SC-002 count reconciliation (packet 036 verification.md section 7), read-only against the local Development preview
// with the section 8 fixtures loaded. For Jordan, Priya and Sam (signed in through the development picker), every linked
// count on Home, the SYN-101..103 Project Dashboards they can see and Portfolio is compared with the total of the list
// request its destination page sends, as the same person. Fails on any difference, on a list that drops the link's
// filters, and on a missing agreed tile.
//   TUESDAY_UI_BASE_URL (default http://localhost:5173), COUNTS_REPORT (JSON report path).
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://localhost:5173'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'TUESDAY_UI_BASE_URL must be a local Development preview')
const reportPath = process.env.COUNTS_REPORT ?? '/private/tmp/claude-501/-Users-jaypatel-PM-Tool/f879a8c1-966b-47f2-8c07-d393d08b7d6e/scratchpad/counts-report.json'
const PERSONAS = [['jordan', 'Jordan Lee'], ['priya', 'Priya Nair'], ['sam', 'Sam Patel']]
const PROJECTS = ['SYN-101', 'SYN-102', 'SYN-103']
// Agreed tile list (section 7). Lanes, project segments and discipline rows come from the page's own data.
const AGREED = {
  home: ['Active Projects', 'Open Tasks', 'Overdue Tasks', 'On Track · Green', 'On Track · evaluated', 'Team Members'],
  dashboard: [...['Total', 'Complete', 'In progress', 'In review', 'Overdue', 'Blocked', 'Waiting', 'Unassigned'].map((x) => `Tasks · ${x}`),
    ...['Total', 'Due ≤ 14 d', 'At risk', 'Issued'].map((x) => `Deliverables · ${x}`), 'Decisions · Open', 'Decisions · Overdue',
    'Issues and risks · Open issues', 'Issues and risks · High issues', 'Issues and risks · High risks', 'Due this week', 'Blocked'],
  portfolio: ['Red (computed)', 'Yellow (computed)', 'Green (computed)', 'Active projects', 'Submissions in 14 days'],
}
const VIEW = new Set(['panel', 'group', 'sort', 'view', 'page', 'side', 'swim', 'ws']) // link parameters that are layout, not filters
const BUDGET = 300 // API requests per person per minute: half the API's 600, so other sessions as the same person keep working
const PLUMBING = new Set(['page', 'pageSize', 'sort', 'includeTasks', 'projects', 'mine']) // request parameters that are not filters

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
    const [label, value] = tile.children
    if (label?.tagName !== 'SPAN' || value?.tagName !== 'SPAN') continue
    const title = own(label)
    if (tile.tagName !== 'DIV') out.push({ name: title, shown: num(own(value)), href: tile.tagName === 'A' ? tile.getAttribute('href') : null })
    else tile.querySelectorAll('a[href]').forEach((a) => out.push(linked(a, title)))
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
    for (const [flag, label] of [['open', 'Open'], ['overdue', 'Overdue'], ['blocked', 'Blocked']]) {
      const a = tr.querySelector(`a[href*="${flag}=true"]`)
      if (a) out.push({ name: `${discipline} · ${label}`, shown: num(own(a)), href: a.getAttribute('href') })
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
  const endpoint = new RegExp(`^${u.pathname.slice(1).replace(/^projects\/[^/]+\//, 'projects/[^/]+/')}$`)
  const want = [...u.searchParams].filter(([k]) => !VIEW.has(k))
  const seen = []
  const match = (r) => {
    const a = new URL(r.url())
    if (!a.pathname.startsWith('/api/v1/') || r.request().method() !== 'GET' || !endpoint.test(a.pathname.slice(8))) return false
    seen.push(a.search)
    const extra = [...a.searchParams.keys()].filter((k) => !PLUMBING.has(k) && !want.some(([w]) => w === k))
    // The app's query helper leaves out false flags, the API's default, so mine=false may travel as nothing.
    return want.every(([k, v]) => a.searchParams.get(k) === v || (v === 'false' && !a.searchParams.has(k))) && extra.length === 0 && (a.searchParams.get('page') ?? '1') === '1'
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
  return { total: Array.isArray(body) ? body.length : body.totalCount ?? (body.people ?? body.projects).length, api: r.url().replace(base, '') }
}

/** Opens a source page and waits for its counts to render. */
async function open(page, source, ready) {
  const loaded = page.waitForResponse((r) => new URL(r.url()).pathname.slice(8) === ready && r.request().method() === 'GET', { timeout: 20000 })
  await page.goto(`${base}${source}`)
  const data = await (await loaded).json()
  await page.waitForFunction(() => document.querySelector('main [data-accent] span'), null, { timeout: 15000 })
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
        if (p.ok) sources.push([`Dashboard ${number}`, `/projects/${number}/dashboard`, `projects/${(await p.json()).id}/dashboard`, 'dashboard'])
        else skipped.push(`${who}: ${number} not visible (HTTP ${p.status})`)
      }
      if (me.capabilities.portfolio) sources.push(['Portfolio', '/portfolio', 'portfolio', 'portfolio'])
      else skipped.push(`${who}: Portfolio not permitted`)
      sources.push(['Home (all projects)', '/home?projects=all', 'home', 'home']) // last: the app remembers the chosen scope
      for (const [label, source, ready, kind] of sources) {
        await pace()
        const data = await open(page, source, ready)
        const tiles = await page.evaluate(extract)
        const required = AGREED[kind].filter((name) => kind !== 'home' || !name.startsWith('On Track') || data.metrics.onTrack.evaluated > 0)
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
          required.push(...data.disciplines.flatMap((d) => ['Open', 'Overdue', 'Blocked'].map((x) => `${d.name} · ${x}`)))
          for (const card of tiles.filter((t) => t.card && !t.href)) card.href = `${source.replace('/dashboard', '')}/tasks?${data[card.name === 'Blocked' ? 'blocked' : 'dueThisWeek'].link}`
        }
        for (const name of required) if (!tiles.some((t) => t.name === name && (t.href || kind === 'portfolio'))) missing.push(`${who} ${label}: ${name}`)
        if (kind === 'portfolio') for (const tile of tiles.filter((t) => !t.href && AGREED.portfolio.includes(t.name))) {
          // A filter tile has no link of its own: clicking it sets the page's filters, and that URL is its list.
          await page.locator('main button[data-accent]', { hasText: tile.name }).first().click()
          await page.waitForURL((u) => u.search.length > 1, { timeout: 5000 }).catch(() => undefined)
          tile.href = new URL(page.url()).pathname + new URL(page.url()).search
          await open(page, source, ready)
        }
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
