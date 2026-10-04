// Resources workflow UI checks with mocked API responses (run after `npm run build`); API tests cover server rules.
// Tuesday visual batch 1: a partial view never claims spare capacity (FR-CAP-06), proposed requests stay outside load
// (FR-CAP-04), negative remaining keeps its minus sign, person accents survive a re-sort, denied access, phones and a
// refused capacity preview each get an explanation, and the screens pass axe.
const { chromium } = require('playwright');
const path = require('path'), { spawn } = require('child_process'), assert = require('assert/strict');
const port = process.env.PORT ?? '5177', base = `http://127.0.0.1:${port}`;
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`;
const sam = id(1), alex = id(2), jill = id(3), pid = id(10), proposed = id(20), confirmed = id(21);
const weeks = ['2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26', '2026-11-02', '2026-11-09', '2026-11-16'];
const cells = (set) => weeks.map((week, i) => ({ week, hours: 0, pct: 0, available: 40, confirmed: 0, proposed: 0, committed: 0, ...set[i] }));
const person = (pid_, name, extra) => ({ id: pid_, displayName: name, supervisorId: sam, supervisorName: 'Sam Supervisor', capacity: 40, capacityOverride: false, noDueDate: 0,
  openTasks: 2, unestimated: 0, overdue: 0, projects: 1, overAssigned: false, underAssigned: false, cluster: false, canSetCapacity: true, indicator: 'ok', ...extra });
let partial = true, forbidden = false;
const grid = (sort) => {
  const people = [
    person(alex, 'Alex Overloaded', { overAssigned: true, indicator: 'over', partialScope: partial,
      cells: cells([{ committed: 52, pct: 130, hours: 52 }, { committed: 50, available: 32, pct: 156, confirmed: 30, hours: 44, proposed: 20 }]) }),
    person(jill, 'Jill Partial', { partialScope: partial, unestimated: 2, cells: cells([{ committed: 20, pct: 50, hours: 20 }]) }),
  ]
  return { weeks, today: '2026-10-03', currentWeek: weeks[0], defaultCapacity: 40, people: sort === 'name' ? people.reverse() : people }
}
const me = { id: sam, displayName: 'Sam Supervisor', email: 'sam@hub.test', roles: [], systemRoles: ['Supervisor'],
  capabilities: { createProject: false, createTask: true, portfolio: false, workload: true, staff: true, allStaff: false, admin: false, templates: false, readOnly: false, directReports: 2 },
  settings: { today: '2026-10-03', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8 }, preferences: { denseRows: true, digestEnabled: true } };
const project = { id: pid, projectNumber: 'P-DEMO', name: 'Synthetic resource checks', client: 'Pilot client', pmName: 'Pat PM', status: 'Active', visibility: 'Open', rowVersion: 1,
  links: [], disciplines: [], starred: false, myRoles: [], health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] },
  permissions: { edit: { ok: false }, healthOverride: { ok: false }, raiseRegister: { ok: false }, isPm: false, leadOf: [], createTaskIn: [], createDeliverableIn: [], transitions: [] } };
const allocation = (aid, status) => ({ id: aid, personId: jill, personName: 'Jill Partial', purpose: 'Production', fromDate: '2026-10-05', throughDate: '2026-10-09', plannedHours: 16, status, rowVersion: 0, personState: 'Eligible' });
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
    if (p === 'config') data = { authMode: 'Development', entra: {} }
    else if (p === 'me') data = me
    else if (p === 'me/sign-in') data = {}
    else if (p === 'me/notifications/pulse') data = { stamp: '0' }
    else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }
    else if (p === 'workspaces') data = []
    else if (p === 'views') data = { views: [], canShare: false }
    else if (p === 'reference') data = { disciplines: [], clients: [], offices: [], deliverableTypes: [], phases: [], projectTypes: [] }
    else if (p === 'projects') data = { items: [project], page: 1, pageSize: 200, totalCount: 1 }
    else if (p === 'workload') [status, data] = forbidden ? [403, { code: 'forbidden', detail: 'You cannot open the workload view.' }] : [200, grid(u.searchParams.get('sort'))]
    else if (p === 'projects/P-DEMO' || p === `projects/${pid}`) data = project
    else if (p.endsWith('/date-review')) data = { window: null, tasks: [], deliverables: [] }
    else if (p.endsWith('/follow')) data = { level: 'My items only', source: 'Assignment' }
    else if (p === `projects/${pid}/changes/options`) data = { actorId: sam, canWrite: true, manageDisciplineIds: [], people: [{ id: jill, displayName: 'Jill Partial' }], disciplines: [], sources: [], heads: [], deliverables: [], tasks: [] }
    else if (p === `projects/${pid}/allocations`) data = { items: [allocation(proposed, 'Proposed'), allocation(confirmed, 'Confirmed')], page: 1, pageSize: 50, totalCount: 2 }
    else if (p === `projects/${pid}/allocations/${proposed}/confirmation-preview`) [status, data] = [403, { code: 'forbidden', detail: 'You cannot open the workload view.' }]
    else if (p.startsWith(`projects/${pid}/allocations/`)) {
      const aid = p.split('/').pop()
      data = { ...allocation(aid, aid === proposed ? 'Proposed' : 'Confirmed'), personEligible: true, links: [], days: [], overCapacityWarningRecorded: false,
        confirmedAt: aid === confirmed ? '2026-10-02T12:00:00Z' : undefined, canConfirm: aid === proposed, canManage: false }
    } else unknown.push(p)
    await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
  });
  const axe = async () => { await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') }); return (await page.evaluate(() => axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } }))).violations.map(v => `${v.id}: ${v.nodes[0].target}`) }
  const row = name => page.getByRole('row').filter({ has: page.getByRole('rowheader').filter({ hasText: name }) })

  // Partial scope: a single notice, negative remaining keeps "−", positive remaining is not claimed, proposed stays apart.
  await page.goto(`${base}/workload`);
  const grid1 = page.getByRole('region', { name: 'Workload by person and week' }); await grid1.waitFor();
  await page.getByText('Totals include only the projects you can see.').waitFor();
  const alexRow = await row('Alex Overloaded').innerText(), jillRow = await row('Jill Partial').innerText();
  assert.match(alexRow, /Remaining −12 h/, 'Negative remaining keeps a true minus sign');
  assert.match(alexRow, /\+20 h proposed · not counted/, 'Proposed requests are labelled and kept out of load');
  assert.match(alexRow, /Over-assigned/); assert.match(jillRow, /2 unestimated/, 'Unestimated work sits beside the hours');
  assert.doesNotMatch(jillRow, /Remaining 20 h/, 'A partial view never claims spare capacity');
  const accent = await row('Alex Overloaded').locator('[data-accent]').first().getAttribute('data-accent');
  const desktopViolations = await axe();

  // A re-sort keeps each person's accent (it is derived from the id, never the row index).
  await page.locator('#wl-sort').selectOption('name');
  await page.waitForFunction(() => document.querySelector('tbody th[scope=row]')?.textContent.includes('Jill Partial'));
  assert.equal(await row('Alex Overloaded').locator('[data-accent]').first().getAttribute('data-accent'), accent, 'Stable identity accent');

  // Complete scope shows remaining capacity; no partial notice.
  partial = false; await page.reload(); await grid1.waitFor();
  assert.match(await row('Jill Partial').innerText(), /Remaining 20 h/);
  assert.equal(await page.getByText('Totals include only the projects you can see.').count(), 0);

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

  const report = { scope: 'Chromium UI with mocked API; server rules are tested separately', errors, unmockedGets: [...new Set(unknown)], desktopViolations, panelViolations };
  console.log(JSON.stringify(report));
  assert.deepEqual(errors, []); assert.deepEqual(report.unmockedGets, [], 'Every API call is mocked');
  assert.deepEqual(desktopViolations, [], 'Workload accessibility'); assert.deepEqual(panelViolations, [], 'Allocation panel accessibility');
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() });
