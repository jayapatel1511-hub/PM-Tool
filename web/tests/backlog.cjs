// Packet 051 S06 (FR-BKL-02 to FR-BKL-04, FR-BKL-09) real-API checks against the local API and PostgreSQL: AC-BKL-06 (flag off: no
// view, column, count or quick add, and the browser asks for none of it), AC-BKL-01 (the Backlog list, the dashboard count and the
// Board column agree; Overdue and Due soon leave the Backlog out), AC-BKL-02 (quick add defaults, one log entry), AC-BKL-03
// (a date takes a task out of the Backlog and clearing it returns the task at the end of the manual order) and the drag and bulk
// ways of scheduling (FR-BKL-04). Turns the flag on and restores it; synthetic records only, on an explicitly disposable database.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'this check creates records and sets a flag, so it requires an explicitly disposable database')

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
const until = async (check, what) => { for (let i = 0; i < 60; i++) { if (await check()) return; await new Promise(r => setTimeout(r, 250)) } throw new Error(`timed out: ${what}`) }
const axe = async (page, label) => { // WCAG 2.1 A and AA on the new surfaces
  await page.waitForLoadState('networkidle')
  if (!(await page.evaluate(() => typeof window.axe !== 'undefined'))) await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  const violations = await page.evaluate(async () => (await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } }))
    .violations.map(v => ({ id: v.id, impact: v.impact, nodes: v.nodes.map(n => n.target.join(' ')).slice(0, 5) })))
  assert.deepEqual(violations, [], `${label} at ${page.viewportSize().width}px`)
}
const nextFriday = iso => { const d = new Date(`${iso}T00:00:00Z`); return addDays(iso, ((5 - d.getUTCDay() + 7) % 7) || 7) }

