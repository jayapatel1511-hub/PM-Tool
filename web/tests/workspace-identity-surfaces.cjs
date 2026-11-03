// Remaining052 project-bearing workspace reads; actual app with explicitly synthetic APIs.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, panelIds } = require('./platform-fixtures.cjs')
const { meView } = require('./me.fixture.cjs')
const { homeRead, homeDeadline, fileProject, fileLink, filesRead, teamPerson, teamRead } = require('./workspace.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const other = '00000000-0000-0000-0000-000000008102'
const kinds = ['Task', 'Deliverable', 'Milestone']
const ids = [task.id, panelIds.deliverable, panelIds.milestone]
;(async () => {
  const { page, browser } = await newMockPage(base)
  page.setDefaultTimeout(8000)
  let enabled = false, colour = '#1597cc'
  const errors = [], writes = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  await page.route('**/api/v1/**', route => {
    const request = route.request(), path = new URL(request.url()).pathname.replace('/api/v1/', '')
    if (path === 'me') return route.fulfill({ json: meView({ ...me, flags: { ...me.flags, visual_system: enabled, home_overview: false } }) })
    if (path === 'home') return route.fulfill({ json: homeRead({ projectId: project.id,
      upcomingDeadlines: { total: 3, truncated: false, items: kinds.map((type, index) => homeDeadline({ type, id: ids[index], projectId: project.id,
        key: `${project.projectNumber}-${type[0]}${index + 1}`, name: `Synthetic ${type.toLowerCase()} identity`, date: '2026-10-06', identityColour: colour })) } }) })
    if (path === 'team') return route.fulfill({ json: teamRead([teamPerson({ userId: me.id, displayName: me.displayName, projects: [
      { projectId: project.id, projectNumber: project.projectNumber, projectName: project.name, roles: ['TeamMember'], leads: [], identityColour: colour },
      { projectId: other, projectNumber: 'SYN-UNKNOWN', projectName: 'Synthetic unknown identity', roles: ['TeamMember'], leads: [], identityColour: null },
    ] })], 2) })
    if (path === 'files') return route.fulfill({ json: filesRead({ total: 2, projects: [project.id, other].map((projectId, index) => fileProject({ projectId,
      projectNumber: index ? 'SYN-UNKNOWN' : project.projectNumber, name: index ? 'Synthetic unknown identity' : project.name,
      identityColour: index ? null : colour, count: 1, items: [{ itemType: 'Project', itemId: projectId,
        key: index ? 'SYN-UNKNOWN' : project.projectNumber, name: 'Synthetic link group', links: [fileLink({ id: `${projectId}-link`, title: 'Synthetic reference', url: 'https://example.test/reference.pdf' })] }] })) }) })
    if (request.method() !== 'GET') writes.push(path)
    return route.fallback()
  })
  const deadlines = () => page.locator('#h-deadlines')
  const marks = () => deadlines().locator('[data-item-mark]')
  const fills = locator => locator.evaluateAll(nodes => nodes.map(node => getComputedStyle(node).backgroundColor))
  const legacy = () => deadlines().locator('li [data-accent]').evaluateAll(nodes => nodes.map(node => ({ accent: node.dataset.accent, background: getComputedStyle(node).backgroundColor })))
  try {
    await page.goto(`${base}/home`); await deadlines().getByText('Synthetic task identity', { exact: true }).waitFor()
    const offMarks = await legacy(); assert.equal(offMarks.length, 3); assert.equal(await marks().count(), 0)
    enabled = true; await page.reload(); await marks().nth(2).waitFor()
    assert.deepEqual(await marks().evaluateAll(nodes => nodes.map(node => node.dataset.itemKind)), kinds)
    assert.deepEqual(await fills(marks()), kinds.map(() => 'rgb(21, 151, 204)'))
    assert.ok((await marks().evaluateAll(nodes => nodes.map(node => ({ width: getComputedStyle(node).width, height: getComputedStyle(node).height })))).every(row => row.width === '10px' && row.height === '10px'))
    const homeUrl = page.url()
    await deadlines().getByRole('button', { name: /Synthetic task identity/ }).click()
    await page.waitForURL(value => value.searchParams.get('panel') === `Task:${task.id}`)
    await page.locator('[data-slot="sheet-content"]').waitFor(); await page.keyboard.press('Escape'); await page.waitForURL(homeUrl)
    await page.goto(`${base}/team`); await page.locator(`[data-identity-colour="${colour}"]`).waitFor()
    assert.equal(await page.locator('[data-identity-state="missing"]').count(), 1)
    assert.equal(await page.locator('[data-identity-state="missing"][data-accent]').count(), 0)
    assert.equal(await page.getByRole('link', { name: `${project.projectNumber} ${project.name}`, exact: true }).getAttribute('href'), `/projects/${project.projectNumber}/team`)
    await page.goto(`${base}/files`); const file = () => page.locator(`#f-${project.id}`)
    await file().locator(`[data-identity-colour="${colour}"]`).waitFor()
    assert.equal(await page.locator(`#f-${other} [data-identity-state="missing"]`).count(), 1)
    assert.equal(await file().getByRole('heading').getByRole('link').getAttribute('href'), `/projects/${project.projectNumber}`)
    colour = null
    await page.goto(`${base}/home`); await marks().nth(2).waitFor()
    assert.deepEqual(await fills(marks()), kinds.map(() => 'rgb(70, 80, 99)'), 'missing identity remains neutral, never a project hash')
    for (const route of ['home', 'team', 'files']) {
      await page.setViewportSize({ width: 375, height: 812 }); await page.goto(`${base}/${route}`)
      await page.getByRole('heading', { name: route === 'home' ? 'Home' : route === 'team' ? 'Team' : 'Files', exact: true }).first().waitFor()
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `${route} keeps phone reflow`)
    }
    enabled = false; colour = '#1597cc'; await page.setViewportSize({ width: 1440, height: 1000 })
    await page.goto(`${base}/home`); await deadlines().getByText('Synthetic task identity', { exact: true }).waitFor()
    assert.equal(await marks().count(), 0); assert.deepEqual(await legacy(), offMarks)
    await page.goto(`${base}/team`); await page.getByRole('link', { name: `${project.projectNumber} ${project.name}`, exact: true }).waitFor()
    assert.equal(await page.locator('[data-identity-colour], [data-identity-state]').count(), 0)
    await page.goto(`${base}/files`); await file().waitFor()
    assert.equal(await page.locator('[data-identity-colour], [data-identity-state]').count(), 0, 'off Files keeps its original header without an added mark')
    assert.deepEqual(writes, []); assert.deepEqual(errors, [])
    console.log('PASS: workspace stored identity/type marks, missing neutral fallback, retained exact opens/links, phone reflow and stale-wire off restoration')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
