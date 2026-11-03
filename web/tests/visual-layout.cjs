// Packet 052 S02: v2 band, topbar and project header layout.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const { meView, referenceData } = require('./me.fixture.cjs')
const { projectDetail } = require('./projects.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local development source harness')

const longName = 'Harbour service coordination and multidisciplinary delivery programme for the north expansion works with phased construction sequencing and stakeholder review'.slice(0, 120)
assert.equal(longName.length, 120)
const project = projectDetail({ id: '00000000-0000-0000-0000-000000000101', projectNumber: 'SYN-101', name: longName, client: 'Synthetic client', pmName: 'Synthetic PM', targetCompletionDate: '2026-11-02' })
const originalProject = { ...project, health: { ...project.health } }

const boundary = locator => locator.evaluate(el => {
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1
  const context = canvas.getContext('2d')
  const rgba = value => { context.clearRect(0, 0, 1, 1); context.fillStyle = value; context.fillRect(0, 0, 1, 1); return [...context.getImageData(0, 0, 1, 1).data] }
  const over = (next, base) => base.map((value, i) => next[i] * next[3] / 255 + value * (1 - next[3] / 255))
  const ancestors = []; for (let node = el.parentElement; node; node = node.parentElement) ancestors.push(node)
  let outside = [255, 255, 255]
  for (const node of ancestors.reverse()) {
    const style = getComputedStyle(node)
    if (style.backgroundImage !== 'none') throw new Error('Boundary sample needs explicit image compositing')
    outside = over(rgba(style.backgroundColor), outside)
  }
  const style = getComputedStyle(el), inside = over(rgba(style.backgroundColor), outside)
  const edge = over(rgba(style.borderTopColor), inside)
  const luminance = rgb => rgb.reduce((sum, value, i) => { value /= 255; return sum + (value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4) * [.2126, .7152, .0722][i] }, 0)
  const contrast = (a, b) => (Math.max(luminance(a), luminance(b)) + .05) / (Math.min(luminance(a), luminance(b)) + .05)
  return { inside: contrast(edge, inside), outside: contrast(edge, outside), width: parseFloat(style.borderTopWidth), disabled: !!el.disabled }
})