;(async () => {
  const jordan = await api('me', 'jordan')
  const before = !!jordan.flags?.backlog
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  const structural = reference.disciplines.find(x => x.name === 'Structural')
  assert.ok(civil && structural, 'Civil and Structural discipline fixtures missing')
  const people = {}
  for (const who of ['alex', 'priya', 'marc']) people[who] = await api('me', who)
  const today = jordan.settings.today
  const run = suffix() // reruns against the same database must not match an earlier run's rows

  // Alex's home discipline is Civil (FR-GRP-01); the project lists Structural first, so the quick-add default is the home one.
  const alexAdmin = (await api('admin/users?q=alex', 'jordan')).find(u => u.email === 'alex@hub.test')
  await send(`admin/users/${alexAdmin.id}`, 'jordan', { homeDisciplineId: civil.id, rowVersion: alexAdmin.rowVersion }, 'PATCH')

  const created = await send('projects', 'priya', { projectNumber: `K${suffix()}`, name: `Synthetic 051 backlog project ${run}`, clientId: reference.clients[0].id, officeId: reference.offices[0].id,
    disciplines: [{ disciplineId: structural.id, leadUserId: people.marc.id }, { disciplineId: civil.id, leadUserId: people.marc.id }],
    members: [{ userId: people.alex.id, roles: ['TeamMember'], disciplineId: civil.id }] })
  const draft = await api(`projects/${created.id}`, 'priya')
  await send(`projects/${created.id}/transition`, 'priya', { toStatus: 'Active', rowVersion: draft.rowVersion })
  const detail = await api(`projects/${created.id}`, 'priya')
  const number = created.projectNumber
  const disciplines = detail.disciplines ?? detail.projectDisciplines
  const pdCivil = disciplines.find(d => d.disciplineId === civil.id).id
  const pdStructural = disciplines.find(d => d.disciplineId === structural.id).id
  const make = async (name, dates = {}, discipline = pdCivil) => {
    const r = await send(`projects/${created.id}/tasks`, 'priya', { name, projectDisciplineId: discipline, assigneeId: people.alex.id, ...dates })
    return { id: r.id, name }
  }
  // Twenty open tasks, six of them with neither a due date nor a start date (AC-BKL-01).
  const dated = []
  for (let i = 1; i <= 4; i++) dated.push(await make(`Synthetic 051 overdue ${i} ${run}`, { startDate: addDays(today, -20), dueDate: addDays(today, -i) }))
  for (let i = 1; i <= 4; i++) dated.push(await make(`Synthetic 051 due soon ${i} ${run}`, { startDate: addDays(today, -2), dueDate: addDays(today, i) }))
  for (let i = 1; i <= 6; i++) dated.push(await make(`Synthetic 051 later ${i} ${run}`, { dueDate: addDays(today, 20 + i) }))
  const backlog = []
  for (let i = 1; i <= 6; i++) backlog.push(await make(`Synthetic 051 backlog ${i} ${run}`))
  const names = list => list.map(t => t.name)
  const listNames = async (query, who = 'alex') => (await api(`projects/${created.id}/tasks?pageSize=200&${query}`, who)).items.map(r => r.name)
  assert.equal(dated.length + backlog.length, 20)

  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const results = {}, pages = []
  try {
    await setting('flag.backlog', false)
    const session = async who => {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
      await context.addInitScript(email => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
      const page = await context.newPage()
      pages.push(page)
      page.errors = []; page.backlogRequests = []; page.writes = []
      page.on('pageerror', e => page.errors.push(e.message))
      page.on('request', r => {
        if (r.url().includes('/api/v1/') && /backlog/i.test(r.url())) page.backlogRequests.push(`${r.method()} ${new URL(r.url()).pathname}${new URL(r.url()).search}`)
        if (['POST', 'PATCH', 'PUT', 'DELETE'].includes(r.method()) && r.url().includes('/api/v1/')) page.writes.push(`${r.method()} ${new URL(r.url()).pathname}`)
      })
      return page
    }
    const open = async (page, url, ready) => {
      await page.goto(`${base}${url}`, { waitUntil: 'networkidle', timeout: 60000 })
      await page.getByRole('main').first().waitFor()
      if (ready) await ready.waitFor({ timeout: 15000 })
    }
    const alex = await session('alex')
    const tasksUrl = `/projects/${number}/tasks`
    const rowOf = (page, name) => page.getByRole('row').filter({ hasText: name })

    // AC-BKL-06: flag off. The parameter is ignored, nothing Backlog is drawn, and the browser asks for none of it.
    await open(alex, `${tasksUrl}?backlog=true`, rowOf(alex, backlog[0].name))
    assert.equal(await alex.getByRole('heading', { name: 'Backlog', exact: true }).count(), 0, 'no Backlog heading while dark')
    assert.equal(await alex.getByRole('link', { name: 'Backlog', exact: true }).count(), 0, 'no Backlog view link while dark')
    assert.equal(await alex.getByRole('form', { name: 'Quick add to the Backlog' }).count(), 0, 'no quick add while dark')
    assert.equal(await rowOf(alex, dated[0].name).count(), 1, 'the parameter changes nothing: dated tasks are still listed')
    await open(alex, `/projects/${number}/board`)
    await alex.getByText(dated[10].name).first().waitFor({ timeout: 15000 })
    assert.equal(await alex.getByRole('heading', { name: 'Backlog', exact: true }).count() + await alex.getByText('Backlog', { exact: true }).count(), 0, 'no Backlog column while dark')
    await open(alex, `/projects/${number}`)
    await alex.getByRole('main').getByText('Overdue', { exact: true }).first().waitFor({ timeout: 15000 })
    assert.equal(await alex.getByRole('main').getByText('Backlog', { exact: true }).count(), 0, 'no Backlog count while dark')
    assert.deepEqual(alex.backlogRequests, [], 'no Backlog request while dark')
    const darkRows = (await api(`projects/${created.id}/tasks?pageSize=200&backlog=true`, 'alex')).items
    assert.equal(darkRows.length, 20, 'the API ignores the parameter while dark')
    assert.ok(darkRows.every(r => !('backlog' in r)), 'no Backlog row field while dark')
    results['AC-BKL-06'] = { list: 'parameter ignored', board: 'no column', dashboard: 'no count', quickAdd: 'absent', requests: 0 }

    await setting('flag.backlog', true)
    const shot = async (page, name) => { if (process.env.BACKLOG_SHOTS) await page.screenshot({ path: `${process.env.BACKLOG_SHOTS}/${name}.png`, fullPage: true }) }

    // AC-BKL-01: the dashboard count opens the Backlog list, and the Board column holds the same six; Overdue and Due soon leave them out.
    await open(alex, `/projects/${number}`)
    const countLink = alex.getByRole('main').getByRole('link', { name: /^Backlog\s*6$/ })
    await countLink.waitFor({ timeout: 15000 })
    const dash = await api(`projects/${created.id}/dashboard`, 'alex')
    assert.deepEqual([dash.tasks.backlog.value, dash.tasks.total.value, dash.tasks.overdue.value], [6, 20, 4])
    await countLink.click()
    await alex.getByRole('heading', { name: 'Backlog', exact: true }).waitFor({ timeout: 15000 })
    assert.match(alex.url(), /\/tasks\?backlog=true/, 'the count opens the Backlog list')
    await alex.getByText('6 tasks · No due date and no start date').waitFor({ timeout: 15000 })
    const rowNames = async page => (await page.locator('tbody tr td:nth-child(3)').allInnerTexts()).map(x => x.trim())
    const shown = await rowNames(alex)
    assert.deepEqual(shown, names(backlog), 'the list shows exactly the six Backlog tasks in their order')
    assert.deepEqual(await listNames('backlog=true&sort=board'), names(backlog), 'the API list is the same six')
    assert.equal(await alex.getByRole('form', { name: 'Quick add to the Backlog' }).count(), 1, 'quick add on the Backlog view')
    await axe(alex, 'Backlog list')
    await alex.setViewportSize({ width: 390, height: 844 })
    await axe(alex, 'Backlog list')
    await alex.setViewportSize({ width: 1440, height: 1000 })
    assert.ok(!(await listNames('overdue=true')).some(n => names(backlog).includes(n)), 'Overdue leaves the Backlog out')
    assert.ok(!(await listNames('dueSoon=true')).some(n => names(backlog).includes(n)), 'Due soon leaves the Backlog out')
    await open(alex, `/projects/${number}/board`)
    const column = name => alex.getByRole('list', { name, exact: true })
    await column('Backlog').waitFor({ timeout: 15000 })
    assert.equal(await column('Backlog').getByRole('listitem').count(), 6, 'the Board Backlog column holds the same six')
    assert.equal(await column('To Do').getByRole('listitem').count(), 14, 'dated tasks stay in their lanes')
    assert.equal(await alex.getByRole('heading', { name: 'Backlog', exact: true }).count(), 0)
    await axe(alex, 'Board with the Backlog column')
    results['AC-BKL-01'] = { dashboard: '6, links to the list', list: 6, board: 6, overdue: 4, backlogInOverdueOrDueSoon: 0 }

    // AC-BKL-02: quick add. Every default is on the row; Enter makes one ordinary task with one log entry.
    await open(alex, `${tasksUrl}?backlog=true`, rowOf(alex, backlog[0].name))
    const form = alex.getByRole('form', { name: 'Quick add to the Backlog' })
    assert.equal(await form.locator('#bq-discipline').inputValue(), pdCivil, 'home discipline, though the project lists Structural first')
    assert.notEqual(pdCivil, pdStructural)
    assert.equal(await form.locator('#bq-priority').inputValue(), 'Medium')
    assert.ok((await form.innerText()).includes('Alex Chen') || await form.getByRole('combobox', { name: /Assignee/ }).count() > 0, 'the creator is the assignee')
    await form.getByText('No dates', { exact: true }).waitFor()
    const writesBefore = alex.writes.length
    await form.locator('#bq-name').fill('Check culvert sizing')
    await form.locator('#bq-name').press('Enter')
    await alex.getByText(/^[A-Z0-9]+-T\d+ created$/).first().waitFor({ timeout: 15000 })
    await rowOf(alex, 'Check culvert sizing').waitFor({ timeout: 15000 })
    assert.equal(await form.locator('#bq-name').inputValue(), '', 'the input is cleared for the next one')
    assert.deepEqual(alex.writes.slice(writesBefore), [`POST /api/v1/projects/${created.id}/tasks`], 'one ordinary task create')
    const quick = (await api(`projects/${created.id}/tasks?q=${encodeURIComponent('Check culvert sizing')}`, 'alex')).items
    assert.equal(quick.length, 1)
    const q1 = quick[0]
    assert.deepEqual([q1.name, q1.disciplineName, q1.assigneeId, q1.priority, q1.startDate ?? null, q1.dueDate ?? null, q1.deliverableId ?? null, q1.backlog],
      ['Check culvert sizing', 'Civil', people.alex.id, 'Medium', null, null, null, true])
    const log = await api(`items/Task/${q1.id}/activity`, 'alex')
    assert.equal((log.items ?? log).length, 1, 'one activity entry')
    const row = await rowOf(alex, 'Check culvert sizing').innerText()
    assert.ok(row.includes('Civil') && row.includes('Alex Chen') && row.includes('Not Started'), `the row shows every default: ${row}`)
    results['AC-BKL-02'] = { defaults: 'Civil (home), Alex Chen, Medium, no dates', create: 'one POST', activity: 1 }

    // AC-BKL-03: a date takes the task out of the Backlog and into Upcoming; clearing it puts it back at the end of the manual order.
    const arranged = [...backlog].reverse().concat([{ id: q1.id, name: q1.name }])
    await send(`projects/${created.id}/board-order`, 'priya', { taskIds: arranged.map(t => t.id) }, 'PUT')
    await open(alex, `${tasksUrl}?backlog=true`, rowOf(alex, backlog[0].name))
    assert.deepEqual(await rowNames(alex), names(arranged), 'the manual order is kept')
    const first = arranged[0], friday = nextFriday(today)
    // A team member's due-date change needs a reason (T-16); the row asks for it after the date is given.
    const why = async (page, key, text) => {
      const dialog = page.getByRole('dialog', { name: `Why is ${key} changing?` })
      await dialog.waitFor({ timeout: 15000 })
      await dialog.getByLabel('Reason').fill(text)
      await dialog.getByRole('button', { name: 'Confirm' }).click()
    }
    const key = async id => (await api(`tasks/${id}`, 'alex')).task.key
    const firstKey = await key(first.id)
    await alex.getByTitle(`Due date of ${firstKey}`).click()
    await alex.locator('input[type="date"]').fill(friday)
    await alex.locator('input[type="date"]').press('Enter')
    await why(alex, firstKey, 'Pulled into Friday review')
    await rowOf(alex, first.name).waitFor({ state: 'detached', timeout: 15000 })
    assert.equal((await api(`tasks/${first.id}`, 'alex')).task.dueDate, friday)
    assert.ok(!(await listNames('backlog=true')).includes(first.name), 'out of the Backlog')
    assert.equal((await api(`projects/${created.id}/dashboard`, 'alex')).tasks.backlog.value, 6)
    await open(alex, '/my-work?tab=upcoming')
    await alex.getByText(first.name).first().waitFor({ timeout: 15000 })
    await open(alex, tasksUrl, rowOf(alex, first.name))
    await alex.getByTitle(`Due date of ${firstKey}`).click()
    await alex.locator('input[type="date"]').fill('')
    await alex.locator('input[type="date"]').press('Enter')
    await why(alex, firstKey, 'Review moved out of this month')
    await until(async () => (await api(`tasks/${first.id}`, 'alex')).task.dueDate == null, 'the date is cleared')
    const back = await listNames('backlog=true&sort=board')
    assert.equal(back.length, 7)
    assert.equal(back.at(-1), first.name, 'back in the Backlog, at the end of the manual order')
    await open(alex, `${tasksUrl}?backlog=true`, rowOf(alex, first.name))
    assert.equal((await rowNames(alex)).at(-1), first.name, 'the list shows it last')
    results['AC-BKL-03'] = { scheduled: friday, upcoming: 'listed', cleared: 'returned last of 7' }

    // FR-BKL-04: dragging a card out of the Backlog asks for the date first; cancelling puts it back.
    await open(alex, `/projects/${number}/board`)
    await column('Backlog').waitFor({ timeout: 15000 })
    // A pointer drag from a card's handle to the middle of the named lane, inside the visible part of the page.
    const drag = async (card, into) => {
        const handle = alex.getByRole('button', { name: new RegExp(`^Move ${card.key} `) })
      await handle.scrollIntoViewIfNeeded()
      const from = await handle.boundingBox(), to = await column(into).boundingBox()
      assert.ok(from && to)
      const y = Math.min(Math.max(to.y + 90, 150), 900)
      await alex.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
      await alex.mouse.down()
      await alex.mouse.move(to.x + to.width / 2, y - 10, { steps: 12 })
      await alex.mouse.move(to.x + to.width / 2, y, { steps: 4 })
      await alex.mouse.up()
      await alex.waitForTimeout(800) // the drag library swallows a click made the instant after a drop; a person is slower
    }
    const cards = await Promise.all(backlog.map(async t => ({ ...t, key: await key(t.id) })))
    const [c1, c2, c3, c4] = cards.slice(1, 5)
    const writes = alex.writes.length
    await drag(c1, 'To Do')
    const ask = alex.getByRole('dialog').filter({ hasText: `Schedule ${c1.key} ${c1.name}` })
    await ask.waitFor({ timeout: 15000 })
    await ask.getByLabel('Reason').waitFor({ timeout: 15000 }) // T-16: asked for up front, since Alex is not a manager
    await axe(alex, 'Schedule dialog')
    assert.equal(alex.writes.length, writes, 'nothing is saved before the date is given')
    await ask.getByRole('button', { name: 'Cancel' }).click()
    await ask.waitFor({ state: 'detached' })
    assert.equal(await column('Backlog').getByRole('listitem').filter({ hasText: c1.name }).count(), 1, 'cancelling puts the card back')
    assert.equal(alex.writes.length, writes, 'cancelling saves nothing')
    await drag(c1, 'To Do')
    await ask.waitFor({ timeout: 15000 })
    assert.equal(await ask.getByRole('button', { name: 'Schedule', exact: true }).isDisabled(), true, 'a date is required')
    await ask.locator('#schedule-due').fill(addDays(today, 9))
    await ask.getByLabel('Reason').fill('Needed for the next site visit')
    await ask.getByRole('button', { name: 'Schedule', exact: true }).click()
    await ask.waitFor({ state: 'detached', timeout: 15000 })
    await column('To Do').getByRole('listitem').filter({ hasText: c1.name }).waitFor({ timeout: 15000 })
    assert.equal(await column('Backlog').getByRole('listitem').filter({ hasText: c1.name }).count(), 0, 'the card left the Backlog column')
    assert.equal((await api(`tasks/${c1.id}`, 'alex')).task.dueDate, addDays(today, 9))
    // Nothing is dropped into the Backlog: it is the way back only by clearing the dates.
    const before2 = alex.writes.length
    await drag(c1, 'Backlog')
    await alex.getByText("Clear a task's dates to return it to the Backlog.").waitFor({ timeout: 15000 })
    assert.equal(alex.writes.length, before2, 'dropping a card into the Backlog saves nothing')
    assert.equal(await column('To Do').getByRole('listitem').filter({ hasText: c1.name }).count(), 1, 'the card snaps back')
    // The column folds away and stays folded for this viewer.
    await alex.getByRole('button', { name: 'Collapse the Backlog' }).click()
    await column('Backlog').waitFor({ state: 'detached' })
    await alex.getByRole('button', { name: /^Show the Backlog \(\d+\)$/ }).waitFor()
    await alex.reload({ waitUntil: 'networkidle' })
    await alex.getByRole('button', { name: /^Show the Backlog \(\d+\)$/ }).waitFor({ timeout: 15000 })
    assert.equal(await column('Backlog').count(), 0, 'still folded after a reload')
    await alex.getByRole('button', { name: /^Show the Backlog/ }).click()
    await column('Backlog').waitFor({ timeout: 15000 })
    results['FR-BKL-04 drag'] = { askedFirst: true, cancel: 'card back, no write', scheduled: addDays(today, 9), dropIntoBacklog: 'refused', collapse: 'kept after reload' }

    // FR-BKL-04: the bulk date action on the Backlog list (a shift has no date to move, so it is not offered there).
    const priya = await session('priya')
    await open(priya, `${tasksUrl}?backlog=true`, rowOf(priya, c2.name))
    await priya.getByRole('checkbox', { name: `Select ${c2.key}` }).check()
    await priya.getByRole('checkbox', { name: `Select ${c3.key}` }).check()
    const bar = priya.getByRole('region', { name: 'Bulk actions' })
    await bar.waitFor()
    assert.equal(await bar.getByRole('button', { name: 'Shift due dates' }).count(), 0, 'no shift on undated work')
    await bar.getByRole('button', { name: 'Set due date' }).click()
    const bulk = priya.getByRole('dialog')
    await bulk.locator('#tb-due').fill(addDays(today, 14))
    await bulk.getByRole('button', { name: 'Confirm' }).click()
    await rowOf(priya, c2.name).waitFor({ state: 'detached', timeout: 15000 })
    await rowOf(priya, c3.name).waitFor({ state: 'detached', timeout: 15000 })
    for (const c of [c2, c3]) assert.equal((await api(`tasks/${c.id}`, 'alex')).task.dueDate, addDays(today, 14))
    assert.ok(!(await listNames('backlog=true')).some(n => [c2.name, c3.name, c1.name].includes(n)), 'all scheduled tasks left the Backlog')
    results['FR-BKL-04 bulk'] = { by: 'the project manager', dueDate: addDays(today, 14), left: 2 }

    // With the flag off again, the dates stay and everything reads as before (AC-BKL-06).
    await setting('flag.backlog', false)
    await open(alex, `${tasksUrl}?backlog=true`)
    await alex.getByText(c1.name).first().waitFor({ timeout: 15000 })
    assert.equal(await alex.getByRole('heading', { name: 'Backlog', exact: true }).count(), 0)
    assert.equal(await alex.getByRole('form', { name: 'Quick add to the Backlog' }).count(), 0)
    await setting('flag.backlog', true)
    assert.deepEqual(alex.errors, [], 'no page errors')
    console.log(JSON.stringify({ scope: 'Chromium against the local API and PostgreSQL', results }))
  } catch (e) {
    if (process.env.BACKLOG_SHOTS) for (const [i, page] of pages.entries()) await page.screenshot({ path: `${process.env.BACKLOG_SHOTS}/fail-${i}.png`, fullPage: true }).catch(() => {})
    throw e
  } finally {
    await setting('flag.backlog', before).catch(e => console.error(e.message))
    await browser.close()
  }
})().catch(e => { console.error(e); process.exitCode = 1 })
