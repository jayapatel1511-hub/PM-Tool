// 040-S15-Projects: the project list, header and lifecycle go through the typed client. The list is read from its contract path with the
// contract's query names (scope, page size and the page's own filters, none of the page's other parameters); Star and Remove star send a
// bare POST and DELETE; Create sends the identity, disciplines and members and opens the new project by its number; Settings sends a PATCH
// with only the changed fields and the row version in If-Match; header links are added with a null type and removed with the link's own row
// version; Put on hold and Mark complete send the contract body (the closeout choices only for Complete) after reading the closeout; the
// date review banner reads its contract path. Every write carries X-Hub-Source: UI. Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project, me } = require('./platform-fixtures.cjs')
const { projectRow, projectList, closeoutView, dateReviewView } = require('./projects.fixture.cjs')
const { referenceData, referenceDiscipline, referenceClient, referenceOffice } = require('./me.fixture.cjs')

const port = Number(process.env.PROJECTS_TEST_PORT ?? 7327)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const client = id(701), office = id(702), civil = id(703), taskA = id(711), createdId = id(720)

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    const state = { starred: false, rowVersion: 1, status: 'Active', name: project.name, links: [], reviewable: false }
    const send = (request) => {
      const url = new URL(request.url())
      return { method: request.method(), path: url.pathname, query: Object.fromEntries(url.searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] }
    }
    const detail = () => ({
      ...project, name: state.name, status: state.status, starred: state.starred, rowVersion: state.rowVersion, links: state.links,
      permissions: { ...project.permissions, edit: { ok: true, reason: null }, isPm: true, reviewDates: { ok: true, reason: null }, transitions: state.status === 'Active' ? ['On Hold', 'Complete'] : [] },
    })
    const row = () => projectRow({ id: project.id, projectNumber: project.projectNumber, name: state.name, status: state.status, starred: state.starred })

    await page.route('**/api/v1/me', route => route.fulfill({ json: { ...me, capabilities: { ...me.capabilities, createProject: true } } }))
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil')], clients: [referenceClient(client, 'Synthetic client')], offices: [referenceOffice(office, 'Synthetic office')] }) }))
    await page.route('**/api/v1/templates', route => route.fulfill({ json: { canEdit: false, templates: [] } }))
    await page.route(url => url.pathname === '/api/v1/projects', route => {
      const w = send(route.request())
      if (w.method === 'POST') {
        writes.push(w)
        return route.fulfill({ status: 201, headers: { location: `/api/v1/projects/${createdId}` }, json: { id: createdId, projectNumber: w.body.projectNumber } })
      }
      reads.push(w)
      return route.fulfill({ json: projectList([row()]) })
    })
    await page.route(url => url.pathname.startsWith('/api/v1/projects/') && !url.pathname.includes(createdId), route => {
      const w = send(route.request())
      const rest = w.path.replace('/api/v1/projects/', '').split('/')
      const [key, tail, tailId] = rest
      const own = key === project.id || key === project.projectNumber
      if (!own) return route.fallback()
      if (w.method === 'GET') {
        reads.push(w)
        if (!tail) return route.fulfill({ json: detail() })
        if (tail === 'closeout') return route.fulfill({ json: closeoutView({ openTasks: 1, tasks: [{ id: taskA, key: 'T-001', name: 'Draft the layout', status: 'In Progress' }], openRegisterItems: 2 }) })
        if (tail === 'date-review') return route.fulfill({ json: state.reviewable
          ? dateReviewView({ window: { heldFrom: '2026-09-14', resumedOn: '2026-10-05', days: 21 }, tasks: [{ id: taskA }], deliverables: [] })
          : dateReviewView() })
        return route.fallback()
      }
      if (tail === 'tasks' || tail === 'deliverables') return route.fallback()
      writes.push(w)
      if (!tail && w.method === 'PATCH') {
        state.rowVersion += 1; if (w.body.name) state.name = w.body.name
        return route.fulfill({ json: { id: project.id, rowVersion: state.rowVersion }, headers: { etag: `"${state.rowVersion}"` } })
      }
      if (tail === 'star') { state.starred = w.method === 'POST'; return route.fulfill({ status: 204 }) }
      if (tail === 'links' && w.method === 'POST') {
        const link = { id: id(731), title: w.body.title || w.body.url, url: w.body.url, linkType: 'Web Link', rowVersion: 1 }
        state.links = [...state.links, link]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/links/${link.id}` }, json: link })
      }
      if (tail === 'links') { state.links = state.links.filter(l => l.id !== tailId); return route.fulfill({ status: 204 }) }
      if (tail === 'transition') {
        state.status = w.body.toStatus; state.rowVersion += 1
        return route.fulfill({ json: { id: project.id, status: state.status, rowVersion: state.rowVersion } })
      }
      return route.fallback()
    })

    // The list: the contract path with the contract's query names. Only the page's own filters are sent; its view parameters are not.
    await page.goto(`${preview.base}/projects?status=Active&health=Green&includeArchived=true&sort=name:asc&group=none`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('link', { name: project.name }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.deepEqual(reads[0], { method: 'GET', path: '/api/v1/projects', query: { Mine: 'true', PageSize: '200', Status: 'Active', Health: 'Green', IncludeArchived: 'true', Sort: 'name:asc' }, body: null, ifMatch: null, source: 'UI' })

    // Star and Remove star: a bare POST then a bare DELETE on the contract path, and the list reads again.
    const readsBefore = reads.length
    await page.getByRole('button', { name: 'Star project' }).first().click()
    await page.getByRole('button', { name: 'Remove star' }).first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[0], { method: 'POST', path: `/api/v1/projects/${project.id}/star`, query: {}, body: null, ifMatch: null, source: 'UI' })
    assert.ok(reads.length > readsBefore, 'the list is read again after a star')
    await page.getByRole('button', { name: 'Remove star' }).first().click()
    await page.getByRole('button', { name: 'Star project' }).first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[1], { method: 'DELETE', path: `/api/v1/projects/${project.id}/star`, query: {}, body: null, ifMatch: null, source: 'UI' })

    // Create: step one names the project, step two the disciplines; the body is the identity, the disciplines and members, and the new number opens.
    await page.goto(`${preview.base}/projects`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Create project', exact: true }).first().click()
    await page.locator('#p-num').fill('P-NEW-1')
    await page.locator('#p-name').fill('Harbour crossing')
    await page.locator('#p-client').selectOption(client)
    await page.locator('#p-office').selectOption(office)
    await page.getByRole('button', { name: 'Next', exact: true }).click()
    await page.getByRole('checkbox', { name: /Civil/ }).click()
    await page.getByRole('button', { name: 'Create project', exact: true }).last().click()
    await page.waitForURL(/\/projects\/P-NEW-1$/)
    const created = writes[2]
    assert.equal(created.method, 'POST'); assert.equal(created.path, '/api/v1/projects'); assert.equal(created.ifMatch, null); assert.equal(created.source, 'UI')
    assert.deepEqual({ projectNumber: created.body.projectNumber, name: created.body.name, clientId: created.body.clientId, officeId: created.body.officeId, startDate: created.body.startDate, targetCompletionDate: created.body.targetCompletionDate, projectTypeId: created.body.projectTypeId, disciplines: created.body.disciplines, members: created.body.members },
      { projectNumber: 'P-NEW-1', name: 'Harbour crossing', clientId: client, officeId: office, startDate: null, targetCompletionDate: null, projectTypeId: null, disciplines: [{ disciplineId: civil, leadUserId: null }], members: [] })

    // Settings: only the changed field goes in the PATCH, with the row version in If-Match.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/settings`, { waitUntil: 'domcontentloaded' })
    await page.locator('#s-name').fill('Renamed crossing')
    const patched = page.waitForResponse(r => r.request().method() === 'PATCH')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await patched
    assert.deepEqual(writes[3], { method: 'PATCH', path: `/api/v1/projects/${project.id}`, query: {}, body: { name: 'Renamed crossing' }, ifMatch: '"1"', source: 'UI' })

    // Header links: added with a null type, removed with the link's own row version.
    await page.locator('#s-link-url').fill('https://example.test/folder')
    await page.locator('#s-link-title').fill('Project folder')
    await page.getByRole('button', { name: 'Add', exact: true }).click()
    await page.getByRole('button', { name: 'Remove Project folder' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[4], { method: 'POST', path: `/api/v1/projects/${project.id}/links`, query: {}, body: { url: 'https://example.test/folder', title: 'Project folder', linkType: null }, ifMatch: null, source: 'UI' })
    await page.getByRole('button', { name: 'Remove Project folder' }).click()
    await page.getByRole('button', { name: 'Remove Project folder' }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[5], { method: 'DELETE', path: `/api/v1/projects/${project.id}/links/${id(731)}`, query: {}, body: null, ifMatch: '"1"', source: 'UI' })

    // Complete: the closeout is read first and its choices go in the body; no reason box is skipped.
    await page.getByRole('button', { name: 'Mark complete' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByText('1 open tasks').waitFor({ state: 'visible' })
    assert.ok(reads.some(r => r.path === `/api/v1/projects/${project.id}/closeout`), 'the closeout is read from its contract path')
    await dialog.getByLabel('1 open tasks').selectOption('cancel')
    await dialog.getByLabel(/^Reason/).fill('All work issued')
    await dialog.getByRole('button', { name: 'Mark complete' }).click()
    await page.getByText('Change status').waitFor({ state: 'detached' })
    assert.deepEqual(writes[6], { method: 'POST', path: `/api/v1/projects/${project.id}/transition`, query: {}, body: { toStatus: 'Complete', reason: 'All work issued', rowVersion: 2, closeout: { tasks: 'cancel', deliverables: 'leave', decisions: 'leave', registers: 'leave' } }, ifMatch: null, source: 'UI' })
    assert.equal(state.status, 'Complete')

    // Hold: a project that is Active again, with no closeout choices in the body (null, not omitted).
    state.status = 'Active'
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Put on hold' }).click()
    await page.getByRole('dialog').getByLabel(/^Reason/).fill('Client paused the work')
    await page.getByRole('dialog').getByRole('button', { name: 'Put on hold' }).click()
    await page.getByText('Change status').waitFor({ state: 'detached' })
    assert.deepEqual(writes[7], { method: 'POST', path: `/api/v1/projects/${project.id}/transition`, query: {}, body: { toStatus: 'On Hold', reason: 'Client paused the work', rowVersion: 3, closeout: null }, ifMatch: null, source: 'UI' })

    // Date review: back from hold the banner reads its contract path and names the work.
    state.status = 'Active'; state.reviewable = true
    await page.goto(`${preview.base}/projects/${project.projectNumber}/settings`, { waitUntil: 'domcontentloaded' })
    await page.getByText(/Back from hold: 1 task was due between/).waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.some(r => r.path === `/api/v1/projects/${project.id}/date-review`), 'the date review is read from its contract path')
    assert.equal(writes.length, 8, 'no other write')
    // The new project's own page, opened after Create, is outside this suite; every other read is mocked.
    const ours = (x) => !String(x).includes('P-NEW-1') && !String(x).includes(createdId)
    assert.deepEqual(ui.mock.unknownGets.filter(ours), [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors.filter(ours), [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
