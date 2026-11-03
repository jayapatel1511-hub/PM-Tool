// Packet 052 S09: rendered colour/font/reflow evidence from an explicitly isolated
// synthetic API. Reflow at half the CSS viewport and DPR 2 is labelled equivalent;
// it does not claim an actual browser zoom setting or human keyboard acceptance.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { createHash } = require('node:crypto')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_CENSUS_API_BASE
const output = process.env.TUESDAY_REFLOW_OUTPUT
assert.equal(process.env.TUESDAY_REAL_QA, 'isolated-synthetic')
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname))
assert.ok(output, 'Set TUESDAY_REFLOW_OUTPUT to the evidence JSON path')

const screens = ['/home', '/my-work', '/boards', '/projects', '/tasks', '/calendar',
  '/portfolio', '/workload', '/planner', '/reports', '/team',
  '/projects/SYN-101/dashboard', '/projects/SYN-101/tasks',
  '/projects/SYN-101/deliverables', '/projects/SYN-101/timeline',
  '/projects/SYN-101/allocations', '/projects/SYN-101/coordination']
const flags = ['visual_system', 'screen_patterns', 'home_overview', 'work_screens',
  'project_screens', 'capacity_screens', 'register_screens', 'people_screens']
const variants = [
  { name: 'desktop', width: 1600, height: 900, dpr: 1, spacing: false },
  { name: 'phone', width: 375, height: 812, dpr: 1, spacing: false },
  { name: '200-percent-reflow-equivalent', width: 800, height: 450, dpr: 2, spacing: false },
  { name: '200-percent-reflow-equivalent-with-text-spacing', width: 800, height: 450, dpr: 2, spacing: true },
]

