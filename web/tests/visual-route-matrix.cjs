// Actual Shell + ProjectLayout against the production route registry, with explicit leaf sentinels, for every
// registered route x role, flags on and off, at 1440 and 375 px (packet 052 S07/S08: AC-VSN-08, -09, -10, -12).
// This proves navigation binding/registry coverage, not destination-page loading or API permissions.
// TUESDAY_ROUTE_MATRIX_OUT=<file> writes the route x role highlight matrix as JSON.
// TUESDAY_ROUTE_MATRIX_FLAGS=off runs only the flags-off sweep, so the same harness can be pointed at the live
// release's source to show the flags-off expectations below are that release's behaviour.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const { newMockPage, taskDetail } = require('./platform-fixtures.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')
const { projectDetail } = require('./projects.fixture.cjs')
const { profiles, cases, projectNumber, taskId, legacyRail } = require('./visual-route-matrix.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local development source harness')
const offOnly = process.env.TUESDAY_ROUTE_MATRIX_FLAGS === 'off'
const project = projectDetail({ id: '00000000-0000-0000-0000-000000000901', projectNumber, name: 'Synthetic navigation project', client: 'Synthetic client', pmName: 'Synthetic PM' })
const widths = [{ width: 1440, height: 1000 }, { width: 375, height: 812 }]

