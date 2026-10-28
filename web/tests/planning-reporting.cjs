// Packet 047 mocked-browser workflows. These exercise rendered controls and network behaviour;
// API contract and persistence rules remain covered by the server tests.
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const { viewList } = require('./views.fixture.cjs')
const { myWork, myWorkPerson } = require('./my-work.fixture.cjs')
const { meView, referenceData, referenceClient, referenceOffice } = require('./me.fixture.cjs')
const assert = require('node:assert/strict')
const { spawn } = require('node:child_process')
const path = require('node:path')
const fs = require('node:fs')

const port = process.env.PORT || '19503'
const base = `http://127.0.0.1:${port}`
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const uid = id(1), pid = id(2)
const posts = [], unknown = [], errors = []
let routeEnabled = true, flagEnabled = true, readOnly = false, hidden = false, imported = false
const me = () => meView({ id: uid, displayName: readOnly ? 'Read Only' : 'Priya PM', email: 'priya@hub.test', isTemplateEditor: true,
  systemRoles: readOnly ? ['ReadOnly'] : ['ProjectManager'], capabilities: { createProject: !readOnly, createTask: !readOnly, portfolio: true, workload: true, staff: true, allStaff: true, admin: !readOnly, templates: !readOnly, readOnly },
  settings: { today: '2026-10-18', taskDueSoonDays: 7, chainDepthLimit: 20, coordinationLookaheadWeeks: 4, workingDaysEnabled: true, routeCounterEnabled: routeEnabled },
  preferences: { denseRows: false }, flags: { onboarding_card: flagEnabled, route_counter: flagEnabled, project_import: flagEnabled, my_work_follow_up: true, project_status_note: true, template_roles: true } })
const work = myWork({ person: myWorkPerson(uid, 'Priya PM'), today: '2026-10-18' })
const project = { id: pid, projectNumber: 'P-DEMO', name: 'Synthetic project', client: 'Pilot client', pmName: 'Priya PM', pm: { displayName: 'Priya PM' }, office: { name: 'Halifax' }, status: 'Active', visibility: 'Open', rowVersion: 1, links: [], disciplines: [], starred: false, myRoles: ['PM'], health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] }, statusNote: { text: 'Current note', at: '2026-10-17T12:00:00Z', author: 'Priya PM', header: { computedHealth: 'Green', reportedHealth: 'Green', openAttention: 0 } }, permissions: { edit: { ok: true }, needsReason: false, isPm: true, leadOf: [], createTaskIn: [], createDeliverableIn: [], transitions: [] } }
const template = { id: id(10), familyId: id(11), name: 'Synthetic template', description: 'Browser fixture', status: 'Draft', rowVersion: 1, canEdit: true, family: [], disciplines: [], milestones: [{ ref: 'm1', name: 'Kickoff', milestoneType: 'Kickoff', anchor: 'ProjectStart', offset: 0, isClientFacing: true }], deliverables: [], tasks: [], dependencies: [], roles: [{ ref: 'r1', disciplineId: null, label: 'Project lead', startMilestoneRef: 'm1', startOffsetDays: 0, startOffsetDayKind: 'WorkingDay', endMilestoneRef: null, endOffsetDays: 0, endOffsetDayKind: 'WorkingDay', hours: 8, note: null }], basisSuggestions: [] }

