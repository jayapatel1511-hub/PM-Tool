// Packet 052 S07: real shell tab rows, parent-area modes, scope retention and phone containment.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')
const { homeRead } = require('./workspace.fixture.cjs')
const { viewList } = require('./views.fixture.cjs')
const { planningGrid } = require('./planning.fixture.cjs')
const { projectDetail, projectList, projectRow } = require('./projects.fixture.cjs')
const { coordinationOptions, reviewPage } = require('./reviews.fixture.cjs')
const { emptyPortfolio } = require('./portfolio.fixture.cjs')
const { availabilityRead, availabilityPerson } = require('./availability.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const projectId = '00000000-0000-0000-0000-000000000101'
const project = projectDetail({ id: projectId, projectNumber: 'SYN-101', name: 'Synthetic project', client: 'Synthetic client', pmName: 'Synthetic PM' })
const rows = [projectRow({ id: projectId, projectNumber: 'SYN-101', name: 'Synthetic project', status: 'Active' }), projectRow({ id: '00000000-0000-0000-0000-000000000102', projectNumber: 'SYN-102', name: 'Setup project', status: 'Setup' })]

;(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' })
    page.setDefaultTimeout(10000)
    const expectedDeniedAvailability = new Set()
    const errors = []; page.on('pageerror', error => errors.push(error)); page.on('console', message => {
      if (message.type() !== 'error') return
      // Only the explicitly exercised denied read may emit its expected network403.
      if (expectedDeniedAvailability.has(message.location().url) && message.text().includes('403')) return
      errors.push(new Error(message.text()))
    })
    const portfolioRequests = []
    const projectRequests = [], availabilityRequests = []
    let shownProjects = rows
    let holdProjects = false
    const releaseProjects = []
    let enabled = false
    let homeOverview = false
    let readOnly = false
    await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'sam@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    await page.route('**/api/**', async route => {
      const request = route.request(); const pathname = new URL(request.url()).pathname.replace('/api/v1/', '')
      if (pathname === 'config') return route.fulfill({ json: clientConfig() })
      if (pathname === 'me') return route.fulfill({ json: meView({ id: '00000000-0000-0000-0000-000000000002', displayName: 'Alex Chen', email: 'alex@hub.test', capabilities: { workload: !readOnly, portfolio: !readOnly, readOnly }, flags: { visual_system: enabled, home_overview: homeOverview } }) })
      if (pathname === 'reference') return route.fulfill({ json: referenceData() })
      if (pathname === 'workspaces') return route.fulfill({ json: [] })
      if (pathname === 'views') return route.fulfill({ json: viewList() })
      if (pathname === 'me/notifications/pulse' || pathname === 'me/notifications/unread-count') return route.fulfill({ json: {} })
      if (pathname === 'home') return route.fulfill({ json: homeRead({ projectId }) })
      if (pathname === 'projects') { projectRequests.push(new URL(request.url())); if (holdProjects) await new Promise(resolve => releaseProjects.push(resolve)); return route.fulfill({ json: projectList(shownProjects) }) }
      if (pathname === 'portfolio') { portfolioRequests.push(new URL(request.url())); return route.fulfill({ json: emptyPortfolio() }) }
      if (pathname === `projects/${project.projectNumber}` || pathname === `projects/${project.id}`) return route.fulfill({ json: project })
      if (pathname === `projects/${project.id}/changes/options`) return route.fulfill({ json: coordinationOptions({}) })
      if (pathname === `projects/${project.id}/reviews`) return route.fulfill({ json: reviewPage([]) })
      if (pathname === `projects/${project.projectNumber}/reviews`) return route.fulfill({ json: reviewPage([]) })
      if (pathname === `projects/${project.id}/issues`) return route.fulfill({ json: { items: [], totalCount: 0, page: 1, pageSize: 50 } })
      if (pathname === 'planning/grid') return route.fulfill({ json: planningGrid() })
      if (pathname === 'planning/availability') {
        availabilityRequests.push(new URL(request.url()))
        if (readOnly) {
          expectedDeniedAvailability.add(request.url())
          return route.fulfill({ status: 403, json: { title: 'Forbidden', status: 403, detail: 'Synthetic ReadOnly availability denial' } })
        }
        return route.fulfill({ json: availabilityRead({ people: [availabilityPerson({ id: '00000000-0000-0000-0000-000000000777', displayName: 'Synthetic visible colleague' })] }) })
      }
      if (request.method() !== 'GET') return route.fulfill({ status: 204, body: '' })
      return route.fulfill({ json: {} })
    })

    await page.goto(`${base}/home?projects=alpha%2Cbeta`); await page.locator('[data-workspace-tabs]').waitFor()
    assert.equal(await page.locator('[data-workspace-tabs]').getAttribute('data-visual-tabs'), 'legacy')
    assert.equal(await page.locator('[data-workspace-tabs] select').count(), 1)
    await page.goto(`${base}/projects/${project.projectNumber}/reviews`); await page.locator('[data-visual-project-tabs]').waitFor().catch(async e => { console.error('legacy project errors', errors.map(x => x.message)); console.error((await page.locator('body').innerText()).slice(0,1000)); throw e })
    assert.equal(await page.locator('[data-visual-project-tabs] button').filter({ hasText: 'More' }).count(), 1)
    await page.goto(`${base}/projects/${project.projectNumber}/vacation`)
    await page.getByRole('heading', { name: 'Vacation & Coverage', exact: true }).waitFor()
    assert.equal(await page.getByText('Availability for people you can view across projects.', { exact: true }).count(), 0, 'off embedded subtitle remains unchanged')
    assert.equal(await page.getByRole('navigation', { name: 'Breadcrumb', exact: true }).getByText('Vacation & Coverage', { exact: true }).count(), 0, 'off breadcrumb keeps its prior inventory')
    assert.equal(await page.locator('[data-visual-project-tabs] a[aria-current]').count(), 0, 'off project grouping is unchanged on the old alias')

    enabled = true
    await page.goto(`${base}/home?projects=alpha%2Cbeta`); await page.reload()
    const workspace = page.locator('[data-workspace-tabs]'); await workspace.waitFor()
    assert.deepEqual(await workspace.locator('nav a').allTextContents(), ['Dashboard', 'Board', 'List', 'Timeline', 'Calendar', 'Files', 'Team', 'Workload'])
    assert.equal(await workspace.locator('a[aria-current="page"]').getAttribute('href'), '/home?projects=alpha%2Cbeta')
    assert.equal(await workspace.getByRole('button', { name: /copy link/i }).count(), 1)
    assert.equal(await workspace.locator('nav').evaluate(el => getComputedStyle(el).overflowX), 'auto')

    await page.goto(`${base}/projects/${project.projectNumber}/reviews`)
    const projectTabs = page.locator('[data-visual-project-tabs]'); await projectTabs.waitFor().catch(async e => { console.error('visual project errors', errors.map(x => x.message)); console.error((await page.locator('body').innerText()).slice(0,1000)); throw e })
    assert.deepEqual((await projectTabs.locator('.visual-project-tabs-v2__primary a').allTextContents()).slice(0, 6), ['Overview', 'Work', 'Deliverables', 'Coordination', 'People', 'Files'])
    assert.deepEqual((await projectTabs.locator('.visual-project-tabs-v2__primary a').allTextContents()).slice(-2), ['Activity', 'Settings'])
    assert.equal(await projectTabs.locator('.visual-project-tabs-v2__secondary a[aria-current="page"]').textContent(), 'Reviews')
    const quietGeometry = await projectTabs.locator('.visual-project-tabs-v2__primary').evaluate(nav => {
      const links = [...nav.querySelectorAll('a')]
      return { right: nav.getBoundingClientRect().right, filesRight: links[5].getBoundingClientRect().right, activityLeft: links[6].getBoundingClientRect().left, settingsRight: links[7].getBoundingClientRect().right }
    })
    assert.ok(Math.abs(quietGeometry.right - quietGeometry.settingsRight) <= 1, 'quiet Settings ends at the right edge of the desktop tab row')
    assert.ok(quietGeometry.activityLeft > quietGeometry.filesRight + 16, 'quiet links form a separate right-side cluster')
    // The project tab row is exercised on a real Reviews route; its links retain
    // every existing deep-link target. Data-heavy destination pages have their
    // own fixture contracts and are intentionally not remounted here.
    const projectHrefs = await projectTabs.locator('a').evaluateAll(as => as.map(a => a.getAttribute('href')))
    for (const path of ['dashboard', 'files', 'settings'])
      assert.ok(projectHrefs.some(href => href?.split('?')[0].endsWith(`/${path}`)), `deep link target ${path}: ${projectHrefs.join(', ')}`)

    await page.goto(`${base}/projects/${project.projectNumber}/vacation?year=2026&month=10`)
    await page.getByRole('heading', { name: 'Vacation & Coverage', exact: true }).waitFor()
    await page.getByText('Availability for people you can view across projects.', { exact: true }).waitFor()
    await page.getByText('Synthetic visible colleague', { exact: true }).first().waitFor()
    assert.equal(await projectTabs.locator('.visual-project-tabs-v2__primary a[aria-current="true"]').textContent(), 'People')
    assert.equal(await projectTabs.locator('.visual-project-tabs-v2__secondary a').filter({ hasText: 'Vacation' }).count(), 0, 'legacy alias does not become a project sibling')
    assert.equal(await page.getByRole('navigation', { name: 'Breadcrumb', exact: true }).locator('[aria-current="page"]').textContent(), 'Vacation & Coverage')
    assert.equal(await page.locator('.visual-sidebar a[aria-current="page"]').getAttribute('href'), '/projects')
    assert.ok(availabilityRequests.length > 0)
    for (const request of availabilityRequests) {
      assert.equal(request.searchParams.has('projectId'), false, 'embedding does not invent a project people filter')
      assert.equal(request.searchParams.has('projects'), false)
    }
    readOnly = true
    await page.goto(`${base}/projects/${project.projectNumber}/vacation?year=2026&month=10`)
    await page.getByRole('alert').first().waitFor()
    assert.equal(expectedDeniedAvailability.size, 1, 'denied embedded scenario actually exercises the mocked403 read')
    assert.equal(await page.getByRole('navigation', { name: 'Breadcrumb', exact: true }).getByText('Vacation & Coverage', { exact: true }).count(), 0, 'ReadOnly denial does not append an allowed-only breadcrumb alias')
    assert.equal(await projectTabs.locator('.visual-project-tabs-v2__primary a[aria-current]').count(), 0, 'denied embedded Vacation selects no project group')
    assert.equal(await projectTabs.locator('.visual-project-tabs-v2__primary').getByRole('link', { name: 'People', exact: true }).getAttribute('aria-current'), null)
    assert.equal(await page.locator('.visual-sidebar a[aria-current="page"]').getAttribute('href'), '/projects', 'permitted project shell keeps its Projects parent around the denied child')
    assert.equal(await page.locator('.visual-sidebar a[href="/planner"]').count(), 0, 'ReadOnly denial does not expose Resources')
    readOnly = false
    await page.goto(`${base}/vacation`)
    await page.getByText('Plan with HR-approved leave, office holidays and real availability.', { exact: true }).waitFor()
    assert.equal(await page.getByText('Availability for people you can view across projects.', { exact: true }).count(), 0, 'global Vacation keeps its own subtitle')

    homeOverview = true
    shownProjects = Array.from({ length: 52 }, (_, i) => projectRow({ id: `00000000-0000-0000-0000-${String(i + 201).padStart(12, '0')}`, projectNumber: `SCOPE-${i + 1}`, name: `Synthetic scoped project ${i + 1}`, status: 'Active' }))
    await page.goto(`${base}/projects?homePreset=ProjectManager&homeProjects=mine&mine=false&status=Active`)
    await page.locator('[data-area-tabs]').waitFor()
    const assertScalarScope = href => {
      const url = new URL(href, base)
      assert.equal(url.pathname, '/projects')
      assert.equal(url.searchParams.get('homePreset'), 'ProjectManager')
      assert.equal(url.searchParams.get('homeProjects'), 'mine')
      assert.equal(url.searchParams.get('mine'), 'false')
      assert.equal(url.searchParams.get('status'), 'Active')
      assert.equal(url.searchParams.has('ids'), false, '52-row cohort stays scalar instead of an expanded/truncated ID URL')
      return url
    }
    assertScalarScope(await page.locator('[data-area-tabs]').getByRole('link', { name: 'Boards', exact: true }).getAttribute('href'))
    const portfolioHref = new URL(await page.locator('[data-area-tabs]').getByRole('link', { name: 'Portfolio', exact: true }).getAttribute('href'), base)
    assert.equal(portfolioHref.searchParams.has('homePreset'), false, 'no unsupported Portfolio preset promise')
    assert.equal(portfolioHref.searchParams.has('homeProjects'), false)
    await page.locator('[data-area-tabs]').getByRole('link', { name: 'Boards', exact: true }).click()
    await page.locator('[data-project-board]').waitFor()
    assert.equal(assertScalarScope(page.url()).searchParams.get('view'), 'board')
    assertScalarScope(await page.locator('[data-area-tabs]').getByRole('link', { name: 'List', exact: true }).getAttribute('href'))
    await page.locator('[data-area-tabs]').getByRole('link', { name: 'List', exact: true }).click()
    await page.waitForURL(url => !url.searchParams.has('view'))
    assertScalarScope(page.url())
    assert.ok(projectRequests.some(url => url.searchParams.get('HomePreset') === 'ProjectManager' && url.searchParams.get('HomeProjects') === 'mine'), 'supported scalar scope reaches the project reader')
    homeOverview = false; shownProjects = rows
    await page.goto(`${base}/projects?homePreset=ProjectManager&homeProjects=mine`)
    await page.locator('[data-area-tabs]').waitFor()
    const unsupported = new URL(await page.locator('[data-area-tabs]').getByRole('link', { name: 'Boards', exact: true }).getAttribute('href'), base)
    assert.equal(unsupported.searchParams.has('homePreset'), false, 'overview-off tab does not carry unsupported Home parameters')
    assert.equal(unsupported.searchParams.has('homeProjects'), false)

    await page.goto(`${base}/projects?status=Active&mine=false&ids=${projectId}&computedHealth=Green&view=board`); await page.locator('[data-area-tabs]').waitFor()
    assert.equal(await page.locator('[data-area-tabs] a[aria-current="page"]').textContent(), 'Boards')
    assert.deepEqual([...new URL(await page.locator('[data-area-tabs] a').nth(1).getAttribute('href'), base).searchParams].sort(), [...new URL(`/projects?status=Active&mine=false&ids=${projectId}&computedHealth=Green&view=board`, base).searchParams].sort())
    await page.locator('[data-project-board]').waitFor()
    await page.goto(`${base}/projects?status=Active&mine=false&ids=${projectId}`); assert.equal(await page.locator('[data-area-tabs] a[aria-current="page"]').textContent(), 'List')
    await page.goto(`${base}/projects?ids=${projectId}&starred=true&priority=High`)
    await page.getByRole('link', { name: 'Portfolio', exact: true }).click()
    await page.waitForURL(/\/portfolio/)
    await page.waitForFunction(() => document.querySelectorAll('[data-area-tabs] a[aria-current="page"]').length === 1)
    assert.equal(new URL(page.url()).pathname, '/portfolio')
    assert.equal(await page.locator('[data-area-tabs] a[aria-current="page"]').textContent(), 'Portfolio')
    assert.ok(portfolioRequests.some(url => url.searchParams.get('ids') === projectId && url.searchParams.get('starred') === 'true' && url.searchParams.get('priority') === 'High'), 'Portfolio receives project filters')
    await page.getByRole('link', { name: 'Boards', exact: true }).click(); await page.waitForURL(/\/projects\?.*view=board/)

    shownProjects = []
    await page.goto(`${base}/projects?view=board&q=nothing`)
    await page.getByText('No projects to show', { exact: true }).waitFor()
    assert.ok(await page.getByRole('button', { name: 'Clear filters', exact: true }).count() > 0, 'empty board offers filter clearing')
    await page.goto(`${base}/projects?view=board&clientId=${projectId}&pmId=${projectId}&phaseId=${projectId}&projectTypeId=${projectId}&submissionWithinDays=7&starred=true&priority=High`)
    await page.getByText('No projects to show', { exact: true }).waitFor()
    await page.getByRole('button', { name: 'Clear filters', exact: true }).first().click()
    await page.waitForFunction(() => !['clientId', 'pmId', 'phaseId', 'projectTypeId', 'submissionWithinDays', 'starred', 'priority'].some(key => new URL(location.href).searchParams.has(key)))
    assert.equal(new URL(page.url()).searchParams.get('view'), 'board', 'clear transferred filters retains board layout')
    holdProjects = true
    await page.goto(`${base}/projects?view=board&q=pending`)
    await page.getByRole('status', { name: 'Loading' }).waitFor()
    holdProjects = false; shownProjects = rows
    releaseProjects.splice(0).forEach(resolve => resolve())
    await page.locator('[data-project-board]').waitFor()

    await page.goto(`${base}/planner?projectId=${projectId}`); await page.locator('[data-area-tabs]').waitFor()
    assert.deepEqual(await page.locator('[data-area-tabs] a').allTextContents(), ['Team Planner', 'Workload', 'Vacation & Coverage'])
    assert.match(await page.locator('[data-area-tabs] a').first().getAttribute('href'), /projectId=/)

    await page.setViewportSize({ width: 375, height: 812 })
    await page.goto(`${base}/projects/${project.projectNumber}/reviews`)
    const quietSettings = projectTabs.locator('.visual-project-tabs-v2__primary').getByRole('link', { name: 'Settings', exact: true })
    await quietSettings.focus()
    await quietSettings.scrollIntoViewIfNeeded()
    assert.equal(await quietSettings.evaluate(el => { const box = el.getBoundingClientRect(), row = el.closest('nav').getBoundingClientRect(); return box.left >= row.left - 1 && box.right <= row.right + 1 }), true, 'quiet link remains reachable in the phone tab scroll region')
    await page.keyboard.press('Enter')
    await page.waitForURL(new RegExp(`/projects/${project.projectNumber}/settings$`))
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), 375, 'quiet-link phone navigation does not widen the page')
    await page.goto(`${base}/home?projects=alpha%2Cbeta`)
    await page.locator('[data-workspace-tabs] nav').waitFor()
    assert.equal(await page.locator('[data-workspace-tabs] nav').evaluate(el => getComputedStyle(el).overflowX), 'auto')
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), 375, 'phone page has no horizontal overflow')
    assert.equal(errors.length, 0, errors.map(error => error.stack ?? error.message).join('\n'))
    console.log('PASS: legacy parity, workspace/parent tabs, scalar Home List-Boards scope, embedded Vacation disclosure/alias and desktop/phone quiet links.')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
