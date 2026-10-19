// Stable S05 boot/deep-link acceptance against a self-started production preview and mocked API.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, task, project } = require('./platform-fixtures.cjs')
;(async () => {
  const preview = await startPreview(5189)
  const { browser, page, mock } = await newMockPage(preview.base)
  const pageErrors = []; const consoleErrors = []
  page.on('pageerror', error => pageErrors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') consoleErrors.push(message.text()) })
  try {
    await page.goto(`${preview.base}/my-work?panel=Task:${task.id}`, { waitUntil: 'domcontentloaded' })
    const dialog = page.getByRole('dialog')
    try { await dialog.waitFor({ state: 'visible', timeout: 20000 }) } catch (error) { throw new Error(`${error.message}\nBODY: ${(await page.locator('body').innerText()).slice(0, 2000)}\nPAGE_ERRORS: ${pageErrors.join('; ')}\nCONSOLE: ${consoleErrors.join('; ')}\nUNKNOWN_GETS: ${mock.unknownGets.join('; ')}`) }
    let text = ''
    for (let i = 0; i < 40; i++) { text = await dialog.innerText(); if (await dialog.getByRole('heading', { name: task.name }).count()) break; await page.waitForTimeout(250) }
    assert.ok(await dialog.getByRole('heading', { name: task.name }).count(), `task panel body did not mount: ${text}; page errors: ${pageErrors.join('; ')}`)
    assert.match(text, /T-001|Synthetic task/)
    assert.doesNotMatch(text, /Page not found|\bLoading\b/)
    assert.equal(new URL(page.url()).searchParams.get('panel'), `Task:${task.id}`)
    assert.equal(await page.locator('text=Page not found').count(), 0)
    assert.deepEqual(mock.apiErrors, [])
    assert.deepEqual(mock.unknownGets, [], `unhandled mocked GETs: ${mock.unknownGets.join('; ')}`)
    assert.deepEqual(pageErrors, [], `browser errors: ${pageErrors.join('; ')}`)
    assert.deepEqual(consoleErrors, [], `console errors: ${consoleErrors.join('; ')}`)
    console.log(JSON.stringify({ result: 'PASS', route: '/my-work', deepLink: `Task:${task.id}`, project: project.projectNumber }))
  } finally { await browser.close(); preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
