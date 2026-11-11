// Actual component presentation/interactions, synthetic API-free inputs. Requires the Vite source harness.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')
const fixture = require('./planner-semantic.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local development harness')

;(async () => {
  const { page, browser } = await newMockPage(base)
  const errors = [], planningRequests = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  page.on('request', request => { if (new URL(request.url()).pathname.includes('/api/v1/planning/')) planningRequests.push({ url: request.url(), method: request.method() }) })
  try {
    await page.goto(`${base}/projects`)
    await page.getByRole('heading', { name: 'Projects', exact: true }).waitFor()
    await page.evaluate(async ({ fixture, me, reference }) => {
      // Use the exact URLs already loaded by the app, including Vite's version query.
      // Loading a second router/query module would manufacture a different provider context.
      const dependency = filename => {
        const url = performance.getEntriesByType('resource').map(item => item.name).find(value => new URL(value).pathname.endsWith(`/node_modules/.vite/deps/${filename}`))
        if (!url) throw new Error(`Development app did not load ${filename}`)
        return url
      }
      const React = await import(dependency('react.js'))
      const ReactDOM = await import(dependency('react-dom_client.js'))
      const { MemoryRouter } = await import(dependency('react-router.js'))
      const { QueryClient, QueryClientProvider } = await import(dependency('@tanstack_react-query.js'))
      const { Ctx } = await import('/src/lib/auth-context.ts')
      const { PlannerGrid } = await import('/src/components/planner/PlannerGrid.tsx')
      const { PlannerMonthGrid } = await import('/src/components/planner/PlannerMonthGrid.tsx')
      const { ProjectPlanningGrid } = await import('/src/components/planner/ProjectPlanningGrid.tsx')
      const { PlanningBandBar } = await import('/src/components/planner/PlanningBandBar.tsx')
      const { ContributionList } = await import('/src/components/planner/ContributionList.tsx')
      const { VacationPage } = await import('/src/pages/Vacation.tsx')
      const R = React.default ?? React, client = ReactDOM.default ?? ReactDOM
      const root = document.createElement('div'); root.id = 'planner-semantic-fixture'
      root.style.cssText = 'position:fixed;inset:0;z-index:40;overflow:auto;padding:16px;background:var(--page)'
      document.body.append(root)
      const renderer = client.createRoot(root)
      const query = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, retry: false, refetchOnWindowFocus: false } } })
      query.setQueryData(['reference'], reference)
      query.setQueryData(['planning-cell', fixture.detail.personId, fixture.weeks[0], true, undefined], fixture.detail)
      query.setQueryData(['availability-view', '2026-10-01', '2026-10-31'], { ...fixture.availability, people: fixture.availability.people.map(p => ({ ...p, days: p.days.filter(d => d.date.startsWith('2026-10')) })) })
      query.setQueryData(['availability-view', '2026-01-01', '2026-12-31'], { ...fixture.availability, from: '2026-01-01', through: '2026-12-31' })
      const originalVisual = document.documentElement.getAttribute('data-visual')
      window.__plannerActions = []
      const onCell = (person, cell) => window.__plannerActions.push({ kind: 'cell', person: person.id, week: cell.week })
      const onEntry = entry => window.__plannerActions.push({ kind: 'entry', id: entry.id })
      const onQuickAdd = async (person, week, text, key) => { window.__plannerActions.push({ kind: 'quick', person, week, text, key }) }
      const render = ({ view = 'weekly', visual = true, capacity = true, unit = 'hours', editable = false } = {}) => {
        if (visual) document.documentElement.dataset.visual = 'v2'; else delete document.documentElement.dataset.visual
        const value = { me: { ...me, flags: { visual_system: visual, capacity_screens: capacity, figure_names: true } } }
        const shared = { data: fixture.data, onEntry, kept: fixture.kept, unit }
        const component = view === 'band' ? R.createElement(PlanningBandBar, { ...fixture.data.people[0].cells[0], partial: true, provisional: true })
          : view === 'month' ? R.createElement(PlannerMonthGrid, { data: fixture.data, unit, groupBy: 'none', onCell })
          : view === 'project' ? R.createElement(ProjectPlanningGrid, { ...shared, zoom: 'weeks' })
          : view === 'detail' ? R.createElement(ContributionList, { personId: fixture.detail.personId, week: fixture.weeks[0], onClose: () => render({ view: 'weekly', visual, capacity }) })
          : view === 'vacation' || view === 'year' ? R.createElement(VacationPage)
          : R.createElement(PlannerGrid, { ...shared, groupBy: 'none', editable, onCell, onQuickAdd })
        renderer.render(R.createElement(Ctx.Provider, { value }, R.createElement(QueryClientProvider, { client: query },
          R.createElement(MemoryRouter, { key: `${view}-${visual}-${capacity}-${unit}-${editable}`, initialEntries: [view === 'year' ? '/vacation?year=2026&view=year' : '/vacation?year=2026&month=10'] },
            R.createElement('div', { id: 'planner-semantic-surface', 'data-view': view, 'data-enabled': String(visual), 'data-unit': unit, 'data-capacity': String(capacity) }, component)))))
      }
      window.__plannerHarness = { render, dispose: () => { renderer.unmount(); root.remove(); query.clear(); if (originalVisual === null) delete document.documentElement.dataset.visual; else document.documentElement.dataset.visual = originalVisual } }
      render({ visual: false, capacity: false })
    }, { fixture: { ...fixture, id: undefined }, me: meView({ id: fixture.id(1), displayName: 'Synthetic owner', email: 'synthetic@hub.test', systemRoles: ['Supervisor'], capabilities: { workload: true, staff: true }, settings: { today: '2026-10-06' } }), reference: referenceData() })

    const surface = page.locator('#planner-semantic-surface')
    const render = async options => {
      await page.evaluate(options => window.__plannerHarness.render(options), options)
      await page.locator(`#planner-semantic-surface[data-view="${options.view ?? 'weekly'}"][data-enabled="${String(options.visual ?? true)}"][data-unit="${options.unit ?? 'hours'}"][data-capacity="${String(options.capacity ?? true)}"]`).waitFor({ state: 'attached' })
    }
    const personRow = name => surface.getByRole('row').filter({ hasText: name }).first()
    const totalButton = name => personRow(name).locator('td').first().locator('button').first()
    const expand = async () => { await personRow('Reported away').getByRole('button', { name: 'Expand planning row', exact: true }).click() }
    const sourceButton = i => surface.getByRole('button', { name: new RegExp(`Recorded source ${i + 1}(?![0-9])`) })
    const style = locator => locator.evaluate(el => { const s = getComputedStyle(el); return { border: s.borderLeftColor, bg: s.backgroundImage, borderStyle: s.borderStyle } })

    await surface.waitFor()
    await expand()
    const legacySource = await style(sourceButton(6))
    assert.match(legacySource.bg, /linear-gradient/, 'off private draft keeps its old hatch')
    const legacyColours = await surface.locator('button[data-accent]').evaluateAll(xs => xs.map(x => getComputedStyle(x).borderLeftColor))
    assert.ok(new Set(legacyColours).size > 1, 'off source categories retain their distinct tints')
    await render({ view: 'band', visual: false, capacity: false })
    const legacyWidths = await surface.locator('[data-planning-summary] > div > span').evaluateAll(xs => xs.map(x => x.getBoundingClientRect().width))

    await render({ visual: true, capacity: false })
    await expand()
    assert.equal(await surface.locator('button[data-planner-source]').count(), 10)
    const neutral = await surface.locator('button[data-planner-source]').evaluateAll(xs => xs.map(x => getComputedStyle(x).borderLeftColor))
    assert.equal(new Set(neutral).size, 1, 'every enabled source marker is neutral')
    assert.deepEqual(await surface.locator('button[data-planner-source]').evaluateAll(xs => xs.map(x => x.dataset.plannerSource)), fixture.sources)
    const expectedSources = ['Major project', 'Other project', 'Proposal', 'Business development', 'Admin', 'Supervision', 'Training', 'Internal initiative', 'Field work', 'Other']
    for (let i = 0; i < expectedSources.length; i++) assert.ok((await sourceButton(i).innerText()).includes(expectedSources[i]), `source word retained: ${expectedSources[i]}`)
    const draft = sourceButton(6)
    assert.equal((await style(draft)).bg, 'none', 'private draft does not borrow the away hatch')
    assert.match(await draft.innerText(), /Private draft/)
    assert.match(await draft.innerText(), /Confidence: Possible/)
    assert.match(await draft.innerText(), /No longer matches/)
    assert.equal(await draft.locator('svg[aria-label="Private draft"]').count(), 1, 'lock remains separate from confidence/kept facts')
    assert.equal(await draft.evaluate(el => getComputedStyle(el, '::after').borderTopStyle), 'dashed')
    assert.equal(await draft.locator('.planner-semantic-possible').evaluate(el => getComputedStyle(el).textDecorationStyle), 'dotted')
    assert.equal(await draft.evaluate(el => getComputedStyle(el).borderTopStyle), 'dashed', 'kept border remains distinct from the inset draft outline')
    await draft.focus(); await page.keyboard.press('Tab'); await page.keyboard.press('Shift+Tab')
    // Read the settled focus style, after the browser's outline transitions finish.
    await draft.evaluate(async el => { await Promise.all(el.getAnimations().map(animation => animation.finished.catch(() => {}))) })
    assert.equal(await draft.evaluate(el => document.activeElement === el && el.matches(':focus-visible')), true)
    assert.equal(await draft.evaluate(el => getComputedStyle(el).outlineWidth), '2px', 'draft inset does not replace keyboard focus')
    assert.equal(await draft.evaluate(el => getComputedStyle(el).outlineOffset), '2px')
    await draft.click()
    assert.deepEqual(await page.evaluate(() => window.__plannerActions.at(-1)), { kind: 'entry', id: fixture.entries[6].id })
    assert.match(await surface.innerText(), /Visibility: Self-entered/)
    await personRow('Approved allocation').getByRole('button', { name: 'Expand planning row', exact: true }).click()
    assert.match(await surface.innerText(), /Approval status: Confirmed/)
    assert.equal(await surface.getByRole('link', { name: /SYN-501/ }).count(), 0, 'approval remains read-only when its supplied canOpen is false')
    const awayButton = totalButton('Reported away')
    assert.match(await awayButton.getAttribute('aria-label'), /Time off 4 h/)
    assert.match(await awayButton.getAttribute('aria-label'), /Confidence: Confirmed.*6 h/)
    assert.equal(await awayButton.evaluate(el => getComputedStyle(el).backgroundImage), 'none', 'four hours do not hatch the whole week')
    const swatch = awayButton.locator('[data-planner-away="week"]')
    const awayStyle = await swatch.evaluate(el => { const s = getComputedStyle(el, '::before'); return { image: s.backgroundImage, width: parseFloat(s.width), height: parseFloat(s.height), total: el.closest('button').getBoundingClientRect().width } })
    assert.match(awayStyle.image, /135deg/); assert.match(awayStyle.image, /148, 160, 180/); assert.match(awayStyle.image, /213, 218, 227/)
    assert.ok(awayStyle.width < awayStyle.total / 2 && awayStyle.height <= 12, 'the mark is a swatch, not an interval-sized block')
    assert.equal(await personRow('Zero without away').locator('[data-planner-away]').count(), 0, 'zero and additional capacity do not infer away')
    assert.equal(await personRow('Partial planning').locator('[data-planner-away]').count(), 0)
    assert.match(await totalButton('Partial planning').innerText(), /at most 40 h/, 'existing partial weekly upper-bound disclosure is retained')
    assert.equal(await totalButton('Partial planning').locator('.planner-semantic-quantity').count(), 0, 'an upper-bound disclosure is not promoted to a known spare-capacity figure')
    assert.match(await totalButton('Provisional away').innerText(), /Time off 8 h/)
    assert.match(await totalButton('Provisional away').innerText(), /Provisional availability/)
    assert.equal(planningRequests.length, 0, 'closed cells/source rows perform no extra planning/private-data reads')
    await awayButton.focus(); await page.keyboard.press('ArrowRight'); await page.keyboard.press('Enter')
    assert.deepEqual(await page.evaluate(() => window.__plannerActions.at(-1)), { kind: 'cell', person: fixture.data.people[0].id, week: fixture.weeks[1] })

    await render({ view: 'band', visual: true })
    const widths = await surface.locator('[data-planning-summary] > div > span').evaluateAll(xs => xs.map(x => x.getBoundingClientRect().width))
    assert.equal(widths.length, legacyWidths.length); widths.forEach((value, i) => assert.ok(Math.abs(value - legacyWidths[i]) < 1, 'band quantities/scale are unchanged'))
    assert.match(await surface.getByRole('img').getAttribute('aria-label'), /project records are outside your scope/)
    assert.match(await surface.getByRole('img').getAttribute('aria-label'), /Provisional availability/)
    assert.equal((await surface.locator('figcaption').innerText()).replace(/\s/g, ''), '6h/6h/4h', 'exact band values remain visible')
    assert.equal(await surface.locator('[data-planning-confidence="Confirmed"]').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(15, 138, 150)')
    assert.equal(await surface.locator('[data-planning-confidence="Possible"]').evaluate(el => getComputedStyle(el).borderTopStyle), 'dotted')
    assert.equal(await surface.locator('[data-planning-summary]').evaluate(el => getComputedStyle(el).backgroundImage), 'none', 'unused capacity is not invented time away')
    assert.equal(await surface.locator('[data-planner-away]').count(), 0)

    await render({ unit: 'percent' })
    assert.match(await totalButton('Zero without away').innerText(), /Percentage unavailable/)
    assert.match(await totalButton('Provisional away').innerText(), /Percentage unavailable/)
    await render({ editable: true })
    await totalButton('Reported away').click()
    const quick = surface.getByRole('textbox')
    await quick.fill('4 h Synthetic work'); await page.keyboard.press('Escape')
    await quick.waitFor({ state: 'detached' })
    assert.equal(await quick.count(), 0)
    await page.waitForFunction(() => document.activeElement?.getAttribute('aria-label')?.includes('Reported away'))
    assert.equal(await totalButton('Reported away').evaluate(el => el === document.activeElement), true)
    assert.equal((await page.evaluate(() => window.__plannerActions)).filter(x => x.kind === 'quick').length, 0, 'cancelled quick-add creates nothing')

    await render({ view: 'month' })
    const month = personRow('Reported away')
    assert.equal(await month.locator('[data-planner-away="month"]').textContent(), 'Time off 4 h')
    assert.equal(await month.locator('td').first().evaluate(el => getComputedStyle(el).backgroundImage), 'none', 'partial away does not hatch a month')
    await month.locator('button').first().click()
    await page.getByRole('button', { name: /^2026-10-12/ }).click()
    assert.deepEqual(await page.evaluate(() => window.__plannerActions.at(-1)), { kind: 'cell', person: fixture.data.people[0].id, week: fixture.weeks[1] })
    await render({ view: 'month', unit: 'percent' })
    assert.match(await personRow('Provisional away').innerText(), /Percentage unavailable/)
    await render({ view: 'month' })
    assert.match(await personRow('Partial planning').innerText(), /Remaining capacity unavailable/)
    await render({ view: 'project' })
    await surface.getByRole('button', { name: 'Recorded source 1', exact: true }).click()
    assert.deepEqual(await page.evaluate(() => window.__plannerActions.at(-1)), { kind: 'entry', id: fixture.entries[0].id })

    await render({ view: 'detail' })
    const dialog = page.getByRole('dialog'), daily = dialog.locator('table').first()
    await daily.waitFor()
    for (const [date, expected] of [['2026-10-06', true], ['2026-10-11', true], ['2026-10-07', false], ['2026-10-08', false], ['2026-10-09', false], ['2026-10-10', false]]) {
      const row = daily.getByRole('row').filter({ hasText: date })
      assert.equal(await row.getAttribute('data-planner-nonworking'), expected ? 'true' : null, `only explicit canonical truth hatches ${date}`)
      assert.equal((await row.evaluate(el => getComputedStyle(el).backgroundImage)) !== 'none', expected)
    }
    assert.equal(await daily.locator('[data-planner-away="day"]').count(), 2)
    assert.match(await daily.getByRole('row').filter({ hasText: '2026-10-09' }).innerText(), /Time off 8 h.*Provisional availability/s)
    assert.equal(await daily.getByRole('row').filter({ hasText: '2026-10-06' }).locator('[data-planner-away]').count(), 0, 'holiday is not personal leave')

    await render({ view: 'vacation' })
    const dates = surface.locator('table tbody tr').first().locator('td')
    // October's date columns make the 7th, 8th and working Saturday10th explicit non-inference cases.
    for (const [index, expected] of [[5, true], [6, false], [7, false], [9, false], [10, true]]) assert.equal(await dates.nth(index).getAttribute('data-planner-nonworking'), expected ? 'true' : null)
    assert.equal(await dates.nth(4).locator('[data-planner-away]').textContent(), 'Time off 4 h')
    assert.match(await dates.nth(8).innerText(), /Time off 8 h.*Provisional/s)
    assert.match(await surface.innerText(), /Full day|Full working day/i, 'null hoursPerDay retains its full-day record meaning')
    assert.equal(await surface.locator('#schedule-hours').count(), 0, 'hidden schedule detail is not reconstructed into an editor')
    await render({ view: 'year' })
    assert.equal(await surface.locator('[data-planner-nonworking]').count(), 0, 'year month columns do not infer daily non-working intervals')
    assert.equal(await surface.locator('[data-planner-away="month"]').count(), 1)
    assert.equal(await surface.locator('[data-planner-away="month"]').textContent(), 'Time off 12 h')
    assert.equal(await surface.locator('td.planner-semantic-nonworking').count(), 0)

    await render({ visual: true })
    const quantity = totalButton('Reported away').locator('.planner-semantic-quantity').first()
    const ink = await quantity.evaluate(el => {
      const s = getComputedStyle(el), canvas = document.createElement('canvas'); canvas.width = canvas.height = 1
      const context = canvas.getContext('2d')
      const rgba = value => { context.clearRect(0, 0, 1, 1); context.fillStyle = value; context.fillRect(0, 0, 1, 1); return [...context.getImageData(0, 0, 1, 1).data] }
      const ancestors = []; for (let node = el; node; node = node.parentElement) ancestors.push(node)
      let background = [255, 255, 255]
      for (const node of ancestors.reverse()) { const next = rgba(getComputedStyle(node).backgroundColor), alpha = next[3] / 255; background = background.map((channel, i) => next[i] * alpha + channel * (1 - alpha)) }
      return { color: s.color, size: parseFloat(s.fontSize), weight: Number(s.fontWeight), background }
    })
    assert.deepEqual({ color: ink.color, size: ink.size, weight: ink.weight }, { color: 'rgb(22, 26, 35)', size: 19, weight: 700 }, 'primary hours under 24 px use ink, not teal (FR-VSN-21)')
    const linear = value => { value /= 255; return value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4 }
    const luminance = rgb => rgb.map(linear).reduce((sum, value, i) => sum + value * [.2126, .7152, .0722][i], 0)
    const a = luminance(ink.background), b = luminance([22, 26, 35])
    const contrast = (Math.max(a, b) + .05) / (Math.min(a, b) + .05)
    assert.ok(contrast >= 4.5, 'primary hours figure under 24 px has 4.5:1 against its actual composited grid surface (AC-VSN-11)')
    assert.equal(planningRequests.length, 0, 'presentation harness seeded existing reads and made no planning/private-data request or write')
    await render({ visual: false, capacity: false }); await expand()
    assert.deepEqual(await style(sourceButton(6)), legacySource, 'off restoration keeps original source/draft/kept presentation')
    assert.equal(await surface.locator('[data-planner-away]').count(), 0)
    await render({ visual: false, capacity: true }); await expand()
    assert.deepEqual(await style(sourceButton(6)), legacySource, 'capacity screens alone does not activate visual source/draft semantics')
    await render({ view: 'detail', visual: false })
    await page.getByRole('dialog').locator('table').first().waitFor()
    assert.equal(await page.getByRole('dialog').locator('[data-planner-away], [data-planner-nonworking]').count(), 0, 'off contribution keeps original day presentation')
    await render({ view: 'vacation', visual: false })
    assert.equal(await surface.locator('[data-planner-nonworking], [data-planner-away]').count(), 0, 'off Vacation ignores the optional new truth fields')
    assert.ok((await surface.locator('table td').nth(4).getAttribute('class')).includes('bg-peach/20'), 'off retains positive-leave peach fill')
    await page.setViewportSize({ width: 375, height: 812 })
    for (const view of ['weekly', 'month', 'project', 'vacation']) {
      await render({ view })
      assert.ok(await page.locator('#planner-semantic-fixture').evaluate(el => el.scrollWidth - el.clientWidth <= 1), `${view}: component overflow stays in its own region at375px`)
    }
    assert.equal(errors.length, 0, errors.map(error => error.stack ?? error.message).join('\n'))
    console.log('PASS: actual planner/Vacation components, named roles, exact facts/flags, no interval inference, preserved actions/off restoration; API-free synthetic harness.')
  } finally {
    await page.evaluate(() => window.__plannerHarness?.dispose()).catch(() => {})
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
