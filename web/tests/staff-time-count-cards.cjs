// 052 Staff/Time adoption against synthetic current API cohorts. Server permissions,
// full visual census and native zoom remain separate root-owned acceptance.
const assert = require('node:assert/strict')
const { newMockPage, project } = require('./platform-fixtures.cjs')
const { meView, referenceData, referenceOffice, referenceDiscipline } = require('./me.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const { timeRow, timeView } = require('./time.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local reviewed app')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const viewer = id(2), office = id(400), civil = id(401), first = '2026-11-09', last = '2026-11-15'
const person = (n, projects, overdue, blocked, reviewsStalled) => ({ id: id(n), displayName: `Synthetic person ${n}`, jobTitle: null, officeId: office, isActive: true,
  projects, roles: { pm: 0, dl: n === 411 ? 1 : 0, team: 2, reviewer: 0, viewer: 0 }, open: overdue + blocked, overdue, blocked, blocking: 0, reviews: reviewsStalled + 1, reviewsStalled, deliverablesDue: 0, lastActivityAt: null })
// Person411 has three distinct visible projects, including a lead-only project.
// The membership expansion has only two rows; the Projects sum remains authoritative.
const people = [person(411, 3, 2, 1, 2), person(412, 2, 0, 2, 1), person(413, 1, 1, 0, 0), person(414, 1, 0, 0, 0)]
const entries = [1.25, 2.75].map((hours, index) => timeRow({ id: id(500 + index), projectId: project.id, userId: viewer, taskId: id(510),
  projectNumber: project.projectNumber, taskKey: 'SYN-CC-T1', taskName: 'Synthetic counted work', person: 'Synthetic viewer', workDate: first, hours, canEdit: false }))

;(async () => {
  const { page, browser } = await newMockPage(base, { events: false })
  const errors = [], staffReads = [], timeReads = [], assignmentReads = [], writes = [], saved = []
  let visual = true, capacity = false, zeroTime = false, holdWeek = false, releaseWeek, notifyWeek
  let weekStarted = new Promise(resolve => { notifyWeek = resolve })
  page.setDefaultTimeout(10000)
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  try {
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (!['GET', 'HEAD'].includes(request.method()) && path !== 'me/sign-in') writes.push(`${request.method()} ${path}`)
      if (path === 'me') return route.fulfill({ json: meView({ id: viewer, displayName: 'Synthetic viewer', email: 'count@hub.test', systemRoles: ['Supervisor'],
        capabilities: { staff: true, allStaff: true, readOnly: true }, settings: { today: first }, flags: { visual_system: visual, small_ux: true, capacity_screens: capacity } }) })
      if (path === 'reference') return route.fulfill({ json: referenceData({ offices: [referenceOffice(office, 'Synthetic office')], disciplines: [referenceDiscipline(civil, 'Civil')] }) })
      if (path === 'staff') {
        staffReads.push(Object.fromEntries(url.searchParams))
        assert.equal(url.searchParams.has('cardIndicators'), false, 'display predicates never become API filters')
        const indicator = url.searchParams.get('indicator'), rows = people.filter(person => !indicator || person[indicator] > 0)
        return route.fulfill({ json: { scope: url.searchParams.get('scope') ?? 'direct', canSeeAll: true, people: rows, asOf: '2026-11-01T13:00:00Z',
          tiles: { staff: rows.length, assignments: rows.reduce((n, row) => n + row.projects, 0), withOverdue: rows.filter(row => row.overdue > 0).length, withBlocked: rows.filter(row => row.blocked > 0).length, reviewsStalled: rows.reduce((n, row) => n + row.reviewsStalled, 0) } } })
      }
      if (path.endsWith('/assignments')) {
        assignmentReads.push(path)
        return route.fulfill({ json: [1, 2].map(n => ({ memberId: id(600 + n), rowVersion: 1, projectId: id(620 + n), projectNumber: `SYN-${n}`, name: `Synthetic membership ${n}`,
          status: 'Active', reportedHealth: 'Green', roles: ['TeamMember'], leads: [], primaryDiscipline: null, addedAt: '2026-10-01T13:00:00Z', open: 0, overdue: 0, nextDue: null, canRemove: false })) })
      }
      if (path === 'views' && request.method() === 'GET') return route.fulfill({ json: viewList(saved) })
      if (path === 'views' && request.method() === 'POST') {
        const body = request.postDataJSON(); saved.push(savedView({ id: id(700), name: body.name, params: body.params }))
        return route.fulfill({ status: 201, json: viewSaved(id(700)) })
      }
      if (path === 'time') {
        timeReads.push(path)
        const dto = timeView(url.searchParams.get('from'), url.searchParams.get('to'), zeroTime ? [] : entries, true)
        // Own-week totals must use the displayed week entries, not this decoy general-read total.
        if (capacity && !url.searchParams.has('scope')) dto.total = 99
        return route.fulfill({ json: dto })
      }
      if (path === 'time/week') {
        timeReads.push(path); notifyWeek()
        if (holdWeek) await new Promise(resolve => { releaseWeek = resolve })
        return route.fulfill({ json: { from: first, to: last, today: first, personId: viewer, capacityHours: 40, asOf: '2026-11-01T13:00:00Z',
          days: Array.from({ length: 7 }, (_, n) => ({ date: `2026-11-${String(9 + n).padStart(2, '0')}`, normalHours: n < 5 ? 8 : 0, availableHours: n < 5 ? 8 : 0, timeAwayHours: 0, holidayHours: 0, additionalHours: 0, provisional: false, unavailable: false })),
          tasks: [{ taskId: id(510), taskKey: 'SYN-CC-T1', taskName: 'Synthetic counted work', projectId: project.id, projectNumber: project.projectNumber, projectName: project.name, canCreate: false, deleted: false, assigned: true, previousWeek: false, entries: zeroTime ? [] : entries }] } })
      }
      return route.fallback()
    })
    const cards = page.locator('[data-staff-counts] [data-count-card]'), rows = page.locator('#staff-people table tbody > tr[data-staff-person]')
    const counts = async expected => {
      await page.waitForFunction(expected => JSON.stringify([...document.querySelectorAll('[data-staff-counts] .count-card__value')].map(node => node.textContent)) === JSON.stringify(expected.map(String)), expected)
      assert.deepEqual(await cards.locator('.count-card__value').allTextContents(), expected.map(String))
    }
    const focusPeople = () => page.waitForFunction(() => document.activeElement?.id === 'staff-people')
    await page.goto(`${base}/staff?scope=all`)
    await counts([4, 7, 2, 2, 3])
    await page.evaluate(async () => { await document.fonts.ready })
    const firstRow = await rows.first().boundingBox()
    const geometry = await page.evaluate(() => Object.fromEntries(['.visual-topbar', '.staff-count-view > .visual-page-band', '.staff-count-view__filters', '[data-staff-counts]', '#staff-people thead', '#staff-people tbody tr[data-staff-person]'].map(selector => {
      const rect = document.querySelector(selector)?.getBoundingClientRect(); return [selector, rect && { top: rect.top, height: rect.height, bottom: rect.bottom }]
    })))
    assert.ok(firstRow && firstRow.y <= 405, `nonempty1440 Staff first row must remain<=405px; got${firstRow?.y}: ${JSON.stringify(geometry)}`)
    assert.equal(await page.getByLabel('Office', { exact: true }).count(), 1)
    assert.equal(await page.getByLabel('Discipline', { exact: true }).count(), 1)
    assert.equal(await page.getByRole('checkbox', { name: 'Show inactive', exact: true }).count(), 1)
    assert.deepEqual(await cards.evaluateAll(nodes => nodes.map(node => node.tagName)), Array(5).fill('A'))
    assert.deepEqual(await cards.evaluateAll(nodes => nodes.map(node => node.className.match(/count-card--(teal|blue|red|amber)/)[1])), ['teal', 'blue', 'red', 'red', 'amber'])
    assert.equal(await cards.locator('a,button,input').count(), 0)
    assert.equal(await rows.locator('[data-staff-projects]').allTextContents().then(values => values.reduce((n, value) => n + Number(value), 0)), 7)
    assert.equal(await rows.locator('[data-staff-stalled]').allTextContents().then(values => values.reduce((n, value) => n + Number(value), 0)), 3)
    assert.equal(await page.getByRole('columnheader').count(), await rows.first().locator('td').count())
    await cards.nth(1).getByText('All staff · sum of Projects for 4 people shown', { exact: true }).waitFor()
    const sumReads = staffReads.length
    await cards.nth(1).press('Enter'); await focusPeople(); await counts([4, 7, 2, 2, 3])
    assert.equal(await rows.count(), 4, 'seven assignments are a Projects sum over four people, not seven people')
    await cards.nth(4).press('Enter'); await focusPeople(); await counts([2, 5, 1, 2, 3])
    assert.equal(await rows.count(), 2, 'three stalled reviews are contributed by two people')
    assert.equal(await rows.locator('[data-staff-stalled]').allTextContents().then(values => values.reduce((n, value) => n + Number(value), 0)), 3)
    await page.getByRole('group', { name: 'Active filters', exact: true }).getByRole('button', { name: 'Clear filters', exact: true }).click()
    await counts([4, 7, 2, 2, 3]); assert.equal(staffReads.length, sumReads)
    const beforeLocal = staffReads.length
    await cards.nth(2).focus(); await page.getByRole('tooltip').filter({ hasText: 'People with at least one overdue item.' }).waitFor()
    await cards.nth(2).press('Enter'); await focusPeople(); await counts([2, 4, 2, 1, 2])
    await cards.nth(3).click(); await counts([1, 3, 1, 1, 2])
    await cards.nth(4).click(); await counts([1, 3, 1, 1, 2])
    assert.equal(new URL(page.url()).searchParams.get('cardIndicators'), 'overdue,blocked,reviewsStalled')
    const same = page.url(); await cards.nth(2).click(); await focusPeople(); assert.equal(page.url(), same)
    assert.equal(staffReads.length, beforeLocal, 'local intersections do not refetch Staff')
    assert.equal(assignmentReads.length, 0, 'cards do not fetch per-person memberships to manufacture a sum')
    await rows.first().getByRole('button', { name: 'Show assignments of Synthetic person 411', exact: true }).click()
    await page.getByText('Synthetic membership 2', { exact: true }).waitFor()
    assert.equal(assignmentReads.length, 1); await counts([1, 3, 1, 1, 2])

    // A current blocked server cohort must not expand to outside-overdue person413.
    const scoped = `${base}/staff?scope=all&office=${office}&discipline=${civil}&inactive=1&indicator=blocked`
    await page.goto(scoped); await counts([2, 5, 1, 2, 3])
    const scopedReads = staffReads.length
    await cards.nth(2).click(); await counts([1, 3, 1, 1, 2]); await focusPeople()
    assert.equal(await rows.count(), 1); assert.equal(await rows.first().getAttribute('data-staff-person'), id(411))
    const firstIntersection = page.url()
    await cards.nth(3).click(); await counts([1, 3, 1, 1, 2])
    await page.goBack(); await page.waitForURL(firstIntersection); await focusPeople()
    await page.goForward(); await page.waitForURL(url => url.searchParams.get('cardIndicators') === 'overdue,blocked')
    await cards.nth(4).click(); await counts([1, 3, 1, 1, 2])
    const expectedParams = { scope: 'all', office, discipline: civil, inactive: '1', indicator: 'blocked', cardIndicators: 'overdue,blocked,reviewsStalled' }
    assert.deepEqual(Object.fromEntries(new URL(page.url()).searchParams), expectedParams)
    assert.equal(staffReads.length, scopedReads)
    await page.getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    const save = page.getByRole('dialog', { name: 'Save this view', exact: true })
    await save.getByLabel('Name', { exact: true }).fill('Synthetic intersection')
    await save.getByRole('button', { name: 'Save', exact: true }).click()
    await save.waitFor({ state: 'hidden' }); assert.deepEqual(saved[0].params, expectedParams)
    await page.goto(`${base}/staff?scope=all`); await counts([4, 7, 2, 2, 3])
    await page.getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Synthetic intersection', exact: true }).click()
    await counts([1, 3, 1, 1, 2])
    assert.equal(new URL(page.url()).searchParams.get('cardIndicators'), expectedParams.cardIndicators)
    await page.reload(); await counts([1, 3, 1, 1, 2])
    const active = page.getByRole('group', { name: 'Active filters', exact: true })
    await active.getByRole('button', { name: 'Remove filter: Blocked', exact: true }).click()
    await counts([1, 3, 1, 1, 2])
    await active.getByRole('button', { name: 'Clear filters', exact: true }).click()
    await counts([2, 5, 1, 2, 3]); assert.equal(new URL(page.url()).searchParams.get('indicator'), 'blocked')
    await page.getByRole('button', { name: 'Clear staff filter', exact: true }).click()
    await counts([4, 7, 2, 2, 3])
    await page.goto(`${base}/staff?scope=all&cardIndicators=blocked,overdue,overdue,unknown`)
    await page.waitForURL(url => url.searchParams.get('cardIndicators') === 'overdue,blocked'); await counts([1, 3, 1, 1, 2])
    await page.setViewportSize({ width: 375, height: 812 })
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
    const phone = page.locator('#staff-people ul[aria-label="My Staff"] > [data-staff-person]')
    assert.equal(await phone.count(), 1)
    assert.equal(await phone.locator('[data-staff-projects]').innerText(), '3'); assert.equal(await phone.locator('[data-staff-stalled]').innerText(), '2')
    visual = false
    await page.goto(scoped + '&cardIndicators=overdue'); await page.getByRole('button', { name: /^With overdue work/ }).waitFor()
    assert.equal(await cards.count(), 0); assert.equal(await page.locator('#staff-people').count(), 0)
    await page.getByRole('button', { name: /^With overdue work/ }).click()
    await page.waitForURL(url => url.searchParams.get('indicator') === 'overdue')
    await page.getByRole('button', { name: /^With overdue work/, pressed: true }).click()
    await page.waitForURL(url => !url.searchParams.has('indicator'))

    // Time keeps exact URL scope and sums hours, not entry row counts.
    await page.setViewportSize({ width: 1440, height: 1000 }); visual = true
    const timeUrl = `${base}/time?from=${first}&to=${last}&scope=all&userId=${viewer}&projectId=${project.id}&taskId=${id(510)}`
    await page.goto(timeUrl)
    const total = page.getByRole('link', { name: /^Total shown 4 h / })
    await total.waitFor(); assert.match(await total.getAttribute('class'), /count-card--teal/)
    const target = new URL(await total.getAttribute('href'), base)
    assert.deepEqual(Object.fromEntries(target.searchParams), Object.fromEntries(new URL(timeUrl).searchParams)); assert.equal(target.hash, '#time-entries')
    const timeBefore = timeReads.length
    await total.focus(); await page.getByRole('tooltip').filter({ hasText: 'Sum of the hours in the entries shown.' }).waitFor()
    await total.press('Enter'); await page.waitForFunction(() => document.activeElement?.id === 'time-entries')
    assert.equal(timeReads.length, timeBefore)
    assert.equal(await page.getByRole('table', { name: 'Time entries by work date', exact: true }).locator('tbody tr').count(), 3, 'two contributing entry rows plus their day subtotal sum4h')
    zeroTime = true; await page.goto(timeUrl); await page.getByRole('link', { name: /^Total shown 0 h / }).waitFor()
    await page.getByRole('link', { name: /^Total shown 0 h / }).press('Enter'); await page.waitForFunction(() => document.activeElement?.id === 'time-entries')
    zeroTime = false; capacity = true; holdWeek = true; weekStarted = new Promise(resolve => { notifyWeek = resolve })
    await page.goto(`${base}/time?from=${first}&to=${last}`); await weekStarted
    await page.getByRole('link', { name: /^Total shown Unavailable / }).waitFor()
    holdWeek = false; releaseWeek()
    await page.getByRole('link', { name: /^Total shown 4 h / }).waitFor()
    const grid = page.getByRole('region', { name: 'Your task hours by day', exact: true })
    assert.ok((await page.getByRole('link', { name: /^Total shown 4 h / }).boundingBox()).y >= (await grid.boundingBox()).y + (await grid.boundingBox()).height, 'own Time totals stay after the full-width grid')
    assert.ok((await grid.locator('tbody tr').first().boundingBox()).y <= 405, 'own Time first row density is retained')
    await page.setViewportSize({ width: 375, height: 812 })
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
    visual = false; await page.reload(); await grid.waitFor(); assert.equal(await page.locator('.count-card').count(), 0)
    assert.ok((await page.getByText('4 h', { exact: true }).count()) > 0)
    assert.deepEqual(writes, ['POST views'], 'only the explicitly tested saved-view write occurs; no staffing/time mutation')
    assert.ok(staffReads.every(params => !('cardIndicators' in params)))
    assert.deepEqual(errors, [])
    console.log('PASS: Staff exact AND/cohort/sum/saved/legacy/focus/density/phone behavior; Time scoped-hour links/zero/unknown/week-source/full-width/phone/off; no automatic membership reads or operational writes. Synthetic API only.')
  } finally {
    releaseWeek?.()
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
