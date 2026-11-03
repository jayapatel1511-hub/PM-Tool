// Synthetic Pickup actor UI only. Root supplies verified preview/CHROME_EXECUTABLE_PATH; no service start or API authority claim.
/** @typedef {import('../../../../web/src/gen/api').components['schemas']} Schemas */
const assert = require('node:assert/strict')
const path = require('node:path')
const root = path.resolve(__dirname, '../../../..')
const { newMockPage, me, project } = require(path.join(root, 'web/tests/platform-fixtures.cjs'))
const { surfacePerson } = require(path.join(root, 'web/tests/person-surface-completeness.fixture.cjs'))
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use only the root-verified local preview')
assert.ok(process.env.CHROME_EXECUTABLE_PATH, 'Root must supply its verified Google Chrome executable for this Chrome receipt')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const userA = id(8801), userB = id(8802), deniedUser = id(8803)
const sameName = 'Synthetic same-name offerer', factToken = 'Known pickup person facts must not survive another author 404'
const names = new Map([[userA, sameName], [userB, sameName], [deniedUser, 'Historical narrow pickup author'], [me.id, me.displayName]])
const discipline = project.disciplines[0]
/** @returns {Schemas['TaskPickupListRow']} */
const offer = (n, user, canPickUp = false, canAssign = false) => {
  const offeredBy = names.get(user)
  assert.ok(offeredBy, 'Every offeredBy name has a real fixture User ID')
  return { id: id(n), taskId: id(n + 100), offeredById: user, offeredBy, openedAt: '2026-11-02T12:00:00Z',
  reason: `Retained synthetic offer ${n}`, hoursRemaining: null, neededBy: '2026-11-06', note: null, canPickUp, canAssign,
  task: { key: `T-PICK-${n}`, name: `Retained pickup task ${n}`, projectId: project.id, projectNumber: project.projectNumber,
    projectDisciplineId: discipline.id, disciplineName: discipline.name, dueDate: '2026-11-09', rowVersion: 7 },
  }
}
const principal = [offer(8811, userA, true, true), offer(8812, userA, false, true), offer(8813, userB, false, true), offer(8814, deniedUser)]
/** @type {Schemas['TaskPickupListRow'][]} */
const offers = [...Array.from({ length: 24 }, (_, i) => offer(8900 + i, me.id)), ...principal]
/** The allowed own-load hint stays partial; it is separate from forbidden person/workload reads. @type {Schemas['LoadResult']} */
const ownLoad = { week: '2026-11-02', partial: true, band: null, hours: null, capacity: 40 }

