// 040-S15-Team: the project Team tab goes through the typed client. The team is read from its contract path; a role change and a primary
// discipline change send a PATCH with the row version in If-Match; Activate, Remove and Add on the discipline list send their contract
// bodies (the add names no lead as null); Add member sends the person, roles and no primary discipline as null; removing a member reads
// the person's open items and sends one DELETE with the new owner and the reason in the query and the row version in If-Match.
// Every write carries X-Hub-Source: UI. Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project } = require('./platform-fixtures.cjs')
const { teamDiscipline, teamMember, projectTeam } = require('./team.fixture.cjs')
const { referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const { templateList } = require('./templates.fixture.cjs')

const port = Number(process.env.TEAM_TEST_PORT ?? 7372)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const civil = id(801), electrical = id(802), geotech = id(803)
const pat = id(811), diane = id(812), marc = id(813), newcomer = id(814)

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    let disciplines = [teamDiscipline({ id: civil, disciplineId: id(901), name: 'Civil', leadUserId: marc, sortOrder: 0 }), teamDiscipline({ id: electrical, disciplineId: id(902), name: 'Electrical', isActive: false, sortOrder: 1 })]
    let members = [
      teamMember({ id: id(821), userId: pat, displayName: 'Pat Lee', roles: ['PM'], isPrimaryPm: true }),
      teamMember({ id: id(822), userId: diane, displayName: 'Diane Park', primaryDisciplineId: null }),
      teamMember({ id: id(823), userId: marc, displayName: 'Marc Dubois', leadOf: [civil] }),
    ]
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    const bump = (list, rowId, change) => list.map(x => x.id === rowId ? { ...x, ...change, rowVersion: x.rowVersion + 1 } : x)

    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(id(901), 'Civil'), referenceDiscipline(id(902), 'Electrical'), referenceDiscipline(geotech, 'Geotechnical')] }) }))
    await page.route('**/api/v1/templates', route => route.fulfill({ json: templateList([], false) }))
    await page.route('**/api/v1/users**', route => route.fulfill({ json: [
      { id: newcomer, displayName: 'Nia Newcomer', jobTitle: 'Designer', isActive: true, onProject: false },
      { id: marc, displayName: 'Marc Dubois', jobTitle: 'Senior Engineer', isActive: true, onProject: true },
    ] }))
    await page.route(`**/api/v1/projects/${project.id}/team`, route => {
      reads.push(new URL(route.request().url()).pathname)
      return route.fulfill({ json: projectTeam({ disciplines, members }) })
    })
    await page.route(`**/api/v1/projects/${project.id}/disciplines**`, route => {
      const request = route.request(), w = send(request)
      writes.push(w)
      const pdId = w.path.split('/')[6]
      if (w.method === 'POST') {
        disciplines = [...disciplines, teamDiscipline({ id: id(804), disciplineId: w.body.disciplineId, name: 'Geotechnical', sortOrder: 2 })]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/projects/${project.id}/disciplines/${id(804)}` }, json: { id: id(804), rowVersion: 1 } })
      }
      if (w.method === 'PATCH') disciplines = bump(disciplines, pdId, w.body)
      else disciplines = disciplines.filter(x => x.id !== pdId)
      return route.fulfill({ status: 204 })
    })
    await page.route(`**/api/v1/projects/${project.id}/members**`, route => {
      const request = route.request(), w = send(request)
      const parts = w.path.split('/'), memberId = parts[6]
      if (w.method === 'GET') {
        reads.push(w.path)
        return route.fulfill({ json: { tasks: [{ id: id(831), key: 'T-001', name: 'Draft the layout', status: 'In Progress', dueDate: '2026-10-20', role: 'Assignee' }], deliverables: [], count: 1 } })
      }
      writes.push(w)
      if (w.method === 'POST') {
        members = [...members, teamMember({ id: id(824), userId: w.body.userId, displayName: 'Nia Newcomer', roles: w.body.roles })]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/projects/${project.id}/members/${id(824)}` }, json: { id: id(824), rowVersion: 1 } })
      }
      if (w.method === 'PATCH') { members = bump(members, memberId, w.body); return route.fulfill({ status: 204 }) }
      members = members.filter(x => x.id !== memberId)
      return route.fulfill({ json: { reassigned: 1 } })
    })

    await page.goto(`${preview.base}/projects/${project.projectNumber}/team`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('row', { name: /Diane Park/ }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes(`/api/v1/projects/${project.id}/team`), 'the team is read from its contract path')

    // A role box: PATCH with the roles and the row version.
    await page.getByRole('checkbox', { name: /Reviewer — Diane Park/ }).click()
    await page.getByRole('checkbox', { name: /Reviewer — Diane Park/ }).waitFor({ state: 'visible' })
    await page.waitForFunction(() => document.querySelector('[role=checkbox][aria-label$="Diane Park"][aria-label^="Reviewer"]')?.getAttribute('aria-checked') === 'true')
    assert.deepEqual(writes[0], { method: 'PATCH', path: `/api/v1/projects/${project.id}/members/${id(822)}`, query: {}, body: { roles: ['TeamMember', 'Reviewer'] }, ifMatch: '"1"', source: 'UI' })

    // The primary discipline: the new row version rides in If-Match, and clearing it sends null.
    const primary = page.getByRole('row', { name: /Diane Park/ }).first().getByLabel('Primary discipline')
    await primary.selectOption({ label: 'Civil' })
    await page.waitForFunction(() => [...document.querySelectorAll('tr')].some(r => r.textContent.includes('Diane Park') && r.querySelector('select')?.value))
    assert.deepEqual(writes[1], { method: 'PATCH', path: `/api/v1/projects/${project.id}/members/${id(822)}`, query: {}, body: { primaryDisciplineId: civil }, ifMatch: '"2"', source: 'UI' })

    // Disciplines: Activate, Add and Remove.
    await page.getByRole('button', { name: 'Activate' }).click()
    await page.getByRole('button', { name: 'Remove Electrical' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[2], { method: 'PATCH', path: `/api/v1/projects/${project.id}/disciplines/${electrical}`, query: {}, body: { isActive: true }, ifMatch: '"1"', source: 'UI' })
    await page.getByLabel('Add discipline').selectOption({ label: 'Geotechnical' })
    await page.getByRole('button', { name: 'Add', exact: true }).first().click()
    await page.getByRole('button', { name: 'Remove Geotechnical' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[3], { method: 'POST', path: `/api/v1/projects/${project.id}/disciplines`, query: {}, body: { disciplineId: geotech, leadUserId: null }, ifMatch: null, source: 'UI' })
    await page.getByRole('button', { name: 'Remove Electrical' }).click()
    await page.getByRole('button', { name: 'Remove Electrical' }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[4], { method: 'DELETE', path: `/api/v1/projects/${project.id}/disciplines/${electrical}`, query: {}, body: null, ifMatch: '"2"', source: 'UI' })

    // Add member: the person, one role, no primary discipline as null.
    await page.getByRole('button', { name: /^Person/ }).first().click()
    await page.getByRole('option', { name: /Nia Newcomer/ }).click()
    await page.getByRole('button', { name: 'Add', exact: true }).last().click()
    await page.getByRole('row', { name: /Nia Newcomer/ }).first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[5], { method: 'POST', path: `/api/v1/projects/${project.id}/members`, query: {}, body: { userId: newcomer, roles: ['TeamMember'], primaryDisciplineId: null }, ifMatch: null, source: 'UI' })

    // Remove member: the open items are read, and one DELETE names the new owner and the reason.
    await page.getByRole('button', { name: 'Remove Diane Park' }).first().click()
    const dialog = page.getByRole('dialog')
    await dialog.getByText('Draft the layout').waitFor({ state: 'visible' })
    assert.ok(reads.includes(`/api/v1/projects/${project.id}/members/${id(822)}/open-items`), 'the open items are read from their contract path')
    await dialog.getByRole('button', { name: /^New owner|^Choose/ }).first().click()
    await page.getByRole('option', { name: /Marc Dubois/ }).click()
    await dialog.getByLabel(/^Reason/).fill('Moved to another project')
    await dialog.getByRole('button', { name: 'Remove', exact: true }).click()
    await page.getByRole('row', { name: /Diane Park/ }).waitFor({ state: 'detached' })
    const removal = writes[6]
    assert.deepEqual(removal, { method: 'DELETE', path: `/api/v1/projects/${project.id}/members/${id(822)}`, query: { reassignTo: marc, reason: 'Moved to another project' }, body: null, ifMatch: '"3"', source: 'UI' })
    assert.equal(writes.length, 7)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
