// Design basis register UI checks with mocked API responses (run after `npm run build`); API tests cover server rules.
// Regression for the 2026-10-01 rehearsal: impact decisions offered for "Pending Assessment" (D1), the affected-work
// filter sends a bare id (D2), dialogs return focus to their opener (D3) and withdrawn-version uses are flagged (D4).
const { chromium } = require('playwright');
const path = require('path'), { spawn } = require('child_process'), assert = require('assert/strict');
const port = process.env.PORT ?? '5176', base = `http://127.0.0.1:${port}`;
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`;
const pid = id(1), consumer = id(2), lead = id(3), civil = id(4), task = id(7), e1 = id(20), e2 = id(21), v11 = id(30), v12 = id(31), v21 = id(32), u1 = id(40), u2 = id(41), i1 = id(50), i2 = id(51);
const coordinationUses = Array.from({ length: 5 }, (_, n) => ({ id: id(600 + n), targetType: 'Task', targetId: id(610 + n), sourceRevisionId: id(620 + n), targetKey: `P-DEMO-T${String(n + 1).padStart(3, '0')}`, targetName: `Coordination target ${n + 1}`, sourceKey: `P-DEMO-B${String(n + 1).padStart(3, '0')}`, sourceUrl: `https://example.test/source/${n + 1}`, revision: `R${n + 1}`, intendedUse: `Input use ${n + 1}` }));
let who = 'consumer';
const actor = () => (who === 'consumer' ? consumer : lead);
const project = () => ({ id: pid, projectNumber: 'P-DEMO', name: 'Basis checks', client: 'Pilot client', pmName: 'Pat PM', status: 'Active', visibility: 'Open', rowVersion: 1, links: [], starred: false,
  disciplines: [{ id: civil, disciplineId: id(5), name: 'Civil', code: 'CIV', colour: '#2563eb', leadUserId: lead, isActive: true, leadName: 'Marc Lead' }],
  myRoles: who === 'consumer' ? ['TeamMember'] : ['DisciplineLead'], health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] },
  permissions: { raiseRegister: { ok: false }, edit: { ok: false }, healthOverride: { ok: false }, isPm: false, leadOf: who === 'consumer' ? [] : [civil], createTaskIn: [civil], createDeliverableIn: [civil], transitions: [] } });
const me = () => ({ id: actor(), displayName: who, email: `${who}@hub.test`, roles: [], systemRoles: [], capabilities: { createProject: false, createTask: true, portfolio: false, workload: false, staff: false, admin: false, templates: false, readOnly: false, directReports: 0 },
  settings: { coordinationLookaheadWeeks: 6, today: '2026-10-01', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8 }, preferences: { denseRows: true, digestEnabled: true } });
const options = () => ({ actorId: actor(), canWrite: true, manageDisciplineIds: who === 'consumer' ? [] : [civil], people: [{ id: consumer, displayName: 'Alex Consumer' }, { id: lead, displayName: 'Marc Lead' }],
  disciplines: [{ id: civil, name: 'Civil' }], sources: [], heads: [], deliverables: [],
  tasks: [{ id: task, key: 'P-DEMO-T0001', name: 'Watermain layout', projectDisciplineId: civil, ownerId: consumer, rowVersion: 3, status: 'In Progress' }] });
const version = (vid, entryId, number, status, extra = {}) => ({ id: vid, entryId, number, status, scope: 'Site - watermain', statement: 'Minimum cover', numericValue: 1.5, units: 'm', sourceSystem: 'DMS', declaredRevision: 'A', sourceUrl: 'https://example.test/a', rowVersion: 1, ...extra });
const row = (eid, key, current, latest) => ({ id: eid, key, title: `Entry ${key}`, kind: 'Criterion', ownerId: consumer, projectDisciplineId: civil, currentVersionId: current, rowVersion: 2, currentStatus: current ? 'Confirmed' : undefined, latestStatus: latest, conflictCount: 0 });
const detail = eid => eid === e1
  ? { entry: row(e1, 'P-DEMO-B001', v12, 'Confirmed'), versions: [version(v11, e1, 1, 'Superseded'), version(v12, e1, 2, 'Confirmed', { supersedesVersionId: v11, numericValue: 1.8 })].map(v => ({ version: v, sourceMissing: false })),
    uses: [{ id: u1, versionId: v11, targetType: 'Task', targetId: task, intendedUse: 'Cover check', ownerId: consumer, rowVersion: 1, isCurrent: true }],
    impacts: [{ id: i1, basisUseId: u1, oldVersionId: v11, newVersionId: v12, status: 'Pending Assessment', ownerId: consumer, rowVersion: 1 }], conflicts: [], dispositions: [],
    canManage: who === 'lead', canEditProposed: false, canConfirm: false }
  : { entry: row(e2, 'P-DEMO-B002', null, 'Withdrawn'), versions: [{ version: version(v21, e2, 1, 'Withdrawn'), sourceMissing: false }],
    uses: [{ id: u2, versionId: v21, targetType: 'Task', targetId: task, intendedUse: 'Profile check', ownerId: consumer, rowVersion: 1, isCurrent: true }],
    impacts: [{ id: i2, basisUseId: u2, oldVersionId: v21, withdrawalVersionId: v21, status: 'Pending Assessment', ownerId: consumer, rowVersion: 1 }], conflicts: [], dispositions: [],
    canManage: who === 'lead', canEditProposed: false, canConfirm: false };
