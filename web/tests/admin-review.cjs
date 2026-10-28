// Packet 051 S09 (FR-ADR-01, FR-ADR-03, FR-ADR-04, FR-ADR-06, FR-ADR-07, FR-ADR-08, AC-ADR-02, AC-ADR-04, AC-ADR-05, AC-ADR-06): the Admin review
// page against a self-started production preview and the mocked platform API. Flag off: no route and no tab. Flag on: each check's count opens
// its list with links to the existing tools, a review is recorded with per-check and whole-review notes, the last review shows beside the
// counts now, and the export asks for the recorded review. A non-Admin gets the server's refusal and no table.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, me } = require('./platform-fixtures.cjs')

const port = Number(process.env.ADMIN_REVIEW_TEST_PORT ?? 7263)
const alexId = '00000000-0000-0000-0000-0000000000a1'
const reviewId = '00000000-0000-0000-0000-0000000000b1'
const checks = [
  { key: 'inactiveOnTeams', title: 'Inactive users still on teams', rule: 'Inactive users who are a team member on a live project', count: 2, listUrl: '/admin/review/checks/inactiveOnTeams' },
  { key: 'noManager', title: 'Users without a manager', rule: 'Active users with no manager', count: 1, listUrl: '/admin/review/checks/noManager' },
  { key: 'noSignIn', title: 'No sign-in for 90 days', rule: 'Active users whose last sign-in is more than 90 days ago', count: 0, listUrl: '/admin/review/checks/noSignIn' },
  { key: 'inactiveReference', title: 'Reference data deactivated while in use', rule: 'Inactive reference data still set on a live project', count: 1, listUrl: '/admin/review/checks/inactiveReference' },
  { key: 'settingsChanged', title: 'Settings changed', rule: 'Organisation settings changed since the last review', count: 1, listUrl: '/admin/review/checks/settingsChanged' },
]
const lists = {
  inactiveOnTeams: [{ id: alexId, name: 'Alex Chen', detail: 'alex@hub.test', at: null, link: `/people/${alexId}/reassign-work` }, { id: null, name: 'Omar Diaz', detail: 'omar@hub.test', at: null, link: '/people/o/reassign-work' }],
  noManager: [{ id: 'lena', name: 'Lena Park', detail: 'lena@hub.test', at: null, link: '/admin/users' }],
  inactiveReference: [{ id: 'c1', name: 'Old client', detail: 'clients', at: null, link: '/admin/reference/clients' }],
  settingsChanged: [{ id: null, name: 'task_due_soon_days', detail: null, at: '2026-10-10T12:00:00Z', link: '/admin/settings' }],
}

