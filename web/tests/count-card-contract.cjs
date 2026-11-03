// 052 FR03/04/15 and AC07: real shared source component + real Home→Tasks route,
// with labelled synthetic API cohorts. Not backend authorization or every-screen proof.
const assert = require('node:assert/strict')
const { newMockPage, project } = require('./platform-fixtures.cjs')
const { meView } = require('./me.fixture.cjs')
const { homeRead } = require('./workspace.fixture.cjs')
const { taskRow, taskState, taskPage } = require('./tasks.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local Vite development source harness')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const tones = [
  ['blue', 'rgb(47, 111, 224)', 'rgb(232, 240, 254)', '/projects?status=Active'],
  ['violet', 'rgb(106, 82, 209)', 'rgb(239, 235, 253)', '/tasks?open=true'],
  ['red', 'rgb(214, 51, 74)', 'rgb(253, 233, 236)', '/tasks?overdue=true'],
  ['amber', 'rgb(199, 125, 10)', 'rgb(253, 241, 220)', '/home?view=deadlines'],
  ['green', 'rgb(24, 145, 95)', 'rgb(227, 245, 236)', '/projects?status=Active&computedHealth=Green'],
  ['teal', 'rgb(15, 138, 150)', 'rgb(222, 243, 244)', '/team'],
]
const overdueTasks = [1, 2].map(n => taskRow({ id: id(910 + n), projectId: project.id, key: `SYN-CC-T00${n}`, name: `Synthetic overdue task ${n}`,
  dueDate: `2026-10-0${n}`, state: taskState({ isOverdue: true, daysOverdue: 3 - n, isMissingDueDate: false }) }))

