// Packet 040 S13 real-API smoke. Uses the Development picker and synthetic records only.
// No route mocks: the browser talks to the local API through the running preview.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const fs = require('node:fs')
const path = require('node:path')

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
  return { number: project.projectNumber, id: project.id, discipline, role: role.id, label: role.roleLabel }
}

async function axe(page) {
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  return page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations
    .filter(x => ['serious', 'critical'].includes(x.impact)).map(x => x.id))
}

;(async () => {
  assert.equal((await (await fetch(`${apiBase}/api/v1/config`)).json()).authMode, 'Development')
  const ids = await seed()
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? {executablePath:process.env.CHROME_EXECUTABLE_PATH} : {}) })
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
    // Exercise every concrete route against the owned synthetic API, including flag-gated admin pages.
    assert.equal(process.env.TUESDAY_SYNTHETIC_API,'1','the route sweep requires an explicitly disposable database')
    const originalSettings=await api('admin/settings','jordan')
    const routeFlags=['flag.task_pickup','flag.org_structure','flag.verifiable_log']
    const saved=originalSettings.filter(x=>routeFlags.includes(x.key))
    assert.equal(saved.length,routeFlags.length)
    const adminContext=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'})
    const adminPage=await adminContext.newPage()
    const routeChecks=[]
    try {
      for(const key of routeFlags) await api(`admin/settings/${key}`,'jordan',{method:'PUT',body:JSON.stringify({value:true})})
      const task=await api(`projects/${ids.id}/tasks`,'priya',{method:'POST',body:JSON.stringify({name:'Synthetic route task',projectDisciplineId:ids.discipline})})
      const template=await api('templates','jordan',{method:'POST',body:JSON.stringify({name:`Synthetic route template ${Date.now()}`})})
      const alex=await api('me','alex')
      const reports=await api('reports','jordan')
      assert.ok(reports.length)
      const projectTabs=['dashboard','coordination','readiness','handoffs','reviews','changes','submissions','allocations','design-basis','tasks','board','milestones','timeline','deliverables','decisions','risks','issues','meetings','files','vacation','calendar','team','activity','settings']
      const routes=['/pickup','/organisation','/admin/structure','/admin/audit-chains','/my-work','/home','/boards','/tasks','/timeline','/files','/team','/projects',
        ...projectTabs.map(tab=>`/projects/${ids.number}/${tab}`),`/tasks/${task.id}`,'/notifications','/search','/reports',`/reports/${reports[0].code}`,
        '/staff','/portfolio','/workload','/planner','/vacation','/calendar','/time',`/people/${alex.id}/reassign-work`,'/preferences','/templates',`/templates/${template.id}`,
        '/admin','/admin/reference/disciplines','/admin/settings','/admin/users','/admin/activity','/admin/operations','/admin/holidays']
      const routeSource=fs.readFileSync(path.resolve(__dirname,'../src/app/routes.tsx'),'utf8')
      const declared=[...routeSource.matchAll(/path: '([^']+)'/g)].map(x=>x[1]).filter(x=>x!=='*'&&x!=='projects/:number')
      const concrete=declared.map(x=>x.replace(':number',ids.number).replace(':code',reports[0].code).replace(':kind','disciplines').replace(':id',x.startsWith('templates/')?template.id:x.startsWith('people/')?alex.id:task.id))
      for(const route of concrete) assert.ok(routes.includes(`/${route}`)||projectTabs.includes(route),`route manifest missing ${route}`)
      await adminPage.goto(`${base}/login`)
      await adminPage.getByRole('button',{name:/Jordan Lee/i}).click()
      await adminPage.waitForURL(url=>!url.pathname.startsWith('/login'))
      await adminPage.waitForLoadState('networkidle')
      let routeErrors=[], failures=[], expectedResponses=[], generation=0
      const requestGeneration=new WeakMap()
      adminPage.on('request',request=>requestGeneration.set(request,generation))
      adminPage.on('requestfailed',request=>{
        const pathname=new URL(request.url()).pathname
        if(requestGeneration.get(request)===generation&&(pathname.includes('/api/')||pathname.includes('/assets/'))) failures.push(`${request.failure()?.errorText} ${pathname}`)
      })
      adminPage.on('pageerror',error=>routeErrors.push(error.message))
      adminPage.on('response',response=>{
        const pathname=new URL(response.url()).pathname
        // A task with no pickup offer intentionally returns404; the panel treats that as the empty state.
        if(response.status()===404&&pathname===`/api/v1/tasks/${task.id}/pickup-offer`) expectedResponses.push(`404 ${pathname}`)
        else if((pathname.includes('/api/')||pathname.includes('/assets/'))&&response.status()>=400) failures.push(`${response.status()} ${pathname}`)
      })
      for(const route of routes) {
        generation++; routeErrors=[]; failures=[]; expectedResponses=[]
        await adminPage.goto(`${base}${route}`,{waitUntil:'networkidle',timeout:60000})
        const main=adminPage.getByRole('main')
        await main.waitFor()
        const heading=main.locator(route.startsWith('/projects/'+ids.number+'/')||route.startsWith('/admin')?'h2':'h1').first()
        await heading.waitFor()
        const title=(await heading.innerText()).trim()
        assert.ok(title,`${route}: loaded route heading`)
        const body=await main.innerText()
        assert.doesNotMatch(body,/Page not found|Something went wrong|Unable to load|Unexpected error/i,`${route}: error state`)
        assert.deepEqual(await main.locator('[role="alert"].bg-bad-bg').allTextContents(),[],`${route}: rendered error banner`)
        assert.equal(new URL(adminPage.url()).pathname,route==='/admin'?'/admin/reference/disciplines':route,`${route}: route must not silently redirect`)
        assert.deepEqual(routeErrors,[],`${route}: page errors`);assert.deepEqual(failures,[],`${route}: failed API/chunk responses`)
        const violations=await axe(adminPage)
        assert.deepEqual(violations,[],`${route}: serious/critical axe violations`)
        routeChecks.push({route,title,violations,errors:[...routeErrors],failedResponses:[...failures],expectedResponses:[...expectedResponses]})
        console.log(JSON.stringify({route,title,result:'PASS'}))
      }
    } finally {
      await adminContext.close()
      for(const setting of saved) await api(`admin/settings/${setting.key}`,'jordan',{method:'PUT',body:JSON.stringify({value:setting.value})})
    }
    console.log(JSON.stringify({ result: 'PASS', project: ids.number, role: ids.label, checks, routeChecks }))
  } finally {
    await context.close(); await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
