// Packet 051 S01 (FR-SAV-01, FR-SAV-07, FR-SAV-08): per-field autosave in the edit panels, against a self-started production
// preview and the mocked platform API. Flag off: text saves on blur only, as before. Flag on: text also saves after
// autosave_delay_ms without typing, one save (one activity entry) per field change, and blur does not send it again. Saves from one
// field go one at a time, so a slow answer never sends an old row version; a field whose change needs a reason saves on blur only.
// Packet 051 S02 (FR-SAV-02, unflagged): with either flag, a save with no answer keeps the typed value, says "Not saved" and goes
// again on its own; a concurrency conflict shows both values and waits for "Keep mine" or "Take theirs". Retry shows with the flag.
// A failed save stays with the record it was typed for: the open panel switching to another cached task never shows or writes it.
// Packet 051 S03 (FR-SAV-03, FR-SAV-04, FR-SAV-06): a create dialog and a comment keep a local draft that Escape and reopening restore,
// Discard and a successful submit drop, an empty form never writes and `draft_keep_days` prunes; without a draft (storage refusing
// it) the leave prompt asks on close, on navigation and on tab close, and the build-update Reload goes through the same guard.
// An edit form has no draft key but asks the same way once something is changed; the New template dialog keeps a draft.
// Packet 051 S04 (FR-SAV-05, AC-SAV-04): when the sign-in session expires (401), a half-written comment is still in the editor on the
// same item after sign-in, and an edit-panel field saves its typed value once on the fresh row version; the flag off keeps today's behaviour.
const assert = require('node:assert/strict')
const { execFileSync } = require('node:child_process')
const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')
const { startPreview, newMockPage, me, project, task, taskDetail, panelIds, deliverableDetail, links, comments } = require('./platform-fixtures.cjs')
const { milestoneRow } = require('./milestones.fixture.cjs')
const { dependencyEdge, dependencyTask, taskDependencies } = require('./dependencies.fixture.cjs')
const { templateList } = require('./templates.fixture.cjs')

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
    const edge = (to) => dependencyEdge({ dependencyId: `00000000-0000-0000-0000-0000000000d${to.key.slice(-1)}`, task: dependencyTask({ id: to.id, key: to.key, name: to.name }) })
    await page.route(new RegExp(`/api/v1/tasks/${taskB.task.id}$`), route => {
      if (route.request().method() === 'PATCH') { savedB.push(route.request().postDataJSON()); return route.fulfill({ json: { ...taskB.task, warnings: [] } }) }
      return route.fulfill({ json: structuredClone(taskB) })
    })
    await page.route(`**/api/v1/tasks/${taskB.task.id}/dependencies`, route => route.fulfill({ json: taskDependencies([edge(task)]) }))
    await page.route(`**/api/v1/tasks/${task.id}/dependencies`, route => route.fulfill({ json: taskDependencies([edge(taskB.task)]) }))
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

// ---- S03: drafts and the leave prompt ----

const NOTICE_BUILD = 'mock-build-notice'
const DAY = 86_400_000
const discipline = project.disciplines[0].id
const until = async (check, ms, what) => { const end = Date.now() + ms; while (!check()) { if (Date.now() > end) assert.fail(what); await new Promise(resolve => setTimeout(resolve, 50)) } }

// Serious or critical WCAG A/AA findings inside one element.
async function axeCheck(page, locator, what) {
  if (!(await page.evaluate(() => !!window.axe))) await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  const found = await locator.evaluate(async (el) => (await axe.run(el, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } }))
    .violations.filter(x => ['serious', 'critical'].includes(x.impact)).map(x => x.id))
  assert.deepEqual(found, [], `${what}: no serious accessibility violations`)
}

