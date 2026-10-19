// Stable S05 boot/deep-link acceptance against a self-started production preview and mocked API.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, task, project, panelIds } = require('./platform-fixtures.cjs')
;(async () => {
  const preview = await startPreview(5189)
  const { browser, page, mock } = await newMockPage(preview.base)
  const pageErrors = []; const consoleErrors = []
  page.on('pageerror', error => pageErrors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') consoleErrors.push(message.text()) })
  try {
    const dialog = page.getByRole('dialog')
    const panels = [
      ['Task', task.id, task.name],
      ['Milestone', panelIds.milestone, 'Synthetic milestone'],
      ['Deliverable', panelIds.deliverable, 'Synthetic deliverable'],
      ['Decision', panelIds.decision, 'Synthetic decision'],
      ['Action', panelIds.action, 'Synthetic action'],
      ['Risk', panelIds.risk, 'Synthetic risk'],
      ['Issue', panelIds.issue, 'Synthetic issue'],
    ]
    for (const [kind, id, title] of panels) {
      await page.goto(`${preview.base}/my-work?panel=${kind}:${id}`, { waitUntil: 'domcontentloaded' })
      try { await dialog.waitFor({ state: 'visible', timeout: 20000 }); await dialog.getByRole('heading', { name: title }).waitFor({ state: 'visible', timeout: 20000 }) } catch (error) {
        throw new Error(`${kind} panel did not mount: ${error.message}\nBODY: ${(await page.locator('body').innerText()).slice(0, 2400)}\nPAGE_ERRORS: ${pageErrors.join('; ')}\nCONSOLE: ${consoleErrors.join('; ')}\nUNKNOWN_GETS: ${mock.unknownGets.join('; ')}`)
      }
      const text = await dialog.innerText()
      assert.doesNotMatch(text, /Page not found|\bLoading\b/)
      assert.equal(new URL(page.url()).searchParams.get('panel'), `${kind}:${id}`)
    }
    await page.goto(`${preview.base}/my-work?panel=Task:${task.id}`, { waitUntil: 'domcontentloaded' })
    await dialog.getByText('Synthetic coordination note', { exact: true }).waitFor({ state: 'visible' })
    await dialog.getByText('Current drawings', { exact: true }).waitFor({ state: 'visible' })
    assert.match(await dialog.innerText(), /T-001|Synthetic task/)
    assert.equal(await page.locator('text=Page not found').count(), 0)
    assert.deepEqual(mock.apiErrors, [])
    assert.deepEqual(mock.unknownGets, [], `unhandled mocked GETs: ${mock.unknownGets.join('; ')}`)
    assert.deepEqual(pageErrors, [], `browser errors: ${pageErrors.join('; ')}`)
    assert.deepEqual(consoleErrors, [], `console errors: ${consoleErrors.join('; ')}`)
    console.log(JSON.stringify({ result: 'PASS', route: '/my-work', panels: panels.map(([kind]) => kind), project: project.projectNumber }))
  } finally { await browser.close(); preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