;(async () => {
  const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' })
  let browser
  try {
    for (let i = 0; i < 60; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
    browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
    await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'priya@hub.test'))
    page.on('pageerror', e => errors.push(e.message))
    await page.route('**/api/**', async route => {
      const req = route.request(), u = new URL(req.url()), p = u.pathname.replace('/api/v1/', ''), method = req.method()
      let status = 200, data = {}
      if (p === 'config') data = clientConfig()
      else if (p === 'me') data = me()
      else if (p === 'me/sign-in') data = {}
      else if (p === 'me/notifications/pulse') data = { stamp: '0' }
      else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }
      else if (p === 'me/work') data = work
      else if (p === 'me/work/watching' || p === 'me/work/requested') data = { total: 1, items: [{ id: id(30), itemType: 'Task', key: 'P-DEMO-T1', title: 'Synthetic follow-up', projectNumber: 'P-DEMO', status: 'Open', ownerName: 'Priya PM' }] }
      else if (p === 'me/preferences') data = { events: [], digest: { digestEnabled: true, holdDigestWhenAway: false, time: '09:00', orgTime: 'America/Halifax', weekendDigests: false, sections: [], sectionsOff: [], weeklySummaryEnabled: true, managesProjects: true }, follows: [] }
      else if (p === 'planning/grid') data = { weeks: [], people: [], today: '2026-10-18', currentWeek: '2026-10-12', defaultCapacity: 40, thresholds: { overPct: 110, amberPct: 90, underPct: 40 } }
      else if (p === 'me/onboarding') data = { hidden, done: false, completed: 0, total: 7, groups: [{ role: 'Everyone', steps: Array.from({ length: 7 }, (_, i) => ({ key: `step-${i}`, label: `Step ${i + 1}`, route: '/my-work', done: false })) }] }
      else if (p === 'me/onboarding/hide') { hidden = true; data = null }
      else if (p === 'me/onboarding/show') { hidden = false; data = null }
      else if (p === 'projects') data = { items: [project], totalCount: 1, page: 1, pageSize: 50 }
      else if (p === `projects/${pid}` || p === 'projects/P-DEMO') data = project
      else if (p === `projects/${pid}/status-note` || p === `projects/P-DEMO/status-note`) data = project.statusNote
      else if (p === 'reference') data = referenceData({ clients: [referenceClient(id(20), 'Pilot client')], offices: [referenceOffice(id(21), 'Halifax')] })
      else if (p === 'templates') data = method === 'POST' ? { id: template.id } : { canEdit: true, templates: [{ ...template, milestones: 1, deliverables: 0, tasks: 0, projects: 0 }] }
      else if (p === `template/${template.id}` || p === `templates/${template.id}`) data = template
      else if (p === `templates/${template.id}/structure` && method === 'PUT') data = template
      else if (p === `templates/${template.id}/preview`) data = { counts: { milestones: 1, deliverables: 0, tasks: 0, roles: 2 }, undated: { milestones: 1, deliverables: 0, tasks: 0 }, milestones: [{ id: 'm1', name: 'Kickoff', date: '2026-10-20', source: 'ProjectStart' }] }
      else if (p === 'projects/import-preview') { const body = req.postDataJSON(); data = { rows: [{ row: 2, projectNumber: 'P-NEW', name: 'New project', classification: body.csv.includes('Unknown') ? 'Error' : 'Create', error: body.csv.includes('Unknown') ? 'Client not found' : undefined }], creates: body.csv.includes('Unknown') ? 0 : 1, exists: 0, errors: body.csv.includes('Unknown') ? 1 : 0, canApply: !body.csv.includes('Unknown') } }
      else if (p === 'projects/import') { imported = true; data = { rows: [], creates: 1, exists: 0, errors: 0, canApply: true } }
      else if (p === 'route-counts') data = null
      else if (p === 'settings') data = {}
      else if (p === 'views') data = viewList([], true)
      else if (p === 'workspaces') data = []
      else { unknown.push(`${method} ${p}`); data = {} }
      if (method !== 'GET' && method !== 'HEAD') posts.push({ path: p, body: req.postDataJSON?.() })
      await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
    })

    await page.goto(`${base}/my-work`)
    const card = page.getByRole('region', { name: /getting started/i })
    await card.waitFor({ timeout: 8000 }).catch(async error => { console.error('onboarding-load-failure', await page.locator('body').innerText(), errors, unknown); throw error })
    assert.match(await card.innerText(), /0 of 7|Step/i)
    const toggle = card.getByRole('button').first(); await toggle.click(); await page.getByText('Step 1').waitFor()
    await toggle.focus(); await page.keyboard.press('Enter'); await page.keyboard.press('Enter'); await page.getByText('Step 1').waitFor()
    await card.getByRole('button', { name: 'Hide', exact: true }).click(); await page.waitForTimeout(50)
    assert.ok(posts.some(x => x.path === 'me/onboarding/hide'))

    await page.goto(`${base}/preferences`); await page.getByRole('button', { name: /show getting started/i }).click({ timeout: 8000 }).catch(async error => { console.error('restore-load-failure', await page.locator('body').innerText(), errors, unknown); throw error }); await page.waitForTimeout(50)
    assert.ok(posts.some(x => x.path === 'me/onboarding/show'))

    const routePostsBefore = posts.filter(x => x.path === 'route-counts').length
    await page.goto(`${base}/my-work`); await page.waitForTimeout(100)
    assert.ok(posts.filter(x => x.path === 'route-counts').length > routePostsBefore, 'enabled route timing sends a matched route')
    assert.ok(posts.filter(x => x.path === 'route-counts').every(x => !/[?&/]/.test(x.body?.route ?? '')), 'route payload contains no raw URL/query')
    routeEnabled = false; flagEnabled = false
    await page.reload(); await page.waitForTimeout(80)
    assert.equal(posts.filter(x => x.path === 'route-counts').length, routePostsBefore + 1, 'disabled gates send no route timing')

    flagEnabled = true; routeEnabled = true; readOnly = false
    await page.reload(); await page.goto(`${base}/projects`); await page.getByRole('main').getByRole('button', { name: 'More', exact: true }).click(); await page.getByRole('menuitem', { name: /import/i }).click();
    const importDialog = page.getByRole('dialog'); await importDialog.getByLabel('CSV content', { exact: true }).fill('project number,name,client,office,project manager email\nP-NEW,New project,Unknown,Halifax,pm@hub.test')
    await importDialog.getByRole('button', { name: /preview/i }).click(); await importDialog.getByText(/1 Error/i).waitFor(); assert.ok(await importDialog.getByRole('button', { name: /create projects/i }).isDisabled())
    await importDialog.getByLabel('CSV content', { exact: true }).fill('project number,name,client,office,project manager email\nP-NEW,New project,Pilot client,Halifax,pm@hub.test'); await importDialog.getByRole('button', { name: /preview/i }).click(); await importDialog.getByText(/1 Create/i).waitFor(); assert.equal(await importDialog.getByRole('button', { name: /create projects/i }).isDisabled(), false); await importDialog.getByRole('button', { name: /create projects/i }).click(); await importDialog.getByText(/Created 1 Setup project/i).waitFor(); assert.equal(imported, true)

    await page.goto(`${base}/my-work?tab=watching`); await page.getByRole('region', { name: /watching/i }).waitFor(); await page.getByRole('button', { name: /views:/i }).click(); await page.getByRole('menuitemradio', { name: /requested/i }).click(); await page.getByRole('region', { name: /requested/i }).waitFor()

    await page.goto(`${base}/templates/${template.id}`); await page.getByText(/Template roles/i).first().waitFor(); const roles = page.getByText(/Template roles/i).first().locator('..'); const roleLabels = page.getByLabel('Role label'); await roleLabels.first().fill('Edited role'); await page.getByLabel('Start milestone').first().selectOption('m1'); await page.getByLabel('End milestone').first().selectOption('m1'); await page.getByLabel('Role discipline').first().selectOption(''); await roles.getByRole('button', { name: /Add/i }).click(); await page.getByLabel('Role label').nth(1).fill('Added role'); assert.equal(await page.getByLabel('Role label').count(), 2); await page.getByRole('button', { name: /Remove/i }).last().click(); assert.equal(await page.getByLabel('Role label').count(), 1); await page.getByLabel('Start date', { exact: true }).fill('2026-10-18'); await page.locator('p').filter({ hasText: /^Milestones: 1, deliverables: 0, tasks: 0/i }).first().waitFor(); await page.locator('p').filter({ hasText: /undated.*milestones: 1/i }).first().waitFor(); assert.match(await page.getByText(/Kickoff/i).first().innerText(), /Kickoff/); await page.getByText(/would be 2026-10-20/i).waitFor()
    readOnly = true; await page.goto(`${base}/my-work`); assert.equal(await page.getByRole('region', { name: /getting started/i }).count(), 0, 'read-only users do not see onboarding')
    const axe = await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') }).then(() => page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa'] } })).violations.map(v => v.id)))
    assert.deepEqual(errors, []); assert.deepEqual(unknown, []); assert.deepEqual(axe, [])
    console.log(JSON.stringify({ scope: 'Chromium with mocked API', posts: posts.length, imported, errors, unknown, axe }))
  } finally { await browser?.close(); server.kill() }
})().catch(e => { console.error(e); process.exitCode = 1 })
