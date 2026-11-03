// G62: finite real-SPA UI proof with strict synthetic transport. Not API/persistence proof.
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const path = require('node:path')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { externalReviewFixture } = require('./external-review.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const screenshot = process.env.TUESDAY_EXTERNAL_REVIEW_SCREENSHOT
if (screenshot) assert.ok(path.isAbsolute(screenshot), 'The review screenshot output must be an absolute path')
const f = externalReviewFixture(project, me.id)
const projectPath = `projects/${project.id}`, packagePath = `${projectPath}/reviews/${f.p.id}`
const start = `${base}/projects/${project.projectNumber}/reviews?q=Synthetic&page=1&findingSeverity=Advisory`
const detailUrl = `${start}&panel=ReviewPackage:${f.p.id}`

;(async () => {
  const saved = { flags: { ...me.flags }, capabilities: { ...me.capabilities }, settings: { ...me.settings } }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const commands = [], unexpectedGets = [], unexpectedWrites = [], pageErrors = [], cases = [], receipts = new Map()
  let data = f.fresh(), choices = f.recordOptions(), armed = null, observed = null, readStatus = 200, optionsStatus = 200, stage = 'setup'
  page.setDefaultTimeout(15000); page.on('pageerror', error => pageErrors.push(error.message))
  const response = (route, status, message) => route.fulfill({ status, contentType: 'application/problem+json', json: { status,
    code: status === 409 ? 'concurrency_conflict' : 'external_review_refused', detail: message,
    errors: { reason: [message] }, ...(status === 409 ? { currentRowVersion: 999 } : {}) } })
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false]))
    Object.assign(me.flags, { register_screens: true, data_entry_safety: true, visual_system: process.env.TUESDAY_REVIEW_VISUAL === '1' })
    me.capabilities = { ...saved.capabilities, readOnly: false }; me.settings.today = '2026-11-02'
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        const raw = request.postData(), body = raw ? JSON.parse(raw) : null
        if (request.method() !== 'POST' || !armed || armed.path !== path) {
          unexpectedWrites.push({ method: request.method(), path, body }); return response(route, 409, 'Unarmed external-review write')
        }
        const mode = armed; armed = null
        commands.push({ path, raw, body }); assert.match(body.requestId, /^[0-9a-f]{8}-[0-9a-f-]{27}$/i)
        mode.check?.(body)
        let receipt = receipts.get(body.requestId)
        if (receipt) assert.equal(raw, receipt.raw, 'Retry must preserve the entire command, including reviewed versions')
        if (mode.kind === 'success' || mode.commit) {
          if (!receipt) {
            const result = path === `${packagePath}/external-reviews` ? f.record(data, body)
              : /\/findings\//.test(path) ? f.action(data, path.split('/').at(-2), body)
                : { id: f.p.id, rowVersion: data.package.rowVersion }
            receipt = { raw, result }; receipts.set(body.requestId, receipt)
          }
        }
        try {
          if (mode.kind === 'abort') return await route.abort('failed')
          if (mode.kind === 'malformed') return await route.fulfill({ json: { id: 'not-a-receipt', rowVersion: -1 } })
          if (mode.kind === 'refusal') return await response(route, mode.status, `Synthetic ${mode.status} external-review blocker`)
          return await route.fulfill({ json: receipt.result })
        } finally { observed?.(); observed = null }
      }
      if (path === `${projectPath}/reviews`) return route.fulfill({ json: f.packages(data) })
      if (path === `${projectPath}/changes/options`) return route.fulfill({ json: f.options })
      if (path === packagePath) return readStatus === 200 ? route.fulfill({ json: data }) : response(route, readStatus, 'Synthetic current review unavailable')
      if (path === `${packagePath}/external-reviews/options`) {
        if (!me.flags.register_screens) { unexpectedGets.push(path); return response(route, 404, 'Feature off') }
        return optionsStatus === 200 ? route.fulfill({ json: choices }) : response(route, optionsStatus, 'Synthetic choices unavailable')
      }
      if (path === `${projectPath}/reviews/issued-sources`) {
        if (!me.flags.register_screens) { unexpectedGets.push(path); return response(route, 404, 'Feature off') }
        return route.fulfill({ json: f.sources(url.searchParams) })
      }
      if (path === `${projectPath}/review-issued-assignment-guidance`) {
        if (!me.flags.register_screens) { unexpectedGets.push(path); return response(route, 404, 'Feature off') }
        assert.deepEqual([...url.searchParams.keys()], ['issuedRevisionIds'])
        return route.fulfill({ json: f.guidance((url.searchParams.get('issuedRevisionIds') ?? '').split(',').filter(Boolean)) })
      }
      if (path === `${projectPath}/review-findings`) return route.fulfill({ json: f.register() })
      if (path === `${projectPath}/reviews/discipline-summary`) return route.fulfill({ json: { disciplines: [] } })
      if (path === `${projectPath}/issues`) return route.fulfill({ json: [] })
      if (['config', 'me', 'reference', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count',
        `projects/${project.projectNumber}`, projectPath].includes(path)) return route.fallback()
      unexpectedGets.push(path); return route.fulfill({ status: 501, json: { code: 'unexpected_external_review_get', detail: path } })
    })
    const inspector = () => page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: `${f.p.key} · ${f.p.title}`, exact: true }) })
    const form = () => page.locator('[data-external-review-form]')
    const packageForm = () => page.locator('[data-issued-review-package-form]')
    const article = text => inspector().locator('article').filter({ has: page.getByText(text, { exact: true }) })
    const confirm = surface => surface.getByRole('button', { name: /^(Confirm|Retry external review)$/ })
    const reset = () => { data = f.fresh(); choices = f.recordOptions(); readStatus = 200; optionsStatus = 200; receipts.clear() }
    const go = async () => { await page.goto(detailUrl); await inspector().getByText('Synthetic carried external comment', { exact: true }).waitFor() }
    const open = async () => {
      const trigger = inspector().getByRole('button', { name: 'Record external review', exact: true })
      const node = await trigger.elementHandle(); await trigger.click(); await form().getByLabel('External party', { exact: true }).waitFor()
      await page.waitForFunction(node => node && !node.disabled, await form().getByLabel('External party', { exact: true }).elementHandle())
      return node
    }
    const fill = async (text = 'Synthetic new external comment') => {
      await form().getByLabel('Affected source revision', { exact: true }).selectOption(f.current.deliverableIssueId)
      await form().getByLabel('External party', { exact: true }).selectOption(f.party.id)
      await form().getByLabel('Return code', { exact: true }).selectOption(f.code.id)
      await form().getByLabel('Received date', { exact: true }).fill('2026-10-28')
      await form().getByLabel('Recording reason', { exact: true }).fill('Synthetic received response recorded for review')
      const comment = form().locator('[data-external-comment]').first()
      await comment.getByLabel('Finding', { exact: true }).fill(text)
      await comment.getByLabel('Discipline', { exact: true }).selectOption(f.discipline)
      await comment.getByLabel('Resolution owner', { exact: true }).selectOption(f.resolver)
    }
    const send = async (surface, mode) => {
      assert.equal(armed, null); armed = mode
      const done = new Promise(resolve => { observed = resolve })
      await confirm(surface).click(); await done
      if (mode.kind === 'success') await surface.waitFor({ state: 'detached' })
      else {
        const commandMessage = mode.kind === 'refusal' ? `Synthetic ${mode.status} external-review blocker`
          : mode.kind === 'abort' ? 'Tuesday could not be reached. This change may not have been saved. Check before trying again.'
            : 'Something went wrong.'
        await surface.getByRole('alert').filter({ hasText: commandMessage }).waitFor()
      }
      return commands.at(-1)
    }
    const cancel = async (surface, dirty, trigger) => {
      const count = commands.length
      await surface.getByRole('button', { name: 'Cancel', exact: true }).click()
      if (dirty) {
        const question = page.getByRole('dialog', { includeHidden: true }).filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true, includeHidden: true }) })
        await question.getByRole('button', { name: 'Discard', exact: true, includeHidden: true }).click(); await question.waitFor({ state: 'detached' })
      }
      await surface.waitFor({ state: 'detached' }); assert.equal(commands.length, count)
      if (trigger) await page.waitForFunction(node => node?.isConnected && document.activeElement === node, trigger)
    }
    const noOverflow = async surface => {
      const widths = await surface.evaluate(node => ({ width: node.clientWidth, scroll: node.scrollWidth, right: node.getBoundingClientRect().right, viewport: innerWidth }))
      assert.ok(widths.scroll <= widths.width + 1 && widths.right <= widths.viewport + 1, JSON.stringify(widths))
    }
    const checkRecord = (body, version = 11) => {
      assert.deepEqual(Object.keys(body).sort(), ['requestId', 'rowVersion', 'deliverableIssueId', 'issueRowVersion', 'externalPartyId', 'partyRowVersion', 'receivedDate', 'returnCodeId', 'returnCodeRowVersion', 'reason', 'comments'].sort())
      assert.equal(body.rowVersion, version); assert.equal(body.deliverableIssueId, f.current.deliverableIssueId); assert.equal(body.issueRowVersion, 8)
      assert.equal(body.externalPartyId, f.party.id); assert.equal(body.partyRowVersion, 5); assert.equal(body.returnCodeId, f.code.id); assert.equal(body.returnCodeRowVersion, 6)
      assert.equal(body.receivedDate, '2026-10-28'); assert.equal(body.comments.length, 1)
      assert.deepEqual(Object.keys(body.comments[0]).sort(), ['text', 'projectDisciplineId', 'resolverId', 'severity', 'issueId'].sort())
      assert.equal(body.comments[0].resolverId, f.resolver); assert.equal(body.comments[0].projectDisciplineId, f.discipline)
      assert.equal(body.comments[0].issueId, null)
    }

    stage = 'normal record and provenance'
    await go()
    assert.equal(data.package.status, 'Approved')
    assert.equal(await inspector().getByRole('button', { name: 'Record external review', exact: true }).count(), 1, 'The server allows recording on a current started Approved package')
    const carried = article('Synthetic carried external comment'), context = carried.locator('[data-external-finding-context]')
    assert.equal(await context.getByRole('button', { name: f.party.name, exact: true }).count(), 0, 'An external party has no internal person action')
    assert.match(await context.innerText(), /Originally received against[\s\S]*D-EXT-01 · A/)
    assert.match(await context.innerText(), /Reviewed in this round[\s\S]*Revision not recorded/)
    assert.equal(await carried.getByText('Unassigned — PM designation required', { exact: true }).count(), 1)
    let opener = await open()
    assert.equal(await form().getByLabel('Received date', { exact: true }).inputValue(), '')
    assert.equal(await form().getByLabel('External party', { exact: true }).inputValue(), '')
    assert.equal(await confirm(form()).isDisabled(), true)
    assert.equal(commands.length, 0, 'Blank selections disable confirmation and make no write')
    await fill()
    await form().getByLabel('Received date', { exact: true }).fill('2026-10-01')
    await confirm(form()).click(); assert.equal(commands.length, 0, 'A received date before the actual issue date makes no write')
    await form().getByLabel('Received date', { exact: true }).fill('2026-10-28')
    const longReason = `Synthetic received response recording reason ${'with retained context '.repeat(200)}`
    await form().getByLabel('Recording reason', { exact: true }).fill(longReason)
    assert.ok(longReason.length > 4000)
    assert.equal(await form().getByLabel('Recording reason', { exact: true }).inputValue(), longReason)
    await noOverflow(form())
    if (screenshot) {
      const reasonField = form().getByLabel('Recording reason', { exact: true })
      await reasonField.fill('Synthetic response received for the issued drawing; comments recorded for review.')
      try {
        await form().getByLabel('Affected source revision', { exact: true }).scrollIntoViewIfNeeded()
        await fs.writeFile(screenshot, await page.screenshot({ fullPage: false }), { flag: 'wx' })
      } finally { await reasonField.fill(longReason) }
      assert.equal(await reasonField.inputValue(), longReason)
    }
    await send(form(), { path: `${packagePath}/external-reviews`, kind: 'success', check: body => { checkRecord(body); assert.equal(body.reason, longReason) } })
    await article('Synthetic new external comment').waitFor()
    assert.equal(await article('Synthetic new external comment').getByText('Unassigned — PM designation required', { exact: true }).count(), 1)
    assert.equal(commands.length, 1, 'Recording did not issue a verifier, response, or closure command')
    cases.push('Normal current-started record with explicit actual IDs, four versions, disabled blank confirmation, native invalid-date refusal, reason over4000 preserved in wire, typed party provenance, original/current issued revision, no automatic verifier')

    stage = 'PM verifier and own closure evidence'
    const finding = data.findings.find(row => row.text === 'Synthetic carried external comment')
    await carried.getByRole('button', { name: 'Reassign verifier', exact: true }).click()
    let action = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Reassign independent verifier', exact: true }) })
    assert.equal(await action.getByLabel('Independent verifier', { exact: true }).inputValue(), '')
    assert.equal(await action.getByLabel('Independent verifier', { exact: true }).locator(`option[value="${f.resolver}"]`).count(), 1, 'Canonical server policy, including AllowSelfReview, decides independence; the client adds no resolver veto')
    await action.getByLabel('Independent verifier', { exact: true }).selectOption(f.viewer)
    await action.getByLabel('Reason or response', { exact: true }).fill('Synthetic PM explicitly designates the independent reviewer')
    await send(action, { path: `${packagePath}/findings/${finding.id}/action`, kind: 'success', check: body => { assert.equal(body.action, 'assignVerifier'); assert.equal(body.ownerId, f.viewer); assert.equal(body.rowVersion, 3) } })
    await carried.getByRole('button', { name: 'Verify closure', exact: true }).click()
    action = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Verify finding closure', exact: true }) })
    assert.equal(await action.getByLabel('Verifier closure evidence', { exact: true }).inputValue(), '', 'Never copy the resolver evidence into the verifier signature')
    await action.getByLabel('Reason or response', { exact: true }).fill('Synthetic independent check confirms the correction')
    const countBeforeEvidence = commands.length
    await confirm(action).click(); assert.equal(commands.length, countBeforeEvidence)
    await action.getByLabel('Verifier closure evidence', { exact: true }).fill('https://example.test/verifier-evidence')
    await send(action, { path: `${packagePath}/findings/${finding.id}/action`, kind: 'success', check: body => { assert.equal(body.action, 'Verified Closed'); assert.equal(body.evidenceUrl, 'https://example.test/verifier-evidence'); assert.equal(body.rowVersion, 4) } })
    // The visible status pill also contains its check icon; the exact text-only span is inside the still-closed History.
    await carried.locator('[data-semantic-tone="done"]').filter({ hasText: 'Verified Closed' }).waitFor()
    assert.equal(finding.evidenceUrl, 'https://example.test/resolver-evidence')
    await carried.getByText('History', { exact: true }).click()
    assert.equal(await carried.getByRole('link', { name: 'Verifier closure evidence', exact: true }).getAttribute('href'), 'https://example.test/verifier-evidence')
    cases.push('Explicit PM designation and separate verifier reason/evidence; resolver evidence remains distinct; closure event retains verifier evidence')

    stage = 'stale reviewed versions'
    reset(); await go(); opener = await open(); await fill('Synthetic stale-version draft')
    choices.parties[0].rowVersion = 50; choices.returnCodes[0].rowVersion = 60; choices.issuedRevisions[0].rowVersion = 80
    const staleOptionsRead = page.waitForResponse(response => response.url().endsWith('/external-reviews/options'))
    await form().getByRole('button', { name: 'Check current choices', exact: true }).click(); await staleOptionsRead
    assert.deepEqual(await Promise.all([
      form().getByLabel('Affected source revision', { exact: true }).inputValue(),
      form().getByLabel('External party', { exact: true }).inputValue(),
      form().getByLabel('Return code', { exact: true }).inputValue(),
    ]), [f.current.deliverableIssueId, f.party.id, f.code.id])
    await send(form(), { path: `${packagePath}/external-reviews`, kind: 'refusal', status: 409, check: checkRecord })
    assert.equal(await form().getByLabel('Recording reason', { exact: true }).inputValue(), 'Synthetic received response recorded for review')
    assert.match(await form().innerText(), /Synthetic 409 external-review blocker/)
    await cancel(form(), true, opener)
    cases.push('409 preserves typed draft and originally selected issue/party/code/package versions after normal options reread; no silent promotion')

    stage = 'uncertain receipt retry'
    reset(); await go(); opener = await open(); await fill('Synthetic uncertain recorded comment')
    const first = await send(form(), { path: `${packagePath}/external-reviews`, kind: 'abort', commit: true, check: checkRecord })
    assert.equal(await form().getByLabel('Recording reason', { exact: true }).isDisabled(), true)
    const uncertain403 = await send(form(), { path: `${packagePath}/external-reviews`, kind: 'refusal', status: 403, check: checkRecord })
    assert.equal(uncertain403.raw, first.raw)
    assert.equal(await form().getByLabel('Recording reason', { exact: true }).isDisabled(), true, 'A refusal after uncertainty must not unlock a different command')
    const retry = await send(form(), { path: `${packagePath}/external-reviews`, kind: 'success', check: checkRecord })
    assert.equal(retry.raw, first.raw); assert.equal(data.findings.filter(row => row.text === 'Synthetic uncertain recorded comment').length, 1)
    cases.push('Lost response after synthetic commit retries byte-identical body/request ID; later 403 stays uncertain; explicit success produces one synthetic receipt')

    stage = 'malformed success'
    reset(); await go(); opener = await open(); await fill('Synthetic malformed-success draft')
    await send(form(), { path: `${packagePath}/external-reviews`, kind: 'malformed', check: checkRecord })
    assert.equal(await form().getByRole('button', { name: 'Retry external review', exact: true }).count(), 1)
    assert.equal(await form().getByLabel('Recording reason', { exact: true }).isDisabled(), true)
    await cancel(form(), true, opener)
    cases.push('Malformed success is unconfirmed and locks the exact command rather than clearing the draft')

    stage = 'read and permission loss'
    reset(); await go(); opener = await open(); await fill('Synthetic retained authority draft')
    await send(form(), { path: `${packagePath}/external-reviews`, kind: 'refusal', status: 403, check: checkRecord })
    assert.equal(await form().getByLabel('Recording reason', { exact: true }).isDisabled(), false)
    assert.equal(await form().getByLabel('Finding', { exact: true }).inputValue(), 'Synthetic retained authority draft')
    const beforeReads = commands.length
    optionsStatus = 403
    let pending = page.waitForResponse(response => response.url().endsWith('/external-reviews/options'))
    await form().getByRole('button', { name: 'Check current choices', exact: true }).click(); await pending
    await form().getByRole('alert').filter({ hasText: 'Synthetic choices unavailable' }).waitFor()
    await form().getByRole('alert').filter({ hasText: 'Synthetic 403 external-review blocker' }).waitFor()
    assert.equal(await confirm(form()).isDisabled(), true)
    assert.equal(await form().getByLabel('Recording reason', { exact: true }).inputValue(), 'Synthetic received response recorded for review')
    optionsStatus = 200; choices.canRecord = false
    pending = page.waitForResponse(response => response.url().endsWith('/external-reviews/options'))
    await form().getByRole('button', { name: 'Check current choices', exact: true }).click(); await pending
    assert.equal(await confirm(form()).isDisabled(), true)
    choices.canRecord = true
    pending = page.waitForResponse(response => response.url().endsWith('/external-reviews/options'))
    await form().getByRole('button', { name: 'Check current choices', exact: true }).click(); await pending
    await page.waitForFunction(node => node && !node.disabled, await confirm(form()).elementHandle())
    readStatus = 403
    pending = page.waitForResponse(response => response.url().endsWith(`/reviews/${f.p.id}`))
    await form().getByRole('button', { name: 'Check current review', exact: true }).click(); await pending
    await form().waitFor({ state: 'hidden' })
    const unavailableRecordTitle = await inspector().getByRole('heading', { name: `${f.p.key} · ${f.p.title}`, exact: true }).elementHandle()
    await page.waitForFunction(node => node?.isConnected && document.activeElement === node, unavailableRecordTitle)
    readStatus = 200
    await inspector().getByRole('button', { name: 'Check current review', exact: true }).click()
    await form().waitFor(); assert.equal(await form().getByLabel('Finding', { exact: true }).inputValue(), 'Synthetic retained authority draft')
    assert.equal(commands.length, beforeReads, 'Read failures, lost permission, and recovery cannot issue a POST')
    assert.equal(await opener.evaluate(node => node.isConnected), false, 'The parent error removed the original opener node')
    // The recovered parent is aria-hidden while its child form is open. Capture its actual replacement without moving focus.
    const recoveredOpener = await page.getByRole('button', { name: 'Record external review', exact: true, includeHidden: true }).elementHandle()
    assert.ok(recoveredOpener, 'The successfully recovered parent renders its exact record action again')
    await cancel(form(), true, recoveredOpener)
    cases.push('Normal command 403 retains editable draft; options 403 and canRecord=false disable retained form; parent 403 hides portal without unmounting draft; explicit reread restores it; reads issue zero writes')

    stage = '375 keyboard, dirty cancel, natural focus'
    reset(); await page.setViewportSize({ width: 375, height: 812 }); await go()
    const trigger = inspector().getByRole('button', { name: 'Record external review', exact: true })
    opener = await trigger.elementHandle(); await trigger.focus(); await page.keyboard.press('Enter'); await form().waitFor()
    await fill('Synthetic mobile keyboard draft'); await noOverflow(form())
    await form().getByRole('button', { name: 'Cancel', exact: true }).focus(); await page.keyboard.press('Enter')
    let question = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true }) })
    await question.getByRole('button', { name: 'Keep editing', exact: true }).click(); await question.waitFor({ state: 'detached' })
    assert.equal(await form().getByLabel('Finding', { exact: true }).inputValue(), 'Synthetic mobile keyboard draft')
    await cancel(form(), true, opener)
    const retained = new URL(page.url()); assert.equal(retained.searchParams.get('q'), 'Synthetic'); assert.equal(retained.searchParams.get('page'), '1'); assert.equal(retained.searchParams.get('findingSeverity'), 'Advisory')
    cases.push('375px real form has no overflow; Enter opens and cancels; dirty Keep editing preserves text; Discard restores actual opener focus and list filters')

    stage = 'issued package create and new round'
    await page.setViewportSize({ width: 1440, height: 1000 }); reset(); await page.goto(start)
    await page.getByRole('button', { name: 'New review package', exact: true }).click()
    let newForm = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'New review package', exact: true }) })
    await newForm.getByLabel('Review sources', { exact: true }).selectOption('issued'); await packageForm().waitFor()
    const unknownCheck = packageForm().getByLabel(/D-UNKNOWN/)
    assert.equal(await unknownCheck.isDisabled(), true)
    await packageForm().getByLabel('Title', { exact: true }).fill('Synthetic issued-only package')
    await packageForm().getByLabel('Discipline', { exact: true }).selectOption(f.discipline)
    await packageForm().getByLabel('Coordinator', { exact: true }).selectOption(f.viewer)
    await packageForm().getByLabel('Review purpose and scope', { exact: true }).fill('Synthetic actual issued revision review')
    await packageForm().locator('[data-issued-source-picker]').getByRole('checkbox').first().check()
    await packageForm().getByRole('checkbox', { name: 'Civil', exact: true }).check()
    await packageForm().getByLabel('Reviewer', { exact: true }).selectOption(f.viewer)
    assert.equal(await packageForm().getByLabel('Reviewer', { exact: true }).locator(`option[value="${f.author}"]`).count(), 0)
    await send(packageForm(), { path: `${projectPath}/reviews`, kind: 'success', check: body => { assert.deepEqual(body.sourceRevisionIds, []); assert.deepEqual(body.issuedRevisionIds, [f.current.deliverableIssueId]); assert.equal(body.requiredForIssue, false); assert.equal(body.assignments[0].reviewerId, f.viewer) } })
    await inspector().getByRole('button', { name: 'Start a new round', exact: true }).click(); await packageForm().waitFor()
    assert.equal(await packageForm().getByLabel('Review sources', { exact: true }).count(), 0, 'An existing package cannot switch source mode')
    await packageForm().locator('[data-issued-source-picker]').getByRole('checkbox').first().check()
    await packageForm().getByLabel('Reason or response', { exact: true }).fill('Synthetic next round on the selected actual issue')
    stage = 'new round parent read and coordinator loss'
    const beforeRoundReads = commands.length
    for (const status of [403, 404]) {
      readStatus = status
      let read = page.waitForResponse(response => response.url().endsWith(`/reviews/${f.p.id}`))
      await packageForm().getByRole('button', { name: 'Check current review', exact: true }).click(); await read
      await packageForm().waitFor({ state: 'hidden' })
      assert.equal(await page.locator('[data-issued-review-package-form]:visible').count(), 0, `${status} hides the owned new-round portal`)
      const unavailableRoundTitle = await inspector().getByRole('heading', { name: `${f.p.key} · ${f.p.title}`, exact: true }).elementHandle()
      await page.waitForFunction(node => node?.isConnected && document.activeElement === node, unavailableRoundTitle)
      assert.equal(commands.length, beforeRoundReads)
      readStatus = 200
      read = page.waitForResponse(response => response.url().endsWith(`/reviews/${f.p.id}`))
      await inspector().getByRole('button', { name: 'Check current review', exact: true }).click(); await read
      await packageForm().waitFor()
      assert.equal(await packageForm().getByLabel('Reason or response', { exact: true }).inputValue(), 'Synthetic next round on the selected actual issue')
      assert.equal(await packageForm().getByLabel('Reviewer', { exact: true }).inputValue(), f.viewer)
      assert.equal(await packageForm().locator('[data-issued-source-picker]').getByRole('checkbox').first().isChecked(), true)
    }
    data.canCoordinate = false
    let roleRead = page.waitForResponse(response => response.url().endsWith(`/reviews/${f.p.id}`))
    await packageForm().getByRole('button', { name: 'Check current review', exact: true }).click(); await roleRead
    await packageForm().waitFor({ state: 'hidden' })
    assert.equal(await page.locator('[data-issued-review-package-form]:visible').count(), 0, 'A current successful canCoordinate=false read also hides confirmation')
    const readOnlyRoundTitle = await inspector().getByRole('heading', { name: `${f.p.key} · ${f.p.title}`, exact: true }).elementHandle()
    await page.waitForFunction(node => node?.isConnected && document.activeElement === node, readOnlyRoundTitle)
    assert.equal(commands.length, beforeRoundReads)
    data.canCoordinate = true
    roleRead = page.waitForResponse(response => response.url().endsWith(`/reviews/${f.p.id}`))
    await inspector().getByRole('button', { name: 'Check current review', exact: true }).click(); await roleRead
    await packageForm().waitFor()
    assert.equal(await packageForm().getByLabel('Reason or response', { exact: true }).inputValue(), 'Synthetic next round on the selected actual issue')
    assert.equal(await packageForm().getByLabel('Reviewer', { exact: true }).inputValue(), f.viewer)
    assert.equal(commands.length, beforeRoundReads, 'All denied/read-only/recovery paths issue zero new-round commands')
    await cancel(packageForm(), true)
    const recoveredRoundTrigger = await inspector().getByRole('button', { name: 'Start a new round', exact: true }).elementHandle()
    await page.waitForFunction(node => node?.isConnected && document.activeElement === node, recoveredRoundTrigger)
    assert.equal(commands.length, beforeRoundReads)
    cases.push('Issued new-round parent403/404 and successful canCoordinate=false hide confirmation with focus on exact parent title, preserve mounted draft/source/assignment, recover only after explicit successful reread, and dirty Cancel naturally focuses the current exact new-round trigger')
    await inspector().getByRole('button', { name: 'Start a new round', exact: true }).click(); await packageForm().waitFor()
    await packageForm().locator('[data-issued-source-picker]').getByRole('checkbox').first().check()
    await packageForm().getByLabel('Reason or response', { exact: true }).fill('Synthetic next round on the selected actual issue')
    await send(packageForm(), { path: `${packagePath}/rounds`, kind: 'success', check: body => { assert.deepEqual(body.sourceRevisionIds, []); assert.deepEqual(body.issuedRevisionIds, [f.current.deliverableIssueId]); assert.equal(body.rowVersion, 11); assert.equal('requiredForIssue' in body, false) } })
    cases.push('Create and next round use actual issued IDs plus empty internal IDs, Unknown catalog revisions disabled, captured-author exclusion, supplementary false, immutable source mode, reviewed package version')

    stage = 'role and flag off parity'
    reset(); data.canRecordExternalReview = false; data.canReassignVerifier = false; data.canCoordinate = false; data.canManage = false; data.canWrite = false
    await go()
    assert.equal(await inspector().getByRole('button', { name: 'Record external review', exact: true }).count(), 0)
    assert.equal(await inspector().getByRole('button', { name: 'Reassign verifier', exact: true }).count(), 0)
    me.flags.register_screens = false
    // Use an actual internal-only shape for off parity; the API hides external packages when the flag is off.
    data = f.fresh(); data.manifest = []; delete data.externalReviews; delete data.canRecordExternalReview
    data.findings = []; data.package.currentRoundId = f.p.currentRoundId
    await page.goto(start); await page.getByRole('button', { name: 'New review package', exact: true }).click()
    newForm = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'New review package', exact: true }) })
    assert.equal(await newForm.getByLabel('Review sources', { exact: true }).count(), 0)
    assert.equal(await newForm.getByText('External reviews are supplementary and do not gate issue', { exact: true }).count(), 0)
    await newForm.getByLabel('Title', { exact: true }).fill('Synthetic legacy internal review')
    await newForm.getByLabel('Discipline', { exact: true }).selectOption(f.discipline)
    await newForm.getByLabel('Coordinator', { exact: true }).selectOption(f.viewer)
    await newForm.getByLabel('Review purpose and scope', { exact: true }).fill('Synthetic old wire path')
    await newForm.getByRole('checkbox', { name: /SRC-INTERNAL/ }).check()
    await newForm.getByRole('checkbox', { name: 'Civil', exact: true }).check()
    await newForm.getByLabel('Reviewer', { exact: true }).selectOption(f.viewer)
    await send(newForm, { path: `${projectPath}/reviews`, kind: 'success', check: body => {
      assert.deepEqual(Object.keys(body).sort(), ['requestId', 'title', 'purpose', 'projectDisciplineId', 'coordinatorId', 'sourceRevisionIds', 'assignments', 'requiredForIssue', 'reason'].sort())
      assert.deepEqual(body.sourceRevisionIds, [f.internalSource.id]); assert.equal(body.requiredForIssue, true); assert.equal('issuedRevisionIds' in body, false)
    } })
    cases.push('Role-off hides record/designation; register_screens off has no new controls/reads and retains exact internal create body')

    assert.deepEqual(unexpectedGets, []); assert.deepEqual(unexpectedWrites, []); assert.deepEqual(pageErrors, []); assert.deepEqual(mock.unknownGets, [])
    const result = { status: 'PASS', boundary: 'Synthetic strict transport against real built SPA; no provider authorization/persistence claim', cases,
      counters: { commands: commands.length, unexpectedGets: 0, unexpectedWrites: 0, pageErrors: 0 }, desktopAnd375: true, visualSystem: process.env.TUESDAY_REVIEW_VISUAL === '1' }
    if (process.env.TUESDAY_EXTERNAL_REVIEW_OUTPUT) await fs.writeFile(process.env.TUESDAY_EXTERNAL_REVIEW_OUTPUT, JSON.stringify(result, null, 2) + '\n', { flag: 'wx' })
    console.log(JSON.stringify(result, null, 2))
  } catch (error) {
    const focus = await page.evaluate(() => {
      const active = document.activeElement
      return { tag: active?.tagName ?? null, id: active?.id ?? null, role: active?.getAttribute('role') ?? null,
        label: active?.getAttribute('aria-label') ?? null, text: active && active !== document.body ? active.textContent?.slice(0, 120) : null,
        dialogTitle: active?.closest('[role="dialog"]')?.querySelector('h2')?.textContent ?? null }
    }).catch(error => ({ unavailable: String(error) }))
    console.error(JSON.stringify({ stage, completedCases: cases, currentUrl: page.url(), commands: commands.length, unexpectedGets, unexpectedWrites, pageErrors, baseUnknownGets: mock.unknownGets, focus }))
    throw error
  } finally { me.flags = saved.flags; me.capabilities = saved.capabilities; me.settings = saved.settings; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