async function draftPage(base, on, { blocked = false } = {}) {
  const { browser, page, mock } = await newMockPage(base)
  const errors = []
  page.on('pageerror', error => errors.push(error.message))
  // A browser that will not keep a draft (private window, full storage, blocked site data).
  if (blocked) await page.addInitScript(() => {
    const set = Storage.prototype.setItem
    Storage.prototype.setItem = function (key, value) { if (String(key).startsWith('draft.')) throw new DOMException('blocked', 'QuotaExceededError'); return set.call(this, key, value) }
  })
  await page.route('**/api/v1/me', route => route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: { ...me, flags: { ...me.flags, data_entry_safety: on },
    settings: on ? { ...me.settings, autosaveDelayMs: delay, draftKeepDays: 7 } : me.settings } }))
  const allowed = { ...project, disciplines: project.disciplines.map(d => ({ ...d, isActive: true })), permissions: { ...project.permissions, createTaskIn: [discipline], manageMilestones: { ok: true }, createMilestone: { ok: true } } }
  await page.route(new RegExp(`/api/v1/projects/(${project.projectNumber}|${project.id})$`), route => route.fulfill({ json: allowed }))
  const created = []
  await page.route(new RegExp(`/api/v1/projects/${project.id}/tasks$`), route => {
    if (route.request().method() !== 'POST') return route.fallback()
    created.push(route.request().postDataJSON()); return route.fulfill({ json: { id: task.id, key: 'T-009' } })
  })
  const comments = []
  await page.route(`**/api/v1/items/Task/${task.id}/comments`, route => {
    if (route.request().method() !== 'POST') return route.fallback()
    comments.push(route.request().postDataJSON()); return route.fulfill({ json: { id: '00000000-0000-0000-0000-000000000499' } })
  })
  const drafts = () => page.evaluate(() => Object.keys(localStorage).filter(k => k.startsWith('draft.')).sort())
  const draftOf = (key) => page.evaluate((k) => JSON.parse(localStorage.getItem(k) ?? 'null'), key)
  const wait = (ms) => page.waitForTimeout(ms)
  const prompt = page.getByRole('dialog', { name: 'Keep editing or discard?' })
  const newTask = page.getByRole('dialog', { name: 'New task' })
  const openNewTask = async () => {
    await page.getByRole('button', { name: 'New', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Task', exact: true }).click()
    const pick = page.getByRole('button', { name: 'Continue' })
    await pick.waitFor({ state: 'visible' })
    await page.waitForFunction(() => { const b = [...document.querySelectorAll('button')].find(x => x.textContent === 'Continue'); return b && !b.disabled })
    await pick.click()
    await newTask.locator('#t-name').waitFor({ state: 'visible' })
  }
  const start = async (url = '/my-work') => { await page.goto(`${base}${url}`, { waitUntil: 'domcontentloaded' }); await page.getByRole(url.includes('panel=') ? 'dialog' : 'main').waitFor({ state: 'visible', timeout: 20000 }) } // an open panel hides the page behind it
  const finish = async () => { assert.deepEqual(errors, [], `browser errors: ${errors.join('; ')}`) }
  return { browser, page, mock, drafts, draftOf, wait, prompt, newTask, openNewTask, start, finish, created, comments }
}

async function runDrafts(base, on) {
  const ui = await draftPage(base, on)
  const { page, drafts, draftOf, wait, prompt, newTask, openNewTask, start } = ui
  try {
    await start()
    // AC-SAV-01: Escape in the middle of a new task, then open the same dialog again.
    await openNewTask()
    await newTask.locator('#t-name').fill('Drafted task')
    await newTask.locator('#t-desc').fill('Half-written description')
    await page.keyboard.press('Escape')
    await newTask.waitFor({ state: 'detached' })
    assert.equal(await prompt.count(), 0, `flag ${on}: a form with a draft closes without a question`)
    const kept = await drafts()
    if (!on) {
      assert.deepEqual(kept, [], 'flag off: nothing is written to local storage')
      await openNewTask()
      assert.equal(await newTask.locator('#t-name').inputValue(), '', 'flag off: the dialog opens empty')
      assert.equal(await newTask.getByText(/Draft restored/).count(), 0, 'flag off: no draft notice')
      await newTask.locator('#t-name').fill('Typed and abandoned')
      await page.keyboard.press('Escape')
      await newTask.waitFor({ state: 'detached' })
      assert.equal(await prompt.count(), 0, 'flag off: closing never asks')
      assert.equal(await page.evaluate(() => { const e = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(e); return e.defaultPrevented }), false, 'flag off: no leave-page prompt')
      assert.deepEqual(await drafts(), [])
      return { flag: on, drafts: 0 }
    }
    assert.equal(kept.length, 1, 'one draft for the new task')
    assert.match(kept[0], new RegExp(`^draft\\.${me.id}\\.task\\.new\\.${project.id}$`), 'keyed by user, form and context')
    assert.equal((await draftOf(kept[0])).value.f.name, 'Drafted task')
    await openNewTask()
    await newTask.getByText(/^Draft restored from \d{4}-\d{2}-\d{2} \d{2}:\d{2}/).waitFor({ state: 'visible' })
    await axeCheck(page, newTask.getByRole('status'), 'the draft notice')
    assert.equal(await newTask.locator('#t-name').inputValue(), 'Drafted task', 'the title is back')
    assert.equal(await newTask.locator('#t-desc').inputValue(), 'Half-written description', 'the description is back')
    assert.equal(await page.evaluate(() => { const e = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(e); return e.defaultPrevented }), false, 'a draft is already safe: no leave-page prompt')
    // Discard clears the draft and the form; an empty form writes no draft.
    await newTask.getByRole('button', { name: 'Discard', exact: true }).click()
    assert.equal(await newTask.locator('#t-name').inputValue(), '')
    assert.equal(await newTask.locator('#t-desc').inputValue(), '')
    assert.equal(await newTask.getByText(/Draft restored/).count(), 0)
    assert.deepEqual(await drafts(), [], 'Discard drops the draft')
    await newTask.locator('#t-name').fill('x')
    await wait(100)
    assert.equal((await drafts()).length, 1)
    await newTask.locator('#t-name').fill('')
    await wait(100)
    assert.deepEqual(await drafts(), [], 'typing and deleting again leaves no draft')
    await page.keyboard.press('Escape')
    await newTask.waitFor({ state: 'detached' })
    assert.equal(await prompt.count(), 0, 'a form with no change closes without a question')
    assert.deepEqual(await drafts(), [], 'an empty form writes no draft')
    // A successful submit drops the draft.
    await openNewTask()
    await newTask.locator('#t-name').fill('Task that is created')
    await wait(100)
    assert.equal((await drafts()).length, 1)
    await newTask.getByRole('button', { name: 'Create task' }).click()
    await newTask.waitFor({ state: 'detached' })
    assert.deepEqual(ui.created.map(x => x.name), ['Task that is created'])
    assert.deepEqual(await drafts(), [], 'the draft is gone once the task is created')
    await page.keyboard.press('Escape') // the new task's panel
    await openNewTask()
    assert.equal(await newTask.locator('#t-name').inputValue(), '', 'the next New task opens empty')
    // draft_keep_days: a draft older than 7 days is dropped, one inside it stays and restores.
    await newTask.locator('#t-name').fill('Week-old draft')
    await page.keyboard.press('Escape')
    await newTask.waitFor({ state: 'detached' })
    const [key] = await drafts()
    const age = (days) => page.evaluate(([k, ms]) => { const d = JSON.parse(localStorage.getItem(k)); d.at = new Date(Date.now() - ms).toISOString(); localStorage.setItem(k, JSON.stringify(d)) }, [key, days * DAY])
    await age(6)
    await start()
    assert.deepEqual(await drafts(), [key], 'a 6-day-old draft is kept')
    await openNewTask()
    assert.equal(await newTask.locator('#t-name').inputValue(), 'Week-old draft')
    await page.keyboard.press('Escape')
    await newTask.waitFor({ state: 'detached' })
    await age(8)
    await start()
    assert.deepEqual(await drafts(), [], 'an 8-day-old draft is pruned when the app opens')
    await age(8).catch(() => {})
    // A draft is also checked when its form opens, so one that expired between visits is not restored.
    await page.evaluate(([k, ms]) => localStorage.setItem(k, JSON.stringify({ at: new Date(Date.now() - ms).toISOString(), value: { f: { name: 'Expired' } } })), [key, 8 * DAY])
    await openNewTask()
    assert.equal(await newTask.locator('#t-name').inputValue(), '', 'an expired draft is not restored')
    assert.deepEqual(await drafts(), [], 'and it is removed')
    await page.keyboard.press('Escape')
    await newTask.waitFor({ state: 'detached' })
    // A comment draft: closing the panel keeps it, reopening restores it, posting drops it.
    await start(`/my-work?panel=Task:${task.id}`)
    const panel = page.getByRole('dialog')
    const box = panel.getByRole('textbox', { name: 'Comment', exact: true })
    await box.fill('Half-written comment')
    await wait(100)
    assert.deepEqual(await drafts(), [`draft.${me.id}.comment.Task.${task.id}`])
    await page.keyboard.press('Escape')
    await panel.waitFor({ state: 'detached' })
    assert.equal(await prompt.count(), 0, 'closing the panel with a comment draft does not ask')
    await start(`/my-work?panel=Task:${task.id}`)
    await panel.getByText(/^Draft restored from/).waitFor({ state: 'visible' })
    assert.equal(await box.inputValue(), 'Half-written comment')
    await panel.getByRole('button', { name: 'Post' }).click()
    await until(() => ui.comments.length === 1, 3000, 'the comment is posted')
    assert.deepEqual(ui.comments, [{ body: 'Half-written comment' }])
    for (let i = 0; i < 60 && (await drafts()).length; i++) await wait(50) // the box clears when the post is answered
    assert.deepEqual(await drafts(), [], 'posting drops the comment draft')
    ui.mock.unknownGets.length = 0
    await ui.finish()
    return { flag: on, drafts: 0 }
  } finally { await ui.browser.close() }
}

// Without a draft (the browser would not keep it) the leave prompt asks, so nothing typed goes without a choice.
async function runLeavePrompt(base) {
  const ui = await draftPage(base, true, { blocked: true })
  const { page, drafts, prompt, newTask, openNewTask, start, wait } = ui
  const unload = () => page.evaluate(() => { const e = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(e); return e.defaultPrevented })
  try {
    await start(`/projects/${project.projectNumber}/tasks`)
    await page.getByRole('link', { name: 'My Work', exact: true }).first().click()
    await page.waitForURL('**/my-work')
    await openNewTask()
    assert.equal(await unload(), false, 'nothing typed: no leave-page prompt')
    await newTask.locator('#t-name').fill('Not kept anywhere')
    await wait(100)
    assert.deepEqual(await drafts(), [], 'the browser kept no draft')
    assert.equal(await unload(), true, 'tab close and reload use the browser prompt for input no draft holds')
    // Closing asks; Keep editing keeps everything.
    await page.keyboard.press('Escape')
    await prompt.waitFor({ state: 'visible' })
    await axeCheck(page, prompt, 'the leave prompt')
    await prompt.getByRole('button', { name: 'Keep editing' }).click()
    await prompt.waitFor({ state: 'detached' })
    assert.equal(await newTask.locator('#t-name').inputValue(), 'Not kept anywhere')
    // Navigating away asks the same question (the Back button).
    await page.goBack()
    await prompt.waitFor({ state: 'visible' })
    await prompt.getByRole('button', { name: 'Keep editing' }).click()
    await prompt.waitFor({ state: 'detached' })
    assert.match(page.url(), /\/my-work$/, 'still on the page after Keep editing')
    assert.equal(await newTask.locator('#t-name').inputValue(), 'Not kept anywhere')
    await page.goBack()
    await prompt.waitFor({ state: 'visible' })
    await prompt.getByRole('button', { name: 'Discard', exact: true }).click()
    await page.waitForURL('**/tasks')
    assert.equal(await newTask.locator('#t-name').inputValue(), '', 'Discard lets the navigation go and empties the New dialog, which stays in the page frame')
    await page.keyboard.press('Escape')
    await newTask.waitFor({ state: 'detached' })
    assert.equal(await prompt.count(), 0, 'nothing is left to ask about')
    // Cancel with no draft asks too, and Discard closes.
    await page.getByRole('link', { name: 'My Work', exact: true }).first().click()
    await page.waitForURL('**/my-work')
    await openNewTask()
    await newTask.locator('#t-name').fill('Cancelled without a draft')
    await newTask.getByRole('button', { name: 'Cancel' }).click()
    await prompt.waitFor({ state: 'visible' })
    await prompt.getByRole('button', { name: 'Discard', exact: true }).click()
    await newTask.waitFor({ state: 'detached' })
    assert.equal(await unload(), false, 'nothing is left to protect')
    await ui.finish()
    return { prompts: 3 }
  } finally { await ui.browser.close() }
}

// FR-SAV-04 for forms that have no draft: an edit form (no key) and a create form open with the browser keeping nothing still ask
// before typed input goes. Flag off: today's behaviour, nothing asks. The New template dialog is a create form outside the project pages.
async function runEditPrompt(base, on) {
  const ui = await draftPage(base, on)
  const { page, drafts, prompt, start, wait } = ui
  const unload = () => page.evaluate(() => { const e = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(e); return e.defaultPrevented })
  const milestone = milestoneRow({ id: panelIds.milestone, projectId: project.id, key: 'M-001', name: 'Issue for review', date: '2026-11-02', originalDate: '2026-11-02' })
  await page.route(new RegExp(`/api/v1/projects/${project.id}/milestones(\\?.*)?$`), route => route.request().method() === 'GET' ? route.fulfill({ json: [milestone] }) : route.fallback())
  await page.route(/\/api\/v1\/templates$/, route => route.request().method() === 'GET' ? route.fulfill({ json: templateList([]) }) : route.fallback())
  try {
    await start(`/projects/${project.projectNumber}/milestones`)
    const edit = page.getByRole('dialog', { name: 'Edit M-001' })
    const openEdit = async () => {
      await page.getByRole('button', { name: 'Actions for M-001' }).click()
      await page.getByRole('menuitem', { name: 'Edit', exact: true }).click()
      await edit.locator('#m-name').waitFor({ state: 'visible' })
    }
    await openEdit()
    await page.keyboard.press('Escape')
    await edit.waitFor({ state: 'detached' })
    assert.equal(await prompt.count(), 0, `flag ${on}: an edit form with no change closes without a question`)
    await openEdit()
    await edit.locator('#m-name').fill('Changed but not saved')
    await wait(100)
    assert.deepEqual(await drafts(), [], `flag ${on}: an edit form keeps no draft`)
    assert.equal(await unload(), on, `flag ${on}: tab close and reload ${on ? 'use' : 'skip'} the browser prompt for a changed edit form`)
    if (!on) {
      await page.keyboard.press('Escape')
      await edit.waitFor({ state: 'detached' })
      assert.equal(await prompt.count(), 0, 'flag off: closing never asks')
    } else {
      for (const close of [() => page.keyboard.press('Escape'), () => edit.getByRole('button', { name: 'Cancel' }).click(), () => page.mouse.click(5, 5)]) {
        await close()
        await prompt.waitFor({ state: 'visible' })
        await prompt.getByRole('button', { name: 'Keep editing' }).click()
        await prompt.waitFor({ state: 'detached' })
        assert.equal(await edit.locator('#m-name').inputValue(), 'Changed but not saved', 'Keep editing keeps the change')
      }
      await edit.getByRole('button', { name: 'Cancel' }).click()
      await prompt.getByRole('button', { name: 'Discard', exact: true }).click()
      await edit.waitFor({ state: 'detached' })
      assert.equal(await unload(), false, 'nothing is left to protect')
      await openEdit()
      assert.equal(await edit.locator('#m-name').inputValue(), 'Issue for review', 'the record, not the abandoned change, opens again')
      await page.keyboard.press('Escape')
      await edit.waitFor({ state: 'detached' })
    }
    // A create dialog on a page of its own: the draft is kept and restored, and a form with a draft closes without a question.
    await start('/templates')
    const create = page.getByRole('dialog', { name: 'New template' })
    const openCreate = async () => { await page.getByRole('button', { name: 'New template' }).first().click(); await create.locator('#tpl-name').waitFor({ state: 'visible' }) }
    await openCreate()
    await create.locator('#tpl-name').fill('Half-named template')
    await wait(100)
    await page.keyboard.press('Escape')
    await create.waitFor({ state: 'detached' })
    assert.equal(await prompt.count(), 0, 'a create form with a draft closes without a question')
    if (!on) assert.deepEqual(await drafts(), [], 'flag off: nothing is written')
    else {
      assert.deepEqual(await drafts(), [`draft.${me.id}.template.new`])
      await openCreate()
      await create.getByText(/^Draft restored from/).waitFor({ state: 'visible' })
      assert.equal(await create.locator('#tpl-name').inputValue(), 'Half-named template')
      await create.getByRole('button', { name: 'Discard', exact: true }).click()
      assert.deepEqual(await drafts(), [], 'Discard drops the draft')
    }
    ui.mock.unknownGets.length = 0
    await ui.finish()
    return { flag: on }
  } finally { await ui.browser.close() }
}

// AC-SAV-05 (FR-SAV-06): the build-update notice stays a notice; its Reload goes through the leave guard. Needs a build that knows its
// own id (VITE_HUB_BUILD), so this suite builds one into a scratch folder and serves it.
async function runReload(on, blocked) {
  const out = path.join(os.tmpdir(), `hub-data-entry-${process.pid}`)
  execFileSync(process.execPath, ['node_modules/vite/bin/vite.js', 'build', '--outDir', out, '--emptyOutDir', '--logLevel', 'error'],
    { cwd: path.resolve(__dirname, '..'), env: { ...process.env, VITE_HUB_BUILD: NOTICE_BUILD }, stdio: 'inherit' })
  const preview = await startPreview(port + 1, { outDir: out })
  const ui = await draftPage(preview.base, on, { blocked })
  const { page, drafts, prompt, newTask, openNewTask, start, wait } = ui
  try {
    await start()
    const reload = page.getByRole('button', { name: 'Reload', exact: true })
    await reload.waitFor({ state: 'visible', timeout: 10000 })
    assert.equal(await reload.count(), 1, 'the notice is a notice: the page did not reload by itself')
    await page.evaluate(() => { window.sameLoad = true })
    await openNewTask()
    await newTask.locator('#t-name').fill('Typed before the update')
    await wait(100)
    const same = () => page.evaluate(() => window.sameLoad === true).catch(() => false)
    if (!on) {
      await page.keyboard.press('Escape') // flag off: the notice sits behind an open dialog as it always did
      await newTask.waitFor({ state: 'detached' })
      await reload.click()
      await page.waitForFunction(() => window.sameLoad !== true)
      assert.equal(await prompt.count(), 0, 'flag off: Reload asks nothing')
    } else if (blocked) {
      await reload.click()
      await prompt.waitFor({ state: 'visible' })
      await prompt.getByRole('button', { name: 'Keep editing' }).click()
      await wait(300)
      assert.equal(await same(), true, 'Keep editing: the page did not reload')
      assert.equal(await newTask.locator('#t-name').inputValue(), 'Typed before the update')
      await reload.click()
      await prompt.getByRole('button', { name: 'Discard', exact: true }).click()
      await page.waitForFunction(() => window.sameLoad !== true)
    } else {
      assert.equal((await drafts()).length, 1)
      await reload.click()
      await page.waitForFunction(() => window.sameLoad !== true)
      assert.equal(await prompt.count(), 0, 'a draft protects the input: Reload goes straight through')
      await page.getByRole('main').waitFor({ state: 'visible', timeout: 20000 })
      assert.equal((await drafts()).length, 1, 'the draft survives the reload')
      await openNewTask()
      await newTask.getByText(/^Draft restored from/).waitFor({ state: 'visible' })
      assert.equal(await newTask.locator('#t-name').inputValue(), 'Typed before the update', 'after reload the draft is offered')
    }
    await ui.finish()
    return { flag: on, blocked }
  } finally { await ui.browser.close(); preview.child.kill(); fs.rmSync(out, { recursive: true, force: true }) }
}

// ---- S04: sign-in expiry ----

// FR-SAV-05: a write is refused with a 401 while the person is working. The sign-in flow runs (a real picker, a real reload to the same
// route), and afterwards the input is where it was: a comment draft is in the editor, an edit-panel field saves its value once.
async function runSignInExpiry(base, on) {
  const ui = await draftPage(base, on)
  const { page, drafts, wait, start } = ui
  let expired = false
  const deliverable = structuredClone({ ...deliverableDetail, permissions: { ...deliverableDetail.permissions, edit: { ok: true } } })
  const saves = []
  await page.route('**/api/dev/users', route => route.fulfill({ json: [{ email: me.email, displayName: me.displayName, jobTitle: 'Engineer', roles: [] }] }))
  await page.route(`**/api/v1/items/Task/${task.id}/comments`, route => route.request().method() === 'POST' && expired ? route.fulfill({ status: 401, json: {} }) : route.fallback())
  await page.route(new RegExp(`/api/v1/deliverables/${panelIds.deliverable}$`), route => {
    const request = route.request()
    if (request.method() === 'GET') return route.fulfill({ json: structuredClone(deliverable) })
    if (request.method() !== 'PATCH') return route.fallback()
    if (expired) return route.fulfill({ status: 401, json: {} })
    const body = request.postDataJSON()
    saves.push({ body, ifMatch: Number(request.headers()['if-match']?.replaceAll('"', '')) })
    if ('description' in body) deliverable.description = body.description
    deliverable.deliverable.rowVersion++
    return route.fulfill({ json: { ...deliverable.deliverable, warnings: [] } })
  })
  const pending = () => page.evaluate(() => JSON.parse(sessionStorage.getItem('hub.pendingFields') ?? 'null'))
  const panel = page.getByRole('dialog')
  // The sign-in picker comes up on the login route; choosing the person goes back to the route the work was on.
  const signIn = async (back, whileSignedOut = async () => {}) => {
    const person = page.getByRole('button', { name: new RegExp(me.displayName) })
    await person.waitFor({ state: 'visible', timeout: 10000 })
    assert.equal(new URL(page.url()).pathname, '/login', 'sign-in runs')
    await whileSignedOut()
    expired = false
    await person.click()
    await page.waitForURL(url => url.pathname + url.search === back, { timeout: 10000 })
    await panel.waitFor({ state: 'visible', timeout: 20000 })
  }
  try {
    // AC-SAV-04: a half-written comment and an expired session.
    const item = `/my-work?panel=Task:${task.id}`
    await start(item)
    const box = panel.getByRole('textbox', { name: 'Comment', exact: true })
    await box.fill('Half-written comment')
    await wait(100)
    expired = true
    await panel.getByRole('button', { name: 'Post' }).click()
    assert.deepEqual(ui.comments, [], 'the refused post reached nobody')
    await signIn(item)
    assert.equal(new URL(page.url()).search, `?panel=Task:${task.id}`, `flag ${on}: the same item is open`)
    if (!on) {
      assert.equal(await box.inputValue(), '', 'flag off: nothing is kept, as before')
      assert.equal(await panel.getByText(/Draft restored/).count(), 0)
      assert.deepEqual(await drafts(), [])
    } else {
      await panel.getByText(/^Draft restored from/).waitFor({ state: 'visible' })
      assert.equal(await box.inputValue(), 'Half-written comment', 'the comment is still in the editor')
      await panel.getByRole('button', { name: 'Post' }).click()
      await until(() => ui.comments.length === 1, 3000, 'posting succeeds')
      assert.deepEqual(ui.comments, [{ body: 'Half-written comment' }])
      for (let i = 0; i < 60 && (await drafts()).length; i++) await wait(50)
      assert.deepEqual(await drafts(), [], 'posting drops the draft')
    }
    assert.equal(await pending(), null, 'a comment is kept as a draft, not as a pending field')

    // An edit-panel field: the save is refused, the typed value waits through sign-in and is saved once on the fresh row version.
    const record = `/my-work?panel=Deliverable:${panelIds.deliverable}`
    await start(record)
    await panel.getByRole('button', { name: 'Synthetic deliverable detail', exact: true }).first().click()
    const field = panel.getByRole('textbox', { name: 'Description', exact: true })
    expired = true
    await field.fill('Typed before the session expired')
    await field.press('Tab')
    await signIn(record, async () => {
      const kept = await pending(), pendingKey = `/my-work?Deliverable:${panelIds.deliverable}`
      if (!on) assert.equal(kept, null, 'flag off: nothing is stored')
      else {
        assert.equal(kept.length, 1)
        assert.deepEqual({ ...kept[0], at: undefined }, { user: me.id, route: pendingKey, field: 'Description', value: 'Typed before the session expired', at: undefined }, 'the typed value waits, keyed by person, record and field')
      }
    })
    if (!on) {
      await panel.getByRole('button', { name: 'Synthetic deliverable detail', exact: true }).first().waitFor({ state: 'visible' })
      await wait(1500)
      assert.deepEqual(saves, [], 'flag off: nothing is saved after sign-in')
    } else {
      await panel.getByRole('button', { name: 'Typed before the session expired', exact: true }).first().waitFor({ state: 'visible' })
      await panel.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
      await wait(1500)
      assert.deepEqual(saves, [{ body: { description: 'Typed before the session expired' }, ifMatch: 1 }], 'one retry, one save')
      assert.equal(deliverable.description, 'Typed before the session expired')
      assert.equal(await pending(), null, 'the retry is spent')
      assert.equal(await panel.getByText(/Not saved/).count(), 0)
    }
    ui.mock.unknownGets.length = 0
    await ui.finish()
    return { flag: on, saves: saves.length }
  } finally { await ui.browser.close() }
}

;(async () => {
  const preview = await startPreview(port)
  let results, drafts
  try {
    results = [await run(preview.base, false), await run(preview.base, true)]
    drafts = [await runDrafts(preview.base, false), await runDrafts(preview.base, true), await runLeavePrompt(preview.base), await runEditPrompt(preview.base, false), await runEditPrompt(preview.base, true), await runSignInExpiry(preview.base, false), await runSignInExpiry(preview.base, true)]
  } finally { await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() }) }
  const reloads = [await runReload(false, false), await runReload(true, false), await runReload(true, true)]
  console.log(JSON.stringify({ result: 'PASS', results, drafts, reloads }))
})().catch(error => { console.error(error); process.exitCode = 1 })
