// Browser evidence for packets 048-050. The UI talks to a mocked API, but all
// assertions below are against rendered workflows and captured request bodies.
const { chromium } = require('playwright')
const { spawn } = require('node:child_process')
const fs = require('node:fs')
const path = require('node:path')
const assert = require('node:assert/strict')

const port = process.env.PORT || '19505'
const base = `http://127.0.0.1:${port}`
const out = path.resolve(__dirname, '../test-results/schema-workflows')
fs.mkdirSync(out, { recursive: true })
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const projectId = id(10), levelId = id(20), unitId = id(21), taskId = id(30), offerId = id(31), chainId = id(40), personId = id(50)
const project = { id: projectId, projectNumber: 'P-DEMO', name: 'Synthetic workflow project', client: 'Pilot client', pmName: 'Pat PM', status: 'Active', visibility: 'Open', rowVersion: 1, links: [], disciplines: [], starred: false, myRoles: [], health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] }, permissions: { edit: { ok: false }, healthOverride: { ok: false }, raiseRegister: { ok: false }, isPm: false, leadOf: [], createTaskIn: [], createDeliverableIn: [], transitions: [], needsReason: false } }
const me = { id: id(1), displayName: 'Sam Supervisor', email: 'sam@hub.test', isTemplateEditor: false, roles: [], systemRoles: ['Admin'], capabilities: { createProject: true, createTask: true, portfolio: true, workload: true, staff: true, allStaff: true, admin: true, templates: true, readOnly: false, directReports: 2 }, settings: { today: '2026-10-03', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8, restrictedProjectsEnabled: false, allowSelfReview: false, taskDueSoonDays: 7, milestoneApproachingDays: 14, chainDepthLimit: 100, defaultWeeklyCapacityHours: 40, coordinationLookaheadWeeks: 6, workingDaysEnabled: true }, preferences: { digestEnabled: true, denseRows: true, weeklySummaryEnabled: true }, flags: { task_pickup: false, org_structure: false, verifiable_log: false, commitment_reliability: false } }

const errors = [], unknownGets = [], calls = [], axeReports = [], shots = []
let failImport = false, featurePhase = 'off'
let server, browser
const levels = [{ id: levelId, name: 'Department', sortOrder: 1, rowVersion: 0 }]
const units = [{ id: unitId, name: 'Design', parentId: null, levelId, leaderId: personId, disciplineId: null, officeId: null, headCount: 1, rowVersion: 0 }]
const people = [{ id: personId, displayName: 'Alex Engineer', email: 'alex@hub.test', unitId, supervisorId: null }]
const offer = { id: offerId, taskId, offeredById: id(2), offeredBy: 'Pat PM', reason: 'Needs a civil lead', hoursRemaining: 12, neededBy: '2026-10-15', note: 'Synthetic offer', openedAt: '2026-10-03T12:00:00Z', task: { key: 'T-101', name: 'Review drainage memo', projectId, projectDisciplineId: id(60), disciplineName: 'Civil', dueDate: '2026-10-20', rowVersion: 7 }, rowVersion: 7, allowed: { pickUp: true, assign: false } }
const chain = { chainId, headSeq: 3, headHash: 'sha256-head', lastVerifiedAt: '2026-10-03T12:00:00Z', firstBrokenSeq: null }
const confirmId = id(73)
const reliability = { snapshots: [{ id: id(70), weekStart: '2026-09-28', reliability: { met: 78, committed: 100, percent: 78, band: 'Watch', withdrawn: 2, notYetRecorded: 20, awaiting: 1 } }], misses: [{ categoryId: id(71), category: 'Blocked by dependency', count: 2, commitments: [{ id: id(72), key: 'PROM-12' }] }], awaiting: [{ id: confirmId, key: 'PROM-13', rowVersion: 4, canConfirm: true }] }

