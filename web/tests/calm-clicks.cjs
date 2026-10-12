// AC-CALM-10 click log (specs/037-calm-workspace/verification.md section 4): the six common paths, clicked through in the
// UI under test from the landing page (/my-work), counting every click. Run it on the base commit's UI with
// CALM_VERSION=base and on this one with CALM_VERSION=now; the steps differ only where the layout does.
// Counting: a choice in a select is two clicks (open, pick); typing is logged as keystrokes, not clicks. The last click of a
// path that would save (planning, owner) or download (export) is counted once the control is shown and enabled, and not
// made, so the data stays unchanged. Where a path has a second common route (a filter instead of a dashboard link), both
// are logged and the path's count is its shorter route.
//   TUESDAY_UI_BASE_URL (default http://localhost:5173), CALM_VERSION (base | now), CALM_OUT (JSON log path, optional)
const assert = require('node:assert/strict')
const fs = require('node:fs')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://localhost:5173'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'TUESDAY_UI_BASE_URL must be a local Development preview')
const v = process.env.CALM_VERSION
assert.ok(['base', 'now'].includes(v), 'CALM_VERSION must be base or now')
const P = 'SYN-101'
const nextWeek = (() => { const d = new Date(); d.setDate(d.getDate() - ((d.getDay() + 6) % 7) + 7); return d.toISOString().slice(0, 10) })()

const side = (href) => `nav[aria-label="Main navigation"] a[href="${href}"]:visible`
const tab = (seg) => `main nav[aria-label="Project sections"] a[href="/projects/${P}/${seg}"]`
const filters = 'main button:text-matches("^Filters")'
const toProject = [['click', side('/projects'), 'sidebar Projects'], ['click', `main a[href="/projects/${P}"]`, `${P} in the project list`]]

// [path, who, route, steps, check]; a step is [kind, selector, note, value]
const ROUTES = [
  ['1 Open my most overdue task from My Work', 'alex', 'My Work list', [
    ['click', `${v === 'base' ? '#mw-tasks' : '#mw-today'} li >> nth=0 >> button >> nth=1`, 'first task in the Overdue group'],
  ], async (p) => p.getByRole('button', { name: 'Open full page' }).isVisible()],
  ['2 Show SYN-101 Civil tasks that are overdue', 'priya', 'dashboard discipline row', [
    ...toProject, ['click', 'main tr:has(a:text-is("Civil")) a[href$="&overdue=true"]', 'Civil row, Overdue count'],
  ], async (p) => /disciplineId=/.test(p.url()) && /overdue=true/.test(p.url())],
  ['2 Show SYN-101 Civil tasks that are overdue', 'priya', 'Tasks filters', [
    ...toProject, ['click', tab('tasks'), v === 'base' ? 'Tasks tab' : 'Work group (opens Tasks)'],
    ...(v === 'now' ? [['click', filters, 'Filters']] : []),
    ['choose', '#tasks-discipline', 'Discipline: Civil', 'Civil'],
    ['click', `${v === 'base' ? 'main' : '[role=dialog]'} button[aria-pressed]:has-text("Overdue")`, 'Overdue chip'],
  ], async (p) => /disciplineId=/.test(p.url()) && /overdue=true/.test(p.url())],
  ['3 Open SYN-101 high risks', 'priya', 'dashboard tile', [
    ...toProject, ['click', 'main a[href*="/risks?"][href*="severity=High"]', 'High risks count'],
  ], async (p) => /\/risks\?.*severity=High/.test(p.url())],
  ['3 Open SYN-101 high risks', 'priya', 'Risks filters', [
    ...toProject,
    ...(v === 'base' ? [['click', tab('risks'), 'Risks tab']] : [['click', tab('coordination'), 'Coordination group'], ['click', `main nav a[href="/projects/${P}/risks"]`, 'Risks page']]),
    ...(v === 'now' ? [['click', filters, 'Filters']] : []),
    ['choose', '#register-severity', 'Severity: High', 'High'],
  ], async (p) => /\/risks\?.*severity=High/.test(p.url())],
  ['4 Add 8 h of planning for Alex next week', 'sam', 'Planner quick add', [
    ['click', side('/planner'), v === 'base' ? 'sidebar Team Planner' : 'sidebar Resources (opens the Planner)'],
    ['click', `button[aria-label^="Alex Chen, ${nextWeek}:"]`, `Alex, week of ${nextWeek}`],
    ['type', 'input[aria-label="Quick add planning entry"]', 'type the hours', '8 h'],
    ['key', 'Enter', 'Enter saves (not pressed)'],
  ], async (p) => p.locator('input[aria-label="Quick add planning entry"]').isVisible()],
  ['5 Export SYN-101 deliverables', 'priya', 'Deliverables export', [
    ...toProject, ['click', tab('deliverables'), v === 'base' ? 'Deliverables tab' : 'Deliverables group'],
    ['click', 'main button:has-text("Export")', 'Export'],
    ['last', '[role=menuitem]:has-text("CSV")', 'CSV (download not started)'],
  ], async () => true],
  ['6 Change a task\'s owner', 'priya', 'task panel', [
    ['click', side('/tasks'), 'sidebar Tasks'],
    ['click', 'main button:has-text("SYN-101-T0001") >> nth=0', 'task SYN-101-T0001'],
    ['click', 'button[data-slot=popover-trigger]:has-text("Alex Chen") >> nth=-1', 'Owner field'],
    ['last', '[cmdk-item]:not(:has-text("Alex Chen")) >> nth=0', 'another person (not saved)'],
  ], async () => true],
]

