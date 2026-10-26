// Packet 046 real-API browser check against a disposable local API and PostgreSQL (run by web/scripts/run-smoke-live.mjs).
// AC-INT-01: the same probes find every addition of the six flags when they are on and none when they are off; the new
// endpoints answer 404 and GET /users keeps today's order while off; a stored PM baseline leaves slip unchanged and
// slip_display_days has no effect while schedule_baseline is off.
// AC-INT-29: every new control is reached with Tab and operated with the keyboard alone, its result read back from the
// API; axe (WCAG 2.1 A and AA) passes on every changed screen at desktop (1440 px) and phone (390 px) widths.
// Synthetic records only; the original flag and setting values are restored at the end.
const assert = require('node:assert/strict')
const { randomUUID } = require('node:crypto')
const fs = require('node:fs')
const path = require('node:path')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'this check writes records and flags, so it requires an explicitly disposable database')

const FLAGS = ['row_quick_actions', 'picker_project_rank', 'date_context', 'schedule_baseline', 'small_ux', 'timeline_handoffs_chain']
const DESKTOP = { width: 1440, height: 1000 }, PHONE = { width: 390, height: 844 }

async function call(pathname, who, options = {}) {
  return fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { 'X-Dev-User': `${who}@hub.test`, 'X-Hub-Source': 'UI', 'Content-Type': 'application/json', ...(options.headers ?? {}) } })
}
async function api(pathname, who = 'priya', options = {}) {
  const response = await call(pathname, who, options)
  if (!response.ok) throw new Error(`${options.method ?? 'GET'} ${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const send = (method, pathname, body, who = 'priya') => api(pathname, who, { method, body: JSON.stringify(body) })
const setting = (key, value) => send('PUT', `admin/settings/${key}`, { value }, 'jordan')
const setFlags = async on => { for (const f of FLAGS) await setting(`flag.${f}`, on) }

async function seed() {
  const reference = await api('reference')
  const named = (list, name) => { const x = reference[list].find(r => r.name === name); assert.ok(x, `${name} missing from ${list}`); return x.id }
  const today = (await api('me')).settings.today
  const day = n => { const d = new Date(`${today}T00:00:00Z`); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10) }
  const user = async who => (await api('me', who)).id
  const [marc, omar, alex] = [await user('marc'), await user('omar'), await user('alex')]
  const created = await send('POST', 'projects', {
    projectNumber: `I${randomUUID().replaceAll('-', '').slice(0, 7).toUpperCase()}`, name: 'Synthetic 046 interaction', clientId: reference.clients[0].id,
    officeId: reference.offices[0].id, startDate: day(-30), targetCompletionDate: day(20),
    disciplines: [{ disciplineId: named('disciplines', 'Civil'), leadUserId: marc }, { disciplineId: named('disciplines', 'Electrical'), leadUserId: omar }],
    members: [{ userId: alex, roles: ['TeamMember'], disciplineId: named('disciplines', 'Civil') }],
  })
  await send('POST', `projects/${created.id}/transition`, { toStatus: 'Active', rowVersion: (await api(`projects/${created.id}`)).rowVersion })
  const project = await api(`projects/${created.id}`)
  const civil = project.disciplines.find(x => x.name === 'Civil').id, electrical = project.disciplines.find(x => x.name === 'Electrical').id
  const milestone = await send('POST', `projects/${project.id}/milestones`, { name: 'Synthetic design review', milestoneType: 'Design Submission', date: day(35) })
  const deliverable = await send('POST', `projects/${project.id}/deliverables`, { name: 'Synthetic drawing set', projectDisciplineId: civil, deliverableTypeId: named('deliverableTypes', 'Drawing Package'), milestoneId: milestone.id, startDate: day(-5), dueDate: day(30) })
  const task = body => send('POST', `projects/${project.id}/tasks`, { projectDisciplineId: civil, assigneeId: alex, ...body })
  const first = await task({ name: 'Synthetic survey', deliverableId: deliverable.id, startDate: day(1), dueDate: day(5) })
  await task({ name: 'Synthetic design', deliverableId: deliverable.id, startDate: day(6), dueDate: day(10), dependsOn: [first.id] })
  await task({ name: 'Synthetic site visit', startDate: day(1), dueDate: day(3) })
  const receiving = await task({ name: 'Synthetic lighting layout', projectDisciplineId: electrical, assigneeId: omar, startDate: day(7), dueDate: day(12) })
  const source = (await api(`deliverables/${deliverable.id}`)).deliverable
  await send('POST', `projects/${project.id}/handoffs`, {
    requestId: randomUUID(), title: 'Synthetic civil to electrical', sourceDeliverableId: deliverable.id, sourceRowVersion: source.rowVersion,
    declaredRevision: 'A', sourceUrl: 'https://example.test/synthetic.pdf', receivingDisciplineId: electrical, sendingOwnerId: marc, receivingOwnerId: omar,
    targetTaskId: receiving.id, intendedUse: 'Synthetic input', acceptanceCriteria: 'Synthetic criteria', neededBy: day(4), promisedBy: day(6),
  })
  // Two edits of one task within minutes, so grouped activity has a group to show.
  const tasks = Object.fromEntries((await api(`projects/${project.id}/tasks`)).items.map(t => [t.name, t]))
  const visit = tasks['Synthetic site visit']
  const edited = await send('PATCH', `tasks/${visit.id}`, { priority: 'High', rowVersion: visit.rowVersion })
  await send('PATCH', `tasks/${visit.id}`, { estimatedHours: 6, rowVersion: edited.rowVersion })
  // Five weekdays from next Monday, two of them over an 8-hour day (the "2 of 5 dates" wording).
  const monday = (() => { const d = new Date(`${today}T00:00:00Z`); d.setUTCDate(d.getUTCDate() + ((8 - d.getUTCDay()) % 7 || 7)); return d.toISOString().slice(0, 10) })()
  const weekday = n => { const d = new Date(`${monday}T00:00:00Z`); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10) }
  await send('POST', `projects/${project.id}/allocations`, {
    requestId: randomUUID(), personId: alex, purpose: 'Production', fromDate: weekday(0), throughDate: weekday(4), plannedHours: 44,
    days: [12, 12, 6, 7, 7].map((hours, i) => ({ workDate: weekday(i), hours })), links: [{ workType: 'Task', workId: first.id, workDate: weekday(0) }], reason: null,
  })
  const shared = { name: 'Synthetic shared by status', ...await send('POST', 'views', { name: 'Synthetic shared by status', listType: 'tasks', projectId: project.id, scope: 'Project', params: { group: 'status' }, isDefault: false }) }
  return { day, project, milestone, deliverable: source, shared, first: tasks['Synthetic survey'], second: tasks['Synthetic design'], visit }
}

async function settled(page) {
  await page.waitForLoadState('networkidle').catch(() => {})
  await page.evaluate(async () => Promise.all(document.getAnimations().filter(a => a.effect?.getTiming().iterations !== Infinity).map(a => a.finished.catch(() => {}))))
}
async function axe(page, label, audits) {
  await settled(page)
  if (!(await page.evaluate(() => typeof window.axe !== 'undefined'))) await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  const violations = await page.evaluate(async () => (await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } }))
    .violations.map(v => ({ id: v.id, impact: v.impact, nodes: v.nodes.map(n => n.target.join(' ')).slice(0, 5) })))
  audits.push({ screen: label, width: page.viewportSize().width, violations: violations.length })
  assert.deepEqual(violations, [], `${label} at ${page.viewportSize().width}px`)
}
/** Presses Tab from the current focus until `target` holds focus; fails if it is not in the tab order. */
async function tabTo(page, target, what, max = 80) {
  const handle = await target.elementHandle()
  for (let i = 0; i <= max; i++) {
    if (await handle.evaluate(el => el === document.activeElement)) return i
    await page.keyboard.press('Tab')
  }
  assert.fail(`${what} is not reachable with Tab`)
}
async function until(read, ok, what) {
  const deadline = Date.now() + 15000
  let value
  while (Date.now() < deadline) { value = await read(); if (ok(value)) return value; await new Promise(r => setTimeout(r, 200)) }
  assert.fail(`${what}: ${JSON.stringify(value)}`)
}
const focusedName = page => page.evaluate(() => document.activeElement?.getAttribute('aria-label') ?? document.activeElement?.textContent?.trim())

;(async () => {
  const settings = await api('admin/settings', 'jordan')
  const original = settings.filter(x => [...FLAGS.map(f => `flag.${f}`), 'slip_display_days'].includes(x.key))
  assert.equal(original.length, FLAGS.length + 1, 'packet 046 flags or slip_display_days missing from settings')
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const report = { scope: 'Local API and PostgreSQL, synthetic records, headless Chromium; not hosted, phone-device, screen-reader or human evidence', flagPair: {}, keyboard: {}, schedule: {}, axe: [] }
  try {
    await setFlags(false); await setting('slip_display_days', 0)
    const s = await seed()
    const p = s.project, n = p.projectNumber
    report.project = n
    const session = async (viewport, who = 'Priya Nair') => {
      const context = await browser.newContext({ viewport, reducedMotion: 'reduce', locale: 'en-US' })
      const page = await context.newPage()
      page.setDefaultTimeout(15000)
      page.errors = []
      page.on('pageerror', e => page.errors.push(e.message))
      page.writes = []
      page.on('request', r => { if (r.url().includes('/api/v1/') && r.method() !== 'GET') page.writes.push(`${r.method()} ${new URL(r.url()).pathname}`) })
      await page.goto(`${base}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
      await page.getByRole('button', { name: new RegExp(who, 'i') }).click()
      await page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 30000 })
      return page
    }
    const open = async (page, url) => { await page.goto(`${base}${url}`, { waitUntil: 'domcontentloaded', timeout: 60000 }); await page.locator('main').first().waitFor({ state: 'attached' }); await settled(page) }
    const panel = page => page.getByRole('dialog').first()
    const openTask = async (page, t) => { await open(page, `/projects/${n}/tasks?group=none&panel=Task:${t.id}`); await panel(page).getByRole('heading', { name: t.name, exact: true }).waitFor() }
    const startDate = page => panel(page).getByText('Start date', { exact: true }).locator('..').getByRole('button').first()
    const assigneePicker = page => panel(page).getByText('Assignee', { exact: true }).locator('..').getByRole('button').first()
    let sam
    const supervisor = async () => (sam ??= await session(DESKTOP, 'Sam Patel'))
    const quiet = page => page.waitForTimeout(600).then(() => settled(page))

    // ---- AC-INT-01: one probe per addition, run with every flag off and then on.
    const probes = {
      row_quick_actions: async page => { await open(page, `/projects/${n}/tasks?group=none`); await page.getByText(s.first.name, { exact: true }).first().waitFor(); return await page.getByRole('button', { name: `Open ${s.first.key}`, exact: true }).count() + await page.getByRole('button', { name: `Rename ${s.first.key}`, exact: true }).count() === 2 },
      picker_project_rank: async page => { await openTask(page, s.second); await assigneePicker(page).click(); await page.getByRole('option', { name: /Alex Chen/ }).first().waitFor(); const on = await page.getByRole('option', { name: /On this project/ }).count() > 0; await page.keyboard.press('Escape'); return on },
      date_context: async page => { await openTask(page, s.second); await startDate(page).click(); await page.keyboard.press('ArrowDown'); await quiet(page); const on = await panel(page).getByText(/Earliest consistent start/).count() > 0; await page.keyboard.press('Escape'); return on },
      'schedule_baseline (deliverable)': async page => { await open(page, `/projects/${n}/deliverables?panel=Deliverable:${s.deliverable.id}`); await panel(page).getByRole('heading', { name: s.deliverable.name }).first().waitFor(); return await panel(page).getByRole('button', { name: 'Re-baseline', exact: true }).count() === 1 },
      'schedule_baseline (milestone)': async page => { await open(page, `/projects/${n}/milestones?panel=Milestone:${s.milestone.id}`); await panel(page).getByText('Synthetic design review').first().waitFor(); return await panel(page).getByRole('button', { name: 'Re-baseline', exact: true }).count() === 1 },
      'schedule_baseline (dashboard note)': async page => { await open(page, `/projects/${n}`); await page.getByRole('main').getByText(p.name).first().waitFor(); return await page.getByText('Planned completion after target', { exact: true }).count() > 0 },
      'small_ux (last activity actor)': async page => { await open(page, `/projects/${n}/tasks?group=none`); await page.getByText(s.first.name, { exact: true }).first().waitFor(); return await page.getByText(/ · Priya Nair$/).count() > 0 },
      'small_ux (deliverable from task)': async page => { await openTask(page, s.second); return await panel(page).getByRole('button', { name: 'Create deliverable from this task', exact: true }).count() === 1 },
      'small_ux (grouped activity)': async page => { await open(page, `/projects/${n}/activity`); await page.getByRole('main').getByText(s.visit.key).first().waitFor(); return await page.getByRole('checkbox', { name: 'Group changes', exact: true }).count() === 1 },
      'small_ux (copy as personal view)': async page => { await open(page, `/projects/${n}/tasks?view=${s.shared.id}&group=status`); await page.getByRole('button', { name: s.shared.name, exact: true }).click(); await page.getByRole('menuitem').first().waitFor(); const on = await page.getByRole('menuitem', { name: 'Copy as personal view', exact: true }).count() === 1; await page.keyboard.press('Escape'); return on },
      'small_ux (staff views)': async () => { const page = await supervisor(); await open(page, '/staff'); await page.getByRole('heading', { level: 1 }).first().waitFor(); return await page.getByRole('button', { name: 'Views', exact: true }).count() === 1 },
      'small_ux (planner link)': async page => { await open(page, '/portfolio'); await page.getByRole('heading', { name: 'Portfolio', exact: true }).waitFor(); return await page.getByRole('link', { name: 'Capacity by discipline', exact: true }).count() === 1 },
      'small_ux (Gantt collapse in URL)': async page => { await open(page, `/timeline?projects=${p.id}`); await page.getByRole('button', { name: `Collapse ${n}`, exact: true }).click(); await page.getByRole('button', { name: `Expand ${n}`, exact: true }).waitFor(); return new URL(page.url()).searchParams.get('collapsed') === p.id },
      // Capacity review belongs to the person's supervisor (Sam Patel supervises Alex Chen).
      'small_ux (n of m dates)': async () => { const page = await supervisor(); await open(page, `/projects/${n}/allocations`); await page.getByRole('button', { name: /Alex Chen/ }).first().click(); await page.getByRole('button', { name: 'Review capacity', exact: true }).click(); const text = page.getByText(/dates exceed available capacity/); await text.first().waitFor(); const on = /2 of 5 dates/.test(await text.first().innerText()); await page.keyboard.press('Escape'); return on },
      'timeline_handoffs_chain (handoff row)': async page => { await open(page, `/projects/${n}/timeline`); await page.getByRole('heading', { name: 'Timeline', exact: true }).waitFor(); return await page.getByRole('link', { name: new RegExp(`${n}-H001`) }).count() > 0 },
      'timeline_handoffs_chain (chain filter)': async page => { await open(page, `/projects/${n}/timeline?chainTo=${s.milestone.id}`); await page.getByRole('heading', { name: 'Timeline', exact: true }).waitFor(); return await page.getByText(/tasks lead to this milestone/).count() > 0 },
    }
    const probe = async (page, on) => {
      const seen = {}
      for (const [name, check] of Object.entries(probes)) { seen[name] = await check(page); assert.equal(seen[name], on, `${name} with the flags ${on ? 'on' : 'off'}`) }
      return seen
    }

    const pm = await session(DESKTOP)
    for (const [pathname, body] of [[`items/Task/${s.second.id}/date-context`], [`milestones/${s.milestone.id}/rebaseline`, { reason: 'Synthetic', clear: false, rowVersion: 1 }], [`deliverables/${s.deliverable.id}/rebaseline`, { reason: 'Synthetic', clear: false, rowVersion: 1 }]]) {
      assert.equal((await call(pathname, 'priya', body ? { method: 'POST', body: JSON.stringify(body) } : {})).status, 404, `${pathname} with its flag off`)
    }
    const offTimeline = await api(`projects/${p.id}/timeline`)
    assert.equal(offTimeline.handoffs, undefined, 'no handoff rows with timeline_handoffs_chain off')
    assert.ok(offTimeline.tasks.every(t => !('affectedMilestoneIds' in t)), 'no chain ids with timeline_handoffs_chain off')
    const people = await api('users'), projectPeople = await api(`users?projectId=${p.id}`)
    assert.deepEqual(projectPeople.map(x => x.id), people.map(x => x.id), 'GET /users orders as today with a project while picker_project_rank is off')
    assert.ok(projectPeople.every(x => !('onProject' in x)), 'no onProject marker while picker_project_rank is off')
    report.flagPair.offApi = { dateContext: 404, rebaseline: 404, timelineHandoffs: 'absent', usersOrder: 'unchanged' }
    report.flagPair.off = await probe(pm, false)
    const offWrites = pm.writes.length

    await setFlags(true)
    report.flagPair.on = await probe(pm, true)
    assert.equal(pm.writes.length, offWrites, `probes are read-only: ${pm.writes.slice(offWrites).join(', ')}`)
    console.log(JSON.stringify({ flagPair: report.flagPair }))

    // ---- AC-INT-29: every new control reached with Tab and used with the keyboard; results read back from the API.
    const task = async t => (await api(`tasks/${t.id}`)).task
    await open(pm, `/projects/${n}/tasks?group=none`)
    const keyButton = t => pm.locator(`[data-task-focus="key"][data-task-id="${t.id}"]`)
    const actionCell = t => pm.getByRole('button', { name: `Open ${t.key}`, exact: true }).locator('..')
    await actionCell(s.visit).waitFor({ state: 'attached' })
    assert.equal(await actionCell(s.visit).evaluate(el => getComputedStyle(el).opacity), '0', 'row actions hidden until hover or focus')
    await keyButton(s.visit).focus()
    await tabTo(pm, pm.getByRole('button', { name: `Open ${s.visit.key}`, exact: true }), 'Open')
    await actionCell(s.visit).evaluate(el => new Promise(resolve => { const end = Date.now() + 2000; const poll = () => getComputedStyle(el).opacity === '1' || Date.now() > end ? resolve() : requestAnimationFrame(poll); poll() }))
    assert.equal(await actionCell(s.visit).evaluate(el => getComputedStyle(el).opacity), '1', 'row actions shown while focus is inside the row')
    await tabTo(pm, pm.getByRole('button', { name: `Start ${s.visit.key}`, exact: true }), 'Start')
    await pm.keyboard.press('Enter')
    // The status path answers with the existing readiness dialog (FR-RDY-02); it is completed with the keyboard too.
    const ready = pm.getByRole('dialog').filter({ hasText: 'before it is Ready?' })
    await ready.waitFor()
    await tabTo(pm, ready.getByRole('checkbox'), 'readiness acknowledgement')
    await pm.keyboard.press('Space')
    await tabTo(pm, ready.getByRole('textbox'), 'start reason')
    await pm.keyboard.type('Synthetic site access confirmed')
    await tabTo(pm, ready.getByRole('button', { name: 'Acknowledge and start', exact: true }), 'Acknowledge and start')
    await pm.keyboard.press('Enter')
    await ready.waitFor({ state: 'hidden' })
    report.keyboard.start = (await until(() => task(s.visit), t => t.status === 'In Progress', 'Start with Enter moves the task through the status path')).status

    await keyButton(s.first).focus()
    await tabTo(pm, pm.getByRole('button', { name: `Block ${s.first.key}`, exact: true }), 'Block')
    await pm.keyboard.press('Enter')
    const block = pm.getByRole('dialog')
    await tabTo(pm, block.getByLabel('Reason', { exact: true }), 'block reason')
    await pm.keyboard.type('Synthetic client input outstanding')
    await tabTo(pm, block.getByRole('button', { name: 'Set block', exact: true }), 'Set block')
    await pm.keyboard.press('Enter')
    await block.waitFor({ state: 'hidden' })
    report.keyboard.block = (await until(() => task(s.first), t => t.manualBlockType === 'Client', 'Block dialog completed with the keyboard')).manualBlockType

    await open(pm, `/projects/${n}/tasks?group=none`)
    await keyButton(s.second).focus()
    await tabTo(pm, pm.getByRole('button', { name: `Rename ${s.second.key}`, exact: true }), 'Rename')
    await pm.keyboard.press('Enter')
    const name = pm.getByRole('textbox', { name: 'Name', exact: true })
    await name.waitFor()
    assert.equal(await name.evaluate(el => el === document.activeElement), true, 'the name editor takes focus')
    await pm.keyboard.press('Escape')
    assert.equal((await task(s.second)).name, s.second.name, 'Escape writes nothing')
    await keyButton(s.second).focus()
    await tabTo(pm, pm.getByRole('button', { name: `Rename ${s.second.key}`, exact: true }), 'Rename')
    await pm.keyboard.press('Enter'); await name.waitFor()
    await pm.keyboard.press('ControlOrMeta+a'); await pm.keyboard.type('Synthetic design renamed'); await pm.keyboard.press('Enter')
    await pm.getByRole('button', { name: 'Synthetic design renamed', exact: true }).waitFor()
    report.keyboard.rename = (await until(() => task(s.second), t => t.name === 'Synthetic design renamed', 'Rename with Enter')).name
    s.second.name = report.keyboard.rename

    // Date hint (keyboard editing of the date shows it; Escape writes nothing), project-ranked picker, deliverable from a task.
    await openTask(pm, s.second)
    await panel(pm).getByRole('button', { name: 'Close', exact: true }).first().focus()
    await tabTo(pm, startDate(pm), 'Start date')
    await pm.keyboard.press('Enter')
    await pm.keyboard.press('ArrowDown')
    await panel(pm).getByText(/Earliest consistent start/).waitFor()
    report.keyboard.dateHint = await panel(pm).getByText(/Earliest consistent start/).innerText()
    const beforeEscape = (await task(s.second)).startDate
    await pm.keyboard.press('Escape')
    assert.equal((await task(s.second)).startDate, beforeEscape, 'Escape from the date editor writes nothing')
    // Escape also closes the side panel, as it does for every inline editor in a panel (existing behaviour), so reopen it.
    report.keyboard.escapeClosesPanel = await pm.getByRole('dialog').count() === 0
    await openTask(pm, s.second)
    await panel(pm).getByRole('button', { name: 'Close', exact: true }).first().focus()
    await tabTo(pm, assigneePicker(pm), 'Assignee picker')
    await pm.keyboard.press('Enter')
    const ranked = pm.getByRole('option', { name: /On this project/ })
    await ranked.first().waitFor()
    report.keyboard.picker = await ranked.first().innerText()
    await pm.keyboard.press('Escape')
    await panel(pm).getByRole('button', { name: 'Close', exact: true }).first().focus()
    await tabTo(pm, panel(pm).getByRole('button', { name: 'Create deliverable from this task', exact: true }), 'Create deliverable from this task')
    await pm.keyboard.press('Enter')
    const creating = pm.getByRole('dialog').filter({ has: pm.getByRole('heading', { name: 'New deliverable', exact: true }) })
    await creating.waitFor()
    report.keyboard.deliverableFromTask = await creating.locator('#d-name').inputValue()
    assert.equal(report.keyboard.deliverableFromTask, s.second.name, 'the new deliverable is prefilled from the task')
    await pm.keyboard.press('Escape'); await creating.waitFor({ state: 'hidden' })

    // Re-baseline: the milestone is moved 3 days, re-baselined by keyboard, then moved 2 more (slip 5, 2 since re-baseline).
    const milestone = () => api(`milestones/${s.milestone.id}`).then(x => x.milestone)
    const moveMilestone = async date => send('POST', `milestones/${s.milestone.id}/change-date`, { newDate: date, reason: 'Synthetic client request', cascadeDeliverables: false, includeTasks: false, rowVersion: (await milestone()).rowVersion })
    await moveMilestone(s.day(38))
    const rebaseline = async url => {
      await open(pm, url)
      const button = panel(pm).getByRole('button', { name: 'Re-baseline', exact: true })
      await button.waitFor()
      await panel(pm).getByRole('button', { name: 'Close', exact: true }).first().focus()
      await tabTo(pm, button, 'Re-baseline')
      await pm.keyboard.press('Enter')
      const confirm = pm.getByRole('alertdialog').or(pm.getByRole('dialog').filter({ has: pm.getByRole('button', { name: 'Confirm', exact: true }) })).last()
      await confirm.waitFor()
      await tabTo(pm, confirm.getByRole('textbox').first(), 're-baseline reason')
      await pm.keyboard.type('Synthetic client-agreed programme')
      await tabTo(pm, confirm.getByRole('button', { name: 'Confirm', exact: true }), 'Confirm')
      await pm.keyboard.press('Enter')
      await confirm.waitFor({ state: 'hidden' })
    }
    await rebaseline(`/projects/${n}/milestones?panel=Milestone:${s.milestone.id}`)
    await until(milestone, m => m.baselineDate === s.day(38), 'milestone baseline stored')
    await moveMilestone(s.day(40))
    const rebased = await milestone()
    report.keyboard.milestoneRebaseline = { baselineDate: rebased.baselineDate, slipDays: rebased.slipDays, sinceBaselineDays: rebased.sinceBaselineDays }
    assert.deepEqual(report.keyboard.milestoneRebaseline, { baselineDate: s.day(38), slipDays: 5, sinceBaselineDays: 2 })
    await rebaseline(`/projects/${n}/deliverables?panel=Deliverable:${s.deliverable.id}`)
    const deliverable = () => api(`deliverables/${s.deliverable.id}`).then(x => x.deliverable)
    const rebasedDeliverable = await until(deliverable, d => d.baselineDueDate === s.day(30), 'deliverable baseline stored')
    await send('PATCH', `deliverables/${s.deliverable.id}`, { dueDate: s.day(33), reason: 'Synthetic client request', rowVersion: rebasedDeliverable.rowVersion })
    report.keyboard.deliverableRebaseline = { baselineDueDate: rebasedDeliverable.baselineDueDate, sinceBaselineDays: (await deliverable()).sinceBaselineDays }
    assert.equal(report.keyboard.deliverableRebaseline.sinceBaselineDays, 3)

    // Timeline: chain filter, its chip and the handoff link, all with the keyboard; none of them writes.
    const timelineWrites = pm.writes.length
    await open(pm, `/projects/${n}/timeline`)
    const chainSelect = pm.locator('#tl-chain') // inside the Filters popover at desktop width
    await tabTo(pm, pm.getByRole('button', { name: /^Filters/ }), 'Filters')
    await pm.keyboard.press('Enter')
    await tabTo(pm, chainSelect, 'Chain to milestone')
    await pm.keyboard.type(`${n}-M01`) // type-ahead picks the option on a closed select, as on every platform
    await pm.waitForURL(u => u.searchParams.get('chainTo') === s.milestone.id)
    await pm.keyboard.press('Escape'); await chainSelect.waitFor({ state: 'hidden' })
    const count = pm.getByRole('status').filter({ hasText: /tasks lead to this milestone/ })
    report.keyboard.chain = await count.innerText()
    assert.match(report.keyboard.chain, /^2 of \d+ tasks lead to this milestone/)
    const expand = pm.getByRole('button', { name: /^Show the 2 tasks of / })
    await tabTo(pm, expand, 'show the deliverable tasks')
    await pm.keyboard.press('Enter')
    await until(() => pm.getByRole('button', { name: /in the chain to/ }).count(), c => c === 3, 'the deliverable and its two tasks are named "in the chain to", not marked by colour alone')
    await tabTo(pm, pm.getByRole('button', { name: 'Remove filter: Chain to milestone', exact: true }), 'chain chip')
    await pm.keyboard.press('Enter')
    await pm.waitForURL(u => !u.searchParams.has('chainTo'))
    const handoff = pm.getByRole('link', { name: new RegExp(`${n}-H001`) }).first()
    report.keyboard.handoffText = await pm.getByText(/Promised 2 d after needed/).first().innerText()
    await tabTo(pm, handoff, 'handoff link', 200)
    await pm.keyboard.press('Enter')
    await pm.waitForURL(u => u.pathname.endsWith('/handoffs') && (u.searchParams.get('panel') ?? '').startsWith('Handoff:'))
    assert.equal(pm.writes.length, timelineWrites, `timeline filters and links never write: ${pm.writes.slice(timelineWrites).join(', ')}`)

    // Grouped activity, Gantt collapse, Portfolio link, Staff and copied views.
    await open(pm, `/projects/${n}/activity`)
    const group = pm.getByRole('checkbox', { name: 'Group changes', exact: true })
    await tabTo(pm, group, 'Group changes')
    await pm.keyboard.press('Space')
    await pm.waitForURL(u => u.searchParams.get('groupChanges') === 'true')
    const summary = pm.locator('summary').filter({ hasText: /Priya Nair made [2-9] changes/ }).first()
    await tabTo(pm, summary, 'group summary')
    await pm.keyboard.press('Enter')
    assert.equal(await summary.evaluate(el => el.parentElement.open), true, 'Enter expands the group')
    report.keyboard.groupedActivity = await summary.innerText()

    await open(pm, `/timeline?projects=${p.id}`)
    await tabTo(pm, pm.getByRole('button', { name: `Collapse ${n}`, exact: true }), 'Gantt collapse')
    await pm.keyboard.press('Enter')
    await pm.waitForURL(u => u.searchParams.get('collapsed') === p.id)
    await pm.reload(); await pm.getByRole('button', { name: `Expand ${n}`, exact: true }).waitFor()
    report.keyboard.ganttCollapsed = new URL(pm.url()).searchParams.get('collapsed') === p.id

    await open(pm, '/portfolio')
    await tabTo(pm, pm.getByRole('link', { name: 'Capacity by discipline', exact: true }), 'Capacity by discipline')
    await pm.keyboard.press('Enter')
    await pm.waitForURL(u => u.pathname === '/planner' && u.searchParams.get('groupBy') === 'homeDiscipline')
    report.keyboard.plannerLink = new URL(pm.url()).search

    const saveView = async (page, url, trigger, item, name) => {
      await open(page, url)
      await tabTo(page, trigger, 'Views')
      await page.keyboard.press('Enter')
      await page.getByRole('menuitem').first().waitFor()
      await until(() => page.evaluate(() => document.activeElement?.getAttribute('role')), role => role === 'menuitem', 'the menu takes focus')
      for (let i = 0; i < 12 && (await focusedName(page)) !== item; i++) { await page.keyboard.press('ArrowDown'); await page.waitForTimeout(50) }
      assert.equal(await focusedName(page), item, `${item} reached with arrow keys`)
      await page.keyboard.press('Enter')
      const dialog = page.getByRole('dialog')
      const field = dialog.getByLabel('Name', { exact: true })
      await field.waitFor()
      if (name) { await tabTo(page, field, 'view name'); await page.keyboard.press('ControlOrMeta+a'); await page.keyboard.type(name) }
      await tabTo(page, dialog.getByRole('button', { name: 'Save', exact: true }), 'Save')
      await page.keyboard.press('Enter')
      await dialog.waitFor({ state: 'hidden' })
    }
    const sv = await supervisor()
    await saveView(sv, '/staff?inactive=1', sv.getByRole('button', { name: 'Views', exact: true }), 'Save current view…', 'Synthetic staff view')
    report.keyboard.staffView = (await until(async () => (await api('views?listType=staff', 'sam')).views.find(v => v.name === 'Synthetic staff view'), v => v?.scope === 'Personal', 'staff view saved')).scope
    const sharedBefore = (await api(`views?listType=tasks&projectId=${p.id}`)).views.find(v => v.id === s.shared.id)
    await saveView(pm, `/projects/${n}/tasks?view=${s.shared.id}&group=status`, pm.getByRole('button', { name: sharedBefore.name, exact: true }), 'Copy as personal view')
    const taskViews = (await api(`views?listType=tasks&projectId=${p.id}`)).views
    const copy = taskViews.find(v => v.name === `${sharedBefore.name} (copy)`)
    report.keyboard.copiedView = { scope: copy?.scope, params: copy?.params, sharedUnchanged: JSON.stringify(taskViews.find(v => v.id === s.shared.id)) === JSON.stringify(sharedBefore) }
    assert.deepEqual(report.keyboard.copiedView, { scope: 'Personal', params: sharedBefore.params, sharedUnchanged: true })

    // Status pill (pending until the server answers, FR-INT-21): Complete is chosen with the keyboard and the answer is shown.
    await open(pm, `/projects/${n}/tasks?group=none`)
    const status = pm.getByRole('button', { name: `Change status of ${s.visit.key}, now In Progress`, exact: true })
    await keyButton(s.visit).focus()
    await tabTo(pm, status, 'status menu')
    await pm.keyboard.press('Enter')
    await until(() => pm.evaluate(() => document.activeElement?.getAttribute('role')), role => role?.startsWith('menu'), 'the status menu takes focus')
    for (let i = 0; i < 8 && (await focusedName(pm)) !== 'Complete'; i++) { await pm.keyboard.press('ArrowDown'); await pm.waitForTimeout(50) }
    assert.equal(await focusedName(pm), 'Complete', 'Complete reached with arrow keys')
    await pm.keyboard.press('Enter')
    report.keyboard.status = (await until(() => task(s.visit), t => t.status === 'Complete', 'Complete chosen with the keyboard')).status
    await pm.getByRole('button', { name: `Change status of ${s.visit.key}, now Complete`, exact: true }).waitFor()

    // Allocation capacity review (the supervisor's), reached by keyboard.
    await open(sv, `/projects/${n}/allocations`)
    await tabTo(sv, sv.getByRole('button', { name: /Alex Chen/ }).first(), 'allocation row')
    await sv.keyboard.press('Enter')
    const review = sv.getByRole('button', { name: 'Review capacity', exact: true })
    await review.waitFor()
    await tabTo(sv, review, 'Review capacity')
    await sv.keyboard.press('Enter')
    report.keyboard.capacityWording = await sv.getByText(/dates exceed available capacity/).first().innerText()
    assert.match(report.keyboard.capacityWording, /^2 of 5 dates exceed available capacity/)
    await sv.keyboard.press('Escape')
    assert.deepEqual(pm.errors, [], 'page errors during keyboard checks')
    console.log(JSON.stringify({ keyboard: report.keyboard }))

    // ---- AC-INT-29: axe on every changed screen and its new dialogs, at desktop and phone widths.
    const states = [
      ['Task List', async page => { await open(page, `/projects/${n}/tasks?group=none`); await page.getByText(s.first.name, { exact: true }).first().waitFor() }],
      ['Task panel with date hint', async page => { await openTask(page, s.second); await startDate(page).click(); await page.keyboard.press('ArrowDown'); await panel(page).getByText(/Earliest consistent start/).waitFor() }],
      ['Assignee picker ranked by project', async page => { await openTask(page, s.second); await assigneePicker(page).click(); await page.getByRole('option', { name: /On this project/ }).first().waitFor() }],
      ['New deliverable from a task', async page => { await openTask(page, s.second); await panel(page).getByRole('button', { name: 'Create deliverable from this task', exact: true }).click(); await page.getByRole('heading', { name: 'New deliverable', exact: true }).waitFor() }],
      ['Block dialog', async page => { await openTask(page, s.second); await panel(page).getByRole('button', { name: 'Set block', exact: true }).click(); await page.getByRole('alertdialog').or(page.getByRole('dialog')).filter({ hasText: `Block ${s.second.key}` }).getByLabel('Reason', { exact: true }).waitFor() }],
      ['Deliverable panel with baseline', async page => { await open(page, `/projects/${n}/deliverables?panel=Deliverable:${s.deliverable.id}`); await panel(page).getByText(/since re-baseline/).waitFor() }],
      ['Milestone panel with baseline', async page => { await open(page, `/projects/${n}/milestones?panel=Milestone:${s.milestone.id}`); await panel(page).getByText(/since re-baseline/).waitFor() }],
      ['Re-baseline dialog', async page => { await open(page, `/projects/${n}/milestones?panel=Milestone:${s.milestone.id}`); await panel(page).getByRole('button', { name: 'Re-baseline', exact: true }).click(); await page.getByRole('button', { name: 'Confirm', exact: true }).waitFor() }],
      ['Milestones list', async page => { await open(page, `/projects/${n}/milestones`); await page.getByText('Synthetic design review').first().waitFor() }],
      ['Timeline with handoffs and chain', async page => { await open(page, `/projects/${n}/timeline?chainTo=${s.milestone.id}`); await page.getByText(/tasks lead to this milestone/).waitFor() }],
      ['Project dashboard note', async page => { await open(page, `/projects/${n}`); await page.getByText('Planned completion after target', { exact: true }).first().waitFor() }],
      ['Grouped activity', async page => { await open(page, `/projects/${n}/activity?groupChanges=true`); await page.locator('summary').filter({ hasText: /Priya Nair made [2-9] changes/ }).first().click() }],
      ['Gantt', async page => { await open(page, `/timeline?projects=${p.id}`); await page.getByRole('button', { name: `Collapse ${n}`, exact: true }).waitFor() }],
      ['Staff with views', async (page, viewport) => { page = await reviewer(viewport); await open(page, '/staff'); await page.getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem').first().waitFor(); return page }],
      ['Copy as personal view dialog', async page => { await open(page, `/projects/${n}/tasks?view=${s.shared.id}&group=status`); await page.getByRole('button', { name: s.shared.name, exact: true }).click(); await page.getByRole('menuitem', { name: 'Copy as personal view', exact: true }).click(); await page.getByRole('dialog').getByLabel('Name', { exact: true }).waitFor() }],
      // Below tablet width the Portfolio and Allocations show their "available on tablet and desktop" notice (existing rule).
      ['Portfolio', async (page, viewport) => { await open(page, '/portfolio'); await page.getByRole('heading', { name: 'Portfolio', exact: true }).waitFor(); if (viewport === DESKTOP) await page.getByRole('link', { name: 'Capacity by discipline', exact: true }).waitFor() }],
      ['Allocation capacity review', async (page, viewport) => { page = await reviewer(viewport); await open(page, `/projects/${n}/allocations`); if (viewport !== DESKTOP) { await page.getByText('This screen is available on tablet and desktop.').waitFor(); return page } await page.getByRole('button', { name: /Alex Chen/ }).first().click(); await page.getByRole('button', { name: 'Review capacity', exact: true }).click(); await page.getByText(/2 of 5 dates exceed available capacity/).first().waitFor(); return page }],
    ]
    const reviewers = new Map([[DESKTOP, await supervisor()]])
    const reviewer = async viewport => { if (!reviewers.has(viewport)) reviewers.set(viewport, await session(viewport, 'Sam Patel')); return reviewers.get(viewport) }
    for (const viewport of [DESKTOP, PHONE]) {
      const page = viewport === DESKTOP ? pm : await session(PHONE)
      for (const [label, reach] of states) { const shown = await reach(page, viewport) ?? page; await axe(shown, label, report.axe) }
      assert.deepEqual(page.errors, [], `page errors at ${viewport.width}px`)
    }
    // The drag confirmation lists the successor a move would break (desktop: dragging is a pointer action).
    await open(pm, `/projects/${n}/timeline`)
    await pm.getByRole('button', { name: /^Show the 2 tasks of / }).click()
    const bar = pm.locator(`button[style*="cursor: grab"][aria-label*="${s.first.key}"]`).first()
    const box = await bar.boundingBox()
    const dragWrites = pm.writes.length
    await pm.mouse.move(box.x + box.width / 2, box.y + box.height / 2); await pm.mouse.down(); await pm.mouse.move(box.x + box.width / 2 + 120, box.y + box.height / 2, { steps: 8 }); await pm.mouse.up()
    const moveDialog = pm.getByRole('dialog')
    await moveDialog.getByText(new RegExp(s.second.key)).first().waitFor()
    await axe(pm, 'Drag confirmation with successor warning', report.axe)
    await pm.keyboard.press('Escape'); await moveDialog.waitFor({ state: 'hidden' })
    assert.equal(pm.writes.length, dragWrites, 'cancelling the move writes nothing')
    console.log(JSON.stringify({ axe: report.axe }))

    // ---- AC-INT-01 schedule invariants: threshold and stored baselines only act while schedule_baseline is on.
    const ghosts = async () => { await open(pm, `/projects/${n}/timeline`); await pm.getByRole('heading', { name: 'Timeline', exact: true }).waitFor(); return { original: await pm.locator('[title^="Original dates "]').count() > 0, baseline: await pm.locator('[title^="PM baseline dates "]').count() > 0 } }
    report.schedule.onThreshold0 = await ghosts()
    assert.deepEqual(report.schedule.onThreshold0, { original: true, baseline: true })
    await setting('slip_display_days', 5)
    report.schedule.onThreshold5 = await ghosts()
    assert.deepEqual(report.schedule.onThreshold5, { original: false, baseline: false }, 'a 3-day slip is not drawn under a 5-day threshold')
    await setFlags(false)
    report.schedule.offThreshold5 = await ghosts()
    assert.deepEqual(report.schedule.offThreshold5, { original: true, baseline: false }, 'with schedule_baseline off the threshold has no effect and no baseline is drawn')
    const off = await milestone()
    report.schedule.offMilestone = { slipDays: off.slipDays, baselineDate: off.baselineDate ?? null, sinceBaselineDays: off.sinceBaselineDays ?? null }
    assert.deepEqual(report.schedule.offMilestone, { slipDays: 5, baselineDate: null, sinceBaselineDays: null }, 'a stored baseline does not change slip')
    await open(pm, `/projects/${n}/milestones`)
    const row = pm.getByRole('row').filter({ hasText: 'Synthetic design review' }).first()
    assert.match(await row.innerText(), /\b5 d\b/, 'the Slip column still measures from the original date')
    assert.equal(await pm.getByText(/since re-baseline/).count(), 0)
    assert.deepEqual(pm.errors, [], 'page errors')
    console.log(JSON.stringify({ result: 'PASS', project: n, schedule: report.schedule, axeScreens: report.axe.length }))
  } finally {
    for (const x of original) await setting(x.key, x.value).catch(e => console.error(`could not restore ${x.key}: ${e.message}`))
    await browser.close()
    const out = path.resolve(__dirname, '../test-results/interaction-real'); fs.mkdirSync(out, { recursive: true }); fs.writeFileSync(path.join(out, 'report.json'), JSON.stringify(report, null, 2))
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
