// S14 actual-app/mock-API acceptance. Companion API cases prove real sharing/visibility/version boundaries.
const assert = require('node:assert/strict')
const { newMockPage, me, project, panelIds, comments, links } = require('./platform-fixtures.cjs')
const { projectTimeline, ganttPage } = require('./timeline.fixture.cjs')
const { projectDiscipline, projectList, projectRow } = require('./projects.fixture.cjs')
const { deliverableList, deliverableRow } = require('./deliverables.fixture.cjs')
const { milestoneRow } = require('./milestones.fixture.cjs')
const { taskRow, taskDetail, taskPermissions } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const civil = project.disciplines[0].id, electrical = id(6201), d1 = panelIds.deliverable, d2 = id(6202), t1 = id(6203), t2 = id(6204), te = id(6205), milestone = panelIds.milestone
const ids = { discipline: civil, task: t1, deliverable: d1, milestone, handoff: id(6206) }
const schedule = projectTimeline(ids)
Object.assign(schedule.tasks[0], { key: 'S14-T001', name: 'Shared Civil task', originalDueDate: '2026-10-28' })
schedule.tasks.push({ ...schedule.tasks[0], id: t2, key: 'S14-T002', name: 'Shared successor task', startDate: '2026-10-28', dueDate: '2026-11-03' },
  { ...schedule.tasks[0], id: te, key: 'S14-T003', name: 'Electrical excluded task', deliverableId: d2, projectDisciplineId: electrical })
Object.assign(schedule.deliverables[0], { key: 'S14-D001', name: 'Shared Civil deliverable', originalDueDate: '2026-10-27' })
schedule.deliverables.push({ ...schedule.deliverables[0], id: d2, key: 'S14-D002', name: 'Electrical excluded deliverable' })
schedule.dependencies.push({ id: id(6207), predecessorTaskId: t1, successorTaskId: t2, type: 'FS', lagDays: 0, highlighted: true })
schedule.milestones[0].name = 'S14 review milestone'
const secondProject = { ...project, id: id(6210), projectNumber: 'P-S14-2', name: 'Second visible Timeline project' }
const secondSchedule = projectTimeline({ discipline: id(6211), task: id(6212), deliverable: id(6213), milestone: id(6214), handoff: id(6215) })
const timeline = { ...ganttPage(project, schedule), projects: [...ganttPage(project, schedule).projects, ...ganttPage(secondProject, secondSchedule).projects] }
const sourceSnapshot = JSON.stringify(timeline)
const definitions = [], viewReads = [], timelineReads = [], writes = [], errors = []
const apiMutations = [], businessMutations = []
const allowedMutation = (method, path) => method === 'POST' && (path === '/api/v1/views' || path === '/api/v1/me/sign-in')
let viewer = 'Priya'
const people = { Priya: { id: id(6220), name: 'Priya Nair' }, Marc: { id: id(6221), name: 'Marc Dubois' } }

