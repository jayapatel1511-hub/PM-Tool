// Shared component CSS acceptance; requires the existing Vite development harness for source imports.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local development harness')

;(async () => {
  const { page, browser } = await newMockPage(base)
  try {
    const errors = []
    page.on('pageerror', e => errors.push(e)); page.on('console', m => { if (m.type() === 'error') errors.push(new Error(m.text())) })
    await page.goto(`${base}/projects`)
    await page.getByRole('heading', { name: 'Projects', exact: true }).waitFor()
    await page.evaluate(async () => {
      const React = await import('/node_modules/.vite/deps/react.js'); const ReactDOM = await import('/node_modules/.vite/deps/react-dom_client.js')
      const { StatusPill, Chip } = await import('/src/components/hub/pills.tsx')
      const { ItemMark } = await import('/src/components/hub/item-mark.tsx')
      const { Section, SummaryTile } = await import('/src/components/hub/common.tsx')
      const { Avatar } = await import('/src/components/hub/people.tsx')
      const { LANE_FILLS, laneFill, laneClass, TIME_AWAY_FILL, NONWORKING_FILL } = await import('/src/components/hub/semantic-colours.ts')
      const root = document.createElement('div'); root.id = 'component-fixture'; document.body.append(root)
      const R = React.default ?? React; const client = ReactDOM.default ?? ReactDOM
      window.__chipClicks = 0
      window.__laneRoles = { fills: LANE_FILLS, unknown: laneFill('Invented') ?? null, inherited: laneFill('constructor') ?? null, away: TIME_AWAY_FILL, nonworking: NONWORKING_FILL }
      client.createRoot(root).render(R.createElement(R.Fragment, null,
        R.createElement('div', { id: 'semantic-statuses' }, ...['On Track', 'In Progress', 'At Risk', 'Overdue', 'Complete', 'Not Started'].map(status => R.createElement(StatusPill, { key: status, status }))),
        R.createElement('div', { id: 'semantic-chips' }, ...['ok', 'work', 'warn', 'bad', 'done', 'idle'].map(tone => R.createElement(Chip, { key: tone, tone, onClick: tone === 'work' ? () => window.__chipClicks++ : undefined }, tone))),
        R.createElement('div', { id: 'semantic-avatars' }, ...Array.from({ length: 12 }, (_, n) => R.createElement(Avatar, { key: n, id: `synthetic-person-${n}`, name: 'Synthetic Person' }))),
        R.createElement(Section, { id: 'semantic-section', title: 'Synthetic neutral section', accent: 'mint' }, 'Recorded section content'),
        R.createElement('div', { id: 'semantic-metric' }, R.createElement(SummaryTile, { label: 'Synthetic metric awaiting its scoped migration', value: 4, accent: 'mint', onClick: () => {} })),
        ...Object.keys(LANE_FILLS).map(lane => R.createElement('div', { key: lane, 'data-lane-probe': lane, className: `semantic-lane ${laneClass(lane)}` }, `${lane}: 1`)),
        R.createElement('div', { id: 'semantic-unknown-lane', className: 'semantic-lane' }, 'Unknown lane'),
        R.createElement('div', { id: 'semantic-away', className: 'semantic-time-away', role: 'img', 'aria-label': 'Time off: 4 hours' }, 'Time off: 4 hours'),
        R.createElement('div', { id: 'semantic-nonworking', className: 'semantic-nonworking-day', role: 'img', 'aria-label': 'Non-working day' }, 'Non-working day'),
        ...['Task', 'Deliverable', 'Milestone'].map(kind => R.createElement(ItemMark, { key: kind, kind, label: kind }))))
    })
    const pills = page.locator('#semantic-statuses .semantic-pill'), chips = page.locator('#semantic-chips .semantic-pill')
    const avatars = page.locator('#semantic-avatars [data-visual-decoration="person"]')
    await pills.nth(5).waitFor()
    const colours = locator => locator.evaluateAll(xs => xs.map(x => ({ tone: x.dataset.semanticTone, bg: getComputedStyle(x).backgroundColor, fg: getComputedStyle(x).color, symbol: x.firstElementChild.textContent })))
    const avatarStyles = () => avatars.evaluateAll(xs => xs.map(x => ({ fg: getComputedStyle(x).color, border: getComputedStyle(x).borderColor })))
    const legacyPills = await colours(pills), legacyAvatars = await avatarStyles()
    assert.ok(new Set(legacyAvatars.map(x => x.fg)).size > 1, 'off keeps existing per-person hash accents')
    const legacyMetric = await page.locator('#semantic-metric [data-accent] span').first().evaluate(el => getComputedStyle(el).color)
    assert.equal(await page.locator('#semantic-away').evaluate(el => getComputedStyle(el).backgroundImage), 'none')
    await page.locator('html').evaluate(el => { el.dataset.visual = 'v2' })
    const expected = {
      tones: ['ok', 'work', 'warn', 'bad', 'done', 'idle'], symbols: ['●', '◐', '▲', '■', '✓', '○'],
      bg: ['rgb(234, 245, 239)', 'rgb(240, 239, 250)', 'rgb(255, 244, 219)', 'rgb(255, 240, 241)', 'rgb(237, 242, 253)', 'rgb(241, 242, 244)'],
      fg: ['rgb(56, 105, 83)', 'rgb(87, 78, 135)', 'rgb(122, 88, 23)', 'rgb(180, 35, 50)', 'rgb(55, 88, 154)', 'rgb(89, 96, 108)'],
    }
    for (const rows of [await colours(pills), await colours(chips)]) {
      assert.deepEqual(rows.map(x => x.tone), expected.tones); assert.deepEqual(rows.map(x => x.symbol), expected.symbols)
      assert.deepEqual(rows.map(x => x.bg), expected.bg); assert.deepEqual(rows.map(x => x.fg), expected.fg)
    }
    assert.equal(await page.locator('#semantic-chips').getByRole('button').count(), 1)
    await page.locator('#semantic-chips').getByRole('button', { name: /work/ }).click()
    assert.equal(await page.evaluate(() => window.__chipClicks), 1)
    const resolved = await page.evaluate(() => {
      // Fresh nodes and the same tested property avoid reading an interpolated colour
      // from a reused probe (including browser oklab transition serialization).
      const value = (token, property) => {
        const node = document.createElement('span')
        node.style.cssText = 'position:fixed;visibility:hidden;transition:none!important;animation:none!important;border:1px solid transparent'
        node.style[property] = `var(${token})`
        document.body.append(node)
        const resolved = getComputedStyle(node)[property]
        node.remove()
        return resolved
      }
      return { ink2: value('--ink-2', 'color'), line3: value('--line-3', 'borderColor'), raised: value('--raised', 'backgroundColor') }
    })
    assert.equal(resolved.ink2, 'rgb(70, 80, 99)')
    assert.deepEqual(await avatarStyles(), legacyAvatars.map(() => ({ fg: resolved.ink2, border: resolved.line3 })))
    const section = await page.locator('#semantic-section > [data-visual-decoration="section"]').evaluate(el => ({ bg: getComputedStyle(el).backgroundColor, fg: getComputedStyle(el).getPropertyValue('--acc-fg').trim() }))
    assert.equal(section.bg, resolved.raised); assert.equal(section.fg, '#465063')
    assert.equal(await page.locator('#semantic-metric [data-accent] span').first().evaluate(el => getComputedStyle(el).color), legacyMetric, 'neutral decoration must not indiscriminately erase metric accents')
    assert.deepEqual(await page.evaluate(() => window.__laneRoles), { fills: { 'To Do': 'var(--lane-todo)', 'In Progress': 'var(--lane-progress)', Review: 'var(--lane-review)', Done: 'var(--lane-done)', 'On Hold': 'var(--time-away-hatch)' }, unknown: null, inherited: null, away: 'var(--time-away-hatch)', nonworking: 'var(--nonworking-hatch)' })
    const lanes = await page.locator('[data-lane-probe]').evaluateAll(xs => xs.map(x => getComputedStyle(x).borderTopColor))
    assert.deepEqual(lanes, ['rgb(148, 160, 180)', 'rgb(106, 82, 209)', 'rgb(240, 164, 43)', 'rgb(47, 111, 224)', 'rgb(148, 160, 180)'])
    assert.equal(await page.locator('#semantic-unknown-lane').evaluate(el => getComputedStyle(el).borderTopColor), resolved.ink2)
    const away = await page.locator('#semantic-away').evaluate(el => getComputedStyle(el).backgroundImage)
    assert.match(away, /135deg/); assert.match(away, /148, 160, 180/); assert.match(away, /213, 218, 227/)
    assert.equal(await page.locator('[data-lane-probe="On Hold"]').evaluate(el => getComputedStyle(el).backgroundImage), away)
    const nonworking = await page.locator('#semantic-nonworking').evaluate(el => getComputedStyle(el).backgroundImage)
    assert.match(nonworking, /135deg/); assert.notEqual(nonworking, away)
    const marks = await page.locator('#component-fixture [data-item-mark]').evaluateAll(xs => xs.map(x => ({ kind: x.dataset.itemKind, radius: getComputedStyle(x).borderRadius, width: getComputedStyle(x).width, height: getComputedStyle(x).height, label: x.getAttribute('aria-label') })))
    assert.deepEqual(marks.map(x => x.kind), ['Task', 'Deliverable', 'Milestone']); assert.deepEqual(marks.map(x => x.radius), ['999px', '2px', '0px'])
    assert.ok(marks.every(x => x.width === '10px' && x.height === '10px')); assert.deepEqual(marks.map(x => x.label), ['Task', 'Deliverable', 'Milestone'])
    await page.locator('html').evaluate(el => { delete el.dataset.visual })
    await page.waitForFunction(expected => JSON.stringify([...document.querySelectorAll('#semantic-avatars [data-visual-decoration="person"]')]
      .map(el => ({ fg: getComputedStyle(el).color, border: getComputedStyle(el).borderColor }))) === JSON.stringify(expected), legacyAvatars)
    assert.deepEqual(await colours(pills), legacyPills); assert.deepEqual(await avatarStyles(), legacyAvatars)
    assert.equal(await page.locator('#semantic-away').evaluate(el => getComputedStyle(el).backgroundImage), 'none')
    assert.equal(errors.length, 0, errors.map(e => e.stack ?? e.message).join('\n'))
    console.log('PASS: six pill/chip roles and symbols, neutral person/section roles with metric isolation, lane/hatch roles,10px shape geometry and unchanged off restoration')
  } finally { await browser.close() }
})().catch(err => { console.error(err); process.exitCode = 1 })
