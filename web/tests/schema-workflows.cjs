// Browser evidence for packets 048-050. The UI talks to a mocked API, but all
// assertions below are against rendered workflows and captured request bodies.
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const { spawn } = require('node:child_process')
const fs = require('node:fs')
const path = require('node:path')
const assert = require('node:assert/strict')
const { commitmentRow, commitmentList, commitmentDetail } = require('./weekly-commitments.fixture.cjs')
const { coordinationOptions } = require('./reviews.fixture.cjs')

const port = process.env.PORT || '19505'
const base = `http://127.0.0.1:${port}`
const out = path.resolve(__dirname, '../test-results/schema-workflows')
fs.mkdirSync(out, { recursive: true })
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const projectId = id(10), levelId = id(20), unitId = id(21), taskId = id(30), offerId = id(31), chainId = id(40), personId = id(50)
const project = { id: projectId, projectNumber: 'P-DEMO', name: 'Synthetic workflow project', client: 'Pilot client', pmName: 'Pat PM', status: 'Active', visibility: 'Open', rowVersion: 1, links: [], disciplines: [], starred: false, myRoles: [], health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] }, permissions: { edit: { ok: false }, healthOverride: { ok: false }, reviewDates: { ok: false }, snooze: { ok: false }, captureWeeklyPlan: { ok: false }, raiseRegister: { ok: false }, isPm: false, leadOf: [], createTaskIn: [], createDeliverableIn: [], transitions: [], needsReason: false } }
const me = { id: id(1), displayName: 'Sam Supervisor', email: 'sam@hub.test', isTemplateEditor: false, roles: [], systemRoles: ['Admin'], capabilities: { createProject: true, createTask: true, portfolio: true, workload: true, staff: true, allStaff: true, admin: true, templates: true, readOnly: false, directReports: 2 }, settings: { today: '2026-10-03', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8, restrictedProjectsEnabled: false, allowSelfReview: false, taskDueSoonDays: 7, milestoneApproachingDays: 14, chainDepthLimit: 100, defaultWeeklyCapacityHours: 40, coordinationLookaheadWeeks: 6, workingDaysEnabled: true }, preferences: { digestEnabled: true, denseRows: true, weeklySummaryEnabled: true }, flags: { task_pickup: false, org_structure: false, verifiable_log: false, commitment_reliability: false } }

const errors = [], unknownGets = [], calls = [], axeReports = [], shots = []
let failImport = false, featurePhase = 'off', totalApiCalls = 0
let server, browser
const levels = [{ id: levelId, name: 'Department', sortOrder: 1, rowVersion: 0 }]
const units = [{ id: unitId, name: 'Design', parentId: null, levelId, leaderId: personId, disciplineId: null, officeId: null, headCount: 1, rowVersion: 0 }]
const people = [{ id: personId, displayName: 'Alex Engineer', email: 'alex@hub.test', unitId, supervisorId: null }]
const offer = { id: offerId, taskId, offeredById: id(2), offeredBy: 'Pat PM', reason: 'Needs a civil lead', hoursRemaining: 12, neededBy: '2026-10-15', note: 'Synthetic offer', openedAt: '2026-10-03T12:00:00Z', task: { key: 'T-101', name: 'Review drainage memo', projectId, projectDisciplineId: id(60), disciplineName: 'Civil', dueDate: '2026-10-20', rowVersion: 7 }, rowVersion: 7, canPickUp: true, canAssign: false, allowed: { pickUp: true, assign: false } }
const assignOffer = { ...offer, id: id(171), taskId: id(172), task: { ...offer.task, key: 'T-102', name: 'Check culvert sizing', projectNumber: '100001' }, canPickUp: false, canAssign: true, allowed: { pickUp: false, assign: true } }
const chain = { chainId, headSeq: 3, headHash: 'sha256-head', lastVerifiedAt: '2026-10-03T12:00:00Z', firstBrokenSeq: null }
const confirmId = id(73)
const reliability = { snapshots: [{ id: id(70), weekStart: '2026-09-28', reliability: { met: 78, committed: 100, percent: 78, band: 'Watch', withdrawn: 2, notYetRecorded: 20, awaiting: 1 } }], misses: [{ categoryId: id(71), category: 'Blocked by dependency', count: 2, commitments: [id(72)] }], awaiting: [{ id: confirmId, key: 'PROM-13', rowVersion: 4, canConfirm: true }] }
const promiseDetail = commitmentDetail(commitmentRow(projectId, { id: id(72), key: 'PROM-12', targetId: taskId, performerId: personId, weekStart: '2026-09-28', targetDate: '2026-10-02', intendedOutput: 'Complete drainage review', completionCriteria: 'Peer review recorded', state: 'Not Met', rowVersion: 4 }),
  [{ id: id(74), fromState: 'Committed', toState: 'Not Met', reason: 'Blocked by dependency', actorId: personId }])
