// Packet 052 S02/S03/S06/S09 acceptance probe against an isolated synthetic Development API (never an operational
// database). Run after visual-census-real.cjs has created SYN-101..103; adds SYN-104 (every task bucket and On Hold),
// SYN-120 (a 120-character name) and one approved leave day, turns every 052/053 flag on, measures, then restores flags.
//   TUESDAY_REAL_QA=isolated-synthetic TUESDAY_CENSUS_API_BASE=http://127.0.0.1:<port> TUESDAY_ACCEPTANCE_OUTPUT=<json>
// Measures (flags on, 1440x900 unless stated): band order on every route (FR-VSN-11), the project header at 1440 and
// 375 px (AC-VSN-05), text-input boundaries (FR-VSN-13), count-card colours/links/counts/definitions (AC-VSN-02, 07),
// lane colours and hatch (AC-VSN-03), pill values (FR-VSN-05), rendered token and text-contrast censuses (AC-VSN-01, 11).
const assert = require('node:assert/strict')
const fs = require('node:fs')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_CENSUS_API_BASE
const output = process.env.TUESDAY_ACCEPTANCE_OUTPUT
assert.equal(process.env.TUESDAY_REAL_QA, 'isolated-synthetic')
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname))
assert.ok(output, 'Set TUESDAY_ACCEPTANCE_OUTPUT to the evidence JSON path')
const FLAGS = ['visual_system', 'screen_patterns', 'home_overview', 'work_screens', 'project_screens', 'capacity_screens', 'register_screens', 'people_screens']
const LONG_NAME = 'Synthetic census long-name project for the one-line header check, with a deliberately long descriptive tail end'.padEnd(120, '.')

