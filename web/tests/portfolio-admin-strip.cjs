// Packet 051 S10 (FR-ADR-07, FR-ADR-08): the Portfolio admin strip shows the last Admin review's date and counts to
// Executives only while flag.admin_review is on; otherwise it is absent and never requested.
const { chromium } = require('playwright')
const assert = require('node:assert/strict')
const { startPreview, mockPlatformApi } = require('./platform-fixtures.cjs')

const port = Number(process.env.ADMIN_STRIP_TEST_PORT || '7266')
const personId = '00000000-0000-0000-0000-000000000002'
const strip = { reviewedAt: '2026-10-14T15:00:00Z', checks: [{ key: 'inactiveOnTeams', title: 'Inactive users still on teams', count: 2 }, { key: 'noManager', title: 'Users without a manager', count: 1 }] }

;(async () => {
  const { base, child } = await startPreview(port)
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
  page.setDefaultTimeout(10000)
  const fallback = mockPlatformApi(page)
  const errors = [], stripGets = []
  let flag = true, roles = ['Executive'], admin = false, answer = strip
  await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'alex@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
  page.on('pageerror', e => errors.push(e.message))
  await page.route('**/api/**', async route => {
    const p = new URL(route.request().url()).pathname.replace('/api/v1/', ''); let data
    if (p === 'config') data = { authMode: 'Development', entra: {} }
    else if (p === 'me') data = { id: personId, displayName: 'Alex Chen', email: 'alex@hub.test', roles: [], systemRoles: roles, capabilities: { createProject: false, createTask: true, portfolio: true, workload: false, staff: false, allStaff: false, admin, templates: false, readOnly: false, directReports: 0 }, settings: { today: '2026-10-18', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8, restrictedProjectsEnabled: true }, preferences: { denseRows: true }, flags: { admin_review: flag } }
    else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }
    else if (p === 'me/notifications/pulse') data = { stamp: '0' }
    else if (p === 'reference') data = { disciplines: [], clients: [], offices: [], phases: [], projectTypes: [], limits: { roleLabel: 80 } }
    else if (p === 'organisation') data = { includeIndirectReports: false, units: [] }
    else if (p === 'workspaces') data = []
    else if (p === 'views') data = { views: [], canShare: false }
    else if (p === 'portfolio') data = { ownOnly: false, tiles: { active: 0, red: 0, yellow: 0, green: 0, submissions14: 0, overdueDecisions: 0 }, projects: [] }
    else if (p === 'portfolio/admin-review') { stripGets.push(p); data = answer }
    else { await route.fallback(); return }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) })
  })
  const open = async () => { await page.goto(`${base}/portfolio`); await page.getByRole('heading', { name: 'Portfolio', exact: true }).waitFor(); await page.getByText('No projects match these filters.').waitFor() }
  const stripBox = page.locator('[data-admin-review-strip]')
  try {
    // Executive, flag on: the date and every recorded count, as plain figures.
    await open(); await stripBox.waitFor()
    const text = await stripBox.innerText()
    assert.match(text, /Admin review/); assert.match(text, /Last recorded 2026-10-1[45]/)
    assert.match(text, /Inactive users still on teams\s*2/); assert.match(text, /Users without a manager\s*1/)
    assert.equal(await stripBox.getByRole('link').count(), 0, 'the strip opens no list')
    await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
    const axe = await page.evaluate(async () => (await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } })).violations.map(v => v.id))
    assert.deepEqual(axe, [])

    // Nothing recorded yet.
    answer = { reviewedAt: null, checks: [] }
    await open(); await stripBox.getByText('None recorded yet').waitFor()

    // An Admin sees it too; a Supervisor, or the flag off, shows nothing and asks for nothing.
    roles = ['Admin']; admin = true; answer = strip
    await open(); await stripBox.waitFor()
    const before = stripGets.length
    roles = ['Supervisor']; admin = false
    await open(); assert.equal(await stripBox.count(), 0)
    roles = ['Executive']; flag = false
    await open(); assert.equal(await stripBox.count(), 0)
    assert.equal(stripGets.length, before, 'no request for the strip')
    assert.deepEqual(errors, []); assert.deepEqual(fallback.unknownGets, [])
    console.log(JSON.stringify({ scope: 'Chromium mocked portfolio admin strip', executive: true, none: true, admin: true, supervisorAbsent: true, flagOffAbsent: true, axe, errors }))
  } finally { await browser.close(); child.kill() }
})().catch(e => { console.error(e); process.exitCode = 1 })
