// AC-OPS-07, AC-OPS-08, AC-OPS-09, AC-OPS-18, AC-OPS-19, AC-OPS-21: actual client error logic plus the mocked Operations UI; no live reports.
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path');
const { spawn } = require('node:child_process'), { chromium } = require('playwright'), ts = require('typescript');
const { clientConfig } = require('./config.fixture.cjs');
const { meView } = require('./me.fixture.cjs');
const { operationsRead, opsDeadLetter } = require('./admin.fixture.cjs');
const root = path.resolve(__dirname, '..'), port = process.env.PORT ?? '19501', base = `http://127.0.0.1:${port}`;
const cache = new Map();
function sourceModule(file) {
  if (cache.has(file)) return cache.get(file);
  const module = { exports: {} }; cache.set(file, module.exports);
  const source = ts.transpileModule(fs.readFileSync(file, 'utf8').replaceAll('import.meta.env.VITE_HUB_BUILD', '"dev"'), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  new Function('require', 'module', 'exports', source)(name => name === 'sonner' ? { toast: { warning() {} } } : sourceModule(path.join(root, 'src', name.replace('@/', '') + '.ts')), module, module.exports);
  return module.exports;
}
let server, browser;
(async () => {
  const { ApiError, api, shouldRetryRead } = sourceModule(path.join(root, 'src/lib/api.ts'));
  for (const status of [502, 503, 504]) for (const method of ['GET', 'POST']) {
    const error = new ApiError(status, {}, method);
    assert.match(error.message, method === 'GET' ? /Try again in a moment/ : status === 504 ? /may not have been saved/ : /was not saved/);
    assert.equal(new ApiError(status, { detail: 'Specific detail', traceId: 'TRACE' }, method).message, 'Specific detail (ref TRACE)');
  }
  assert.equal(new ApiError(409, { detail: 'Conflict' }).message, 'Conflict');
  assert.equal(new ApiError(422, { detail: 'Invalid' }).message, 'Invalid');
  const originalFetch = global.fetch;
  try {
    global.fetch = async () => { throw new TypeError('synthetic offline') };
    await assert.rejects(api('synthetic', { method: 'POST' }), e => e.status === 0 && /Check before trying again/.test(e.message));
    await assert.rejects(api('synthetic'), e => e.status === 0 && shouldRetryRead(0, e) && shouldRetryRead(1, e) && !shouldRetryRead(2, e));
    assert.equal(shouldRetryRead(0, new ApiError(422, {})), false);
    const aborted = new DOMException('synthetic', 'AbortError'); global.fetch = async () => { throw aborted };
    await assert.rejects(api('synthetic'), e => e === aborted);
  } finally { global.fetch = originalFetch }
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: root, stdio: 'ignore' });
  for (let n = 0; n < 100; n++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'synthetic@hub.test'));
  const clientReports = [];
  let unread = 3, failed = true, renderError = false, retries = 0, countReads = 0, accountDenseRows = true, densitySaves = 0;
  async function mockApi(route) {
    const p = new URL(route.request().url()).pathname;
    let data = [];
    if (p.endsWith('/client-errors')) { clientReports.push(route.request().postDataJSON()); return route.fulfill({status: 204}); }
    if (p.endsWith('/config')) data = clientConfig();
    else if (p === '/api/dev/users') data = [];
    else if (p.endsWith('/me')) data = meView({ id: '00000000-0000-0000-0000-000000000001', displayName: 'Synthetic Admin', email: 'synthetic@hub.test', systemRoles: ['Admin'], capabilities: { admin: true }, preferences: { denseRows: accountDenseRows } });
    else if (p.endsWith('/preferences/density')) { densitySaves++; accountDenseRows = JSON.parse(route.request().postData() || '{}').denseRows; return route.fulfill({ status: 204 }); }
    else if (p.endsWith('/notifications/pulse')) data = { stamp: 'synthetic' };
    else if (p.endsWith('/notifications/unread-count')) { countReads++; data = { notifications: unread, following: 50 } }
    else if (p.endsWith('/outbox/retry')) { retries++; failed = false; data = { retried: 1 } }
    else if (p.endsWith('/admin/operations')) data = { ...operationsRead({ deadLetterCount: failed ? 1 : 0, deadLetters: failed ? [opsDeadLetter({ id: '00000000-0000-0000-0000-000000000002', eventType: 'SyntheticEvent', projectNumber: 'SYN-1', lastError: 'SyntheticException', deadLetteredAt: '2026-10-18T22:00:00Z' })] : [] }), ...(renderError && { jobs: {} }) }; // the render error is a deliberately malformed read
    else if (p.endsWith('/projects') || p.endsWith('/me/starred-projects')) data = { items: [], total: 0 };
    else if (p.endsWith('/me/sign-in')) return route.fulfill({ status: 204 });
    return route.fulfill({ json: data });
  }
  await page.route('**/api/**', mockApi);
  const unexpectedErrors = [];
  page.on('console', msg => { if (msg.type() === 'error') unexpectedErrors.push(`console: ${msg.text()}`) });
  page.on('pageerror', e => unexpectedErrors.push(`page: ${e.message}`));
  await page.goto(base + '/admin/operations');
  await page.getByRole('button', { name: 'Retry', exact: true }).waitFor({timeout: 8000}).catch(async e => { console.log(await page.locator('body').innerText()); throw e });
  assert.ok(await page.getByText('SyntheticException', { exact: false }).count());
  // The admin tabs registrations.tsx adds must reach the Admin tab bar (FR-LIVE-05); a shadowed ADMIN_EXTRA once hid both.
  for (const href of ['/admin/operations', '/admin/holidays']) assert.equal(await page.locator(`nav a[href="${href}"]`).count(), 1, `Admin tab bar links ${href}`);
  await page.waitForFunction(() => document.title === '(3) Tuesday');
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
  assert.deepEqual(await page.evaluate(async () => (await axe.run(document.querySelector('main'))).violations.map(v => v.id)), []);
  await page.getByRole('button', { name: 'Retry', exact: true }).click();
  await page.getByText('SyntheticEvent', { exact: true }).waitFor({ state: 'detached' });
  assert.equal(retries, 1);
  assert.equal(countReads, 1, 'tab title must reuse bell query');
  unread = 120; await page.reload(); await page.waitForFunction(() => document.title === '(99+) Tuesday');
  unread = 0; await page.reload(); await page.waitForFunction(() => document.title === 'Tuesday');
  await page.getByRole('button', { name: 'Account' }).click();
  await page.getByRole('menuitemradio', { name: 'Comfortable' }).click();
  assert.equal(accountDenseRows, false);
  assert.ok(densitySaves > 0, 'comfortable choice must save to account');
  const secondContext = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const second = await secondContext.newPage();
  await second.addInitScript(() => sessionStorage.setItem('hub.devUser', 'synthetic@hub.test'));
  await second.route('**/api/**', mockApi);
  await second.goto(base + '/admin/operations');
  await second.waitForFunction(() => document.documentElement.dataset.density === 'comfortable');
  await secondContext.close();
  const localContext = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const local = await localContext.newPage();
  await local.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'synthetic@hub.test'); localStorage.setItem('hub.density', 'compact') });
  await local.route('**/api/**', mockApi);
  await local.goto(base + '/admin/operations');
  await local.waitForFunction(() => document.documentElement.dataset.density === 'compact');
  await localContext.close();
  renderError = true;
  const renderReport = page.waitForResponse(response => response.url().endsWith('/client-errors'));
  await page.reload();
  await page.getByRole('heading', { name: 'Tuesday hit a snag' }).waitFor();
  const reference = await page.getByText(/^Reference:/).innerText();
  assert.match(reference, /admin\/operations.*[A-Z2-9]{6}$/);
  await renderReport;
  assert.equal(clientReports.length, 1, 'one render report for the shown reference');
  assert.equal(clientReports[0].Reference, reference.replace('Reference: ', ''));
  assert.equal(clientReports[0].Route, 'admin/operations');
  assert.equal(clientReports[0].Name, 'TypeError');
  await page.evaluate(() => {
    window.dispatchEvent(new ErrorEvent('error', { message: 'Synthetic "private name"', error: new Error('Synthetic "private name"') }));
    window.dispatchEvent(new PromiseRejectionEvent('unhandledrejection', { reason: new Error('Synthetic rejection'), promise: Promise.resolve() }));
    for (let i = 0; i < 7; i++) window.dispatchEvent(new ErrorEvent('error', { message: 'Distinct error '+i, error: new Error('Distinct error '+i) }));
  });
  await page.waitForTimeout(100);
  assert.equal(clientReports.length, 5, 'page report cap');
  assert.ok(!JSON.stringify(clientReports).includes('private name'));

  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
  assert.deepEqual(await page.evaluate(async () => (await axe.run()).violations.map(v => v.id)), []);
  await page.screenshot({ path: path.join(root, 'test-results', 'operations', 'error-page.png'), fullPage: true });
  const unexpected = unexpectedErrors.filter(error => !/TypeError: [\w$]+\.jobs\.map is not a function/.test(error));
  assert.deepEqual(unexpected, [], `unexpected browser errors: ${unexpected.join('; ')}`);
  console.log('PASS operations: error copy, trace refs, read retries, dead-letter retry, title counts and error fallback/axe, sanitized reports/cap and two-browser density');
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() });
