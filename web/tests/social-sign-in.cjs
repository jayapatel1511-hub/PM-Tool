const { chromium } = require('playwright');
const { spawn } = require('child_process');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');

const origin = 'http://127.0.0.1:5189';
const output = path.resolve(__dirname, '../test-results/social-sign-in');
let server, browser;

(async () => {
  fs.mkdirSync(output, { recursive: true });
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', '5189', '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'ignore' });
  for (let n = 0; n < 100; n++) {
    try { if ((await fetch(origin)).ok) break; } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  let providers = ['Microsoft', 'Google'];
  let signedIn = false;
  const linkRequests = [];
  await page.route('**/api/**', async route => {
    const pathname = new URL(route.request().url()).pathname;
    if (pathname === '/api/v1/config') return route.fulfill({ json: { authMode: 'Social', providers, entra: {} } });
    if (pathname === '/api/v1/me') return signedIn ? route.fulfill({ json: {
      id: '00000000-0000-0000-0000-000000000001', displayName: 'Synthetic Person', email: 'person@outside.example', roles: [], systemRoles: [],
      capabilities: { createProject: false, createTask: false, portfolio: false, workload: false, staff: false, allStaff: false, admin: false, templates: false, readOnly: false, directReports: 0 },
      settings: { today: '2026-10-14', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8 }, preferences: { denseRows: false, digestEnabled: true },
    } }) : route.fulfill({ status: 401, json: { detail: 'Signed out' } });
    if (pathname.startsWith('/api/v1/auth/social/sign-in/')) return route.fulfill({ contentType: 'text/html', body: '<h1>Provider redirect reached</h1>' });
    if (pathname === '/api/v1/auth/social/providers') return route.fulfill({ json: { available: providers, connected: ['Google'] } });
    if (pathname.startsWith('/api/v1/auth/social/link/')) {
      linkRequests.push({ pathname, method: route.request().method() });
      return route.fulfill({ contentType: 'text/html', body: '<h1>Connect provider redirect reached</h1>' });
    }
    if (pathname === '/api/v1/me/preferences') return route.fulfill({ json: { events: [], follows: [], digest: { digestEnabled: true, holdDigestWhenAway: true, time: '08:00', orgTime: '08:00', weekendDigests: false, sections: [], sectionsOff: [], weeklySummaryEnabled: false, managesProjects: false } } });
    if (pathname === '/api/v1/me/sign-in') return route.fulfill({ status: 204 });
    if (pathname === '/api/v1/me/notifications/pulse') return route.fulfill({ json: { stamp: '0' } });
    if (pathname === '/api/v1/me/notifications/unread-count') return route.fulfill({ json: { notifications: 0, following: 0 } });
    if (pathname === '/api/v1/workspaces') return route.fulfill({ json: [] });
    if (pathname === '/api/v1/views') return route.fulfill({ json: { views: [], canShare: false } });
    if (pathname === '/api/v1/projects') return route.fulfill({ json: { items: [], totalCount: 0 } });
    throw new Error(`Unexpected request: ${pathname}`);
  });
  await page.goto(`${origin}/projects/P-DEMO/tasks?view=mine`);
  await page.getByRole('button', { name: 'Continue with Google', exact: true }).waitFor();
  assert.equal(new URL(page.url()).pathname, '/login');
  assert.equal(await page.evaluate(() => sessionStorage.getItem('hub.returnTo')), '/projects/P-DEMO/tasks?view=mine');
  assert.equal(await page.getByLabel('User ID', { exact: true }).count(), 0);
  assert.equal(await page.locator('input[type=password]').count(), 0);
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
  const desktop = await page.evaluate(() => axe.run({ runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } }));
  assert.deepEqual(desktop.violations.map(v => v.id), []);
  await page.screenshot({ path: path.join(output, 'desktop.png'), fullPage: true });
  await page.getByRole('button', { name: 'Continue with Microsoft', exact: true }).click();
  await page.waitForURL('**/api/v1/auth/social/sign-in/Microsoft');
  await page.goto(`${origin}/login`);
  await page.getByRole('button', { name: 'Continue with Google', exact: true }).click();
  await page.waitForURL('**/api/v1/auth/social/sign-in/Google');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${origin}/login?authError=email_exists`);
  await page.getByRole('alert').filter({ hasText: 'This email already has a Tuesday profile' }).waitFor();
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
  await page.screenshot({ path: path.join(output, 'phone.png'), fullPage: true });
  providers = ['Google'];
  await page.goto(`${origin}/login`);
  await page.getByRole('button', { name: 'Continue with Google', exact: true }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Continue with Microsoft', exact: true }).count(), 0);
  providers = [];
  await page.goto(`${origin}/login`);
  await page.getByRole('alert').filter({ hasText: 'Sign-in is temporarily unavailable' }).waitFor();
  providers = ['Microsoft', 'Google']; signedIn = true;
  await page.goto(`${origin}/preferences?accountLinked=true`);
  await page.getByRole('button', { name: 'Connect Microsoft', exact: true }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Google connected', exact: true }).isDisabled(), true);
  await page.getByRole('status').filter({ hasText: 'Your sign-in account is connected' }).waitFor();
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
  const methods = await page.evaluate(() => axe.run(document.querySelector('#sign-in-methods'), { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } }));
  assert.deepEqual(methods.violations.map(v => v.id), []);
  await page.getByRole('button', { name: 'Connect Microsoft', exact: true }).click();
  await page.waitForURL('**/api/v1/auth/social/link/Microsoft');
  assert.deepEqual(linkRequests, [{ pathname: '/api/v1/auth/social/link/Microsoft', method: 'POST' }]);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ scope: 'Built SPA with synthetic API responses; provider callbacks covered separately by PostgreSQL integration tests', checks: ['both provider buttons navigate', 'password form absent', 'protected-route return preserved', 'conflict error', 'phone overflow', 'configured providers only', 'missing-provider state', 'second-provider connect uses POST', 'connected provider disabled', 'connection feedback', 'desktop and account-method WCAG AA'], errors, accessibilityViolations: desktop.violations.length + methods.violations.length }));
})().catch(e => { console.error(e); process.exitCode = 1; }).finally(async () => { await browser?.close(); server?.kill(); });
