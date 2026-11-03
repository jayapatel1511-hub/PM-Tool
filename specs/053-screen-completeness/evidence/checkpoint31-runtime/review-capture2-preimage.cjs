// Root-owned separate combined-design capture; original acceptance driver stays intact.
// AC-SCR-67/68/84: finite actual-SPA synthetic browser evidence; persistence/privacy are provider cases.
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const path = require('node:path')
const { newMockPage, me, project, links, comments } = require(path.join(process.cwd(), 'tests', 'platform-fixtures.cjs'))
const { coordinationConsequenceFixture } = require(path.join(process.cwd(), 'tests', 'coordination-consequence-agenda.fixture.cjs'))
const { taskDetail } = require(path.join(process.cwd(), 'tests', 'tasks.fixture.cjs'))
const { taskDependencies } = require(path.join(process.cwd(), 'tests', 'dependencies.fixture.cjs'))
const { readinessWindow } = require(path.join(process.cwd(), 'tests', 'readiness.fixture.cjs'))
const { commitmentRow, commitmentList, commitmentDetail } = require(path.join(process.cwd(), 'tests', 'weekly-commitments.fixture.cjs'))
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
;(async () => {
  const original = { ...me.flags }, f = coordinationConsequenceFixture(project, me.id)
  const { page, browser, mock } = await newMockPage(base, { events: false }), unknown = [], writes = [], errors = [], cases = [], reads = [], screenshots = []
  const taskIds = new Map(f.tasks.map(task => [task.id, task]))
  let large = false, agendaFailure = false, linksFailure = false, unavailable = false
  page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
  const screenshot = async name => { if (!process.env.TUESDAY_EVIDENCE_DIR) return
    await fs.mkdir(process.env.TUESDAY_EVIDENCE_DIR, { recursive: true }); const file = path.join(process.env.TUESDAY_EVIDENCE_DIR, `s38-${name}.png`)
    await fs.writeFile(file, await page.screenshot({ fullPage: false }), { flag: 'wx' }); screenshots.push(file)
  }
  const promise = commitmentRow(project.id, { id: f.proposed, key: 'G59-P001', targetId: f.tasks[0].id, performerId: me.id, weekStart: f.from,
    targetDate: '2026-11-12', intendedOutput: 'Synthetic proposed layout', completionCriteria: 'Independently checked', state: 'Proposed', rowVersion: 1 })
  try {
    me.flags = Object.fromEntries(Object.keys(original).map(key => [key, false])); Object.assign(me.flags, { register_screens: true, screen_patterns: true, visual_system: true })
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write'], { origin: base })
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), p = url.pathname.replace('/api/v1/', ''), method = request.method()
      if (method !== 'GET') {
        if (p === 'me/sign-in') return route.fallback()
        writes.push(`${method} ${p}`); unknown.push(`${method} ${p}`); return route.fulfill({ status: 409, json: { detail: 'S38 does not arm any business write' } })
      }
      reads.push(`${p}${url.search}`)
      if (p === `projects/${project.id}/coordination/readiness-agenda`) {
        assert.equal(me.flags.register_screens, true); assert.equal(url.searchParams.get('from'), f.from); assert.equal(url.searchParams.get('to'), f.to)
        assert.deepEqual([...url.searchParams.keys()].sort(), ['constraintsPage', 'from', 'pageSize', 'proposalsPage', 'to'].sort())
        const cp = Number(url.searchParams.get('constraintsPage')), pp = Number(url.searchParams.get('proposalsPage')); assert.ok([1, 2].includes(cp)); assert.equal(pp, 1); assert.equal(url.searchParams.get('pageSize'), '50')
        return route.fulfill(agendaFailure ? { status: 503, json: { detail: 'Synthetic meeting Readiness refusal' } } : { json: f.agenda(cp, pp) })
      }
      if (p === `projects/${project.id}/handoffs/${f.h}/readiness-linked-tasks`) {
        assert.equal(me.flags.register_screens, true); assert.deepEqual([...url.searchParams.keys()].sort(), ['page', 'pageSize']); assert.equal(url.searchParams.get('pageSize'), '50')
        const pg = Number(url.searchParams.get('page')); assert.ok([1, 2].includes(pg))
        const data = f.linked(pg, large)
        if (unavailable) { data.summary = { ...data.summary, state: 'Unavailable', taskCount: null, directTaskCount: null, constraintLinkedTaskCount: null, datedTaskCount: null, firstRecordedDueDate: null, directReceiptSatisfied: null }; data.tasks = null }
        return route.fulfill(linksFailure ? { status: 403, json: { detail: 'Synthetic handoff relationship refusal' } } : { json: data })
      }
      if (p === `projects/${project.id}/coordination`) return route.fulfill({ json: f.weekly() })
      if (p === `projects/${project.id}/discipline-coordination`) {
        const data = f.projection(large); if (!me.flags.register_screens) for (const row of data.outgoing) delete row.readinessLinks
        return route.fulfill({ json: data })
      }
      if (p === `projects/${project.id}/team`) return route.fulfill({ json: { members: [] } })
      if (p === `projects/${project.id}/attention`) return route.fulfill({ json: { items: [], snoozed: 0, total: 0, truncated: false } })
      if (p === `projects/${project.id}/handoffs/options`) return route.fulfill({ json: f.options() })
      if (p === `projects/${project.id}/handoffs/${f.h}`) { const data = f.detail(large); if (!me.flags.register_screens) delete data.row.readinessLinks; return route.fulfill({ json: data }) }
      if (p === `projects/${project.id}/handoffs`) { const data = f.list(); if (!me.flags.register_screens) for (const row of data.items) delete row.readinessLinks; return route.fulfill({ json: data }) }
      if (p === `projects/${project.id}/readiness/window`) return route.fulfill({ json: readinessWindow({ from: f.from, to: f.to }) })
      if (p === `projects/${project.id}/weekly-commitments`) return route.fulfill({ json: commitmentList([]) })
      if (p === `projects/${project.id}/weekly-commitments/${f.proposed}`) return route.fulfill({ json: commitmentDetail(promise, []) })
      const task = taskIds.get(p.split('/')[1])
      if (task && p === `tasks/${task.id}`) return route.fulfill({ json: taskDetail({ task }) })
      if (task && p === `tasks/${task.id}/dependencies`) return route.fulfill({ json: taskDependencies() })
      const item = p.split('/')
      if (item[0] === 'items' && item[1] === 'Task' && taskIds.has(item[2]) && item.length === 4 && ['links', 'comments'].includes(item[3])) return route.fulfill({ json: item[3] === 'links' ? links : comments })
      const fallback = ['config', 'me', 'reference', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count',
        `projects/${project.id}`, `projects/${project.projectNumber}`, `projects/${project.id}/changes/options`, `projects/${project.id}/milestones`, `projects/${project.id}/deliverables`,
        `projects/${project.id}/tasks`, `projects/${project.id}/date-review`, `projects/${project.projectNumber}/tasks`]
      if (fallback.includes(p)) return route.fallback()
      unknown.push(`GET ${p}${url.search}`); return route.fulfill({ status: 501, json: { detail: `Unlisted S38 GET ${p}` } })
    })
    const root = `${base}/projects/${project.projectNumber}/coordination?from=2026-10-01&to=2026-12-01&sort=due`
    const meeting = () => page.locator('#wc-readinessAgenda')
    const linkRegion = () => page.getByRole('region', { name: 'Readiness-linked open tasks', exact: true })
    await page.setViewportSize({ width: 1440, height: 1100 })
    await page.goto(root)
    await linkRegion().getByText('Readiness-linked open tasks: 2', { exact: true }).waitFor()
    await page.getByRole('banner').first().waitFor()
    await screenshot('07-readiness-links-desktop')
    await meeting().getByRole('navigation', { name: 'Open constraints', exact: true }).getByRole('button', { name: 'Next', exact: true }).click()
    await meeting().getByText('Showing 1 of 51; page 2 of 2', { exact: true }).waitFor()
    await meeting().scrollIntoViewIfNeeded()
    const agendaFile = path.join(process.env.TUESDAY_EVIDENCE_DIR, 's38-08-readiness-agenda-desktop.png')
    await fs.writeFile(agendaFile, await meeting().screenshot(), { flag: 'wx' }); screenshots.push(agendaFile)
    await page.setViewportSize({ width: 375, height: 900 })
    await page.goto(root)
    await linkRegion().getByText('Readiness-linked open tasks: 2', { exact: true }).waitFor()
    await linkRegion().scrollIntoViewIfNeeded()
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    await screenshot('07-readiness-links-phone')
    await meeting().getByRole('navigation', { name: 'Open constraints', exact: true }).getByRole('button', { name: 'Next', exact: true }).click()
    await meeting().getByText('Showing 1 of 51; page 2 of 2', { exact: true }).waitFor()
    await meeting().scrollIntoViewIfNeeded()
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    await screenshot('08-readiness-agenda-phone')
    assert.deepEqual(writes, []); assert.deepEqual(unknown, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS_READONLY_VISUAL', screenshots, businessWrites: 0, profile: 'register_screens + screen_patterns + visual_system; finite synthetic HTTP', scope: 'Review framing only, not independent acceptance' }))
  } finally { me.flags = original; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
