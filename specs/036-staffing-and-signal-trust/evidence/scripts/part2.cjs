// Packet 036 acceptance sweep, part 2: checks that write or need backdated data, on the temporary HEAD stack
// (TUESDAY_UI_BASE_URL=http://localhost:5091: API and SPA built at 4760e2c4, database pm-acc036-temp). Nothing here touches 5080.
const fs = require('node:fs')
const { open, shot, settle, close, base, api } = require('./lib.cjs')
if (!base.includes(':5091')) throw new Error('part2 runs only against the temporary stack on 5091')
const only = process.argv.slice(2)
const results = {}
const rec = (id, ok, detail) => { results[id] = { ok, ...detail }; console.log(`${ok ? 'PASS' : 'FAIL'} ${id}`, JSON.stringify(detail)) }

const sections = {
  // AC-ROLE-01 (dialog), AC-ROLE-03a (Fill with Jill and her SYN-104 task) and AC-ROLE-04b (cancel reopens the role).
  async fill() {
    const page = await open('priya', { width: 1600, height: 1000 })
    await page.goto(`${base}/projects/SYN-104/allocations?view=roles`); await settle(page, 1500)
    await page.getByRole('button', { name: 'Add open role' }).first().click()
    const d = page.getByRole('dialog')
    await d.getByLabel('Role', { exact: true }).fill('Civil technologist')
    await d.getByLabel('From', { exact: true }).fill('2026-10-26')
    await d.getByLabel('Through', { exact: true }).fill('2026-11-20')
    await d.getByLabel('Planned hours').fill('80')
    const create = page.waitForResponse((r) => r.request().method() === 'POST' && /open-roles$/.test(new URL(r.url()).pathname))
    await d.getByRole('button', { name: 'Save' }).click()
    const created = await (await create).json()
    await d.waitFor({ state: 'detached' })
    const row = page.getByRole('row', { name: /Civil technologist/ }).first(); await row.waitFor()
    await shot(page, 'AC-ROLE-01-created')
    await row.getByRole('button', { name: 'Fill Civil technologist' }).click()
    const f = page.getByRole('dialog')
    await f.getByRole('heading', { name: 'Fill Civil technologist' }).waitFor()
    const prefill = [await f.getByLabel('From', { exact: true }).inputValue(), await f.getByLabel('Through', { exact: true }).inputValue(), await f.getByLabel('Planned hours').inputValue()]
    await f.getByLabel('Person').selectOption({ label: 'Jill Martin' })
    const task = f.getByLabel('Linked task 1')
    const options = await task.locator('option').allTextContents()
    await task.selectOption({ label: options.find((o) => o.includes('Servicing concept layout')) })
    await shot(page, 'AC-ROLE-03a-fill-form')
    const post = page.waitForRequest((r) => r.method() === 'POST' && /\/allocations$/.test(new URL(r.url()).pathname))
    const resp = page.waitForResponse((r) => r.request().method() === 'POST' && /\/allocations$/.test(new URL(r.url()).pathname))
    await f.getByRole('button', { name: 'Propose allocation' }).click()
    const body = (await post).postDataJSON(); const status = (await resp).status()
    await page.waitForTimeout(800); await page.keyboard.press('Escape'); await page.waitForTimeout(600)
    const filled = page.getByRole('row', { name: /Civil technologist/ }).first()
    await filled.getByText('Filled').waitFor()
    const filledText = (await filled.innerText()).replace(/\s+/g, ' ')
    await shot(page, 'AC-ROLE-03a-filled')
    const roles = (await api('priya', `projects/${created.projectId ?? (await api('priya', 'projects/SYN-104')).body.id}/open-roles`)).body
    const role = roles.find((r) => r.id === created.id)
    const act = (await api('priya', `items/OpenRole/${created.id}/activity`)).body
    // Cancel the proposed allocation: the role returns to Open.
    await filled.getByRole('button', { name: /Jill Martin/ }).click()
    await page.getByRole('button', { name: 'Cancel allocation' }).click()
    const c = page.getByRole('dialog').last()
    await c.getByLabel('Reason').fill('Acceptance check: the role should reopen.')
    await c.getByRole('button', { name: 'Cancel allocation' }).click()
    await page.waitForTimeout(1000); await page.keyboard.press('Escape'); await page.waitForTimeout(600)
    const reopened = page.getByRole('row', { name: /Civil technologist/ }).first()
    await reopened.getByText(/Open/).first().waitFor()
    const reopenedText = (await reopened.innerText()).replace(/\s+/g, ' ')
    await shot(page, 'AC-ROLE-04b-reopened')
    const after = (await api('priya', `projects/${role.projectId}/open-roles`)).body.find((r) => r.id === created.id)
    rec('AC-ROLE-03a', status === 200 && body.openRoleId === created.id && role.status === 'Filled' && !!role.filledAllocationId && /Filled · Jill Martin/.test(filledText),
      { prefill, postBody: { openRoleId: body.openRoleId, personId: body.personId, fromDate: body.fromDate, throughDate: body.throughDate, plannedHours: body.plannedHours }, status, roleAfterFill: { status: role.status, filledAllocationId: role.filledAllocationId }, rowAfterFill: filledText, activity: (act.items ?? act).slice?.(0, 4)?.map((a) => `${a.action} ${a.correlationId ?? ''}`), screenshots: ['AC-ROLE-01-created.png', 'AC-ROLE-03a-fill-form.png', 'AC-ROLE-03a-filled.png'] })
    rec('AC-ROLE-04b-B', after.status === 'Open' && !after.filledAllocationId && /Open/.test(reopenedText), { roleAfterCancel: { status: after.status, filledAllocationId: after.filledAllocationId }, row: reopenedText, screenshot: 'AC-ROLE-04b-reopened.png' })
  },

  // AC-GRP-01a (browser): Jordan sets Alex's home discipline to Civil; the request carries it and the history shows it.
  async admin() {
    const page = await open('jordan', { width: 1600, height: 1000 })
    await page.goto(`${base}/admin/users`); await settle(page, 1500)
    const row = page.getByRole('row', { name: /Alex Chen/ }).first(); await row.waitFor()
    await row.getByRole('button', { name: 'Edit' }).click()
    const d = page.getByRole('dialog')
    const sel = d.getByLabel(/Home discipline/)
    const options = await sel.locator('option').allTextContents()
    await sel.selectOption({ label: 'Civil' })
    await shot(page, 'AC-GRP-01a-editor')
    const req = page.waitForRequest((r) => r.method() === 'PATCH' && r.url().includes('/admin/users/'))
    const res = page.waitForResponse((r) => r.request().method() === 'PATCH' && r.url().includes('/admin/users/'))
    await d.getByRole('button', { name: 'Save' }).click()
    const body = (await req).postDataJSON(); const status = (await res).status()
    await d.waitFor({ state: 'detached' })
    const users = (await api('jordan', 'admin/users')).body
    const alex = users.find((u) => u.displayName === 'Alex Chen')
    const hist = (await api('jordan', `admin/users/${alex.id}/activity`)).body
    const lena = await open('lena', { width: 1600, height: 1000 })
    await lena.goto(`${base}/workload?groupBy=homeDiscipline`); await settle(lena, 1500)
    const groups = await lena.locator('th[scope=rowgroup]').allInnerTexts()
    const forbidden = (await api('priya', `admin/users/${alex.id}`, { method: 'PATCH', body: JSON.stringify({ homeDisciplineId: null, rowVersion: alex.rowVersion }) })).status
    rec('AC-GRP-01a-B', status === 200 && !!body.homeDisciplineId && alex.homeDisciplineId === body.homeDisciplineId && groups.some((g) => /^civil/i.test(g)) && forbidden === 403,
      { options, patchBody: { homeDisciplineId: body.homeDisciplineId }, status, stored: alex.homeDisciplineId, history: JSON.stringify(hist).match(/HomeDiscipline[^}]*/)?.[0] ?? (Array.isArray(hist) ? hist.length : typeof hist), workloadGroupsAfter: groups, nonAdminPatch: forbidden, screenshot: 'AC-GRP-01a-editor.png' })
  },

  // AC-TL-01 with a real 10-day-old creation date, AC-TL-03 with the template's undated milestone, AC-TL-04 local creation date.
  async timeline() {
    const page = await open('priya', { width: 1600, height: 1000 })
    await page.goto(`${base}/projects/SYN-105/timeline?zoom=week`); await page.getByRole('table').waitFor(); await settle(page, 1200)
    await page.getByRole('button', { name: 'Show all tasks' }).click(); await page.waitForTimeout(400)
    const tl = await page.evaluate(() => {
      const pick = (s) => [...document.querySelectorAll('[role=cell] button[aria-label]')].find((b) => b.getAttribute('aria-label').includes(s))
      const bar = pick('No start date task'), del = pick('Halifax evening deliverable')
      const w = (b) => b?.getBoundingClientRect().width, f = (b) => b?.querySelector('span.bg-card\\/65')?.getBoundingClientRect().width
      const uns = [...document.querySelectorAll('#tl-unscheduled li')].map((l) => l.textContent.trim())
      return { bar: bar?.getAttribute('aria-label'), barWidth: w(bar), fadeWidth: f(bar), del: del?.getAttribute('aria-label'), uns }
    })
    await shot(page, 'AC-TL-01-backdated-week-zoom')
    await page.locator('#tl-unscheduled').scrollIntoViewIfNeeded(); await shot(page, 'AC-TL-03-timeline')
    const pxPerDay = tl.barWidth / 21 // created 2026-10-02 through due 2026-10-22: 21 days
    await page.goto(`${base}/timeline?mine=false`); await settle(page, 1800)
    const g = await page.evaluate(() => ({ uns: [...document.querySelectorAll('#g-unscheduled li')].map((l) => l.textContent.trim()), noStart: [...document.querySelectorAll('button[aria-label]')].map((b) => b.getAttribute('aria-label')).find((l) => l.includes('No start date task')) }))
    await page.locator('#g-unscheduled').scrollIntoViewIfNeeded().catch(() => undefined); await shot(page, 'AC-TL-03-gantt')
    const timelineApi = (await api('priya', `projects/${(await api('priya', 'projects/SYN-105')).body.id}/timeline`)).body
    const createdOn = JSON.stringify(timelineApi ?? {}).match(/"createdOn":"2026-10-1[12]"/g)
    rec('AC-TL-01-backdated', /2026-10-02 to 2026-10-22/.test(tl.bar ?? '') && /start not set/.test(tl.bar ?? '') && Math.abs(tl.fadeWidth / pxPerDay - 10) <= 1.5 && /start not set/.test(g.noStart ?? ''),
      { bar: tl.bar, barWidth: tl.barWidth, fadeWidth: tl.fadeWidth, fadedDays: Math.round(tl.fadeWidth / pxPerDay * 10) / 10, gantt: g.noStart, screenshot: 'AC-TL-01-backdated-week-zoom.png' })
    rec('AC-TL-03-B', tl.uns.some((u) => u.includes('Client workshop') && /Milestone/.test(u)) && g.uns.some((u) => u.includes('Client workshop')), { timelineUnscheduled: tl.uns, ganttUnscheduled: g.uns, screenshots: ['AC-TL-03-timeline.png', 'AC-TL-03-gantt.png'] })
    rec('AC-TL-04-B', /2026-10-11 to 2026-11-13/.test(tl.del ?? ''), { createdAtUtc: '2026-10-12T01:30:00Z (22:30 on 2026-10-11 in America/Halifax)', deliverableBar: tl.del, createdOnInPayload: createdOn, screenshot: 'AC-TL-01-backdated-week-zoom.png' })
  },
}

;(async () => {
  for (const [name, fn] of Object.entries(sections)) {
    if (only.length && !only.includes(name)) continue
    try { await fn() } catch (e) { rec(`section ${name}`, false, { error: String(e.stack ?? e).slice(0, 700) }) }
  }
  await close()
  const file = `${__dirname}/results-part2.json`
  const prev = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {}
  fs.writeFileSync(file, JSON.stringify({ ...prev, ...results }, null, 1))
})()
