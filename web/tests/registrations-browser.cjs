// Stable S05 boot/deep-link acceptance against a self-started production preview and mocked API.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, task, project } = require('./platform-fixtures.cjs')
;(async () => {
  const preview = await startPreview()
  const { browser, page, mock } = await newMockPage(preview.base)
  const pageErrors = []
  page.on('pageerror', error => pageErrors.push(error.message))
  try {
    await page.goto(`${preview.base}/my-work?panel=Task:${task.id}`, { waitUntil: 'domcontentloaded' })
    const dialog = page.getByRole('dialog')
    await dialog.waitFor({ state: 'visible', timeout: 20000 })
    let text = ''
    for (let i = 0; i < 40; i++) { text = await dialog.innerText(); if (await dialog.getByRole('heading', { name: task.name }).count()) break; await page.waitForTimeout(250) }
    assert.ok(await dialog.getByRole('heading', { name: task.name }).count(), `task panel body did not mount: ${text}; page errors: ${pageErrors.join('; ')}`)
    assert.match(text, /T-001|Synthetic task/)
    assert.doesNotMatch(text, /Page not found|\bLoading\b/)
    assert.equal(new URL(page.url()).searchParams.get('panel'), `Task:${task.id}`)
    assert.equal(await page.locator('text=Page not found').count(), 0)
    assert.deepEqual(mock.apiErrors, [])
    assert.deepEqual(pageErrors, [], `browser errors: ${pageErrors.join('; ')}`)
    console.log(JSON.stringify({ result: 'PASS', route: '/my-work', deepLink: `Task:${task.id}`, project: project.projectNumber }))
  } finally { await browser.close(); preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
