const { openRoles, contractExample } = require('./open-roles.fixture.cjs')
const { coordinationOptions } = require('./reviews.fixture.cjs')
const { personTasks, capacitySet } = require('./workload.fixture.cjs')
const { allocationRow, allocationPage, allocationDetail, dayAvailability } = require('./allocations.fixture.cjs')
const { planningGrid, planningPerson, planningCell } = require('./planning.fixture.cjs')
// Resources workflow UI checks with mocked API responses (run after `npm run build`); API tests cover server rules.
// Tuesday visual batch 1: a partial view never claims spare capacity (FR-CAP-06), proposed requests stay outside load
// (FR-CAP-04), negative remaining keeps its minus sign, person accents survive a re-sort, denied access, phones and a
// refused capacity preview each get an explanation, and the screens pass axe.
const { chromium } = require('playwright');
const { clientConfig } = require('./config.fixture.cjs');
const { meView, referenceData } = require('./me.fixture.cjs');
const { viewList } = require('./views.fixture.cjs');
const path = require('path'), { spawn } = require('child_process'), assert = require('assert/strict');
const port = process.env.PORT ?? '5177', base = `http://127.0.0.1:${port}`;
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`;
const sam = id(1), alex = id(2), jill = id(3), pid = id(10), proposed = id(20), confirmed = id(21);
const weeks = ['2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26', '2026-11-02', '2026-11-09', '2026-11-16'];
// The server's band at the default thresholds (FR-FIG-05); `shadeBefore` is the client rule it replaced, kept here only to prove
// every fixture cell keeps its shade (AC-FIG-05).
const band = pct => pct > 110 ? 'over' : pct > 90 ? 'near' : pct >= 40 ? 'within' : 'light';
const shadeBefore = pct => pct > 110 ? 'bg-bad-bg' : pct > 90 ? 'bg-warn-bg' : pct >= 40 ? 'bg-ok-bg/50' : null;
// `uncommitted` is the server's FR-FIG-03 value: available − committed, null on a partial row unless negative.
const cells = (set) => weeks.map((week, i) => { const c = { week, hours: 0, pct: 0, available: 40, confirmed: 0, proposed: 0, committed: 0, ...set[i] }; const u = c.available - c.committed; return { ...c, band: band(c.pct), uncommitted: u < 0 || !partial ? u : null } });
const person = (pid_, name, extra) => ({ id: pid_, displayName: name, supervisorId: sam, supervisorName: 'Sam Supervisor', capacity: 40, capacityOverride: false, noDueDate: 0,
  openTasks: 2, unestimated: 0, overdue: 0, projects: 1, overAssigned: false, underAssigned: false, cluster: false, canSetCapacity: true, indicator: 'ok', ...extra });
let partial = true, forbidden = false, figureNames = false, unassignedOn = false, assigned = false, assignmentConflict = true;
let typedPatchHeaders, typedCapacity, typedAvailability, manageAllocations = false, typedClose;
const availabilityReads = [];
const taskDrillCalls = [];
const assignmentCalls = [], capacityCalls = [];
const taskId = id(90);
const grid = (sort) => {
  const people = [
    person(alex, 'Alex Overloaded', { overAssigned: true, indicator: 'over', partialScope: partial, overWeek: weeks[0],
      cells: cells([{ committed: 52, pct: 130, hours: 52 }, { committed: 50, available: 32, pct: 156, confirmed: 30, hours: 44, proposed: 20 }]) }),
    person(jill, 'Jill Partial', { partialScope: partial, unestimated: 2, cells: cells([{ committed: 20, pct: 50, hours: 20 }, {}, { committed: 38, pct: 95, hours: 38 }]) }),
  ]
  return { weeks, today: '2026-10-03', currentWeek: weeks[0], defaultCapacity: 40, thresholds: { overPct: 110, amberPct: 90, underPct: 40 }, people: sort === 'name' ? people.reverse() : people,
    ...(unassignedOn ? { unassigned: { totalCount: assigned ? 0 : 51, showAllPath: `/tasks?unassigned=true&projects=${pid}`, tasks: assigned ? [] : [{ id: taskId, key: 'P-DEMO-T0090', name: 'Synthetic unassigned task', projectId: pid, projectNumber: 'P-DEMO', projectName: 'Synthetic resource checks', startDate: '2026-10-12', dueDate: '2026-10-16', estimatedHours: 4, rowVersion: 7, canAssign: true, editPath: `/tasks/${taskId}` }] } } : {}) }
}
const me = meView({ id: sam, displayName: 'Sam Supervisor', email: 'sam@hub.test', systemRoles: ['Supervisor'],
  capabilities: { createTask: true, workload: true, staff: true, directReports: 2 }, settings: { today: '2026-10-03' } });
// FR-FIG-08: Sam is neither PM nor Discipline Lead, so the server refuses every project-level flag.
const pmOrDl = { ok: false, reason: 'Only the Project Manager or the Discipline Lead can do this.' }, pmOnly = { ok: false, reason: 'Only the Project Manager can do this.' };
const refusedFlags = { proposeAllocation: pmOrDl, snooze: pmOrDl, manageExternalParties: pmOnly, reviewDates: pmOrDl, captureWeeklyPlan: pmOrDl, createSubmission: pmOrDl };
const project = { id: pid, projectNumber: 'P-DEMO', name: 'Synthetic resource checks', client: 'Pilot client', pmName: 'Pat PM', status: 'Active', visibility: 'Open', rowVersion: 1,
  links: [], disciplines: [], starred: false, myRoles: [], health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] },
  permissions: { edit: { ok: false }, healthOverride: { ok: false }, raiseRegister: { ok: false }, ...refusedFlags, isPm: false, leadOf: [], createTaskIn: [], createDeliverableIn: [], transitions: [], needsReason: false } };
const allocation = (aid, status) => allocationRow({ id: aid, personId: jill, status });
const errors = [], unknown = [];
let server, browser;
(async () => {
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' });
  for (let i = 0; i < 50; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'sam@hub.test'));
  page.on('pageerror', e => errors.push(e.message));
  await page.route('**/api/**', async route => {
    const u = new URL(route.request().url()), p = u.pathname.replace('/api/v1/', '')
    let data = {}, status = 200
    if (p === 'config') data = clientConfig()
    else if (p === 'me') data = { ...me, flags: { figure_names: figureNames, unfilled_beside_capacity: unassignedOn } }
    else if (p === 'me/sign-in') data = {}
    else if (p === 'me/notifications/pulse') data = { stamp: '0' }
    else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }
    else if (p === 'workspaces') data = []
    else if (p === 'views') data = viewList()
    else if (p === 'reference') data = referenceData()
    else if (p === 'projects') data = { items: [project], page: 1, pageSize: 200, totalCount: 1 }
    else if (p === 'users') data = [{ id: jill, displayName: 'Jill Partial', isActive: true }]
    else if (p === 'planning/grid') { capacityCalls.push(Object.fromEntries(u.searchParams)); data = planningGrid({ weeks: ['2026-10-12'], people: [planningPerson({ id: jill, displayName: 'Jill Partial', cells: [planningCell({ week: '2026-10-12', remaining: 12, timeAway: 8 })] })] }) }
    else if (p === `tasks/${taskId}` && route.request().method() === 'PATCH') {
      assignmentCalls.push({ body: route.request().postDataJSON(), version: route.request().headers()['if-match'] });
      if (assignmentConflict) { assignmentConflict = false; status = 409; data = { code: 'concurrency_conflict', detail: 'Synthetic stale task version' } }
      else { assigned = true; data = { id: taskId, rowVersion: 8 } }
    }
    else if (p === `workload/${alex}/tasks`) { taskDrillCalls.push(Object.fromEntries(u.searchParams)); data = personTasks(weeks, project) }
    else if (p === `users/${alex}/capacity` && route.request().method() === 'PUT') { const body = route.request().postDataJSON(); typedCapacity = { body, headers: route.request().headers() }; data = capacitySet(alex, body.hours) }
    else if (p === `users/${alex}/availability`) { availabilityReads.push(Object.fromEntries(u.searchParams)); data = [dayAvailability({ workDate: u.searchParams.get('from') ?? '', rowVersion: 3 })] }
    else if (p.startsWith(`users/${alex}/availability/`) && route.request().method() === 'PUT') { const body = route.request().postDataJSON(); typedAvailability = { path: p, body, headers: route.request().headers() }; data = dayAvailability({ workDate: p.split('/').pop() ?? '', rowVersion: 4, availableHours: body.availableHours, category: body.category }) }
    else if (p === 'workload') [status, data] = forbidden ? [403, { code: 'forbidden', detail: 'You cannot open the workload view.' }] : [200, grid(u.searchParams.get('sort'))]
    else if (p === 'projects/P-DEMO' || p === `projects/${pid}`) data = project
    else if (p.endsWith('/date-review')) data = { window: null, tasks: [], deliverables: [] }
    else if (p.endsWith('/follow')) data = { level: 'My items only', source: 'Assignment' }
    else if (p === `projects/${pid}/changes/options`) data = coordinationOptions({ actorId: sam, canWrite: true, people: [{ id: jill, displayName: 'Jill Partial' }] })
    else if (p === `projects/${pid}/open-roles`) data = openRoles
    else if (p === `open-roles/${contractExample.id}` && route.request().method() === 'PATCH') { typedPatchHeaders = route.request().headers(); data = { ...contractExample, rowVersion: contractExample.rowVersion + 1 } }
    else if (p === `projects/${pid}/allocations`) data = allocationPage([allocation(proposed, 'Proposed'), allocation(confirmed, 'Confirmed')])
    else if (p === `projects/${pid}/allocations/${proposed}/confirmation-preview`) [status, data] = [403, { code: 'forbidden', detail: 'You cannot open the workload view.' }]
    else if (p === `projects/${pid}/allocations/review-options`) data = [] // the proposal form's reviewer list (an array, as the server returns)
    else if (p === `projects/${pid}/allocations/${confirmed}/cancel` && route.request().method() === 'POST') { typedClose = { body: route.request().postDataJSON(), headers: route.request().headers() }; data = { id: confirmed, rowVersion: 1 } }
    else if (p.startsWith(`projects/${pid}/allocations/`)) {
      const aid = p.split('/').pop()
      data = allocationDetail({ ...allocation(aid, aid === proposed ? 'Proposed' : 'Confirmed'), projectId: pid,
        confirmedAt: aid === confirmed ? '2026-10-02T12:00:00Z' : null, canConfirm: aid === proposed, canManage: manageAllocations })
    } else unknown.push(p)
    await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
  });
  const axe = async () => { await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') }); return (await page.evaluate(() => axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } }))).violations.map(v => `${v.id}: ${v.nodes[0].target}`) }
  const row = name => page.getByRole('row').filter({ has: page.getByRole('rowheader').filter({ hasText: name }) })

  // Partial scope: a single notice, negative remaining keeps "−", positive remaining is not claimed, proposed stays apart.
  await page.goto(`${base}/workload`);
  const grid1 = page.getByRole('region', { name: 'Workload by person and week' }); await grid1.waitFor();
  await page.getByRole('status').filter({ hasText: 'Partial view' }).getByRole('button', { name: 'About this page' }).click();
  await page.getByText('Totals include only the projects you can see.').waitFor();
  await page.keyboard.press('Escape');
  const alexRow = await row('Alex Overloaded').innerText(), jillRow = await row('Jill Partial').innerText();
  assert.match(alexRow, /Remaining −12 h/, 'Negative remaining keeps a true minus sign');
  assert.match(alexRow, /\+20 h proposed · not counted/, 'Proposed requests are labelled and kept out of load');
  assert.match(alexRow, /Over-assigned · \S/, 'Over-assigned names the server overWeek (AC-FIG-07)'); assert.match(jillRow, /2 unestimated/, 'Unestimated work sits beside the hours');
  assert.doesNotMatch(jillRow, /Remaining 20 h/, 'A partial view never claims spare capacity');
  // AC-FIG-05: every fixture cell keeps the shade the client rule gave it before bands came from the server.
  for (const p of grid().people) {
    const shown = await row(p.displayName).locator('[data-band]').evaluateAll(els => els.map(e => e.className))
    assert.equal(shown.length, p.cells.length)
    p.cells.forEach((c, i) => { const want = shadeBefore(c.pct); assert.ok(want ? shown[i].split(' ').includes(want) : !/bg-(bad|warn|ok)-bg/.test(shown[i]), `${p.displayName} week ${i} keeps its shade`) })
  }
  // FR-FIG-06: the legend in the Method popover names the thresholds the server returned.
  await page.getByRole('button', { name: 'How this is calculated' }).click();
  const legend = await page.getByRole('list', { name: 'How to read a week' }).innerText();
  assert.match(legend, /amber to 110 % load/); assert.match(legend, /light below 40 % load, calm to 90 %, amber to 110 %, red above/);
  await page.keyboard.press('Escape'); await page.getByRole('list', { name: 'How to read a week' }).waitFor({ state: 'detached' });
  const accent = await row('Alex Overloaded').locator('[data-accent]').first().getAttribute('data-accent');
  const desktopViolations = await axe();

  // A re-sort keeps each person's accent (it is derived from the id, never the row index).
  await page.getByRole('button', { name: /^Sort:/ }).click();
  await page.getByRole('menuitemradio', { name: 'Name' }).click();
  await page.waitForFunction(() => document.querySelector('tbody th[scope=row]')?.textContent.includes('Jill Partial'));
  assert.equal(await row('Alex Overloaded').locator('[data-accent]').first().getAttribute('data-accent'), accent, 'Stable identity accent');

  // Complete scope shows remaining capacity; no partial notice.
  partial = false; await page.reload(); await grid1.waitFor();
  assert.match(await row('Jill Partial').innerText(), /Remaining 20 h/);
  assert.equal(await page.getByRole('status').filter({ hasText: 'Partial view' }).count(), 0);
  // flag.figure_names off: today's "Remaining", no basis line, no grid description, no "at most" wording.
  assert.equal(await page.getByText('Uncommitted', { exact: false }).count(), 0, 'No Uncommitted wording with the flag off');
  assert.equal(await grid1.getAttribute('aria-describedby'), null);
  assert.doesNotMatch(await grid1.innerText(), /at most/);

  // AC-FIG-03, AC-FIG-04 (flag on): "Uncommitted N h" from the server value, no cell says "Remaining", the grid's accessible
  // description is the basis line, and the Method popover's committed-load step names Uncommitted.
  figureNames = true; partial = true; await page.reload(); await grid1.waitFor();
  const named = { alex: await row('Alex Overloaded').innerText(), jill: await row('Jill Partial').innerText() };
  assert.match(named.alex, /Uncommitted −12 h/); assert.doesNotMatch(named.jill, /Uncommitted 20 h/, 'A partial row still withholds spare capacity');
  assert.equal(await grid1.locator('[data-band]').filter({ hasText: 'Remaining' }).count(), 0, 'No Workload cell says Remaining');
  const basis = 'Uncommitted = available − committed load (confirmed reservations and forecast task work). Planning entries are not counted.';
  assert.equal(await page.locator(`[id="${await grid1.getAttribute('aria-describedby')}"]`).textContent(), basis);
  await page.getByRole('button', { name: 'How this is calculated' }).click();
  assert.match(await page.getByRole('dialog').innerText(), /Uncommitted is available capacity minus committed load/);
  await page.keyboard.press('Escape'); await page.getByRole('list', { name: 'How to read a week' }).waitFor({ state: 'detached' });
  const namedViolations = await axe();
  partial = false; await page.reload(); await grid1.waitFor();
  assert.match(await row('Jill Partial').innerText(), /Uncommitted 20 h/);
  figureNames = false;

  // Packet047 AC-PRO-13: dark payload, task-week picker, row version and conflict feedback.
  assert.equal(await page.getByRole('region', { name: 'Unassigned tasks' }).count(), 0);
  unassignedOn = true; await page.reload(); await grid1.waitFor();
  const unassigned = page.getByRole('region', { name: 'Unassigned tasks' }); await unassigned.waitFor();
  assert.equal(await unassigned.getByRole('link', { name: 'Show all 51' }).getAttribute('href'), `/tasks?unassigned=true&projects=${pid}`);
  // AC-FIG-04: the person picker's free figure. Flag off: today's figure with no tooltip or description; flag on: both are the
  // remaining-capacity basis, and the open picker passes axe.
  const pickerFigure = page.getByRole('option', { name: /Jill Partial/ }).locator('[data-capacity]');
  const openPicker = async () => {
    await unassigned.getByRole('button', { name: 'Assign P-DEMO-T0090' }).click(); await page.getByText('12 h free · 8 h away').waitFor();
    // axe reads settled colours, so wait for the popover's entry animation.
    await page.evaluate(() => Promise.all(document.getAnimations().filter(a => a.effect?.getTiming().iterations !== Infinity).map(a => a.finished.catch(() => undefined))));
  };
  await openPicker();
  assert.equal(await pickerFigure.getAttribute('title'), null); assert.equal(await pickerFigure.getAttribute('aria-describedby'), null);
  await page.keyboard.press('Escape');
  figureNames = true; await page.reload(); await unassigned.waitFor(); await openPicker();
  const freeBasis = 'Remaining capacity in the Weekly Planner: capacity − Confirmed − Expected';
  assert.equal(await pickerFigure.getAttribute('title'), freeBasis);
  assert.equal(await page.locator(`[id="${await pickerFigure.getAttribute('aria-describedby')}"]`).textContent(), freeBasis);
  const pickerViolations = await axe();
  await page.keyboard.press('Escape'); figureNames = false; await page.reload(); await unassigned.waitFor();
  await unassigned.getByRole('button', { name: 'Assign P-DEMO-T0090' }).click();
  await page.getByRole('option', { name: /Jill Partial/ }).waitFor();
  await page.getByText('12 h free · 8 h away').waitFor();
  assert.equal(capacityCalls.at(-1).from, '2026-10-12');
  await page.getByRole('option', { name: /Jill Partial/ }).click();
  await unassigned.getByRole('alert').filter({ hasText: 'Synthetic stale task version' }).waitFor();
  assert.equal(assignmentCalls[0].body.assigneeId, jill); assert.equal(assignmentCalls[0].version, '"7"');
  await unassigned.getByRole('button', { name: 'Assign P-DEMO-T0090' }).click();
  await page.getByRole('option', { name: /Jill Partial/ }).click();
  await unassigned.waitFor({ state: 'detached' }); assert.equal(assignmentCalls.length, 2);
  unassignedOn = false;

  // S15 Workload: the expand row and the capacity dialog call the typed helper; the mock answers with contract-typed fixtures.
  await page.getByRole('button', { name: "Show Alex Overloaded's tasks" }).click();
  await page.getByText('Synthetic dated task').waitFor(); await page.getByText('Synthetic open task').waitFor();
  assert.deepEqual(taskDrillCalls, [{}], 'The expand row asks for the current weeks: no from parameter');
  const drillRow = (name) => page.getByRole('row').filter({ hasText: name });
  assert.match(await drillRow('Synthetic dated task').innerText(), /Due 2026-10-09[\s\S]*Est\. 16 h[\s\S]*Left 12 h/, 'Dated task shows its due date, estimate and remaining hours');
  assert.match(await drillRow('Synthetic open task').innerText(), /No due date/, 'A null due date and estimate render as missing, not as a crash');
  await page.getByRole('button', { name: "Set capacity for Alex Overloaded" }).click();
  await page.getByLabel('Hours per week').fill('32'); await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'detached' });
  assert.deepEqual(typedCapacity.body, { hours: 32 }); assert.equal(typedCapacity.headers['x-hub-source'], 'UI');
  await page.getByRole('button', { name: "Set capacity for Alex Overloaded" }).click();
  await page.getByLabel('Hours per week').fill(''); await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'detached' });
  assert.deepEqual(typedCapacity.body, { hours: null }, 'An empty field clears the override');
  // S15 Allocations: the daily availability dialog reads and writes the day through the typed helper, on the version it read.
  await page.getByRole('button', { name: 'Set daily availability for Alex Overloaded' }).click();
  await page.getByText('Currently 4 h (Reduced)').waitFor();
  const day = await page.getByLabel('Date').inputValue(); assert.deepEqual(availabilityReads.at(-1), { from: day, through: day }, 'The read asks for the one chosen day');
  await page.getByLabel('Available hours').fill('6'); await page.getByLabel('Category').selectOption('Additional'); await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'detached' });
  assert.equal(typedAvailability.path, `users/${alex}/availability/${day}`); assert.deepEqual(typedAvailability.body, { expectedRowVersion: 3, availableHours: 6, category: 'Additional' });
  assert.equal(typedAvailability.headers['x-hub-source'], 'UI');

  // Denied access explains itself and offers no export or saved views.
  forbidden = true; await page.reload();
  await page.getByText('Resources is not part of your access').waitFor();
  assert.equal(await page.getByRole('main').getByRole('button', { name: 'Export' }).count(), 0, 'No export without access');

  // Phones get the desktop/tablet notice instead of the grid (§13.0 Responsive).
  forbidden = false; await page.setViewportSize({ width: 375, height: 812 }); await page.reload();
  await page.getByText('This screen is available on tablet and desktop.').first().waitFor();
  assert.equal(await page.getByRole('region', { name: 'Workload by person and week' }).count(), 0);
  await page.setViewportSize({ width: 1440, height: 1000 });

  // Allocations: approval status is labelled as such; view-only and refused-preview states explain the next step.
  await page.goto(`${base}/projects/P-DEMO/allocations`);
  await page.getByRole('columnheader', { name: 'Approval status' }).waitFor();
  await page.goto(`${base}/projects/P-DEMO/allocations?allocation=${confirmed}`);
  await page.getByRole('dialog').getByText('View only').waitFor();
  await page.goto(`${base}/projects/P-DEMO/allocations?allocation=${proposed}`);
  await page.getByRole('dialog').getByRole('button', { name: 'Review capacity' }).click();
  await page.getByRole('dialog').getByText('Capacity can’t be checked with your access').waitFor();
  assert.equal(await page.getByText('You cannot open the workload view.').count(), 0, 'The refusal is explained in context');
  const panelViolations = await axe();
  // S15 Allocations: a close command goes through the typed helper with a request id, the row version the panel read and the reason.
  manageAllocations = true;
  await page.goto(`${base}/projects/P-DEMO/allocations?allocation=${confirmed}`);
  await page.getByRole('button', { name: 'Cancel allocation' }).click();
  const closing = page.getByRole('dialog', { name: 'Cancel allocation' }); await closing.getByLabel('Reason').fill('Synthetic reason for closing');
  await closing.getByRole('button', { name: 'Cancel allocation' }).click(); await closing.waitFor({ state: 'detached' });
  assert.deepEqual({ ...typedClose.body, requestId: undefined }, { requestId: undefined, rowVersion: 0, reason: 'Synthetic reason for closing' }); assert.match(typedClose.body.requestId, /^[0-9a-f-]{36}$/);
  assert.equal(typedClose.headers['x-hub-source'], 'UI'); manageAllocations = false;

  // FR-FIG-11, FR-FIG-12 (AC-FIG-10, AC-FIG-11): the role form asks for a reason exactly when the server's needsReason says so,
  // and takes its label limit from GET /reference.
  const roleForm = async () => { await page.goto(`${base}/projects/P-DEMO/allocations?view=roles`); await page.getByRole('button', { name: 'Add open role' }).first().click(); return page.getByRole('dialog') }
  project.permissions.isPm = true; project.permissions.proposeAllocation = { ok: true }; project.permissions.needsReason = true; project.status = 'Complete';
  let dialog = await roleForm();
  await page.waitForFunction(() => document.querySelector('#role-label')?.getAttribute('maxlength') === '80');
  assert.equal(await dialog.getByLabel('Role').getAttribute('maxlength'), '80', 'Label limit from /reference');
  await dialog.getByLabel('Reason').waitFor();
  project.permissions.needsReason = false;
  dialog = await roleForm(); await dialog.getByLabel('Role').waitFor();
  assert.equal(await dialog.getByLabel('Reason').count(), 0, 'No reason field without needsReason, whatever the status');

  // AC-PLAT-13: generated PATCH helpers preserve shared transport headers.
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  openRoles.push({ ...contractExample, projectId: pid, roleLabel: 'Synthetic typed role' });
  await page.goto(`${base}/projects/P-DEMO/allocations?tab=roles`);
  await page.getByRole('button', { name: 'More actions for Synthetic typed role' }).click();
  await page.getByRole('menuitem', { name: 'Edit', exact: true }).click();
  const editRole = page.getByRole('dialog');
  await editRole.getByLabel('Role', { exact: true }).fill('Synthetic typed role edited');
  await editRole.getByRole('button', { name: 'Save', exact: true }).click();
  await editRole.waitFor({ state: 'hidden' });
  assert.equal(typedPatchHeaders?.['if-match'], `"${contractExample.rowVersion}"`);
  assert.equal(typedPatchHeaders?.['x-hub-source'], 'UI');
  // A Workload Fill link (?fill=) opens the proposal once; Cancel closes it and drops the parameter so it does not reopen.
  await page.goto(`${base}/projects/P-DEMO/allocations?tab=roles&fill=${contractExample.id}`);
  const fillDialog = page.getByRole('dialog'); await fillDialog.waitFor();
  await fillDialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await fillDialog.waitFor({ state: 'hidden' }); await page.waitForTimeout(300);
  assert.equal(await page.getByRole('dialog').count(), 0, 'a cancelled fill dialog stays closed');
  assert.equal(new URL(page.url()).searchParams.get('fill'), null, 'Cancel removes the fill parameter');
  openRoles.length = 0;

  // FR-FIG-09 (D5): a control today's client offered the PM but the server refuses stays visible, disabled, with the server's reason.
  project.status = 'Archived'; project.permissions.proposeAllocation = { ok: false, reason: 'This project is Archived and read-only.' };
  await page.goto(`${base}/projects/P-DEMO/allocations`);
  const refusedPropose = page.getByRole('main').getByRole('button', { name: 'Propose allocation' }).first();
  await refusedPropose.waitFor();
  assert.ok(await refusedPropose.isDisabled(), 'Refused proposal is disabled');
  assert.equal(await refusedPropose.getAttribute('title'), 'This project is Archived and read-only.', 'Refused proposal carries the server reason');


  const report = { scope: 'Chromium UI with mocked API; server rules are tested separately', errors, unmockedGets: [...new Set(unknown)], desktopViolations, namedViolations, pickerViolations, panelViolations };
  console.log(JSON.stringify(report));
  assert.deepEqual(errors, []); assert.deepEqual(report.unmockedGets, [], 'Every API call is mocked');
  assert.deepEqual(desktopViolations, [], 'Workload accessibility'); assert.deepEqual(namedViolations, [], 'Workload accessibility with flag.figure_names on'); assert.deepEqual(pickerViolations, [], 'Person picker accessibility with flag.figure_names on'); assert.deepEqual(panelViolations, [], 'Allocation panel accessibility');
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() });