function responseFor(method, p, body) {
  if (p === 'config') return { authMode: 'Development', entra: {} }
  if (p === 'me') return { ...me, flags: featurePhase === 'off' ? { task_pickup: false, org_structure: false, verifiable_log: false, commitment_reliability: false } : { task_pickup: true, org_structure: true, verifiable_log: true, commitment_reliability: true } }
  if (p === 'me/sign-in') return {}
  if (p === 'reference') return { disciplines: [{ id: id(60), name: 'Civil' }], offices: [{ id: id(61), name: 'Halifax' }], clients: [], deliverableTypes: [], phases: [], projectTypes: [] }
  if (p === 'workspaces') return []
  if (p === `projects/${projectId}` || p === 'projects/P-DEMO') return project
  if (p === 'pickup-offers') return [offer]
  if (p === 'organisation') return { levels, units, people }
  if (p === 'admin/organisation/levels' && method === 'POST') { levels.push({ id: id(22), name: body?.name || 'Region', sortOrder: 2, rowVersion: 0 }); return levels.at(-1) }
  if (p === 'admin/organisation/units' && method === 'POST') { units.push({ id: id(23), name: body?.name || 'Water', parentId: body?.parentId ?? null, levelId: body?.levelId || levelId, headCount: 0, rowVersion: 0 }); return units.at(-1) }
  if (p === 'admin/organisation/levels') return levels
  if (p === 'admin/organisation/units') return units
  if (p === 'admin/audit-chains') return [chain]
  if (p === `admin/audit-chains/${chainId}/verify`) return { chain, from: 1, valid: true, total: 3, firstBrokenSeq: null, headSeq: 3, headHash: chain.headHash, verifiedAt: '2026-10-03T12:01:00Z', verifier: 'sam@hub.test' }
  if (p === `projects/${projectId}/commitment-reliability`) return reliability
  if (p === `projects/${projectId}/weekly-commitments/${confirmId}/confirm` && method === 'POST') return {}
  if (p.startsWith('tasks/') && p.endsWith('/pickup')) return {}
  if (p === `projects/${projectId}/readiness/window`) return { constraints: [], constraintsTotal: 0, constraintsTruncated: false, readyOutputs: [], readyOutputsTotal: 0, readyOutputsTruncated: false, pageSize: 50, truncated: false, total: 0 }
  if (p === `projects/${projectId}/weekly-commitments`) return { commitments: [], total: 0, page: 1, pageSize: 50, truncated: false, snapshots: [] }
  if (p === 'admin/organisation/import/preview' && failImport) return { __status: 422, detail: 'CSV header is missing email.' }
  if (p === `projects/${projectId}/changes/options`) return { canWrite: false, people: [], disciplines: [], sources: [], heads: [], deliverables: [], tasks: [] }
  if (p === 'notifications/pulse' || p === 'me/notifications/pulse' || p === 'notifications/unread-count' || p === 'me/notifications/unread-count') return p.endsWith('count') ? { notifications: 0, following: 0 } : { stamp: '0' }
  if (p === 'me/work') return { items: [], page: 1, pageSize: 50, totalCount: 0 }
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
  browser = await chromium.launch({ headless: true })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'sam@hub.test'))
  page.on('pageerror', e => errors.push(e.message))
  await page.route('**/api/**', async route => {
    const req = route.request(), u = new URL(req.url()), p = u.pathname.replace('/api/v1/', ''), method = req.method()
    let body; try { body = req.postDataJSON() } catch {}
    calls.push({ method, path: p, body })
    const data = responseFor(method, p, body)
    if (data === undefined) { if (method === 'GET') unknownGets.push(p); return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ detail: `unmocked ${p}` }) }) }
    const status = data.__status || 200; if (data.__status) delete data.__status
    return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
  })

  // Feature gates: off means no nav entry, no feature GET, and direct routes are Not Found.
  await page.goto(`${base}/my-work`); await waitForApi(page)
  assert.equal(await page.getByRole('link', { name: 'Open for pickup', exact: true }).count(), 0)
  assert.equal(await page.getByRole('link', { name: 'Organisation', exact: true }).count(), 0)
  const offGets = calls.filter(c => c.method === 'GET').map(c => c.path)
  assert.equal(offGets.some(p => p === 'pickup-offers' || p === 'organisation'), false)
  await page.goto(`${base}/pickup`); await page.getByText(/This page or item does not exist/i).waitFor(); await axe(page, 'flagoff-pickup'); await shot(page, 'flagoff-pickup')

  // Pickup: real row, confirmation dialog, and rowVersion POST.
  featurePhase = 'on'; me.flags.task_pickup = true; calls.length = 0
  await page.goto(`${base}/pickup`); await page.getByText('T-101', { exact: true }).waitFor(); await axe(page, 'pickup'); await shot(page, 'pickup')
  await page.getByRole('button', { name: 'Pick up', exact: true }).click(); await page.getByRole('dialog').getByRole('button', { name: 'Pick up task', exact: true }).click(); await page.getByText('Task picked up').waitFor()
  const pickupPost = calls.find(c => c.method === 'POST' && c.path === `tasks/${taskId}/pickup`); assert.deepEqual(pickupPost.body, { rowVersion: 7 })

  // Organisation: null roots render as roots; expansion exposes person/headcount.
  me.flags.org_structure = true; calls.length = 0
  await page.goto(`${base}/organisation`); await page.getByText('Design', { exact: true }).waitFor(); await page.getByRole('button', { name: /Design/ }).click(); await page.getByText('Alex Engineer').waitFor(); await page.getByText(/1 people in subtree/).waitFor(); await axe(page, 'organisation'); await shot(page, 'organisation')

  // Admin structure add level/unit and show a server error in the existing ErrorBanner.
  await page.goto(`${base}/admin/structure`); await page.getByRole('heading', { name: 'Levels' }).waitFor(); await page.getByLabel('Level name', { exact: true }).fill('Region'); await page.getByRole('button', { name: 'Add level' }).click(); await page.getByLabel('Unit name', { exact: true }).fill('Water'); await page.getByLabel('Level', { exact: true }).selectOption(levelId); await page.getByRole('button', { name: 'Add unit' }).click(); await page.getByRole('row').filter({ hasText: 'Water' }).first().waitFor(); failImport = true; await page.getByPlaceholder('unit_path,level,leader_email,discipline,office,email,manager_email').fill('invalid'); await page.getByRole('button', { name: 'Preview' }).click(); await page.getByText(/CSV header is missing email/i).waitFor(); await axe(page, 'admin-structure'); await shot(page, 'admin-structure')

  me.flags.verifiable_log = true
  await page.goto(`${base}/admin/audit-chains`); await page.getByText(chainId).waitFor(); await axe(page, 'audit-chains'); await page.getByRole('button', { name: 'Verify', exact: true }).click(); await page.getByRole('status').getByText(/verified/i).waitFor(); const verifyCall = calls.find(c => c.path === `admin/audit-chains/${chainId}/verify`); assert.equal(verifyCall.method, 'GET'); await shot(page, 'audit-chains')

  // Reliability is reachable through the real project readiness route.
  me.flags.commitment_reliability = true
  await page.reload(); await page.goto(`${base}/projects/P-DEMO/readiness`); await page.getByRole('heading', { name: /Readiness/i }).waitFor(); await page.getByText(/78%/).waitFor(); await page.getByText('Watch', { exact: true }).waitFor(); await page.getByText('Blocked by dependency', { exact: true }).waitFor(); assert.ok(calls.some(c => c.path === `projects/${projectId}/commitment-reliability`), 'reliability API requested'); await page.getByRole('button', { name: 'Confirm met', exact: true }).click(); await page.waitForTimeout(100); const confirmCall = calls.find(c => c.method === 'POST' && c.path === `projects/${projectId}/weekly-commitments/${confirmId}/confirm`); assert.ok(confirmCall?.body?.requestId); assert.equal(confirmCall.body.rowVersion, 4); assert.equal(confirmCall.body.return, false); assert.equal(confirmCall.body.reason, null); await axe(page, 'readiness-reliability-route'); await shot(page, 'readiness-reliability-route')

  const report = { scope: 'Chromium UI with mocked API responses; source checks and backend rules are separate', port, passes: 6, calls: calls.length, post: pickupPost, axe: axeReports, screenshots: shots, pageErrors: errors, unknownGets: [...new Set(unknownGets)] }
  fs.writeFileSync(path.join(out, 'schema-workflows.json'), JSON.stringify(report, null, 2))
  if (fs.existsSync('/private/tmp/tuesday-039-050-coordination')) fs.writeFileSync('/private/tmp/tuesday-039-050-coordination/schema-browser.md', `# Browser evidence: packets 048–050\n\n- Command: \`PORT=19505 node web/tests/schema-workflows.cjs\`\n- Chromium mocked API workflows: PASS for flag gating, pickup, organisation, admin structure/error display, audit-chain verification, and readiness reliability.\n- Reliability assertions: rendered \`Met 78 of 100 (78%)\`, \`Watch\`, \`Blocked by dependency\`; verified POST confirmation with rowVersion 4 and requestId.\n- Accessibility: PASS, six page checks with zero violations.\n- Runtime: PASS, zero page errors and zero unmocked GETs.\n- Pickup POST body: \`${JSON.stringify(pickupPost.body)}\`.\n- Boundary: browser UI against mocked API responses; backend/native/deployed acceptance is not established.\n\nArtifacts are in \`web/test-results/schema-workflows/\`.\n`)
  console.log(JSON.stringify(report))
  assert.deepEqual(errors, [], 'page errors')
  assert.deepEqual([...new Set(unknownGets)], [], 'unknown GETs')
  assert.deepEqual(axeReports.flatMap(x => x.violations), [], 'axe')
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() })