;(async () => {
  const { page, browser } = await newMockPage(base, { events: false })
  const errors = [], reads = []
  let visual = true
  page.setDefaultTimeout(10000)
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  try {
    await page.route('**/api/v1/me', route => route.fulfill({ json: meView({ id: id(2), displayName: 'Synthetic count viewer', email: 'count@hub.test',
      settings: { today: '2026-10-03' }, flags: { visual_system: visual } }) }))
    await page.route('**/api/v1/home**', route => route.fulfill({ json: homeRead({ projectId: project.id, layout: [{ id: 'metrics', hidden: false }],
      metrics: { activeProjects: 1, openTasks: 2, overdueTasks: 2, onTrack: { green: 1, evaluated: 1, pct: 100 }, teamMembers: 1 } }) }))
    await page.route('**/api/v1/tasks?**', route => {
      const params = new URL(route.request().url()).searchParams
      reads.push(Object.fromEntries(params))
      const rows = params.get('projects') === project.id && params.get('overdue') === 'true' ? overdueTasks : []
      return route.fulfill({ json: taskPage(rows, { pageSize: 100 }) })
    })
    const homeUrl = `${base}/home?projects=${project.id}`
    await page.goto(homeUrl)
    await page.locator('#h-metrics + .home-count-cards').waitFor()
    await page.waitForFunction(() => document.documentElement.dataset.visual === 'v2')

    // Import the actual component, contexts and styles from observed development URLs.
    // This standalone fixture deliberately makes no destination/list-parity claim.
    await page.evaluate(async tones => {
      const observed = path => performance.getEntriesByType('resource').map(item => item.name).filter(value => new URL(value).pathname === path).at(-1)
      const dependency = name => {
        const url = observed(`/node_modules/.vite/deps/${name}`)
        if (!url) throw new Error(`Development source harness did not load ${name}`)
        return url
      }
      const React = await import(dependency('react.js')), ReactDOM = await import(dependency('react-dom_client.js'))
      const { MemoryRouter } = await import(dependency('react-router.js'))
      const { CountCard } = await import(observed('/src/components/hub/count-card.tsx') ?? '/src/components/hub/count-card.tsx')
      const { TooltipProvider } = await import(observed('/src/components/ui/tooltip.tsx') ?? '/src/components/ui/tooltip.tsx')
      const R = React.default ?? React, client = ReactDOM.default ?? ReactDOM
      const root = document.createElement('div'); root.id = 'count-card-contract'
      root.style.cssText = 'position:fixed;inset:16px;z-index:45;overflow:auto;background:var(--page)'
      document.body.append(root)
      const renderer = client.createRoot(root)
      function Samples() {
        const [red, setRed] = R.useState(3)
        window.__setCountCardRed = setRed
        return R.createElement('div', { className: 'home-count-cards' }, tones.map(([tone, , , to], index) => R.createElement(CountCard, {
          key: tone, label: `Synthetic ${tone}`, value: tone === 'red' ? red === null ? R.createElement('span', { 'aria-hidden': true }, '—') : red : index + 1,
          valueText: tone === 'red' && red === null ? 'Unavailable' : undefined, tone, to,
          where: 'Synthetic cohort', definition: `Exact synthetic ${tone} definition`,
        })))
      }
      renderer.render(R.createElement(TooltipProvider, null, R.createElement(MemoryRouter, null, R.createElement(Samples))))
      window.__removeCountCardContract = () => { renderer.unmount(); root.remove(); delete window.__setCountCardRed; delete window.__removeCountCardContract }
    }, tones)
    const fixture = page.locator('#count-card-contract'), cards = fixture.locator('[data-count-card]')
    await cards.first().waitFor()
    assert.equal(await cards.count(), 6)
    assert.deepEqual(await cards.evaluateAll(nodes => nodes.map(node => node.tagName)), Array(6).fill('A'))
    for (const [index, [tone, strong, tint, href]] of tones.entries()) {
      const card = cards.nth(index), value = index === 2 ? 3 : index + 1
      assert.equal(await card.getAttribute('href'), href)
      assert.equal(await card.locator('a,button,input,select').count(), 0, 'whole link has no interactive descendants')
      assert.equal(await fixture.getByRole('link', { name: `Synthetic ${tone} ${value} Synthetic cohort`, exact: true }).count(), 1)
      await page.waitForFunction(({ index, strong, tint }) => {
        const card = document.querySelectorAll('#count-card-contract [data-count-card]')[index], icon = card?.querySelector('.count-card__icon'), value = card?.querySelector('.count-card__value')
        return icon && value && getComputedStyle(icon).backgroundColor === tint && getComputedStyle(icon).color === strong && getComputedStyle(value).color === strong
      }, { index, strong, tint })
      const style = await card.evaluate(el => {
        const s = getComputedStyle(el), icon = el.querySelector('.count-card__icon'), i = icon.getBoundingClientRect(), value = getComputedStyle(el.querySelector('.count-card__value'))
        return { background: s.backgroundColor, padding: s.paddingTop, radius: s.borderRadius, border: s.borderTopWidth, icon: [i.width, i.height], hiddenIcon: icon.getAttribute('aria-hidden'), dotted: getComputedStyle(el.querySelector('.count-card__label')).textDecorationStyle, fontSize: value.fontSize, lineHeight: value.lineHeight, numeric: value.fontVariantNumeric }
      })
      assert.deepEqual(style, { background: 'rgb(255, 255, 255)', padding: '20px', radius: '14px', border: '1px', icon: [32, 32], hiddenIcon: 'true', dotted: 'dotted', fontSize: '32px', lineHeight: '40px', numeric: 'tabular-nums' })
      await card.focus()
      await page.getByRole('tooltip').filter({ hasText: `Exact synthetic ${tone} definition` }).waitFor()
      await page.keyboard.press('Escape'); await card.blur()
    }
    await cards.first().hover()
    await page.getByRole('tooltip').filter({ hasText: 'Exact synthetic blue definition' }).waitFor()
    await page.mouse.move(0, 0)
    const red = cards.nth(2)
    assert.equal(await red.locator('[data-alert]').count(), 1)
    assert.equal(await red.locator('.count-card__meta').innerText(), '■ Needs attention')
    await page.evaluate(() => window.__setCountCardRed(0))
    await page.waitForFunction(() => document.querySelector('#count-card-contract .count-card--red .count-card__value')?.textContent === '0')
    assert.equal(await red.locator('[data-alert], .count-card__meta').count(), 0)
    await page.waitForFunction(() => getComputedStyle(document.querySelector('#count-card-contract .count-card--red .count-card__value')).color === 'rgb(22, 26, 35)')
    assert.equal(await red.locator('.count-card__value').evaluate(el => getComputedStyle(el).color), 'rgb(22, 26, 35)')
    assert.equal(await fixture.getByRole('link', { name: 'Synthetic red 0 Synthetic cohort', exact: true }).count(), 1)
    await page.evaluate(() => window.__setCountCardRed(null))
    await fixture.getByRole('link', { name: 'Synthetic red Unavailable Synthetic cohort', exact: true }).waitFor()
    assert.equal(await red.locator('[data-alert], .count-card__meta').count(), 0)
    for (const [width, columns, padding, fontSize] of [[1440, 6, '20px', '32px'], [1100, 3, '20px', '32px'], [375, 2, '16px', '28px']]) {
      await page.setViewportSize({ width, height: 900 })
      await page.waitForFunction(({ width, columns, padding, fontSize }) => {
        const grid = document.querySelector('#count-card-contract .home-count-cards'), first = grid?.firstElementChild
        return innerWidth === width && first && getComputedStyle(grid).gridTemplateColumns.split(' ').length === columns
          && getComputedStyle(first).paddingTop === padding && getComputedStyle(first.querySelector('.count-card__value')).fontSize === fontSize
      }, { width, columns, padding, fontSize })
      const layout = await fixture.evaluate(el => {
        const grid = el.querySelector('.home-count-cards'), style = getComputedStyle(grid), nodes = [...grid.children]
        return { columns: style.gridTemplateColumns.split(' ').length, gap: style.gap, heights: nodes.map(node => node.getBoundingClientRect().height), padding: getComputedStyle(nodes[0]).paddingTop, fontSize: getComputedStyle(nodes[0].querySelector('.count-card__value')).fontSize, contained: el.scrollWidth <= el.clientWidth + 1 }
      })
      assert.equal(layout.columns, columns); assert.equal(layout.gap, '16px')
      assert.equal(layout.padding, padding); assert.equal(layout.fontSize, fontSize)
      assert.ok(layout.heights.every(height => Math.abs(height - layout.heights[0]) <= 1), 'equivalent cards have equal sizes')
      assert.equal(layout.contained, true)
    }
    await page.evaluate(() => window.__removeCountCardContract())

    // Real Home link, real keyboard activation, real Tasks page and mocked exact scope.
    await page.setViewportSize({ width: 1440, height: 1000 })
    await page.goto(homeUrl)
    const overdue = page.getByRole('link', { name: 'Overdue Tasks 2 1 project', exact: true })
    await overdue.waitFor(); await overdue.focus()
    await page.getByRole('tooltip').filter({ hasText: 'Open tasks past their due date.' }).waitFor()
    assert.equal(await overdue.getAttribute('href'), `/tasks?projects=${project.id}&overdue=true`)
    const response = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/tasks' && new URL(response.url()).searchParams.get('overdue') === 'true')
    await overdue.press('Enter'); await response
    await page.waitForURL(url => url.pathname === '/tasks' && url.searchParams.get('projects') === project.id && url.searchParams.get('overdue') === 'true')
    const rows = page.getByRole('table', { name: 'Tasks', exact: true }).locator('tbody tr')
    await rows.nth(1).waitFor()
    assert.equal(await rows.count(), 2)
    for (const task of overdueTasks) assert.equal(await rows.filter({ hasText: task.key }).count(), 1)
    assert.equal(reads.at(-1).projects, project.id); assert.equal(reads.at(-1).overdue, 'true')
    visual = false
    await page.goto(homeUrl)
    await page.getByRole('article', { name: 'Overdue Tasks', exact: true }).waitFor()
    await page.waitForFunction(() => !document.documentElement.hasAttribute('data-visual'))
    assert.equal(await page.locator('html').getAttribute('data-visual'), null)
    assert.equal(await page.locator('.count-card').count(), 0, 'legacy Home does not acquire the new card component')
    assert.deepEqual(errors, [])
    console.log('PASS: source CountCard six metric roles/focus/names/red0/unknown/6-3-2 geometry; real Home→Tasks Enter with2 mocked scoped rows; legacy Home restored. Staff sums and server admission remain separate.')
  } finally {
    await page.evaluate(() => window.__removeCountCardContract?.()).catch(() => {})
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
