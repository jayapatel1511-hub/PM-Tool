// Packet 040 AC-PLAT-06 and AC-PLAT-07 real-API check. Each view is saved through the Views menu and reopened from a fresh
// page load against the local API: onHold must reach the address and the list request, and groupBy must decide the
// Workload and Weekly Planner groups. Uses the Development picker and synthetic records only.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
assert.equal(process.env.TUESDAY_SYNTHETIC_API, '1', 'this check saves views, so it requires an explicitly disposable database')

async function api(pathname, who, options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json' } })
  if (!response.ok) throw new Error(`${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.json()
}

async function seed() {
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  const marc = await api('me', 'marc')
  const project = await api('projects', 'priya', { method: 'POST', body: JSON.stringify({
    projectNumber: `P${Math.random().toString(36).slice(2, 9).toUpperCase()}`, name: 'Synthetic 040 saved views',
    clientId: reference.clients[0].id, officeId: reference.offices[0].id, disciplines: [{ disciplineId: civil.id, leadUserId: marc.id }], members: [],
  }) })
  const draft = await api(`projects/${project.id}`, 'priya')
  await api(`projects/${project.id}/transition`, 'priya', { method: 'POST', body: JSON.stringify({ toStatus: 'Active', rowVersion: draft.rowVersion }) })
  return { project, offices: reference.offices.map(o => o.name) }
}

async function until(read, ok, what) {
  const deadline = Date.now() + 15000
  let value
  while (Date.now() < deadline) { value = await read(); if (ok(value)) return value; await new Promise(r => setTimeout(r, 200)) }
  assert.fail(`${what}: ${JSON.stringify(value)}`)
}

;(async () => {
  const { project, offices } = await seed()
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const results = []
  try {
    const session = async (name) => {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
      const page = await context.newPage()
      page.errors = []
      page.on('pageerror', e => page.errors.push(e.message))
      await page.goto(`${base}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
      await page.getByRole('button', { name: new RegExp(name, 'i') }).click()
      await page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 30000 })
      return page
    }
    // A full page load each time, so no list response is served from the client cache.
    const open = async (page, url) => { await page.goto(`${base}${url}`, { waitUntil: 'networkidle', timeout: 60000 }); await page.getByRole('main').waitFor() }
    const views = page => page.getByRole('button', { name: 'Views', exact: true })
    const save = async (page, url, name) => {
      await open(page, url)
      await views(page).click()
      await page.getByRole('menuitem', { name: 'Save current view…' }).click()
      const dialog = page.getByRole('dialog')
      await dialog.getByLabel('Name').fill(name)
      await dialog.getByRole('button', { name: 'Save', exact: true }).click()
      await dialog.waitFor({ state: 'detached' })
      await page.waitForURL(u => u.searchParams.has('view'))
    }
    const reopen = async (page, url, name, listRequest) => {
      await open(page, url)
      const request = listRequest && page.waitForRequest(listRequest, { timeout: 15000 })
      await views(page).click()
      await page.getByRole('menuitem', { name, exact: true }).click()
      await page.waitForURL(u => u.searchParams.has('view'))
      return request && await request
    }

    // AC-PLAT-06: onHold=false survives save and reopen on all four task lists.
    const pm = await session('Priya Nair')
    const lists = [
      ['tasks', `/projects/${project.projectNumber}/tasks`, `/api/v1/projects/${project.id}/tasks`],
      ['board', `/projects/${project.projectNumber}/board`, `/api/v1/projects/${project.id}/tasks`],
      ['workspace-tasks', '/tasks', '/api/v1/tasks'],
      ['workspace-board', '/boards', '/api/v1/tasks'],
    ]
    for (const [list, path, endpoint] of lists) {
      const name = `Synthetic not on hold ${list} ${project.projectNumber}`
      await save(pm, `${path}?onHold=false`, name)
      const request = await reopen(pm, path, name, r => new URL(r.url()).pathname === endpoint && new URL(r.url()).searchParams.get('onHold') === 'false')
      assert.equal(new URL(pm.url()).searchParams.get('onHold'), 'false', `${list}: address`)
      const sent = new URL(request.url())
      results.push({ list, address: new URL(pm.url()).search, request: sent.pathname + sent.search })
      console.log(JSON.stringify(results.at(-1)))
    }
    assert.deepEqual(pm.errors, [], 'task list page errors')

    // AC-PLAT-07: Workload and Weekly Planner views reopen grouped as saved; without groupBy they group by Reports to.
    const lead = await session('Lena')
    const reportsTo = /^(Reports to .+|No supervisor) · \d+$/
    const byOffice = h => offices.some(o => h.startsWith(`${o} · `)) || /^Not set · \d+$/.test(h)
    for (const path of ['/workload', '/planner']) {
      const groups = () => lead.locator('th[scope="rowgroup"]').allTextContents().then(x => x.map(h => h.trim()))
      const [office, plain] = [`Synthetic by office ${path} ${project.projectNumber}`, `Synthetic default grouping ${path} ${project.projectNumber}`]
      await save(lead, `${path}?groupBy=office`, office)
      await save(lead, path, plain)
      await reopen(lead, path, office)
      assert.equal(new URL(lead.url()).searchParams.get('groupBy'), 'office', `${path}: address`)
      const officeGroups = await until(groups, x => x.length > 0 && x.every(byOffice), `${path}: office groups`)
      await reopen(lead, `${path}?groupBy=office`, plain)
      assert.equal(new URL(lead.url()).searchParams.has('groupBy'), false, `${path}: default address`)
      const supervisorGroups = await until(groups, x => x.length > 0 && x.every(h => reportsTo.test(h)), `${path}: Reports to groups`)
      results.push({ path, officeGroups, supervisorGroups })
      console.log(JSON.stringify(results.at(-1)))
    }
    assert.deepEqual(lead.errors, [], 'workload and planner page errors')
    console.log(JSON.stringify({ result: 'PASS', project: project.projectNumber, results }))
  } finally {
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
