// S08: destination URL scope survives list/board switches, names every active filter, and clears explicitly.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task } = require('./platform-fixtures.cjs')
const { taskPage } = require('./tasks.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const disciplines = `${project.disciplines[0].id},${id(991)}`
// A deliberately long CSV survives unchanged; scalar presets themselves carry no expanded cohort at all.
const people = Array.from({ length: 225 }, (_, n) => id(1000 + n)).join(',')
;(async () => {
  const saved = { ...me.flags }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const requests = [], errors = []; page.on('pageerror', error => errors.push(error.message))
  try {
    me.flags.home_overview = true
    me.flags.live_events = false
    await page.route('**/api/v1/tasks?*', route => { requests.push(new URL(route.request().url())); return route.fulfill({ json: taskPage([task]) }) })
    const query = new URLSearchParams({ homePreset: 'DisciplineLead', disciplineId: disciplines, assigneeId: people, open: 'true', sort: 'dueDate', keep: 'origin-value' })
    const start = `${base}/tasks?${query}`
    await page.goto(start, { waitUntil: 'domcontentloaded' })
    const filters = () => page.getByRole('group', { name: 'Active filters', exact: true })
    await filters().getByText('Discipline Lead', { exact: true }).waitFor()
    await filters().getByText('225 selected', { exact: true }).waitFor()
    await filters().getByText('2 selected', { exact: true }).waitFor()
    const sent = requests.at(-1)
    assert.equal(sent.searchParams.get('homePreset'), 'DisciplineLead')
    assert.equal(sent.searchParams.get('disciplineId'), disciplines)
    assert.equal(sent.searchParams.get('assigneeId'), people, 'no client cohort truncation')
    await page.getByRole('group', { name: 'View', exact: true }).getByRole('link', { name: 'Board', exact: true }).click()
    await page.waitForURL(url => url.pathname === '/boards')
    const board = new URL(page.url())
    assert.equal(board.searchParams.get('homePreset'), 'DisciplineLead')
    assert.equal(board.searchParams.get('disciplineId'), disciplines)
    assert.equal(board.searchParams.get('assigneeId'), people)
    await filters().getByRole('button', { name: /Remove filter: Home preset/ }).click()
    assert.equal(new URL(page.url()).searchParams.has('homePreset'), false)
    assert.equal(new URL(page.url()).searchParams.get('assigneeId'), people)
    await filters().getByRole('button', { name: 'Clear filters', exact: true }).click()
    const cleared = new URL(page.url())
    for (const key of ['homePreset', 'disciplineId', 'assigneeId', 'open']) assert.equal(cleared.searchParams.has(key), false)
    assert.equal(cleared.searchParams.get('keep'), 'origin-value', 'clearing task filters preserves unrelated URL state')
    me.flags.home_overview = false; requests.length = 0
    const response = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/tasks')
    await page.goto(start)
    await page.getByRole('heading', { name: /Tasks/ }).first().waitFor()
    await response
    const legacy = requests.at(-1)
    assert.equal(legacy.searchParams.has('homePreset'), false)
    assert.equal(legacy.searchParams.has('disciplineId'), false)
    assert.equal(await filters().getByText('Discipline Lead', { exact: true }).count(), 0)
    assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: exact scalar preset/CSV filter requests; named filters; board switch; explicit clear; unchanged-off task parameters')
  } finally { me.flags = saved; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
