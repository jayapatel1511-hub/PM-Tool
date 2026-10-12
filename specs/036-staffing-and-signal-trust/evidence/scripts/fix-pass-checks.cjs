// Browser checks for the run 2 fix pass, read-only, against the HEAD API and UI on 5098 (preview database).
const { chromium } = require(require('path').resolve(__dirname, '../../../../web/node_modules/playwright'))
const fs = require('fs')
const AXE = fs.readFileSync(require('path').resolve(__dirname, '../../../../web/node_modules/axe-core/axe.min.js'), 'utf8')
const base = 'http://localhost:5098'
const [outDir] = process.argv.slice(2)
const api = async (who, path) => (await fetch(`${base}/api/v1/${path}`, { headers: { 'X-Dev-User': `${who}@hub.test` } })).json()
const results = {}
;(async () => {
  const browser = await chromium.launch({ headless: true })
  const open = async (who, w = 1600, h = 1000) => { const ctx = await browser.newContext({ viewport: { width: w, height: h }, reducedMotion: 'reduce', bypassCSP: true }); await ctx.addInitScript((e) => { sessionStorage.setItem('hub.devUser', e); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`); const pg = await ctx.newPage(); pg.setDefaultTimeout(15000); return pg }
  const settle = async (page, ms = 1200) => { await page.locator('main').waitFor(); await page.waitForLoadState('networkidle').catch(() => {}); await page.waitForTimeout(ms) }
  const shot = (page, name, clip) => page.screenshot({ path: `${outDir}/${name}.png`, ...(clip ? { clip } : {}) })
  const users = Object.fromEntries((await api('jordan', 'users')).map((u) => [u.displayName, u.id]))
  const p101 = await api('jordan', 'projects/SYN-101'), p104 = await api('jordan', 'projects/SYN-104'), p105 = await api('jordan', 'projects/SYN-105')

  // 1. Picker: a person whose planned work includes a project the viewer cannot see shows "Partial view" (grid row partial).
  {
    const tasks = (await api('jordan', `projects/${p101.id}/tasks?pageSize=200`)).items
    const t4 = tasks.find((t) => t.key === 'SYN-101-T0004')
    const page = await open('sam')
    const grid = []
    await page.route('**/api/v1/planning/grid?**', async (route) => {
      grid.push(decodeURIComponent(route.request().url()))
      const r = await route.fetch(); const j = await r.json()
      j.people = j.people.map((p) => (p.id === users['Jill Martin'] ? { ...p, partial: true } : p))
      await route.fulfill({ response: r, json: j })
    })
    await page.goto(`${base}/projects/SYN-101/tasks?panel=Task:${t4.id}`); await settle(page, 1500)
    await page.locator('div.grid', { has: page.getByText('Assignee', { exact: true }) }).first().getByRole('button').first().click()
    await page.locator('[data-capacity]').first().waitFor(); await page.waitForTimeout(600)
    const items = await page.locator('[cmdk-item]').evaluateAll((els) => els.map((e) => ({ name: e.querySelector('span.truncate')?.textContent, figure: e.querySelector('[data-capacity]')?.textContent ?? null, title: e.querySelector('[data-capacity]')?.getAttribute('title') ?? null })).filter((x) => x.figure))
    await shot(page, 'fix-picker-partial-view', { x: 900, y: 0, width: 700, height: 700 })
    const jill = items.find((x) => x.name === 'Jill Martin'), others = items.filter((x) => x.name !== 'Jill Martin')
    results.pickerPartial = { pass: !!jill && jill.figure.startsWith('Partial view') && others.every((x) => / free| over/.test(x.figure)) && grid.length === 1, requests: grid.length, items }
    await page.context().close()
  }

  console.log('step 2', Object.keys(results).join(','))
  // 2. Timeline: a deliverable link's tooltip names its lag (payload lagDays), a derived one names the type only.
  {
    const page = await open('priya')
    await page.route(`**/api/v1/projects/${p101.id}/timeline`, async (route) => {
      const r = await route.fetch(); const j = await r.json()
      const [a, b] = j.deliverables.filter((d) => d.dueDate)
      j.deliverableLinks = [...j.deliverableLinks.filter((l) => !(l.from === a.id && l.to === b.id)), { from: a.id, to: b.id, type: 'FinishToStart', lagDays: 5, highlighted: false, derived: false }]
      results.timelineInjected = { from: a.key, to: b.key }
      await route.fulfill({ response: r, json: j })
    })
    await page.goto(`${base}/projects/SYN-101/timeline`); await settle(page, 1500)
    const titles = await page.locator('svg g > title').allTextContents()
    results.timelineLag = { pass: titles.includes('Finish-to-Start · lag 5 working days'), titles }
    await page.context().close()
    const real = await api('priya', `projects/${p101.id}/timeline`)
    results.timelinePayload = { deliverableLinks: real.deliverableLinks }
  }

  console.log('step 3', Object.keys(results).join(','))
  // 3. Portfolio with a filter set: each health and Active tile count equals the rows its link opens (AC-CNT-01).
  {
    const page = await open('priya')
    const rows = []
    page.on('response', async (r) => { if (new URL(r.url()).pathname === '/api/v1/portfolio') { const j = await r.json().catch(() => null); if (j) rows.push({ url: decodeURIComponent(r.url()), n: j.projects.length, tiles: j.tiles }) } })
    await page.goto(`${base}/portfolio?computedHealth=Red`); await settle(page)
    const tiles = rows.at(-1).tiles
    const checks = []
    for (const h of ['Red', 'Yellow', 'Green']) {
      const href = await page.locator(`a[href*="computedHealth=${h}"][href*="status=Active"]`).first().getAttribute('href')
      const p2 = await open('priya')
      const seen = []
      p2.on('requestfinished', async (rq) => { if (rq.url().includes('/api/v1/portfolio?')) { const r = await rq.response(); seen.push((await r.json()).projects.length) } })
      await p2.goto(`${base}${href}`); await p2.waitForTimeout(4000)
      const rendered = seen.at(-1)
      checks.push({ tile: h, count: tiles[h.toLowerCase()], href, rows: rendered }); await p2.context().close()
    }
    await page.getByRole('button', { name: /Active/ }).first().click(); await settle(page, 800)
    checks.push({ tile: 'Active', count: tiles.active, href: page.url().replace(base, ''), rows: rows.at(-1).n })
    await page.goto(`${base}/portfolio?computedHealth=Red`); await settle(page)
    await shot(page, 'fix-portfolio-filtered-tiles', { x: 232, y: 64, width: 1368, height: 360 })
    results.portfolioFiltered = { pass: checks.every((c) => c.count === c.rows), checks }
    await page.context().close()
  }

  console.log('step 4', Object.keys(results).join(','))
  // 4. The batched assignment notice's link opens My Work Overview with the project filter, not Today.
  {
    const page = await open('alex')
    const count = async (url) => { await page.goto(`${base}${url}`); await settle(page); return { url, view: await page.getByRole('button', { name: /^My Work views:/ }).getAttribute('aria-label'), chip: await page.getByRole('group', { name: 'Active filters' }).innerText().catch(() => null), items: await page.locator('main section li').count() } }
    const now = await count(`/my-work?tab=overview&project=${p105.id}`), old = await count(`/my-work?project=${p105.id}`)
    results.myWorkLink = { pass: now.view.endsWith('Overview') && now.items > old.items, now, old }
    await page.context().close()
  }

  console.log('step 5', Object.keys(results).join(','))
  // 5. Workload: Under-assigned names the two weeks it is judged on (R7).
  {
    const page = await open('jordan')
    await page.goto(`${base}/workload`); await settle(page)
    const labels = (await page.locator('tbody tr th').allInnerTexts()).map((x) => x.replace(/\s+/g, ' ')).filter((x) => x.includes('Under-assigned')).map((x) => x.slice(x.indexOf('Under-assigned')))
    results.workloadUnder = { pass: labels.length > 0 && labels.every((l) => /^Under-assigned · \w{3} \d+, \w{3} \d+$/.test(l.trim())), rows: labels.length, labels: [...new Set(labels)] }
    await page.context().close()
  }

  console.log('step 6', Object.keys(results).join(','))
  // 6. Planner: the Team total names its bands; group subtotals say they include Possible.
  {
    const page = await open('lena')
    await page.goto(`${base}/planner`); await settle(page)
    const foot = await page.locator('tfoot th').innerText()
    const sub = await page.locator('td[data-subtotal]').first().getAttribute('title')
    results.plannerTotals = { pass: foot.includes('Confirmed + Expected') && sub === 'Planned (Confirmed + Expected + Possible) of capacity', foot, subtotalTitle: sub }
    await page.context().close()
  }

  console.log('step 7', Object.keys(results).join(','))
  // 7. Allocations: the open roles switch counts open ones in words; the person chip falls back to "Selected".
  {
    const page = await open('priya')
    await page.goto(`${base}/projects/SYN-104/allocations`); await settle(page)
    const sw = await page.getByRole('radio', { name: /^Open roles/ }).or(page.getByRole('button', { name: /^Open roles/ })).first().innerText()
    const roles = await api('priya', `projects/${p104.id}/open-roles`)
    await page.goto(`${base}/projects/SYN-101/allocations?personId=00000000-0000-0000-0000-000000000001`); await settle(page)
    const chip = await page.getByRole('group', { name: 'Active filters' }).innerText()
    results.allocations = { pass: sw.trim() === `Open roles · ${roles.filter((r) => r.status === 'Open').length} open` && /Person: Selected/.test(chip.replace(/\s+/g, ' ')), switchLabel: sw.trim(), statuses: roles.map((r) => r.status), chip: chip.replace(/\s+/g, ' ') }
    await page.context().close()
  }

  console.log('step 8', Object.keys(results).join(','))
  // 8. axe with a toolbar menu open: no aria-hidden-focus now that menus are not modal.
  {
    const out = []
    for (const [who, path, name] of [['priya', '/projects/SYN-101/tasks', /^Group/], ['priya', '/workload', /^Sort/], ['priya', '/my-work', /^My Work views/], ['sam', '/planner', /^Group/]]) {
      const page = await open(who)
      await page.goto(`${base}${path}`); await settle(page)
      await page.getByRole('button', { name }).first().click(); await page.waitForTimeout(400)
      const hidden = await page.evaluate(() => [...document.querySelectorAll('[aria-hidden=true]')].filter((e) => e.id === 'root' || e.querySelector('a[href="#main"], a[href^="#"]')).length)
      await page.addScriptTag({ content: AXE })
      const v = await page.evaluate(async () => (await axe.run(document, { runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] })).violations.filter((x) => ['serious', 'critical'].includes(x.impact)).map((x) => x.id))
      out.push({ who, path, menuOpen: await page.getByRole('menu').count(), ariaHiddenAncestors: hidden, seriousOrCritical: v })
      await page.context().close()
    }
    results.axeMenuOpen = { pass: out.every((x) => x.menuOpen === 1 && x.seriousOrCritical.length === 0), screens: out }
  }
  await browser.close()
  fs.writeFileSync(`${outDir}/fix-pass-checks.json`, JSON.stringify(results, null, 2))
  for (const [k, v] of Object.entries(results)) if ('pass' in v) console.log(`${v.pass ? 'PASS' : 'FAIL'} ${k}`)
})().catch((e) => { console.error(e); process.exitCode = 1 })
