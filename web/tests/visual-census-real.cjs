// Required 17-screen census against an isolated synthetic local API, never an operational database.
// Creates fresh SYN-101..103 fixtures. Run once on an empty disposable database; no seed guard is bypassed.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const { randomUUID, createHash } = require('node:crypto')
const path = require('node:path')
const { chromium } = require('playwright')
const { census, LIMIT } = require('./clutter.cjs')

const base = process.env.TUESDAY_CENSUS_API_BASE
const output = process.env.TUESDAY_CENSUS_OUTPUT
const partMode = process.env.TUESDAY_CENSUS_PARTS ?? 'visual-home'
assert.ok(['visual-home', 'all'].includes(partMode), 'TUESDAY_CENSUS_PARTS must be visual-home or all')
const flagNames = partMode === 'all'
  ? ['visual_system', 'screen_patterns', 'home_overview', 'work_screens', 'project_screens', 'capacity_screens', 'register_screens', 'people_screens']
  : ['visual_system', 'home_overview']
assert.equal(process.env.TUESDAY_REAL_QA, 'isolated-synthetic')
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname))
assert.ok(output, 'TUESDAY_CENSUS_OUTPUT must name the JSON evidence file')
const api = async (who, path, method = 'GET', body, expected = 200) => {
  const response = await fetch(`${base}/api/v1${path}`, { method, headers: { 'X-Dev-User': `${who}@hub.test`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text()
  assert.equal(response.status, expected, `${method} ${path}: ${text}`)
  return text ? JSON.parse(text) : null
}
const flag = (name, value) => api('jordan', `/admin/settings/flag.${name}`, 'PUT', { value })

;(async () => {
  assert.equal((await (await fetch(`${base}/api/v1/config`)).json()).authMode, 'Development')
  const ref = await api('jordan', '/reference'), users = await api('jordan', '/admin/users')
  const uid = name => users.find(u => u.email === `${name}@hub.test`).id
  const civil = ref.disciplines.find(d => d.code === 'CIV').id
  const today = (await api('priya', '/me')).settings.today
  const week = new Date(`${today}T00:00:00Z`); week.setUTCDate(week.getUTCDate() - (week.getUTCDay() + 6) % 7)
  const monday = week.toISOString().slice(0, 10)
  const previousDay = new Date(`${today}T00:00:00Z`); previousDay.setUTCDate(previousDay.getUTCDate() - 1)
  const through = new Date(week); through.setUTCDate(through.getUTCDate() + 11)
  const end = through.toISOString().slice(0, 10)
  if (process.env.TUESDAY_CENSUS_REUSE_FIXTURES === '1') {
    for (let index = 1; index <= 3; index++) {
      const project = await api('jordan', `/projects/SYN-10${index}`)
      assert.equal(project.name, `Synthetic census project ${index}`, 'Reuse requires this script’s exact synthetic fixture')
      assert.equal((await api('jordan', `/projects/${project.id}/tasks`)).totalCount, index === 1 ? 6 : 4)
    }
  } else {
  for (let index = 1; index <= 3; index++) {
    const p = await api('priya', '/projects', 'POST', {
      projectNumber: `SYN-10${index}`, name: `Synthetic census project ${index}`, clientId: ref.clients[0].id, officeId: ref.offices[0].id,
      startDate: monday, targetCompletionDate: end,
      disciplines: [{ disciplineId: civil, leadUserId: uid('marc') }],
      members: ['alex', 'sam', 'jill'].map(name => ({ userId: uid(name), roles: ['TeamMember'], disciplineId: civil })),
    }, 201)
    let detail = await api('priya', `/projects/${p.id}`)
    await api('priya', `/projects/${p.id}/transition`, 'POST', { toStatus: 'Active', rowVersion: detail.rowVersion })
    detail = await api('priya', `/projects/${p.id}`)
    const milestone = await api('priya', `/projects/${p.id}/milestones`, 'POST', { name: 'Synthetic census coordination milestone', milestoneType: 'Client Workshop', date: end }, 201)
    const deliverable = await api('priya', `/projects/${p.id}/deliverables`, 'POST', {
      name: 'Synthetic census drawing package', projectDisciplineId: detail.disciplines[0].id, deliverableTypeId: ref.deliverableTypes[0].id,
      ownerId: uid('alex'), milestoneId: milestone.id, startDate: monday, dueDate: today, requiresReview: false,
    }, 201)
    const tasks = []
    for (let task = 1; task <= 4; task++) tasks.push(await api('priya', `/projects/${p.id}/tasks`, 'POST', {
      name: `Synthetic census task ${index}.${task}`, projectDisciplineId: detail.disciplines[0].id, deliverableId: deliverable.id,
      assigneeId: uid(task % 2 ? 'alex' : 'jill'), estimatedHours: 8, startDate: monday, dueDate: today, requiresReview: false,
    }, 201))
    await api('priya', `/projects/${p.id}/links`, 'POST', { title: 'Synthetic census document pointer', url: 'https://docs.example.test/census.pdf', linkType: 'Other' }, 201)
    if (index === 1) await api('priya', `/projects/${p.id}/allocations`, 'POST', {
      requestId: randomUUID(), personId: uid('alex'), purpose: 'Production', fromDate: monday, throughDate: monday,
      plannedHours: 8, days: [{ workDate: monday, hours: 8 }], links: [{ workType: 'Task', workId: tasks[0].id, workDate: monday }],
    })
    if (index === 1) for (const who of ['priya', 'sam']) await api('priya', `/projects/${p.id}/tasks`, 'POST', {
      name: `Synthetic census assigned work for ${who}`, projectDisciplineId: detail.disciplines[0].id,
      assigneeId: uid(who), estimatedHours: 4, startDate: monday < previousDay.toISOString().slice(0, 10) ? monday : previousDay.toISOString().slice(0, 10),
      dueDate: previousDay.toISOString().slice(0, 10), requiresReview: false,
    }, 201)
  }
  await api('sam', '/planning/entries', 'POST', { personId: uid('alex'), hoursPerWeek: 8, startWeek: monday, endWeek: monday,
    label: 'Synthetic census planning entry', sourceCategory: 'Training', confidence: 'Expected', visibility: 'Published' }, 201)
  }

  if (process.env.TUESDAY_CENSUS_SEED_ONLY === '1') {
    const seeded = { seedOnly: true, scope: 'Synthetic fixtures created or verified only; no browser acceptance was run.', projects: ['SYN-101', 'SYN-102', 'SYN-103'] }
    fs.writeFileSync(output, JSON.stringify(seeded, null, 2) + '\n')
    console.log(JSON.stringify(seeded))
    return
  }
  const screens = ['/home', '/my-work', '/boards', '/projects', '/tasks', '/calendar', '/portfolio', '/workload', '/planner', '/reports', '/team',
    '/projects/SYN-101/dashboard', '/projects/SYN-101/tasks', '/projects/SYN-101/deliverables', '/projects/SYN-101/timeline', '/projects/SYN-101/allocations', '/projects/SYN-101/coordination']
  const extras = { '/home': 'section, [data-accent].rounded-lg', '/my-work': 'section li', '/boards': 'section', '/calendar': '[role=region]', '/reports': 'li',
    '/projects/SYN-101/timeline': '[role=region]', '/projects/SYN-101/coordination': 'section.rounded-lg' }
  const digest = file => createHash('sha256').update(fs.readFileSync(path.resolve(__dirname, '../../', file))).digest('hex')
  const requestedScreens = (process.env.TUESDAY_CENSUS_ROUTES ?? '').split(',').filter(Boolean)
  assert.ok(requestedScreens.every(route => screens.includes(route)), 'Route filter must name a standard census screen')
  const selectedScreens = requestedScreens.length ? screens.filter(route => requestedScreens.includes(route)) : screens
  const report = { base, screens: selectedScreens, partMode, flagNames, scope: 'Real local API, new synthetic fixtures; not hosted or human acceptance',
    buildEvidence: { label: process.env.TUESDAY_CENSUS_BUILD_LABEL ?? 'Unlabelled local build', apiAssemblySha256: digest('src/Hub.Api/bin/Debug/net10.0/Hub.Api.dll'), uiIndexSha256: digest('src/Hub.Api/wwwroot/index.html') },
    rows: [], pageErrors: [], externalFonts: [] }
  const browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH, headless: true })
  try {
    for (const enabled of [false, true]) {
      for (const name of flagNames) await flag(name, enabled)
      for (const who of ['priya', 'sam']) {
        const context = await browser.newContext({ viewport: { width: 1600, height: 900 }, reducedMotion: 'reduce' })
        await context.addInitScript(who => { sessionStorage.setItem('hub.devUser', `${who}@hub.test`); sessionStorage.setItem('hub.signedIn', '1') }, who)
        const page = await context.newPage(); page.setDefaultTimeout(15000)
        page.on('pageerror', error => report.pageErrors.push({ who, enabled, url: page.url(), message: error.message }))
        page.on('request', request => { if (request.resourceType() === 'font' && new URL(request.url()).origin !== new URL(base).origin) report.externalFonts.push(request.url()) })
        for (const route of selectedScreens) {
          await page.goto(`${base}${route}`, { waitUntil: 'domcontentloaded' })
          await page.locator('main').waitFor()
          await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {})
          // DevTools evaluation keeps the application's real CSP intact; no unsafe-inline exception is introduced.
          await page.evaluate(fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8'))
          const metrics = await page.evaluate(census, extras[route] ?? null)
          const violations = await page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations
            .filter(v => ['serious', 'critical'].includes(v.impact)).map(v => ({ id: v.id, impact: v.impact, description: v.description, nodes: v.nodes.map(n => ({ target: n.target, summary: n.failureSummary })) })))
          const row = { enabled, who, route, metrics, violations }; report.rows.push(row)
          fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n')
          console.log(JSON.stringify({ enabled, who, route, firstDataY: metrics.firstDataY, serious: violations.length }))
        }
        await context.close()
      }
    }
  } finally { await browser.close(); fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n') }
  const findings = report.rows.filter(r => r.violations.length || r.metrics.firstDataY == null || r.metrics.firstDataY > LIMIT.firstDataY)
  console.log(JSON.stringify({ screens: selectedScreens.length, observations: report.rows.length, findings: findings.length, pageErrors: report.pageErrors.length, externalFonts: report.externalFonts.length }))
  if (findings.length || report.pageErrors.length || report.externalFonts.length) process.exitCode = 1
})().catch(error => { console.error(error); process.exitCode = 1 })
