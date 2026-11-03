// FR/AC-SCR-28..31: staffing links, filter/group URL state, fresh peek and date span.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { projectRow, projectList } = require('./projects.fixture.cjs')
const { referenceData, referenceClient, referenceOffice } = require('./me.fixture.cjs')
const { milestoneRow, milestoneDetail } = require('./milestones.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const secondId = '00000000-0000-0000-0000-000000000411', thirdId = '00000000-0000-0000-0000-000000000412'
const sameNameClientProjectId = '00000000-0000-0000-0000-000000000426'
const clientId = '00000000-0000-0000-0000-000000000421', phaseId = '00000000-0000-0000-0000-000000000422', typeId = '00000000-0000-0000-0000-000000000423'
const staffing = { openRoles: 2, overCapacityPeople: 1, limitedScope: false, partialView: false, ownPlannerOnly: false, lookaheadWeeks: 4, from: '2026-10-26', through: '2026-11-22' }
const rows = [
  projectRow({ id: project.id, projectNumber: project.projectNumber, name: 'Crossing renewal', priority: 'High', client: { id: clientId, name: 'Harbour municipality' }, pm: { id: me.id, displayName: 'Alex Chen' },
    phaseId, phase: 'Design', projectTypeId: typeId, startDate: '2026-01-05', targetCompletionDate: '2027-02-28', staffing, span: { today: '2026-10-31', openMilestones: 4 } }),
  projectRow({ id: secondId, projectNumber: 'P-SECOND', name: 'Pump station renewal', priority: 'High', client: { id: clientId, name: 'Harbour municipality' }, pm: { id: me.id, displayName: 'Alex Chen' },
    phaseId, phase: 'Design', projectTypeId: typeId, staffing: { ...staffing, openRoles: 0, overCapacityPeople: 0, limitedScope: true, partialView: true }, span: { today: '2026-10-31', openMilestones: 0 } }),
  projectRow({ id: thirdId, projectNumber: 'P-THIRD', name: 'Low priority study', priority: 'Low', client: { id: '00000000-0000-0000-0000-000000000424', name: 'Other client' }, staffing: { ...staffing, openRoles: 0, overCapacityPeople: null }, span: { today: '2026-10-31', openMilestones: 0 } }),
  projectRow({ id: sameNameClientProjectId, projectNumber: 'P-FOURTH', name: 'Separate client with the same name', priority: 'High', client: { id: '00000000-0000-0000-0000-000000000427', name: 'Harbour municipality' },
    pm: { id: me.id, displayName: 'Alex Chen' }, staffing: { ...staffing, openRoles: 0, overCapacityPeople: null }, span: { today: '2026-10-31', openMilestones: 0 } }),
]
let note = 'Awaiting utility confirmation', peekReads = 0, scrollRows = 0
const requests = []
;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui, errors = []
  page.setDefaultTimeout(12000)
  page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (m.type() === 'error') errors.push(m.text()) })
  try {
    await page.route('**/api/v1/projects?**', route => {
      const url = new URL(route.request().url()), query = Object.fromEntries([...url.searchParams].map(([k, v]) => [k.toLowerCase(), v]))
      requests.push(query)
      const candidates = [...rows, ...Array.from({ length: scrollRows }, (_, i) => projectRow({ ...rows[0], id: `00000000-0000-0000-0007-${String(i + 1).padStart(12, '0')}`, projectNumber: `P-SCROLL-${i + 1}`, name: `Scrollable project ${i + 1}` }))]
      const filtered = candidates.filter(r => (!query.priority || r.priority === query.priority) && (!query.clientid || r.client.id === query.clientid)
        && (!query.pmid || r.pm.id === query.pmid) && (!query.phaseid || r.phaseId === query.phaseid) && (!query.projecttypeid || r.projectTypeId === query.projecttypeid))
      return route.fulfill({ json: projectList(filtered) })
    })
    await page.route('**/api/v1/reference', r => r.fulfill({ json: referenceData({ clients: [referenceClient(clientId, 'Harbour municipality')], offices: [referenceOffice(rows[0].officeId, rows[0].office)],
      phases: [{ id: phaseId, name: 'Design', isActive: true, sortOrder: 1 }], projectTypes: [{ id: typeId, name: 'Municipal', isActive: true }] }) }))
    await page.route('**/api/v1/users?**', r => r.fulfill({ json: [{ id: me.id, displayName: 'Alex Chen', isActive: true }] }))
    const linkedMilestone = milestoneRow({ id: '00000000-0000-0000-0006-000000000003', projectId: project.id, key: 'M-003', name: 'Open milestone 3', date: '2026-11-03', status: 'On Track' })
    await page.route(`**/api/v1/milestones/${linkedMilestone.id}`, r => r.fulfill({ json: milestoneDetail({ milestone: linkedMilestone, project: { id: project.id, projectNumber: project.projectNumber, name: 'Crossing renewal', status: 'Active' } }) }))
    await page.route(`**/api/v1/projects/${project.id}/peek`, route => {
      peekReads++
      return route.fulfill({ json: { id: project.id, projectNumber: project.projectNumber, name: 'Crossing renewal',
        milestones: [1, 2, 3].map(i => ({ id: `00000000-0000-0000-0006-${String(i).padStart(12, '0')}`, key: `M-00${i}`, name: `Open milestone ${i}`, date: `2026-11-0${i}`, status: 'On Track' })),
        disciplines: [{ id: project.disciplines[0].id, name: 'Civil', lead: { id: me.id, name: 'Alex Chen', state: 'Active' }, overdueTasks: 3 },
          { id: '00000000-0000-0000-0000-000000000425', name: 'Electrical', lead: { id: null, name: null, state: 'Unassigned' }, overdueTasks: 0 }],
        statusNoteEnabled: true, statusNote: { text: note, authorId: me.id, author: 'Alex Chen', at: '2026-10-31T13:00:00Z', header: { computedHealth: 'Green', reportedHealth: null, nextMilestone: null, openAttention: 0 } }, asOf: '2026-10-31T14:00:00Z' } })
    })
    me.flags = { ...me.flags, project_screens: false, screen_patterns: false }
    await page.goto(`${base}/projects?mine=false`)
    await page.getByRole('link', { name: 'Crossing renewal', exact: true }).first().waitFor()
    assert.equal(await page.getByRole('columnheader', { name: 'Staffing', exact: true }).count(), 0)
    assert.equal(await page.getByRole('button', { name: `Peek at ${project.projectNumber}`, exact: true }).count(), 0)
    assert.equal(peekReads, 0)

    me.flags.project_screens = true
    await page.goto(`${base}/projects?mine=false&priority=High&group=client`)
    const firstStaff = page.locator(`[data-project-staffing="${project.id}"]:visible`).first()
    await firstStaff.getByRole('link', { name: '2 open roles', exact: true }).waitFor()
    assert.equal(await firstStaff.getByRole('link', { name: '2 open roles', exact: true }).getAttribute('href'), `/projects/${project.projectNumber}/allocations?tab=roles&roleStatus=Open`)
    const plannerHref = await firstStaff.getByRole('link', { name: '1 over capacity', exact: true }).getAttribute('href')
    const drill = new URL(plannerHref, base)
    assert.equal(drill.pathname, '/planner'); assert.equal(drill.searchParams.get('teamProjectId'), project.id); assert.equal(drill.searchParams.get('indicator'), 'OverPlanned'); assert.equal(drill.searchParams.get('weeks'), '4'); assert.equal(drill.searchParams.get('from'), '2026-10-26')
    assert.equal(drill.searchParams.has('projectId'), false, 'a people filter must keep all-project planning inputs')
    const partial = page.locator(`[data-project-staffing="${secondId}"]:visible`).first()
    await partial.getByText('0 shown over capacity', { exact: true }).waitFor(); await partial.getByText('Limited to your planner scope', { exact: true }).waitFor()
    await partial.getByText(/Partial or provisional planning totals/).waitFor()
    await page.locator(`[data-project-span="${project.id}"]:visible`).first().getByText('4 milestones left', { exact: true }).waitFor()
    assert.equal(await page.locator(`[data-project-span="${project.id}"]:visible`).first().getByRole('img').count(), 1)
    await page.locator(`[data-project-span="${secondId}"]:visible`).first().getByText('No target', { exact: true }).waitFor()
    const group = page.locator('table th[scope="rowgroup"]').filter({ hasText: 'Harbour municipality' })
    assert.equal(await group.count(), 2, 'group identity uses Client ID, so equal names do not merge clients')
    assert.deepEqual((await group.allInnerTexts()).map(text => Number(text.trim().match(/(\d+)$/)[1])).sort(), [1, 2])
    await page.locator(`[data-project-staffing="${sameNameClientProjectId}"]:visible`).first().getByText('Capacity unavailable', { exact: true }).waitFor()
    assert.equal(await page.getByRole('link', { name: 'Low priority study', exact: true }).count(), 0)
    assert.equal(peekReads, 0, 'peek is read only when opened')

    await page.getByRole('button', { name: /^Filters/ }).click()
    assert.equal(await page.getByLabel('Priority', { exact: true }).inputValue(), 'High')
    await page.getByLabel('Client', { exact: true }).selectOption(clientId)
    await page.getByLabel('Phase', { exact: true }).selectOption(phaseId)
    await page.getByLabel('Project type', { exact: true }).selectOption(typeId)
    await page.getByRole('button', { name: 'Project Manager', exact: true }).click()
    await page.getByRole('option', { name: /Alex Chen/ }).click()
    await page.keyboard.press('Escape')
    await page.waitForFunction(expected => { const p = new URL(location.href).searchParams; return Object.entries(expected).every(([k, v]) => p.get(k) === v) }, { clientId, phaseId, projectTypeId: typeId, pmId: me.id, priority: 'High', group: 'client' })
    await page.reload(); await firstStaff.waitFor()
    assert.equal(new URL(page.url()).searchParams.get('group'), 'client')
    assert.ok(requests.some(q => q.clientid === clientId && q.phaseid === phaseId && q.projecttypeid === typeId && q.pmid === me.id && q.priority === 'High'))
    await page.getByRole('button', { name: /^Group by:/ }).click(); await page.getByRole('menuitemradio', { name: 'Office', exact: true }).click()
    assert.equal(new URL(page.url()).searchParams.get('group'), 'office')
    await page.getByRole('button', { name: /^Group by:/ }).click(); await page.getByRole('menuitemradio', { name: 'Project Manager', exact: true }).click()
    assert.equal(new URL(page.url()).searchParams.get('group'), 'pm')

    // More list rows and a smaller viewport create actual main/peek overflow for the return regression.
    scrollRows = 20
    await page.setViewportSize({ width: 1440, height: 480 })
    await page.reload(); await page.getByRole('button', { name: `Peek at ${project.projectNumber}`, exact: true }).waitFor()
    const origin = page.url()
    await page.getByRole('button', { name: `Peek at ${project.projectNumber}`, exact: true }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByText(note, { exact: true }).waitFor(); assert.equal(peekReads, 1)
    assert.equal(await dialog.getByRole('link', { name: /M-00/ }).count(), 3)
    await dialog.getByText('3 overdue tasks', { exact: true }).waitFor(); await dialog.getByText('Unassigned', { exact: true }).waitFor()
    await dialog.getByText(/Alex Chen.*2026-10-31/).waitFor()
    assert.equal(new URL(page.url()).pathname, '/projects'); assert.equal(new URL(page.url()).searchParams.get('priority'), 'High')

    // This return is provided by project_screens alone, independently of screen_patterns.
    assert.equal(me.flags.screen_patterns, false)
    const sourceMilestone = dialog.getByRole('link', { name: 'M-003 Open milestone 3', exact: true })
    assert.equal(await sourceMilestone.getAttribute('href'), `/projects/${project.projectNumber}/milestones?panel=Milestone:${linkedMilestone.id}`, 'native link keeps its exact href')
    await sourceMilestone.scrollIntoViewIfNeeded()
    await dialog.locator('[data-slot="dialog-body"]').evaluate(e => { e.scrollTop = 80 })
    await page.locator('main#content').evaluate(e => { e.scrollTop = 160 })
    const peekOrigin = page.url(), position = await page.evaluate(() => ({ main: document.querySelector('main#content').scrollTop, panel: document.querySelector('[data-slot="dialog-body"]').scrollTop }))
    assert.ok(position.main > 0); assert.ok(position.panel > 0, 'the source peek genuinely overflows')
    await sourceMilestone.click()
    await page.waitForURL(u => u.pathname === `/projects/${project.projectNumber}/milestones` && u.searchParams.get('panel') === `Milestone:${linkedMilestone.id}`)
    await page.getByRole('dialog').getByRole('heading', { name: linkedMilestone.name, exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(peekOrigin)
    await dialog.getByText(note, { exact: true }).waitFor(); assert.equal(peekReads, 2, 'return reopens the current source peek')
    await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected.main && document.querySelector('[data-slot="dialog-body"]')?.scrollTop === expected.panel, position)
    assert.equal(new URL(page.url()).searchParams.get('peek'), project.id); assert.equal(new URL(page.url()).searchParams.get('priority'), 'High'); assert.equal(new URL(page.url()).searchParams.get('group'), 'pm')
    assert.equal(new URL(page.url()).searchParams.get('clientId'), clientId); assert.equal(new URL(page.url()).searchParams.get('pmId'), me.id)
    await dialog.getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    note = 'Changed after the first peek'
    await page.getByRole('button', { name: `Peek at ${project.projectNumber}`, exact: true }).click()
    await dialog.getByText(note, { exact: true }).waitFor(); assert.equal(peekReads, 3, 'reopening reads current details')
    await dialog.getByRole('button', { name: 'Close', exact: true }).click()
    await page.setViewportSize({ width: 390, height: 844 })
    await page.getByRole('button', { name: `Peek at ${project.projectNumber}`, exact: true }).waitFor()
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)
    assert.equal(overflow, false, 'staffing/span phone cards remain contained')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: project staffing/filter/group/span and fresh peek; milestone return restores peek/query/main/dialog scroll with screen_patterns off')
  } catch (e) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw e } finally { await ui.browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