;(async () => {
  const saved = { flags: { ...me.flags }, capabilities: { ...me.capabilities }, settings: { ...me.settings } }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], writes = [], unknownGets = [], personReads = [], offerReads = [], loadReads = []
  page.setDefaultTimeout(15000)
  page.on('pageerror', error => errors.push(error.message))
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false]))
    Object.assign(me.flags, { task_pickup: true, people_screens: true, capacity_screens: true }) // work_screens remains false.
    me.capabilities = { ...saved.capabilities, workload: false, readOnly: false }
    me.settings.today = '2026-11-02'; me.settings.dateFormat = 'yyyy-MM-dd'
    await page.setViewportSize({ width: 375, height: 700 })
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), p = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (p === 'me/sign-in') return route.fallback() // Existing synthetic authentication handshake only.
        writes.push({ method: request.method(), path: p, body: request.postData() })
        return route.fulfill({ status: 409, json: { code: 'unexpected_pickup_actor_write', detail: 'Only open/close/cancel actions are allowed' } })
      }
      if (p === 'pickup-offers') { offerReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: offers }) }
      if (p === 'pickup-offers/my-load') { loadReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: ownLoad }) }
      if (/^people\/[^/]+\/panel$/.test(p)) {
        const user = p.split('/')[1]; personReads.push({ user, query: Object.fromEntries(url.searchParams) })
        if (user === deniedUser) return route.fulfill({ status: 404, contentType: 'application/problem+json', json: {
          title: 'Not found', status: 404, code: 'not_found', detail: 'Synthetic author outside narrow Person scope', traceId: 'pickup-person404' } })
        assert.ok(names.has(user), 'Never substitute an offer/task ID for its offeredById')
        const person = surfacePerson(user, names.get(user)); person.jobTitle = factToken
        return route.fulfill({ json: person })
      }
      if (['config', 'me', 'reference', 'projects', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count'].includes(p)) return route.fallback()
      unknownGets.push(p) // Includes any forbidden planning/grid/workload or accidental task-detail read.
      return route.fulfill({ status: 501, json: { code: 'unhandled_pickup_actor_get', detail: p } })
    })
    const origin = `${base}/pickup?projects=${project.id}&projectId=${project.id}&disciplineId=${discipline.id}&neededBy=2026-11-06`
    const frames = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    const list = () => page.getByRole('list', { name: 'Open for pickup', exact: true })
    const row = o => list().locator('li').filter({ has: page.getByText(o.task.key, { exact: true }) })
    const actor = o => row(o).locator(`[data-person-panel-trigger="${o.offeredById}"][id*="${encodeURIComponent(`pickup:${o.id}:offered-by`)}"]`)
    const unique = async () => {
      const ids = await page.locator('[data-person-panel-trigger][id]').evaluateAll(nodes => nodes.map(node => node.id))
      assert.equal(ids.length, new Set(ids).size)
      assert.equal(await page.locator('button [data-person-panel-trigger],a [data-person-panel-trigger],label [data-person-panel-trigger],summary [data-person-panel-trigger],[role="option"] [data-person-panel-trigger]').count(), 0)
    }
    const filters = async () => {
      assert.equal(page.url(), origin)
      const selects = page.locator('#content label select')
      assert.equal(await selects.nth(0).inputValue(), project.id)
      assert.equal(await selects.nth(1).inputValue(), discipline.id)
      assert.equal(await page.locator('#content input[type="date"]').inputValue(), '2026-11-06')
    }
    const roundTrip = async (o, denied = false) => {
      const source = actor(o)
      assert.equal(await source.count(), 1)
      await source.scrollIntoViewIfNeeded(); await source.focus(); await frames()
      const focusId = await source.getAttribute('id')
      const before = await page.locator('#content').evaluate(node => ({ top: node.scrollTop, left: node.scrollLeft }))
      assert.ok(before.top > 0, 'The exact offerer source is genuinely scrolled before activation')
      const response = denied ? page.waitForResponse(r => r.url().includes(`/people/${o.offeredById}/panel`) && r.status() === 404) : null
      await page.keyboard.press('Enter')
      await page.waitForURL(url => url.searchParams.get('panel') === `Person:${o.offeredById}`)
      if (denied) {
        assert.equal((await response).status(), 404)
        await page.getByText('This page or item does not exist, or you cannot view it.', { exact: true }).last().waitFor()
        assert.equal(await page.locator('[data-person-panel]').count(), 0, 'Another author cannot reuse cached successful Person facts')
        assert.equal(await page.getByText(factToken, { exact: true }).count(), 0)
      } else {
        const person = page.locator(`[data-person-panel="${o.offeredById}"]`)
        await person.waitFor()
        assert.ok((await person.innerText()).includes(factToken))
        assert.equal(await person.locator('section[aria-label]').count(), 0, 'No current-week hours/capacity section without Workload')
        assert.equal(await person.getByRole('heading', { name: /^This week ·/ }).count(), 0)
      }
      await page.keyboard.press('Escape'); await page.waitForURL(origin)
      await page.waitForFunction(id => document.activeElement?.id === id, focusId)
      await frames()
      const after = await page.locator('#content').evaluate(node => ({ top: node.scrollTop, left: node.scrollLeft }))
      assert.ok(Math.abs(after.top - before.top) <= 2); assert.ok(Math.abs(after.left - before.left) <= 2)
      await filters(); await unique()
      return focusId
    }
    const assignHrefs = async () => Promise.all(principal.slice(0, 3).map(o => row(o).getByRole('link', { name: 'Assign pickup', exact: true }).getAttribute('href')))
    const cancelPick = async () => {
      const start = row(principal[0]).getByRole('button', { name: 'Pick up', exact: true })
      await start.focus(); await page.keyboard.press('Enter')
      const dialog = page.getByRole('dialog')
      await dialog.getByRole('heading', { name: `Pick up ${principal[0].task.key}`, exact: true }).waitFor()
      assert.equal(await dialog.getByRole('button', { name: 'Pick up task', exact: true }).count(), 1)
      await dialog.getByRole('button', { name: 'Cancel', exact: true }).focus(); await page.keyboard.press('Enter')
      await dialog.waitFor({ state: 'hidden' }); await filters()
      assert.equal(await row(principal[0]).count(), 1); assert.deepEqual(writes, [])
    }
    await page.goto(origin); await actor(principal[0]).waitFor(); await unique()
    assert.equal(await list().locator('li').count(), offers.length)
    const hrefs = await assignHrefs()
    assert.deepEqual(hrefs, principal.slice(0, 3).map(o => `/projects/${project.projectNumber}/tasks?panel=Task:${o.taskId}`))
    const first = await roundTrip(principal[0]), second = await roundTrip(principal[1]), third = await roundTrip(principal[2])
    assert.notEqual(first, second, 'Same User in two offers keeps distinct offer/role return targets')
    assert.notEqual(first, third, 'A same-name different User is identified by the actual offeredById')
    assert.ok(personReads.some(r => r.user === userA)); assert.ok(personReads.some(r => r.user === userB))
    await cancelPick(); await roundTrip(principal[3], true)
    assert.deepEqual(await assignHrefs(), hrefs)
    assert.ok(offerReads.some(q => q.projectId === project.id && q.disciplineId === discipline.id && q.neededBy === '2026-11-06'))
    assert.ok(loadReads.length > 0, 'The existing own-load hint is explicitly mocked; no planner read is substituted')
    const readCount = personReads.length
    me.flags.people_screens = false
    await page.reload(); await row(principal[0]).waitFor()
    assert.equal(await list().locator('li').count(), offers.length)
    assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0)
    for (const o of principal) assert.ok((await row(o).innerText()).includes(o.offeredBy), 'Flag off keeps the original offerer name')
    assert.deepEqual(await assignHrefs(), hrefs); await cancelPick()
    assert.equal(personReads.length, readCount, 'Flag off issues no Person read')
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 2), false, '375px page has no overflow')
    assert.deepEqual(writes, []); assert.deepEqual(unknownGets, []); assert.deepEqual(mock.unknownGets, []); assert.deepEqual(errors, [])
    console.log(JSON.stringify({ status: 'PASS', scope: 'synthetic Pickup actor UI only; no API authority or command acceptance claim',
      browser: browser.version(), chromeExecutable: process.env.CHROME_EXECUTABLE_PATH ?? null, width: 375, businessWrites: writes.length,
      personReads, offerReads, ownLoadReads: loadReads.length, cases: ['two offers/same User distinct IDs', 'same-name different User',
        'Enter/Escape natural exact URL/focus/filter/nonzero-scroll return', 'answered narrow author404 clears other cached facts',
        'no Workload hours/planner reads', 'pickup confirmation cancel only and Assign href unchanged', 'flag-off original names/offers/actions at375'] }))
  } finally {
    me.flags = saved.flags; me.capabilities = saved.capabilities; me.settings = saved.settings
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