;(async () => {
  const originalMe = structuredClone(me), originalDisciplines = project.disciplines, { browser, page, mock } = await newMockPage(base, { events: false })
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    page.on('request', request => {
      const path = new URL(request.url()).pathname, method = request.method()
      if (!path.startsWith('/api/') || method === 'GET' || method === 'HEAD') return
      const mutation = { method, path }; apiMutations.push(mutation)
      if (!allowedMutation(method, path)) businessMutations.push(mutation)
    })
    me.flags = { ...Object.fromEntries(Object.keys(me.flags).map(key => [key, false])), work_screens: true, schedule_baseline: true }
    const setViewer = name => { viewer = name; me.id = people[name].id; me.displayName = people[name].name }
    setViewer('Priya')
    project.disciplines = [...originalDisciplines, projectDiscipline({ id: electrical, disciplineId: id(6222), name: 'Electrical' })]
    await page.addInitScript(() => { window.__timelinePrints = 0; window.print = () => { window.__timelinePrints++ } })
    await page.route('**/api/v1/projects?**', route => route.fulfill({ json: projectList([projectRow(project), projectRow(secondProject)]) }))
    await page.route(`**/api/v1/projects/${project.id}/milestones*`, route => route.fulfill({ json: [milestoneRow({ id: milestone, projectId: project.id, key: 'S14-M01', name: 'S14 review milestone', date: '2026-11-01', originalDate: '2026-10-29', baselineDate: '2026-10-29' })] }))
    await page.route(`**/api/v1/projects/${project.id}/deliverables*`, route => route.fulfill({ json: deliverableList(schedule.deliverables.map((row, n) => deliverableRow({ ...row, projectId: project.id,
      projectDisciplineId: n ? electrical : civil, disciplineName: n ? 'Electrical' : 'Civil', milestoneId: milestone, ownerId: me.id, requiresReview: false }))) }))
    await page.route(`**/api/v1/projects/${project.id}/timeline*`, route => {
      timelineReads.push({ route: 'project', params: Object.fromEntries(new URL(route.request().url()).searchParams) }); return route.fulfill({ json: schedule })
    })
    await page.route('**/api/v1/timeline*', route => {
      const query = Object.fromEntries(new URL(route.request().url()).searchParams); timelineReads.push({ route: 'workspace', params: query })
      const selected = ['mine', 'all', undefined].includes(query.projects) ? timeline.projects : timeline.projects.filter(row => query.projects.split(',').includes(row.project.id))
      return route.fulfill({ json: { ...timeline, projects: selected } })
    })
    await page.route(/\/api\/v1\/tasks\/[0-9a-f-]+(?:\/dependencies)?(?:\?.*)?$/, route => {
      const path = new URL(route.request().url()).pathname, taskId = path.split('/')[4], source = schedule.tasks.find(row => row.id === taskId); assert.ok(source)
      return route.fulfill({ json: path.endsWith('/dependencies') ? taskDependencies() : taskDetail({ task: taskRow({ ...source, projectId: project.id, assigneeId: me.id, assigneeName: me.displayName }), permissions: taskPermissions({ dependencies: false, transitions: [] }) }) })
    })
    await page.route(/\/api\/v1\/items\/Task\/[0-9a-f-]+\/(comments|links|activity)(?:\?.*)?$/, route => {
      const path = new URL(route.request().url()).pathname
      return route.fulfill({ json: path.endsWith('/activity') ? { items: [], page: 1, pageSize: 100, totalCount: 0 } : path.endsWith('/comments') ? comments : links })
    })
    await page.route('**/api/v1/views**', route => {
      const request = route.request(), url = new URL(request.url()), kind = url.searchParams.get('listType'), pid = url.searchParams.get('projectId')
      if (request.method() === 'GET') {
        viewReads.push({ kind, pid, viewer })
        return route.fulfill({ json: viewList(definitions.filter(row => row.body.listType === kind && (row.body.projectId ?? null) === pid && (row.body.scope === 'Project' || row.owner === me.id))
          .map(row => savedView({ id: row.id, name: row.body.name, projectId: row.body.projectId, scope: row.body.scope, isDefault: row.body.isDefault,
            params: row.body.params, canEdit: true, owner: row.ownerName })), !!pid) })
      }
      assert.equal(request.method(), 'POST'); const body = request.postDataJSON(); writes.push(body)
      for (const key of ['panel', 'taskSection', 'view', 'page', 'move', 'preview']) assert.equal(body.params[key], undefined, `${key} cannot become a saved Timeline definition`)
      const viewId = id(6300 + definitions.length); definitions.push({ id: viewId, body, owner: me.id, ownerName: me.displayName }); return route.fulfill({ status: 201, json: viewSaved(viewId) })
    })
    // Register last so unexpected writes cannot reach the platform fixture's permissive non-GET fallback.
    await page.route('**/api/**', async route => {
      const request = route.request(), method = request.method(), path = new URL(request.url()).pathname
      if (method !== 'GET' && method !== 'HEAD' && !allowedMutation(method, path))
        return route.fulfill({ status: 409, json: { error: 'Unexpected API mutation rejected by Timeline acceptance fixture' } })
      return route.fallback()
    })
    const projectUrl = `${base}/projects/${project.projectNumber}/timeline`
    const region = () => page.getByRole('region', { name: 'Timeline', exact: true })
    const rowButton = name => page.locator('[role="rowheader"] button').filter({ hasText: name })
    async function openFilters() { await page.getByRole('button', { name: /^Filters/ }).click(); await page.locator('[data-timeline-layer-controls]:visible').waitFor() }
    async function toggleLayer(name, keyboard = false) {
      const controls = page.locator('[data-timeline-layer-controls]:visible')
      const keys = { Dependencies: 'dependencies', Milestones: 'milestones', 'Original dates': 'originalDates', 'Baseline dates': 'baselineDates', Handoffs: 'handoffs' }
      const checkbox = controls.getByRole('checkbox', { name, exact: true })
      const checked = await checkbox.getAttribute('aria-checked')
      assert.ok(checked === 'true' || checked === 'false', `${name} has a settled boolean state`)
      const expected = []
      for (const [label, key] of Object.entries(keys)) {
        const option = controls.getByRole('checkbox', { name: label, exact: true })
        if (await option.count() && (label === name ? checked === 'false' : await option.getAttribute('aria-checked') === 'true')) expected.push(key)
      }
      if (keyboard) await checkbox.press('Space'); else await checkbox.click()
      // A click/press resolves before React Router necessarily commits this URL-backed state.
      // Settle each real transition before a second toggle can read the previous render's layer set.
      await page.waitForURL(url => url.searchParams.get('layers') === (expected.sort().join(',') || 'none'))
      await controls.getByRole('checkbox', { name, exact: true, checked: checked === 'false' }).waitFor()
    }
    async function save(name, share = false, def = false) {
      await page.getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
      const dialog = page.getByRole('dialog').last(); await dialog.getByLabel('Name', { exact: true }).fill(name)
      if (share) await dialog.getByRole('checkbox', { name: 'Share with everyone on this project', exact: true }).click()
      if (def) await dialog.getByRole('checkbox', { name: 'Open this list in this view by default', exact: true }).click()
      await dialog.getByRole('button', { name: 'Save', exact: true }).click(); await dialog.waitFor({ state: 'detached' }); await page.waitForURL(url => url.searchParams.has('view'))
    }

    await page.goto(`${projectUrl}?disciplineId=${civil}&zoom=month`)
    await page.getByRole('button', { name: 'Show the 2 tasks of S14-D001', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('expanded') === d1); await region().locator('svg[data-timeline-layer="dependencies"]').waitFor()
    assert.equal(await rowButton('Electrical excluded deliverable').count(), 0)
    await region().locator('[title^="Original dates"]').first().waitFor()
    await region().locator('[title^="PM baseline"]').first().waitFor()
    const readCount = timelineReads.length
    await openFilters(); await toggleLayer('Original dates'); await toggleLayer('Baseline dates'); await page.keyboard.press('Escape')
    await region().locator('[title^="Original dates"],[title^="PM baseline"]').first().waitFor({ state: 'detached' })
    assert.equal(await region().locator('[title^="Original dates"],[title^="PM baseline"]').count(), 0)
    assert.equal(timelineReads.length, readCount, 'layer choices add no canonical HTTP reads')
    await save('Civil Months and dependencies', true)
    const shared = definitions[0].body; assert.equal(shared.scope, 'Project'); assert.equal(shared.projectId, project.id)
    assert.equal(shared.params.disciplineId, civil); assert.equal(shared.params.zoom, 'month'); assert.equal(shared.params.layers, 'dependencies,milestones')
    assert.equal(shared.params.group, 'discipline'); assert.equal(shared.params.cols, 'items,dates'); assert.equal(shared.params.expanded, d1); assert.equal(shared.params.collapsed, 'none')

    setViewer('Marc'); await page.goto(projectUrl); await page.getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Civil Months and dependencies', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('view') === definitions[0].id && url.searchParams.get('disciplineId') === civil && url.searchParams.get('zoom') === 'month')
    await rowButton('Shared Civil task').waitFor(); await rowButton('Shared successor task').waitFor(); assert.equal(await rowButton('Electrical excluded task').count(), 0)
    await region().locator('svg[data-timeline-layer="dependencies"]').waitFor()
    const origin = page.url(); await rowButton('Shared Civil task').press('Enter'); await page.waitForURL(url => url.searchParams.get('panel') === `Task:${t1}`)
    await page.getByRole('dialog').last().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    await page.getByRole('button', { name: 'Print', exact: true }).click(); assert.equal(await page.evaluate(() => window.__timelinePrints), 1)
    await openFilters(); const controls = page.locator('[data-timeline-layer-controls]:visible')
    assert.equal(await controls.getByRole('checkbox', { name: 'Dependencies', exact: true }).getAttribute('aria-checked'), 'true')
    assert.equal(await controls.getByRole('checkbox', { name: 'Baseline dates', exact: true }).getAttribute('aria-checked'), 'false')
    await toggleLayer('Dependencies', true); await page.keyboard.press('Escape')
    await region().locator('svg[data-timeline-layer="dependencies"]').first().waitFor({ state: 'detached' })
    assert.equal(await region().locator('svg[data-timeline-layer="dependencies"]').count(), 0); assert.equal(await rowButton('Shared Civil task').count(), 1)
    await page.goto(`${projectUrl}?disciplineId=${civil}&zoom=month&expanded=${d1}&layers=none`)
    await rowButton('Shared Civil task').waitFor(); assert.equal(await region().locator('svg[data-timeline-layer="dependencies"]').count(), 0)
    await openFilters(); assert.equal(await page.locator('[data-timeline-layer-controls]:visible [role="checkbox"][aria-checked="true"]').count(), 0); await page.keyboard.press('Escape')
    me.flags.schedule_baseline = false; me.flags.timeline_handoffs_chain = false
    await page.goto(`${projectUrl}?disciplineId=${civil}&expanded=${d1}&layers=baselineDates,handoffs,dependencies,milestones,originalDates`)
    await rowButton('Shared Civil task').waitFor(); assert.equal(await region().locator('[title^="PM baseline"]').count(), 0)
    await openFilters(); assert.equal(await page.locator('[data-timeline-layer-controls]:visible').getByRole('checkbox').count(), 3); assert.equal(await page.locator('#tl-chain').count(), 0); await page.keyboard.press('Escape')
    me.flags.schedule_baseline = true; me.flags.timeline_handoffs_chain = true
    await page.goto(`${projectUrl}?disciplineId=${civil}&expanded=${d1}`); await rowButton('Shared Civil task').waitFor()
    await openFilters(); assert.equal(await page.locator('[data-timeline-layer-controls]:visible').getByRole('checkbox').count(), 5); await page.keyboard.press('Escape')

    const scope = `${project.id},${secondProject.id}`
    await page.goto(`${base}/timeline?projects=${scope}&zoom=quarter&start=2026-10-01&hideCompleted=true&layers=dependencies,milestones&collapsed=none`)
    await page.getByRole('button', { name: /Shared Civil task/ }).first().waitFor()
    await save('Marc quarterly workspace', false, true)
    const personal = definitions[1].body; assert.equal(personal.scope, 'Personal'); assert.equal(personal.projectId, null)
    assert.equal(personal.params.projects, scope); assert.equal(personal.params.start, '2026-10-01'); assert.equal(personal.params.zoom, 'quarter')
    assert.equal(personal.params.group, 'project'); assert.equal(personal.params.cols, 'items,dates'); assert.equal(personal.params.layers, 'dependencies,milestones')
    await page.goto(`${base}/timeline`); await page.waitForURL(url => url.searchParams.get('view') === definitions[1].id && url.searchParams.get('projects') === scope)
    await page.goto(`${base}/timeline?projects=${project.id}&zoom=week`); await page.getByRole('button', { name: 'Views', exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('zoom'), 'week'); assert.equal(new URL(page.url()).searchParams.get('view'), null, 'an explicit URL wins over a personal default')
    await page.goto(`${base}/timeline?projects=mine&zoom=week`); await page.getByRole('button', { name: 'Views', exact: true }).waitFor(); await save('Marc explicit My projects')
    assert.equal(definitions[2].body.params.projects, 'mine', 'effective default scope is captured, not inherited later')
    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 }); await openFilters()
      assert.equal(await page.locator('[data-timeline-layer-controls]:visible').getByRole('checkbox').count(), 4)
      await page.keyboard.press('Escape'); assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `no body overflow at${width}`)
    }
    for (const small of [false, true]) {
      me.flags.work_screens = false; me.flags.small_ux = small; const readsBefore = viewReads.length
      await page.goto(`${projectUrl}?disciplineId=${civil}&zoom=month&layers=none&expanded=${d1}`)
      await page.getByRole('button', { name: 'Print', exact: true }).waitFor(); assert.equal(await page.getByRole('button', { name: 'Views', exact: true }).count(), 0)
      assert.equal(await page.locator('[data-timeline-layer-controls]').count(), 0); assert.equal(await rowButton('Shared Civil task').count(), 0, 'off retains project local expansion')
      assert.equal(viewReads.length, readsBefore)
    }
    assert.equal(JSON.stringify(timeline), sourceSnapshot, 'no schedule/permission/source data changes')
    assert.equal(writes.length, 3); assert.equal(apiMutations.filter(mutation => mutation.method === 'POST' && mutation.path === '/api/v1/views').length, 3)
    assert.deepEqual(businessMutations, [], 'only the three intended view saves and optional sign-in baseline may mutate the mocked API')
    assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(mock.unknownGets, [])
    for (const read of timelineReads) assert.deepEqual(Object.keys(read.params), read.route === 'project' ? [] : ['projects'])
    console.log(JSON.stringify({ result: 'PASS', shared: 'Priya→Marc', filters: 'Civil', zoom: 'month', layers: 'dependencies,milestones', workspace: 'personal quarter', defaults: 'explicit URL wins', widths: [375, 768, 1280], businessWrites: businessMutations.length,
      viewWrites: apiMutations.filter(mutation => mutation.path === '/api/v1/views').length, signInBaselineWrites: apiMutations.filter(mutation => mutation.path === '/api/v1/me/sign-in').length }))
  } finally { Object.assign(me, originalMe); project.disciplines = originalDisciplines; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