// Reuse the consuming task, with the real TaskRow and TaskDetailData contracts used by the register and panel.
const taskRow = () => ({ id: task, key: options().tasks[0].key, name: options().tasks[0].name, projectDisciplineId: civil,
  projectId: pid, projectNumber: 'P-DEMO', projectName: 'Basis checks', projectStatus: 'Active', status: 'In Progress', lane: 'In Progress',
  disciplineName: 'Civil', disciplineCode: 'CIV', disciplineColour: '#2563eb', disciplineOrder: 1, milestoneDerived: false,
  assigneeId: consumer, assigneeName: 'Alex Consumer', assigneeActive: true, reviewerId: lead, reviewerName: 'Marc Lead', requiresReview: true,
  startDate: '2026-10-01', originalStartDate: '2026-10-01', dueDate: '2026-10-05', originalDueDate: '2026-10-05', priority: 'Medium', progressPct: 20,
  estimatedHours: 4, reviewRound: 0, dueDateChangeCount: 0, lastActivityAt: '2026-10-01T09:00:00Z', createdAt: '2026-10-01T09:00:00Z',
  sortOrder: 1, rowVersion: 3, commentCount: 0, predecessorCount: 0, successorCount: 0, collaboratorIds: [], state: null });
const taskDetail = () => ({ task: taskRow(), project: project(), description: 'Check watermain cover against design basis version 1.',
  collaborators: [], watchers: [], permissions: { edit: { ok: true }, assign: { ok: true }, setReviewer: { ok: true }, dueDate: { ok: true },
    dueNeedsReason: false, block: { ok: true }, delete: { ok: false }, restore: false, comment: true,
    transitions: [{ to: 'Ready for Review', allowed: false, reason: 'Only the assignee may submit for review.', needsReason: false }], isReviewer: true, dependencies: false,
    enterTime: true, needsReason: false, allowSelfReview: false, authoriseStart: false } });