async function api(who, path, method = 'GET', body, expected) {
  const response = await fetch(`${base}/api/v1${path}`, { method, headers: { 'X-Dev-User': `${who}@hub.test`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text()
  if (expected) assert.equal(response.status, expected, `${method} ${path}: ${text}`)
  else assert.ok(response.ok, `${method} ${path}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}

async function fixtures() {
  assert.equal((await api('jordan', '/config')).authMode, 'Development')
  assert.equal((await api('jordan', '/projects/SYN-101')).name, 'Synthetic census project 1', 'Run visual-census-real.cjs first')
  const ref = await api('jordan', '/reference'), users = await api('jordan', '/admin/users')
  const uid = name => users.find(u => u.email === `${name}@hub.test`).id
  const civil = ref.disciplines.find(d => d.code === 'CIV').id
  const today = (await api('priya', '/me')).settings.today
  const week = new Date(`${today}T00:00:00Z`); week.setUTCDate(week.getUTCDate() - (week.getUTCDay() + 6) % 7)
  const monday = week.toISOString().slice(0, 10)
  const end = new Date(week); end.setUTCDate(end.getUTCDate() + 25)
  const exists = async number => (await fetch(`${base}/api/v1/projects/${number}`, { headers: { 'X-Dev-User': 'jordan@hub.test' } })).ok
  const project = async (number, name) => {
    const p = await api('priya', '/projects', 'POST', { projectNumber: number, name, clientId: ref.clients[0].id, officeId: ref.offices[0].id,
      startDate: monday, targetCompletionDate: end.toISOString().slice(0, 10), disciplines: [{ disciplineId: civil, leadUserId: uid('marc') }],
      members: ['alex', 'jill'].map(n => ({ userId: uid(n), roles: ['TeamMember'], disciplineId: civil })) }, 201)
    const detail = await api('priya', `/projects/${p.id}`)
    await api('priya', `/projects/${p.id}/transition`, 'POST', { toStatus: 'Active', rowVersion: detail.rowVersion })
    return api('priya', `/projects/${p.id}`)
  }
  if (!await exists('SYN-120')) await project('SYN-120', LONG_NAME)
  const lanes = await exists('SYN-104') ? await api('priya', '/projects/SYN-104') : await project('SYN-104', 'Synthetic census lanes project')
  const current = (await api('priya', `/projects/${lanes.id}/tasks`)).items
  for (const [name, final, extra, steps] of [['Lane to do', 'Not Started', {}, []], ['Lane in progress', 'In Progress', {}, [['In Progress']]],
    ['Lane review', 'Ready for Review', { requiresReview: true, reviewerId: uid('marc') }, [['In Progress'], ['Ready for Review']]],
    ['Lane done', 'Complete', {}, [['In Progress'], ['Complete']]], ['Lane on hold', 'On Hold', {}, [['On Hold', { reason: 'Synthetic census hold' }]]]]) {
    const id = current.find(t => t.name === name)?.id ?? (await api('priya', `/projects/${lanes.id}/tasks`, 'POST', { name, projectDisciplineId: lanes.disciplines[0].id, assigneeId: uid('alex'),
      estimatedHours: 4, startDate: monday, dueDate: end.toISOString().slice(0, 10), requiresReview: false, ...extra }, 201)).id
    for (const [toStatus, more] of steps) {
      const { task } = await api('priya', `/tasks/${id}`)
      if (task.status === final) break
      if (task.status === toStatus) continue
      await api('priya', `/tasks/${id}/transition`, 'POST', { toStatus, rowVersion: task.rowVersion, acknowledgeReadiness: true, reason: 'Synthetic census fixture', ...more })
    }
  }
  const leave = await fetch(`${base}/api/v1/planning/leave/manual`, { method: 'POST', headers: { 'X-Dev-User': 'jordan@hub.test', 'Content-Type': 'application/json' },
    body: JSON.stringify({ personId: uid('alex'), sourceSystem: 'ManualHR', externalId: 'synthetic-census-leave', fromDate: monday, throughDate: monday, hoursPerDay: 8, status: 'Approved', rowVersion: 0 }) })
  return { monday, leaveStatus: leave.status }
}

// ---- in-page measurements -------------------------------------------------------------------------------------
function pageTools() {
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  const rgba = css => { ctx.clearRect(0, 0, 1, 1); ctx.fillStyle = '#000'; ctx.fillStyle = css; ctx.fillRect(0, 0, 1, 1); const d = ctx.getImageData(0, 0, 1, 1).data; return [d[0], d[1], d[2], d[3] / 255] }
  const parse = css => {
    const m = /rgba?\(([^)]+)\)/.exec(css)
    if (m) { const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1] }
    return rgba(css)
  }
  const over = (fg, bg) => { const a = fg[3]; return [fg[0] * a + bg[0] * (1 - a), fg[1] * a + bg[1] * (1 - a), fg[2] * a + bg[2] * (1 - a), 1] }
  const lum = c => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4 }; return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]) }
  const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05) }
  const chromatic = c => { if (!c[3]) return false; const max = Math.max(c[0], c[1], c[2]) / 255, min = Math.min(c[0], c[1], c[2]) / 255, l = (max + min) / 2; const s = max === min ? 0 : (max - min) / (1 - Math.abs(2 * l - 1)); return s >= 0.15 && l >= 0.08 && l <= 0.96 }
  const visible = el => { const r = el.getBoundingClientRect(), s = getComputedStyle(el); return r.width > 1 && r.height > 1 && s.display !== 'none' && s.visibility !== 'hidden' && s.opacity !== '0' }
  // Effective background: composite every ancestor background colour; a background image on the way is unresolved.
  const background = el => {
    const layers = []
    for (let n = el; n && n.nodeType === 1; n = n.parentElement) {
      const s = getComputedStyle(n)
      if (s.backgroundImage !== 'none' && !/^url/.test(s.backgroundImage) && n !== el) layers.push({ image: s.backgroundImage })
      const c = parse(s.backgroundColor)
      if (c[3] > 0) layers.push({ colour: c, opacity: Number(s.opacity) })
      if (c[3] >= 1) break
    }
    let bg = [255, 255, 255, 1], image = null
    for (const layer of layers.reverse()) { if (layer.image) image = layer.image; else bg = over(layer.colour, bg) }
    return { bg, image }
  }
  const describe = el => ({ tag: el.tagName.toLowerCase(), cls: typeof el.className === 'string' ? el.className.slice(0, 120) : '', text: (el.getAttribute('aria-label') || el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 80) })
  const hex = c => '#' + c.slice(0, 3).map(v => Math.round(v).toString(16).padStart(2, '0')).join('')
  return { rgba, parse, over, ratio, chromatic, visible, background, describe, hex }
}

function bandOrder() {
  const T = window.__T
  const main = document.querySelector('main'), m = main.getBoundingClientRect()
  const header = main.querySelector('[data-visual-project-header]')
  const band = header ?? main.querySelector('.visual-page-band')
  const h1 = [...main.querySelectorAll('h1')].filter(T.visible)
  const out = { project: !!header, band: !!band, h1Count: h1.length }
  if (!band) return out
  const b = band.getBoundingClientRect()
  out.bandTopOffset = Math.round(b.top - m.top)
  out.bandBackground = T.hex(T.parse(getComputedStyle(band).backgroundColor))
  out.h1InBand = h1.length === 1 && band.contains(h1[0])
  // Above the band nothing visible may sit inside main (banners and toasts are outside the page component).
  out.visibleAboveBand = [...main.querySelectorAll('*')].filter(e => !band.contains(e) && !e.contains(band) && T.visible(e) && e.getBoundingClientRect().bottom <= b.top + 1 && e.getBoundingClientRect().top >= m.top).map(T.describe).slice(0, 3)
  if (header) {
    const tabs = main.querySelector('[data-visual-project-tabs]')
    out.tabsAfterHeader = !!tabs && tabs.getBoundingClientRect().top >= b.bottom - 1
    out.tabsBackground = tabs ? T.hex(T.parse(getComputedStyle(tabs).backgroundColor)) : null
    const nested = main.querySelector('.visual-page--project > .visual-page-band')
    out.nestedBandAfterTabs = nested ? nested.getBoundingClientRect().top >= (tabs?.getBoundingClientRect().bottom ?? b.bottom) - 1 : null
    const dashboard = main.querySelector('.visual-dashboard-band.visual-page--overlap'), cards = dashboard?.querySelector('.home-count-cards')
    out.cardOverlap = cards ? Math.round(dashboard.querySelector(':scope > .visual-page-band').getBoundingClientRect().bottom - cards.getBoundingClientRect().top) : null
    return out
  }
  const column = h1[0]?.parentElement, columnBox = column?.getBoundingClientRect()
  // The eyebrow sits before the h1; a subtitle is a paragraph after it.
  const subtitles = column ? [...column.children].filter(e => e.tagName === 'P' && T.visible(e) && (h1[0].compareDocumentPosition(e) & Node.DOCUMENT_POSITION_FOLLOWING)) : []
  out.subtitleLines = subtitles.map(p => Math.round(p.getBoundingClientRect().height / parseFloat(getComputedStyle(p).lineHeight)))
  const block = band.querySelector('.visual-title-block')
  const tabsBox = band.querySelector('.visual-page-band-tabs')?.getBoundingClientRect()
  out.titleBlock = block ? { position: block.getBoundingClientRect().top >= h1[0].getBoundingClientRect().bottom - 1 ? 'below title' : 'beside title', inBand: band.contains(block), beforeTabs: !tabsBox || block.getBoundingClientRect().bottom <= tabsBox.top + 1 } : null
  const actions = band.querySelector('.visual-page-actions')
  out.actionsRight = actions && T.visible(actions) ? Math.round(b.right - actions.getBoundingClientRect().right) <= 40 && actions.getBoundingClientRect().left >= columnBox.left : null
  const tabs = band.querySelector('.visual-page-band-tabs')
  out.tabsAfterTitle = tabs ? tabs.getBoundingClientRect().top >= columnBox.bottom - 1 : null
  const content = main.querySelector('.visual-page-content')
  out.contentOverlap = content ? Math.round(b.bottom - content.getBoundingClientRect().top) : null
  const cards = main.querySelector('.home-count-cards, [data-count-card]')
  out.cardOverlap = cards && band.closest('.visual-page--overlap') ? Math.round(b.bottom - cards.getBoundingClientRect().top) : null
  return out
}

function inputBoundaries() {
  const T = window.__T
  const rows = []
  for (const el of document.querySelectorAll('input:not([type=checkbox]):not([type=radio]):not([type=hidden]):not([type=range]), select, textarea, [role=combobox]')) {
    if (!T.visible(el) || el.matches(':disabled')) continue
    const s = getComputedStyle(el)
    const outside = T.background(el.parentElement)
    const fill = T.over(T.parse(s.backgroundColor), outside.bg)
    const width = parseFloat(s.borderBottomWidth) || parseFloat(s.borderTopWidth)
    const border = width > 0 ? T.over(T.parse(s.borderBottomColor), fill) : null
    const borderRatio = border ? T.ratio(border, outside.bg) : 0
    const fillRatio = T.ratio(fill, outside.bg)
    rows.push({ ...T.describe(el), name: el.getAttribute('aria-label') || el.getAttribute('placeholder') || el.name || null, border: border && T.hex(border), outside: T.hex(outside.bg), borderRatio: +borderRatio.toFixed(2), fillRatio: +fillRatio.toFixed(2), inlineArrow: el.tagName === 'SELECT' && s.appearance !== 'none' && borderRatio < 1.1 && fillRatio < 1.1,
      pass: borderRatio >= 3 || fillRatio >= 3 || (el.tagName === 'SELECT' && s.appearance !== 'none' && borderRatio < 1.1 && fillRatio < 1.1) })
  }
  return rows
}

function colourCensus(tokenNames) {
  const T = window.__T
  const root = getComputedStyle(document.documentElement)
  const key = c => c.slice(0, 3).map(Math.round).join(',') + ',' + (+c[3].toFixed(2))
  const tokens = new Map(tokenNames.map(n => [n, root.getPropertyValue(n).trim()]).filter(([, v]) => v).map(([n, v]) => [key(T.rgba(v)), n]))
  const found = new Map()
  const textFailures = [], unresolved = []
  for (const el of document.body.querySelectorAll('*')) {
    if (!T.visible(el) || el.closest('svg defs')) continue
    const s = getComputedStyle(el)
    const where = el.closest('main') ? 'main' : el.closest('nav[aria-label="Main navigation"], .visual-sidebar') ? 'sidebar' : el.closest('.visual-topbar, header') ? 'topbar' : 'other'
    for (const property of ['color', 'background-color', 'border-top-color', 'border-bottom-color', 'border-left-color', 'border-right-color', 'fill', 'stroke', 'outline-color']) {
      if (property.startsWith('border-') && !(parseFloat(s.getPropertyValue(property.replace('color', 'width'))) > 0)) continue
      if (property === 'outline-color' && (s.outlineStyle === 'none' || !(parseFloat(s.outlineWidth) > 0))) continue
      if (property === 'color' && ![...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()) && !el.matches('svg, svg *')) continue
      const raw = s.getPropertyValue(property)
      if (!raw || raw === 'none' || /^url/.test(raw)) continue
      const c = T.parse(raw)
      // As in the §41 census, a translucent colour is judged composited on white (ink hairlines are not hues).
      if (!T.chromatic(c[3] < 1 ? T.over(c, [255, 255, 255, 1]) : c)) continue
      const k = key(c), token = tokens.get(k) ?? null
      const id = `${where}|${property}|${k}`
      const rec = found.get(id) ?? { where, property, colour: T.hex(c), alpha: +c[3].toFixed(2), token, count: 0, examples: [] }
      rec.count++; if (rec.examples.length < 3) rec.examples.push(T.describe(el))
      found.set(id, rec)
    }
    // Text contrast (AC-VSN-11): text under 24 px needs 4.5:1, larger text 3:1, against the rendered background.
    if (![...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim())) continue
    if (el.closest('[aria-hidden=true]') && !el.textContent.trim().match(/[A-Za-z0-9]/)) continue
    if (el.matches(':disabled, [aria-disabled=true]') || el.closest(':disabled, [aria-disabled=true]')) continue
    const { bg, image } = T.background(el)
    let opacity = 1; for (let n = el; n; n = n.parentElement) opacity *= Number(getComputedStyle(n).opacity)
    const fg = T.parse(s.color); fg[3] *= opacity
    const size = parseFloat(s.fontSize), need = size >= 24 ? 3 : 4.5
    const r = T.ratio(T.over(fg, bg), bg)
    const row = { where, ...T.describe(el), size, colour: T.hex(T.over(fg, bg)), background: T.hex(bg), ratio: +r.toFixed(2), need }
    const benign = image && (/^linear-gradient\(to (right|left), rgb\(255, 255, 255\)/.test(image) || /^repeating-linear-gradient\(135deg, color\(srgb 0\.08/.test(image))
    if (image && !benign) unresolved.push({ ...row, image: image.slice(0, 60) })
    else if (r < need) textFailures.push(benign ? { ...row, onBaseUnderShade: true } : row)
  }
  const colours = [...found.values()]
  return { nonToken: colours.filter(c => !c.token), tokenFills: [...new Set(colours.filter(c => c.token && c.property === 'background-color').map(c => c.token))], textFailures, unresolved: unresolved.slice(0, 20), unresolvedCount: unresolved.length }
}

function fonts() {
  const body = getComputedStyle(document.body).fontFamily
  const mono = [...document.querySelectorAll('main .key, main .font-mono')].filter(e => e.getBoundingClientRect().width > 0).map(e => getComputedStyle(e).fontFamily)
  return { body, bodyPlex: body.startsWith('"IBM Plex Sans"'), monoSamples: mono.length, monoAllPlex: mono.every(f => f.startsWith('"IBM Plex Mono"')),
    loaded: [...document.fonts].filter(f => f.status === 'loaded').map(f => `${f.family} ${f.weight}`) }
}

function pills() {
  const T = window.__T
  const pairs = { ok: ['#386953', '#eaf5ef'], warn: ['#7a5817', '#fff4db'], bad: ['#b42332', '#fff0f1'], idle: ['#59606c', '#f1f2f4'], done: ['#37589a', '#edf2fd'], work: ['#574e87', '#f0effa'] }
  const symbols = { ok: '●', warn: '▲', bad: '■', idle: '○', done: '✓', work: '◐' }
  return [...document.querySelectorAll('main .semantic-pill, [data-visual-project-header] .semantic-pill')].filter(T.visible).map(el => {
    const tone = el.dataset.semanticTone, s = getComputedStyle(el)
    const colour = T.hex(T.parse(s.color)), bg = T.hex(T.over(T.parse(s.backgroundColor), T.background(el.parentElement).bg))
    const text = el.textContent.trim()
    return { tone, text: text.slice(0, 40), colour, bg, valuesMatch: colour === pairs[tone]?.[0] && bg === pairs[tone]?.[1], symbol: text.startsWith(symbols[tone]), hasText: /[A-Za-z0-9]/.test(text) }
  })
}

function countCards() {
  const T = window.__T
  const strong = { blue: '#2f6fe0', violet: '#6a52d1', red: '#d6334a', amber: '#c77d0a', green: '#18915f', teal: '#0f8a96' }
  const tint = { blue: '#e8f0fe', violet: '#efebfd', red: '#fde9ec', amber: '#fdf1dc', green: '#e3f5ec', teal: '#def3f4' }
  return [...document.querySelectorAll('main [data-count-card]')].filter(T.visible).map(el => {
    const tone = [...el.classList].find(c => c.startsWith('count-card--') && c !== 'count-card--link')?.slice(12)
    const value = el.querySelector('.count-card__value'), icon = el.querySelector('.count-card__icon')
    const figure = value?.textContent.trim() ?? ''
    const numeric = /^\d+$/.test(figure) ? Number(figure) : null
    const label = el.querySelector('.count-card__label')?.textContent.trim()
    const name = (el.getAttribute('aria-labelledby') ?? '').split(/\s+/).filter(Boolean).map(id => { const n = document.getElementById(id); return n ? (n.getAttribute('aria-label') || n.textContent).trim().replace(/\s+/g, ' ') : '' }).join(' ')
    const where = el.querySelector('.count-card__where')?.textContent.trim().replace(/\s+/g, ' ')
    const valueColour = value ? T.hex(T.parse(getComputedStyle(value).color)) : null
    const expectedValue = tone === 'red' && !(numeric > 0) ? '#161a23' : strong[tone]
    const meta = el.querySelector('.count-card__meta')?.textContent.trim() ?? null
    const links = el.tagName === 'A' ? [el] : [...el.querySelectorAll('a[href]')]
    return { tone, label, figure, numeric, accessibleName: name, nameHasLabelFigureWhere: !!label && name.includes(label) && name.includes(figure.replace(/\s+/g, ' ')) && (!where || name.includes(where)),
      tag: el.tagName.toLowerCase(), href: el.getAttribute('href'), links: links.map(a => ({ href: a.getAttribute('href'), text: a.textContent.trim() })),
      nestedLinks: el.tagName === 'A' ? el.querySelectorAll('a, button').length : [...el.querySelectorAll('a')].filter(a => a.querySelector('a, button')).length,
      valueColour, expectedValue, valueColourOk: valueColour === expectedValue,
      iconBg: icon ? T.hex(T.parse(getComputedStyle(icon).backgroundColor)) : null, iconColour: icon ? T.hex(T.parse(getComputedStyle(icon).color)) : null,
      iconOk: !!icon && T.hex(T.parse(getComputedStyle(icon).backgroundColor)) === tint[tone] && T.hex(T.parse(getComputedStyle(icon).color)) === strong[tone],
      meta, alertOk: tone !== 'red' || (numeric > 0 ? !!meta && meta.includes('■') && /Needs attention/.test(meta) : !meta) }
  })
}

function lanes() {
  const T = window.__T
  const expected = { 'To Do': '#94a0b4', 'In Progress': '#6a52d1', Review: '#f0a42b', Done: '#2f6fe0' }
  const fillOf = el => { const s = getComputedStyle(el); return el instanceof SVGElement ? s.fill : s.backgroundImage !== 'none' ? s.backgroundImage : s.backgroundColor }
  const check = (lane, raw) => lane === 'On Hold' ? /repeating-linear-gradient|url\(/.test(raw) : T.hex(T.parse(raw)) === expected[lane]
  const rows = []
  for (const el of document.querySelectorAll('main [data-home-donut-lane], main [data-home-lane], main .home-chart-swatch')) {
    const legend = el.parentElement?.textContent.trim().replace(/\s*\d+$/, '').replace('Review and revision', 'Review')
    const raw = fillOf(el)
    const capacity = { Planned: /15, 138, 150/, Free: /222, 243, 244/, 'Time off': /repeating-linear-gradient\(135deg, rgb\(148, 160, 180\)/, 'Non-working': /repeating-linear-gradient/ }
    const role = !el.dataset.homeDonutLane && !el.dataset.homeLane && Object.keys(capacity).find(k => legend?.startsWith(k))
    if (role) { rows.push({ surface: 'Home capacity legend', lane: role, name: legend, fill: raw.slice(0, 60), ok: capacity[role].test(raw) }); continue }
    const lane = el.dataset.homeDonutLane ?? el.dataset.homeLane ?? legend
    rows.push({ surface: el.dataset.homeDonutLane ? 'Home donut' : el.dataset.homeLane ? 'Home bars' : 'Home bar legend', lane, name: el.closest('a')?.getAttribute('aria-label') ?? null, fill: raw.slice(0, 60), ok: check(lane, raw) })
  }
  for (const el of document.querySelectorAll('main [data-board-lane]')) {
    const lane = { 'Not Started': 'To Do', 'To Do': 'To Do', 'In Progress': 'In Progress', Review: 'Review', Done: 'Done', 'On Hold': 'On Hold' }[el.dataset.boardLane] ?? el.dataset.boardLane
    const s = getComputedStyle(el)
    rows.push({ surface: 'Board column', lane, name: el.getAttribute('aria-label'), fill: lane === 'On Hold' ? s.backgroundImage.slice(0, 60) : s.borderTopColor, ok: lane === 'On Hold' ? /repeating-linear-gradient/.test(s.backgroundImage) : T.hex(T.parse(s.borderTopColor)) === expected[lane] })
  }
  for (const el of document.querySelectorAll('main [data-planner-away]')) {
    const before = getComputedStyle(el, '::before').backgroundImage
    rows.push({ surface: 'Planner time away', lane: 'Time off', name: el.textContent.trim(), fill: before.slice(0, 60), ok: /repeating-linear-gradient/.test(before) && /Time off/.test(el.textContent) })
  }
  return rows
}

function header() {
  const T = window.__T
  const h = document.querySelector('[data-visual-project-header]'), hb = h.getBoundingClientRect()
  const inside = el => { if (!el || !T.visible(el)) return false; const r = el.getBoundingClientRect(); return r.left >= hb.left - 1 && r.right <= hb.right + 1 && r.top >= hb.top - 1 && r.bottom <= hb.bottom + 1 }
  const shown = el => { if (!el || !T.visible(el)) return 0; let r = el.getBoundingClientRect(), left = r.left, right = r.right
    for (let n = el.parentElement; n && n !== h.parentElement; n = n.parentElement) { const s = getComputedStyle(n); if (s.overflowX !== 'visible') { const c = n.getBoundingClientRect(); left = Math.max(left, c.left); right = Math.min(right, c.right) } }
    return Math.max(0, Math.round(right - left)) }
  const name = h.querySelector('h1'), number = h.querySelector('.visual-project-number')
  const pills = [...h.querySelectorAll('.visual-project-main .semantic-pill')]
  const meta = h.querySelector('.visual-project-meta'), metaSpans = meta ? [...meta.children] : []
  const pm = meta?.querySelector('[data-person-panel-trigger]') ?? metaSpans[0]
  const ns = getComputedStyle(name)
  const overflowing = [...h.querySelectorAll('*')].filter(e => T.visible(e) && !e.closest('[role=tooltip]') && e.getBoundingClientRect().bottom > hb.bottom + 1).map(T.describe).slice(0, 3)
  return { height: Math.round(hb.height), width: Math.round(hb.width), within72: hb.height <= 72 && overflowing.length === 0, overflowing,
    number: { text: number?.textContent, mono: getComputedStyle(number).fontFamily.startsWith('"IBM Plex Mono"'), shown: shown(number), full: number ? Math.round(number.scrollWidth) : 0 },
    name: { lines: Math.round(name.getBoundingClientRect().height / parseFloat(ns.lineHeight)), whiteSpace: ns.whiteSpace, ellipsis: ns.textOverflow === 'ellipsis', truncated: name.scrollWidth > name.clientWidth, shown: shown(name), title: name.getAttribute('title')?.length, focusable: name.tabIndex >= 0 },
    pills: pills.map(p => ({ text: p.textContent.trim(), inside: inside(p), shown: shown(p), full: Math.round(p.scrollWidth) })),
    meta: metaSpans.map(s => ({ text: s.textContent.trim(), shown: shown(s), full: Math.round(s.scrollWidth), clipped: s.scrollWidth > s.clientWidth + 1 })),
    pmName: pm ? { text: pm.textContent.trim(), shown: shown(pm), full: Math.round(pm.scrollWidth) } : null,
    docOverflow: document.documentElement.scrollWidth > innerWidth + 1 }
}

// ---- destination counts ------------------------------------------------------------------------------------------
async function destinationCount(page, href) {
  const url = new URL(href, base)
  return page.evaluate(path => {
    const vis = el => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden' }
    const rows = [...document.querySelectorAll('main tbody tr')].filter(vis)
    const keys = re => new Set(rows.flatMap(r => r.innerText.match(re) ?? [])).size
    if (/^\/projects\/[^/]+\/tasks$|^\/tasks$/.test(path)) return { method: 'distinct task keys', count: keys(/\b[A-Z]+-\d{3}-T\d{4}\b/g) }
    if (/\/deliverables$/.test(path)) return { method: 'distinct deliverable keys', count: keys(/\b[A-Z]+-\d{3}-D\d{3}\b/g) }
    if (path === '/projects' || path === '/portfolio') return { method: 'visible rows with a project link', count: rows.filter(r => r.querySelector('a[href^="/projects/"]')).length }
    if (path === '/home') { const s = document.querySelector('#h-deadlines')?.closest('section') ?? document.querySelector('main'); return { method: 'deadline rows', count: [...s.querySelectorAll('li, tbody tr')].filter(vis).filter(r => /\b[A-Z]+-\d{3}(-[TDM]\d+)?\b/.test(r.innerText)).length } }
    if (path === '/team') return { method: 'visible person rows', count: rows.filter(r => r.querySelectorAll('td').length > 1).length }
    if (path === '/staff') {
      const table = document.querySelector('#staff-people table, main table')
      if (!table) return { method: 'visible person rows (none: empty state)', count: 0, sums: { Projects: 0, 'Stalled reviews': 0 } }
      const heads = [...table.querySelectorAll('thead th')].map(th => th.innerText.trim())
      const people = rows.filter(r => r.querySelectorAll('td').length > 1), cell = (r, name) => r.children[heads.indexOf(name)]?.innerText.trim()
      return { method: 'visible person rows', count: people.length, sums: { Projects: people.reduce((n, r) => n + (Number(cell(r, 'Projects')) || 0), 0), 'Stalled reviews': people.reduce((n, r) => n + (Number(cell(r, 'Stalled reviews')) || 0), 0) } }
    }
    if (path === '/time') return { method: 'sum of each row total (h)', count: rows.reduce((n, r) => n + (Number((r.lastElementChild?.innerText.match(/([\d.]+)\s*h/) ?? [])[1]) || 0), 0) }
    return { method: 'visible data rows', count: rows.filter(r => r.querySelectorAll('td').length > 1).length }
  }, url.pathname)
}

;(async () => {
  const setup = await fixtures()
  const settings = await api('jordan', '/admin/settings')
  const originals = FLAGS.map(name => [name, settings.find(x => x.key === `flag.${name}`)?.value])
  for (const name of FLAGS) await api('jordan', `/admin/settings/flag.${name}`, 'PUT', { value: true })
  const task = (await api('priya', `/projects/${(await api('priya', '/projects/SYN-104')).id}/tasks`)).items?.[0] ?? null
  const report = { base, scope: 'Synthetic local API, every 052/053 flag on; automated measurement, not hosted or human acceptance', setup, bandOrder: [], header: [], cardRows: [], inputs: [], countCards: [], lanes: [], pills: [], colours: [], fonts: [], pageErrors: [], externalFonts: [] }
  const browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH, headless: true })
  const tools = pageTools.toString()
  const open = async (who, viewport = { width: 1440, height: 900 }) => {
    const context = await browser.newContext({ viewport, reducedMotion: 'reduce' })
    await context.addInitScript(w => { sessionStorage.setItem('hub.devUser', `${w}@hub.test`); sessionStorage.setItem('hub.signedIn', '1') }, who)
    const page = await context.newPage(); page.setDefaultTimeout(15000)
    page.on('pageerror', e => report.pageErrors.push({ who, url: page.url(), message: e.message }))
    page.on('request', r => { if (r.resourceType() === 'font' && new URL(r.url()).origin !== new URL(base).origin) report.externalFonts.push(r.url()) })
    return { context, page }
  }
  const go = async (page, route) => {
    await page.goto(`${base}${route}`, { waitUntil: 'domcontentloaded' }); await page.locator('main').waitFor()
    await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {}); await page.evaluate(() => document.fonts.ready); await page.waitForTimeout(300)
    await page.evaluate(`window.__T = (${tools})()`)
  }
  const tokenNames = ['--page', '--side', '--card', '--raised', '--ink', '--ink-2', '--ink-3', '--line', '--line-2', '--line-3', '--input', '--navy', '--brand', '--brand-tint', '--on-navy-focus',
    ...['blue', 'violet', 'red', 'amber', 'green', 'teal'].flatMap(x => [`--metric-${x}`, `--metric-${x}-tint`]), '--lane-todo', '--lane-progress', '--lane-review', '--lane-done', '--hatch-strong', '--hatch-light',
    ...['ok', 'warn', 'critical', 'idle', 'complete', 'work'].flatMap(x => [`--pill-${x}`, `--pill-${x}-bg`]), ...['coral', 'mint', 'rose', 'cyan', 'lime'].map(x => `--project-${x}`), '--action-hover']
  const reportCode = (await api('priya', '/reports'))[0]?.code
  const routes = {
    priya: ['/my-work', '/home', '/boards', '/tasks', '/timeline', '/files', '/team', '/calendar', '/projects', '/portfolio', '/workload', '/planner', '/vacation', '/time', '/reports',
      ...(reportCode ? [`/reports/${reportCode}`] : []), '/search', '/notifications', '/preferences', '/templates', ...(task ? [`/tasks/${task.id}`] : []),
      ...['dashboard', 'coordination', 'readiness', 'handoffs', 'reviews', 'changes', 'submissions', 'allocations', 'design-basis', 'tasks', 'board', 'milestones', 'timeline',
        'deliverables', 'decisions', 'risks', 'issues', 'meetings', 'files', 'calendar', 'team', 'activity', 'settings'].map(x => `/projects/SYN-104/${x}`)],
    sam: ['/staff'],
    jordan: ['/admin', '/admin/users', '/admin/settings', '/admin/activity', '/admin/operations', '/admin/holidays'],
  }
  const census = ['/home', '/projects/SYN-104/tasks', '/workload', '/planner']
  try {
    for (const [who, list] of Object.entries(routes)) {
      const { context, page } = await open(who)
      for (const route of list) {
        await go(page, route)
        report.bandOrder.push({ who, route, url: new URL(page.url()).pathname, ...await page.evaluate(bandOrder) })
        report.inputs.push(...(await page.evaluate(inputBoundaries)).map(r => ({ who, route, ...r })))
        report.pills.push(...(await page.evaluate(pills)).map(r => ({ who, route, ...r })))
        report.lanes.push(...(await page.evaluate(lanes)).map(r => ({ who, route, ...r })))
        report.colours.push({ who, route, ...await page.evaluate(colourCensus, tokenNames) })
        if (census.includes(route)) report.fonts.push({ route, ...await page.evaluate(fonts) })
        console.log(JSON.stringify({ who, route, band: report.bandOrder.at(-1).band }))
      }
      await context.close()
    }
    // Project header with a 120-character name, desktop and phone (AC-VSN-05).
    for (const viewport of [{ width: 1440, height: 900 }, { width: 375, height: 812 }]) {
      const { context, page } = await open('priya', viewport)
      for (const route of ['/projects/SYN-120/dashboard', '/projects/SYN-120/tasks']) {
        await go(page, route)
        const measured = await page.evaluate(header)
        await page.locator('[data-visual-project-header] h1').focus()
        const tip = await page.locator('[role=tooltip]').first().textContent({ timeout: 3000 }).catch(() => null)
        report.header.push({ viewport: viewport.width, route, ...measured, tooltipOnFocus: tip === LONG_NAME, nameLength: LONG_NAME.length })
        if (viewport.width === 375) await page.screenshot({ path: output.replace(/\.json$/, '-header-375.png'), clip: { x: 0, y: 0, width: 375, height: 220 } })
      }
      await context.close()
    }
    // Cards per row (FR-VSN-15, AC-VSN-10): up to six on desktop, three below 1180 px, two below 768 px; equal sizes.
    for (const viewport of [{ width: 1440, height: 900 }, { width: 1000, height: 900 }, { width: 375, height: 812 }]) {
      for (const [who, route] of [['priya', '/home'], ['priya', '/portfolio'], ['priya', '/projects/SYN-101/dashboard'], ['sam', '/staff']]) {
        const { context, page } = await open(who, viewport)
        await go(page, route)
        report.cardRows.push({ viewport: viewport.width, who, route, ...await page.evaluate(() => {
          const boxes = [...document.querySelectorAll('main [data-count-card]')].map(el => el.getBoundingClientRect()).filter(r => r.width > 0)
          const tops = [...new Set(boxes.map(r => Math.round(r.top)))].sort((a, b) => a - b)
          const perRow = tops.map(top => boxes.filter(r => Math.round(r.top) === top).length)
          const widths = boxes.map(r => Math.round(r.width)), heights = tops.map(top => boxes.filter(r => Math.round(r.top) === top).map(r => Math.round(r.height)))
          return { cards: boxes.length, perRow, equalWidths: Math.max(...widths) - Math.min(...widths) <= 1, equalHeightsPerRow: heights.every(h => Math.max(...h) - Math.min(...h) <= 1), docOverflow: document.documentElement.scrollWidth > innerWidth + 1 }
        }) })
        await context.close()
      }
    }
    // Count cards: colours, one link, accessible name, definition on focus, destination count (AC-VSN-02, 07).
    for (const [who, route] of [['priya', '/home'], ['priya', '/portfolio'], ['priya', '/projects/SYN-101/dashboard'], ['priya', '/projects/SYN-104/dashboard'], ['priya', '/time'], ['sam', '/staff']]) {
      const { context, page } = await open(who)
      await go(page, route)
      const cards = await page.evaluate(countCards)
      for (const [index, card] of cards.entries()) {
        const target = page.locator('main [data-count-card]').nth(index)
        const focusable = card.tag === 'a' ? target : target.locator('button').first()
        await page.mouse.move(0, 0); await focusable.focus()
        card.definitionOnFocus = await page.locator('[role=tooltip]').first().textContent({ timeout: 2500 }).catch(() => null)
        await page.keyboard.press('Escape')
        for (const link of card.links) {
          if (card.tag === 'a' && route === '/home' && card.tone === 'red') {
            await target.focus(); await page.keyboard.press('Enter'); await page.waitForURL(u => u.pathname !== '/home').catch(() => {})
            await page.locator('main').waitFor(); await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {}); await page.waitForTimeout(500)
            link.openedBy = 'Enter'
          } else await go(page, link.href)
          Object.assign(link, await destinationCount(page, link.href))
          await go(page, route)
        }
        report.countCards.push({ who, route, ...card })
      }
      await context.close()
    }
  } finally {
    await browser.close()
    for (const [name, value] of originals) if (value !== undefined) await api('jordan', `/admin/settings/flag.${name}`, 'PUT', { value }).catch(() => {})
    fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n')
  }
  console.log(JSON.stringify({ routes: report.bandOrder.length, headers: report.header.length, inputs: report.inputs.length, cards: report.countCards.length, lanes: report.lanes.length, pills: report.pills.length, pageErrors: report.pageErrors.length }))
})().catch(error => { console.error(error); process.exitCode = 1 })
