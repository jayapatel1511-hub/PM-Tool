// 040-S15-Workspace: the workspace views go through the typed client. Home reads its overview from the contract path with the scope and the date range
// under their contract names, and Edit Dashboard saves the layout with a PUT and restores the default with a DELETE, each followed by a fresh read; Files
// reads with the scope, the search and the type; Team reads with the scope; the project-scope menu creates a named workspace with a POST, edits it with a
// PATCH whose version is in If-Match, and deletes it with a DELETE, each followed by a fresh read of the list. Every write carries X-Hub-Source: UI.
// Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project } = require('./platform-fixtures.cjs')
const { workspaceRow, widgetLayout, layoutAnswer, homeDeadline, homeRead, fileProject, fileLink, filesRead, teamPerson, teamRead } = require('./workspace.fixture.cjs')

const port = Number(process.env.WORKSPACE_TEST_PORT ?? 7311)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const task = id(901), milestone = id(902), wsId = id(951)
const until = async (what, ok) => { for (let i = 0; i < 100; i++) { if (ok()) return; await new Promise(resolve => setTimeout(resolve, 100)) } assert.fail(`timed out waiting for ${what}`) }

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = { home: [], files: [], team: [], workspaces: 0 }
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    let layout = widgetLayout(), isDefault = true
    let workspaces = []

    await page.route('**/api/v1/home**', route => {
      const w = send(route.request())
      reads.home.push(w.query)
      return route.fulfill({ json: homeRead({ projectId: project.id, layout, isDefault,
        upcomingDeadlines: { total: 2, truncated: false, items: [
          homeDeadline({ type: 'Task', id: task, projectId: project.id, key: 'P-MOCK-T0001', name: 'Pour the footing', date: '2026-10-06', status: 'In Progress', owner: 'Alex Chen' }),
          homeDeadline({ type: 'Milestone', id: milestone, projectId: project.id, key: 'P-MOCK-M0001', name: 'Issue for tender', date: '2026-10-09' }),
        ] } }) })
    })
    await page.route('**/api/v1/me/dashboard-layout', route => {
      const w = send(route.request())
      writes.push(w)
      if (w.method === 'PUT') { layout = w.body.widgets; isDefault = false } else { layout = widgetLayout(); isDefault = true }
      return route.fulfill({ json: layoutAnswer(layout, isDefault) })
    })
    await page.route('**/api/v1/files**', route => {
      const w = send(route.request())
      reads.files.push(w.query)
      const links = [fileLink({ id: id(911), title: 'Site portal', url: 'https://portal.example.test/site', linkType: 'Teams' }), fileLink({ id: id(912), title: 'Bridge folder', url: '\\\\fs01\\projects\\bridge', linkType: 'Network Folder', isNetworkPath: true })]
      return route.fulfill({ json: filesRead({ total: links.length, projects: [fileProject({ projectId: project.id, canAdd: true, count: links.length,
        items: [{ itemType: 'Project', itemId: project.id, key: 'P-MOCK', name: 'Synthetic live project', links }] })] }) })
    })
    await page.route('**/api/v1/team**', route => {
      const w = send(route.request())
      reads.team.push(w.query)
      return route.fulfill({ json: teamRead([teamPerson({ userId: id(921), displayName: 'Marc Dubois', jobTitle: 'Civil lead',
        projects: [{ projectId: project.id, projectNumber: 'P-MOCK', projectName: 'Synthetic live project', roles: ['TeamMember'], leads: ['Civil'] }] })], 1) })
    })
    await page.route('**/api/v1/workspaces**', route => {
      const w = send(route.request())
      if (w.method === 'GET') { reads.workspaces++; return route.fulfill({ json: workspaces }) }
      writes.push(w)
      if (w.method === 'POST') {
        workspaces = [workspaceRow({ id: wsId, name: w.body.name, projectIds: w.body.projectIds })]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/workspaces/${wsId}` }, json: workspaces[0] })
      }
      if (w.method === 'PATCH') {
        workspaces = [workspaceRow({ id: wsId, name: w.body.name, projectIds: w.body.projectIds, rowVersion: workspaces[0].rowVersion + 1 })]
        return route.fulfill({ json: workspaces[0] })
      }
      workspaces = []
      return route.fulfill({ status: 204, body: '' })
    })

    // Home: the overview is read for the default scope, with the figures, the lanes and the deadlines drawn from the typed answer.
    await page.goto(`${preview.base}/home`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Upcoming Deadlines' }).waitFor({ state: 'visible', timeout: 20000 })
    assert.deepEqual(reads.home.at(-1), { projects: 'mine' }, 'the overview is read from its contract path for the default scope, with no range')
    for (const text of ['Open Tasks', 'Synthetic live project']) assert.ok(await page.getByText(text).first().isVisible(), `Home shows ${text}`)
    const due = page.getByRole('button', { name: /Pour the footing/ })
    assert.match(await due.innerText(), /Alex Chen/, 'a deadline shows its owner') // the milestone has none
    assert.doesNotMatch(await page.getByRole('button', { name: /Issue for tender/ }).innerText(), /Alex Chen/)

    // The date range goes out as the contract's from and to.
    await page.getByRole('button', { name: /Upcoming Deadlines date range/ }).click()
    await page.locator('#h-from').fill('2026-10-05')
    await page.waitForFunction(() => location.search.includes('from=2026-10-05'))
    await page.waitForTimeout(300)
    assert.deepEqual(reads.home.at(-1), { projects: 'mine', from: '2026-10-05' })
    await page.keyboard.press('Escape')

    // Edit Dashboard: hiding a widget saves the whole layout with a PUT, then the overview is read again.
    await page.getByRole('button', { name: 'Edit Dashboard' }).click()
    const before = reads.home.length
    await page.getByRole('button', { name: 'Hide', exact: true }).first().click()
    await page.getByRole('button', { name: 'Restore default' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[0], { method: 'PUT', path: '/api/v1/me/dashboard-layout', query: {}, body: { widgets: widgetLayout(['metrics']) }, ifMatch: null, source: 'UI' })
    await until('the overview to be read again after the layout is saved', () => reads.home.length > before)

    // Restore default: a DELETE, the default layout back, and another read.
    const again = reads.home.length
    await page.getByRole('button', { name: 'Restore default' }).click()
    await page.getByRole('button', { name: 'Restore default' }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[1], { method: 'DELETE', path: '/api/v1/me/dashboard-layout', query: {}, body: null, ifMatch: null, source: 'UI' })
    await until('the overview to be read again after the default is restored', () => reads.home.length > again)
    await page.getByRole('button', { name: 'Done', exact: true }).click()

    // Files: the scope, then the search and the type, under their contract names.
    await page.goto(`${preview.base}/files`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Site portal').waitFor({ state: 'visible', timeout: 20000 })
    assert.deepEqual(reads.files.at(-1), { projects: 'mine' })
    assert.ok(await page.getByText('Bridge folder').isVisible(), 'a network path is listed beside the others')
    await page.locator('#files-search').fill('portal')
    await page.locator('#files-search').press('Enter')
    await page.waitForFunction(() => location.search.includes('q=portal'))
    await page.getByRole('button', { name: /^Filters/ }).click()
    await page.locator('#files-type').selectOption('Teams')
    await page.waitForFunction(() => location.search.includes('type=Teams'))
    await page.keyboard.press('Escape')
    await page.waitForTimeout(300)
    assert.deepEqual(reads.files.at(-1), { projects: 'mine', q: 'portal', type: 'Teams' })

    // Team: the scope, and the people drawn from the typed answer.
    await page.goto(`${preview.base}/team`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Marc Dubois').waitFor({ state: 'visible', timeout: 20000 })
    assert.deepEqual(reads.team.at(-1), { projects: 'mine' })
    assert.ok(await page.getByText('Civil lead').isVisible(), 'the job title is shown when there is one')

    // New workspace: the name and the chosen projects, then the list is read again and the scope follows it.
    const menu = () => page.getByRole('button', { name: /^Project scope:/ })
    await menu().click()
    await page.getByRole('menuitem', { name: 'New workspace' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.locator('#ws-name').fill('Atlantic Civil')
    await dialog.locator('#ws-projects').click()
    await page.getByRole('checkbox').first().click()
    await page.keyboard.press('Escape')
    const listed = reads.workspaces
    await dialog.getByRole('button', { name: 'Save', exact: true }).click()
    await page.waitForFunction(() => location.search.includes('ws='))
    assert.deepEqual(writes[2], { method: 'POST', path: '/api/v1/workspaces', query: {}, body: { name: 'Atlantic Civil', projectIds: [project.id] }, ifMatch: null, source: 'UI' })
    await until('the list of workspaces to be read again after it is created', () => reads.workspaces > listed)
    await page.getByRole('button', { name: 'Project scope: Atlantic Civil' }).waitFor({ state: 'visible' })
    await until('the team to be read for the workspace', () => reads.team.at(-1)?.projects === project.id)

    // Edit: the same body shape with the version in If-Match.
    await menu().click()
    await page.getByRole('menuitem', { name: 'Edit Atlantic Civil…' }).click()
    await dialog.locator('#ws-name').fill('Atlantic Civil North')
    await dialog.getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('button', { name: 'Project scope: Atlantic Civil North' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[3], { method: 'PATCH', path: `/api/v1/workspaces/${wsId}`, query: {}, body: { name: 'Atlantic Civil North', projectIds: [project.id] }, ifMatch: '"1"', source: 'UI' })

    // Delete: the version in If-Match, then the scope falls back to My projects and the list is read again.
    await menu().click()
    await page.getByRole('menuitem', { name: 'Edit Atlantic Civil North…' }).click()
    await dialog.getByRole('button', { name: 'Delete workspace' }).click()
    const gone = reads.workspaces
    await page.getByRole('dialog').getByRole('button', { name: 'Delete workspace' }).click()
    await page.getByRole('button', { name: 'Project scope: My projects' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[4], { method: 'DELETE', path: `/api/v1/workspaces/${wsId}`, query: {}, body: null, ifMatch: '"2"', source: 'UI' })
    await until('the list of workspaces to be read again after it is deleted', () => reads.workspaces > gone)

    assert.equal(writes.length, 5)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, homeReads: reads.home.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