async function api(path, method = 'GET', body) {
  const response = await fetch(`${base}/api/v1${path}`, { method,
    headers: { 'X-Dev-User': 'jordan@hub.test', ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text()
  assert.ok(response.ok, `${method} ${path}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}

// Runs in the page. Values matching tokens establish rendered values only, not
// that the originating source declaration uses a named token.
function renderedAudit() {
  const main = document.querySelector('main') || document.body
  const visible = el => {
    const r = el.getBoundingClientRect(), s = getComputedStyle(el)
    return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden' && s.opacity !== '0'
  }
  const describe = el => ({ tag: el.tagName.toLowerCase(), id: el.id || null,
    classes: typeof el.className === 'string' ? el.className : null,
    text: (el.getAttribute('aria-label') || el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 140) })
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  const rgba = css => {
    ctx.clearRect(0, 0, 1, 1); ctx.fillStyle = css; ctx.fillRect(0, 0, 1, 1)
    return [...ctx.getImageData(0, 0, 1, 1).data]
  }
  const key = c => c.join(',')
  const chromatic = c => {
    if (!c[3]) return false
    const max = Math.max(...c.slice(0, 3)) / 255, min = Math.min(...c.slice(0, 3)) / 255
    const l = (max + min) / 2, saturation = max === min ? 0 : (max - min) / (1 - Math.abs(2 * l - 1))
    return saturation >= .15 && l >= .08 && l <= .96
  }
  const style = getComputedStyle(document.documentElement)
  const tokenNames = ['page', 'side', 'card', 'raised', 'ink', 'ink-2', 'ink-3', 'line', 'line-2', 'line-3', 'input', 'navy', 'brand', 'brand-tint',
    ...['blue', 'violet', 'red', 'amber', 'green', 'teal'].flatMap(x => [`metric-${x}`, `metric-${x}-tint`]),
    ...['todo', 'progress', 'review', 'done'].map(x => `lane-${x}`), 'hatch-strong', 'hatch-light',
    ...['ok', 'warn', 'critical', 'idle', 'complete', 'work'].flatMap(x => [`pill-${x}`, `pill-${x}-bg`]),
    ...['coral', 'mint', 'rose', 'cyan', 'lime'].map(x => `project-${x}`), 'action-hover']
  const tokens = tokenNames.map(name => ({ name: `--${name}`, value: style.getPropertyValue(`--${name}`).trim() }))
  const known = new Set(tokens.filter(x => x.value).map(x => key(rgba(x.value))))
  const colours = new Map(), smallText = [], clippedControls = []
  const elements = [...main.querySelectorAll('*')].filter(visible)
  for (const el of elements) {
    const css = getComputedStyle(el)
    for (const property of ['color', 'background-color', 'border-top-color', 'border-right-color', 'border-bottom-color', 'border-left-color', 'fill', 'stroke']) {
      const value = css.getPropertyValue(property)
      if (!value || ['none', 'currentcolor'].includes(value)) continue
      if (property.startsWith('border-') && parseFloat(css.getPropertyValue(property.replace('color', 'width'))) === 0) continue
      const colour = rgba(value)
      if (!chromatic(colour)) continue
      const k = `${property}:${key(colour)}`
      const record = colours.get(k) || { property, value, matchesTokenValue: known.has(key(colour)), count: 0, examples: [] }
      record.count++
      if (record.examples.length < 3) record.examples.push(describe(el))
      colours.set(k, record)
    }
    if ([...el.childNodes].some(n => n.nodeType === Node.TEXT_NODE && n.textContent.trim()) && parseFloat(css.fontSize) < 11) {
      smallText.push({ ...describe(el), fontSize: css.fontSize })
    }
    // Intentionally scrollable data and named ellipses are not generic clipping.
    // Keep every suspect as a finding for inspection; do not assert it is hidden content.
    if (el.matches('button, a[href], input, select, textarea, [role=button], [role=tab]') &&
      ['hidden', 'clip'].includes(css.overflowX) && el.scrollWidth > el.clientWidth + 2 && !el.title) {
      clippedControls.push({ ...describe(el), clientWidth: el.clientWidth, scrollWidth: el.scrollWidth })
    }
  }
  return { viewport: { width: innerWidth, height: innerHeight, dpr: devicePixelRatio },
    visualMode: document.documentElement.dataset.visual || null,
    documentWidth: document.documentElement.scrollWidth,
    sidewaysDocumentOverflow: document.documentElement.scrollWidth > innerWidth + 1,
    titleCount: main.querySelectorAll('h1').length, tokens, colours: [...colours.values()], smallText, clippedControls,
    scrollableDataRegions: elements.filter(el => {
      const css = getComputedStyle(el)
      return ['auto', 'scroll'].includes(css.overflowX) && el.scrollWidth > el.clientWidth + 2
    }).map(describe) }
}

;(async () => {
  assert.equal((await api('/config')).authMode, 'Development')
  const project = await api('/projects/SYN-101')
  assert.equal(project.name, 'Synthetic census project 1', 'Run only on visual-census-real synthetic fixtures')
  const settings = await api('/admin/settings')
  const originals = flags.map(name => {
    const setting = settings.find(x => x.key === `flag.${name}`)
    assert.ok(setting, `Missing isolated QA flag ${name}`)
    return [name, setting.value]
  })
  const digest = file => createHash('sha256').update(fs.readFileSync(path.resolve(__dirname, '../../', file))).digest('hex')
  const requestedScreens = (process.env.TUESDAY_CENSUS_ROUTES ?? '').split(',').filter(Boolean)
  assert.ok(requestedScreens.every(route => screens.includes(route)), 'Route filter must name a standard census screen')
  const selectedScreens = requestedScreens.length ? screens.filter(route => requestedScreens.includes(route)) : screens
  const report = { base, screens: selectedScreens, flags, scope: 'Synthetic local API, all amendment flags enabled. Automated reflow equivalent; actual browser zoom and human acceptance remain unverified.',
    buildEvidence: { apiAssemblySha256: digest('src/Hub.Api/bin/Debug/net10.0/Hub.Api.dll'), uiIndexSha256: digest('src/Hub.Api/wwwroot/index.html') },
    rows: [], pageErrors: [], externalFonts: [] }
  let browser
  try {
    for (const name of flags) await api(`/admin/settings/flag.${name}`, 'PUT', { value: true })
    browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH, headless: true })
    for (const variant of variants) {
      const context = await browser.newContext({ viewport: { width: variant.width, height: variant.height },
        screen: { width: 1600, height: 900 }, deviceScaleFactor: variant.dpr, reducedMotion: 'reduce' })
      await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'priya@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
      const page = await context.newPage(); page.setDefaultTimeout(15000)
      page.on('pageerror', error => report.pageErrors.push({ variant: variant.name, url: page.url(), message: error.message }))
      page.on('request', request => { if (request.resourceType() === 'font' && new URL(request.url()).origin !== new URL(base).origin) report.externalFonts.push(request.url()) })
      for (const route of selectedScreens) {
        await page.goto(`${base}${route}`, { waitUntil: 'domcontentloaded' })
        await page.locator('main').waitFor()
        await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {})
        if (variant.spacing) await page.evaluate(() => {
          const node = document.createElement('style'); node.id = 'wcag-text-spacing-audit'
          node.textContent = '* { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; } p { margin-bottom: 2em !important; }'
          document.head.append(node)
          const css = getComputedStyle(document.querySelector('main'))
          if (Math.abs(parseFloat(css.letterSpacing) - parseFloat(css.fontSize) * .12) > .1)
            throw new Error('WCAG text-spacing override did not take effect')
        })
        await page.evaluate(() => document.fonts.ready)
        const metrics = await page.evaluate(renderedAudit)
        assert.equal(metrics.visualMode, 'v2')
        await page.evaluate(fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8'))
        const violations = await page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations
          .filter(x => ['serious', 'critical'].includes(x.impact)).map(x => ({ id: x.id, impact: x.impact, nodes: x.nodes.map(n => ({ target: n.target, summary: n.failureSummary })) })))
        report.rows.push({ variant: variant.name, route, metrics, violations })
        fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n')
        console.log(JSON.stringify({ variant: variant.name, route, overflow: metrics.sidewaysDocumentOverflow, smallText: metrics.smallText.length, clippedControls: metrics.clippedControls.length, serious: violations.length }))
      }
      await context.close()
    }
  } finally {
    if (browser) await browser.close()
    report.flagRestoreFailures = []
    for (const [name, value] of originals) {
      try { await api(`/admin/settings/flag.${name}`, 'PUT', { value }) }
      catch (error) { report.flagRestoreFailures.push(String(error)) }
    }
    fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n')
  }
  const failures = report.rows.filter(x => x.metrics.sidewaysDocumentOverflow || x.metrics.smallText.length || x.metrics.clippedControls.length || x.violations.length)
  console.log(JSON.stringify({ observations: report.rows.length, rowsNeedingReview: failures.length, pageErrors: report.pageErrors.length, flagRestoreFailures: report.flagRestoreFailures.length }))
  if (failures.length || report.pageErrors.length || report.externalFonts.length || report.flagRestoreFailures.length) process.exitCode = 1
})().catch(error => { console.error(error); process.exitCode = 1 })
