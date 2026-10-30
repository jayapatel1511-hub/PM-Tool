const { chromium } = require('playwright');
const { clientConfig } = require('./config.fixture.cjs');
const { viewList } = require('./views.fixture.cjs');
const { meView } = require('./me.fixture.cjs');
const { spawn } = require('child_process');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const { pulse, unreadCount, preferences } = require('./notifications.fixture.cjs')

const port = process.env.PORT ?? '5189';
const origin = `http://127.0.0.1:${port}`;
const output = path.resolve(__dirname, '../test-results/social-sign-in');
let server, browser;

(async () => {
  fs.mkdirSync(output, { recursive: true });
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'ignore' });
  for (let n = 0; n < 100; n++) {
    try { if ((await fetch(origin)).ok) break; } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  let providers = ['Microsoft', 'Google'];
  let localPasswordEnabled = false;
  let signedIn = false;
  const linkRequests = [];
  const localRequests = [];
  const logoutRequests = [];
  await page.route('**/api/**', async route => {
    const pathname = new URL(route.request().url()).pathname;
    if (pathname === '/api/v1/config') return route.fulfill({ json: clientConfig({ authMode: 'Social', localPasswordEnabled, providers }) });
    if (pathname === '/api/v1/me') return signedIn ? route.fulfill({ json: meView({
      id: '00000000-0000-0000-0000-000000000001', displayName: 'Synthetic Person', email: 'person@outside.example', settings: { today: '2026-10-14' }, preferences: { denseRows: false },
    }) }) : route.fulfill({ status: 401, json: { detail: 'Signed out' } });
    if (pathname.startsWith('/api/v1/auth/social/sign-in/')) return route.fulfill({ contentType: 'text/html', body: '<h1>Provider redirect reached</h1>' });
    if (pathname === '/api/v1/auth/social/sign-out') { logoutRequests.push(pathname); signedIn = false; return route.fulfill({ status: 204 }); }
    if (pathname === '/api/v1/auth/local/sign-in') {
      const body = route.request().postDataJSON();
      localRequests.push(body);
      if (body.password !== 'synthetic-password') return route.fulfill({ status: 401, json: { detail: 'Invalid credentials' } });
      signedIn = true;
      return route.fulfill({ status: 204 });
    }
    if (pathname === '/api/v1/auth/local/sign-out') { signedIn = false; return route.fulfill({ status: 204 }); }
    if (pathname === '/api/v1/auth/social/providers') return route.fulfill({ json: { available: providers, connected: ['Google'] } });
    if (pathname.startsWith('/api/v1/auth/social/link/')) {
      linkRequests.push({ pathname, method: route.request().method() });
      return route.fulfill({ contentType: 'text/html', body: '<h1>Connect provider redirect reached</h1>' });
    }
    if (pathname === '/api/v1/me/preferences') return route.fulfill({ json: preferences({ digest: { holdDigestWhenAway: true } }) });
    if (pathname === '/api/v1/me/sign-in') return route.fulfill({ status: 204 });
    if (pathname === '/api/v1/client-errors') return route.fulfill({ status: 204 });
    if (pathname === '/api/v1/me/notifications/pulse') return route.fulfill({ json: pulse('0') });
    if (pathname === '/api/v1/me/notifications/unread-count') return route.fulfill({ json: unreadCount(0, 0) });
    if (pathname === '/api/v1/workspaces') return route.fulfill({ json: [] });
    if (pathname === '/api/v1/views') return route.fulfill({ json: viewList() });
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
  providers = ['Microsoft', 'Google'];
  localPasswordEnabled = true;
  await page.goto(`${origin}/preferences`);
  await page.getByLabel('User ID', { exact: true }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Continue with Google', exact: true }).count(), 1);
  await page.getByLabel('User ID', { exact: true }).fill('existing.tester');
  await page.getByLabel('Password', { exact: true }).fill('wrong synthetic password');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'User ID or password is incorrect.' }).waitFor();
  await page.getByLabel('Password', { exact: true }).fill('synthetic-password');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.waitForURL('**/preferences');
  await page.getByRole('button', { name: 'Account', exact: true }).click();
  await page.getByRole('menuitem', { name: 'Sign out', exact: true }).click();
  await page.waitForURL('**/');
  assert.deepEqual(logoutRequests, ['/api/v1/auth/social/sign-out']);
  assert.deepEqual(localRequests, [
    { userName: 'existing.tester', password: 'wrong synthetic password' },
    { userName: 'existing.tester', password: 'synthetic-password' },
  ]);
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
  console.log(JSON.stringify({ scope: 'Built SPA with synthetic API responses; provider callbacks covered separately by PostgreSQL integration tests', checks: ['both provider buttons navigate', 'password form absent', 'hybrid local-password form and wrong/success sign-in', 'account-menu Social logout POST', 'protected-route return preserved', 'conflict error', 'phone overflow', 'configured providers only', 'missing-provider state', 'second-provider connect uses POST', 'connected provider disabled', 'connection feedback', 'desktop and account-method WCAG AA'], errors, accessibilityViolations: desktop.violations.length + methods.violations.length }));
})().catch(e => { console.error(e); process.exitCode = 1; }).finally(async () => { await browser?.close(); server?.kill(); });
