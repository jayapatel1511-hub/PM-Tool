// Actual Shell + ProjectLayout against the production route registry, with explicit leaf sentinels.
// This proves navigation binding/registry coverage, not destination-page loading or API permissions.
const assert = require('node:assert/strict')
const { newMockPage, taskDetail } = require('./platform-fixtures.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')
const { projectDetail } = require('./projects.fixture.cjs')
const { profiles, cases, projectNumber, taskId } = require('./visual-route-matrix.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local development source harness')
const project = projectDetail({ id: '00000000-0000-0000-0000-000000000901', projectNumber, name: 'Synthetic navigation project', client: 'Synthetic client', pmName: 'Synthetic PM' })

;(async () => {
  const { page, browser } = await newMockPage(base)
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  try {
    await page.goto(`${base}/projects`)
    await page.getByRole('heading', { name: 'Projects', exact: true }).waitFor()
    const patterns = await page.evaluate(async ({ project, taskDetail, taskId, me, reference }) => {
      const dependency = name => {
        const url = performance.getEntriesByType('resource').map(item => item.name).find(value => new URL(value).pathname.endsWith(`/node_modules/.vite/deps/${name}`))
        if (!url) throw new Error(`Development app did not load ${name}`)
        return url
      }
      const React = await import(dependency('react.js')), ReactDOM = await import(dependency('react-dom_client.js'))
      const { createMemoryRouter, RouterProvider, useLocation } = await import(dependency('react-router.js'))
      const { QueryClient, QueryClientProvider } = await import(dependency('@tanstack_react-query.js'))
      const { Ctx } = await import('/src/lib/auth-context.ts')
      const { TooltipProvider } = await import('/src/components/ui/tooltip.tsx')
      const { Shell } = await import('/src/app/Shell.tsx')
      const { routes } = await import('/src/app/routes.tsx')
      const R = React.default ?? React, client = ReactDOM.default ?? ReactDOM
      const registry = []
      const clone = (nodes, prefix = '') => nodes.map(node => {
        const full = [prefix, node.path].filter(Boolean).join('/')
        registry.push(full)
        if (node.children) return { path: node.path, element: node.element, children: clone(node.children, full) }
        if (node.index) return { index: true, element: node.element }
        function Leaf() { const location = useLocation(); return R.createElement('div', { 'data-route-shell-probe': full, 'data-path': location.pathname }, 'Navigation probe only; destination content is not mounted.') }
        return { path: node.path, element: R.createElement(Leaf) }
      })
      const router = createMemoryRouter([{ element: R.createElement(Shell), children: clone(routes) }], { initialEntries: ['/home'] })
      const query = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, retry: false, refetchOnWindowFocus: false } } })
      query.setQueryData(['project', project.projectNumber], project); query.setQueryData(['project', project.id], project)
      query.setQueryData(['task', taskId], taskDetail); query.setQueryData(['reference'], reference)
      query.setQueryData(['workspaces'], [])
      const originalVisual = document.documentElement.getAttribute('data-visual')
      const root = document.createElement('div'); root.id = 'visual-route-matrix'
      root.style.cssText = 'position:fixed;inset:0;z-index:45;overflow:hidden;background:var(--page)'
      document.body.append(root)
      const renderer = client.createRoot(root)
      const setProfile = (profile, visual = true) => renderer.render(R.createElement(Ctx.Provider, { value: { me: { ...me, displayName: profile.name, systemRoles: profile.roles, isTemplateEditor: !!profile.capabilities.templates && !profile.capabilities.admin, capabilities: { ...me.capabilities, ...profile.capabilities }, flags: { visual_system: visual, org_structure: true, task_pickup: true, verifiable_log: true, admin_review: true } }, signOut: () => {} } },
        R.createElement(QueryClientProvider, { client: query }, R.createElement(TooltipProvider, null, R.createElement('div', { 'data-nav-profile': profile.name, 'data-nav-visual': String(visual) }, R.createElement(RouterProvider, { router }))))))
      window.__routeMatrix = { profile: setProfile, go: (path, options) => router.navigate(path, options), location: () => router.state.location, dispose: () => { renderer.unmount(); router.dispose(); query.clear(); root.remove(); if (originalVisual === null) delete document.documentElement.dataset.visual; else document.documentElement.dataset.visual = originalVisual } }
      return [...new Set(registry)].sort()
    }, { project, taskDetail, taskId, me: meView({ id: '00000000-0000-0000-0000-000000000002', displayName: 'Synthetic route viewer', email: 'route@hub.test' }), reference: referenceData() })
    assert.deepEqual(patterns, [...new Set(cases.map(item => item.pattern))].sort(), 'every current registered route pattern is inventoried; additions cannot silently escape this matrix')
    const frame = page.locator('#visual-route-matrix'), rail = frame.locator('.visual-sidebar')
    let observations = 0, utilityObservations = 0
    for (const profile of profiles) {
      await page.evaluate(profile => window.__routeMatrix.profile(profile), profile)
      await page.waitForFunction(name => document.querySelector('#visual-route-matrix [data-nav-profile]')?.getAttribute('data-nav-profile') === name, profile.name)
      const hrefs = await rail.locator('ul a[href]').evaluateAll(xs => xs.map(x => x.getAttribute('href')))
      assert.deepEqual(hrefs, profile.hrefs, `${profile.name}: exact main and foot order from supplied capabilities`)
      assert.equal(new Set(hrefs).size, hrefs.length, 'aliases never add duplicate rows')
      assert.equal(await rail.locator('ul li:empty').count(), 0, 'hidden capabilities leave no empty row')
      assert.equal(await rail.getByRole('button', { name: 'More', exact: true }).count(), 0)
      for (const item of cases) {
        if (item.capability && !profile.capabilities[item.capability] || item.vacation && profile.capabilities.readOnly) continue
        await page.evaluate(path => window.__routeMatrix.go(path), item.path)
        const path = item.settled ?? item.path
        await page.waitForFunction(path => document.querySelector('#visual-route-matrix [data-route-shell-probe]')?.getAttribute('data-path') === path, path)
        const expected = item.vacation && !item.group ? profile.capabilities.workload ? '/planner' : '/time' : item.parent
        const selected = rail.locator('ul a[aria-current="page"]')
        assert.equal(await selected.count(), expected ? 1 : 0, `${profile.name} ${item.path}: expected parent count`)
        if (expected) assert.equal(await selected.getAttribute('href'), expected, `${profile.name} ${item.path}`)
        if (item.utilityNoParent) utilityObservations++
        if (item.group) {
          const current = frame.locator('.visual-project-tabs-v2__primary a[aria-current]')
          assert.equal(await current.count(), 1, `${item.path}: one selected group or quiet destination`)
          assert.equal(await current.textContent(), item.group)
          if (item.pattern.endsWith('/team')) {
            const workload = frame.locator('.visual-project-tabs-v2__secondary a[href^="/workload?"]')
            assert.equal(await workload.count(), profile.capabilities.workload ? 1 : 0)
            if (profile.capabilities.workload) assert.equal(new URL(await workload.getAttribute('href'), base).searchParams.get('projectId'), project.id)
          }
        }
        observations++
      }
    }
    // Keep the real production index element: query/hash/return state and replace semantics
    // would be meaningless if the fixture supplied its own preserving Navigate.
    for (const visual of [false, true]) {
      await page.evaluate(({ profile, visual }) => window.__routeMatrix.profile(profile, visual), { profile: profiles[0], visual })
      await page.waitForFunction(visual => document.querySelector('#visual-route-matrix [data-nav-visual]')?.getAttribute('data-nav-visual') === String(visual), visual)
      const sourceState = { from: '/home?projects=mine', sourceFilters: { status: 'Active', page: 2 }, focusId: 'synthetic-project-row' }
      await page.evaluate(() => window.__routeMatrix.go('/home?origin=route-probe'))
      await page.evaluate(({ path, state }) => window.__routeMatrix.go(path, { state }), { path: `/projects/${projectNumber}?status=Active&sort=name&page=2#details`, state: sourceState })
      await page.waitForFunction(path => document.querySelector('#visual-route-matrix [data-route-shell-probe]')?.getAttribute('data-path') === path, `/projects/${projectNumber}/dashboard`)
      const location = await page.evaluate(() => window.__routeMatrix.location())
      assert.equal(location.search, '?status=Active&sort=name&page=2')
      assert.equal(location.hash, '#details')
      assert.deepEqual(location.state, sourceState, `index preserves opaque source-return state when visual=${visual}`)
      await page.evaluate(() => window.__routeMatrix.go(-1))
      await page.waitForFunction(() => window.__routeMatrix.location().pathname === '/home')
      assert.equal((await page.evaluate(() => window.__routeMatrix.location())).search, '?origin=route-probe', 'replace leaves the original source as the Back destination')
    }
    await page.setViewportSize({ width: 375, height: 812 })
    for (const profile of profiles) {
      await page.evaluate(profile => window.__routeMatrix.profile(profile), profile)
      await page.evaluate(() => window.__routeMatrix.go('/home'))
      const menu = frame.getByRole('button', { name: 'Main navigation', exact: true })
      await menu.waitFor()
      // Wait for the visible rail's capability update even though it is hidden at phone widths.
      await page.waitForFunction(expected => JSON.stringify([...document.querySelectorAll('#visual-route-matrix .visual-sidebar ul a[href]')].map(x => x.getAttribute('href'))) === JSON.stringify(expected), profile.hrefs)
      await menu.click()
      const dialog = page.getByRole('dialog')
      await dialog.waitFor()
      assert.deepEqual(await dialog.locator('ul a[href]').evaluateAll(xs => xs.map(x => x.getAttribute('href'))), profile.hrefs, `${profile.name}: same exact permissioned drawer list`)
      assert.equal(await frame.locator('nav.fixed.bottom-0 a[href="/my-work"]').count(), 1)
      assert.equal(await frame.locator('nav.fixed.bottom-0 a[href="/notifications"]').count(), 1)
      await page.keyboard.press('Escape')
      await dialog.waitFor({ state: 'hidden' })
      await page.waitForFunction(() => document.activeElement === document.querySelector('#visual-route-matrix button[aria-label="Main navigation"]'))
      assert.equal(await frame.evaluate(el => el.scrollWidth <= el.clientWidth + 1), true)
    }
    assert.equal(errors.length, 0, errors.map(error => error.stack ?? error.message).join('\n'))
    console.log(`PASS: ${observations} source route-shell/group observations and9 capability/drawer profiles. ${utilityObservations} verify the confirmed utility no-parent policy; actual destination/API admission remain separate.`)
  } finally {
    await page.evaluate(() => window.__routeMatrix?.dispose()).catch(() => {})
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
