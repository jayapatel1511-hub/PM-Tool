// Actual app + labelled synthetic API. Exact band/card edges and uncovered facts,
// not a shared-component stand-in or universal route/role census.
const assert = require('node:assert/strict')
const { newMockPage, project } = require('./platform-fixtures.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')
const { projectDetail, projectRow } = require('./projects.fixture.cjs')
const { dashboardView, dashboardCount: count } = require('./dashboard.fixture.cjs')
const { attentionList } = require('./evaluation.fixture.cjs')
const { homeRead } = require('./workspace.fixture.cjs')
const { emptyPortfolio } = require('./portfolio.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local reviewed app')
const stamp = '2026-11-01T13:00:00Z', viewer = '00000000-0000-0000-0000-000000000002'
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const civil = project.disciplines[0].id
const p = projectDetail({ ...project, permissions: { ...project.permissions, createTaskIn: [civil] }, statusNote: { text: 'Synthetic project status note remains below the counts', authorId: viewer, author: 'Synthetic viewer', at: stamp,
  header: { computedHealth: 'Green', reportedHealth: null, nextMilestone: null, openAttention: 0 } } })
const staff = [1, 2, 3].map(n => ({ id: id(800 + n), displayName: `Synthetic person ${n}`, jobTitle: null, officeId: null, isActive: true, projects: n, roles: { pm: 0, dl: 0, team: 1, reviewer: 0, viewer: 0 },
  open: 1, overdue: 0, blocked: 0, blocking: 0, reviews: 0, reviewsStalled: 0, deliverablesDue: 0, lastActivityAt: null }))
const dashboard = dashboardView({ asOf: stamp,
  tasks: { total: count(3, ''), complete: count(0, 'status=Complete'), inProgress: count(2, 'status=In%20Progress'), review: count(1, 'readyForReview=true'), overdue: count(1, 'overdue=true'), blocked: count(1, 'blocked=true'), waiting: count(0, 'waiting=true'), unassigned: count(0, 'unassigned=true'), backlog: count(0, 'backlog=true') },
  deliverables: { total: count(1, ''), upcoming: count(1, 'indicator=open&dueFrom=2026-11-01&dueTo=2026-11-15'), issued: count(0, 'status=Issued'), atRisk: count(0, 'indicator=atRisk') },
  decisions: { pending: count(1, 'status=Pending'), overdue: count(0, 'indicator=overdue') }, registers: { issuesOpen: count(1, 'indicator=open'), issuesHigh: count(1, 'indicator=open&severity=High'), risksHigh: count(1, 'indicator=open&severity=High') },
  staffing: { state: 'NoTarget', target: null, fromDate: '2026-11-01', throughDate: '2026-11-15', allocationCoverage: 'Complete', unattributedConfirmedHours: 0, knownUnattributedConfirmedHours: 0, unavailableUnattributed: 0, disciplines: [], asOf: stamp } })
const portfolioRows = [0, 1, 2].map(n => ({ ...projectRow({ id: n ? id(850 + n) : p.id, projectNumber: n ? `SYN-${n}` : p.projectNumber, name: `Synthetic portfolio project ${n}`, computedHealth: ['Red', 'Yellow', 'Green'][n] }), trend: [] }))
const portfolio = { ...emptyPortfolio(), asOf: stamp, projects: portfolioRows, tiles: { active: 3, red: 1, yellow: 1, green: 1, submissions14: 0, overdueDecisions: 0 },
  cardFacts: Object.fromEntries(['active', 'red', 'yellow', 'green', 'submissions14', 'overdueDecisions'].map(key => [key, { projects: key === 'active' ? 3 : ['submissions14', 'overdueDecisions'].includes(key) ? 0 : 1, clients: 1 }])) }