const myWork = { person: { id: id(1), displayName: 'Sam Supervisor', isActive: true }, readOnly: false, today: '2026-10-03', attention: { total: 0, items: [] }, tasks: [], reviews: { tasks: [], deliverables: [] }, deliverables: [], people: {}, successors: [], decisions: [], actions: [], projects: [], milestones: [], completed: [] }

function responseFor(method, p, body, query) {
  if (p === 'config') return clientConfig()
  if (p === 'me') return { ...me, flags: featurePhase === 'off' ? { task_pickup: false, org_structure: false, verifiable_log: false, commitment_reliability: false } : { task_pickup: true, org_structure: true, verifiable_log: true, commitment_reliability: true } }
  if (p === 'me/sign-in') return {}
  if (p === 'reference') return { disciplines: [{ id: id(60), name: 'Civil' }], offices: [{ id: id(61), name: 'Halifax' }], clients: [], deliverableTypes: [], phases: [], projectTypes: [] }
  if (p === 'workspaces') return []
  if (p === `projects/${projectId}` || p === 'projects/P-DEMO') return project
  if (p === 'pickup-offers') return [offer, assignOffer]
  if (p === 'pickup-offers/my-load') return { week: query?.get('week') ?? '2026-09-28', band: 'Within', hours: 24, capacity: 40, partial: false }
  if (p === 'organisation') return { levels, units, people }
  if (p === 'admin/organisation/levels' && method === 'POST') { levels.push({ id: id(22), name: body?.name || 'Region', sortOrder: 2, rowVersion: 0 }); return levels.at(-1) }
  if (p === 'admin/organisation/units' && method === 'POST') { units.push({ id: id(23), name: body?.name || 'Water', parentId: body?.parentId ?? null, levelId: body?.levelId || levelId, headCount: 0, rowVersion: 0 }); return units.at(-1) }
  if (p === 'admin/organisation/levels') return levels
  if (p === 'admin/organisation/units') return units
  if (p === 'admin/audit-chains') return [chain]
  if (p === `admin/audit-chains/${chainId}/verify`) return { chain, from: 1, valid: true, total: 3, firstBrokenSeq: null, headSeq: 3, headHash: chain.headHash, verifiedAt: '2026-10-03T12:01:00Z', verifier: 'sam@hub.test' }
  if (p === `projects/${projectId}/commitment-reliability`) return reliability
  if (p === `projects/${projectId}/weekly-commitments/${id(72)}`) return promiseDetail
  if (p === `projects/${projectId}/weekly-commitments/${confirmId}/confirm` && method === 'POST') return {}
  if (p.startsWith('tasks/') && p.endsWith('/pickup')) return {}
  if (p === `projects/${projectId}/readiness/window`) return { constraints: [], constraintsTotal: 0, constraintsTruncated: false, readyOutputs: [], readyOutputsTotal: 0, readyOutputsTruncated: false, pageSize: 50, truncated: false, total: 0 }
  if (p === `projects/${projectId}/weekly-commitments`) return commitmentList([])
  if (p === 'admin/organisation/import/preview' && failImport) return { __status: 422, detail: 'CSV header is missing email.' }
  if (p === 'admin/organisation/import/preview' && method === 'POST') return { rows: 2, changes: [{ kind: 'create', unitPath: 'Design / Water' }, { kind: 'unchanged', email: 'alex@hub.test' }], creates: 1, applied: false }
  if (p === `projects/${projectId}/changes/options`) return coordinationOptions({ canWrite: false })
  if (p === 'notifications/pulse' || p === 'me/notifications/pulse' || p === 'notifications/unread-count' || p === 'me/notifications/unread-count') return p.endsWith('count') ? { notifications: 0, following: 0 } : { stamp: '0' }
  if (p === 'me/work') return myWork
  if (p === 'tasks') return { items: [], page: 1, pageSize: 200, totalCount: 0 }
  if (p === 'planning/grid') return { weeks: ['2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26', '2026-11-02'], currentWeek: '2026-09-28', people: [{ id: id(1), displayName: 'Sam Supervisor', canCreateSelfEntry: false, cells: [{ week: '2026-09-28', capacity: 40, timeAway: 0, additionalAvailability: 0, confirmed: 0, expected: 0, possible: 0, remaining: 40, taskEstimates: 0, unestimatedTasks: 0, staleEntries: 0, overPlanned: false, underPlanned: false }], entries: [] }] }
  if (p === 'views') return { views: [], canShare: false }
  if (p === 'projects') return { items: [project], page: 1, pageSize: 200, totalCount: 1 }
  return undefined
}