const registerQueries = [], savedViews = [], coordinationQueries = [];
const lists = [], decisions = [], errors = [], unknown = [], taskWrites = [], panelLayouts = [];
let server, browser;
(async () => {
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' });
  for (let i = 0; i < 50; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.addInitScript(() => {
    sessionStorage.setItem('hub.devUser', 'basis@hub.test');
    window.__printCalls = 0;
    window.__printedTaskIds = [];
    window.print = () => {
      window.__printCalls++;
      window.__printedTaskIds = [...new Set([...document.querySelectorAll('section[aria-labelledby="dcv-using"] a[href*="panel"]')]
        .map(a => new URL(a.href).searchParams.get('panel')).filter(panel => panel?.startsWith('Task:')).map(panel => panel.slice('Task:'.length)))].sort();
    };
  });
  page.on('pageerror', e => errors.push(e.message));
  await page.route('**/api/**', async route => {
    const req = route.request(), u = new URL(req.url()), p = u.pathname.replace('/api/v1/', ''), method = req.method(); let data = {};
    if (method !== 'GET' && p.startsWith(`tasks/${task}`)) taskWrites.push({ method, path: p });
    if (p === 'config') data = { authMode: 'Development', entra: {} };
    else if (p === 'me') data = me(); else if (p === 'me/sign-in') data = {}; else if (p === 'me/notifications/pulse') data = { stamp: '0' };
    else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }; else if (p === 'workspaces') data = []; else if (p === 'views' && method === 'POST') { const body = req.postDataJSON(); savedViews.push({ ...body, id: id(300 + savedViews.length), scope: 'Personal', rowVersion: 1, dropped: [], canEdit: true }); data = savedViews.at(-1) }
    else if (p === 'views') data = { views: savedViews.filter(v => v.listType === u.searchParams.get('listType')), canShare: false };
    else if (p === 'projects') data = { items: [project()], totalCount: 1 }; else if (p === `projects/${pid}` || p === 'projects/P-DEMO') data = project();
    else if (p === `projects/${pid}/coordination`) data = {
      window: { thisWeek: { from: '2026-10-01', to: '2026-10-07' }, nextWeek: { from: '2026-10-08', to: '2026-10-14' }, since: '2026-09-01', canMarkReviewed: false },
      headline: { computedHealth: 'Green', overdueTasks: { value: 0, link: '#' }, blockedTasks: { value: 0, link: '#' }, decisionsOverdue: 0, deliverablesAtRisk: { value: 0, link: '#' } },
      milestones: [], deliverables: [], decisions: [], blocked: [], overdue: [], dueThisWeek: [], disciplines: [], issues: [], risks: [], waiting: [],
      completed: { tasks: [], deliverables: [], decisions: [] }, upcoming: { tasks: [], deliverables: [] }, held: { tasks: [], deliverables: [] }
    };
    else if (p === `projects/${pid}/attention`) data = { items: [], snoozed: 0, total: 0 };
    else if (p === `projects/${pid}/discipline-coordination`) {
      const print = !u.searchParams.has('page') && !u.searchParams.has('pageSize');
      coordinationQueries.push({ params: Object.fromEntries(u.searchParams), print });
      if (print) await new Promise(resolve => setTimeout(resolve, 100));
      const page = Number(u.searchParams.get('page') ?? 1);
      const uses = print ? coordinationUses : page === 2 ? coordinationUses.slice(4) : coordinationUses.slice(0, 4);
      data = {
        handoffs: [], outgoing: [], incoming: [], uses, usesTotal: 5, usesPage: print ? 1 : page, usesPageSize: print ? 5 : 4,
        changes: [{ id: id(650), key: 'P-DEMO-C001', title: 'Revised input', status: 'Open', pendingAssessments: 1, acknowledgedPending: 0, projectPending: 1, ownerId: consumer }],
        changesTotal: 1, changesPage: 1, changesPageSize: 4,
        reviews: [{ id: id(660), key: 'P-DEMO-R001', title: 'Pending package review', status: 'In Review', outstandingDisciplines: 1, blockingFindings: 0, coordinatorId: consumer }],
        reviewsTotal: 1, reviewsPage: 1, reviewsPageSize: 4,
        linkedIssues: [{ id: id(690), key: 'P-DEMO-I0001', title: 'Input coordination issue', status: 'Open', ownerId: consumer, ownerName: 'Alex Consumer', projectDisciplineId: civil }],
        linkedIssuesTotal: 1, linkedIssuesPage: 1, linkedIssuesPageSize: 4,
        startability: [], startabilityReadyTotal: 0, startabilityFrom: '2026-10-01', startabilityTo: '2026-10-31', startabilityTotal: 0, startabilityPage: 1, startabilityPageSize: 4,
        linkedActions: [], changeTargets: [], unavailableChangeTargets: [],
        upcomingSubmissions: [{ id: id(670), key: 'P-DEMO-SUB0001', title: 'Permit package', status: 'Checking', effectiveStatus: 'Pending review', targetDate: '2026-10-05', coordinatorId: consumer, failingChecks: [{ sourceId: id(650), kind: 'Pending review', code: 'REVIEW', message: 'Review is pending', sourcePath: `/projects/P-DEMO/reviews?panel=ReviewPackage:${id(660)}` }] }],
        upcomingSubmissionsTotal: 1, upcomingSubmissionsPage: 1,
        staffingConflicts: [{ id: id(680), key: 'P-DEMO-T0001', targetType: 'Task', targetId: coordinationUses[0].targetId, description: 'Staffing confirmation pending', neededBy: '2026-10-05', state: 'Pending', affectedOwnerId: consumer, removalOwnerId: lead }],
        staffingConflictsTotal: 1, staffingConflictsPage: 1,
        blockerGroups: [], blockerGroupsPage: 1, blockerGroupsTotal: 0, handoffPage: 1, handoffPageSize: 4, outgoingPage: 1, incomingPage: 1, outgoingTotal: 0, incomingTotal: 0,
        page, pageSize: print ? 5 : 4, evaluatedAt: '2026-10-01T12:00:00Z'
      };
    }
    else if (p.endsWith('/date-review')) data = { window: null, tasks: [], deliverables: [] }; else if (p.endsWith('/follow')) data = { level: 'My items only', source: 'Assignment' };
    else if (p.endsWith('/changes/options')) data = options();
    else if (p === `projects/${pid}/team`) data = { members: [{ userId: consumer, displayName: 'Alex Consumer', primaryDisciplineId: civil }, { userId: lead, displayName: 'Marc Lead', primaryDisciplineId: civil }] };
    else if (p === `projects/${pid}/decisions` || p === `projects/${pid}/deliverables` || p === `projects/${pid}/milestones`) data = [];
    else if (p === `projects/${pid}/tasks`) data = { items: [taskRow()], page: 1, pageSize: 200, totalCount: 1 };
    else if (p === `tasks/${task}`) data = taskDetail();
    else if (p === `tasks/${task}/dependencies`) data = { dependsOn: [], blocks: [] };
    else if (p === `items/Task/${task}/comments`) data = { items: [], canComment: true };
    else if (p === `items/Task/${task}/links`) data = { links: [], inherited: [], canAdd: true };
    else if (p === `projects/${pid}/design-basis`) { lists.push(u.search); data = { items: [row(e1, 'P-DEMO-B001', v12, 'Confirmed'), row(e2, 'P-DEMO-B002', null, 'Withdrawn')], pageSize: 50, totalCount: 101 } }
    else if (p === `projects/${pid}/submissions`) { registerQueries.push({ list: 'submissions', params: Object.fromEntries(u.searchParams) }); data = { items: [{ id: id(100), key: 'P-DEMO-SUB0001', title: 'Synthetic permit', coordinatorId: consumer, targetDate: '2026-10-05', status: 'Checking', blockerCount: 2 }], pageSize: 50, totalCount: 101 } }
    else if (p === `projects/${pid}/allocations`) { registerQueries.push({ list: 'allocations', params: Object.fromEntries(u.searchParams) }); data = { items: [{ id: id(110), personName: 'Alex Consumer', purpose: 'Production', fromDate: '2026-10-01', throughDate: '2026-10-05', plannedHours: 8, status: 'Proposed' }], pageSize: 50, totalCount: 101 } }
    else if (p === `projects/${pid}/readiness/window`) { registerQueries.push({ list: 'window', params: Object.fromEntries(u.searchParams) }); data = { constraints: [{ id: id(130), targetType: 'Task', targetId: task, category: 'Handoff', description: 'Synthetic constraint', neededBy: '2026-10-05', sourceUrl: 'https://example.test/input' }], constraintsTotal: 101, constraintsTruncated: false, readyOutputs: [{ id: id(131), targetType: 'Task', targetId: task, key: 'P-DEMO-T0001', name: 'Watermain layout', dueDate: '2026-10-05', intendedOutput: 'Synthetic layout', completionCriteria: 'Checked', state: 'Ready' }], readyOutputsTotal: 101, readyOutputsTruncated: false, pageSize: 50 } }
    else if (p === `projects/${pid}/weekly-commitments`) { registerQueries.push({ list: 'commitments', params: Object.fromEntries(u.searchParams) }); data = { commitments: [{ id: id(120), key: 'P-DEMO-WC0001', targetType: 'Task', targetId: task, performerId: consumer, weekStart: '2026-09-28', targetDate: '2026-10-05', intendedOutput: 'Synthetic output', completionCriteria: 'Checked', state: 'Proposed', rowVersion: 1 }], snapshots: [], total: 101, truncated: false, page: Number(u.searchParams.get('page') ?? 1), pageSize: 50 } }
    else if (p === `projects/${pid}/issues`) data = Array.from({ length: 51 }, (_, n) => ({ id: id(200 + n), key: `P-DEMO-I${String(n + 1).padStart(4, '0')}`, title: `Synthetic issue ${String(n + 1).padStart(3, '0')}`, issueType: 'Coordination', severity: 'High', status: 'Open', ownerId: consumer, ownerName: 'Alex Consumer', raisedByName: 'Alex Consumer', projectDisciplineId: civil, disciplineName: 'Civil', affectedDisciplineNames: [], affectedDisciplineSummary: '', dateRaised: '2026-10-01', targetResolutionDate: '2026-10-05', daysOverdue: 0, isOverdue: false, locationLabels: ['North', 'South'], locationSummary: 'North; South', documentLabels: [], documentRevisions: [], documentSummary: '', rowVersion: 1, verificationStatus: 'None' }));
    else if (p === `projects/${pid}/design-basis/${e1}` || p === `projects/${pid}/design-basis/${e2}`) data = detail(p.endsWith(e1) ? e1 : e2);
    else if (method === 'POST' && p.endsWith('/decide')) { decisions.push({ path: p, ...req.postDataJSON() }); data = { id: i1, rowVersion: 2 } }
    else if (method === 'GET') { unknown.push(p); data = {} } else throw new Error(`Unmocked endpoint ${method} ${p}`);
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
  });
  const focused = () => page.evaluate(() => document.activeElement?.textContent?.trim() ?? '');
  const register = page.getByRole('main');
  await page.goto(`${base}/projects/P-DEMO/design-basis`); await page.getByRole('heading', { name: 'Design basis and assumptions' }).waitFor();

  // D2: the affected-work filter sends the bare task id the API binds.
  await register.getByLabel('Affected task or deliverable', { exact: true }).selectOption({ label: 'P-DEMO-T0001 · Watermain layout' });
  await page.waitForFunction(() => location.search.includes('affectedWorkId='));
  await page.waitForTimeout(200);
  assert.ok(lists.some(q => new URLSearchParams(q).get('affectedWorkId') === task), `list queries: ${lists.join(' ')}`);
  await page.goto(`${base}/projects/P-DEMO/design-basis`); await page.getByRole('heading', { name: 'Design basis and assumptions' }).waitFor();

  // D3: Escape returns focus to the control that opened the dialog.
  await page.getByRole('button', { name: 'New basis entry', exact: true }).focus(); await page.keyboard.press('Enter');
  await page.getByRole('dialog', { name: 'New basis entry' }).waitFor(); await page.keyboard.press('Escape');
  await page.getByRole('dialog', { name: 'New basis entry' }).waitFor({ state: 'detached' }); await page.waitForTimeout(100);
  assert.equal(await focused(), 'New basis entry');

  // D1: the consumer can adopt the confirmed replacement of a "Pending Assessment".
  await page.getByRole('button', { name: /^P-DEMO-B001 · / }).focus(); await page.keyboard.press('Enter');
  let entry = page.getByRole('dialog', { name: /^P-DEMO-B001 · / }); await entry.getByText('Uses superseded version').waitFor();
  await entry.getByRole('button', { name: 'Decide impact', exact: true }).click();
  const form = page.getByRole('dialog', { name: 'Decide impact' }); await form.waitFor();
  assert.deepEqual(await form.getByLabel('Decision', { exact: true }).locator('option').allInnerTexts(), ['Choose…', 'Adopt new version']);
  await form.getByLabel('Rationale', { exact: true }).fill('Updated the layout to the new cover');
  await form.getByLabel('Evidence link', { exact: true }).fill('https://example.test/evidence');
  await form.getByRole('button', { name: 'Confirm', exact: true }).click();
  await form.waitFor({ state: 'detached' });
  assert.equal(decisions.length, 1); assert.equal(decisions[0].action, 'Adopt'); assert.equal(decisions[0].targetRowVersion, 3); assert.match(decisions[0].requestId, /^[a-f0-9-]{36}$/);
  await page.keyboard.press('Escape'); await entry.waitFor({ state: 'detached' }); await page.waitForTimeout(100);
  assert.match(await focused(), /^P-DEMO-B001 · /);

  // D4: a current use of a withdrawn version is flagged; the consumer has no decision to make on it.
  await page.getByRole('button', { name: /^P-DEMO-B002 · / }).click();
  entry = page.getByRole('dialog', { name: /^P-DEMO-B002 · / }); await entry.getByText('Uses withdrawn version').waitFor();
  assert.equal(await entry.getByRole('button', { name: 'Decide impact', exact: true }).count(), 0);

  // D1: an independent discipline lead records Unaffected for the withdrawal.
  who = 'lead'; await page.reload(); entry = page.getByRole('dialog', { name: /^P-DEMO-B002 · / }); await entry.getByText('Uses withdrawn version').waitFor();
  await entry.getByRole('button', { name: 'Decide impact', exact: true }).click();
  assert.deepEqual(await page.getByRole('dialog', { name: 'Decide impact' }).getByLabel('Decision', { exact: true }).locator('option').allInnerTexts(), ['Choose…', 'Unaffected by change']);

  // FR-MDC-08: the coordination projection pages input uses while retaining full context and prints all rows.
  who = 'consumer';
  await page.goto(`${base}/projects/P-DEMO/coordination`);
  await page.getByRole('heading', { name: 'Discipline coordination', exact: true }).waitFor();
  const using = page.getByRole('heading', { name: /^Which revision are we using\?/ });
  await using.waitFor();
  for (let n = 1; n <= 4; n++) await page.getByRole('link', { name: `P-DEMO-T${String(n).padStart(3, '0')}`, exact: true }).waitFor();
  assert.equal(await page.getByRole('link', { name: 'P-DEMO-T005', exact: true }).count(), 0);
  await page.locator('section[aria-labelledby="dcv-using"] a[href="https://example.test/source/1"]').waitFor();
  await page.getByRole('heading', { name: /^What changed\?/ }).locator('..').locator('..').getByRole('link', { name: 'P-DEMO-C001', exact: true }).waitFor();
  await page.getByRole('heading', { name: 'Pending reviews · 1', exact: true }).waitFor();
  await page.getByRole('link', { name: 'P-DEMO-SUB0001', exact: true }).waitFor();
  await page.getByRole('link', { name: 'P-DEMO-T0001', exact: true }).last().waitFor();
  assert.ok(coordinationQueries.some(q => q.params.page === '1' && q.params.pageSize === '4' && q.params.disciplineId === undefined), 'Coordination uses page one sends page size four');
  const usesPager = page.locator('[aria-label="Which revision are we using?"]');
  await Promise.all([page.waitForResponse(r => { const u = new URL(r.url()); return u.pathname.endsWith('/discipline-coordination') && u.searchParams.get('page') === '2' }), usesPager.getByRole('button', { name: 'Next', exact: true }).click()]);
  await page.waitForURL(/rowsPage=2/);
  await page.getByRole('link', { name: 'P-DEMO-T005', exact: true }).waitFor();
  assert.equal(await page.getByRole('link', { name: 'P-DEMO-T001', exact: true }).count(), 0);
  await page.getByRole('link', { name: 'P-DEMO-C001', exact: true }).waitFor();
  assert.ok(coordinationQueries.some(q => q.params.page === '2' && q.params.pageSize === '4'), 'Coordination uses page two reaches the fifth row');
  await page.goto(`${base}/projects/P-DEMO/coordination`);
  await page.getByRole('heading', { name: 'Discipline coordination', exact: true }).waitFor();
  await page.getByRole('button', { name: 'Print', exact: true }).click();
  await page.waitForFunction(() => window.__printCalls === 1);
  const printed = await page.evaluate(() => window.__printedTaskIds);
  assert.deepEqual(printed, coordinationUses.map(u => u.targetId).sort(), 'Print captures all five rendered input-use target ids');
  assert.ok(coordinationQueries.some(q => q.print && q.params.page === undefined && q.params.pageSize === undefined), 'Print requests the unpaged coordination projection');
  await page.waitForFunction(() => [...document.querySelectorAll('button')].some(button => button.textContent?.trim() === 'Print' && !button.disabled));
  coordinationUses[4].targetId = id(699);
  const secondProjection = page.waitForResponse(r => { const u = new URL(r.url()); return u.pathname.endsWith('/discipline-coordination') && !u.searchParams.has('page') && !u.searchParams.has('pageSize'); });
  await page.getByRole('button', { name: 'Print', exact: true }).click();
  await secondProjection;
  await page.waitForFunction(() => window.__printCalls === 2);
  const reprinted = await page.evaluate(() => window.__printedTaskIds);
  assert.deepEqual(reprinted, coordinationUses.map(u => u.targetId).sort(), 'Second print captures changed input-use target ids after the fresh response');

  // FR-MDC-06: list controls retain URL filters, page before expanding issue groups and save definitions only.
  await page.goto(`${base}/projects/P-DEMO/design-basis`);
  await Promise.all([page.waitForResponse(r => { const u = new URL(r.url()); return u.pathname.endsWith('/design-basis') && u.searchParams.get('page') === '2' }), register.getByRole('button', { name: 'Next', exact: true }).click()]);
  await page.waitForURL(/page=2/);
  await register.getByLabel('Kind', { exact: true }).selectOption('Assumption');
  await page.waitForFunction(() => location.search.includes('kind=Assumption') && !new URLSearchParams(location.search).has('page'));
  assert.ok(lists.some(q => new URLSearchParams(q).get('page') === '2'), 'Basis Next reaches page two');

  for (const list of ['submissions', 'allocations']) {
    await page.goto(`${base}/projects/P-DEMO/${list}`);
    await Promise.all([page.waitForResponse(r => { const u = new URL(r.url()); return u.pathname.endsWith(`/${list}`) && u.searchParams.get('page') === '2' }), register.getByRole('button', { name: 'Next', exact: true }).click()]);
    await page.waitForURL(/page=2/);
    await register.getByLabel('Search', { exact: true }).fill('Synthetic');
    await page.waitForFunction(() => location.search.includes('q=Synthetic') && !new URLSearchParams(location.search).has('page'));
    await register.getByRole('button', { name: 'Views', exact: true }).click();
    await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click();
    const save = page.getByRole('dialog', { name: 'Save this view', exact: true });
    await save.getByLabel('Name', { exact: true }).fill(`${list} synthetic filter`);
    await save.getByRole('button', { name: 'Save', exact: true }).click();
    await save.waitFor({ state: 'detached' });
    assert.equal(savedViews.at(-1).listType, list);
    assert.equal(savedViews.at(-1).params.q, 'Synthetic');
    assert.ok(!savedViews.at(-1).params.allocation && !savedViews.at(-1).params.panel, 'Open panels are not saved');
    assert.ok(registerQueries.some(q => q.list === list && q.params.page === '2'), `${list} Next sends page two`);
  }

  await page.goto(`${base}/projects/P-DEMO/issues?group=location`);
  await register.getByRole('button', { name: 'Synthetic issue 001', exact: true }).first().waitFor();
  assert.equal(await register.getByRole('button', { name: 'Synthetic issue 001', exact: true }).count(), 2, 'A two-location issue stays on one identity page');
  assert.equal(await register.getByRole('button', { name: 'Synthetic issue 051', exact: true }).count(), 0);
  await register.getByRole('button', { name: 'Next', exact: true }).click();
  await page.waitForURL(/page=2/);
  await register.getByRole('button', { name: 'Synthetic issue 051', exact: true }).first().waitFor();
  assert.equal(await register.getByRole('button', { name: 'Synthetic issue 051', exact: true }).count(), 2);
  assert.equal(await register.getByRole('button', { name: 'Synthetic issue 001', exact: true }).count(), 0);
  await register.getByRole('button', { name: 'Title', exact: true }).click();
  await page.waitForFunction(() => !new URLSearchParams(location.search).has('page'));
  await register.getByRole('button', { name: 'Synthetic issue 001', exact: true }).first().waitFor();

  await page.goto(`${base}/projects/P-DEMO/readiness`);
  for (const [label, param] of [['Constraints to remove', 'constraintsPage'], ['Ready outputs', 'readyPage']]) {
    const pager = register.getByRole('navigation', { name: label, exact: true });
    await Promise.all([page.waitForResponse(r => { const u = new URL(r.url()); return u.pathname.endsWith('/readiness/window') && u.searchParams.get(param) === '2' }), pager.getByRole('button', { name: 'Next', exact: true }).click()]);
    await page.waitForURL(new RegExp(`${param}=2`));
    await pager.getByRole('button', { name: 'Next', exact: true }).waitFor();
  }
  const promises = register.getByRole('navigation', { name: 'Weekly promises', exact: true });
  await Promise.all([page.waitForResponse(r => { const u = new URL(r.url()); return u.pathname.endsWith('/weekly-commitments') && u.searchParams.get('page') === '2' }), promises.getByRole('button', { name: 'Next', exact: true }).click()]);
  await page.waitForURL(/promisePage=2/);
  await register.getByLabel('From', { exact: true }).fill('2026-10-02');
  await page.waitForFunction(() => location.search.includes('from=2026-10-02') && !new URLSearchParams(location.search).has('promisePage'));
  await promises.getByRole('button', { name: 'Next', exact: true }).waitFor();
  assert.ok(registerQueries.some(q => q.list === 'commitments' && q.params.page === '2'), 'Promise Next reaches page two');
  assert.ok(registerQueries.some(q => q.list === 'commitments' && q.params.targetFrom === '2026-10-02' && q.params.page === '1'), 'Window filtering happens in the API before paging');
  assert.ok(registerQueries.some(q => q.list === 'window' && q.params.from === '2026-10-02' && q.params.constraintsPage === '1' && q.params.readyPage === '1'), 'Date changes reset both aggregate list pages');

  // Shared Sheet: preserve keyboard focus and keep editable people/date controls inside the panel at each viewport.
  await page.goto(`${base}/projects/P-DEMO/tasks`);
  const dateFields = [['Start date', '2026-10-01'], ['Due date', '2026-10-05']];
  const dateNameFailures = [];
  for (const width of [1440, 390, 320]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.waitForFunction((isPhone) => Boolean(document.querySelector(`[data-task-layout="${isPhone ? 'phone' : 'desktop'}"]`)), width <= 639);
    for (const [dateLabel, dateValue] of dateFields) {
      const taskTitle = register.getByRole('button', { name: 'Watermain layout', exact: true });
      await taskTitle.waitFor();
      await register.getByRole('button', { name: 'P-DEMO-T0001', exact: true }).focus();
      await page.keyboard.press('Tab');
      const opener = await taskTitle.elementHandle();
      assert.equal(await opener.evaluate(el => document.activeElement === el), true, 'Tab reaches the task title');
      await page.keyboard.press('Enter');
      const panel = page.getByRole('dialog', { name: 'Task', exact: true });
      await panel.getByRole('heading', { name: 'Watermain layout', exact: true }).waitFor();
      await panel.getByText('No comments yet. Discuss the work here; changes are recorded in History.', { exact: true }).waitFor();
      await panel.getByText('No document links yet.', { exact: true }).waitFor();
      await panel.evaluate(async el => {
        await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
        await Promise.all(el.getAnimations().map(animation => animation.finished));
      });
      const bounds = await panel.boundingBox();
      const clears = panel.getByRole('button', { name: 'Clear', exact: true });
      assert.equal(await clears.count(), 2, 'Assigned assignee and reviewer both retain their Clear control');
      const clearBounds = await Promise.all([clears.nth(0).boundingBox(), clears.nth(1).boundingBox()]);
      assert.ok(clearBounds.every(control => control?.width === 32 && control.height === 32), 'Clear controls retain their full size');
      await panel.getByRole('button', { name: dateValue, exact: true }).click();
      const date = panel.locator('input[type="date"]');
      await date.waitFor();
      assert.equal(await date.inputValue(), dateValue);
      if (await panel.getByLabel(dateLabel, { exact: true }).and(date).count() !== 1) dateNameFailures.push(`${dateLabel} has no accessible field name at ${width}px`);
      panelLayouts.push({ width, bounds, clearBounds, dateLabel, dateBounds: await date.boundingBox() });
      await page.keyboard.press('Escape');
      await panel.waitFor({ state: 'detached' });
      await page.waitForFunction(el => document.activeElement === el, opener, { timeout: 2000 });
      assert.equal(await opener.evaluate(el => el.isConnected && document.activeElement === el), true, 'Escape restores the original task title');
      assert.equal(new URL(page.url()).searchParams.has('panel'), false);
    }
  }
  // A live resize while a task control owns focus must preserve the same logical control.
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.waitForSelector('[data-task-layout="desktop"]');
  const resizeTitle = register.getByRole('button', { name: 'Watermain layout', exact: true });
  await resizeTitle.waitFor();
  await resizeTitle.focus();
  await page.setViewportSize({ width: 320, height: 1000 });
  await page.waitForFunction(() => document.activeElement?.getAttribute('data-task-focus') === 'title');
  assert.equal(await page.evaluate(() => document.activeElement?.getAttribute('data-task-id')), task, 'Resize keeps focus on the same task');
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.waitForSelector('[data-task-layout="desktop"]');
  await page.waitForFunction(() => document.activeElement?.getAttribute('data-task-focus') === 'title');
  const filter = register.getByRole('searchbox', { name: 'Search', exact: true });
  await filter.focus();
  await page.setViewportSize({ width: 320, height: 1000 });
  await page.waitForSelector('[data-task-layout="phone"]');
  assert.equal(await page.evaluate(() => document.activeElement?.getAttribute('data-task-focus')), null, 'Resize does not steal focus from a filter');
  assert.deepEqual(dateNameFailures, [], 'Native date editors are associated with their visible field labels');
  const fits = (control, panel, width) => control && panel && control.width > 0 && control.height > 0 &&
    control.x >= Math.max(0, panel.x) - 1 && control.x + control.width <= Math.min(width, panel.x + panel.width) + 1 &&
    control.y >= Math.max(0, panel.y) - 1 && control.y + control.height <= panel.y + panel.height + 1;
  const layoutFailures = panelLayouts.flatMap(sample => [
    ...sample.clearBounds.map((control, i) => ({ name: `${i === 0 ? 'Assignee' : 'Reviewer'} Clear`, control })),
    { name: `${sample.dateLabel} editor`, control: sample.dateBounds },
  ].filter(({ control }) => !fits(control, sample.bounds, sample.width)).map(({ name }) => `${name} overflows at ${sample.width}px`));
  assert.deepEqual(layoutFailures, [], 'Controls fit inside both the sheet and viewport');
  assert.deepEqual(taskWrites, [], 'Opening and cancelling the native date editor does not save the task');

  assert.deepEqual(unknown, []);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ scope: 'Chromium UI with mocked API; backend rules tested separately', listQueries: lists.length, registerQueries: registerQueries.length, savedViews: savedViews.length, issueIdentityPages: 2, decisions: decisions.length, panelWidths: [...new Set(panelLayouts.map(sample => sample.width))], dateFields: dateFields.map(([label]) => label), unmockedGets: [...new Set(unknown)] }));
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() });
