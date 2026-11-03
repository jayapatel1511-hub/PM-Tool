// Root checkpoint14 census follow-up: 800 CSS px / DPR2, WCAG text spacing, full task names.
const assert = require('node:assert/strict')
const { chromium } = require('playwright')
const { mockPlatformApi, me, project, task } = require('./platform-fixtures.cjs')
const { taskRow } = require('./tasks.fixture.cjs')
const { dashboardView } = require('./dashboard.fixture.cjs')
const { attentionList } = require('./evaluation.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
;(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const page = await browser.newPage({ viewport: { width: 800, height: 450 }, deviceScaleFactor: 2, reducedMotion: 'reduce' }), errors = []
  const mock = mockPlatformApi(page)
  page.setDefaultTimeout(12000)
  page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (m.type() === 'error') errors.push(m.text()) })
  try {
    await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'alex@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    const tasks = [1, 2, 3, 4].map(i => taskRow({ ...task, id: `00000000-0000-0000-0009-${String(i).padStart(12, '0')}`, key: `T-${i}`, name: `Synthetic census task1.${i} — Verify existing utility crossing and the revised drainage long profile`, dueDate: '2026-11-02' }))
    await page.route(`**/api/v1/projects/${project.id}/dashboard*`, route => route.fulfill({ json: dashboardView({ dueThisWeek: { items: tasks.slice(0, 2), total: 2, link: '#' }, blocked: { items: tasks.slice(2), total: 2, link: '#' } }) }))
    await page.route(`**/api/v1/projects/${project.id}/attention*`, route => route.fulfill({ json: attentionList([]) }))
    const spacing = async () => page.addStyleTag({ content: '* { line-height:1.5 !important; letter-spacing:0.12em !important; word-spacing:0.16em !important; } p { margin-bottom:2em !important; }' })
    me.flags = { ...me.flags, visual_system: false }
    await page.goto(`${base}/projects/${project.projectNumber}/dashboard`); await spacing()
    await page.getByRole('button', { name: tasks[0].name, exact: true }).waitFor()
    assert.equal(await page.getByRole('button', { name: tasks[0].name, exact: true }).evaluate(e => getComputedStyle(e).whiteSpace), 'nowrap', 'legacy truncation layout stays unchanged')
    me.flags.visual_system = true; await page.reload(); await spacing()
    for (const task of tasks) {
      const button = page.getByRole('button', { name: task.name, exact: true }); await button.waitFor(); await button.focus()
      const metrics = await button.evaluate(e => ({ active: document.activeElement === e, text: e.textContent, whiteSpace: getComputedStyle(e).whiteSpace, width: e.clientWidth, scrollWidth: e.scrollWidth, height: e.clientHeight, scrollHeight: e.scrollHeight }))
      assert.equal(metrics.active, true); assert.equal(metrics.text, task.name); assert.equal(metrics.whiteSpace, 'normal')
      assert.ok(metrics.width > 0 && metrics.scrollWidth <= metrics.width + 1, JSON.stringify(metrics)); assert.ok(metrics.scrollHeight <= metrics.height + 1, JSON.stringify(metrics))
    }
    assert.equal(await page.evaluate(() => window.devicePixelRatio), 2)
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth), false)
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: visual Dashboard names wrap and remain fully visible/focusable at 800 CSS px, DPR2 and WCAG text spacing')
  } catch (e) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw e } finally { await browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
