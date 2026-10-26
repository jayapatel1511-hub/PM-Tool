// Packet 051 S01 (FR-SAV-01, FR-SAV-07, FR-SAV-08): per-field autosave in the edit panels, against a self-started production
// preview and the mocked platform API. Flag off: text saves on blur only, as before. Flag on: text also saves after
// autosave_delay_ms without typing, one save (one activity entry) per field change, and blur does not send it again. Saves from one
// field go one at a time, so a slow answer never sends an old row version; a field whose change needs a reason saves on blur only.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, me, task, taskDetail, panelIds, deliverableDetail } = require('./platform-fixtures.cjs')

const delay = 300
const port = Number(process.env.DATA_ENTRY_TEST_PORT ?? 7206)

async function run(base, on) {
  const { browser, page, mock } = await newMockPage(base)
  const errors = []; const patches = []
  let slow = 0
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
    if (request.method() === 'GET') return route.fulfill({ json: state[type] })
    if (request.method() !== 'PATCH') return route.fallback()
    if (slow) await new Promise(resolve => setTimeout(resolve, slow))
    const body = request.postDataJSON()
    const ifMatch = Number(request.headers()['if-match']?.replaceAll('"', ''))
    patches.push({ type, body, ifMatch })
    if (ifMatch !== row(type).rowVersion) return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', message: 'Changed by someone else' } })
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
