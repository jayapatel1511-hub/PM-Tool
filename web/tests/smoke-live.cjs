// Packet 040 S13 real-API smoke. Uses the Development picker and synthetic records only.
// No route mocks: the browser talks to the local API through the running preview.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:19502'
const apiBase = process.env.TUESDAY_API_BASE_URL ?? 'http://127.0.0.1:18502'
for (const url of [base, apiBase]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), `${url} must be local`)
const headers = who => ({ 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json' })
async function api(pathname, who, options = {}) {
  const response = await fetch(`${apiBase}/api/v1/${pathname}`, { ...options, headers: { ...headers(who), ...(options.headers ?? {}) } })
  if (!response.ok) throw new Error(`${pathname}: HTTP ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}

async function seed() {
  const reference = await api('reference', 'priya')
  const civil = reference.disciplines.find(x => x.name === 'Civil')
  const marc = await api('me', 'marc')
  const body = {
    projectNumber: `P${Math.random().toString(36).slice(2, 9).toUpperCase()}`, name: 'Synthetic 040 live smoke',
    clientId: reference.clients[0].id, officeId: reference.offices[0].id,
    disciplines: [{ disciplineId: civil.id, leadUserId: marc.id }], members: [],
  }
  const project = await api('projects', 'priya', { method: 'POST', body: JSON.stringify(body) })
  const draft = await api(`projects/${project.id}`, 'priya')
  await api(`projects/${project.id}/transition`, 'priya', { method: 'POST', body: JSON.stringify({ toStatus: 'Active', rowVersion: draft.rowVersion }) })
  const detail = await api(`projects/${project.id}`, 'priya')
  const discipline = detail.disciplines?.[0]?.id ?? detail.projectDisciplines?.[0]?.id
  const role = await api(`projects/${project.id}/open-roles`, 'priya', { method: 'POST', body: JSON.stringify({
    roleLabel: 'Synthetic 040 unfilled role', fromDate: '2026-10-19', throughDate: '2026-10-23', plannedHours: 20, projectDisciplineId: discipline,
  }) })
  return { number: project.projectNumber, id: project.id, role: role.id, label: role.roleLabel }
}

async function axe(page) {
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  return page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations
    .filter(x => ['serious', 'critical'].includes(x.impact)).map(x => x.id))
}

;(async () => {
  assert.equal((await (await fetch(`${apiBase}/api/v1/config`)).json()).authMode, 'Development')
  const ids = await seed()
  const browser = await chromium.launch({ headless: true })
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', e => errors.push(e.message))
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()) })
  try {
    await page.goto(`${base}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 })
    await page.getByRole('button', { name: /Priya Nair/i }).click()
    await page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 30000 })
    const checks = []
    const visit = async (url, ready) => {
      errors.length = 0
      await page.goto(`${base}${url}`, { waitUntil: 'domcontentloaded', timeout: 60000 })
      await page.locator('main').first().waitFor({ timeout: 30000 })
      if (ready) await page.getByText(ready, { exact: false }).first().waitFor({ timeout: 30000 })
      await page.waitForTimeout(700)
      const violations = await axe(page)
      const row = { url, final: new URL(page.url()).pathname + new URL(page.url()).search, violations, errors: [...errors] }
      assert.deepEqual(violations, [], `${url}: serious/critical axe violations`)
      assert.deepEqual(row.errors, [], `${url}: browser errors`)
      checks.push(row)
      return row
    }

    const allocations = await visit(`/projects/${ids.number}/allocations?tab=roles`, ids.label)
    assert.ok(await page.getByText(ids.label, { exact: true }).count(), 'open role is visible in Allocations')
    assert.equal(new URL(page.url()).searchParams.get('tab'), 'roles')
    const workload = await visit('/workload', 'Unfilled roles')
    assert.ok(await page.getByText(ids.label, { exact: true }).count(), 'open role is visible in Workload Unfilled roles')
    assert.ok(workload.final.startsWith('/workload'))

    const legacyAllocation = await visit(`/projects/${ids.number}/allocations?view=roles`, ids.label)
    assert.equal(new URL(page.url()).searchParams.get('tab'), 'roles')
    assert.equal(new URL(page.url()).searchParams.has('view'), false)
    assert.equal(legacyAllocation.final.includes('view='), false)
    const legacyPlanner = await visit('/planner?view=list', 'Planner')
    assert.equal(new URL(page.url()).searchParams.get('layout'), 'list')
    assert.equal(new URL(page.url()).searchParams.has('view'), false)
    assert.equal(legacyPlanner.final.includes('view='), false)
    const legacyDecisions = await visit(`/projects/${ids.number}/decisions?view=log`, 'Decisions')
    assert.equal(new URL(page.url()).searchParams.get('tab'), 'log')
    assert.equal(new URL(page.url()).searchParams.has('view'), false)
    assert.equal(legacyDecisions.final.includes('view='), false)
    console.log(JSON.stringify({ result: 'PASS', project: ids.number, role: ids.label, checks }))
  } finally {
    await context.close(); await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