async function axe(page, label) {
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  const violations = await page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } })).violations.map(v => ({ id: v.id, targets: v.nodes.map(n => n.target) })))
  axeReports.push({ label, violations })
}
async function shot(page, label) { const file = path.join(out, `${label}.png`); await page.screenshot({ path: file, fullPage: true }); shots.push(file) }
async function waitForApi(page) { await page.waitForLoadState('networkidle'); await page.waitForTimeout(80) }

(async () => {
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' })
  for (let i = 0; i < 50; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'sam@hub.test'))
  page.on('pageerror', e => errors.push(e.message))
  await page.route('**/api/**', async route => {
    const req = route.request(), u = new URL(req.url()), p = u.pathname.replace('/api/v1/', ''), method = req.method()
    totalApiCalls++
    let body; try { body = req.postDataJSON() } catch {}
    calls.push({ method, path: p, query: u.searchParams.toString(), body })
    const data = responseFor(method, p, body, u.searchParams)
    if (data === undefined) { if (method === 'GET') unknownGets.push(p); return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ detail: `unmocked ${p}` }) }) }
    const status = data.__status || 200; if (data.__status) delete data.__status
    return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
  })

  // Feature gates: off means no nav entry, no feature GET, and direct routes are Not Found.
  await page.goto(`${base}/my-work`); await waitForApi(page)
  const assertMyWork = async (url, view, pickupVisible) => { await page.goto(`${base}${url}`); await page.getByRole('heading', { name: 'My Work', exact: true }).waitFor(); await page.getByRole('button', { name: new RegExp(`My Work views: ${view}`) }).waitFor(); await page.getByRole('main').getByRole('button', { name: 'Views', exact: true }).waitFor(); await page.getByRole('main').getByRole('button', { name: 'New task', exact: true }).first().waitFor(); const pickup = page.getByRole('main').getByRole('link', { name: 'Open for pickup', exact: true }); if (pickupVisible) await pickup.waitFor(); assert.equal(await pickup.count(), pickupVisible ? 1 : 0, `${view} pickup link flag state`); return { view, pickupVisible, viewSwitcher: true, savedViews: true, newTask: true } }
  const flagPairAssertions = []
  flagPairAssertions.push(await assertMyWork('/my-work?tab=overview', 'Overview', false)); flagPairAssertions.push(await assertMyWork('/my-work?tab=upcoming', 'Upcoming', false))
  assert.equal(await page.getByRole('link', { name: 'Organisation', exact: true }).count(), 0)
  const offGets = calls.filter(c => c.method === 'GET').map(c => c.path)
  assert.equal(offGets.some(p => p === 'pickup-offers' || p === 'organisation'), false)
  await page.goto(`${base}/pickup`); await page.getByText(/This page or item does not exist/i).waitFor(); await axe(page, 'flagoff-pickup'); await shot(page, 'flagoff-pickup')

  // Pickup: real row, confirmation dialog, and rowVersion POST.
  featurePhase = 'on'; me.flags.task_pickup = true; calls.length = 0
  flagPairAssertions.push(await assertMyWork('/my-work?tab=overview', 'Overview', true)); flagPairAssertions.push(await assertMyWork('/my-work?tab=upcoming', 'Upcoming', true))
  await page.goto(`${base}/pickup`); await page.getByText('T-101', { exact: true }).waitFor(); await page.getByText(/Your workload for/).waitFor(); await axe(page, 'pickup'); await shot(page, 'pickup')
  // FR-PKP-04: a row the viewer may only assign shows Assign (to the task panel) and no Pick up.
  await page.getByText('T-102', { exact: true }).waitFor()
  assert.equal(await page.getByRole('button', { name: 'Pick up', exact: true }).count(), 1, 'Pick up only on the same-team row')
  const assignLink = page.getByRole('link', { name: 'Assign pickup', exact: true }); assert.equal(await assignLink.count(), 1)
  assert.ok((await assignLink.getAttribute('href')).includes(`Task:${id(172)}`), 'Assign opens the task panel')
  await page.getByRole('button', { name: 'Pick up', exact: true }).click(); await page.getByRole('dialog').getByRole('button', { name: 'Pick up task', exact: true }).click(); await page.getByText('Task picked up').waitFor()
  const pickupPost = calls.find(c => c.method === 'POST' && c.path === `tasks/${taskId}/pickup`); assert.deepEqual(pickupPost.body, { rowVersion: 7 })
  await waitForApi(page)
  assert.ok(calls.some(c => c.path === 'pickup-offers/my-load' && c.query === 'week=2026-10-12'), 'load hint uses needed-by week')
  assert.ok(calls.filter(c => c.path === 'pickup-offers/my-load').length >= 2, 'pickup refreshes own load')

  // Organisation: null roots render as roots; expansion exposes person/headcount.
  me.flags.org_structure = true; calls.length = 0
  await page.goto(`${base}/organisation`); await page.getByText('Design', { exact: true }).waitFor(); await page.getByRole('button', { name: /Design/ }).click(); await page.getByText('Alex Engineer').waitFor(); await page.getByText(/1 person in subtree/).waitFor(); await axe(page, 'organisation'); await shot(page, 'organisation')

  // Admin structure add level/unit and show a server error in the existing ErrorBanner.
  await page.goto(`${base}/admin/structure`); await page.getByRole('heading', { name: 'Levels' }).waitFor(); await page.getByLabel('Level name', { exact: true }).fill('Region'); await page.getByRole('button', { name: 'Add level' }).click(); await page.getByLabel('Unit name', { exact: true }).fill('Water'); await page.getByLabel('Level', { exact: true }).selectOption(levelId); await page.getByRole('button', { name: 'Add unit' }).click(); await page.getByRole('row').filter({ hasText: 'Water' }).first().waitFor(); failImport = true; await page.getByPlaceholder('unit_path,level,leader_email,discipline,office,email,manager_email').fill('invalid'); await page.getByRole('button', { name: 'Preview' }).click(); await page.getByText(/CSV header is missing email/i).waitFor(); failImport = false; await page.getByPlaceholder('unit_path,level,leader_email,discipline,office,email,manager_email').fill('Design,Department,,,,alex@hub.test,'); await page.getByRole('button', { name: 'Preview' }).click(); await page.getByText('Reviewed preview: 2 rows, 1 creates, 0 applied').waitFor(); await page.getByRole('button', { name: 'Apply', exact: true }).evaluate(async button => { await Promise.all(button.getAnimations().map(animation => animation.finished)) }); const importApplyStyle = await page.getByRole('button', { name: 'Apply', exact: true }).evaluate(button => { const style = getComputedStyle(button); return { color: style.color, backgroundColor: style.backgroundColor, runningAnimations: button.getAnimations().filter(animation => animation.playState === 'running').length } }); await axe(page, 'admin-structure'); await shot(page, 'admin-structure')

  me.flags.verifiable_log = true
  await page.goto(`${base}/admin/audit-chains`); await page.getByText(chainId).waitFor(); await axe(page, 'audit-chains'); await page.getByRole('button', { name: 'Verify', exact: true }).click(); await page.getByRole('status').getByText(/verified/i).waitFor(); const verifyCall = calls.find(c => c.path === `admin/audit-chains/${chainId}/verify`); assert.equal(verifyCall.method, 'GET'); await shot(page, 'audit-chains')

  // Reliability is reachable through the real project readiness route.
  me.flags.commitment_reliability = true
  await page.reload(); await page.goto(`${base}/projects/P-DEMO/readiness`); await page.getByRole('heading', { name: /Readiness/i }).waitFor(); await page.getByText(/78%/).waitFor(); await page.getByText('Watch', { exact: true }).waitFor(); await page.getByText('Blocked by dependency', { exact: true }).waitFor(); assert.ok(calls.some(c => c.path === `projects/${projectId}/commitment-reliability`), 'reliability API requested'); const missLink = page.getByRole('button', { name: new RegExp(`^${id(72).slice(0, 8)}`) }).first(); await missLink.click(); await page.waitForURL(url => new URL(url).searchParams.get('promise') === id(72)); const promiseDialog = page.getByRole('dialog'); await promiseDialog.getByText('Complete drainage review', { exact: true }).waitFor(); await promiseDialog.getByText('Peer review recorded', { exact: true }).waitFor(); await promiseDialog.getByText('Blocked by dependency', { exact: true }).waitFor(); await page.keyboard.press('Escape'); await page.getByRole('button', { name: 'Confirm met', exact: true }).click(); await page.waitForTimeout(100); const confirmCall = calls.find(c => c.method === 'POST' && c.path === `projects/${projectId}/weekly-commitments/${confirmId}/confirm`); assert.ok(confirmCall?.body?.requestId); assert.equal(confirmCall.body.rowVersion, 4); assert.equal(confirmCall.body.return, false); assert.equal(confirmCall.body.reason, null); await axe(page, 'readiness-reliability-route'); await shot(page, 'readiness-reliability-route')

  reliability.snapshots[0].reliability = { met: 0, committed: 0, percent: null, band: null, withdrawn: 0, notYetRecorded: 0, awaiting: 0 }
  await page.reload(); await page.getByText('Met 0 of 0 (—%)', { exact: true }).waitFor(); assert.equal(await page.getByText('Watch', { exact: true }).count(), 0)

  assert.deepEqual(errors, [], 'page errors')
  assert.deepEqual([...new Set(unknownGets)], [], 'unknown GETs')
  assert.deepEqual(axeReports.flatMap(x => x.violations), [], 'axe')
  const report = { scope: 'Chromium UI with mocked API responses; source checks and backend rules are separate', port, importApplyStyle, passes: 8, calls: totalApiCalls, retainedCalls: calls.length, assertions: ['My Work overview/upcoming flag-off and flag-on render', 'needed-by week load hint', 'load refresh after pickup', 'numeric organisation import preview count', 'UUID reliability link opens matching promise detail', 'null reliability rate and band render without page error'], flagPairAssertions, post: pickupPost, axe: axeReports, screenshots: shots, pageErrors: errors, unknownGets: [...new Set(unknownGets)] }
  fs.writeFileSync(path.join(out, 'schema-workflows.json'), JSON.stringify(report, null, 2))
  if (fs.existsSync('/private/tmp/tuesday-039-050-coordination')) fs.writeFileSync('/private/tmp/tuesday-039-050-coordination/schema-browser.md', `# Browser evidence: packets 048–050\n\n- Command: \`PORT=19505 node web/tests/schema-workflows.cjs\`\n- Chromium mocked API workflows: PASS, 8 bounded workflows including My Work overview/upcoming with feature flag off/on, pickup, organisation, admin structure/error display, audit-chain verification, and readiness reliability.\n- My Work flag pairs: overview and upcoming each asserted pickup link count 0 with flag off and 1 with flag on; view switcher, saved Views control, and New task remained available in all four cases.\n- Reliability assertions: rendered \`Met 78 of 100 (78%)\`, \`Watch\`, \`Blocked by dependency\`; verified POST confirmation with rowVersion 4 and requestId.\n- Accessibility: PASS, six page checks with zero violations.\n- Runtime: PASS, zero page errors and zero unmocked GETs.\n- Pickup POST body: \`${JSON.stringify(pickupPost.body)}\`.\n- Boundary: browser UI against mocked API responses; backend/native/deployed acceptance is not established.\n\nArtifacts are in \`web/test-results/schema-workflows/\`.\n`)
  console.log(JSON.stringify(report))
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() })
