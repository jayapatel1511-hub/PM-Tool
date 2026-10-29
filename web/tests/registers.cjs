// 040-S15-Registers: the Risks and Issues registers and their panels go through the typed client. The registers read from their contract paths with the
// filters under the contract's names (Q, Status, Severity, Indicator, and the issue source filters); Raise risk sends the score, the person and the links as the
// contract names them; the risk panel reads its risk from the contract path and a move sends the status with the version in If-Match; the issue panel reads the
// issue, its locations, references and verification from their contract paths, Resolve sends the resolution and date, and Add location sends every location field
// (nulls included) with the version in the body and in If-Match. Every write carries X-Hub-Source: UI. Against the mocked platform API; backend rules are
// tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project, panelIds } = require('./platform-fixtures.cjs')
const { riskRow, riskDetail, issueRow, issueDetail, locationRow, registerCreated, registerSaved } = require('./registers.fixture.cjs')

const port = Number(process.env.REGISTERS_TEST_PORT ?? 7324)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const r1 = panelIds.risk, i1 = panelIds.issue, newRisk = id(801)

// The caller may raise register items and the project's own panels are editable for them.
Object.assign(project.permissions, { raiseRegister: { ok: true } })

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    let risks = [riskRow({ id: r1, projectId: project.id, key: 'P-MOCK-R01', title: 'Permit office backlog', probability: 3, impact: 2, score: 6, band: 'High' })]
    let issue = issueRow({ id: i1, projectId: project.id, key: 'P-MOCK-I01', title: 'Survey crew cannot access site', severity: 'High', targetResolutionDate: '2026-10-20', issueType: 'Coordination' })
    let locations = []
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    const waitRead = async (path) => { for (let n = 0; n < 100 && !reads.some(r => r.path === path); n++) await page.waitForTimeout(100); assert.ok(reads.some(r => r.path === path), `read ${path}`) }
    const permissions = (extra) => ({ edit: { ok: true, reason: null }, transitions: [], comment: true, ...extra })

    await page.route(`**/api/v1/projects/${project.id}/risks**`, route => {
      const w = send(route.request())
      if (w.method === 'GET') { reads.push({ path: w.path, query: w.query }); return route.fulfill({ json: risks }) }
      writes.push(w)
      risks = [...risks, riskRow({ id: newRisk, projectId: project.id, key: 'P-MOCK-R02', title: w.body.title, probability: w.body.probability, impact: w.body.impact, score: w.body.probability * w.body.impact, band: 'Medium' })]
      return route.fulfill({ status: 201, headers: { location: `/api/v1/risks/${newRisk}` }, json: registerCreated(newRisk, 'P-MOCK-R02') })
    })
    await page.route('**/api/v1/risks/**', route => {
      const w = send(route.request()), parts = w.path.split('/'), current = risks.find(x => x.id === parts[4])
      if (w.method === 'GET') { reads.push({ path: w.path }); return route.fulfill({ json: riskDetail({ risk: current, permissions: permissions({ transitions: [{ to: 'Monitoring', ok: true, reason: null }, { to: 'Closed', ok: true, reason: null }] }) }) }) }
      writes.push(w)
      risks = risks.map(x => x.id === current.id ? { ...x, status: w.body.toStatus, rowVersion: x.rowVersion + 1 } : x)
      return route.fulfill({ json: { id: current.id, status: w.body.toStatus, rowVersion: current.rowVersion + 1, issueId: null, issueKey: null } })
    })
    await page.route(`**/api/v1/projects/${project.id}/issues**`, route => {
      const w = send(route.request())
      reads.push({ path: w.path, query: w.query })
      return route.fulfill({ json: [issue] })
    })
    await page.route(`**/api/v1/issues/${i1}**`, route => {
      const w = send(route.request())
      if (w.method === 'GET') {
        reads.push({ path: w.path })
        if (w.path.endsWith('/locations')) return route.fulfill({ json: locations })
        if (/\/(documents|verification|reference-impacts)$/.test(w.path)) return route.fulfill({ json: [] })
        return route.fulfill({ json: issueDetail({ issue, permissions: { ...permissions({ transitions: [{ to: 'In Progress', ok: true, reason: null }, { to: 'Resolved', ok: true, reason: null }] }), changeType: { ok: true, reason: null } } }) })
      }
      writes.push(w)
      if (w.path.endsWith('/transition')) {
        issue = { ...issue, status: w.body.toStatus, resolution: w.body.resolution, resolvedDate: w.body.resolvedDate, rowVersion: issue.rowVersion + 1 }
        return route.fulfill({ json: { id: i1, status: w.body.toStatus, rowVersion: issue.rowVersion } })
      }
      locations = [...locations, locationRow({ ...w.body, id: id(821) })]
      issue = { ...issue, rowVersion: issue.rowVersion + 1 }
      return route.fulfill({ status: 201, headers: { location: `/api/v1/issue-locations/${id(821)}` }, json: registerSaved(id(821), 0) })
    })

    // The Risks register: read from its contract path; filters under the contract's names; no filter sends none.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/risks`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Permit office backlog' }).waitFor({ state: 'visible', timeout: 20000 })
    const lastRisks = () => reads.filter(r => r.path === `/api/v1/projects/${project.id}/risks`).at(-1)
    assert.deepEqual(lastRisks().query, {}, 'an unfiltered register sends no filter')
    await page.goto(`${preview.base}/projects/${project.projectNumber}/risks?q=permit&severity=High&status=Open,Monitoring&indicator=reviewOverdue&ownerId=${id(2)}&disciplineId=${id(4)}`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Permit office backlog' }).waitFor({ state: 'visible' })
    await page.waitForFunction(() => document.body.innerText.includes('Permit'))
    assert.deepEqual(lastRisks().query, { Q: 'permit', Severity: 'High', Status: 'Open,Monitoring', Indicator: 'reviewOverdue', OwnerId: id(2), DisciplineId: id(4) })

    // Raise risk: the score and the optional fields as the contract names them; the new risk opens in its panel.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/risks`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Raise risk' }).first().click()
    const form = page.getByRole('dialog')
    await form.locator('#r-title').fill('Utility records incomplete')
    await form.getByRole('radio', { name: /^2 · / }).first().check()
    await form.getByRole('radio', { name: /^3 · / }).last().check()
    await form.getByRole('button', { name: 'Raise risk', exact: true }).click()
    await page.waitForFunction(() => location.search.includes('panel=Risk'))
    assert.deepEqual(writes[0], { method: 'POST', path: `/api/v1/projects/${project.id}/risks`, query: {}, body: { title: 'Utility records incomplete', probability: 2, impact: 3, ownerId: null, reviewDate: null, projectDisciplineId: null, links: [] }, ifMatch: null, source: 'UI' })
    await waitRead(`/api/v1/risks/${newRisk}`) // the raised risk is read from its contract path

    // The panel: Monitor moves the risk with its version in If-Match and the other fields null.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/risks?panel=Risk:${r1}`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Monitor' }).click()
    await page.waitForFunction(() => document.body.innerText.includes('Monitoring'))
    assert.deepEqual(writes[1], { method: 'POST', path: `/api/v1/risks/${r1}/transition`, query: {}, body: { toStatus: 'Monitoring', reason: null, rowVersion: null, issue: null, issueId: null }, ifMatch: '"1"', source: 'UI' })

    // The Issues register: the source filters go out under the contract's names, numbers as numbers.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/issues?q=gate&severity=High&status=Open&indicator=overdue&location=yard&document=C-101&revision=B&verification=None&alignment=Main&stationFrom=10&stationTo=20.5&stationUnits=m&issueType=Coordination`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Survey crew cannot access site' }).waitFor({ state: 'visible', timeout: 20000 })
    assert.deepEqual(reads.filter(r => r.path === `/api/v1/projects/${project.id}/issues`).at(-1).query, {
      Q: 'gate', Severity: 'High', Status: 'Open', Indicator: 'overdue', Location: 'yard', Document: 'C-101', Revision: 'B', Verification: 'None', Alignment: 'Main', StationFrom: '10', StationTo: '20.5', StationUnits: 'm', IssueType: 'Coordination' })

    // The issue panel reads the issue, its locations, references and verification from their contract paths.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/issues?panel=Issue:${i1}`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Resolve' }).waitFor({ state: 'visible', timeout: 20000 })
    await page.getByRole('heading', { name: 'Location and evidence' }).waitFor({ state: 'visible' })
    for (const tail of ['', '/locations', '/documents', '/verification']) assert.ok(reads.some(r => r.path === `/api/v1/issues/${i1}${tail}`), `the issue panel reads ${tail || 'the issue'} from its contract path`)

    // Add location: every field is named, nulls included; the issue's version goes in the body and in If-Match.
    await page.getByLabel('Site area').fill('North yard')
    await page.getByRole('button', { name: 'Add', exact: true }).first().click() // the first form is the location's
    await page.getByText('North yard').first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[2], { method: 'POST', path: `/api/v1/issues/${i1}/locations`, query: {}, ifMatch: '"1"', source: 'UI', body: {
      rowVersion: 1, kind: 'SiteArea', siteArea: 'North yard', building: null, level: null, room: null, assetSystem: null, alignment: null, startStation: null, endStation: null, stationUnits: null,
      coordinateX: null, coordinateY: null, coordinateZ: null, coordinateReferenceSystem: null, coordinateUnits: null } })

    // Resolve: the resolution and the date, the version in If-Match.
    await page.getByRole('button', { name: 'Resolve', exact: true }).first().click()
    await page.locator('#res-text').fill('Owner issued a key')
    await page.locator('#res-date').fill('2026-10-02')
    await page.getByRole('dialog').getByRole('button', { name: 'Resolve', exact: true }).click()
    await page.waitForFunction(() => document.body.innerText.includes('Resolved'))
    assert.deepEqual(writes[3], { method: 'POST', path: `/api/v1/issues/${i1}/transition`, query: {}, body: { toStatus: 'Resolved', reason: null, rowVersion: null, resolution: 'Owner issued a key', resolvedDate: '2026-10-02' }, ifMatch: '"2"', source: 'UI' })
    assert.equal(writes.length, 4)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
