// Packet 049 real-API checks against the local API and PostgreSQL: AC-PKP-08 (flag off: no pickup link, route, action,
// marker or request), AC-PKP-01 (offer from the task panel; fields unchanged; the right people notified), AC-PKP-09 (Sam lists
// the offer with Assign and no Pick up; his pick-up is 403), AC-PKP-03 (Diane's list omits it), AC-PKP-02 (Jill picks it up
// from Open for pickup) and AC-PKP-06 (a Restricted project's offer is listed and reported only to people who can view it).
// Turns on the flags it needs and restores them; synthetic records only, on an explicitly disposable database.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'this check creates records and sets flags, so it requires an explicitly disposable database')

async function raw(pathname, who, options = {}) {
  return fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json' } })
}
async function api(pathname, who, options = {}) {
  const response = await raw(pathname, who, options)
  if (!response.ok) throw new Error(`${options.method ?? 'GET'} ${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const send = (pathname, who, body, method = 'POST') => api(pathname, who, { method, body: JSON.stringify(body) })
const setting = (key, value) => send(`admin/settings/${key}`, 'jordan', { value }, 'PUT')
const addDays = (iso, n) => new Date(Date.parse(`${iso}T00:00:00Z`) + n * 86400000).toISOString().slice(0, 10)
const suffix = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`.toUpperCase().slice(-7)
const fields = t => ({ assigneeId: t.assigneeId, status: t.status, startDate: t.startDate, dueDate: t.dueDate, state: t.state, rowVersion: t.rowVersion })

;(async () => {
  const jordan = await api('me', 'jordan')
  const before = { task_pickup: !!jordan.flags?.task_pickup, org_structure: !!jordan.flags?.org_structure, restricted: !!jordan.settings.restrictedProjectsEnabled }
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  assert.ok(civil, 'Civil discipline fixture missing')
  const people = {}
  for (const who of ['alex', 'jill', 'marc', 'sam', 'diane', 'priya']) people[who] = await api('me', who)
  // The seeded team (Seed.DevUsers): Alex, Jill and Marc report to Sam; Diane reports to Lena. Sam is outside Alex's team.
  const today = jordan.settings.today
  const run = suffix() // reruns against the same database must not match an earlier run's rows
  const project = async (name, members) => {
    const created = await send('projects', 'priya', { projectNumber: `K${suffix()}`, name, clientId: reference.clients[0].id, officeId: reference.offices[0].id,
      disciplines: [{ disciplineId: civil.id, leadUserId: people.marc.id }], members: members.map(who => ({ userId: people[who].id, roles: ['TeamMember'], disciplineId: civil.id })) })
    const draft = await api(`projects/${created.id}`, 'priya')
    await send(`projects/${created.id}/transition`, 'priya', { toStatus: 'Active', rowVersion: draft.rowVersion })
    const detail = await api(`projects/${created.id}`, 'priya')
    return { id: created.id, number: created.projectNumber, discipline: (detail.disciplines ?? detail.projectDisciplines)[0].id, rowVersion: detail.rowVersion }
  }
  const task = async (p, name) => {
    const created = await send(`projects/${p.id}/tasks`, 'priya', { name, projectDisciplineId: p.discipline, assigneeId: people.alex.id, startDate: addDays(today, -2), dueDate: addDays(today, 10) })
    const { task: t } = await api(`tasks/${created.id}`, 'priya')
    assert.ok(t.key, 'task key')
    return { id: t.id, key: t.key, name: t.name }
  }

  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const results = {}
  try {
    await setting('flag.org_structure', false) // the team is everyone with the assignee's direct manager (FR-PKP-06)
    await setting('flag.task_pickup', false)
    const normal = await project('Synthetic 049 pickup project', ['alex', 'jill', 'diane'])
    const t1 = await task(normal, `Synthetic 049 drainage memo ${run}`)
    const session = async who => {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
      await context.addInitScript(email => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
      const page = await context.newPage()
      page.errors = []; page.pickupRequests = []
      page.on('pageerror', e => page.errors.push(e.message))
      page.on('request', r => { if (/\/api\/v1\/(pickup-offers|tasks\/[^/]+\/pickup)/.test(r.url())) page.pickupRequests.push(`${r.method()} ${new URL(r.url()).pathname}`) })
      return page
    }
    const open = async (page, url) => { await page.goto(`${base}${url}`, { waitUntil: 'networkidle', timeout: 60000 }); await page.getByRole('main').first().waitFor() }
    const openTask = async (page, t, p = normal) => { await page.goto(`${base}/projects/${p.number}/tasks?panel=Task:${t.id}`, { waitUntil: 'networkidle', timeout: 60000 }); await page.getByRole('heading', { name: t.name }).first().waitFor({ timeout: 15000 }) }
    const myWorkLinks = async (page, on) => {
      const seen = []
      for (const tab of ['overview', 'upcoming']) {
        await open(page, `/my-work?tab=${tab}`)
        await page.getByRole('main').getByRole('button', { name: 'New task', exact: true }).first().waitFor({ timeout: 15000 })
        const link = page.getByRole('main').getByRole('link', { name: 'Open for pickup', exact: true })
        if (on) await link.waitFor({ timeout: 15000 })
        seen.push([tab, await link.count()])
      }
      return seen
    }

    // AC-PKP-08: flag off. No My Work link, nav entry, route, panel action or marker, and the browser asks for nothing pickup.
    const alex = await session('alex')
    assert.deepEqual(await myWorkLinks(alex, false), [['overview', 0], ['upcoming', 0]])
    assert.equal(await alex.getByRole('link', { name: 'Open for pickup', exact: true }).count(), 0, 'no navigation entry while dark')
    await open(alex, '/pickup')
    await alex.getByText(/This page or item does not exist/i).waitFor({ timeout: 15000 })
    await openTask(alex, t1)
    await alex.waitForLoadState('networkidle')
    assert.equal(await alex.getByRole('button', { name: 'Offer for pickup' }).count(), 0, 'no offer action while dark')
    assert.equal(await alex.getByText(/Open for pickup/).count(), 0, 'no marker while dark')
    await open(alex, `/projects/${normal.number}/tasks`)
    await alex.getByText(t1.name).first().waitFor({ timeout: 15000 })
    assert.equal(await alex.getByText(/Open for pickup/).count(), 0, 'no list chip while dark')
    assert.deepEqual(alex.pickupRequests, [], 'no pickup request while dark')
    const darkDetail = await api(`tasks/${t1.id}`, 'alex')
    assert.equal(darkDetail.task.pickupOfferOpen ?? null, null, 'no marker field while dark')
    assert.equal((await raw('pickup-offers', 'alex')).status, 404)
    results['AC-PKP-08'] = { myWork: 'link absent on Overview and Upcoming', route: 'Not found', panel: 'no offer action or marker', requests: 0 }

    // AC-PKP-01: Alex offers from the task panel. The task shows Open for pickup; its fields are unchanged; the team, the
    // manager, the PM and the Discipline Lead are notified.
    await setting('flag.task_pickup', true)
    assert.deepEqual(await myWorkLinks(alex, true), [['overview', 1], ['upcoming', 1]])
    const t1Before = fields((await api(`tasks/${t1.id}`, 'priya')).task)
    await openTask(alex, t1)
    await alex.getByRole('button', { name: 'Offer for pickup' }).click()
    const offerDialog = alex.getByRole('dialog').filter({ hasText: `Offer ${t1.key} for pickup` })
    await offerDialog.locator('#pickup-reason').fill('Two deliverables due W42')
    await offerDialog.locator('#pickup-hours').fill('6')
    await offerDialog.locator('#pickup-needed-by').fill(addDays(today, 7))
    await offerDialog.getByRole('button', { name: 'Offer for pickup' }).click()
    await offerDialog.waitFor({ state: 'detached', timeout: 15000 })
    await alex.getByRole('button', { name: 'Withdraw pickup offer' }).waitFor({ timeout: 15000 })
    await alex.getByText('Open for pickup', { exact: true }).first().waitFor({ timeout: 15000 })
    const t1After = (await api(`tasks/${t1.id}`, 'priya')).task
    assert.deepEqual(fields(t1After), t1Before, 'offering changes no task field')
    assert.equal(t1After.pickupOfferOpen, true)
    const offered = {}
    for (const who of ['alex', 'jill', 'marc', 'sam', 'priya', 'diane']) {
      const page = await api('me/notifications?type=TaskPickupOffered', who)
      offered[who] = (page.items ?? page).filter(n => n.itemId === t1.id).length
    }
    assert.deepEqual(offered, { alex: 0, jill: 1, marc: 1, sam: 1, priya: 1, diane: 0 }, 'teammates, manager, PM and Discipline Lead; not the actor or an outsider')
    await open(alex, `/projects/${normal.number}/tasks`)
    await alex.getByRole('row').filter({ hasText: t1.name }).getByText(/Open for pickup/).waitFor({ timeout: 15000 })
    const jill = await session('jill')
    await open(jill, '/notifications')
    await jill.getByText(`${t1.key} is open for pickup: ${t1.name}`).first().waitFor({ timeout: 15000 })
    results['AC-PKP-01'] = { marker: 'panel and task list', unchanged: Object.keys(t1Before), notified: offered }

    // AC-PKP-09: Sam, Alex's direct manager outside the team, sees the offer with Assign and without Pick up; his pick-up is 403.
    const sam = await session('sam')
    await open(sam, '/pickup')
    const samRow = sam.getByRole('listitem').filter({ hasText: t1.name })
    await samRow.waitFor({ timeout: 15000 })
    assert.equal(await samRow.getByRole('link', { name: 'Assign pickup', exact: true }).count(), 1)
    assert.equal(await samRow.getByRole('button', { name: 'Pick up', exact: true }).count(), 0)
    const samPick = await sam.evaluate(async ({ id, rowVersion }) => (await fetch(`/api/v1/tasks/${id}/pickup`, { method: 'POST', headers: { 'X-Dev-User': 'sam@hub.test', 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion }) })).status, { id: t1.id, rowVersion: t1After.rowVersion })
    assert.equal(samPick, 403)
    // AC-PKP-07 in the panel: Assign offers only Alex's team.
    await samRow.getByRole('link', { name: 'Assign pickup', exact: true }).click()
    const assignSelect = sam.getByRole('combobox', { name: 'Assign pickup offer' })
    await assignSelect.waitFor({ timeout: 15000 })
    const candidates = (await assignSelect.locator('option').allInnerTexts()).slice(1).sort()
    assert.deepEqual(candidates, ['Jill Martin', 'Marc Dubois'])
    results['AC-PKP-09'] = { list: 'Assign pickup, no Pick up', pickUp: samPick, assignCandidates: candidates }

    // AC-PKP-03: Diane (outside the team, not an assigner) does not see it and cannot pick it up.
    const diane = await session('diane')
    await open(diane, '/pickup')
    await diane.getByText('No tasks are open for pickup.').waitFor({ timeout: 15000 })
    assert.equal(await diane.getByText(t1.name).count(), 0)
    assert.equal((await raw(`tasks/${t1.id}/pickup`, 'diane', { method: 'POST', body: JSON.stringify({ rowVersion: t1After.rowVersion }) })).status, 403)
    assert.equal((await api(`tasks/${t1.id}`, 'priya')).task.assigneeId, people.alex.id)
    results['AC-PKP-03'] = { list: 'omitted', pickUp: 403 }

    // AC-PKP-02: Jill picks it up from Open for pickup, with no approval.
    await open(jill, '/pickup')
    const jillRow = jill.getByRole('listitem').filter({ hasText: t1.name })
    await jillRow.waitFor({ timeout: 15000 })
    const rowText = await jillRow.innerText()
    for (const part of [normal.number, 'Civil', 'Alex Chen', '6 h remaining', 'Two deliverables due W42']) assert.ok(rowText.includes(part), `row shows ${part}: ${rowText}`)
    await jillRow.getByText(/^Your workload for /).waitFor({ timeout: 15000 })
    await jillRow.getByRole('button', { name: 'Pick up', exact: true }).click()
    const pickDialog = jill.getByRole('dialog').filter({ hasText: `Pick up ${t1.key}` })
    await pickDialog.getByRole('button', { name: 'Pick up task' }).click()
    await jill.getByText('Task picked up').first().waitFor({ timeout: 15000 })
    await jillRow.waitFor({ state: 'detached', timeout: 15000 })
    const picked = await api(`tasks/${t1.id}`, 'priya')
    assert.equal(picked.task.assigneeId, people.jill.id)
    assert.ok(picked.watchers.some(w => w.id === people.alex.id), 'Alex becomes a watcher')
    assert.equal((await raw(`tasks/${t1.id}/pickup-offer`, 'priya')).status, 404, 'the offer is closed')
    results['AC-PKP-02'] = { assignee: 'Jill Martin', watcher: 'Alex Chen', offer: 'closed' }

    // AC-PKP-06: a Restricted project's offer, for a same-team member and a same-team nonmember.
    await setting('restricted_projects_enabled', true)
    const restricted = await project('Synthetic 049 restricted project', ['alex', 'marc'])
    await send(`projects/${restricted.id}`, 'priya', { visibility: 'Restricted', rowVersion: restricted.rowVersion }, 'PATCH')
    const t2 = await task(restricted, `Synthetic 049 restricted check ${run}`)
    const hiddenReason = `Restricted synthetic 049 reason ${run}`
    await send(`tasks/${t2.id}/pickup-offer`, 'alex', { reason: hiddenReason, hoursRemaining: 13.25 })
    const marc = await session('marc')
    await open(marc, '/pickup')
    const marcRow = marc.getByRole('listitem').filter({ hasText: t2.name })
    await marcRow.waitFor({ timeout: 15000 })
    assert.match(await marcRow.innerText(), /13\.25 h remaining/)
    await open(marc, '/reports/pickup-offers')
    await marc.getByRole('main').getByText(restricted.number).first().waitFor({ timeout: 15000 })
    // Jill is in Alex's team and Sam may assign Alex's work, but neither can view the Restricted project.
    for (const page of [jill, sam]) {
      await open(page, '/pickup')
      await page.getByText('No tasks are open for pickup.').or(page.getByRole('list', { name: 'Open for pickup' })).first().waitFor({ timeout: 15000 })
      const text = await page.getByRole('main').innerText()
      for (const secret of [t2.name, hiddenReason, '13.25', restricted.number]) assert.ok(!text.includes(secret), `${secret} hidden from the list`)
    }
    // The report lists the team's offers; Jill's shows the offer she picked up, not the Restricted one.
    await open(jill, '/reports/pickup-offers')
    await jill.getByRole('main').getByText(normal.number).first().waitFor({ timeout: 15000 })
    assert.ok(!(await jill.getByRole('main').innerText()).includes(restricted.number), 'the Restricted project is absent from the report')
    results['AC-PKP-06'] = { member: 'listed with 13.25 h and reported', nonmembers: 'Jill (same team): absent from list and report; Sam (manager): absent from list' }

    for (const page of [alex, jill, sam, diane, marc]) assert.deepEqual(page.errors, [], 'no page errors')
  } finally {
    await setting('flag.task_pickup', before.task_pickup).catch(e => console.error(e.message))
    await setting('flag.org_structure', before.org_structure).catch(e => console.error(e.message))
    await setting('restricted_projects_enabled', before.restricted).catch(e => console.error(e.message))
    await browser.close()
  }
  console.log(JSON.stringify({ scope: 'Chromium against the local API and PostgreSQL', results }))
})().catch(e => { console.error(e); process.exitCode = 1 })
