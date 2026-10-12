// Calm workspace census (packet 037, verification.md appendix A) with the section 4 thresholds, run read-only against
// the local Development preview with its synthetic fixture projects. Nothing is written.
//   TUESDAY_UI_BASE_URL (default http://localhost:5173), CLUTTER_PERSONAS (default priya,sam),
//   CLUTTER_SCREENS (comma-separated path filter, for quick local runs).
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://localhost:5173'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'TUESDAY_UI_BASE_URL must be a local Development preview')
const project = 'SYN-101'
const personas = (process.env.CLUTTER_PERSONAS ?? 'priya,sam').split(',')
const navPersonas = ['jordan', 'lena', 'sam', 'priya', 'alex', 'rita']
// [path, baseline chrome words (section 2, the 5800e3fc re-measure), data selector for screens without table rows]
const SCREENS = [
  ['/home', 114, 'section, [data-accent].rounded-lg'],
  ['/my-work', 78, 'section li'],
  ['/boards', 140, 'section'],
  ['/projects', 33],
  ['/tasks', 43],
  ['/calendar', 150, '[role=region]'],
  ['/portfolio', 58],
  ['/workload', 112],
  ['/planner', 63],
  ['/reports', 169, 'li'],
  ['/team', 16],
  [`/projects/${project}/dashboard`, 168],
  [`/projects/${project}/tasks`, 108],
  [`/projects/${project}/deliverables`, 110],
  [`/projects/${project}/timeline`, 185, '[role=region]'],
  [`/projects/${project}/allocations`, 98],
  [`/projects/${project}/coordination`, 232, 'section.rounded-lg'],
].filter(([p]) => !process.env.CLUTTER_SCREENS || process.env.CLUTTER_SCREENS.split(',').includes(p))
// Words: at most half the baseline, but never below 40 (a title, a scope line and a toolbar need that many).
const LIMIT = { firstDataY: 405, filterFields: 1, chromeControls: 25, wordsShare: 0.5, wordsFloor: 40, fills: 6, groupTabs: 7, sidebarItems: 8 }
const wordLimit = (baseWords) => Math.max(LIMIT.wordsFloor, Math.floor(baseWords * LIMIT.wordsShare))

// Appendix A, apart from the extra data selector, the navigation counts, the fill rule and the data rule. Fill rule: a
// fill counts only when it is chromatic (HSL saturation >= 15 % and lightness 8-96 %, composited on white), so greys,
// near-whites and near-blacks are not colours. Data rule: list screens hold their data in tables and grids, while
// dashboards (R9), the My Work focus list (R10), boards, calendars and register cards hold it in data items (articles
// and list items outside navigation), which the first-data-row measure already counts as data; neither is chrome.
function census(extraData) {
  const H = innerHeight
  const vis = el => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && s.opacity !== '0' }
  const main = document.querySelector('main') || document.body
  const inData = el => !!el.closest('table, [role=grid], [role=table], [role=treegrid], [role=dialog]') || (!!el.closest('article, li, [role=listitem]') && !el.closest('nav'))
  const fold = el => { const r = el.getBoundingClientRect(); return r.top < H && r.bottom > 0 }
  const all = [...main.querySelectorAll('button, a[href], input, select, textarea, [role=button], [role=tab], [role=checkbox], [role=combobox], [role=switch]')].filter(vis)
  const chrome = all.filter(e => !inData(e))
  const fields = chrome.filter(e => e.matches('input:not([type=checkbox]):not([type=radio]), select, textarea, [role=combobox]'))
  const dataSel = ['table tbody tr, [role=row], [role=gridcell], article', extraData].filter(Boolean).join(', ')
  const firstData = [...main.querySelectorAll(dataSel)].filter(vis).map(e => e.getBoundingClientRect().top).sort((a, b) => a - b)[0]
  let cw = 0; const tw = document.createTreeWalker(main, NodeFilter.SHOW_TEXT)
  while (tw.nextNode()) { const n = tw.currentNode, p = n.parentElement; if (!p || !vis(p) || inData(p) || !fold(p)) continue; cw += n.textContent.split(/\s+/).filter(Boolean).length }
  const ctx = Object.assign(document.createElement('canvas'), { width: 1, height: 1 }).getContext('2d', { willReadFrequently: true })
  const chromatic = (c) => {
    ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, 1, 1); ctx.fillStyle = c; ctx.fillRect(0, 0, 1, 1) // any CSS colour space, on white
    const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data, max = Math.max(r, g, b) / 255, min = Math.min(r, g, b) / 255
    const l = (max + min) / 2, s = max === min ? 0 : (max - min) / (1 - Math.abs(2 * l - 1))
    return s >= 0.15 && l >= 0.08 && l <= 0.96 ? `rgb(${r}, ${g}, ${b})` : null
  }
  const fills = new Set([...main.querySelectorAll('*')].filter(vis).filter(fold).map(e => chromatic(getComputedStyle(e).backgroundColor)).filter(Boolean))
  const tabs = document.querySelector('main nav[aria-label="Project sections"]')
  const sidebar = document.querySelector('nav[aria-label="Main navigation"]')
  return {
    chromeControls: chrome.filter(fold).length, filterFields: fields.length, chromeWords: cw,
    firstDataY: firstData == null ? null : Math.round(firstData), fills: fills.size,
    groupTabs: tabs ? [...tabs.querySelectorAll('a[href]')].filter(vis).length : null,
    sidebarItems: sidebar ? [...sidebar.querySelectorAll('a[href]:not([href="/"]), button[aria-expanded]')].filter(vis).length : null,
  }
}