;(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' })
  page.setDefaultTimeout(8000)
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  let enabled = false
  await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'sam@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
  await page.route('**/api/**', async route => {
    const request = route.request()
    const pathname = new URL(request.url()).pathname.replace('/api/v1/', '')
    if (pathname === 'config') return route.fulfill({ json: clientConfig() })
    if (pathname === 'me') return route.fulfill({ json: meView({ id: '00000000-0000-0000-0000-000000000002', displayName: 'Alex Chen', email: 'alex@hub.test', capabilities: { workload: true }, flags: { visual_system: enabled } }) })
    if (pathname === 'reference' || pathname === 'workspaces' || pathname === 'views') return route.fulfill({ json: pathname === 'reference' ? referenceData() : [] })
    if (pathname === 'me/sign-in') return route.fulfill({ status: 204, body: '' })
    if (pathname === `projects/${project.projectNumber}` || pathname === `projects/${project.id}`) return route.fulfill({ json: project })
    if (pathname === 'me/notifications/pulse' || pathname === 'me/notifications/unread-count') return route.fulfill({ json: {} })
    if (request.method() !== 'GET') return route.fulfill({ status: 204, body: '' })
    return route.fulfill({ json: {} })
  })

  await page.goto(`${base}/projects/${project.projectNumber}/settings`)
  await page.locator('[data-visual-project-header]').waitFor({ timeout: 10000 }).catch(async error => { console.error('Page errors:', errors.map(e => e.message)); console.error('Rendered:', await page.locator('body').innerText()); await page.screenshot({ path: '/private/tmp/tuesday-layout-failure.png' }); throw error })
  assert.equal(await page.locator('html').getAttribute('data-visual'), null, 'flag-off remains legacy')
  assert.equal(await page.locator('.visual-project-meta').count(), 0, 'flag-off does not add project metadata')
  const brand = page.locator('.visual-sidebar [data-visual-decoration="brand"]')
  const legacyBrand = await brand.evaluate(el => { const style = getComputedStyle(el); return { color: style.color, background: style.backgroundColor, width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height } })
  assert.equal(legacyBrand.color, 'rgb(56, 105, 83)', 'off retains its legacy mint mark')
  const legacyHeight = await page.locator('[data-visual-project-header]').evaluate(el => el.getBoundingClientRect().height)
  assert.ok(legacyHeight > 0)
  await page.locator('button[aria-label="Collapse navigation"]').click()
  await page.locator('button[aria-label="Expand navigation"]').waitFor()
  await page.waitForFunction(() => Math.round(document.querySelector('.visual-sidebar').getBoundingClientRect().width) === 76)
  assert.equal(await page.locator('.visual-sidebar').evaluate(el => Math.round(el.getBoundingClientRect().width)), 76, 'collapsed rail remains 76px off')

  enabled = true
  await page.reload()
  await page.locator('[data-visual-project-header]').waitFor({ timeout: 10000 }).catch(async error => { console.error('Page errors:', errors.map(e => e.message)); console.error('Rendered:', await page.locator('body').innerText()); await page.screenshot({ path: '/private/tmp/tuesday-layout-failure.png' }); throw error })
  assert.equal(await page.locator('html').getAttribute('data-visual'), 'v2')
  await page.waitForFunction(() => getComputedStyle(document.querySelector('[data-visual-decoration="brand"]')).color === 'rgb(27, 42, 78)')
  assert.deepEqual(await brand.evaluate(el => { const style = getComputedStyle(el); return { color: style.color, background: style.backgroundColor, width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height } }), { color: 'rgb(27, 42, 78)', background: 'rgb(243, 244, 247)', width: legacyBrand.width, height: legacyBrand.height }, 'enabled brand alone uses navy on raised with unchanged geometry')
  assert.equal(await page.locator('[data-visual-decoration="brand"]').count(), 1, 'brand role does not absorb person, metric or status surfaces')
  const header = page.locator('[data-visual-project-header]')
  assert.ok((await header.boundingBox()).height <= 72, 'project header is at most 72px')
  assert.equal(await page.locator('.visual-project-name').getAttribute('title'), longName)
  await page.locator('[data-visual-project-header] button').first().focus()
  assert.equal(await page.locator('.visual-project-name').evaluate(el => el.matches(':focus-visible')), false, 'title is not focused before keyboard navigation')
  await page.locator('[data-visual-project-header] button').first().focus()
  await page.keyboard.press('Tab')
  await page.getByRole('tooltip').waitFor()
  assert.equal(await page.getByRole('tooltip').textContent(), longName)
  assert.equal(await page.evaluate(() => document.activeElement?.classList.contains('visual-project-name')), true, 'project name can receive keyboard focus')
  await page.waitForFunction(() => { const style = getComputedStyle(document.querySelector('.visual-project-name')); return style.outlineWidth === '2px' && style.outlineColor === 'rgb(155, 184, 255)' })
  assert.deepEqual(await page.locator('.visual-project-name').evaluate(el => { const style = getComputedStyle(el); return { color: style.outlineColor, width: style.outlineWidth, offset: style.outlineOffset } }), { color: 'rgb(155, 184, 255)', width: '2px', offset: '2px' }, 'focused project name uses the exact specified navy focus hue, width and offset')
  assert.equal(await page.locator('.visual-project-meta').count(), 1)
  assert.match(await page.locator('.visual-project-meta').innerText(), /Synthetic PM.*Synthetic client.*2026/s)
  assert.equal(await page.locator('.visual-topbar').evaluate(el => getComputedStyle(el).height), '56px')
  await page.waitForFunction(() => {
    const el=document.querySelector('.visual-topbar [data-variant="outline"]')
    return el && getComputedStyle(el).backgroundColor === 'rgba(0, 0, 0, 0)' && getComputedStyle(el).color === 'rgb(255, 255, 255)'
  })
  assert.deepEqual(await page.locator('.visual-project-number').evaluate(el => {
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1
    const context = canvas.getContext('2d'); context.fillStyle = getComputedStyle(el).backgroundColor; context.fillRect(0, 0, 1, 1)
    return [...context.getImageData(0, 0, 1, 1).data]
  }), [255, 255, 255, 36], 'project number retains the exact white 14% dark-band fill across CSS colour serializations')


  await page.locator('[data-visual-project-header] button').first().focus()
  await page.waitForFunction(() => !document.querySelector('.visual-project-name').matches(':focus-visible'))
  assert.equal(await page.locator('.visual-project-name').evaluate(el => getComputedStyle(el).outlineStyle), 'none', 'blurred title has no outline')
  await page.screenshot({ path: '/private/tmp/tuesday-build-header-desktop.png' })
  // Exercise the real Page and Button components on the light project surface.
  await page.evaluate(async () => {
    const source = path => performance.getEntriesByType('resource').map(item => item.name).filter(value => new URL(value).pathname === path).at(-1) ?? path
    const dependency = name => {
      const url = performance.getEntriesByType('resource').map(item => item.name).find(value => new URL(value).pathname.endsWith(`/node_modules/.vite/deps/${name}`))
      if (!url) throw new Error(`Development app did not load ${name}`)
      return url
    }
    const React = await import(dependency('react.js'))
    const ReactDOM = await import(dependency('react-dom_client.js'))
    const modules = Object.fromEntries(['auth-context.ts', 'common.tsx', 'button.tsx', 'input.tsx', 'title-block.tsx'].map(name => [name, source(name === 'auth-context.ts' ? `/src/lib/${name}` : `/src/components/${['button.tsx', 'input.tsx'].includes(name) ? 'ui' : 'hub'}/${name}`)]))
    const { Ctx } = await import(modules['auth-context.ts'])
    const { Page, InProject, Segmented, selectCls } = await import(modules['common.tsx'])
    const { Button } = await import(modules['button.tsx'])
    const { Input } = await import(modules['input.tsx'])
    const { TitleBlock } = await import(modules['title-block.tsx'])
    const R = React.default ?? React; const client = ReactDOM.default ?? ReactDOM
    const container = document.createElement('div'); container.id = 'project-actions-fixture'; document.body.append(container)
    container.style.cssText = 'position:fixed;inset:64px 16px 16px;z-index:45;overflow:auto;background:var(--page)'
    const renderer = client.createRoot(container)
    window.__projectActionsModules = modules
    function ContextProbe() {
      return R.createElement('span', { hidden: true, 'data-project-context': String(R.useContext(InProject)), 'data-visual-context': String(R.useContext(Ctx)?.me.flags?.visual_system === true) })
    }
    function Controls() {
      const [value, setValue] = R.useState('first')
      return R.createElement('div', { id: 'fixture-fields', className: 'grid gap-3' },
        R.createElement(Input, { id: 'fixture-input', 'aria-label': 'Synthetic normal input', defaultValue: 'Recorded value' }),
        R.createElement(Input, { id: 'fixture-readonly', 'aria-label': 'Synthetic readonly input', defaultValue: 'Recorded readonly value', readOnly: true }),
        R.createElement(Input, { id: 'fixture-invalid', 'aria-label': 'Synthetic invalid input', defaultValue: 'Invalid synthetic value', 'aria-invalid': true }),
        R.createElement('select', { id: 'fixture-select', 'aria-label': 'Synthetic native select', className: selectCls, defaultValue: 'first' }, R.createElement('option', { value: 'first' }, 'First'), R.createElement('option', { value: 'second' }, 'Second')),
        R.createElement(Segmented, { label: 'Synthetic view', value, options: [{ value: 'first', label: 'By person' }, { value: 'second', label: 'By project' }], onChange: setValue }))
    }
    function References() {
      const [label, setLabel] = R.useState('Snapshot reference')
      window.__setReferenceLabel = setLabel
      return R.createElement(Page, { title: 'Reference fixture', subtitle: 'Recorded server times', titleBlock: R.createElement(TitleBlock, { asOf: '2026-10-31T14:24:05Z', timeZone: 'America/Halifax', references: [{ label, value: '2026-10-30T13:05:00Z' }] }) }, 'Synthetic content')
    }
    renderer.render(R.createElement(Ctx.Provider, { value: { me: { flags: { visual_system: true } } } },
      R.createElement(R.Fragment, null, R.createElement(InProject.Provider, { value: true }, R.createElement(Page, { title: 'Project action fixture', actions: R.createElement(R.Fragment, null,
        R.createElement(Button, { id: 'fixture-primary' }, 'New milestone'), R.createElement(Button, { id: 'fixture-secondary', variant: 'outline' }, 'Export')) }, R.createElement(R.Fragment, null, R.createElement(ContextProbe), R.createElement(Controls)))), R.createElement(References))))
    window.__removeProjectActions = () => { renderer.unmount(); container.remove(); delete window.__setReferenceLabel; delete window.__projectActionsModules; delete window.__projectActionsSettled }
  })
  await page.locator('#fixture-primary').waitFor()
  // Dynamic source imports can add styles after the first paint. A single white sample
  // can be a transition endpoint; require the actual project context and settled colours.
  let actionStyles
  try {
    const settled = await page.waitForFunction(() => {
      const root = document.querySelector('#project-actions-fixture'), primary = root?.querySelector('#fixture-primary'), secondary = root?.querySelector('#fixture-secondary')
      if (!primary || !secondary) return false
      const probe = root.querySelector('[data-project-context]'), p = getComputedStyle(primary), s = getComputedStyle(secondary)
      const sample = { visual: document.documentElement.dataset.visual, project: !!secondary.closest('.visual-page--project'), projectContext: probe?.dataset.projectContext, visualContext: probe?.dataset.visualContext, primaryBackground: p.backgroundColor, primaryColor: p.color, secondaryBackground: s.backgroundColor, secondaryColor: s.color }
      const expected = sample.visual === 'v2' && sample.project && sample.projectContext === 'true' && sample.visualContext === 'true' && sample.primaryBackground === 'rgb(27, 42, 78)' && sample.primaryColor === 'rgb(255, 255, 255)' && sample.secondaryBackground === 'rgb(255, 255, 255)' && sample.secondaryColor === 'rgb(22, 26, 35)'
      const moving = [primary, secondary].some(el => el.getAnimations().some(animation => animation.pending || animation.playState === 'running'))
      if (!expected || moving) { delete window.__projectActionsSettled; return false }
      const signature = JSON.stringify(sample), previous = window.__projectActionsSettled
      if (!previous || previous.signature !== signature) { window.__projectActionsSettled = { signature, since: performance.now() }; return false }
      return performance.now() - previous.since >= 250 ? sample : false
    })
    actionStyles = await settled.jsonValue()
  } catch (error) {
    console.error('Project action fixture diagnostics:', await page.evaluate(() => {
      const secondary = document.querySelector('#fixture-secondary'), style = secondary && getComputedStyle(secondary)
      const rules = []
      const scan = list => { for (const rule of list) { if (rule.selectorText && secondary?.matches(rule.selectorText)) rules.push(rule.cssText); if (rule.cssRules) scan(rule.cssRules) } }
      for (const sheet of document.styleSheets) { try { scan(sheet.cssRules) } catch {} }
      return { visual: document.documentElement.dataset.visual, modules: window.__projectActionsModules, fixture: document.querySelector('#project-actions-fixture')?.outerHTML, secondaryStyle: style && { background: style.backgroundColor, color: style.color, transition: style.transition, card: style.getPropertyValue('--card'), animations: secondary.getAnimations().map(animation => ({ state: animation.playState, time: animation.currentTime })) }, matchingRules: rules }
    }))
    throw error
  }
  assert.equal(actionStyles.primaryColor, 'rgb(255, 255, 255)')
  assert.equal(actionStyles.secondaryBackground, 'rgb(255, 255, 255)')
  assert.equal(actionStyles.secondaryColor, 'rgb(22, 26, 35)')
  for (const selector of ['#fixture-input', '#fixture-readonly', '#fixture-invalid', '#fixture-select', '#fixture-fields [role="group"]', '#fixture-secondary', '.visual-topbar input[role="combobox"]', '.visual-topbar [data-variant="outline"]']) {
    const sample = await boundary(page.locator(selector).first())
    assert.equal(sample.disabled, false)
    assert.ok(sample.width >= 1 && sample.inside >= 3 && sample.outside >= 3, `${selector}: actual boundary must contrast with both adjacent rendered surfaces: ${JSON.stringify(sample)}`)
  }
  await page.locator('#fixture-fields').getByRole('button', { name: 'By project', exact: true }).click()
  await page.locator('#fixture-fields').getByRole('button', { name: 'By project', exact: true, pressed: true }).waitFor()
  assert.equal(await page.locator('#fixture-fields').getByRole('button', { name: 'By project', exact: true }).getAttribute('aria-pressed'), 'true')
  await page.waitForFunction(() => { const selected = document.querySelector('#fixture-fields [aria-pressed="true"]'); return selected && getComputedStyle(selected).backgroundColor === 'rgb(255, 255, 255)' && !selected.getAnimations().some(animation => animation.pending || animation.playState === 'running') })
  assert.equal(await page.locator('#fixture-fields').getByRole('button', { name: 'By project', exact: true }).evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(255, 255, 255)')
  await page.setViewportSize({ width: 375, height: 740 })
  await page.evaluate(() => window.__setReferenceLabel('LongVerificationReferenceWithoutBreaks'.repeat(4)))
  const reference = page.locator('#project-actions-fixture .visual-title-block')
  await page.waitForFunction(() => [...document.querySelectorAll('#project-actions-fixture .visual-title-block dt')].some(el => el.textContent.length > 100))
  const spacing = await page.addStyleTag({ content: '#project-actions-fixture * { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; }' })
  assert.equal(await reference.evaluate(el => {
    const band = el.closest('.visual-page-band'), box = band.getBoundingClientRect(), style = getComputedStyle(band)
    return Math.abs(el.getBoundingClientRect().width - (box.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight))) <= 1
  }), true, 'phone title block fills the band content width')
  assert.equal(await reference.locator('dt, dd').evaluateAll(xs => xs.every(el => el.scrollWidth <= el.clientWidth + 1)), true, 'long reference words and ISO times reflow within the phone title block')
  assert.equal(await page.locator('#project-actions-fixture').evaluate(el => el.scrollWidth <= el.clientWidth + 1), true)
  await spacing.evaluate(el => el.remove())
  await page.evaluate(() => window.__removeProjectActions())

  await page.setViewportSize({ width: 375, height: 740 })
  assert.equal(await page.evaluate(() => innerWidth), 375, 'test harness applied phone viewport')
  await page.waitForTimeout(100)
  await page.locator('[data-visual-project-header]').waitFor({ timeout: 10000 }).catch(async error => { console.error('Page errors:', errors.map(e => e.message)); console.error('Rendered:', await page.locator('body').innerText()); await page.screenshot({ path: '/private/tmp/tuesday-layout-failure.png' }); throw error })
  assert.deepEqual(await page.locator('main').evaluate(el => ({ width: innerWidth, diff: el.scrollWidth - el.clientWidth })), { width: 375, diff: 0 }, 'phone page does not scroll sideways')
  assert.ok((await header.boundingBox()).height <= 72, 'phone project header remains within 72px')
  const controlsFit = await page.locator('[data-visual-project-header] button, .visual-topbar button').evaluateAll((els) => els.filter(el => getComputedStyle(el).display !== 'none').every(el => {
    const box = el.getBoundingClientRect()
    return box.left >= 0 && box.right <= innerWidth && box.top >= 0 && box.bottom <= innerHeight
  }))
  assert.equal(controlsFit, true, 'visible project and topbar controls stay inside phone viewport')
  assert.ok((await page.locator('.visual-project-name').evaluate(el => el.getBoundingClientRect().height)) <= 24, 'phone project name stays one line')
  assert.equal(await page.locator('.visual-project-name').getAttribute('title'), longName)
  assert.equal(await page.locator('.visual-project-meta').evaluate(el => { const box=el.getBoundingClientRect(); const header=el.closest('header').getBoundingClientRect(); return box.top>=header.top && box.bottom<=header.bottom && box.left>=header.left && box.right<=header.right }), true, 'metadata fits inside its header without clipping')
  await page.screenshot({ path: '/private/tmp/tuesday-build-header-phone.png' })
  project.name = 'SyntheticProjectName'.repeat(8).slice(0, 120)
  await page.reload(); await header.waitFor()
  await page.waitForFunction(() => document.documentElement.getAttribute('data-visual') === 'v2')
  assert.ok((await header.boundingBox()).height <= 72)
  await header.locator('button').first().focus(); await page.keyboard.press('Tab')
  await page.getByRole('tooltip').waitFor()
  assert.equal(await page.getByRole('tooltip').textContent(), project.name, 'unbroken120-character name stays fully available to keyboard users')
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), 375)
  project.name = longName
  project.pmName = 'Synthetic project manager '.repeat(10); project.client = 'Synthetic client name '.repeat(10)
  project.status = 'Archived'; project.health = { ...project.health, computed: 'Red', reported: 'Red' }
  await page.reload(); await header.waitFor()
  await page.waitForFunction(() => document.documentElement.getAttribute('data-visual') === 'v2')
  assert.equal(await page.locator('.visual-project-name').getAttribute('title'), project.name)
  assert.ok((await header.boundingBox()).height <= 72)
  assert.equal(await page.locator('.visual-project-meta > span').last().evaluate(el => el.scrollWidth <= el.clientWidth + 1), true, 'target date remains complete beside long PM/client values')
  await header.getByRole('button', { name: 'Project details and actions', exact: true }).click()
  const details = page.locator('[data-slot="popover-content"][aria-label="Project details and actions"]')
  await details.waitFor()
  assert.ok((await details.textContent()).includes(project.pmName.trim()) && (await details.textContent()).includes(project.client.trim()), 'full clipped metadata remains available in the real Details popover')
  assert.equal(await details.locator('dd').evaluateAll(xs => xs.every(el => el.scrollWidth <= el.clientWidth + 1)), true, 'long metadata wraps inside the real popover instead of being silently clipped')
  await page.keyboard.press('Escape')
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), 375)
  Object.assign(project, originalProject)
  enabled = false
  await page.setViewportSize({ width: 1440, height: 900 })
  await page.reload()
  await page.waitForFunction(() => document.documentElement.getAttribute('data-visual') === null)
  assert.deepEqual(await brand.evaluate(el => { const style = getComputedStyle(el); return { color: style.color, background: style.backgroundColor, width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height } }), legacyBrand, 'turning the flag off restores the exact old brand appearance')
  assert.equal(errors.length, 0, errors.map(error => error.stack ?? error.message).join('\n'))
  console.log('PASS: visual_system off/on, navy 56px topbar, project header <=72px with focused long title/meta, and 375px no-horizontal-scroll.')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
