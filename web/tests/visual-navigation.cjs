// Packet 052 S07: the visual rail is role-filtered, route-preserving and dark behind visual_system.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const { availabilityRead } = require('./availability.fixture.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')
const { homeRead } = require('./workspace.fixture.cjs')
const { viewList } = require('./views.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')

const profiles = [
  { name: 'admin-supervisor', capabilities: { admin: true, staff: true, templates: true, workload: true }, flags: { visual_system: true } },
  { name: 'vacation-user', capabilities: {}, flags: { visual_system: true } },
  { name: 'project-manager', capabilities: { workload: true }, flags: { visual_system: true } },
  { name: 'read-only', capabilities: { readOnly: true }, flags: { visual_system: true } },
  { name: 'staff-supervisor', capabilities: { staff: true, workload: true }, flags: { visual_system: true } },
  { name: 'template-user', capabilities: { templates: true }, flags: { visual_system: true } },
  { name: 'legacy-admin', capabilities: { admin: true }, flags: { visual_system: false } },
  { name: 'legacy-staff', capabilities: { staff: true, workload: true }, flags: { visual_system: false } },
  { name: 'legacy-flagged', capabilities: { admin: true }, flags: { visual_system: false, org_structure: true, task_pickup: true } },
]

;(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  try {
    for (const profile of profiles) {
      const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' })
      const errors = []
      page.on('pageerror', error => errors.push(error))
      page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
      page.setDefaultTimeout(8000)
      page.setDefaultNavigationTimeout(10000)
      await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'sam@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
      await page.route('**/api/**', async route => {
        const request = route.request(); const pathname = new URL(request.url()).pathname.replace('/api/v1/', '')
        if (pathname === 'config') return route.fulfill({ json: clientConfig() })
        if (pathname === 'me') return route.fulfill({ json: meView({ id: '00000000-0000-0000-0000-000000000002', displayName: profile.name, email: `${profile.name}@hub.test`, capabilities: profile.capabilities, flags: profile.flags }) })
        if (pathname === 'reference' || pathname === 'workspaces' || pathname === 'views') return route.fulfill({ json: pathname === 'reference' ? referenceData() : pathname === 'views' ? viewList() : [] })
        if (pathname === 'home') return route.fulfill({ json: homeRead({ projectId: '00000000-0000-0000-0000-000000000101' }) })
        if (pathname === 'planning/availability') return route.fulfill({ json: availabilityRead() })
        if (pathname === 'admin/settings') return route.fulfill({ json: [] })
        if (/^users\/[^/]+\/open-work$/.test(pathname)) return route.fulfill({ json: { person: { id: '00000000-0000-0000-0000-000000000201', displayName: 'Synthetic colleague', isActive: true }, items: [] } })
        if (pathname === 'me/notifications/pulse' || pathname === 'me/notifications/unread-count') return route.fulfill({ json: {} })
        return request.method() === 'GET' ? route.fulfill({ status: 200, json: {} }) : route.fulfill({ status: 204, body: '' })
      })
      // Admin Settings is a stable real route whose page does not require Home's
      // large response graph; the production Shell still renders unmodified.
      const initialRoute = profile.name === 'vacation-user' || profile.name === 'project-manager' ? '/vacation' : profile.capabilities.admin ? '/admin/settings' : '/home'
      await page.goto(`${base}${initialRoute}`, { waitUntil: 'domcontentloaded' })
      const rail = page.locator('.visual-sidebar')
      await rail.waitFor()
      const hrefs = () => rail.locator('ul a[href]').evaluateAll(links => links.map(link => link.getAttribute('href')))
      if (profile.flags.visual_system) {
        assert.deepEqual(await hrefs(), ['/my-work', '/home', '/projects', '/tasks', ...(profile.capabilities.workload ? ['/planner'] : []), '/time', '/reports', '/team', '/files', ...(profile.capabilities.staff ? ['/staff'] : []), ...(profile.capabilities.templates ? ['/templates'] : []), ...(profile.capabilities.admin ? ['/admin'] : [])], profile.name)
        assert.equal(await rail.locator('button').filter({ hasText: 'More' }).count(), 0, `${profile.name}: no More group`)
        // The stable settings route lets this harness verify the rendered rail without
        // mounting each data-heavy destination. Route-specific highlighting is covered
        // by the source-owned `also` aliases and the route component tests.
        if (profile.name === 'vacation-user' || profile.name === 'project-manager') {
          await page.getByRole('heading', { name: 'Vacation & Coverage', exact: true }).waitFor()
          assert.equal(await rail.locator('a[aria-current="page"]').count(), 1)
          assert.equal(await rail.locator('a[aria-current="page"]').getAttribute('href'), profile.capabilities.workload ? '/planner' : '/time')
          assert.equal(await rail.locator('a[href="/planner"]').count(), profile.capabilities.workload ? 1 : 0)
        }
        if (profile.capabilities.admin) assert.equal(await rail.locator('a[aria-current="page"]').count(), 1, `${profile.name}: exactly one active row`)
      } else {
        assert.equal(await page.locator('html').getAttribute('data-visual'), null, 'flag-off keeps the visual root off')
        assert.ok(await rail.locator('button').filter({ hasText: 'More' }).count() > 0, 'flag-off retains More')
        if (profile.name === 'legacy-flagged') {
          assert.equal(await rail.locator('a[href="/team"]').count(), 1, 'legacy Team remains available')
        }
        await page.goto(`${base}/vacation`)
        await page.getByRole('heading', { name: 'Vacation & Coverage', exact: true }).waitFor()
        assert.equal(await rail.locator('a[href="/vacation"]').count(), 0, `${profile.name}: baseline BUILT excludes a legacy Vacation row`)
        assert.equal(await rail.locator('a[aria-current="page"]').count(), 0, `${profile.name}: legacy Vacation preserves its unmapped baseline`)
        assert.ok(await rail.locator('button').filter({ hasText: 'More' }).count() > 0, 'off Vacation retains the legacy More disclosure')
        assert.equal(await rail.locator('a[href="/planner"][aria-current]').count(), 0, 'off Vacation does not select Resources')
        assert.equal(await rail.locator('a[href="/time"][aria-current]').count(), 0, 'visual Time fallback does not leak into off navigation')
      }
      // This is a capability/selection probe, not proof of endpoint permission.
      await page.goto(`${base}/people/00000000-0000-0000-0000-000000000201/reassign-work`)
      await page.getByRole('heading', { name: 'Reassign work: Synthetic colleague', exact: true }).waitFor()
      const selected = rail.locator('a[aria-current="page"]')
      assert.equal(await selected.count(), profile.flags.visual_system && profile.capabilities.staff ? 1 : 0, `${profile.name}: exact reassign alias is visual-only and Staff remains hidden when denied`)
      if (profile.flags.visual_system && profile.capabilities.staff) {
        assert.equal(await selected.getAttribute('href'), '/staff')
        for (const path of ['/people/00000000-0000-0000-0000-000000000201', '/people/00000000-0000-0000-0000-000000000201/reassign-work-extra', '/people/00000000-0000-0000-0000-000000000201/reassign-work/extra']) {
          await page.goto(`${base}${path}`)
          await page.getByRole('heading', { name: 'Page not available', exact: true }).waitFor()
          assert.equal(await selected.count(), 0, `nearby path is not a Staff alias: ${path}`)
        }
      }
      assert.equal(errors.length, 0, `${profile.name}: ${errors.map(error => error.stack ?? error.message).join('\n')}`)
      await page.close()
    }
    console.log('PASS: capability rail profiles, Vacation fallback, exact permission-filtered reassign alias and nearby-path/off parity.')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
