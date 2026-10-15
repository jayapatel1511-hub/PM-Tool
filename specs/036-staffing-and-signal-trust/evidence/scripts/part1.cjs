// Packet 036 acceptance sweep, part 1: read-only browser checks on the shared preview (Vite 5185 -> API 5080).
// Usage: node part1.cjs [section ...]   Results go to results-part1.json; screenshots to the packet evidence folder.
const fs = require('node:fs')
const { open, shot, settle, close, base, api } = require('./lib.cjs')
const axeSrc = fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8')
const only = process.argv.slice(2)
const results = {}
const rec = (id, ok, detail) => { results[id] = { ok, ...detail }; console.log(`${ok ? 'PASS' : 'FAIL'} ${id}`, JSON.stringify(detail)) }
const num = (s) => { const m = String(s ?? '').replace(/,/g, '').replace('−', '-').match(/-?\d+(\.\d+)?/); return m ? Number(m[0]) : null }
const ids = {}
async function project(n) { return ids[n] ??= (await api('jordan', `projects/${n}`)).body }
async function tasksOf(n) { const p = await project(n); return (await api('jordan', `projects/${p.id}/tasks?pageSize=200`)).body.items }
async function axe(page, label) {
  await page.addScriptTag({ content: axeSrc })
  const r = await page.evaluate(() => axe.run(document.querySelector('main') ?? document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } }))
  return r.violations.filter((v) => ['serious', 'critical'].includes(v.impact)).map((v) => `${label} ${v.id}`)
}

