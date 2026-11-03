// AC-SCR-67/68/84: finite actual-SPA synthetic browser evidence; persistence/privacy are provider cases.
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const path = require('node:path')
const { newMockPage, me, project, links, comments } = require('./platform-fixtures.cjs')
const { coordinationConsequenceFixture } = require('./coordination-consequence-agenda.fixture.cjs')
const { taskDetail } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { readinessWindow } = require('./readiness.fixture.cjs')
const { commitmentRow, commitmentList, commitmentDetail } = require('./weekly-commitments.fixture.cjs')
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
    me.flags = Object.fromEntries(Object.keys(original).map(key => [key, false])); Object.assign(me.flags, { register_screens: true, screen_patterns: true })
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
    await page.goto(root); await meeting().getByText('Synthetic overdue input constraint', { exact: false }).waitFor()
    await meeting().getByText('Constraints needed by 2026-11-15, including overdue', { exact: true }).waitFor()
    await meeting().getByText('Showing 50 of 51; page 1 of 2', { exact: true }).waitFor()
    await meeting().getByText('Synthetic removal owner', { exact: false }).first().waitFor()
    await linkRegion().getByText('Readiness-linked open tasks: 2', { exact: true }).waitFor()
    await linkRegion().getByText('First recorded due 2026-11-10 · 1 of 2 tasks dated', { exact: true }).waitFor()
    await linkRegion().getByText('Direct handoff receipt satisfied; constraints keep their own resolution', { exact: true }).waitFor()
    cases.push('AC67/68: main meeting window ignores separate lookahead URL; carryover, recorded performer, known partial dates and historical receipt facts are labelled honestly')
    await screenshot('desktop')

    const constraintPages = meeting().getByRole('navigation', { name: 'Open constraints', exact: true })
    agendaFailure = true; await constraintPages.getByRole('button', { name: 'Next', exact: true }).click()
    await meeting().getByRole('alert').filter({ hasText: 'Synthetic meeting Readiness refusal' }).waitFor()
    assert.equal(await meeting().getByText('Synthetic overdue input constraint', { exact: false }).count(), 0)
    const retry = meeting().getByRole('alert').filter({ hasText: 'Synthetic meeting Readiness refusal' }).getByRole('button', { name: 'Retry', exact: true })
    assert.equal(await retry.getAttribute('type'), 'button'); agendaFailure = false; await retry.click()
    await meeting().getByText('Synthetic last constraint', { exact: false }).waitFor(); await meeting().getByText('Showing 1 of 51; page 2 of 2', { exact: true }).waitFor()
    await meeting().getByText('Synthetic proposed layout', { exact: false }).waitFor(); assert.equal(new URL(page.url()).searchParams.get('readinessConstraintsPage'), '2')
    assert.equal(new URL(page.url()).searchParams.get('readinessProposalsPage'), null); assert.deepEqual(writes, [])
    cases.push('AC68: independent page2 current failure hides cached rows; explicit GET Retry preserves proposal page and sends no command')

    await page.getByRole('button', { name: 'Meeting mode', exact: true }).click()
    await page.waitForFunction(() => [...document.querySelectorAll('button[aria-pressed]')].some(button =>
      button.textContent?.trim() === 'Meeting mode' && button.getAttribute('aria-pressed') === 'true'), undefined, { timeout: 15000 })
    await page.getByRole('button', { name: 'Meeting mode', exact: true }).getAttribute('aria-pressed').then(value => assert.equal(value, 'true'))
    await page.getByRole('button', { name: 'More actions', exact: true }).click(); await page.getByRole('menuitem', { name: 'Copy summary', exact: true }).click()
    const copied = await page.evaluate(() => navigator.clipboard.readText())
    assert.ok(copied.includes('Meeting window: 2026-11-09–2026-11-15')); assert.ok(copied.includes('Showing 1 of 51; page 2 of 2')); assert.ok(copied.includes('G59-C051')); assert.ok(copied.includes('G59-P001'))
    assert.equal(copied.includes('G59-C001 '), false)
    await page.emulateMedia({ media: 'print' }); await meeting().getByText('Showing 1 of 51; page 2 of 2', { exact: true }).waitFor(); await screenshot('print-media-current-page'); await page.emulateMedia({ media: 'screen' })
    cases.push('AC68: real meeting toggle, clipboard summary and print-media content retain the same window/current pages and complete-or-paged disclosure')

    const proposalLink = meeting().getByRole('link', { name: 'G59-P001 · Synthetic proposed layout', exact: true })
    assert.equal(await proposalLink.getAttribute('href'), `/projects/${project.projectNumber}/readiness?from=${f.from}&to=${f.to}&panel=OutputCommitment:${f.proposed}`)
    await proposalLink.scrollIntoViewIfNeeded(); const sourceUrl = page.url()
    const sourceScroll = await page.locator('main#content').evaluate(content => ({ top: content.scrollTop, left: content.scrollLeft }))
    assert.ok(sourceScroll.top > 0, 'the source workspace really has a positive scroll position')
    await proposalLink.click(); await page.getByRole('dialog').getByText('Synthetic proposed layout', { exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('from'), f.from); assert.equal(await page.getByRole('dialog').getByRole('button', { name: 'Sign promise', exact: true }).count(), 0)
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(sourceUrl)
    await page.waitForFunction(expected => {
      const content = document.getElementById('content')
      return content && content.scrollTop > 0 && Math.abs(content.scrollTop - expected.top) <= 1 && Math.abs(content.scrollLeft - expected.left) <= 1
        && document.activeElement?.textContent?.includes('G59-P001')
    }, sourceScroll, { timeout: 15000 })
    cases.push('AC68: actual promise inspector opens exact recorded ID/window without chair Commit and naturally returns URL/page/scroll/focus')

    large = true; await page.reload(); await linkRegion().getByText('Readiness-linked open tasks: 57', { exact: true }).waitFor()
    await linkRegion().getByRole('button', { name: 'Show linked tasks', exact: true }).click()
    await linkRegion().getByText('Direct handoff target', { exact: true }).waitFor(); await linkRegion().getByText('Via constraint G59-C001', { exact: true }).waitFor()
    await linkRegion().getByRole('navigation', { name: 'Readiness-linked tasks', exact: true }).getByRole('button', { name: 'Next', exact: true }).click()
    await linkRegion().getByText('Showing 7 of 57; page 2 of 2', { exact: true }).waitFor()
    const selected = f.tasks[50], taskLink = linkRegion().getByRole('link', { name: `${selected.key} · ${selected.name}`, exact: true })
    await taskLink.scrollIntoViewIfNeeded(); const sourceTaskUrl = page.url(); await taskLink.click()
    await page.getByRole('dialog').getByText(selected.name, { exact: true }).waitFor(); await page.getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(sourceTaskUrl)
    await linkRegion().getByText('Showing 7 of 57; page 2 of 2', { exact: true }).waitFor(); await page.waitForFunction(key => document.activeElement?.textContent?.includes(key), selected.key)
    cases.push('AC67: more than50 linked tasks page without changing full-project total; exact direct/constraint provenance and page2 task return survive navigation naturally')

    // A fresh uncached source page receives an unavailable cohort, never a fabricated empty total.
    unavailable = true; await page.reload(); await linkRegion().getByText('Readiness links unavailable', { exact: true }).waitFor()
    assert.equal(await linkRegion().getByText('Readiness-linked open tasks: 0', { exact: true }).count(), 0); unavailable = false
    linksFailure = true; await page.reload(); await linkRegion().getByRole('alert').filter({ hasText: 'Synthetic handoff relationship refusal' }).waitFor()
    assert.equal(await linkRegion().getByText('Readiness-linked open tasks: 57', { exact: true }).count(), 0); linksFailure = false
    cases.push('AC67: unavailable/null and current403 never expose cached counts or false zero')

    await page.setViewportSize({ width: 375, height: 812 }); await page.goto(root)
    await meeting().getByText('Synthetic overdue input constraint', { exact: false }).waitFor(); assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)); await screenshot('375-agenda')
    me.flags.screen_patterns = false; await page.reload(); await meeting().getByText('Synthetic proposed layout', { exact: false }).waitFor(); await linkRegion().getByText('Readiness-linked open tasks: 57', { exact: true }).waitFor()
    cases.push('AC67/68:375px containment and register_screens remain independent of screen_patterns')
    me.flags.register_screens = false; const fromRead = reads.length; await page.reload()
    await page.getByRole('heading', { name: /Coordination/ }).first().waitFor(); assert.equal(await meeting().count(), 0); assert.equal(await linkRegion().count(), 0)
    assert.equal(reads.slice(fromRead).filter(p => p.includes('/readiness-agenda') || p.includes('/readiness-linked-tasks')).length, 0)
    cases.push('AC84: off hides all owned UI/read requests without changing existing coordination routes')
    assert.deepEqual(writes, []); assert.deepEqual(unknown, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS_SYNTHETIC_BROWSER_ONLY', cases, screenshots, writes, ownedReads: reads.filter(p => p.includes('/readiness-agenda') || p.includes('/readiness-linked-tasks')) }))
  } finally { me.flags = original; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
