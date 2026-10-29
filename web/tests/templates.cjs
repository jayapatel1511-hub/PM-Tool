// 040-S15-Templates: the Templates pages and the add-a-discipline-pack dialog go through the typed client. The list and a template are read from their
// contract paths; New template sends the name with the other header fields null; Save sends the whole structure with the row version in the body
// (the PUT has no If-Match); the preview sends the start date with the milestone dates and disciplines null; Publish sends the version it read;
// Edit (new draft) and Retire send an empty body; Discard and Remove suggestion are DELETEs on their contract paths; the pack dialog reads the
// proposal for the chosen template and adds the discipline with its lead null and its milestone mapping. Every write carries X-Hub-Source: UI.
// Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, me, project } = require('./platform-fixtures.cjs')
const { referenceData, referenceDiscipline, referenceDeliverableType } = require('./me.fixture.cjs')
const { teamDiscipline, teamMember, projectTeam } = require('./team.fixture.cjs')
const { templateRow, templateList, templateRead, templatePreview, previewMilestone, templateCreated, templateDraft, templatePublished, packProposal, packAdded } = require('./templates.fixture.cjs')

const port = Number(process.env.TEMPLATES_TEST_PORT ?? 7405)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const civil = id(801), geotech = id(802), report = id(803)
const published = id(701), draft = id(702), created = id(703), restarted = id(704)
const projectMilestone = id(751), templateMilestone = id(731)