;(async () => {
  const { page, browser } = await newMockPage(base)
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  // ProjectLayout reads the project through the API (its cache key carries viewer and flags), so serve it there.
  await page.route(`**/api/v1/projects/${projectNumber}`, route => route.fulfill({ json: project }))
  try {
    await page.goto(`${base}/projects`)
    await page.getByRole('heading', { name: 'Projects', exact: true }).waitFor()
    const patterns = await page.evaluate(async ({ taskDetail, taskId, me, reference }) => {
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
    }, { taskDetail, taskId, me: meView({ id: '00000000-0000-0000-0000-000000000002', displayName: 'Synthetic route viewer', email: 'route@hub.test' }), reference: referenceData() })
    assert.deepEqual(patterns, [...new Set(cases.map(item => item.pattern))].sort(), 'every current registered route pattern is inventoried; additions cannot silently escape this matrix')
    // Located by accessible name, which the live release and this build share (class hooks are newer).
    const frame = page.locator('#visual-route-matrix'), rail = frame.locator('nav[aria-label="Main navigation"]').first()
    const groupRow = frame.locator('nav[aria-label="Project sections"]'), pageSwitch = frame.locator('nav[aria-label="Project sections"] + nav')
    const bottom = frame.locator('nav.fixed.bottom-0')
    const menu = frame.getByRole('button', { name: 'Main navigation', exact: true })
    const moreButton = rail.locator('button').filter({ hasText: /^More$/ }) // counted even while the rail is hidden on phones
    const settle = path => page.waitForFunction(path => document.querySelector('#visual-route-matrix [data-route-shell-probe]')?.getAttribute('data-path') === path, path)
    const setProfile = async (profile, visual) => {
      await page.evaluate(({ profile, visual }) => window.__routeMatrix.profile(profile, visual), { profile, visual })
      await page.waitForFunction(({ name, visual }) => { const el = document.querySelector('#visual-route-matrix [data-nav-profile]'); return el?.getAttribute('data-nav-profile') === name && el.getAttribute('data-nav-visual') === String(visual) }, { name: profile.name, visual })
    }
    const evidence = { widths: widths.map(w => w.width), profiles: profiles.map(({ name, roles, capabilities }) => ({ name, roles, capabilities })), routes: cases.map(({ pattern, path, settled }) => ({ pattern, path, ...(settled ? { settled } : {}) })), highlight: {}, projectGroup: {}, drawer: {} }
    const counts = { observations: 0, utility: 0, drawer: 0, notPermitted: 0 }

    for (const { width, height } of widths) {
      await page.setViewportSize({ width, height })
      const phone = width < 768
      for (const visual of offOnly ? [false] : [true, false]) {
        const flag = visual ? 'on' : 'off'
        const highlight = ((evidence.highlight[width] ??= {})[flag] = {})
        const groups = ((evidence.projectGroup[width] ??= {})[flag] = {})
        for (const profile of profiles) {
          await setProfile(profile, visual)
          for (const item of cases) {
            const row = (highlight[item.path] ??= {})
            if (item.capability && !profile.capabilities[item.capability] || item.vacation && profile.capabilities.readOnly) { row[profile.name] = 'not permitted'; counts.notPermitted++; continue }
            const label = `${width}px flags ${flag} ${profile.name} ${item.path}`
            await page.evaluate(path => window.__routeMatrix.go(path), item.path)
            await settle(item.settled ?? item.path)
            const shown = await rail.locator('ul a[href]').evaluateAll(xs => xs.map(x => x.getAttribute('href')))
            const current = await rail.locator('ul a[aria-current="page"]').evaluateAll(xs => xs.map(x => x.getAttribute('href')))
            row[profile.name] = current.length ? current.join(' + ') : null
            if (visual) {
              // AC-VSN-08: the exact permission-filtered list and foot group, no More, one selected item per content route.
              assert.deepEqual(shown, profile.hrefs, `${label}: exact main and foot order from supplied capabilities`)
              assert.equal(await moreButton.count(), 0, `${label}: no More group`)
              assert.equal(await rail.locator('ul li:empty').count(), 0, `${label}: hidden capabilities leave no empty row`)
              const expected = item.vacation && !item.group ? profile.capabilities.workload ? '/planner' : '/time' : item.parent
              assert.deepEqual(current, expected ? [expected] : [], `${label}: one selected sidebar item (utility pages and the error page: none)`)
              if (item.utilityNoParent) counts.utility++
            } else {
              // AC-VSN-12: flags off, the release's E2 rail and selection, unchanged.
              const legacy = legacyRail(profile.capabilities, item.legacy !== undefined ? item.legacy : item.parent)
              assert.deepEqual(shown, legacy.hrefs, `${label}: E2 primary items and More disclosure`)
              assert.deepEqual(current, legacy.current, `${label}: E2 selection`)
              assert.equal(await moreButton.count(), 1, `${label}: More stays`)
            }
            if (item.group) {
              // AC-VSN-09: every project route loads and selects its group; a group of several pages shows its switch.
              const selected = await groupRow.locator('a[aria-current]').allTextContents()
              const more = visual ? [] : await groupRow.locator('button').allTextContents()
              const group = selected.length ? selected.join(' + ') : more.find(text => text !== 'More') ?? null
              const second = await pageSwitch.locator('a[aria-current="page"]').evaluateAll(xs => xs.map(x => x.getAttribute('href')))
              groups[item.path] ??= {}; groups[item.path][profile.name] = group
              assert.ok(selected.length <= 1, `${label}: at most one selected group`)
              assert.equal(group, visual || item.legacyGroup === undefined ? item.group : item.legacyGroup, `${label}: project group`)
              assert.deepEqual(second, item.siblings > 1 ? [`/projects/${projectNumber}/${item.child}`] : [], `${label}: second-level switch`)
              if (visual) {
                const quiet = await groupRow.locator('.visual-project-tabs-v2__quiet').allTextContents()
                assert.deepEqual(quiet, ['Activity', 'Settings'], `${label}: Activity and Settings as two quiet links (052-D4)`)
              }
              if (item.pattern.endsWith('/team')) {
                const workload = pageSwitch.locator('a[href^="/workload?"]')
                assert.equal(await workload.count(), profile.capabilities.workload ? 1 : 0)
                if (profile.capabilities.workload) assert.equal(new URL(await workload.getAttribute('href'), base).searchParams.get('projectId'), project.id)
              }
            }
            if (phone) {
              // AC-VSN-10 / 052-D10: the bottom bar stays, nothing scrolls sideways, and the drawer traps and returns focus.
              assert.equal(await bottom.locator('a[href="/my-work"]').count(), 1, `${label}: bottom My Work`)
              assert.equal(await bottom.getByRole('button', { name: 'Search', exact: true }).count(), 1, `${label}: bottom Search`)
              assert.equal(await bottom.locator('a[href="/notifications"]').count(), 1, `${label}: bottom Notifications`)
              assert.equal(await frame.evaluate(el => [el, el.querySelector('main')].every(x => x.scrollWidth <= x.clientWidth + 1)), true, `${label}: no sideways scroll`)
              assert.equal(await menu.count(), visual ? 1 : 0, `${label}: the menu button exists only with the flag`)
              if (visual) {
                await menu.click()
                const dialog = page.getByRole('dialog')
                await dialog.waitFor()
                await page.waitForFunction(() => document.activeElement?.closest('[role="dialog"]'))
                assert.deepEqual(await dialog.locator('ul a[href]').evaluateAll(xs => xs.map(x => x.getAttribute('href'))), profile.hrefs, `${label}: drawer holds the same list and foot group`)
                assert.deepEqual(await dialog.locator('ul a[aria-current="page"]').evaluateAll(xs => xs.map(x => x.getAttribute('href'))), current, `${label}: drawer selects what the rail selects`)
                await page.keyboard.press('Shift+Tab')
                assert.ok(await page.evaluate(() => document.activeElement?.closest('[role="dialog"]')), `${label}: focus stays in the drawer`)
                await page.keyboard.press('Escape')
                await dialog.waitFor({ state: 'hidden' })
                await page.waitForFunction(() => document.activeElement === document.querySelector('#visual-route-matrix button[aria-label="Main navigation"]'))
                ;(evidence.drawer[profile.name] ??= []).push(item.path)
                counts.drawer++
              }
            }
            counts.observations++
          }
        }
      }
    }

    // Keep the real production index element: query/hash/return state and replace semantics
    // would be meaningless if the fixture supplied its own preserving Navigate.
    if (!offOnly) for (const visual of [false, true]) {
      await setProfile(profiles[0], visual)
      const sourceState = { from: '/home?projects=mine', sourceFilters: { status: 'Active', page: 2 }, focusId: 'synthetic-project-row' }
      await page.evaluate(() => window.__routeMatrix.go('/home?origin=route-probe'))
      await page.evaluate(({ path, state }) => window.__routeMatrix.go(path, { state }), { path: `/projects/${projectNumber}?status=Active&sort=name&page=2#details`, state: sourceState })
      await settle(`/projects/${projectNumber}/dashboard`)
      const location = await page.evaluate(() => window.__routeMatrix.location())
      assert.equal(location.search, '?status=Active&sort=name&page=2')
      assert.equal(location.hash, '#details')
      assert.deepEqual(location.state, sourceState, `index preserves opaque source-return state when visual=${visual}`)
      await page.evaluate(() => window.__routeMatrix.go(-1))
      await page.waitForFunction(() => window.__routeMatrix.location().pathname === '/home')
      assert.equal((await page.evaluate(() => window.__routeMatrix.location())).search, '?origin=route-probe', 'replace leaves the original source as the Back destination')
    }
    assert.equal(errors.length, 0, errors.map(error => error.stack ?? error.message).join('\n'))
    if (process.env.TUESDAY_ROUTE_MATRIX_OUT) fs.writeFileSync(process.env.TUESDAY_ROUTE_MATRIX_OUT, `${JSON.stringify({ counts, ...evidence }, null, 2)}\n`)
    console.log(`PASS: ${counts.observations} route x role observations at ${widths.map(w => w.width).join(' and ')} px, flags ${offOnly ? 'off only' : 'on and off'}; ${counts.utility} utility no-parent checks; ${counts.drawer} phone drawer focus trap/return checks; ${counts.notPermitted} not-permitted cells skipped. Destination pages and API admission are covered elsewhere.`)
  } finally {
    await page.evaluate(() => window.__routeMatrix?.dispose()).catch(() => {})
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