async function run(base, { flagOn, admin = true }) {
  const { browser, page, mock } = await newMockPage(base)
  const errors = [], posts = [], exports = []
  let last = null
  page.on('pageerror', error => errors.push(error.message))
  await page.route('**/api/v1/me', route => route.fulfill({ json: { ...me, capabilities: { ...me.capabilities, admin }, flags: { ...me.flags, admin_review: flagOn } } }))
  await page.route(/\/api\/v1\/admin\/review/, async route => {
    const request = route.request(); const url = new URL(request.url()); const p = url.pathname.replace('/api/v1/admin/review', '')
    if (!admin) return route.fulfill({ status: 403, json: { code: 'forbidden', detail: 'Only a System Administrator may do this.' } })
    if (p.startsWith('/checks/')) { const c = checks.find(x => x.key === p.slice(8)); return route.fulfill({ json: { key: c.key, title: c.title, rule: c.rule, rows: lists[c.key] ?? [] } }) }
    if (p.endsWith('/export')) { exports.push(`${p}${url.search}`); return route.fulfill({ body: 'Date,2026-10-03', headers: { 'content-type': 'text/csv', 'content-disposition': 'attachment; filename="admin-review.csv"' } }) }
    if (request.method() === 'POST') {
      const body = request.postDataJSON(); posts.push(body)
      last = { id: reviewId, reviewedAt: '2026-10-03T15:00:00Z', reviewerId: me.id, reviewer: 'Jordan Lee', note: body.note ?? null,
        checks: checks.map(c => ({ key: c.key, count: c.count, note: body.checks?.find(n => n.key === c.key)?.note ?? null })) }
      return route.fulfill({ status: 201, json: last })
    }
    // After the record the counts have moved on: alex's check now reads 3 against 2 then.
    return route.fulfill({ json: { checks: checks.map(c => ({ ...c, count: last && c.key === 'inactiveOnTeams' ? 3 : c.count })), last, dueOn: last ? '2027-01-01' : null, overdue: !last } })
  })
  try {
    await page.goto(`${base}/admin/review`, { waitUntil: 'domcontentloaded' })
    if (!flagOn || !admin) {
      if (!flagOn) {
        // AC-ADR-06: no page and no tab with the flag off, and the route is not found.
        await page.getByText(/This page or item does not exist/i).waitFor({ state: 'visible', timeout: 20000 })
        await page.goto(`${base}/admin/settings`, { waitUntil: 'domcontentloaded' })
        await page.getByRole('navigation', { name: 'Administration' }).waitFor({ state: 'visible', timeout: 20000 })
        assert.equal(await page.getByRole('link', { name: 'Admin review' }).count(), 0, 'no Admin review tab with the flag off')
      } else {
        // AC-ADR-04: the server's 403 is shown and nothing of the review is.
        await page.getByRole('alert').getByText('Only a System Administrator may do this.').waitFor({ state: 'visible', timeout: 20000 })
        assert.equal(await page.getByRole('table').count(), 0, 'no table for a non-Admin')
      }
      assert.deepEqual(errors, [], `browser errors: ${errors.join('; ')}`)
      return { flagOn, admin }
    }

    // First visit: nothing recorded, so a review is due, and there is nothing to export yet.
    await page.getByRole('heading', { name: 'Admin review', exact: true }).waitFor({ state: 'visible', timeout: 20000 })
    await page.getByText('No review has been recorded yet.').waitFor()
    await page.getByText('Admin review due: none recorded yet').waitFor()
    assert.equal(await page.getByRole('button', { name: 'Export last review' }).count(), 0, 'nothing to export before a review exists')
    assert.equal(await page.getByRole('link', { name: 'Admin review' }).count(), 1, 'the Admin review tab is shown with the flag on')
    const table = page.getByRole('table', { name: 'Admin review', exact: true })
    for (const c of checks) await table.getByText(c.title, { exact: true }).waitFor()
    await table.getByText(checks[0].rule, { exact: true }).waitFor()

    // A count opens its list; each row links to the tool that fixes it and adds no edit path of its own (FR-ADR-05).
    const show = page.getByRole('button', { name: 'Show list: Inactive users still on teams (2)' })
    assert.equal(await show.getAttribute('aria-expanded'), 'false')
    await show.click()
    const list = page.getByRole('table', { name: 'Inactive users still on teams', exact: true })
    await list.getByText('Alex Chen', { exact: true }).waitFor()
    assert.equal(await show.getAttribute('aria-expanded'), 'true')
    assert.equal(await list.getByRole('link', { name: 'Open Alex Chen' }).getAttribute('href'), `/people/${alexId}/reassign-work`)
    assert.equal(await list.getByRole('button').count(), 0, 'a list row has no button of its own')
    assert.equal(await page.getByRole('button', { name: /No sign-in/ }).count(), 0, 'a zero count has nothing to open')
    await page.getByRole('button', { name: 'Show list: Reference data deactivated while in use (1)' }).click()
    await page.getByText('Clients', { exact: true }).waitFor() // the reference list's own name, not the raw key
    await page.getByRole('link', { name: 'Open Old client' }).waitFor()
    await page.getByRole('button', { name: 'Show list: Users without a manager (1)' }).click()
    assert.equal(await page.getByRole('link', { name: 'Open Lena Park' }).getAttribute('href'), '/admin/users')
    await page.getByRole('button', { name: 'Show list: Settings changed (1)' }).click()
    await page.getByRole('link', { name: 'Open Task due soon (days)' }).waitFor() // the Settings page's name, not the raw key

    // Axe: the open lists and the form meet WCAG 2.1 AA (§22).
    await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
    const violations = await page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations.map(v => ({ id: v.id, targets: v.nodes.map(n => n.target) })))
    assert.deepEqual(violations, [], `accessibility violations: ${JSON.stringify(violations)}`)
    if (process.env.ADMIN_REVIEW_SHOT) await page.screenshot({ path: process.env.ADMIN_REVIEW_SHOT, fullPage: true })

    // Record: one whole-review note and one check note; the counts are the server's, so the body carries notes only.
    await page.getByRole('textbox', { name: 'Note for Inactive users still on teams' }).fill('Checked with HR')
    await page.getByRole('textbox', { name: 'Note for the whole review' }).fill('Quarterly access review')
    await page.getByRole('button', { name: 'Record review', exact: true }).click()
    const dialog = page.getByRole('dialog', { name: 'Record this review?' })
    await dialog.waitFor({ state: 'visible' })
    assert.deepEqual(posts, [], 'asking does not record')
    await dialog.getByRole('button', { name: 'Record review', exact: true }).click()
    await dialog.waitFor({ state: 'hidden' })
    assert.deepEqual(posts, [{ note: 'Quarterly access review', checks: [{ key: 'inactiveOnTeams', note: 'Checked with HR' }] }])

    // AC-ADR-02: the last review shows, with its counts then beside the counts now, and the form is empty again.
    await page.getByText('Last review 2026-10-03 by Jordan Lee').waitFor()
    await page.getByText('Next review due 2027-01-01').waitFor()
    const row = table.getByRole('row', { name: /Inactive users still on teams/ })
    assert.match(await row.innerText(), /3\s+2\s+Checked with HR/, 'now 3, then 2, with the note it was recorded with')
    assert.equal(await page.getByRole('textbox', { name: 'Note for the whole review' }).inputValue(), '')
    assert.equal(await page.getByRole('textbox', { name: 'Note for Inactive users still on teams' }).inputValue(), '')

    // FR-ADR-06: the export asks for the recorded review, by id, in the chosen format.
    const download = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Export last review' }).click()
    await page.getByRole('menuitem', { name: 'CSV (.csv)' }).click()
    assert.equal((await download).suggestedFilename(), 'admin-review.csv')
    assert.deepEqual(exports, [`/${reviewId}/export?format=csv`])

    assert.deepEqual(errors, [], `browser errors: ${errors.join('; ')}`)
    assert.deepEqual(mock.unknownGets, [], `unhandled mocked GETs: ${mock.unknownGets.join('; ')}`)
    return { flagOn, admin, posts: posts.length, exports: exports.length }
  } finally { await browser.close() }
}

;(async () => {
  const preview = await startPreview(port)
  try {
    const results = [await run(preview.base, { flagOn: false }), await run(preview.base, { flagOn: true, admin: false }), await run(preview.base, { flagOn: true })]
    console.log(JSON.stringify({ result: 'PASS', results }))
  } finally { preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
