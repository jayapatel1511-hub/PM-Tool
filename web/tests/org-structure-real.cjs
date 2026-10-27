// Packet 050 real-API checks against the local API and PostgreSQL: AC-UNT-01 (an Admin builds Atlantic > Civil > Civil
// Halifax Team in the Structure screen and places Alex; Organisation shows the path, leader and a head count of 1 at every
// level), AC-UNT-05 (Sam's My Staff with include_indirect_reports off, then on; a reporting cycle grants no indirect scope
// and is listed for Admins) and AC-UNT-06 (Workload grouped by unit and filtered to Atlantic: each person once, under
// their home unit, and the subtotals add up to the Atlantic total; Planner and Vacation group the same way).
// Sets the flags it needs and restores them; synthetic records only, on an explicitly disposable database.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'this check creates records and sets flags, so it requires an explicitly disposable database')

async function api(pathname, who, options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json' } })
  if (options.expect) { assert.equal(response.status, options.expect, `${options.method ?? 'GET'} ${pathname}`); return null }
  if (!response.ok) throw new Error(`${options.method ?? 'GET'} ${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const send = (pathname, who, body, method = 'POST') => api(pathname, who, { method, body: JSON.stringify(body) })
const setting = (key, value) => send(`admin/settings/${key}`, 'jordan', { value }, 'PUT')
const addDays = (iso, n) => new Date(Date.parse(`${iso}T00:00:00Z`) + n * 86400000).toISOString().slice(0, 10)
const suffix = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`.slice(-7)
const sorted = (xs) => [...xs].sort()
const tenth = (n) => Math.round(n * 10) / 10
async function userRow(who) { return (await api(`admin/users?q=${encodeURIComponent(`${who}@hub.test`)}`, 'jordan')).find(u => u.email === `${who}@hub.test`) }
async function setSupervisor(who, supervisorId) { const u = await userRow(who); await send(`admin/users/${u.id}`, 'jordan', { supervisorId, rowVersion: u.rowVersion }, 'PATCH') }
async function place(who, unitId) { const u = await userRow(who); await send(`admin/organisation/users/${u.id}/placement`, 'jordan', { unitId, rowVersion: u.rowVersion }, 'PATCH') }

;(async () => {
  const settings = await api('admin/settings', 'jordan')
  const before = Object.fromEntries(['flag.org_structure', 'include_indirect_reports'].map(k => [k, settings.find(s => s.key === k)?.value === true]))
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const pages = []
  const session = async (who) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    await context.addInitScript(email => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
    const page = await context.newPage()
    page.errors = []
    page.on('pageerror', e => page.errors.push(e.message))
    pages.push(page)
    return page
  }
  const open = async (page, url) => { await page.goto(`${base}${url}`, { waitUntil: 'networkidle', timeout: 60000 }); await page.getByRole('main').first().waitFor() }
  const results = {}
  const tag = suffix()
  const who = { nia: `nia050${tag}`, quinn: `quinn050${tag}`, q1: `qa050${tag}`, q2: `qb050${tag}` }
  try {
    // Synthetic people. The reporting cycle comes from manager links saved before the amendment: with the flag off they
    // save exactly as they always have, so the cycle exists before org_structure is turned on.
    await setting('flag.org_structure', false)
    await setting('include_indirect_reports', false)
    const people = {}
    for (const w of ['sam', 'marc', 'alex', 'jill', 'lena', 'diane', 'priya', 'omar', ...Object.values(who)]) people[w] = await api('me', w)
    const name = (w) => people[who[w] ?? w].displayName
    await setSupervisor(who.nia, people.marc.id)
    await send(`admin/users/${people[who.quinn].id}/roles`, 'jordan', { role: 'Supervisor' })
    await setSupervisor(who.q1, people[who.quinn].id); await setSupervisor(who.q2, people[who.q1].id); await setSupervisor(who.quinn, people[who.q2].id)
    await setting('flag.org_structure', true)

    // AC-UNT-01: the Admin builds the three-level path in the Structure screen, sets leaders and places Alex.
    const admin = await session('jordan')
    await open(admin, '/admin/structure')
    const patched = () => admin.waitForResponse(r => ['PATCH', 'POST'].includes(r.request().method()) && r.url().includes('/api/v1/admin/organisation/'))
    for (const [unit, level, parent] of [['Atlantic', 'Region', 'Root'], ['Civil', 'Business unit', 'Atlantic'], ['Civil Halifax Team', 'Team', 'Civil']]) {
      await admin.locator('#new-unit').fill(unit)
      await admin.locator('#new-unit-level').selectOption({ label: level })
      await admin.locator('#new-unit-parent').selectOption({ label: parent })
      const done = patched()
      await admin.getByRole('button', { name: 'Add unit' }).click()
      assert.equal((await done).status(), 201, `create ${unit}`)
      await admin.getByLabel(`Unit name: ${unit}`, { exact: true }).waitFor()
    }
    const choose = async (label, option) => {
      const done = patched()
      await admin.getByLabel(label, { exact: true }).selectOption({ label: option })
      const response = await done
      assert.ok(response.ok(), `${label}: HTTP ${response.status()}`)
      await admin.waitForLoadState('networkidle')
    }
    for (const [unit, leader] of [['Atlantic', 'lena'], ['Civil', 'sam'], ['Civil Halifax Team', 'marc']]) await choose(`Unit leader: ${unit}`, name(leader))
    await choose(`Home unit: ${name('alex')}`, 'Civil Halifax Team')
    const units = await api('admin/organisation/units', 'jordan')
    const unit = (n, parentId = null) => units.find(u => u.name === n && (u.parentId ?? null) === parentId)
    const atlantic = unit('Atlantic'), civil = unit('Civil', atlantic.id), team = unit('Civil Halifax Team', civil.id)
    assert.ok(atlantic && civil && team, 'the Structure screen created the three-level path')

    const tree = (page) => page.evaluate(() => [...document.querySelectorAll('main .border-l > button[aria-expanded]')].map(b => {
      const trail = []
      for (let el = b.parentElement; el; el = el.parentElement) if (el.firstElementChild?.matches('button[aria-expanded]')) trail.unshift(el.firstElementChild.querySelector('.font-semibold').textContent)
      return { path: trail.join(' / '), meta: b.lastElementChild.textContent }
    }))
    const organisation = {}
    for (const viewer of ['jordan', 'diane']) {
      const page = await session(viewer)
      await open(page, '/organisation')
      await page.getByRole('button', { name: /Civil Halifax Team/ }).waitFor()
      assert.deepEqual(await tree(page), [
        { path: 'Atlantic', meta: 'Region · 1 person in subtree' },
        { path: 'Atlantic / Civil', meta: 'Business unit · 1 person in subtree' },
        { path: 'Atlantic / Civil / Civil Halifax Team', meta: 'Team · 1 person in subtree' }], `${viewer}: path and head count of 1 at every level`)
      for (const [n, leader] of [['Atlantic', 'lena'], ['Civil', 'sam'], ['Civil Halifax Team', 'marc']]) {
        await page.locator('main .border-l > button[aria-expanded]', { has: page.getByText(n, { exact: true }) }).click()
        await page.getByText(`Leader: ${name(leader)}`, { exact: true }).waitFor()
      }
      // Every signed-in user sees names, levels, leaders and head counts; the person list follows people scope (FR-UNT-15).
      const listed = await page.locator('main li').filter({ hasText: name('alex') }).count()
      assert.equal(listed, viewer === 'jordan' ? 1 : 0, `${viewer}: Alex listed only within people scope`)
      assert.deepEqual(page.errors, [])
      organisation[viewer] = { tree: await tree(page), alexListed: listed === 1 }
    }
    results['AC-UNT-01'] = { createdIn: '/admin/structure', organisation }

    // AC-UNT-05: Sam (a Supervisor who also leads Civil) on My Staff with include_indirect_reports off, then on.
    const sam = await session('sam')
    const scope = sam.getByRole('group', { name: 'Scope' })
    const staffNames = async (page, expected) => {
      const links = page.locator('main table a.font-semibold[href^="/my-work?userId="]')
      for (let i = 0; i < 300 && (await links.count()) !== expected; i++) await page.waitForTimeout(100) // generous: several builders share one Postgres
      const found = sorted(await links.allInnerTexts())
      if (found.length !== expected) console.error(`My Staff at ${page.url()} listed ${found.length}, expected ${expected}:\n${(await page.getByRole('main').first().innerText()).slice(0, 800)}`)
      return found
    }
    const direct = sorted([name('alex'), name('jill'), name('marc')])
    await open(sam, '/staff')
    assert.deepEqual(await scope.getByRole('button').allInnerTexts(), ['My direct reports', 'Civil'], 'off: no All my reports')
    assert.deepEqual(await staffNames(sam, 3), direct, 'off: Sam sees only direct reports')
    await api('staff?scope=reports', 'sam', { expect: 400 })
    await setting('include_indirect_reports', true)
    await open(sam, '/staff')
    assert.deepEqual(await scope.getByRole('button').allInnerTexts(), ['My direct reports', 'All my reports', 'Civil'])
    assert.deepEqual(await staffNames(sam, 3), direct, 'on: the default scope is unchanged')
    await scope.getByRole('button', { name: 'All my reports' }).click()
    await sam.waitForURL(/scope=reports/)
    const below = sorted([...direct, name('nia')])
    assert.deepEqual(await staffNames(sam, 4), below, 'on: All my reports lists every person below Sam')
    const quinn = await session(who.quinn)
    await open(quinn, '/staff')
    await quinn.getByRole('group', { name: 'Scope' }).getByRole('button', { name: 'All my reports' }).click()
    await quinn.waitForURL(/scope=reports/)
    assert.deepEqual(await staffNames(quinn, 1), [name('q1')], 'a reporting cycle grants no indirect scope')
    await open(admin, '/admin/structure')
    const alert = admin.getByRole('alert').filter({ hasText: 'Reporting cycles affect these people' })
    const cycle = sorted(await alert.getByRole('listitem').allInnerTexts())
    assert.deepEqual(cycle, sorted([name('quinn'), name('q1'), name('q2')]), 'the cycle is listed for Admins')
    assert.equal((await api('organisation', 'sam')).managerCycles, null, 'non-Admins get no cycle list')
    await setting('include_indirect_reports', false)
    await open(sam, '/staff')
    assert.deepEqual(await scope.getByRole('button').allInnerTexts(), ['My direct reports', 'Civil'], 'off again: no All my reports')
    for (const page of [sam, quinn, admin]) assert.deepEqual(page.errors, [])
    results['AC-UNT-05'] = { off: direct, onAllMyReports: below, cycleViewerReports: [name('q1')], adminCycleList: cycle }

    // AC-UNT-06: more of the tree, including the same unit name in two branches, and estimated work for each person.
    const create = async (n, level, parentId) => send('admin/organisation/units', 'jordan', { name: n, levelId: (await api('admin/organisation/levels', 'jordan')).find(l => l.name === level).id, parentId, leaderId: null, disciplineId: null, officeId: null })
    const structural = await create('Structural', 'Business unit', atlantic.id)
    const structuralTeam = await create('Halifax Team', 'Team', structural.id)
    const civilTeam = await create('Halifax Team', 'Team', civil.id)
    const pacific = await create('Pacific', 'Region', null)
    for (const [w, u] of [['jill', civil], ['marc', structuralTeam], ['sam', atlantic], ['priya', civilTeam], ['diane', pacific]]) await place(w, u.id)
    const reference = await api('reference', 'priya')
    const civilDiscipline = reference.disciplines.find(x => x.name === 'Civil')
    const draft = await send('projects', 'priya', { projectNumber: `U${suffix().toUpperCase()}`, name: 'Synthetic 050 unit workload', clientId: reference.clients[0].id,
      officeId: reference.offices[0].id, disciplines: [{ disciplineId: civilDiscipline.id, leadUserId: people.marc.id }], members: [] })
    const detail = await api(`projects/${draft.id}`, 'priya')
    await send(`projects/${draft.id}/transition`, 'priya', { toStatus: 'Active', rowVersion: detail.rowVersion })
    const projectDisciplineId = (detail.disciplines ?? detail.projectDisciplines)[0].id
    const today = (await api('me', 'jordan')).settings.today
    const hours = { alex: 16, jill: 8, marc: 12, sam: 6, priya: 5, diane: 10, omar: 7 }
    for (const [w, h] of Object.entries(hours)) await send(`projects/${draft.id}/tasks`, 'priya', { name: `Synthetic 050 load ${w}`, projectDisciplineId,
      assigneeId: people[w].id, estimatedHours: h, startDate: today, dueDate: addDays(today, 11) })

    const grid = await api(`workload?unitId=${atlantic.id}`, 'jordan')
    const members = { alex: 'Civil Halifax Team', jill: 'Civil', marc: 'Atlantic / Structural / Halifax Team', sam: 'Atlantic', priya: 'Atlantic / Civil / Halifax Team' }
    assert.deepEqual(sorted(grid.people.map(p => p.displayName)), sorted(Object.keys(members).map(name)), 'the server filter selects the Atlantic subtree')
    const committed = Object.fromEntries(grid.people.map(p => [p.displayName, p.cells.map(c => c.committed)]))
    const total = grid.weeks.map((_, i) => tenth(grid.people.reduce((n, p) => n + p.cells[i].committed, 0)))
    assert.equal(tenth(total.reduce((a, b) => a + b, 0)), 16 + 8 + 12 + 6 + 5, 'the Atlantic total carries every subtree hour and nothing else')
    const expectedGroups = Object.fromEntries(Object.entries(Object.groupBy(Object.keys(members), w => members[w])).map(([g, ws]) => [g, sorted(ws.map(name))]))

    const viewer = await session('jordan')
    const readGroups = (page, region) => page.evaluate((label) => [...document.querySelectorAll(`[role=region][aria-label="${label}"] table > tbody`)].map(tb => ({
      head: tb.querySelector('th[scope=rowgroup]')?.textContent ?? '',
      subtotals: [...tb.querySelectorAll('td[data-subtotal]')].map(td => Number(td.dataset.subtotal)),
      people: [...tb.querySelectorAll('th[scope=row] .break-words.font-semibold')].map(e => e.textContent) })), region)
    const settle = async (page, region, count) => {
      let groups = []
      for (let i = 0; i < 300; i++) { groups = await readGroups(page, region); if (groups.flatMap(g => g.people).length === count && !groups.some(g => g.head.startsWith('Unknown placement'))) break; await page.waitForTimeout(100) }
      return groups
    }
    const byHead = (groups) => Object.fromEntries(groups.map(g => [g.head.replace(/ · \d+$/, ''), sorted(g.people)]))
    await open(viewer, `/workload?unitId=${atlantic.id}&groupBy=homeUnit`)
    const workload = await settle(viewer, 'Workload by person and week', 5)
    const shown = workload.flatMap(g => g.people)
    assert.equal(new Set(shown).size, shown.length, 'each person appears exactly once')
    assert.deepEqual(byHead(workload), expectedGroups, 'each person appears under their home unit')
    for (const g of workload) {
      assert.equal(g.head, `${g.head.replace(/ · \d+$/, '')} · ${g.people.length}`)
      g.subtotals.forEach((s, i) => assert.equal(tenth(s), tenth(g.people.reduce((n, p) => n + committed[p][i], 0)), `${g.head} week ${i}: subtotal of its rows`))
    }
    const sums = total.map((_, i) => tenth(workload.reduce((n, g) => n + g.subtotals[i], 0)))
    assert.deepEqual(sums, total, 'the group subtotals add up to the Atlantic total, week by week')
    results['AC-UNT-06'] = { atlanticTotalByWeek: total, groups: workload.map(g => ({ head: g.head, people: g.people, subtotals: g.subtotals })) }

    // FR-UNT-13: the Planner and Vacation group by the same home units.
    await open(viewer, `/planner?unitId=${atlantic.id}&groupBy=homeUnit`)
    const planner = await settle(viewer, 'Weekly planning grid', 5)
    assert.deepEqual(byHead(planner), expectedGroups, 'Planner: each person under their home unit')
    await open(viewer, `/vacation?unitId=${atlantic.id}&groupBy=unit`)
    const vacationHeads = async () => viewer.locator('main table tbody tr.bg-muted > th').allInnerTexts()
    for (let i = 0; i < 300 && (await vacationHeads()).length !== 5; i++) await viewer.waitForTimeout(100)
    assert.deepEqual(sorted(await vacationHeads()), sorted(Object.keys(expectedGroups)), 'Vacation: one heading per home unit')
    assert.deepEqual(viewer.errors, [])
    results['FR-UNT-13'] = { planner: byHead(planner), vacationHeads: sorted(await vacationHeads()) }

    const out = path.resolve(__dirname, '../../ci-artifacts/smoke-live/org-structure-real.json')
    fs.mkdirSync(path.dirname(out), { recursive: true })
    fs.writeFileSync(out, `${JSON.stringify({ api: apiBase, ui: base, results }, null, 2)}\n`)
    console.log(JSON.stringify(results))
    console.log('PASS: packet 050 real-API organisation, indirect-scope and unit-grouping checks')
  } finally {
    // Break the synthetic cycle and restore the settings this check changed.
    try { const q = await userRow(who.quinn); if (q?.supervisorId) await send(`admin/users/${q.id}`, 'jordan', { supervisorId: null, rowVersion: q.rowVersion }, 'PATCH') } catch (e) { console.error(e.message) }
    for (const [key, value] of Object.entries(before)) await setting(key, value)
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