const sections = {
  // AC-ROLE-02a: Workload Unfilled roles spreads SYN-104 Civil technologist (80 h, Oct 26 - Nov 20) at 20 h a week.
  async role02a() {
    const page = await open('priya', { width: 1600, height: 1000 })
    await page.goto(`${base}/workload`); await settle(page, 1500)
    const sec = page.locator('#wl-unfilled'); await sec.scrollIntoViewIfNeeded()
    const rows = await sec.locator('tbody tr').evaluateAll((trs) => trs.map((tr) => [...tr.children].map((c) => c.innerText.replace(/\s+/g, ' ').trim())))
    const civil = rows.find((r) => r[0].startsWith('Civil technologist'))
    const grid = (await api('priya', 'workload')).body
    const role = grid.unfilledRoles.find((r) => r.roleLabel === 'Civil technologist')
    const person = grid.people.map((p) => p.displayName)
    await sec.screenshot({ path: `${require('./lib.cjs').EVID}/AC-ROLE-02a.png` })
    const cells = civil?.slice(1, 9).map(num)
    rec('AC-ROLE-02a', JSON.stringify(cells) === JSON.stringify([0, 0, 20, 20, 20, 20, 0, 0]) && !person.includes('Civil technologist'),
      { weeks: grid.weeks, shown: civil, apiCells: role?.cells.map((c) => c.hours), screenshot: 'AC-ROLE-02a.png' })
  },

  // AC-ROLE-05 and section 6.8 R1-R4 in the browser: SYN-103 open roles reach only people who can see SYN-103.
  async privacy() {
    const out = {}
    for (const who of ['priya', 'sam', 'alex', 'rita', 'marc', 'jill', 'jordan']) {
      const page = await open(who, { width: 1600, height: 1000 })
      const seen = {}
      await page.goto(`${base}/workload`); await settle(page, 1500)
      const main = await page.locator('main').innerText()
      seen.workloadUnfilled = await page.locator('#wl-unfilled').count() ? await page.locator('#wl-unfilled').innerText() : null
      seen.workloadMentions = /Hydraulic modeller|SYN-103/.test(main)
      if (['priya', 'jordan', 'marc'].includes(who) && seen.workloadUnfilled) { await page.locator('#wl-unfilled').scrollIntoViewIfNeeded(); await shot(page, `R1-R2-workload-${who}`) }
      await page.goto(`${base}/search?q=Hydraulic`); await settle(page, 1500)
      seen.search = /Hydraulic modeller/.test(await page.locator('main').innerText())
      await page.goto(`${base}/projects/SYN-103/allocations?view=roles`); await settle(page, 1500)
      const roles = await page.locator('main').innerText()
      seen.rolesTab = /Hydraulic modeller/.test(roles)
      seen.rolesTabNotFound = /not found|can.t find|doesn.t exist|no access|404/i.test(roles)
      if (['priya', 'marc'].includes(who)) await shot(page, `R1-R2-syn103-roles-${who}`)
      await page.goto(`${base}/notifications`); await settle(page, 1500)
      seen.notifications = /Hydraulic modeller/.test(await page.locator('main').innerText())
      const role = (await api('jordan', `projects/${(await project('SYN-103')).id}/open-roles`)).body.find((r) => r.roleLabel === 'Hydraulic modeller')
      seen.roleHistoryStatus = await page.evaluate(async ([id, email]) => (await fetch(`/api/v1/items/OpenRole/${id}/activity`, { headers: { 'X-Dev-User': email } })).status, [role.id, `${who}@hub.test`])
      out[who] = seen
      await page.context().close()
    }
    const outsiders = ['priya', 'sam', 'alex', 'rita'], insiders = ['marc', 'jill', 'jordan']
    const r1 = outsiders.every((w) => !out[w].workloadMentions && !out[w].search && !out[w].rolesTab)
    const r2 = insiders.every((w) => out[w].rolesTab) && ['marc', 'jordan'].every((w) => /Hydraulic modeller/.test(out[w].workloadUnfilled ?? ''))
    const r3 = outsiders.every((w) => !out[w].notifications)
    const r4 = outsiders.every((w) => out[w].roleHistoryStatus === 404) && insiders.every((w) => out[w].roleHistoryStatus === 200)
    rec('R1', r1, { outsiders: Object.fromEntries(outsiders.map((w) => [w, out[w]])) })
    rec('R2', r2, { insiders: Object.fromEntries(insiders.map((w) => [w, { rolesTab: out[w].rolesTab, workload: /Hydraulic modeller/.test(out[w].workloadUnfilled ?? ''), workloadSection: !!out[w].workloadUnfilled }])) })
    rec('R3', r3, { notificationsMention: Object.fromEntries(outsiders.map((w) => [w, out[w].notifications])) })
    rec('R4', r4, { historyStatus: Object.fromEntries([...outsiders, ...insiders].map((w) => [w, out[w].roleHistoryStatus])) })
  },

  // AC-GRP-02a/b/c: group by URL, Not set bucket, and subtotals equal the sum of the rows for every week.
  async group() {
    const checks = []
    for (const [path, who] of [['/workload?groupBy=homeDiscipline', 'lena'], ['/workload?groupBy=office', 'lena'], ['/workload?groupBy=jobTitle', 'priya'], ['/workload', 'sam'],
      ['/planner?groupBy=homeDiscipline', 'lena'], ['/planner?groupBy=office', 'priya'], ['/planner?groupBy=jobTitle', 'sam'], ['/planner', 'lena']]) {
      const page = await open(who, { width: 1600, height: 1000 })
      await page.goto(`${base}${path}`); await settle(page, 1800)
      await page.locator('th[scope=rowgroup]').first().waitFor({ timeout: 15000 })
      const planner = path.startsWith('/planner')
      const groups = await page.evaluate((planner) => [...document.querySelectorAll('tbody')].filter((b) => b.querySelector('th[scope=rowgroup]')).map((b) => {
        const head = b.querySelector('tr'); const label = head.querySelector('th').innerText.trim()
        const subs = [...head.querySelectorAll('td[data-subtotal]')].map((td) => Number(td.dataset.subtotal))
        const rows = [...b.querySelectorAll(':scope > tr')].slice(1).filter((tr) => tr.querySelector(':scope > th[scope=row].sticky') && tr.querySelectorAll(':scope > td').length >= subs.length && tr.querySelector(planner ? 'td button span.block.text-xs' : 'td p span.text-lg\\/6'))
        const vals = rows.map((tr) => [...tr.querySelectorAll(':scope > td')].slice(0, subs.length).map((td) => {
          const s = planner ? td.querySelector('button span.block.text-xs')?.textContent : td.querySelector('p span')?.textContent
          return Number((s ?? '').split('/')[0].replace(/[^\d.\-−]/g, '').replace('−', '-'))
        }))
        const names = rows.map((tr) => tr.querySelector('th .break-words.font-semibold')?.textContent?.trim())
        const sums = subs.map((_, i) => Math.round(vals.reduce((n, r) => n + r[i], 0) * 10) / 10)
        return { label, rows: rows.length, names, subs, sums }
      }), planner)
      const tol = (g) => 0.05 * g.rows + 0.05
      const bad = groups.flatMap((g) => g.subs.map((s, i) => Math.abs(s - g.sums[i]) <= tol(g) ? null : `${g.label} week ${i + 1}: subtotal ${s} vs rows ${g.sums[i]}`)).filter(Boolean)
      const url = page.url()
      await page.reload(); await settle(page, 1500)
      const afterReload = await page.locator('th[scope=rowgroup]').allInnerTexts()
      const name = `AC-GRP-02-${path.replace(/^\//, '').replace(/[?=]/g, '-')}-${who}`
      await shot(page, name)
      checks.push({ path, who, url, groups: groups.map((g) => ({ label: g.label, rows: g.rows, weeks: g.subs.length, subtotals: g.subs, sumOfRows: g.sums, names: /^not set/i.test(g.label) ? g.names : undefined })), mismatches: bad, sameGroupsAfterReload: JSON.stringify(afterReload) === JSON.stringify(groups.map((g) => g.label)), screenshot: `${name}.png` })
      await page.context().close()
    }
    // The menu changes groupBy in the URL.
    const page = await open('lena', { width: 1600, height: 1000 })
    await page.goto(`${base}/workload`); await settle(page, 1500)
    await page.getByRole('button', { name: /Reports to/ }).first().click()
    await page.getByRole('menuitemradio', { name: 'Home discipline' }).click()
    await page.waitForURL(/groupBy=homeDiscipline/, { timeout: 5000 }).catch(() => undefined)
    const menuUrl = page.url()
    await shot(page, 'AC-GRP-02a-menu')
    await page.context().close()
    const disc = checks.filter((c) => c.path.includes('homeDiscipline'))
    rec('AC-GRP-02a', checks.every((c) => c.sameGroupsAfterReload && c.groups.length > 0) && /groupBy=homeDiscipline/.test(menuUrl), { menuUrl, pages: checks.map((c) => ({ path: c.path, who: c.who, groups: c.groups.map((g) => `${g.label}`), reload: c.sameGroupsAfterReload, screenshot: c.screenshot })) })
    rec('AC-GRP-02b', disc.every((c) => c.groups.some((g) => /^not set/i.test(g.label) && (g.names ?? []).some((n) => n?.includes('Diane Roy')))), { notSet: disc.map((c) => ({ path: c.path, notSet: c.groups.find((g) => /^not set/i.test(g.label)), last: c.groups.at(-1)?.label })) })
    rec('AC-GRP-02c', checks.every((c) => c.mismatches.length === 0), { pages: checks.map((c) => ({ path: c.path, who: c.who, groups: c.groups.length, weeksChecked: c.groups[0]?.weeks, mismatches: c.mismatches })) })
  },

  // AC-PICK-01a/01b/02a/02c/03b and R5/R6: picker figures, week choice, scope, one request per open.
  async picker() {
    const tasks = await tasksOf('SYN-101')
    const byKey = Object.fromEntries(tasks.map((t) => [t.key, t]))
    const users = (await api('jordan', 'users')).body
    const uid = Object.fromEntries(users.map((u) => [u.displayName, u.id]))
    const cases = [['SYN-101-T0004', 'start 2026-10-12', '2026-10-12'], ['SYN-101-T0007', 'start 2026-10-19 (week W)', '2026-10-19'],
      ['SYN-101-T0011', 'no start, due Sun 2026-11-01', '2026-10-26'], ['SYN-101-T0013', 'no start, due 2026-10-22', '2026-10-19'], ['SYN-101-T0003', 'no dates', '2026-10-12']]
    const out = []
    for (const [key, why, week] of cases) {
      const page = await open('sam', { width: 1600, height: 1000 })
      const grid = []; page.on('request', (r) => { if (r.url().includes('/api/v1/planning/grid') && r.url().includes('personIds')) grid.push(decodeURIComponent(r.url())) })
      const gridResp = []; page.on('response', async (r) => { if (r.url().includes('/api/v1/planning/grid') && r.url().includes('personIds')) gridResp.push(await r.json().catch(() => null)) })
      await page.goto(`${base}/projects/SYN-101/tasks?panel=Task:${byKey[key].id}`); await settle(page, 1500)
      const row = page.locator('div.grid', { has: page.getByText('Assignee', { exact: true }) }).first()
      try { await row.getByRole('button').first().click({ timeout: 8000 }) } catch { out.push({ key, why, readOnly: true }); await shot(page, `AC-PICK-${key}-readonly`, { scratch: true }); await page.context().close(); continue }
      await page.locator('[data-capacity]').first().waitFor({ timeout: 10000 })
      await page.waitForTimeout(600)
      const items = await page.locator('[cmdk-item]').evaluateAll((els) => els.map((e) => ({ text: e.innerText.replace(/\s+/g, ' ').trim(), figure: e.querySelector('[data-capacity]')?.textContent ?? null })))
      const from = grid.map((u) => new URL(u).searchParams.get('from'))
      const figures = {}
      for (const it of items.filter((i) => i.figure)) {
        const name = Object.keys(uid).find((n) => it.text.startsWith(n))
        const cell = (await api('sam', `planning/cells/${uid[name]}/${week}`)).body
        const remaining = cell.remaining ?? cell.cell?.remaining
        const shown = num(it.figure) * (/over/.test(it.figure) ? -1 : 1)
        figures[name] = { figure: it.figure, planner: remaining, equal: Math.abs(shown - remaining) < 0.051 }
      }
      const noFigure = items.filter((i) => !i.figure).map((i) => i.text.split(' ').slice(0, 2).join(' '))
      const respIds = (gridResp[0]?.people ?? []).map((p) => p.displayName)
      const name = `AC-PICK-${key}`
      await shot(page, name)
      out.push({ key, why, expectedWeek: week, requests: grid.length, from, figures, noFigure, responsePeople: respIds, screenshot: `${name}.png` })
      await page.context().close()
    }
    // Alex has no workload permission: plain picker, no request (AC-PICK-02c).
    const alex = await open('alex', { width: 1600, height: 1000 })
    const agrid = []; alex.on('request', (r) => { if (r.url().includes('/api/v1/planning/grid')) agrid.push(r.url()) })
    await alex.goto(`${base}/projects/SYN-101/tasks?panel=Task:${byKey['SYN-101-T0004'].id}`); await settle(alex, 1500)
    const arow = alex.locator('div.grid', { has: alex.getByText('Assignee', { exact: true }) }).first()
    const abuttons = await arow.getByRole('button').count()
    if (abuttons) { await arow.getByRole('button').first().click(); await alex.waitForTimeout(1200) }
    const afig = await alex.locator('[data-capacity]').count()
    await shot(alex, 'AC-PICK-02c-alex')
    const agridOnOpen = agrid.length
    // R6 from the browser: Sam asks for Omar (out of scope) and Alex (in scope); Alex asks for Jill.
    const samPage = await open('sam')
    const r6sam = await samPage.evaluate(async ([o, a]) => { const r = await fetch(`/api/v1/planning/grid?personIds=${o},${a}&from=2026-10-19&weeks=1`, { headers: { 'X-Dev-User': 'sam@hub.test' } }); return { status: r.status, people: (await r.json()).people.map((p) => p.displayName) } }, [uid['Omar Haddad'], uid['Alex Chen']])
    const r6rand = await samPage.evaluate(async ([o]) => { const r = await fetch(`/api/v1/planning/grid?personIds=${o}&from=2026-10-19&weeks=1`, { headers: { 'X-Dev-User': 'sam@hub.test' } }); return { status: r.status, body: await r.text() } }, [uid['Omar Haddad']])
    const r6randUnknown = await samPage.evaluate(async () => { const r = await fetch(`/api/v1/planning/grid?personIds=00000000-0000-0000-0000-00000000beef&from=2026-10-19&weeks=1`, { headers: { 'X-Dev-User': 'sam@hub.test' } }); return { status: r.status, body: await r.text() } })
    const r6alex = await alex.evaluate(async ([j]) => { const r = await fetch(`/api/v1/planning/grid?personIds=${j}&from=2026-10-19&weeks=1`, { headers: { 'X-Dev-User': 'alex@hub.test' } }); return { status: r.status } }, [uid['Jill Martin']])
    const strip = (s) => s.replace(/"evaluatedAt":"[^"]*"/, '')
    const ro = out.filter((c) => c.readOnly).map((c) => c.key); out.splice(0, out.length, ...out.filter((c) => !c.readOnly))
    const t4 = out[0], scopeOk = out.every((c) => !c.responsePeople.some((n) => ['Omar Haddad', 'Diane Roy'].includes(n)) && ['Omar Haddad', 'Diane Roy'].every((n) => c.noFigure.some((x) => n.startsWith(x))))
    rec('AC-PICK-01a', out.every((c) => Object.values(c.figures).every((f) => f.equal) && Object.keys(c.figures).length > 0), { cases: out.map((c) => ({ key: c.key, week: c.expectedWeek, figures: c.figures, screenshot: c.screenshot })) })
    rec('AC-PICK-01b', out.every((c) => c.from.length === 1 && c.from[0] === c.expectedWeek) && out.length >= 4, { readOnlyForSam: ro, cases: out.map((c) => ({ key: c.key, why: c.why, expected: c.expectedWeek, requested: c.from })) })
    rec('AC-PICK-02a', scopeOk, { cases: out.map((c) => ({ key: c.key, responsePeople: c.responsePeople, noFigure: c.noFigure })) })
    rec('AC-PICK-02c', agridOnOpen === 0 && afig === 0, { assigneeButtons: abuttons, gridRequests: agridOnOpen, figures: afig, screenshot: 'AC-PICK-02c-alex.png' })
    rec('AC-PICK-03b', out.every((c) => c.requests === 1), { requestsPerOpen: out.map((c) => [c.key, c.requests, c.from[0]]), candidates: t4 && (Object.keys(t4.figures).length + t4.noFigure.length) })
    rec('R6', r6sam.status === 200 && !r6sam.people.includes('Omar Haddad') && r6sam.people.includes('Alex Chen') && strip(r6rand.body) === strip(r6randUnknown.body) && r6rand.status === r6randUnknown.status && r6alex.status === 403,
      { samOmarAndAlex: r6sam, samOmarOnlyEqualsUnknownId: strip(r6rand.body) === strip(r6randUnknown.body), samOmarStatus: r6rand.status, alexJill: r6alex })
  },

  // AC-CNT-05/-06/-07/-08: Home lanes, hints and axe, Dashboard scope note, review lane label.
  async counts() {
    const home = {}
    for (const who of ['jordan', 'priya', 'sam']) {
      for (const scope of ['', '?projects=all']) {
        const page = await open(who, { width: 1600, height: 1000 })
        await page.goto(`${base}/home${scope}`); await page.locator('#h-status').waitFor(); await settle(page, 1200)
        const r = await page.evaluate(() => {
          const open = Number([...document.querySelectorAll('a[data-accent]')].find((a) => a.textContent.includes('Open Tasks')).textContent.replace(/\D+/g, ''))
          const lanes = [...document.querySelectorAll('#h-status li a')].map((a) => ({ name: a.getAttribute('aria-label'), href: a.getAttribute('href'), title: a.getAttribute('title'), n: Number(a.lastElementChild.textContent) }))
          return { open, lanes }
        })
        const lanes = [...new Map(r.lanes.map((l) => [l.href, l])).values()]
        const sum = lanes.filter((l) => !l.name.startsWith('Done')).reduce((a, l) => a + l.n, 0)
        const hold = lanes.find((l) => l.name.startsWith('On Hold')), review = lanes.find((l) => l.name.startsWith('Review and revision'))
        if (who === 'priya' && scope) { await page.locator('#h-status').scrollIntoViewIfNeeded(); await shot(page, 'AC-CNT-05-home-priya-all'); await page.locator('#h-status li a', { hasText: 'Review and revision' }).first().hover(); await page.waitForTimeout(600); await shot(page, 'AC-CNT-08') }
        await page.goto(`${base}${hold.href}`); await settle(page, 1500)
        const listed = await page.locator('main').innerText().then((s) => s.match(/(\d+) tasks?\b/)?.[1])
        home[`${who}${scope}`] = { openTasks: r.open, lanesPlusOnHold: sum, onHold: hold.n, onHoldHref: hold.href, onHoldList: Number(listed), review: review?.title }
        await page.context().close()
      }
    }
    rec('AC-CNT-05', Object.values(home).every((h) => h.openTasks === h.lanesPlusOnHold && h.onHold === h.onHoldList), { home, screenshot: 'AC-CNT-05-home-priya-all.png' })
    rec('AC-CNT-08', Object.values(home).every((h) => /Ready for Review.*In Review.*Revision Required/.test(h.review ?? '')), { definition: home['priya?projects=all'].review, screenshot: 'AC-CNT-08.png' })

    // AC-CNT-06: every tile has a hint; linked tiles describe themselves; (i) buttons for the rest; axe.
    const page = await open('priya', { width: 1600, height: 1000 })
    const tiles = {}, fails = []
    for (const [label, path] of [['home', '/home'], ['dashboard', '/projects/SYN-101/dashboard'], ['portfolio', '/portfolio']]) {
      await page.goto(`${base}${path}`); await settle(page, 1500)
      tiles[label] = await page.evaluate(() => [...document.querySelectorAll('main [data-accent]')].filter((el) => el.className.includes('bg-(--acc-bg)') && el.className.includes('rounded-lg')).map((el) => {
        const name = (el.querySelector('span span')?.textContent ?? '').trim()
        const d = el.hasAttribute('aria-describedby') ? el : el.querySelector('[aria-describedby]')
        const hint = d ? document.getElementById(d.getAttribute('aria-describedby'))?.textContent?.trim() ?? null : null
        return { name, hint, info: !!el.querySelector('button[aria-label="What this counts"]'), kind: el.tagName === 'DIV' ? (el.querySelector(':scope > a') ? 'box with link' : 'info button') : el.tagName === 'A' ? 'link' : 'filter button' }
      }))
      fails.push(...await axe(page, path))
    }
    await page.goto(`${base}/home`); await settle(page, 1200)
    let focused = null
    for (let i = 0; i < 80; i++) { await page.keyboard.press('Tab'); if (await page.evaluate(() => document.activeElement?.textContent?.includes('Open Tasks'))) { focused = true; break } }
    await page.waitForTimeout(600)
    const tip = await page.locator('[role=tooltip]').first().textContent().catch(() => null)
    await shot(page, 'AC-CNT-06')
    const missing = Object.entries(tiles).flatMap(([p, ts]) => ts.filter((t) => !t.hint && !t.info).map((t) => `${p}: ${t.name}`))
    rec('AC-CNT-06', missing.length === 0 && fails.length === 0 && focused && !!tip, { tiles, withoutHint: missing, axeSeriousOrCritical: fails, keyboardTooltipOnOpenTasks: tip, screenshot: 'AC-CNT-06.png' })

    // AC-CNT-07: Civil scope on the SYN-101 dashboard.
    await page.goto(`${base}/projects/SYN-101/dashboard`); await settle(page, 1500)
    const civil = await page.locator('#dash-scope option', { hasText: 'Civil' }).getAttribute('value')
    await page.selectOption('#dash-scope', civil); await settle(page, 1200)
    const notes = await page.locator('article').evaluateAll((as) => as.map((a) => [a.getAttribute('aria-label'), a.textContent.includes('Whole project')]))
    await page.locator('article').first().scrollIntoViewIfNeeded(); await shot(page, 'AC-CNT-07')
    rec('AC-CNT-07', notes.filter((n) => n[1]).map((n) => n[0]).join('|') === notes.filter((n) => /Decisions|Issues and risks/.test(n[0])).map((n) => n[0]).join('|') && notes.some((n) => n[1]), { tiles: notes, screenshot: 'AC-CNT-07.png' })
  },

  // SYN-105: AC-DEPT-06 arrows, AC-TL-01 faded start, AC-TL-02 dated task under undated deliverable, SC-003 chips.
  async syn105() {
    const page = await open('priya', { width: 1600, height: 1000 })
    await page.goto(`${base}/projects/SYN-105/timeline`); await page.getByRole('table').waitFor(); await settle(page, 1200)
    await page.getByRole('button', { name: 'Show all tasks' }).click(); await page.waitForTimeout(400)
    const tl = await page.evaluate(() => {
      const rows = [...document.querySelectorAll('[role=rowgroup] [role=row]')].map((r) => r.querySelector('[role=rowheader]')?.textContent?.trim())
      const bar = [...document.querySelectorAll('[role=cell] button[aria-label]')].find((b) => b.getAttribute('aria-label').includes('No start date task'))
      const fade = bar?.querySelector('span.bg-card\\/65')
      const titles = [...document.querySelectorAll('svg g title')].map((t) => t.textContent)
      const uns = [...document.querySelectorAll('#tl-unscheduled li')].map((l) => l.textContent.trim())
      return { rows, barLabel: bar?.getAttribute('aria-label'), barTitle: bar?.getAttribute('title'), fadeWidth: fade?.getBoundingClientRect().width, barWidth: bar?.getBoundingClientRect().width, titles, uns }
    })
    await shot(page, 'AC-DEPT-06-timeline')
    await page.getByRole('button', { name: 'Legend' }).click(); await page.waitForTimeout(400)
    const legend = await page.getByRole('dialog', { name: 'Legend' }).innerText()
    await shot(page, 'AC-TL-01-timeline-legend')
    await page.keyboard.press('Escape')
    await page.goto(`${base}/timeline?mine=false`); await settle(page, 1800)
    const g = await page.evaluate(() => {
      const labels = [...document.querySelectorAll('button[aria-label]')].map((b) => b.getAttribute('aria-label'))
      const titles = [...document.querySelectorAll('svg g title')].map((t) => t.textContent)
      const rowLabels = [...document.querySelectorAll('button.sticky')].map((b) => b.textContent.trim())
      return { noStart: labels.find((l) => l.includes('No start date task')), titles: [...new Set(titles)], permit: rowLabels.find((r) => r.includes('Permit drawing markups')) }
    })
    await shot(page, 'AC-DEPT-06-gantt')
    const want = ['Finish-to-Start · no lag', 'Finish-to-Start · lag 2 working days', 'Start-to-Start · no lag', 'Start-to-Start · lag 2 working days', 'Finish-to-Finish · no lag', 'Finish-to-Finish · lag 1 working day']
    rec('AC-DEPT-06', want.every((k) => tl.titles.includes(k)) && g.titles.some((x) => x.startsWith('Start-to-Start')) && g.titles.some((x) => x.startsWith('Finish-to-Finish')) && g.titles.some((x) => x.startsWith('Finish-to-Start')),
      { timelineArrowTitles: tl.titles, ganttArrowTitles: g.titles, screenshots: ['AC-DEPT-06-timeline.png', 'AC-DEPT-06-gantt.png'] })
    rec('AC-TL-01', /start not set/.test(tl.barLabel) && /start not set/.test(tl.barTitle) && tl.fadeWidth > 0 && tl.fadeWidth < tl.barWidth && /Bar: start → due[\s\S]*Start not set/.test(legend) && /start not set/.test(g.noStart ?? ''),
      { barLabel: tl.barLabel, fadeWidth: tl.fadeWidth, barWidth: tl.barWidth, legend: legend.replace(/\n/g, ' | '), gantt: g.noStart, screenshot: 'AC-TL-01-timeline-legend.png' })
    rec('AC-TL-02', tl.rows.some((r) => r?.includes('Permit drawing markups')) && !!g.permit, { timelineRow: tl.rows.find((r) => r?.includes('Permit drawing markups')), ganttRow: g.permit, screenshot: 'AC-DEPT-06-timeline.png' })

    // SC-003 chip-by-chip on date B (the seed day, today): list indicators and each successor's link rows.
    const today = (await api('priya', 'me')).body.settings.today
    const list = await tasksOf('SYN-105')
    const byName = Object.fromEntries(list.map((t) => [t.name, t]))
    const expect = [ // successor, link, type chip, satisfied, blocked, waiting, date flag
      ['Grading design', 'L1', 'FS', true, false, false, null],
      ['Utility conflict drawings', 'L2', 'FS +2d', false, true, false, 'Starts 2026-10-12 before SYN-105-T0003 is due 2026-10-15'],
      ['Storm sewer profiles', 'L3', 'SS', true, false, false, null],
      ['Drainage area plan', 'L4', 'SS +2d', false, true, false, 'Starts 2026-10-12 before SYN-105-T0007 starts 2026-10-15'],
      ['Quantity estimate', 'L5', 'FF', true, false, false, 'Due 2026-10-20 before SYN-105-T0009 is due 2026-10-21'],
      ['Drawing checklist', 'L6', 'FF +1d', true, false, false, null],
    ]
    const chips = []
    await page.setViewportSize({ width: 1920, height: 1200 })
    await page.goto(`${base}/projects/SYN-105/tasks`); await settle(page, 1500)
    await shot(page, 'SC-003-syn105-task-list')
    const listIndicators = await page.evaluate(() => Object.fromEntries([...document.querySelectorAll('main table tbody tr')].map((tr) => [tr.children[2]?.innerText.trim(), tr.innerText.replace(/\s+/g, ' ')])))
    for (const [name, link, chip, satisfied, blocked, waiting, flag] of expect) {
      const t = byName[name]
      await page.goto(`${base}/projects/SYN-105/tasks?panel=Task:${t.id}`); await settle(page, 1200)
      const depBlock = page.locator('div.space-y-1\\.5', { has: page.locator('div.min-h-8', { hasText: 'Depends on' }) }).first()
      await depBlock.scrollIntoViewIfNeeded(); await page.waitForTimeout(300)
      const rows = await depBlock.locator('li').evaluateAll((lis) => lis.map((li) => ({ text: li.innerText.replace(/\s+/g, ' ').trim(), satisfied: !!li.querySelector('[aria-label="Satisfied"], svg[role=img]'), chip: li.querySelector('span[title] span[aria-hidden]')?.textContent, chipTitle: li.querySelector('span[title]')?.getAttribute('title') })))
      const header = await page.getByRole('dialog').last().innerText().catch(() => '')
      const st = t.state ?? {}
      const flagText = (st.inconsistencyDetail ?? []).map((d) => d.text).join(' | ') || null
      const ok = rows.length === 1 && rows[0].chip === chip && rows[0].satisfied === satisfied && !!st.isBlocked === blocked && !!st.isWaiting === waiting && flagText === flag
        && (blocked ? /Blocked/.test(header) : !/\bBlocked\b/.test(header.split('People')[0])) && (flag ? /Date inconsistent/.test(header) : !/Date inconsistent/.test(header))
      await shot(page, `SC-003-${link}-${name.replace(/\s+/g, '-')}`)
      chips.push({ link, successor: `${t.key} ${name}`, ok, ui: rows, apiState: { blocked: !!st.isBlocked, waiting: !!st.isWaiting, blockedBy: (st.blockedBy ?? []).map((b) => b.reason), dateFlag: flagText }, expected: { chip, satisfied, blocked, waiting, flag }, listRow: listIndicators[name] })
    }
    rec('SC-003', chips.every((c) => c.ok), { today, chips })
    rec('AC-DEPT-02-04-B', chips.every((c) => c.ok), { note: 'same pass as SC-003' })
  },

  // AC-DEPT-01b (dialog half, nothing saved) and AC-STALE-03b (Admin screen shows the setting).
  async dialogs() {
    const page = await open('priya', { width: 1600, height: 1000 })
    const list = await tasksOf('SYN-105')
    const t = list.find((x) => x.name === 'No start date task')
    await page.goto(`${base}/projects/SYN-105/tasks?panel=Task:${t.id}`); await settle(page, 1500)
    await page.locator('div.min-h-8', { hasText: 'Depends on' }).getByRole('button', { name: 'Add' }).click()
    const d = page.getByRole('dialog').last()
    const options = await d.getByLabel('Link type').locator('option').evaluateAll((os) => os.map((o) => [o.value, o.textContent]))
    const hints = {}
    for (const [v] of options) { await d.getByLabel('Link type').selectOption(v); await page.waitForTimeout(150); hints[v] = await d.locator('#' + (await d.getByLabel('Link type').getAttribute('aria-describedby') ?? 'none').split(' ')[0]).textContent().catch(() => null) }
    await d.getByLabel('Link type').selectOption('StartToStart'); await d.getByLabel('Lag (days)').fill('2')
    await shot(page, 'AC-DEPT-01b-dialog')
    await page.keyboard.press('Escape'); await page.waitForTimeout(400)
    const after = (await api('priya', `tasks/${t.id}/dependencies`)).body.dependsOn.length
    // The chips as listed on SYN-105 successors (SC-003 screenshots) show FS, FS +2d, SS, SS +2d, FF and FF +1d.
    rec('AC-DEPT-01b', options.map((o) => o[0]).join(',') === 'FinishToStart,StartToStart,FinishToFinish' && Object.values(hints).every(Boolean) && after === 0, { options, hints, savedLinks: after, screenshot: 'AC-DEPT-01b-dialog.png', chips: 'see SC-003-L1..L6 screenshots' })
    await page.context().close()
    const j = await open('jordan', { width: 1600, height: 1000 })
    await j.goto(`${base}/admin/settings`); await settle(j, 1500)
    const label = j.getByText(/Status not moving after \(days\)/).first()
    await label.scrollIntoViewIfNeeded()
    const field = await j.getByLabel(/Status not moving after/).first().evaluate((el) => ({ value: el.value, min: el.min, max: el.max, type: el.type })).catch(() => null)
    const group = await label.evaluate((el) => { let p = el; for (let i = 0; i < 8 && p; i++) { p = p.parentElement; const h = p?.querySelector('h2, h3'); if (h) return h.textContent } return null })
    await shot(j, 'AC-STALE-03b')
    rec('AC-STALE-03b-B', !!field, { field, group, screenshot: 'AC-STALE-03b.png' })
  },
}

;(async () => {
  for (const [name, fn] of Object.entries(sections)) {
    if (only.length && !only.includes(name)) continue
    try { await fn() } catch (e) { rec(`section ${name}`, false, { error: String(e.stack ?? e).slice(0, 600) }) }
  }
  await close()
  const file = `${__dirname}/results-part1.json`
  const prev = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {}
  fs.writeFileSync(file, JSON.stringify({ ...prev, ...results }, null, 1))
})()