;(async () => {
  const { page, browser, mock } = await newMockPage(base, { events: false })
  const errors = [], writes = [], results = []
  let visual = true, metricsFirst = true, blockDashboard = false, releaseDashboard, notifyDashboard
  let dashboardStarted = new Promise(resolve => { notifyDashboard = resolve })
  page.setDefaultTimeout(12000)
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  try {
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (!['GET', 'HEAD'].includes(request.method()) && path !== 'me/sign-in') writes.push(`${request.method()} ${path}`)
      if (path === 'me') return route.fulfill({ json: meView({ id: viewer, displayName: 'Synthetic viewer', email: 'overlap@hub.test', systemRoles: ['Supervisor'], capabilities: { staff: true, allStaff: true, portfolio: true, createTask: true },
        settings: { today: '2026-11-01' }, flags: { visual_system: visual, small_ux: true, project_screens: true, project_status_note: true } }) })
      if (path === 'reference') return route.fulfill({ json: referenceData() })
      if (path === `projects/${p.id}` || path === `projects/${p.projectNumber}`) return route.fulfill({ json: p })
      if (path === `projects/${p.id}/dashboard`) {
        notifyDashboard()
        if (blockDashboard) await new Promise(resolve => { releaseDashboard = resolve })
        return route.fulfill({ json: dashboard })
      }
      if (path === `projects/${p.id}/attention`) return route.fulfill({ json: attentionList([]) })
      if (path === 'staff') return route.fulfill({ json: { scope: url.searchParams.get('scope') ?? 'direct', canSeeAll: true, people: staff, asOf: stamp,
        tiles: { staff: 3, assignments: 6, withOverdue: 0, withBlocked: 0, reviewsStalled: 0 } } })
      if (path === 'home') return route.fulfill({ json: homeRead({ projectId: p.id, asOf: stamp, layout: (metricsFirst ? ['metrics'] : ['tasksByStatus', 'metrics']).map(id => ({ id, hidden: false })) }) })
      if (path === 'portfolio') return route.fulfill({ json: portfolio })
      return route.fallback()
    })
    const measure = async (selector, expected, label) => {
      const first = page.locator(selector).first(); await first.waitFor()
      await page.evaluate(async () => { await document.fonts.ready })
      const data = await first.evaluate(async card => {
        const page = card.closest('.visual-page'), band = page.querySelector(':scope > .visual-page-band'), content = page.querySelector(':scope > .visual-page-content')
        const nodes = { page, band, content }
        const snapshot = () => {
          const c = card.getBoundingClientRect(), b = band.getBoundingClientRect()
          const above = [...band.querySelectorAll('h1,h2,.visual-title-block,.visual-page-actions > *,.visual-page-band-tabs')]
          const tabs = document.querySelector('[data-visual-project-tabs]'); if (tabs) above.push(tabs)
          const facts = above.map(el => { const r = el.getBoundingClientRect(); return { text: el.textContent.slice(0, 90), top: r.top, bottom: r.bottom, width: r.width, height: r.height } }).filter(r => r.width > 0 && r.height > 0)
          const matchingRules = [], sheets = []
          const inspectRules = (rules, sheet, context = []) => {
            for (const rule of rules) {
              if (rule.type === CSSRule.MEDIA_RULE && !matchMedia(rule.conditionText).matches) continue
              if (rule.type === CSSRule.SUPPORTS_RULE && !CSS.supports(rule.conditionText)) continue
              if (rule.selectorText && rule.style) {
                const declarations = Array.from(rule.style).filter(name => /^(?:margin|padding)(?:-|$)|^all$/.test(name))
                if (declarations.length) {
                  try {
                    const matched = Object.entries(nodes).filter(([, el]) => el.matches(rule.selectorText)).map(([name]) => name)
                    if (matched.length) matchingRules.push({ sheet, context, selector: rule.selectorText, matched,
                      declarations: declarations.map(name => ({ name, value: rule.style.getPropertyValue(name), important: rule.style.getPropertyPriority(name) === 'important' })) })
                  } catch { /* A diagnostic must not replace the geometry assertion. */ }
                }
              }
              if (rule.cssRules) inspectRules(rule.cssRules, sheet, [...context, rule.cssText.slice(0, rule.cssText.indexOf('{')).trim()])
              if (rule.styleSheet) { try { inspectRules(rule.styleSheet.cssRules, rule.styleSheet.href, [...context, rule.cssText]) } catch { /* Cross-origin import. */ } }
            }
          }
          for (const sheet of document.styleSheets) {
            const media = sheet.media.mediaText, active = !sheet.disabled && (!media || matchMedia(media).matches)
            sheets.push({ href: sheet.href, disabled: sheet.disabled, media, active })
            if (active) { try { inspectRules(sheet.cssRules, sheet.href) } catch { /* Cross-origin styles may not expose their rules. */ } }
          }
          return { overlap: b.bottom - c.top, band: { top: b.top, bottom: b.bottom, background: getComputedStyle(band).backgroundColor }, cardTop: c.top, facts, paddingTop: getComputedStyle(content).paddingTop, marginTop: getComputedStyle(content).marginTop,
            transforms: [getComputedStyle(card).transform, getComputedStyle(content).transform], contained: document.documentElement.scrollWidth <= innerWidth,
            visual: document.documentElement.getAttribute('data-visual'), classes: Object.fromEntries(Object.entries(nodes).map(([name, el]) => [name, el.className])),
            bandPaddingBottom: getComputedStyle(band).paddingBottom, connected: card.isConnected,
            inlineStyles: Object.fromEntries(Object.entries(nodes).map(([name, el]) => [name, el.getAttribute('style')])),
            animations: Object.fromEntries(Object.entries(nodes).map(([name, el]) => [name, el.getAnimations().map(animation => ({ type: animation.constructor.name, playState: animation.playState, currentTime: animation.currentTime, timing: animation.effect?.getTiming() }))])),
            sheets, matchingRules }
        }
        const initial = snapshot()
        await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))
        const afterTwoFrames = snapshot()
        // Even reduced motion uses a nonzero transition duration. Await the
        // actual layout elements, never a desired padding/margin/overlap value.
        const active = [...new Set(Object.values(nodes).flatMap(el => el.getAnimations()))]
          .filter(animation => animation.pending || animation.playState === 'running')
        let readinessTimeout
        try {
          await Promise.race([
            Promise.allSettled(active.map(animation => animation.finished)),
            new Promise((_, reject) => { readinessTimeout = setTimeout(() => reject(new Error(`Page layout animations did not settle: ${JSON.stringify({ initial, afterTwoFrames })}`)), 2000) }),
          ])
        } finally { clearTimeout(readinessTimeout) }
        await new Promise(resolve => requestAnimationFrame(resolve))
        return { ...snapshot(), initial, afterTwoFrames, animationsAwaited: active.length }
      })
      assert.ok(Math.abs(data.overlap - expected) <= 1, `${label}: actual band/card overlap must be ${expected}: ${JSON.stringify(data)}`)
      assert.equal(data.band.background, 'rgb(27, 42, 78)')
      assert.deepEqual(data.transforms, ['none', 'none'], 'flow reservation, not a transform-only proxy')
      assert.equal(data.paddingTop, '0px'); assert.equal(data.marginTop, `-${expected}px`)
      assert.ok(data.facts.every(fact => fact.bottom <= data.cardTop + 1), `${label}: full headings/reference/actions/tabs stay above actual cards: ${JSON.stringify(data)}`)
      assert.equal(data.contained, true)
      await first.focus()
      assert.equal(await first.evaluate(el => document.activeElement === el), true)
      await page.getByRole('tooltip').waitFor()
      await page.keyboard.press('Escape'); await first.blur()
      const compact = ({ matchingRules, sheets, initial, afterTwoFrames, ...geometry }) => geometry
      results.push({ label, width: page.viewportSize().width, ...compact(data), initial: compact(data.initial), afterTwoFrames: compact(data.afterTwoFrames) })
      return first
    }
    for (const width of [1440, 375]) {
      await page.setViewportSize({ width, height: width === 375 ? 812 : 1000 })
      for (const scope of ['all', 'direct']) {
        await page.goto(`${base}/staff?scope=${scope}`)
        await measure('[data-staff-counts] [data-count-card]', width === 1440 ? 72 : 64, `Staff ${scope}`)
        assert.equal(await page.locator('.staff-count-view > .visual-page-content').evaluate(el => el.firstElementChild.hasAttribute('data-staff-counts')), true)
        assert.ok((await page.locator('[data-staff-counts]').boundingBox()).y < (await page.locator('.staff-count-view__filters').boundingBox()).y)
        assert.equal(await page.getByLabel('Office', { exact: true }).count(), 1)
        assert.equal(await page.getByLabel('Discipline', { exact: true }).count(), 1)
        assert.equal(await page.getByRole('checkbox', { name: 'Show inactive', exact: true }).count(), 1)
        if (width === 1440) {
          const top = (await page.locator('#staff-people table tbody tr[data-staff-person]').first().boundingBox()).y
          assert.ok(top <= 399.5, `Staff nonempty ${scope} density must preserve 399.5 and 405: ${top}`)
        } else {
          await page.getByLabel('Office', { exact: true }).focus()
          assert.equal(await page.getByLabel('Office', { exact: true }).evaluate(el => document.activeElement === el), true)
          assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
        }
        assert.deepEqual(await page.locator('[data-staff-counts] .count-card__value').allTextContents(), ['3', '6', '0', '0', '0'])
      }
      await page.goto(`${base}/projects/${p.projectNumber}/dashboard`)
      const dashboardCards = page.locator('[data-dashboard-counts] .home-count-cards [data-count-card]')
      await measure('[data-dashboard-counts] .home-count-cards [data-count-card]', width === 1440 ? 72 : 64, 'Dashboard')
      assert.equal(await dashboardCards.count(), 6)
      assert.equal(await page.locator('[aria-label="Other project counts"] a').count(), 12)
      assert.equal(await dashboardCards.locator('a,button').count(), 0)
      const band = page.locator('.visual-dashboard-band > .visual-page-band')
      assert.equal(await band.locator('.visual-title-block dd').evaluate(el => getComputedStyle(el).color), 'rgb(255, 255, 255)')
      assert.equal(await band.locator('select').evaluate(el => getComputedStyle(el).color), 'rgb(22, 26, 35)')
      assert.equal(await band.locator('select').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(255, 255, 255)')
      await page.getByText('Synthetic project status note remains below the counts', { exact: true }).waitFor()
      assert.equal(await page.locator('#dashboard-staffing').count(), 1)
      const identityHeaderHeight = (await page.locator('[data-visual-project-header]').boundingBox()).height
      if (width >= 768) assert.ok(Math.abs(identityHeaderHeight - 64) <= 1, `Dashboard desktop identity header must preserve 64px: ${identityHeaderHeight}`)
      else assert.ok(identityHeaderHeight >= 64 && identityHeaderHeight <= 72, `Dashboard phone identity header must retain its 64–72px contract: ${identityHeaderHeight}`)
      await page.goto(`${base}/home?projects=all`)
      await measure('#h-metrics + .home-count-cards [data-count-card]', width === 1440 ? 72 : 64, 'Home')
      if (width === 1440) {
        await page.goto(`${base}/portfolio`)
        await measure('.portfolio-count-cards [data-count-card]', 72, 'Portfolio desktop')
      } else {
        await page.goto(`${base}/portfolio`)
        await page.getByText('This screen is available on tablet and desktop.', { exact: true }).waitFor()
        assert.equal(await page.locator('.portfolio-count-cards').count(), 0)
        assert.equal(await page.locator('main').getByRole('link', { name: 'My Work', exact: true }).getAttribute('href'), '/my-work')
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
        results.push({ label: 'Portfolio phone', width, overlap: 'Not applicable: existing DesktopOnly restriction' })
      }
    }
    // Home's saved first widget stays authoritative; no forced overlap of a chart.
    metricsFirst = false
    await page.setViewportSize({ width: 1440, height: 1000 }); await page.goto(`${base}/home?projects=all`)
    await page.locator('#h-metrics').waitFor(); assert.equal(await page.locator('.visual-page--overlap').count(), 0)
    metricsFirst = true
    // Plain loading band reserves no count overlay before actual figures exist.
    blockDashboard = true; dashboardStarted = new Promise(resolve => { notifyDashboard = resolve })
    await page.goto(`${base}/projects/${p.projectNumber}/dashboard`); await dashboardStarted
    assert.equal(await page.locator('.visual-dashboard-band.visual-page--overlap').count(), 0)
    assert.equal(await page.locator('[data-dashboard-counts]').count(), 0)
    blockDashboard = false; releaseDashboard()
    await measure('[data-dashboard-counts] .home-count-cards [data-count-card]', 72, 'Dashboard after loading')
    visual = false
    for (const path of ['/staff', `/projects/${p.projectNumber}/dashboard`, '/home?projects=all', '/portfolio']) {
      await page.goto(`${base}${path}`)
      if (path === '/staff') await page.getByRole('link', { name: 'Synthetic person 1', exact: true }).waitFor()
      else if (path.includes('/dashboard')) await page.getByText('Synthetic project status note remains below the counts', { exact: true }).waitFor()
      else if (path.startsWith('/home')) await page.getByRole('article', { name: 'Active Projects', exact: true }).waitFor()
      else await page.locator('main table:visible tbody tr').first().waitFor()
      await page.waitForFunction(() => !document.documentElement.hasAttribute('data-visual'))
      assert.equal(await page.locator('.staff-count-view,.visual-dashboard-band,.visual-page--overlap').count(), 0)
      assert.equal(await page.locator('[data-count-card]').count(), 0)
    }
    assert.deepEqual(writes, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ results, boundary: 'Mocked API geometry, not server admission, all roles, native zoom or full visual census' }))
    console.log('PASS: exact 72/64 flow overlap, uncovered titles/reference/actions/tabs, Staff retained density/controls/units, Dashboard 6+12/details, Home ordering, Portfolio desktop/phone restriction, off and loading')
  } finally { releaseDashboard?.(); await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
