// S29 rendered synthetic fixtures only. CapacityPersonFactsTests + root foundation tests own real permission/schema/calendar evidence.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { myStaffPerson, myStaffList } = require('./my-work.fixture.cjs')
const { teamMember, teamDiscipline, projectTeam } = require('./team.fixture.cjs')
const { preferences } = require('./notifications.fixture.cjs')
const { referenceRow, adminUser } = require('./admin.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const today = '2026-10-31', confined = id(1701), eit = id(1702), jillId = id(1710)
const vocabulary = [referenceRow('CertificationTag', { id: confined, name: 'Confined space' }), referenceRow('CertificationTag', { id: eit, name: 'EIT' })]
const malformedCases = [
  { id: id(1703), name: 'Truncated response fixture', reply: 'truncated' },
  { id: id(1704), name: 'Missing acknowledgement fixture', reply: 'missing' },
  { id: id(1705), name: 'Invalid version fixture', reply: 'version' },
  { id: id(1706), name: 'Invalid identifier fixture', reply: 'id' },
]
vocabulary.push(...malformedCases.map(record => referenceRow('CertificationTag', { id: record.id, name: record.name })))
const people = [{ id: me.id, displayName: 'Omar Haddad', rowVersion: 1, personType: null, personStartDate: null, personEndDate: null, certifications: [] },
  { id: jillId, displayName: 'Jill Martin', rowVersion: 1, personType: null, personStartDate: null, personEndDate: null, certifications: [] }]
const dayDifference = date => Math.round((Date.parse(`${date}T00:00:00Z`) - Date.parse(`${today}T00:00:00Z`)) / 86400000)
function notices(person) { return { personType: person.personType, startDate: person.personStartDate, endDate: person.personEndDate, starts: !!person.personStartDate && dayDifference(person.personStartDate) > 0,
  ends: !!person.personEndDate && dayDifference(person.personEndDate) >= 0 && dayDifference(person.personEndDate) <= 60 } }
function facts(person) { return { notices: notices(person), certifications: person.certifications.map(record => { const tag = vocabulary.find(tag => tag.id === record.tagId), days = record.expiresOn ? dayDifference(record.expiresOn) : null
  return { ...record, tagName: tag.name, tagActive: tag.isActive, expiryState: days == null ? 'Unknown' : days < 0 ? 'Expired' : days <= 30 ? 'ExpiresIn' : 'Recorded', daysToExpiry: days != null && days >= 0 && days <= 30 ? days : null }
}) } }

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page, mock } = ui
  const errors = [], unexpectedWrites = [], factReads = [], teamReads = [], creates = [], edits = [], removals = [], parentEdits = [], legacyEdits = [], pickerChoices = [], requests = [], receipts = new Map()
  let nextId = 1800, staleNext = false, loseCreate = false, createReply = 'valid', newerEditorRead = false, releaseNewerEditor = null, notifyNewerEditor = null
  try {
    page.setDefaultTimeout(10000); page.on('pageerror', error => errors.push(error.message))
    me.displayName = 'Omar Haddad'; me.settings.today = today
    me.capabilities = { ...me.capabilities, admin: true, staff: true, workload: false }
    me.flags = { ...me.flags, live_events: false, capacity_screens: false, people_screens: true, visual_system: false, home_overview: true, onboarding: false, org_structure: false }
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', ''), method = request.method()
      requests.push(`${method} ${path}`)
      const editorMatch = /^admin\/users\/([^/]+)\/certifications(?:\/([^/]+))?$/.exec(path)
      const selfMatch = /^me\/certifications(?:\/([^/]+))?$/.exec(path)
      if (editorMatch || selfMatch) {
        const person = people.find(person => person.id === (editorMatch ? editorMatch[1] : me.id)), childId = editorMatch ? editorMatch[2] : selfMatch[1]
        if (method === 'GET') {
          factReads.push(path)
          if (editorMatch && person.id === jillId && newerEditorRead) {
            newerEditorRead = false
            Object.assign(person, { officeId: id(1720), weeklyCapacityHours: 26, isActive: false }); person.rowVersion++
            await new Promise(resolve => { releaseNewerEditor = resolve; notifyNewerEditor?.() })
          }
          return route.fulfill({ json: { userId: person.id, rowVersion: person.rowVersion, canEdit: !!editorMatch || !me.capabilities.readOnly, facts: facts(person), choices: vocabulary.map(tag => ({ id: tag.id, name: tag.name, isActive: tag.isActive })), today } })
        }
        const body = request.postDataJSON()
        if (method === 'POST') {
          const key = request.headers()['idempotency-key']; creates.push({ body, key })
          if (!receipts.has(key)) { const record = { id: id(nextId++), tagId: body.certificationTagId, expiresOn: body.expiresOn, rowVersion: 0 }; person.certifications.push(record); receipts.set(key, { id: record.id, rowVersion: 0 }) }
          if (loseCreate) { loseCreate = false; return route.abort('failed') }
          const reply = createReply; createReply = 'valid'
          if (reply === 'truncated') return route.fulfill({ status: 201, contentType: 'application/json', body: '{"id":' })
          if (reply === 'missing') return route.fulfill({ status: 201, json: {} })
          if (reply === 'version') return route.fulfill({ status: 201, json: { ...receipts.get(key), rowVersion: 1.5 } })
          if (reply === 'id') return route.fulfill({ status: 201, json: { ...receipts.get(key), id: 'not-a-uuid' } })
          return route.fulfill({ status: 201, json: receipts.get(key) })
        }
        if (method === 'PATCH') {
          const record = person.certifications.find(record => record.id === childId); edits.push({ id: childId, body, version: request.headers()['if-match'] })
          if (staleNext) { staleNext = false; record.rowVersion++; record.expiresOn = '2026-11-21'; return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Synthetic certification changed. Reload.' } }) }
          assert.equal(request.headers()['if-match'], `"${record.rowVersion}"`); record.expiresOn = body.expiresOn; record.rowVersion++
          return route.fulfill({ json: { id: record.id, rowVersion: record.rowVersion } })
        }
        if (method === 'DELETE') {
          const index = person.certifications.findIndex(record => record.id === childId), record = person.certifications[index]
          removals.push({ id: childId, body, version: request.headers()['if-match'] }); assert.equal(request.headers()['if-match'], `"${record.rowVersion}"`)
          person.certifications.splice(index, 1); return route.fulfill({ status: 204 })
        }
      }
      const parentMatch = /^admin\/users\/([^/]+)\/person-facts$/.exec(path)
      if (parentMatch && method === 'PATCH') {
        const person = people.find(person => person.id === parentMatch[1]), body = request.postDataJSON(); parentEdits.push({ body, version: request.headers()['if-match'] })
        assert.equal(request.headers()['if-match'], `"${person.rowVersion}"`); assert.ok(body.reason.length >= 5)
        Object.assign(person, { personType: body.personType, personStartDate: body.personStartDate, personEndDate: body.personEndDate }); person.rowVersion++
        return route.fulfill({ json: { id: person.id, rowVersion: person.rowVersion } })
      }
      if (path === 'certification-tags') return route.fulfill({ json: vocabulary.map(tag => ({ id: tag.id, name: tag.name, isActive: tag.isActive })) })
      if (path === 'admin/certificationTags' && method === 'GET') return route.fulfill({ json: vocabulary })
      if (path === 'admin/certificationTags' && method === 'POST') { const body = request.postDataJSON(), tag = referenceRow('CertificationTag', { id: id(nextId++), name: body.name }); vocabulary.push(tag); return route.fulfill({ status: 201, json: tag }) }
      const tagMatch = /^admin\/certificationTags\/([^/]+)$/.exec(path)
      if (tagMatch && method === 'PATCH') { const tag = vocabulary.find(tag => tag.id === tagMatch[1]); assert.equal(request.headers()['if-match'], `"${tag.rowVersion}"`); Object.assign(tag, request.postDataJSON()); tag.rowVersion++; return route.fulfill({ json: tag }) }
      if (path === 'admin/users' && method === 'GET') return route.fulfill({ json: people.map(person => adminUser({ id: person.id, displayName: person.displayName, rowVersion: person.rowVersion,
        officeId: person.officeId ?? null, weeklyCapacityHours: person.weeklyCapacityHours ?? null, isActive: person.isActive ?? true,
        ...(me.flags.capacity_screens ? { personFacts: facts(person) } : {}) })) })
      const legacyUser = /^admin\/users\/([^/]+)$/.exec(path)
      if (legacyUser && method === 'PATCH') {
        const person = people.find(person => person.id === legacyUser[1]), body = request.postDataJSON(), version = request.headers()['if-match']
        legacyEdits.push({ body, version })
        if (version !== `"${person.rowVersion}"`) return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Concurrent legacy fields changed. Reload the user before saving.' } })
        Object.assign(person, body); return route.fulfill({ json: { id: person.id, rowVersion: person.rowVersion } })
      }
      if (path === 'me/preferences') return route.fulfill({ json: preferences() })
      if (path === 'staff') return route.fulfill({ json: myStaffList(people.map(person => myStaffPerson({ id: person.id, displayName: person.displayName, ...(me.flags.capacity_screens ? { personFacts: facts(person) } : {}) }))) })
      const tag = url.searchParams.get('certificationTagId')
      const selected = me.flags.capacity_screens && tag ? people.filter(person => person.certifications.some(record => record.tagId === tag)) : people
      if (path === 'team') { teamReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: { people: selected.map(person => ({ userId: person.id, displayName: person.displayName, email: 'synthetic@hub.test', jobTitle: null,
        projects: [{ projectId: project.id, projectNumber: project.projectNumber, projectName: project.name, roles: ['TeamMember'], leads: [] }], ...(me.flags.capacity_screens ? { personFacts: facts(person) } : {}) })) } }) }
      if (path === `projects/${project.id}/team`) { teamReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: projectTeam({
        disciplines: [teamDiscipline({ id: project.disciplines[0].id, name: 'Civil', leadUserId: jillId, ...(me.flags.capacity_screens ? { leadName: 'Jill Martin' } : {}) })],
        members: selected.map(person => teamMember({ id: id(1900 + people.indexOf(person)), userId: person.id, displayName: person.displayName, ...(me.flags.capacity_screens ? { personFacts: facts(person) } : {}) })),
      }) }) }
      if (path === `projects/${project.id}/disciplines/${project.disciplines[0].id}` && method === 'PATCH') { pickerChoices.push(request.postDataJSON()); return route.fulfill({ status: 204 }) }
      if (path === 'users') return route.fulfill({ json: people.filter(person => person.isActive !== false && (!url.searchParams.get('q') || person.displayName.toLowerCase().includes(url.searchParams.get('q').toLowerCase()))).map(person => ({ id: person.id, displayName: person.displayName, isActive: person.isActive ?? true, onProject: true, ...(me.flags.capacity_screens ? { personNotices: notices(person) } : {}) })) })
      if (path === 'templates') return route.fulfill({ json: { templates: [] } })
      if (!['GET', 'HEAD'].includes(method) && path !== 'me/sign-in') unexpectedWrites.push(`${method} ${path}`)
      return route.fallback()
    })
    const editor = page.locator('[data-certifications-editor]').and(page.getByRole('region', { name: 'Certification records', exact: true }))
    const addGroup = editor.getByRole('group', { name: 'Add a certification record', exact: true })
    await page.goto(`${base}/preferences`); await page.getByRole('heading', { name: 'Notification preferences', exact: true }).waitFor()
    assert.equal(await editor.count(), 0); assert.deepEqual(factReads, [])
    me.flags.capacity_screens = true; me.flags.people_screens = false
    await page.reload(); await editor.waitFor(); await editor.getByText('No certification records yet.', { exact: true }).waitFor()
    await addGroup.getByLabel('Certification tag', { exact: true }).selectOption(confined); await addGroup.getByLabel(/^Expiry date/).fill('2026-11-20')
    await addGroup.getByRole('button', { name: 'Add', exact: true }).click(); await editor.getByText('· Expires in 20 days', { exact: true }).waitFor()
    assert.equal(creates.length, 1); assert.ok(creates[0].key); assert.equal(people[0].certifications.length, 1)
    assert.equal(people[0].certifications[0].rowVersion, 0, 'canonical newly created certification version0 is a valid acknowledgement')
    await page.waitForFunction(() => !document.querySelector('[data-certifications-editor] fieldset')?.disabled)
    assert.equal(await addGroup.getByLabel('Certification tag', { exact: true }).isDisabled(), false, 'valid initial version0 exits the saved/unknown protection after authoritative read')
    assert.equal(await editor.getByLabel('Person type', { exact: true }).count(), 0, 'self section has no Admin type/date editor')

    await page.goto(`${base}/staff`); await page.getByRole('heading', { name: 'My Staff', exact: true }).waitFor()
    await page.locator('table').getByText('· Expires in 20 days', { exact: true }).waitFor()
    await page.setViewportSize({ width: 375, height: 812 }); await page.getByRole('list', { name: 'My Staff', exact: true }).getByText('· Expires in 20 days', { exact: true }).waitFor()
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)); await page.setViewportSize({ width: 1440, height: 1000 })
    await page.goto(`${base}/team?projects=${project.id}&homePreset=Supervisor&homeProjectStatus=Active`)
    await page.getByRole('heading', { name: 'Team', exact: true }).waitFor(); await page.getByText('· Expires in 20 days', { exact: true }).waitFor()
    const filteredResponse = page.waitForResponse(response => response.url().includes('/api/v1/team?') && response.url().includes(`certificationTagId=${confined}`))
    await page.getByRole('button', { name: /^Filters/ }).click(); await page.getByLabel('Certification tag', { exact: true }).selectOption(confined)
    await page.waitForURL(url => url.searchParams.get('certificationTagId') === confined)
    assert.equal(new URL(page.url()).searchParams.get('projects'), project.id); assert.equal(new URL(page.url()).searchParams.get('homePreset'), 'Supervisor'); assert.equal(new URL(page.url()).searchParams.get('homeProjectStatus'), 'Active')
    assert.equal((await filteredResponse).status(), 200)
    assert.equal(teamReads.at(-1).certificationTagId, confined); assert.equal(teamReads.at(-1).projects, project.id)
    await page.goto(`${base}/projects/${project.projectNumber}/team?certificationTagId=${confined}&keep=1`)
    await page.locator('table').getByText('· Expires in 20 days', { exact: true }).waitFor(); await page.getByRole('button', { name: 'Jill Martin', exact: true }).waitFor()
    await page.getByLabel('Certification tag', { exact: true }).selectOption(''); await page.waitForURL(url => !url.searchParams.has('certificationTagId'))
    assert.equal(new URL(page.url()).searchParams.get('keep'), '1')

    await page.goto(`${base}/preferences`); await editor.waitFor()
    await editor.getByRole('button', { name: 'Edit Confined space certification', exact: true }).click()
    await editor.getByLabel(/^Expiry for Confined space/).fill('2026-11-25'); staleNext = true
    await editor.getByRole('button', { name: 'Save', exact: true }).click(); await editor.getByText('Synthetic certification changed. Reload.', { exact: true }).waitFor()
    await editor.getByRole('button', { name: 'Reload', exact: true }).click(); await editor.getByText('· Expires in 21 days', { exact: true }).waitFor(); await editor.getByRole('button', { name: 'Edit Confined space certification', exact: true }).click()
    await editor.getByLabel(/^Expiry for Confined space/).fill(''); await editor.getByRole('button', { name: 'Save', exact: true }).click()
    await editor.getByText('· Expiry not recorded', { exact: true }).waitFor(); assert.equal(edits.at(-1).id, people[0].certifications[0].id); assert.equal(edits.at(-1).version, '"1"')
    loseCreate = true
    await addGroup.getByLabel('Certification tag', { exact: true }).selectOption(eit); await addGroup.getByLabel(/^Expiry date/).fill('2026-10-30'); await addGroup.getByRole('button', { name: 'Add', exact: true }).click()
    const unknown = editor.getByRole('status').filter({ hasText: 'The result is unknown. Retry the same request before changing these inputs.' })
    await unknown.waitFor(); assert.equal(await addGroup.getByLabel('Certification tag', { exact: true }).isDisabled(), true)
    await unknown.getByRole('button', { name: 'Retry', exact: true }).click(); await editor.getByText('EIT', { exact: true }).waitFor()
    await editor.getByText('· Expired', { exact: true }).waitFor()
    assert.deepEqual(creates[1], creates[2]); assert.equal(people[0].certifications.length, 2)
    const remainingId = people[0].certifications[0].id, removedId = people[0].certifications[1].id
    await editor.getByRole('button', { name: 'Remove EIT certification', exact: true }).click()
    const removeDialog = page.getByRole('dialog', { name: 'Remove EIT certification', exact: true })
    await removeDialog.getByRole('button', { name: 'Remove', exact: true }).click(); await removeDialog.waitFor({ state: 'hidden' })
    assert.equal(removals[0].id, removedId); assert.equal(removals[0].version, '"0"'); assert.equal(people[0].certifications.length, 1); assert.equal(people[0].certifications[0].id, remainingId)
    for (const fixture of malformedCases) {
      const count = people[0].certifications.length, attempt = creates.length
      createReply = fixture.reply
      await addGroup.getByLabel('Certification tag', { exact: true }).selectOption(fixture.id)
      await addGroup.getByRole('button', { name: 'Add', exact: true }).click()
      await unknown.waitFor(); assert.equal(await addGroup.getByLabel('Certification tag', { exact: true }).isDisabled(), true, `${fixture.reply} success body stays unknown and locks the intent`)
      assert.equal(await editor.getByText('Saved. Reload to refresh the recorded facts.', { exact: true }).count(), 0, 'malformed success never acknowledges an undefined or invalid saved record')
      assert.equal(people[0].certifications.length, count + 1, 'server created once before the response failed')
      await unknown.getByRole('button', { name: 'Retry', exact: true }).click()
      await editor.locator(`[data-person-certification="${fixture.id}"]`).waitFor()
      await page.waitForFunction(() => !document.querySelector('[data-certifications-editor] fieldset')?.disabled)
      assert.equal(await addGroup.getByLabel('Certification tag', { exact: true }).isDisabled(), false, 'a valid acknowledged ID resolves the uncertainty without permanent lock')
      assert.deepEqual(creates[attempt], creates[attempt + 1]); assert.equal(people[0].certifications.length, count + 1)
    }
    const beforeReadOnly = { ...me.capabilities }
    me.systemRoles = ['ReadOnly']; me.capabilities = { ...beforeReadOnly, admin: false, staff: false, allStaff: false, createProject: false, createTask: false, templates: false, directReports: 0, readOnly: true }
    await page.reload(); await editor.waitFor(); assert.equal(await addGroup.count(), 0); assert.equal(await editor.getByRole('button', { name: /^Edit .* certification$/ }).count(), 0)
    me.systemRoles = ['Admin']; me.capabilities = { ...beforeReadOnly, admin: true, readOnly: false }

    await page.goto(`${base}/admin/users`); await page.getByRole('row').filter({ hasText: 'Jill Martin' }).getByRole('button', { name: 'Edit', exact: true }).click()
    const userDialog = page.getByRole('dialog', { name: 'Jill Martin', exact: true })
    await userDialog.getByText('Person facts and certifications', { exact: true }).click()
    const dates = userDialog.getByRole('group', { name: 'Person type and dates', exact: true })
    await dates.getByLabel('Person type', { exact: true }).selectOption('Co-op'); await dates.getByLabel(/^End date/).fill('2026-12-18'); await dates.getByLabel('Reason', { exact: true }).fill('Record the existing co-op end date')
    await dates.getByRole('button', { name: 'Save person facts', exact: true }).click(); await userDialog.getByText(/Ends 2026-12-18/).waitFor()
    assert.deepEqual(parentEdits[0], { body: { personType: 'Co-op', personStartDate: null, personEndDate: '2026-12-18', reason: 'Record the existing co-op end date' }, version: '"1"' })
    await userDialog.getByRole('button', { name: 'Save', exact: true }).click(); await userDialog.waitFor({ state: 'hidden' })
    assert.equal(legacyEdits.at(-1).version, '"2"', 'matching facts and legacy baselines advance together on the happy path')

    // Reopen from legacy v2, but hold a new facts read after another Admin changes office/capacity/activation at v3.
    // A full reload gives a fresh query cache; the legacy user is fetched before the child read is deliberately delayed.
    await page.reload(); await page.getByRole('row').filter({ hasText: 'Jill Martin' }).getByRole('button', { name: 'Edit', exact: true }).waitFor()
    newerEditorRead = true
    const newerHeld = new Promise(resolve => { notifyNewerEditor = resolve })
    const newerResponse = page.waitForResponse(response => response.url().includes(`/admin/users/${jillId}/certifications`) && response.request().method() === 'GET')
    await page.getByRole('row').filter({ hasText: 'Jill Martin' }).getByRole('button', { name: 'Edit', exact: true }).click()
    await userDialog.getByText('Person facts and certifications', { exact: true }).click(); await newerHeld
    assert.equal(await dates.count(), 0, 'the newer facts read has not replaced or refreshed the already-mounted legacy fields')
    releaseNewerEditor(); releaseNewerEditor = null; assert.equal((await newerResponse).status(), 200)
    await dates.waitFor()
    await dates.getByLabel(/^Start date/).fill('2026-11-01'); await dates.getByLabel('Reason', { exact: true }).fill('Record future start while preserving the concurrent Admin correction')
    await dates.getByRole('button', { name: 'Save person facts', exact: true }).click(); await userDialog.getByText(/Starts 2026-11-01/).waitFor()
    assert.equal(parentEdits.at(-1).version, '"3"'); assert.equal(people[1].rowVersion, 4)
    await userDialog.getByRole('button', { name: 'Save', exact: true }).click()
    await userDialog.getByRole('alert').filter({ hasText: 'Concurrent legacy fields changed. Reload the user before saving.' }).waitFor()
    assert.equal(legacyEdits.at(-1).version, '"2"', 'newer facts editor cannot advance an older legacy baseline')
    assert.equal(legacyEdits.at(-1).body.officeId, null); assert.equal(legacyEdits.at(-1).body.weeklyCapacityHours, null); assert.equal(legacyEdits.at(-1).body.isActive, true)
    assert.equal(people[1].officeId, id(1720)); assert.equal(people[1].weeklyCapacityHours, 26); assert.equal(people[1].isActive, false)
    await userDialog.getByRole('button', { name: 'Cancel', exact: true }).click(); await userDialog.waitFor({ state: 'hidden' })
    // A separate synthetic Admin correction reactivates Jill for the later active-person picker scenario.
    // Preserve the concurrent office/capacity/date values proved above; the picker must respect stored activation.
    people[1].isActive = true; people[1].rowVersion++
    await page.getByRole('button', { name: 'Certification tags', exact: true }).click()
    const vocabularyDialog = page.getByRole('dialog', { name: 'Certification tags', exact: true })
    const tagRow = vocabularyDialog.getByRole('listitem').filter({ has: page.locator('input[value="Confined space"]') })
    await tagRow.getByRole('checkbox', { name: 'Active', exact: true }).uncheck(); await tagRow.getByRole('button', { name: 'Save', exact: true }).click()
    await vocabularyDialog.getByLabel('Tag name', { exact: true }).last().fill('Bridge inspection'); await vocabularyDialog.getByRole('button', { name: 'Add', exact: true }).click()
    await vocabularyDialog.locator('input[value="Bridge inspection"]').waitFor(); assert.ok(requests.every(path => !path.includes('/usage')))
    await page.keyboard.press('Escape')
    await page.goto(`${base}/staff`); await page.locator('table').getByText(/Ends 2026-12-18/).waitFor(); await page.locator('table').getByText('· Inactive tag', { exact: true }).waitFor()
    await page.goto(`${base}/projects/${project.projectNumber}/team`); await page.getByRole('button', { name: 'Jill Martin', exact: true }).click()
    const search = page.getByPlaceholder('Search by name or email', { exact: true }); await search.fill('Jill')
    const candidate = page.getByRole('option').filter({ hasText: 'Jill Martin' }); await candidate.getByText(/Ends 2026-12-18/).waitFor()
    assert.equal(await candidate.locator('[data-person-certification]').count(), 0)
    const pickerWrite = page.waitForResponse(response => response.url().includes(`/projects/${project.id}/disciplines/${project.disciplines[0].id}`) && response.request().method() === 'PATCH')
    await search.press('ArrowDown'); await search.press('Enter'); assert.equal((await pickerWrite).status(), 204)
    assert.equal(pickerChoices.at(-1).leadUserId, jillId)
    await page.setViewportSize({ width: 375, height: 812 }); await page.goto(`${base}/preferences`); await editor.waitFor()
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    const readsBeforeOff = factReads.length
    me.flags.capacity_screens = false; me.flags.people_screens = true; await page.setViewportSize({ width: 1440, height: 1000 }); await page.reload()
    await page.getByRole('heading', { name: 'Notification preferences', exact: true }).waitFor(); assert.equal(await editor.count(), 0); assert.equal(factReads.length, readsBeforeOff)
    await page.goto(`${base}/staff`); await page.getByRole('heading', { name: 'My Staff', exact: true }).waitFor(); assert.equal(await page.locator('[data-person-facts]').count(), 0)
    assert.deepEqual(errors, []); assert.deepEqual(unexpectedWrites, []); assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: synthetic S29 self/Admin edits, exact expiry/unknown/inactive chips, Staff cards/table, scoped Team filters, version/replay safeguards, picker notices/selection, 375px and owner-flag parity')
  } catch (error) { console.error('person-facts-diagnostics', { url: page.url(), creates, edits, removals, parentEdits, legacyEdits, teamReads, factReads, pickerChoices, errors, unexpectedWrites, unknown: mock.unknownGets }); console.error(await page.locator('body').innerText()); throw error }
  finally { releaseNewerEditor?.(); await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
