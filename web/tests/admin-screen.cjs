// Isolated AC-SCR-82/83/84 UI draft. Read-only synthetic transport against Root's integrated SPA; no permission-enforcement claim.
const assert = require('node:assert/strict')
const path = require('node:path')
const { newMockPage, me } = require('./platform-fixtures.cjs')
const f = require('./admin-screen.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
;(async () => {
  const saved = { flags: { ...me.flags }, capabilities: { ...me.capabilities } }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const writes = [], unknown = [], errors = [], userReads = [], roleReads = [], cases = []
  let userError = false, roleError = false, roleMismatch = false, mismatch = false, nextRoleGate = null, stage = 'setup'
  const deferRoleRead = match => {
    assert.equal(nextRoleGate, null)
    let release, arrived
    const wait = new Promise(resolve => { release = resolve }), started = new Promise(resolve => { arrived = resolve })
    nextRoleGate = { match, wait, arrived }
    return { started, release }
  }
  page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false]))
    Object.assign(me.flags, { people_screens: true, visual_system: process.env.TUESDAY_REVIEW_VISUAL === '1' }); me.capabilities = { ...saved.capabilities, admin: true, workload: false }
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        writes.push({ method: request.method(), path, body: request.postData() }); return route.fulfill({ status: 409, json: { code: 'unexpected_admin_screen_write' } })
      }
      if (path === 'admin/users') {
        userReads.push(Object.fromEntries(url.searchParams))
        if (userError) return route.fulfill({ status: 403, json: { code: 'forbidden', detail: 'Current Admin users read refused' } })
        return route.fulfill({ json: f.list(url.searchParams, !!me.flags.people_screens, !!me.flags.org_structure, mismatch) })
      }
      if (path === 'admin/role-explanations') {
        const query = Object.fromEntries(url.searchParams)
        roleReads.push(query); assert.equal(me.flags.people_screens, true)
        assert.deepEqual([...url.searchParams.keys()].sort(), ['membership', 'operation', 'role', 'scenario'])
        if (roleError) return route.fulfill({ status: 403, json: { code: 'forbidden', detail: 'Current role examples read refused' } })
        const payload = f.roles(query.role, query.scenario, query.membership, query.operation)
        if (roleMismatch) payload.selectedMembershipKey = 'Reviewer'
        if (nextRoleGate?.match(query)) { const gate = nextRoleGate; nextRoleGate = null; gate.arrived(); await gate.wait }
        return route.fulfill({ json: payload })
      }
      if (path === `people/${f.id(56101)}/panel`) return route.fulfill({ json: f.person(f.id(56101), 'Synthetic scope supervisor') })
      if (['config', 'me', 'reference', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count'].includes(path)) return route.fallback()
      unknown.push(path); return route.fulfill({ status: 501, json: { code: 'unexpected_admin_screen_read', detail: path } })
    })
    const table = () => page.getByRole('table', { name: 'Users & roles', exact: true })
    const row = name => table().getByRole('row').filter({ has: page.getByText(name, { exact: true }) })
    const cell = async (name, column) => {
      const header = table().getByRole('columnheader', { name: column, exact: true })
      await header.waitFor()
      const index = await header.evaluate(node => node.cellIndex)
      assert.ok(Number.isInteger(index) && index >= 0, `Missing column position: ${column}`)
      return row(name).getByRole('cell').nth(index)
    }
    const examples = () => page.locator('[data-admin-role-examples]')
    const go = async () => { await page.goto(`${base}/admin/users`); await table().waitFor(); await examples().locator('[data-role-row="S01"]').waitFor() }
    const roleResponse = predicate => page.waitForResponse(response => {
      const url = new URL(response.url())
      return url.pathname === '/api/v1/admin/role-explanations' && predicate(Object.fromEntries(url.searchParams), response.status())
    })
    const choose = async (label, value) => {
      await examples().getByLabel(label, { exact: true }).selectOption(value)
      await frames()
      // A previously read selection may legitimately be fresh in React Query; do not demand a nonexistent GET.
      await page.waitForFunction(node => node && !node.disabled, await examples().getByRole('button', { name: 'Refresh examples', exact: true }).elementHandle())
      assert.equal(await examples().getByLabel(label, { exact: true }).inputValue(), value)
    }
    const operation = rowId => examples().locator(`[data-role-row="${rowId}"] [data-role-operation="${f.firstOperation(rowId)}"]`)
    const frames = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    const screenshot = async name => { if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: path.join(process.env.TUESDAY_EVIDENCE_DIR, `${name}.png`), fullPage: true }) }
    stage = 'initial-loading-and-columns'
    await page.setViewportSize({ width: 1440, height: 1000 })
    const initialRead = deferRoleRead(() => true)
    await page.goto(`${base}/admin/users`); await table().waitFor(); await initialRead.started
    assert.equal(await examples().locator('[data-role-row]').count(), 0)
    assert.equal(await examples().getByRole('button', { name: 'Refresh examples', exact: true }).isDisabled(), true)
    initialRead.release(); await examples().locator('[data-role-row="S01"]').waitFor()
    await table().getByRole('columnheader', { name: 'Base/configured weekly capacity', exact: true }).waitFor()
    const supervisor = () => row('Synthetic scope supervisor')
    const supervisorCell = column => cell('Synthetic scope supervisor', column)
    await (await supervisorCell('Base/configured weekly capacity')).getByText('0 h/week · Override', { exact: true }).waitFor()
    await (await supervisorCell('Home discipline')).getByText('Retired civil (Inactive)', { exact: true }).waitFor()
    await (await supervisorCell('People scope')).getByText('2 active direct reports', { exact: true }).waitFor()
    assert.equal(await (await supervisorCell('Template editor')).innerText(), 'Yes')
    await (await cell('Synthetic default capacity', 'Base/configured weekly capacity')).getByText('41 h/week · Organisation default', { exact: true }).waitFor()
    assert.equal(await (await cell('Synthetic default capacity', 'Template editor')).innerText(), 'No')
    await (await cell('Synthetic default capacity', 'People scope')).getByText('Not applicable', { exact: true }).waitFor()
    await (await cell('Synthetic unknown discipline', 'Home discipline')).getByText('Unavailable', { exact: true }).waitFor()
    await page.getByRole('checkbox', { name: 'Show inactive', exact: true }).click()
    await table().getByText('Synthetic inactive supervisor (Inactive)', { exact: true }).waitFor()
    const inactiveScope = await cell('Synthetic inactive supervisor (Inactive)', 'People scope')
    await inactiveScope.getByText('Configured scope (inactive)', { exact: true }).waitFor()
    await inactiveScope.getByText('1 active direct reports', { exact: true }).waitFor()
    await screenshot('admin-screen-desktop-columns')
    cases.push('AC-SCR-82 columns: true zero/base default/retired and unknown discipline/template boolean/current direct scope and inactive configured label')

    stage = 'separate-system-and-project-roles'
    assert.equal(await examples().getByLabel('Project membership', { exact: true }).inputValue(), 'none')
    assert.equal(await examples().getByLabel('System role', { exact: true }).locator('option[value="derived-DisciplineLead"]').count(), 0)
    assert.equal(await examples().getByLabel('Operation', { exact: true }).inputValue(), 'all')
    assert.deepEqual(await examples().locator('[data-role-row]').evaluateAll(nodes => nodes.map(node => node.getAttribute('data-role-row'))), f.rowMetadata.map(row => row.rowId))
    assert.equal(f.rowMetadata.length, 83)
    assert.equal(await examples().locator('[data-role-operation]').count(), 257, 'Every declared child operation remains represented under its own row')
    await choose('System role', 'system-ProjectManager')
    assert.equal(await examples().getByLabel('Project membership', { exact: true }).inputValue(), 'none', 'System PM never silently adds project membership')
    await choose('Operation', f.firstOperation('W15'))
    await operation('W15').locator('[data-semantic-tone]').filter({ hasText: 'Not eligible in this example' }).waitFor()
    await operation('W15').getByRole('region', { name: 'Facts for this operation', exact: true }).getByText('Synthetic project membership: No project membership', { exact: true }).waitFor()
    await operation('W15').locator('[data-role-factors] > summary').click()
    await operation('W15').getByText('Passes this supporting check', { exact: true }).waitFor()
    assert.equal(await examples().getByText('Allowed', { exact: true }).count(), 0)
    await operation('W15').locator('[data-role-requirement="synthetic-project-membership"]').getByText('Not met in this example', { exact: true }).waitFor()
    cases.push('All83 real matrix row identities rendered; system and project membership separate; system PM preserves None; a passing factor never becomes whole-command Allowed')

    stage = 'per-operation-facts-requirements-and-policy'
    await choose('Project membership', 'PM')
    await operation('W15').locator('[data-semantic-tone]').filter({ hasText: 'Conditional' }).waitFor()
    await operation('W15').locator('[data-role-requirement="synthetic-current-source"]').getByText('Requires a live check', { exact: true }).waitFor()
    await operation('W15').getByRole('region', { name: 'Facts for this operation', exact: true }).getByText('Synthetic project membership: Project Manager', { exact: true }).waitFor()
    await choose('Operation', f.firstOperation('S21'))
    await operation('S21').locator('[data-semantic-tone]').filter({ hasText: 'Conditional' }).waitFor()
    assert.equal(await operation('S21').getByText('An unresolved policy choice affects this operation; no permission decision is implied.', { exact: true }).count(), 0)
    assert.equal(await examples().getByText('Allowed', { exact: true }).count(), 0)
    await screenshot('admin-screen-operation-policy')
    cases.push('Operation-specific facts and unmet/live-check requirements stay visible; explicit project membership remains Conditional; S21 report export reads the member-project rule')

    stage = 'pending-old-response-and-selection-mismatch'
    await choose('Operation', 'all')
    await choose('Project membership', 'none')
    const delayed = deferRoleRead(query => query.membership === 'TeamMember')
    const oldResponse = roleResponse(query => query.membership === 'TeamMember')
    await examples().getByLabel('Project membership', { exact: true }).selectOption('TeamMember'); await delayed.started
    assert.equal(await examples().locator('[data-role-row]').count(), 0, 'Pending selection withholds previous operation results')
    await choose('Project membership', 'Viewer')
    delayed.release(); await oldResponse; await frames()
    assert.equal(await examples().getByLabel('Project membership', { exact: true }).inputValue(), 'Viewer')
    await operation('S01').getByRole('region', { name: 'Facts for this operation', exact: true }).getByText('Synthetic project membership: Viewer', { exact: true }).waitFor()
    assert.equal(await examples().getByText('Synthetic project membership: Team Member', { exact: true }).count(), 0)
    roleMismatch = true
    await choose('Project membership', 'PM')
    const mismatched = roleResponse((query, status) => query.membership === 'PM' && status === 200)
    await examples().getByRole('button', { name: 'Refresh examples', exact: true }).click(); await mismatched
    assert.equal(await examples().locator('[data-role-row]').count(), 0)
    await examples().getByText('Role examples unavailable.', { exact: true }).waitFor()
    roleMismatch = false
    const recovered = roleResponse((query, status) => query.membership === 'PM' && status === 200)
    await examples().getByRole('button', { name: 'Refresh examples', exact: true }).click(); await recovered
    await examples().locator('[data-role-row="S01"]').waitFor()
    assert.equal(await examples().getByLabel('Project membership', { exact: true }).inputValue(), 'PM')
    cases.push('Initial/loading/pending results are withheld; a delayed old selection cannot replace current facts; mismatched membership payload is unavailable until explicit successful reread')

    stage = 'guard-and-person-return'
    const edit = supervisor().getByRole('button', { name: 'Edit', exact: true }); await edit.click()
    const dialog = page.getByRole('dialog'); await dialog.waitFor()
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    await page.waitForFunction(node => document.activeElement === node, await edit.elementHandle())
    const source = supervisor().locator(`[data-person-panel-trigger="${f.id(56101)}"]`)
    await source.scrollIntoViewIfNeeded(); await source.click(); await page.locator(`[data-person-panel="${f.id(56101)}"]`).waitFor()
    await page.keyboard.press('Escape'); await page.waitForURL(`${base}/admin/users`)
    const focusId = await source.getAttribute('id'); await page.waitForFunction(id => document.activeElement?.id === id, focusId)
    assert.deepEqual(writes, [])
    cases.push('Existing User editor Cancel and known-person return preserve natural source focus; no user/role/deactivation write introduced')

    stage = 'scope-and-mismatch'; me.flags.org_structure = true; await go()
    await (await supervisorCell('People scope')).getByText('5 people · Organisation scope', { exact: true }).waitFor()
    await (await supervisorCell('People scope')).getByText('Includes this person', { exact: true }).waitFor()
    mismatch = true; await go()
    for (const column of ['Home discipline', 'Base/configured weekly capacity', 'People scope'])
      await (await supervisorCell(column)).getByText('Unavailable', { exact: true }).waitFor()
    assert.equal(await (await supervisorCell('Base/configured weekly capacity')).getByText('77 h/week · Override', { exact: true }).count(), 0)
    assert.equal(await (await supervisorCell('People scope')).getByText('5 people · Organisation scope', { exact: true }).count(), 0)
    mismatch = false
    cases.push('Structure-on metadata has explicit basis/self; mismatched user version withholds capacity and count instead of fabricating values')

    stage = 'refusals'; await go(); userError = true
    await page.getByLabel('Search', { exact: true }).fill('refused')
    await page.getByRole('alert').getByText('Current Admin users read refused', { exact: true }).waitFor()
    assert.equal(await table().count(), 0); userError = false
    await go(); roleError = true
    await examples().getByLabel('Example conditions', { exact: true }).selectOption('other-discipline')
    await examples().getByRole('alert').filter({ hasText: 'Current role examples read refused' }).waitFor()
    assert.equal(await examples().locator('[data-role-row]').count(), 0); roleError = false
    const roleRecovery = roleResponse((query, status) => query.scenario === 'other-discipline' && status === 200)
    await examples().getByRole('button', { name: 'Refresh examples', exact: true }).click(); await roleRecovery
    await examples().locator('[data-role-row="S01"]').waitFor()
    cases.push('Current-key user/role refusal hides prior cached rows/examples and sends no command')

    stage = 'tablet-phone'; await page.setViewportSize({ width: 1024, height: 900 }); await go()
    await table().getByRole('columnheader', { name: 'People scope', exact: true }).waitFor()
    const beforePhone = userReads.length + roleReads.length
    await page.setViewportSize({ width: 375, height: 812 })
    await page.getByText('This screen is available on tablet and desktop.', { exact: true }).waitFor(); await frames()
    assert.equal(await table().count(), 0); assert.equal(await examples().count(), 0)
    assert.equal(userReads.length + roleReads.length, beforePhone)
    cases.push('Tablet retains Admin table; 375px preserves existing phone notice and never mounts the new reads')

    stage = 'off'; me.flags.people_screens = false; await page.setViewportSize({ width: 1440, height: 1000 })
    const beforeOffRoles = roleReads.length
    await page.goto(`${base}/admin/users`); await table().waitFor(); await frames()
    for (const label of ['Home discipline', 'Base/configured weekly capacity', 'Template editor', 'People scope'])
      assert.equal(await table().getByRole('columnheader', { name: label, exact: true }).count(), 0)
    assert.equal(await examples().count(), 0); assert.equal(roleReads.length, beforeOffRoles)
    await row('Synthetic scope supervisor').getByRole('button', { name: 'Edit', exact: true }).click()
    await page.getByRole('dialog').getByLabel('Weekly capacity (h)', { exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Cancel', exact: true }).click()
    cases.push('AC-SCR-84: new columns/examples/GET absent off while legacy user fields and Cancel remain available')
    assert.deepEqual(writes, []); assert.deepEqual(unknown, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS', scope: 'Synthetic read-only UI against integrated draft DTOs; no canonical operation authorization or S49 acceptance claim', cases, userReads, roleReads, writes }))
  } catch (error) {
    let state
    try { state = await page.evaluate(() => ({ url: location.href, active: { id: document.activeElement?.id, text: document.activeElement?.textContent?.slice(0, 150) },
      dialogs: [...document.querySelectorAll('[role="dialog"]')].slice(0, 6).map(node => ({ state: node.getAttribute('data-state'), title: node.querySelector('h2')?.textContent?.slice(0, 150) })) })) } catch (diagnosticError) { state = { diagnosticError: String(diagnosticError) } }
    console.error(JSON.stringify({ stage, ...state, completedCases: cases, userReads, roleReads, writes, unknown, errors })); throw error
  } finally { me.flags = saved.flags; me.capabilities = saved.capabilities; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
