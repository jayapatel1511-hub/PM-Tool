// Packet 047 real-API checks against the local API and PostgreSQL: AC-PRO-01 (the card's counts, and AC-CALM-01/03 with
// the card present), AC-PRO-11 (wizard skip reasons before creation) and AC-PRO-12 (Planner Unfilled roles and Fill).
// Turns on the flags it needs and restores them; synthetic records only, on an explicitly disposable database.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const { census, LIMIT } = require('./clutter.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'this check creates records and sets flags, so it requires an explicitly disposable database')

async function api(pathname, who, options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json' } })
  if (!response.ok) throw new Error(`${options.method ?? 'GET'} ${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const send = (pathname, who, body, method = 'POST') => api(pathname, who, { method, body: JSON.stringify(body) })
const setting = (key, value) => send(`admin/settings/${key}`, 'jordan', { value }, 'PUT')
const addDays = (iso, n) => new Date(Date.parse(`${iso}T00:00:00Z`) + n * 86400000).toISOString().slice(0, 10)
const suffix = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`.toUpperCase().slice(-7)

async function project(who, name, reference, civil) {
  const marc = await api('me', 'marc')
  const created = await send('projects', who, { projectNumber: `R${suffix()}`, name, clientId: reference.clients[0].id, officeId: reference.offices[0].id,
    disciplines: [{ disciplineId: civil.id, leadUserId: marc.id }], members: [] })
  const draft = await api(`projects/${created.id}`, who)
  await send(`projects/${created.id}/transition`, who, { toStatus: 'Active', rowVersion: draft.rowVersion })
  const detail = await api(`projects/${created.id}`, who)
  return { id: created.id, number: created.projectNumber, discipline: (detail.disciplines ?? detail.projectDisciplines)[0].id, rowVersion: detail.rowVersion }
}

;(async () => {
  const jordan = await api('me', 'jordan')
  const flags = ['onboarding_card', 'template_roles', 'unfilled_beside_capacity']
  const before = { flags: Object.fromEntries(flags.map(f => [f, !!jordan.flags?.[f]])), restricted: !!jordan.settings.restrictedProjectsEnabled }
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil'), geo = reference.disciplines.find(x => x.name === 'Geotechnical')
  assert.ok(civil && geo, 'Civil and Geotechnical discipline fixtures missing')
  const today = jordan.settings.today
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const results = {}
  try {
    for (const f of flags) await setting(`flag.${f}`, true)
    await setting('restricted_projects_enabled', true)
    const session = async (who, viewport = { width: 1440, height: 1000 }) => {
      const context = await browser.newContext({ viewport, reducedMotion: 'reduce' })
      await context.addInitScript(email => { sessionStorage.setItem('hub.devUser', email); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
      const page = await context.newPage()
      page.errors = []
      page.on('pageerror', e => page.errors.push(e.message))
      return page
    }
    const open = async (page, url) => { await page.goto(`${base}${url}`, { waitUntil: 'networkidle', timeout: 60000 }); await page.getByRole('main').first().waitFor() }

    // AC-PRO-01: a Project Manager with no projects, at the census size (1600×900).
    const pmWho = `pm047${suffix().toLowerCase()}`
    const pm = await api('me', pmWho) // Development sign-in provisions the person on first request
    await send(`admin/users/${pm.id}/roles`, 'jordan', { role: 'ProjectManager' })
    const zero = await api('me/onboarding', pmWho)
    assert.deepEqual([zero.completed, zero.total, zero.groups.map(g => g.role)], [0, 7, ['Everyone', 'ProjectManager']])
    const pmPage = await session(pmWho, { width: 1600, height: 900 })
    const card = pmPage.getByRole('region', { name: 'Getting started' })
    const readCard = async (expected) => {
      await open(pmPage, '/my-work')
      await card.getByText(expected, { exact: true }).waitFor({ timeout: 15000 })
      const toggle = card.getByRole('button', { name: /^Getting started:/ })
      assert.equal(await toggle.getAttribute('aria-expanded'), 'false', 'the card opens collapsed')
      assert.equal(await card.getByRole('checkbox').count(), 0, 'collapsed, the card is one line')
      const text = await card.innerText()
      assert.doesNotMatch(text, /%/, 'no percentage'); assert.equal(await card.locator('[role=progressbar], progress, [role=meter]').count(), 0, 'no progress bar')
      await pmPage.waitForTimeout(1000)
      return pmPage.evaluate(census, 'section li')
    }
    const empty = await readCard('Getting started: 0 of 7 done')
    assert.ok(empty.chromeControls <= LIMIT.chromeControls, `AC-CALM-03 with the card: ${empty.chromeControls} chrome controls`)
    const marc = await api('me', 'marc')
    const own = await send('projects', pmWho, { projectNumber: `O${suffix()}`, name: 'Synthetic 047 onboarding project', clientId: reference.clients[0].id,
      officeId: reference.offices[0].id, disciplines: [{ disciplineId: civil.id, leadUserId: marc.id }], members: [] })
    await readCard('Getting started: 1 of 7 done')
    await card.getByRole('button', { name: /^Getting started:/ }).click()
    const ticked = await card.getByRole('checkbox').evaluateAll(boxes => boxes.map(b => [b.getAttribute('aria-label'), b.getAttribute('aria-checked') ?? String(b.checked)]))
    assert.deepEqual(ticked.filter(([, on]) => on === 'true').map(([name]) => name), ['Create a project'], `only Create a project is ticked: ${JSON.stringify(ticked)}`)
    assert.equal(ticked.length, 7)
    // A working Today, as the census measures it: an overdue task on the now Active project raises an attention item, the
    // first data row. Activating and assigning tick two more steps.
    const detail = await api(`projects/${own.id}`, pmWho)
    await send(`projects/${own.id}/transition`, pmWho, { toStatus: 'Active', rowVersion: detail.rowVersion })
    await send(`projects/${own.id}/tasks`, pmWho, { name: 'Synthetic 047 overdue task', projectDisciplineId: (detail.disciplines ?? detail.projectDisciplines)[0].id,
      assigneeId: pm.id, startDate: addDays(today, -20), dueDate: addDays(today, -6) })
    for (let i = 0; (await api('me/work', pmWho)).attention.total === 0; i++) { assert.ok(i < 75, 'no attention item for the overdue task'); await new Promise(r => setTimeout(r, 200)) }
    const withRow = await readCard('Getting started: 3 of 7 done')
    assert.ok(withRow.firstDataY != null && withRow.firstDataY <= LIMIT.firstDataY, `AC-CALM-01 with the card: first row at ${withRow.firstDataY}`)
    assert.ok(withRow.chromeControls <= LIMIT.chromeControls, `AC-CALM-03 with the card: ${withRow.chromeControls} chrome controls`)
    assert.deepEqual(pmPage.errors, [])
    results['AC-PRO-01'] = { empty: { chromeControls: empty.chromeControls }, withRow: { firstDataY: withRow.firstDataY, chromeControls: withRow.chromeControls } }

    // AC-PRO-11: the wizard preview names each skipped role before creation, and creation still succeeds.
    const templateName = `Synthetic 047 role template ${suffix()}`
    const template = await send('templates', 'jordan', { name: templateName })
    const structure = await send(`templates/${template.id}/structure`, 'jordan', {
      rowVersion: template.rowVersion,
      disciplines: [{ disciplineId: civil.id, isDefaultIncluded: true }, { disciplineId: geo.id, isDefaultIncluded: false }],
      milestones: [
        { ref: 'start', name: 'Start', milestoneType: 'Kickoff', anchor: 'ProjectStart', offset: 0, isClientFacing: false },
        { ref: 'end', name: 'End', milestoneType: 'Closeout', anchor: 'ProjectStart', offset: 10, isClientFacing: false },
        { ref: 'undated', name: 'Not dated', milestoneType: 'Other', anchor: 'None', offset: null, isClientFacing: false }],
      deliverables: [], tasks: [], dependencies: [],
      roles: [
        { ref: 'r1', disciplineId: civil.id, label: 'Civil reviewer', startMilestoneRef: 'start', startOffsetDays: 0, endMilestoneRef: 'end', endOffsetDays: 0, hours: 12, note: null },
        { ref: 'r2', disciplineId: geo.id, label: 'Geotechnical advisor', startMilestoneRef: 'start', startOffsetDays: 0, endMilestoneRef: 'end', endOffsetDays: 0, hours: 6, note: null },
        { ref: 'r3', disciplineId: null, label: 'Undated demand', startMilestoneRef: 'undated', startOffsetDays: 0, endMilestoneRef: 'end', endOffsetDays: 0, hours: 4, note: null },
        { ref: 'r4', disciplineId: null, label: 'Long demand', startMilestoneRef: 'start', startOffsetDays: 0, endMilestoneRef: 'start', endOffsetDays: 400, hours: 8, note: null }],
    }, 'PUT')
    await send(`templates/${template.id}/publish`, 'jordan', { rowVersion: structure.rowVersion })
    const priya = await session('priya')
    await open(priya, '/projects')
    await priya.getByRole('main').getByRole('button', { name: 'Create project' }).first().click()
    const wizard = priya.getByRole('dialog')
    const number = `W${suffix()}`
    await wizard.locator('#p-num').fill(number)
    await wizard.locator('#p-name').fill('Synthetic 047 wizard project')
    await wizard.locator('#p-client').selectOption({ label: reference.clients[0].name })
    await wizard.locator('#p-office').selectOption({ label: reference.offices[0].name })
    await wizard.locator('#p-start').fill(today)
    await wizard.locator('#p-template').selectOption({ label: `${templateName} (v1)` })
    await wizard.getByRole('button', { name: 'Next' }).click()
    const summary = wizard.getByRole('region', { name: 'What will be created' })
    const roleLines = summary.getByRole('listitem')
    await roleLines.first().waitFor({ timeout: 15000 })
    const lines = await roleLines.allInnerTexts()
    const line = label => lines.find(x => x.startsWith(`${label} ·`)) ?? assert.fail(`no preview line for ${label}: ${JSON.stringify(lines)}`)
    assert.match(line('Civil reviewer'), /Will be created/)
    assert.match(line('Geotechnical advisor'), /Skipped: discipline is not selected$/)
    assert.match(line('Undated demand'), /Skipped: start milestone has no date$/)
    assert.match(line('Long demand'), /Skipped: range exceeds 366 days$/)
    await wizard.getByRole('button', { name: 'Create project' }).click()
    await priya.waitForURL(u => u.pathname === `/projects/${number}` || u.pathname.startsWith(`/projects/${number}/`), { timeout: 30000 })
    const made = await api(`projects/${number}`, 'priya')
    const madeRoles = await api(`projects/${made.id}/open-roles`, 'priya')
    assert.deepEqual(madeRoles.map(r => [r.roleLabel, r.status]), [['Civil reviewer', 'Open']], 'only the dated role is created')
    results['AC-PRO-11'] = { preview: lines, created: madeRoles.map(r => r.roleLabel) }

    // AC-PRO-12: the Planner's Projects view, for a viewer allowed to fill, one who can only view, and one who cannot view.
    const visible = await project('priya', 'Synthetic 047 unfilled project', reference, civil)
    const hidden = await project('priya', 'Synthetic 047 restricted project', reference, civil)
    await send(`projects/${hidden.id}`, 'priya', { visibility: 'Restricted', rowVersion: hidden.rowVersion }, 'PATCH')
    const dow = new Date(`${today}T00:00:00Z`).getUTCDay(), monday = addDays(today, ((8 - dow) % 7) || 7)
    const role = await send(`projects/${visible.id}/open-roles`, 'priya', { roleLabel: 'Synthetic unfilled reviewer', projectDisciplineId: visible.discipline, fromDate: monday, throughDate: addDays(monday, 11), plannedHours: 40 })
    await send(`projects/${hidden.id}/open-roles`, 'priya', { roleLabel: 'Synthetic restricted role', projectDisciplineId: hidden.discipline, fromDate: monday, throughDate: addDays(monday, 4), plannedHours: 10 })
    const grid = await api('planning/grid?includeOpenRoles=true', 'priya')
    const served = grid.unfilledRoles.find(r => r.id === role.id)
    assert.ok(served, 'the grid read lists the open role')
    const roleRow = page => page.getByRole('row').filter({ hasText: 'Synthetic unfilled reviewer' })
    await open(priya, '/planner?layout=projects')
    await roleRow(priya).waitFor({ timeout: 15000 })
    const cells = await roleRow(priya).getByRole('cell').allInnerTexts()
    assert.equal(cells[1], 'Unfilled roles', 'listed under Unfilled roles')
    const weekCells = cells.slice(5)
    assert.deepEqual(weekCells, served.cells.map(c => c.hours > 0 ? `${c.hours.toFixed(1).replace(/\.0$/, '')} h` : '—'), 'hours per week match the grid read')
    assert.ok(weekCells.filter(x => x === '20 h').length === 2, `40 h over two working weeks: ${weekCells}`)
    assert.equal(await priya.getByRole('row').filter({ hasText: 'Synthetic restricted role' }).count(), 1, 'the PM sees the restricted project role')
    await roleRow(priya).getByRole('link', { name: 'Synthetic unfilled reviewer' }).click()
    await priya.getByRole('dialog').getByText('Fill Synthetic unfilled reviewer').first().waitFor({ timeout: 15000 })
    assert.equal(new URL(priya.url()).searchParams.get('fill'), role.id)

    const sam = await session('sam')
    await open(sam, '/planner?layout=projects')
    await roleRow(sam).waitFor({ timeout: 15000 })
    assert.equal(await sam.getByRole('row').filter({ hasText: 'Synthetic restricted role' }).count(), 0, 'a viewer who cannot view the project sees no row')
    const samLink = roleRow(sam).getByRole('link', { name: 'Synthetic unfilled reviewer' })
    assert.match(await samLink.getAttribute('class'), /text-muted-foreground/, 'a plain link for a viewer who cannot fill')
    await samLink.click()
    await sam.waitForURL(u => u.pathname.endsWith('/allocations'), { timeout: 15000 })
    await sam.waitForLoadState('networkidle')
    assert.equal(await sam.getByRole('dialog').count(), 0, 'no fill form for a viewer who cannot fill')
    assert.deepEqual([...priya.errors, ...sam.errors], [])
    results['AC-PRO-12'] = { weekCells, fill: 'opens for the PM; plain link for a Supervisor; no restricted row for the Supervisor' }
  } finally {
    for (const f of flags) await setting(`flag.${f}`, before.flags[f]).catch(e => console.error(e.message))
    await setting('restricted_projects_enabled', before.restricted).catch(e => console.error(e.message))
    await browser.close()
  }
  console.log(JSON.stringify({ scope: 'Chromium against the local API and PostgreSQL', results }))
})().catch(e => { console.error(e); process.exitCode = 1 })