// The caller may open the Templates page and has one published template to add a discipline pack from.
Object.assign(me.capabilities, { templates: true })

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = [], previews = [], packReads = []
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    const kickoff = { ref: templateMilestone, name: 'Kickoff', milestoneType: 'Kickoff', anchor: 'ProjectStart', offset: 5, completesPhaseId: null, isClientFacing: true }
    const civilRow = { disciplineId: civil, isDefaultIncluded: true, templateDisciplineId: id(721) }
    const store = {
      [published]: templateRead({ id: published, familyId: id(711), name: 'Culvert Replacement', status: 'Published', version: 1, publishedAt: '2026-10-01T12:00:00+00:00', canEdit: false,
        family: [{ id: published, version: 1, status: 'Published', publishedAt: '2026-10-01T12:00:00+00:00' }], disciplines: [civilRow], milestones: [kickoff] }),
      [draft]: templateRead({ id: draft, familyId: id(712), name: 'Bridge Inspection', family: [{ id: draft, version: null, status: 'Draft', publishedAt: null }], disciplines: [civilRow], milestones: [kickoff],
        basisSuggestions: [{ id: id(741), disciplineId: civil, kind: 'Criterion', title: 'Design storm', scope: 'Site', statement: 'Minor system return period', numericValue: 5, units: 'years', sourceSystem: null, stableSourceId: null, sourceUrl: null, declaredRevision: null }] }),
    }
    const rowOf = (t) => templateRow({ id: t.id, familyId: t.familyId, name: t.name, version: t.version, status: t.status, publishedAt: t.publishedAt, milestones: t.milestones.length, rowVersion: t.rowVersion })

    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil'), referenceDiscipline(geotech, 'Geotechnical')], deliverableTypes: [referenceDeliverableType(report, 'Report')] }) }))
    await page.route('**/api/v1/templates**', route => {
      const w = send(route.request()), parts = w.path.split('/'), tid = parts[4], sub = parts[5]
      if (!tid) {
        if (w.method === 'GET') { reads.push(w.path); return route.fulfill({ json: templateList(Object.values(store).map(rowOf)) }) }
        writes.push(w)
        store[created] = templateRead({ id: created, name: w.body.name, rowVersion: 1 })
        return route.fulfill({ status: 201, headers: { location: `/api/v1/templates/${created}` }, json: templateCreated(created, 1) })
      }
      if (sub === 'preview') {
        previews.push(w)
        return route.fulfill({ json: templatePreview({ counts: { milestones: 1, deliverables: 0, tasks: 0, dependencies: 0 }, undated: { milestones: 0, deliverables: 0, tasks: 0 },
          milestones: [previewMilestone({ id: templateMilestone, name: 'Kickoff', date: w.body.startDate ? '2026-10-10' : null, source: w.body.startDate ? 'Offset' : 'None' })] }) })
      }
      if (w.method === 'GET') { reads.push(w.path); return route.fulfill({ json: store[tid] }) }
      writes.push(w)
      const now = store[tid]
      if (sub === 'structure') {
        store[tid] = { ...now, name: w.body.header.name, description: w.body.header.description, rowVersion: now.rowVersion + 1 }
        return route.fulfill({ json: store[tid] })
      }
      if (sub === 'publish') {
        store[tid] = { ...now, status: 'Published', version: 1, publishedAt: '2026-10-05T12:00:00+00:00', canEdit: false, rowVersion: now.rowVersion + 1, family: [{ id: tid, version: 1, status: 'Published', publishedAt: '2026-10-05T12:00:00+00:00' }] }
        return route.fulfill({ json: templatePublished(tid, 1, now.rowVersion + 1) })
      }
      if (sub === 'draft') {
        store[restarted] = templateRead({ id: restarted, familyId: now.familyId, name: now.name, disciplines: now.disciplines, milestones: now.milestones, family: [{ id: restarted, version: null, status: 'Draft', publishedAt: null }, ...now.family] })
        return route.fulfill({ status: 201, headers: { location: `/api/v1/templates/${restarted}` }, json: templateDraft(restarted, 1) })
      }
      if (sub === 'retire') { store[tid] = { ...now, status: 'Retired', rowVersion: now.rowVersion + 1 }; return route.fulfill({ status: 204 }) }
      if (sub === 'design-basis') { store[tid] = { ...now, basisSuggestions: now.basisSuggestions.filter(b => b.id !== parts[6]), rowVersion: now.rowVersion + 1 }; return route.fulfill({ status: 204 }) }
      delete store[tid] // DELETE of the draft
      return route.fulfill({ status: 204 })
    })
    await page.route(`**/api/v1/projects/${project.id}/team`, route => route.fulfill({ json: projectTeam({ disciplines: [], members: [teamMember({ id: id(821), userId: id(811), displayName: 'Pat Lee', roles: ['PM'], isPrimaryPm: true })] }) }))
    await page.route('**/api/v1/users**', route => route.fulfill({ json: [] }))
    await page.route(`**/api/v1/projects/${project.id}/template-packs**`, route => {
      const w = send(route.request())
      if (w.method === 'GET') {
        packReads.push(w)
        return route.fulfill({ json: packProposal({ templateId: published, name: 'Culvert Replacement', projectMilestones: [{ id: projectMilestone, key: 'P-MOCK-M01', name: 'Kickoff', date: '2026-10-12' }],
          disciplines: [{ disciplineId: civil, inProject: false, deliverables: 2, tasks: 5, mapping: [{ templateMilestoneId: templateMilestone, name: 'Kickoff', projectMilestoneId: projectMilestone }] }] }) })
      }
      writes.push(w)
      return route.fulfill({ json: packAdded(2, 5, 1) })
    })

    // The list: every version for an editor, read from its contract path.
    await page.goto(`${preview.base}/templates`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('link', { name: 'Culvert Replacement' }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes('/api/v1/templates'), 'the list is read from its contract path')
    await page.getByRole('link', { name: 'Bridge Inspection' }).first().waitFor({ state: 'visible' })

    // New template: the name goes with the other header fields null; the editor opens on the new Draft.
    await page.getByRole('button', { name: 'New template' }).click()
    await page.locator('#tpl-name').fill('Storm Sewer')
    await page.getByRole('dialog').getByRole('button', { name: 'Create', exact: true }).click()
    await page.getByRole('heading', { name: 'Storm Sewer' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[0], { method: 'POST', path: '/api/v1/templates', query: {}, body: { name: 'Storm Sewer', description: null, projectTypeId: null }, ifMatch: null, source: 'UI' })
    assert.ok(reads.includes(`/api/v1/templates/${created}`), 'the new template is read from its contract path')
    assert.deepEqual(previews.at(-1).body, { startDate: null, milestoneDates: null, disciplineIds: null }, 'the preview sends no date and no discipline choice')
    assert.equal(previews.at(-1).path, `/api/v1/templates/${created}/preview`)

    // Save: the whole structure, with the version in the body and no If-Match.
    await page.locator('#t-desc').fill('Storm sewer design')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByText('Template saved').waitFor({ state: 'visible' })
    assert.deepEqual(writes[1], { method: 'PUT', path: `/api/v1/templates/${created}/structure`, query: {},
      body: { rowVersion: 1, header: { name: 'Storm Sewer', description: 'Storm sewer design', projectTypeId: null }, disciplines: [], milestones: [], deliverables: [], tasks: [], dependencies: [] }, ifMatch: null, source: 'UI' })

    // Preview: a start date goes out as the date, the rest still null.
    await page.locator('#tpl-start').fill('2026-10-05')
    await page.waitForFunction(() => document.body.innerText.includes('Preview'))
    await page.waitForTimeout(300)
    assert.deepEqual(previews.at(-1).body, { startDate: '2026-10-05', milestoneDates: null, disciplineIds: null })

    // Publish: the version the editor read goes in the body.
    await page.getByRole('button', { name: 'Publish', exact: true }).click()
    await page.getByText('Published as version 1').waitFor({ state: 'visible' })
    assert.deepEqual(writes[2], { method: 'POST', path: `/api/v1/templates/${created}/publish`, query: {}, body: { rowVersion: 2 }, ifMatch: null, source: 'UI' })

    // Retire: an empty body, after the confirmation.
    await page.getByRole('button', { name: 'Retire', exact: true }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Retire', exact: true }).click()
    await page.getByRole('button', { name: 'Retire', exact: true }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[3], { method: 'POST', path: `/api/v1/templates/${created}/retire`, query: {}, body: {}, ifMatch: null, source: 'UI' })

    // Edit (new draft) on a published version: an empty body, and the editor moves to the Draft it answers with.
    await page.goto(`${preview.base}/templates/${published}`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Edit (new draft)' }).click()
    await page.waitForURL(`**/templates/${restarted}`)
    assert.deepEqual(writes[4], { method: 'POST', path: `/api/v1/templates/${published}/draft`, query: {}, body: {}, ifMatch: null, source: 'UI' })

    // Discard that Draft: a DELETE on the template after the confirmation, then the list.
    await page.getByRole('button', { name: 'Discard draft' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Discard draft' }).click()
    await page.waitForURL('**/templates')
    assert.deepEqual(writes[5], { method: 'DELETE', path: `/api/v1/templates/${restarted}`, query: {}, body: null, ifMatch: null, source: 'UI' })
    await page.getByRole('link', { name: 'Bridge Inspection' }).first().waitFor({ state: 'visible' })

    // Remove a design basis suggestion: a DELETE on the suggestion's contract path, and the suggestion leaves the Draft.
    await page.getByRole('link', { name: 'Bridge Inspection' }).first().click()
    await page.getByRole('button', { name: 'Remove Design storm' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Remove', exact: true }).click()
    await page.getByText('No design basis suggestions.').waitFor({ state: 'visible' })
    assert.deepEqual(writes[6], { method: 'DELETE', path: `/api/v1/templates/${draft}/design-basis/${id(741)}`, query: {}, body: null, ifMatch: null, source: 'UI' })

    // Add from template on the Team tab: the proposal is read for the chosen template, and the discipline goes with its lead null and the mapping.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/team`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Add from template' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.locator('#pk-t').selectOption(published)
    await dialog.locator('#pk-d').waitFor({ state: 'visible' })
    assert.deepEqual({ path: packReads.at(-1).path, query: packReads.at(-1).query }, { path: `/api/v1/projects/${project.id}/template-packs`, query: { templateId: published } })
    await dialog.locator('#pk-d').selectOption(civil)
    await dialog.getByRole('button', { name: 'Add discipline' }).click()
    await page.getByText('Added to Civil').waitFor({ state: 'visible' })
    assert.deepEqual(writes[7], { method: 'POST', path: `/api/v1/projects/${project.id}/template-packs`, query: {}, body: { templateId: published, disciplineId: civil, leadUserId: null, mapping: { [templateMilestone]: projectMilestone } }, ifMatch: null, source: 'UI' })
    assert.equal(writes.length, 8)

    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length, previews: previews.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
