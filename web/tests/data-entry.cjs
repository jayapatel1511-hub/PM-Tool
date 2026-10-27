// Packet 051 S01 (FR-SAV-01, FR-SAV-07, FR-SAV-08): per-field autosave in the edit panels, against a self-started production
// preview and the mocked platform API. Flag off: text saves on blur only, as before. Flag on: text also saves after
// autosave_delay_ms without typing, one save (one activity entry) per field change, and blur does not send it again. Saves from one
// field go one at a time, so a slow answer never sends an old row version; a field whose change needs a reason saves on blur only.
// Packet 051 S02 (FR-SAV-02, unflagged): with either flag, a save with no answer keeps the typed value, says "Not saved" and goes
// again on its own; a concurrency conflict shows both values and waits for "Keep mine" or "Take theirs". Retry shows with the flag.
// A failed save stays with the record it was typed for: the open panel switching to another cached task never shows or writes it.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, me, task, taskDetail, panelIds, deliverableDetail, links, comments } = require('./platform-fixtures.cjs')

const delay = 300
const port = Number(process.env.DATA_ENTRY_TEST_PORT ?? 7206)

async function run(base, on) {
  const { browser, page, mock } = await newMockPage(base)
  const errors = []; const patches = []
  let slow = 0, slowRead = 0, offline = false, aborted = 0
  page.on('pageerror', error => errors.push(error.message))
  const state = {
    Deliverable: structuredClone({ ...deliverableDetail, permissions: { ...deliverableDetail.permissions, edit: { ok: true } } }),
    Task: structuredClone(taskDetail),
  }
  const row = (type) => type === 'Task' ? state.Task.task : state.Deliverable.deliverable
  // Later routes win in Playwright: these sit in front of the shared mock for /me and the two panels.
  await page.route('**/api/v1/me', route => route.fulfill({ json: { ...me, flags: { ...me.flags, data_entry_safety: on },
    settings: on ? { ...me.settings, autosaveDelayMs: delay, draftKeepDays: 7 } : me.settings } }))
  await page.route(/\/api\/v1\/(tasks|deliverables)\/[^/]+$/, async route => {
    const request = route.request()
    const type = request.url().includes('/tasks/') ? 'Task' : 'Deliverable'
    if (request.method() === 'GET') { const json = structuredClone(state[type]); if (slowRead) await new Promise(resolve => setTimeout(resolve, slowRead)); return route.fulfill({ json }) }
    if (request.method() !== 'PATCH') return route.fallback()
    if (offline) { aborted++; return route.abort('internetdisconnected') }
    if (slow) await new Promise(resolve => setTimeout(resolve, slow))
    const body = request.postDataJSON()
    const ifMatch = Number(request.headers()['if-match']?.replaceAll('"', ''))
    patches.push({ type, body, ifMatch })
    if (ifMatch !== row(type).rowVersion) return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', message: 'Changed by someone else',
      changedBy: 'Pat Example', changedAt: new Date(Date.now() - 10000).toISOString(), currentRowVersion: row(type).rowVersion } })
    if (state[type].permissions.needsReason && !body.reason) return route.fulfill({ status: 400, json: { code: 'validation', message: 'Reason required', errors: { reason: ['Give a reason'] } } })
    if ('description' in body) state[type].description = body.description
    Object.assign(row(type), Object.fromEntries(Object.entries(body).filter(([k]) => k !== 'description')), { rowVersion: row(type).rowVersion + 1 })
    await route.fulfill({ json: { ...row(type), warnings: [] } })
  })
  const dialog = page.getByRole('dialog')
  const field = async (type, id, label, current) => {
    await page.goto(`${base}/my-work?panel=${type}:${id}`, { waitUntil: 'domcontentloaded' })
    await dialog.waitFor({ state: 'visible', timeout: 20000 })
    await dialog.getByRole('button', { name: current, exact: true }).first().click()
    return dialog.getByRole('textbox', { name: label, exact: true })
  }
  const wait = (ms) => page.waitForTimeout(ms)
  const until = async (check, ms, what) => { const end = Date.now() + ms; while (!check()) { if (Date.now() > end) assert.fail(what); await wait(50) } }
  try {
    // Deliverable panel: two fields, typed with a pause and left by Tab.
    let box = await field('Deliverable', panelIds.deliverable, 'Name', 'Synthetic deliverable')
    await box.fill('Renamed deliverable')
    await wait(delay * 3)
    assert.equal(patches.length, on ? 1 : 0, `flag ${on}: name saved after a pause only with the flag`)
    if (on) await dialog.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
    await box.press('Tab')
    await wait(delay * 2)
    assert.equal(patches.length, 1, `flag ${on}: leaving the field does not save the same value twice`)
    box = await field('Deliverable', panelIds.deliverable, 'Description', 'Synthetic deliverable detail')
    await box.fill('First line')
    await wait(delay * 3)
    await box.fill('First line, then more')
    await wait(delay * 3)
    await box.press('Tab')
    await wait(delay * 2)
    const deliverable = patches.filter(p => p.type === 'Deliverable')
    assert.deepEqual(deliverable.map(p => Object.keys(p.body)), on ? [['name'], ['description'], ['description']] : [['name'], ['description']], `flag ${on}: one save per field change, one field each`)
    assert.equal(state.Deliverable.description, 'First line, then more')
    assert.deepEqual(deliverable.map(p => p.ifMatch), deliverable.map((_, i) => i + 1), `flag ${on}: each save carries the current row version`)
    // Task panel (the reference): same save path and the same one save per change.
    box = await field('Task', task.id, 'Name', task.name)
    await box.fill('Renamed task')
    await wait(delay * 3)
    assert.equal(patches.filter(p => p.type === 'Task').length, on ? 1 : 0, `flag ${on}: task name saved after a pause only with the flag`)
    await box.press('Tab')
    await wait(delay * 2)
    assert.deepEqual(patches.filter(p => p.type === 'Task').map(p => p.body), [{ name: 'Renamed task' }], `flag ${on}: task panel saves the name once`)
    // Escape still discards an edit that was not saved yet, and a cancelled pause sends nothing.
    box = await field('Task', task.id, 'Name', 'Renamed task')
    await box.fill('Not kept')
    await box.press('Escape')
    await wait(delay * 3)
    assert.equal(patches.filter(p => p.type === 'Task').length, 1, `flag ${on}: Escape before the pause sends nothing`)
    const taskSaves = () => patches.filter(p => p.type === 'Task')
    let name = 'Renamed task'
    if (on) {
      // Typing on while a pause save is still on its way: the next save, from a pause or from Tab, waits for that answer and then
      // carries the new row version instead of being refused as someone else's change.
      slow = delay * 2
      box = await field('Task', task.id, 'Name', name)
      await box.fill('Renamed again')
      await wait(delay + 100)
      await box.fill('Renamed again twice')
      await wait(delay * 6)
      await box.fill('Renamed again thrice')
      await wait(delay + 100)
      await box.fill('Renamed for the last time')
      await box.press('Tab')
      await wait(delay * 6)
      slow = 0
      name = 'Renamed for the last time'
      assert.deepEqual(taskSaves().slice(1).map(p => [p.body.name, p.ifMatch]),
        [['Renamed again', 2], ['Renamed again twice', 3], ['Renamed again thrice', 4], [name, 5]], 'one save at a time, each with the row version before it')
      assert.equal(state.Task.task.name, name)
      assert.equal(await dialog.getByText(/Not saved/).count(), 0, 'no save was refused')
    }
    // FR-SAV-02: the network drops while a deliverable field saves. The field keeps the typed value with "Not saved", the save goes
    // again on its own 2 s later and lands once (one activity entry), and the older value never shows in the field (AC-SAV-02).
    const note = dialog.locator('span[aria-live="polite"]', { hasText: /^■ Not saved/ })
    const older = 'First line, then more'
    box = await field('Deliverable', panelIds.deliverable, 'Description', older)
    await page.evaluate((old) => {
      window.olderShown = false
      setInterval(() => { if ([...document.querySelectorAll('[role=dialog] button')].some(b => b.textContent.includes(old))) window.olderShown = true }, 20)
    }, older)
    offline = true
    await box.fill('Typed while offline')
    await box.press('Tab')
    await note.waitFor({ state: 'visible' })
    assert.equal(await note.textContent(), on ? '■ Not saved · Retry · Discard my change' : '■ Not saved · Discard my change', `flag ${on}: Retry is the flagged addition`)
    await dialog.getByRole('button', { name: 'Typed while offline', exact: true }).waitFor({ state: 'visible' })
    assert.ok(aborted > 0, 'the save met a dropped network')
    const reached = patches.length
    offline = false
    slowRead = 500 // the refetch after the save is slow, so a field that fell back to the server's copy would show the older value
    await until(() => state.Deliverable.description === 'Typed while offline', 8000, `flag ${on}: saved on its own within 8 s of the network coming back`)
    await dialog.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
    await wait(800)
    slowRead = 0
    assert.equal(patches.length - reached, 1, `flag ${on}: one save reached the server, so one activity entry`)
    assert.equal(await dialog.getByRole('button', { name: 'Typed while offline', exact: true }).count(), 1)
    assert.equal(await page.evaluate(() => window.olderShown), false, `flag ${on}: the older value never showed in the field`)
    // "Discard my change" is the only way back to the server's value, and it stops the timed retries.
    box = await field('Deliverable', panelIds.deliverable, 'Description', 'Typed while offline')
    offline = true
    await box.fill('Thrown away')
    await box.press('Tab')
    await note.getByRole('button', { name: 'Discard my change' }).click()
    await dialog.getByRole('button', { name: 'Typed while offline', exact: true }).waitFor({ state: 'visible' })
    const tried = aborted
    await wait(2500)
    assert.equal(aborted, tried, `flag ${on}: nothing goes again after Discard my change`)
    if (on) {
      // Retry sends at once, without waiting for the next timed retry.
      box = await field('Deliverable', panelIds.deliverable, 'Description', 'Typed while offline')
      await box.fill('Saved by Retry')
      await box.press('Tab')
      await note.waitFor({ state: 'visible' })
      offline = false
      await note.getByRole('button', { name: 'Retry', exact: true }).click()
      await until(() => state.Deliverable.description === 'Saved by Retry', 1500, 'Retry saves at once')
    }
    offline = false
    // FR-SAV-02: Pat saved the task name 10 s before Alex's save, which is refused as a conflict. Alex sees both values, who saved the
    // other one and when, and Keep mine saves Alex's value on the fresh version (AC-SAV-03); Take theirs sends nothing.
    const seedConflict = (theirs) => Object.assign(state.Task.task, { name: theirs, rowVersion: state.Task.task.rowVersion + 1 })
    const choice = dialog.locator('span[aria-live="polite"]', { hasText: 'Keep mine' })
    box = await field('Task', task.id, 'Name', name)
    seedConflict('Named by Pat')
    await box.fill('Named by Alex')
    await box.press('Tab')
    await choice.getByText(/^Pat Example, 1[01] s ago: Named by Pat$/).waitFor({ state: 'visible' })
    await choice.getByText('Yours: Named by Alex', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await dialog.getByRole('button', { name: 'Named by Alex', exact: true }).count(), 1, `flag ${on}: the field keeps Alex's value`)
    const fresh = state.Task.task.rowVersion
    await choice.getByRole('button', { name: 'Keep mine' }).click()
    await until(() => state.Task.task.name === 'Named by Alex', 3000, `flag ${on}: Keep mine saves`)
    assert.equal(taskSaves().at(-1).ifMatch, fresh, `flag ${on}: Keep mine goes on the fresh version`)
    box = await field('Task', task.id, 'Name', 'Named by Alex')
    seedConflict('Pat again')
    await box.fill('Alex again')
    await box.press('Tab')
    await choice.getByText(/: Pat again$/).waitFor({ state: 'visible' })
    const refused = taskSaves().length
    await choice.getByRole('button', { name: 'Take theirs' }).click()
    await dialog.getByRole('button', { name: 'Pat again', exact: true }).waitFor({ state: 'visible' })
    await wait(500)
    assert.equal(taskSaves().length, refused, `flag ${on}: Take theirs sends nothing`)
    name = 'Pat again'
    // FR-SAV-02: a failed save belongs to the record it was typed for. Task A's save fails, the open panel moves on to task B (already
    // cached, through the dependency link) and the network returns: B shows its own name with no "Not saved" and is never written to,
    // while A's timed retry still lands on A.
    const taskB = structuredClone(taskDetail)
    Object.assign(taskB.task, { id: '00000000-0000-0000-0000-000000000042', key: 'T-002', name: 'Second task' })
    const savedB = []
    const edge = (to) => ({ dependencyId: `00000000-0000-0000-0000-0000000000d${to.key.slice(-1)}`, lagDays: 0, satisfied: false,
      task: { id: to.id, key: to.key, name: to.name, status: 'Not Started', overdue: false, blocked: false, waiting: false } })
    await page.route(new RegExp(`/api/v1/tasks/${taskB.task.id}$`), route => {
      if (route.request().method() === 'PATCH') { savedB.push(route.request().postDataJSON()); return route.fulfill({ json: { ...taskB.task, warnings: [] } }) }
      return route.fulfill({ json: structuredClone(taskB) })
    })
    await page.route(`**/api/v1/tasks/${taskB.task.id}/dependencies`, route => route.fulfill({ json: { dependsOn: [edge(task)], blocks: [] } }))
    await page.route(`**/api/v1/tasks/${task.id}/dependencies`, route => route.fulfill({ json: { dependsOn: [edge(taskB.task)], blocks: [] } }))
    await page.route(`**/api/v1/items/Task/${taskB.task.id}/links`, route => route.fulfill({ json: links }))
    await page.route(`**/api/v1/items/Task/${taskB.task.id}/comments`, route => route.fulfill({ json: comments }))
    const nameButton = (text) => dialog.getByRole('button', { name: text, exact: true }).first()
    await page.goto(`${base}/my-work?panel=Task:${task.id}`, { waitUntil: 'domcontentloaded' })
    await nameButton(name).waitFor({ state: 'visible', timeout: 20000 })
    await dialog.getByRole('button', { name: /^T-002 / }).click()
    await nameButton('Second task').waitFor({ state: 'visible' })
    await dialog.getByRole('button', { name: /^T-001 / }).click()
    await nameButton(name).click()
    box = dialog.getByRole('textbox', { name: 'Name', exact: true })
    offline = true
    await box.fill('Typed for A')
    await box.press('Tab')
    await note.waitFor({ state: 'visible' })
    await dialog.getByRole('button', { name: /^T-002 / }).click()
    offline = false
    await nameButton('Second task').waitFor({ state: 'visible', timeout: 2000 })
    assert.equal(await note.count(), 0, `flag ${on}: the other task does not say "Not saved"`)
    await until(() => state.Task.task.name === 'Typed for A', 15000, `flag ${on}: the retry saves the text on the task it was typed for`)
    await wait(1000)
    assert.deepEqual(savedB, [], `flag ${on}: nothing is written to the other task`)
    assert.equal(await nameButton('Second task').count(), 1, `flag ${on}: the other task still shows its own name`)
    name = 'Typed for A'
    // A Complete project needs a reason for the change (FR-SAV-01): no save after a pause, so the reason is asked once, on Tab.
    state.Task.permissions.needsReason = true
    const before = taskSaves().length
    box = await field('Task', task.id, 'Name', name)
    await box.fill('Fixing a typo in')
    await wait(delay * 3)
    assert.equal(taskSaves().length, before, `flag ${on}: no pause save when the change needs a reason`)
    await box.press('Tab')
    const reason = page.getByRole('dialog', { name: `Why is ${task.key} changing?` })
    await reason.waitFor({ state: 'visible' })
    assert.deepEqual(taskSaves().slice(before).map(p => p.body), [{ name: 'Fixing a typo in' }], `flag ${on}: one save on Tab, which asks the reason`)
    await reason.getByRole('button', { name: 'Cancel' }).click()
    assert.deepEqual(errors, [], `browser errors: ${errors.join('; ')}`)
    assert.deepEqual(mock.unknownGets, [], `unhandled mocked GETs: ${mock.unknownGets.join('; ')}`)
    return { flag: on, saves: patches.length }
  } finally { await browser.close() }
}

;(async () => {
  const preview = await startPreview(port)
  try {
    const results = [await run(preview.base, false), await run(preview.base, true)]
    console.log(JSON.stringify({ result: 'PASS', results }))
  } finally { preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
