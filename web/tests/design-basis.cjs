// Design basis register UI checks with mocked API responses (run after `npm run build`); API tests cover server rules.
// Regression for the 2026-10-01 rehearsal: impact decisions offered for "Pending Assessment" (D1), the affected-work
// filter sends a bare id (D2), dialogs return focus to their opener (D3) and withdrawn-version uses are flagged (D4).
const { chromium } = require('playwright');
const path = require('path'), { spawn } = require('child_process'), assert = require('assert/strict');
const port = process.env.PORT ?? '5176', base = `http://127.0.0.1:${port}`;
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`;
const pid = id(1), consumer = id(2), lead = id(3), civil = id(4), task = id(7), e1 = id(20), e2 = id(21), v11 = id(30), v12 = id(31), v21 = id(32), u1 = id(40), u2 = id(41), i1 = id(50), i2 = id(51);
let who = 'consumer';
const actor = () => (who === 'consumer' ? consumer : lead);
const project = () => ({ id: pid, projectNumber: 'P-DEMO', name: 'Basis checks', client: 'Pilot client', pmName: 'Pat PM', status: 'Active', visibility: 'Open', rowVersion: 1, links: [], starred: false,
  disciplines: [{ id: civil, disciplineId: id(5), name: 'Civil', code: 'CIV', colour: '#2563eb', leadUserId: lead, isActive: true, leadName: 'Marc Lead' }],
  myRoles: who === 'consumer' ? ['TeamMember'] : ['DisciplineLead'], health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] },
  permissions: { edit: { ok: false }, healthOverride: { ok: false }, isPm: false, leadOf: who === 'consumer' ? [] : [civil], createTaskIn: [civil], createDeliverableIn: [civil], transitions: [] } });
const me = () => ({ id: actor(), displayName: who, email: `${who}@hub.test`, roles: [], systemRoles: [], capabilities: { createProject: false, createTask: true, portfolio: false, workload: false, staff: false, admin: false, templates: false, readOnly: false, directReports: 0 },
  settings: { today: '2026-10-01', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8 }, preferences: { denseRows: true, digestEnabled: true } });
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
  assigneeId: consumer, assigneeName: 'Alex Consumer', assigneeActive: true, requiresReview: false, priority: 'Medium', progressPct: 20,
  estimatedHours: 4, reviewRound: 0, dueDateChangeCount: 0, lastActivityAt: '2026-10-01T09:00:00Z', createdAt: '2026-10-01T09:00:00Z',
  sortOrder: 1, rowVersion: 3, commentCount: 0, predecessorCount: 0, successorCount: 0, collaboratorIds: [], state: null });
const taskDetail = () => ({ task: taskRow(), project: project(), description: 'Check watermain cover against design basis version 1.',
  collaborators: [], watchers: [], permissions: { edit: { ok: true }, assign: { ok: false }, setReviewer: { ok: false }, dueDate: { ok: true },
    dueNeedsReason: false, block: { ok: true }, delete: { ok: false }, restore: false, comment: true,
    transitions: [{ to: 'Complete', allowed: true, needsReason: false }], isReviewer: false, dependencies: false,
    enterTime: true, needsReason: false, allowSelfReview: false, authoriseStart: false } });
const lists = [], decisions = [], errors = [], unknown = [];
let server, browser;
(async () => {
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' });
  for (let i = 0; i < 50; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'basis@hub.test'));
  page.on('pageerror', e => errors.push(e.message));
  await page.route('**/api/**', async route => {
    const req = route.request(), u = new URL(req.url()), p = u.pathname.replace('/api/v1/', ''), method = req.method(); let data = {};
    if (p === 'config') data = { authMode: 'Development', entra: {} };
    else if (p === 'me') data = me(); else if (p === 'me/sign-in') data = {}; else if (p === 'me/notifications/pulse') data = { stamp: '0' };
    else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }; else if (p === 'workspaces') data = []; else if (p === 'views') data = { views: [], canShare: false };
    else if (p === 'projects') data = { items: [project()], totalCount: 1 }; else if (p === `projects/${pid}` || p === 'projects/P-DEMO') data = project();
    else if (p.endsWith('/date-review')) data = { window: null, tasks: [], deliverables: [] }; else if (p.endsWith('/follow')) data = { level: 'My items only', source: 'Assignment' };
    else if (p.endsWith('/changes/options')) data = options();
    else if (p === `projects/${pid}/team`) data = { members: [{ userId: consumer, displayName: 'Alex Consumer', primaryDisciplineId: civil }, { userId: lead, displayName: 'Marc Lead', primaryDisciplineId: civil }] };
    else if (p === `projects/${pid}/decisions` || p === `projects/${pid}/deliverables` || p === `projects/${pid}/milestones`) data = [];
    else if (p === `projects/${pid}/tasks`) data = { items: [taskRow()], page: 1, pageSize: 200, totalCount: 1 };
    else if (p === `tasks/${task}`) data = taskDetail();
    else if (p === `tasks/${task}/dependencies`) data = { dependsOn: [], blocks: [] };
    else if (p === `items/Task/${task}/comments`) data = { items: [], canComment: true };
    else if (p === `items/Task/${task}/links`) data = { links: [], inherited: [], canAdd: true };
    else if (p === `projects/${pid}/design-basis`) { lists.push(u.search); data = { items: [row(e1, 'P-DEMO-B001', v12, 'Confirmed'), row(e2, 'P-DEMO-B002', null, 'Withdrawn')], pageSize: 50, totalCount: 2 } }
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

  // Shared Sheet: Tab to the existing task's title, Enter opens its actual side panel, Escape restores that exact control.
  await page.goto(`${base}/projects/P-DEMO/tasks`);
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
  await page.keyboard.press('Escape');
  await panel.waitFor({ state: 'detached' });
  await page.waitForFunction(el => document.activeElement === el, opener, { timeout: 2000 });
  assert.equal(await opener.evaluate(el => el.isConnected && document.activeElement === el), true, 'Escape restores the original task title');
  assert.equal(new URL(page.url()).searchParams.has('panel'), false);

  assert.deepEqual(unknown, []);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ scope: 'Chromium UI with mocked API; backend rules tested separately', listQueries: lists.length, decisions: decisions.length, unmockedGets: [...new Set(unknown)] }));
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() });