;(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const log = []
  let failed = 0
  try {
    for (const [path, who, route, steps, check] of ROUTES) {
      const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' })
      await ctx.addInitScript((u) => { sessionStorage.setItem('hub.devUser', u); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
      const page = await ctx.newPage()
      await page.goto(`${base}/my-work`); await page.waitForLoadState('networkidle'); await page.waitForTimeout(800)
      let clicks = 0, keys = 0
      const done = []
      try {
        for (const [kind, sel, note, value] of steps) {
          if (kind === 'key') { keys += 1; done.push({ kind, note }); continue }
          const loc = page.locator(sel).filter({ visible: true }).first() // phone layouts keep hidden copies of some links
          await loc.waitFor({ state: 'visible', timeout: 15000 })
          if (kind === 'click') { await loc.click(); clicks += 1 }
          else if (kind === 'choose') { await loc.selectOption({ label: value }); clicks += 2 }
          else if (kind === 'type') { await loc.fill(value); keys += value.length }
          else if (kind === 'last') { assert.ok(await loc.isEnabled(), `${note} is disabled`); clicks += 1 }
          await page.waitForLoadState('networkidle').catch(() => undefined); await page.waitForTimeout(500)
          done.push({ kind, note, clicks, url: page.url().replace(base, '') })
        }
        const ok = await check(page)
        log.push({ path, who, route, clicks, keys, ok, end: page.url().replace(base, ''), steps: done })
        console.log(`${ok ? '✓' : '✗'} ${path} [${route}] as ${who}: ${clicks} clicks${keys ? `, ${keys} keystrokes` : ''} → ${page.url().replace(base, '')}`)
        if (!ok) failed++
      } catch (e) {
        failed++
        log.push({ path, who, route, error: String(e).split('\n')[0], steps: done })
        console.log(`✗ ${path} [${route}] as ${who}: ${String(e).split('\n')[0]} after ${JSON.stringify(done)}`)
      }
      await ctx.close()
    }
  } finally { await browser.close() }
  const paths = [...new Set(log.map((r) => r.path))].map((p) => ({ path: p, clicks: Math.min(...log.filter((r) => r.path === p && r.ok).map((r) => r.clicks)) }))
  console.log(`\n${v}: ${paths.map((x) => `${x.path.split(' ')[0]}=${x.clicks}`).join(', ')}`)
  if (process.env.CALM_OUT) fs.writeFileSync(process.env.CALM_OUT, JSON.stringify({ version: v, base, nextWeek, at: new Date().toISOString(), paths, log }, null, 2))
  if (failed) process.exitCode = 1
})().catch((e) => { console.error(e); process.exitCode = 1 })