;(async () => {
  assert.equal((await (await fetch(`${base}/api/v1/config`)).json()).authMode, 'Development')
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const failures = [], empty = []
  const fail = (id, who, where, value, limit) => failures.push(`${id} ${who} ${where}: ${value} (limit ${limit})`)
  const open = async (who, path) => {
    const page = await browser.newPage({ viewport: { width: 1600, height: 900 }, reducedMotion: 'reduce' })
    await page.addInitScript((email) => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
    await page.goto(`${base}${path}`)
    await page.locator('main').waitFor()
    await page.waitForLoadState('networkidle').catch(() => undefined)
    await page.waitForTimeout(1500)
    return page
  }
  try {
    for (const who of personas) {
      const rows = []
      for (const [path, baseWords, extra] of SCREENS) {
        const page = await open(who, path)
        const r = await page.evaluate(census, extra ?? null)
        await page.close()
        rows.push({ path, ...r })
        // An empty state has no first row to measure; it is listed, not counted as a pass or a failure.
        if (r.firstDataY == null) empty.push(`${who} ${path}`)
        else if (r.firstDataY > LIMIT.firstDataY) fail('AC-CALM-01', who, path, r.firstDataY, LIMIT.firstDataY)
        if (r.filterFields > LIMIT.filterFields) fail('AC-CALM-02', who, path, r.filterFields, LIMIT.filterFields)
        if (r.chromeControls > LIMIT.chromeControls) fail('AC-CALM-03', who, path, r.chromeControls, LIMIT.chromeControls)
        if (r.groupTabs != null && r.groupTabs > LIMIT.groupTabs) fail('AC-CALM-04', who, `${path} project tabs`, r.groupTabs, LIMIT.groupTabs)
        if (r.chromeWords > wordLimit(baseWords)) fail('AC-CALM-05', who, `${path} words`, r.chromeWords, wordLimit(baseWords))
        if (r.fills > LIMIT.fills) fail('AC-CALM-05', who, `${path} fills`, r.fills, LIMIT.fills)
      }
      console.log(`\nCensus as ${who} at 1600x900`)
      console.table(rows.map(({ sidebarItems, ...r }) => r))
    }
    const nav = []
    for (const who of navPersonas) {
      const page = await open(who, '/my-work')
      const { sidebarItems } = await page.evaluate(census, null)
      await page.close()
      nav.push({ who, sidebarItems })
      if (sidebarItems == null || sidebarItems > LIMIT.sidebarItems) fail('AC-CALM-04', who, 'sidebar items', sidebarItems, LIMIT.sidebarItems)
    }
    console.log('\nSidebar items on /my-work')
    console.table(nav)
  } finally { await browser.close() }
  if (empty.length) console.log(`\nNo data rows to measure (empty state, AC-CALM-01 unmeasured): ${empty.join(', ')}`)
  if (failures.length) { console.error(`\nFAIL: ${failures.length} threshold(s) exceeded\n${failures.join('\n')}`); process.exitCode = 1 }
  else console.log('\nPASS: AC-CALM-01 to -05 thresholds hold on every screen.')
})().catch((e) => { console.error(e); process.exitCode = 1 })
