// Actual ProjectLayout + populated project Tasks with synthetic API DTOs.
// Header, actual task and group-summary geometry are distinct observations.
const assert = require('node:assert/strict')
const { newMockPage, project, panelIds } = require('./platform-fixtures.cjs')
const { meView } = require('./me.fixture.cjs')
const { projectDetail } = require('./projects.fixture.cjs')
const { taskRow, taskState, taskPage } = require('./tasks.fixture.cjs')
const { deliverableList, deliverableRow } = require('./deliverables.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL
assert.ok(base && ['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local reviewed app')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const spaced = 'Harbour crossing coordination with phased engineering review and construction delivery '.repeat(2).slice(0, 120)
const unbroken = 'SyntheticProjectNameWithoutBreaks'.repeat(5).slice(0, 120)
assert.equal(spaced.length, 120); assert.equal(unbroken.length, 120)
const civil = project.disciplines[0].id, stamp = '2026-11-01T13:00:00Z'
const dto = projectDetail({ ...project, name: spaced, client: 'Synthetic client', pmName: 'Synthetic PM', targetCompletionDate: '2026-11-30', starred: true,
  health: { ...project.health, computed: 'Green', reported: 'Green' }, permissions: { ...project.permissions, createTaskIn: [civil] } })
const original = { name: dto.name, client: dto.client, pmName: dto.pmName, targetCompletionDate: dto.targetCompletionDate, health: { ...dto.health }, permissions: { ...dto.permissions } }
const tasks = [5, 7].map((estimatedHours, index) => taskRow({ id: id(960 + index), projectId: project.id, key: `SYN-H-T${index + 1}`, name: `Synthetic header task ${index + 1}`,
  projectDisciplineId: civil, deliverableId: panelIds.deliverable, deliverableKey: 'D-001', deliverableName: 'Synthetic header deliverable', estimatedHours, status: 'In Progress',
  dueDate: index === 0 ? '2026-10-31' : '2026-11-05', assigneeId: id(2), assigneeName: 'Synthetic viewer', state: taskState({ isOverdue: index === 0, daysOverdue: index === 0 ? 1 : 0, isMissingDueDate: false, isUnassigned: false }) }))

;(async () => {
  const { page, browser } = await newMockPage(base, { events: false })
  const errors = [], writes = [], observations = [], densityFailures = []
  let visual = false, work = true
  page.setDefaultTimeout(10000)
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  try {
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (!['GET', 'HEAD'].includes(request.method()) && path !== 'me/sign-in') writes.push(`${request.method()} ${path}`)
      if (path === 'me') return route.fulfill({ json: meView({ id: id(2), displayName: 'Synthetic viewer', email: 'header@hub.test', capabilities: { createTask: true, workload: true },
        settings: { today: '2026-11-01' }, flags: { visual_system: visual, work_screens: work, small_ux: true } }) })
      if (path === `projects/${project.id}` || path === `projects/${project.projectNumber}`) return route.fulfill({ json: dto })
      if (path === `projects/${project.id}/deliverables`) return route.fulfill({ json: deliverableList([deliverableRow({ id: panelIds.deliverable, projectId: project.id, key: 'D-001', name: 'Synthetic header deliverable', projectDisciplineId: civil })]) })
      if (path === `projects/${project.id}/tasks`) {
        const grouped = work && url.searchParams.get('group') === 'deliverable'
        return route.fulfill({ json: taskPage(tasks, { pageSize: 100, asOf: stamp, ...(grouped ? { groups: [{ key: `${project.id}:deliverable:${panelIds.deliverable}`, parentKey: null, dimension: 'deliverable', projectId: project.id,
          projectNumber: project.projectNumber, projectName: dto.name, value: panelIds.deliverable, label: 'D-001 Synthetic header deliverable', referenceAvailable: true, rowCount: 2, estimatedHours: 12, unestimatedCount: 0,
          overdueCount: 1, unavailableStateCount: 0, progressPct: 0, progressBasis: 'Hours', progressTaskCount: 2, nextDueDate: '2026-10-31', undatedOpenCount: 0, pageShown: 2, pageTaskIds: tasks.map(task => task.id) }] } : {}) }) })
      }
      return route.fallback()
    })
    const url = group => `${base}/projects/${project.projectNumber}/tasks?group=${group}&sort=key%3Aasc`
    const header = page.locator('[data-visual-project-header]'), name = header.locator('h1')
    const taskRow = index => page.locator('[data-task-layout="desktop"] tbody tr').filter({ hasText: tasks[index].name })
    const settle = async () => {
      await name.waitFor(); await page.getByRole('button', { name: tasks[0].name, exact: true }).waitFor()
      await page.evaluate(async () => { await document.fonts.ready })
    }
    const legacyStyle = () => header.evaluate(el => {
      const style = getComputedStyle(el), h1 = el.querySelector('h1')
      return { height: el.getBoundingClientRect().height, padding: style.padding, border: style.borderTopWidth, background: style.backgroundColor, color: style.color, title: h1.textContent, nameTabIndex: h1.getAttribute('tabindex'), metadata: !!el.querySelector('.visual-project-meta') }
    })
    await page.goto(url('none')); await settle()
    const legacy = await legacyStyle()
    assert.equal(await page.locator('[data-visual-project-layout]').count(), 0)
    visual = true
    for (const fullName of [spaced, unbroken]) {
      dto.name = fullName
      for (const group of ['none', 'deliverable']) {
        await page.goto(url(group)); await settle()
        await page.waitForFunction(() => document.documentElement.dataset.visual === 'v2')
        const geometry = await page.evaluate(() => Object.fromEntries(['.visual-topbar', '[data-visual-project-header]', '[data-visual-project-tabs]', '[data-visual-project-layout] .visual-page-band', '.visual-project-task-source', '[data-visual-project-layout] .visual-page-content', '[data-task-layout="desktop"] thead', '[data-task-layout="desktop"] tbody tr[data-index]'].map(selector => {
          const rect = document.querySelector(selector)?.getBoundingClientRect(); return [selector, rect && { top: rect.top, height: rect.height, bottom: rect.bottom }]
        })))
        const height = (await header.boundingBox()).height
        assert.ok(height > 0 && height <= 72, `project identity header<=72 with${group}: ${JSON.stringify(geometry)}`)
        assert.equal(await name.getAttribute('title'), fullName)
        assert.equal(await name.evaluate(el => getComputedStyle(el).whiteSpace), 'nowrap')
        assert.ok(await name.evaluate(el => el.clientHeight <= parseFloat(getComputedStyle(el).lineHeight) + 1 && el.clientWidth > 0))
        assert.match(await header.locator('.visual-project-number').evaluate(el => getComputedStyle(el).fontFamily), /Plex Mono/)
        assert.match(await header.innerText(), /Active/); assert.match(await header.innerText(), /Green/)
        assert.match(await header.locator('.visual-project-meta').innerText(), /Synthetic PM.*Synthetic client.*2026-11-30/s)
        const firstTask = await taskRow(0).boundingBox()
        const groupRow = group === 'deliverable' ? page.locator('[data-task-layout="desktop"] tbody tr[data-task-group]').first() : null
        const firstMeaningful = groupRow ? await groupRow.boundingBox() : firstTask
        observations.push({ group, name: fullName === spaced ? 'spaced120' : 'unbroken120', header: height, firstMeaningfulY: firstMeaningful.y, firstTaskY: firstTask.y })
        console.log(JSON.stringify(observations.at(-1)))
        if (!firstTask || firstTask.y > 405) densityFailures.push({ group, name: fullName === spaced ? 'spaced120' : 'unbroken120', geometry, firstTaskY: firstTask?.y, firstMeaningfulY: firstMeaningful?.y })
        if (groupRow) {
          await groupRow.getByText('2 shown / 2 matched tasks', { exact: true }).waitFor()
          const source = page.locator('.visual-project-task-source [role="status"]')
          await source.waitFor()
          assert.match(await source.innerText(), /Group totals: first read[\s\S]*later pages are separate live reads\./)
          assert.equal(await source.evaluate(el => el.scrollWidth <= el.clientWidth + 1 && el.scrollHeight <= el.clientHeight + 1), true, 'full source/read explanation remains visible and wraps')
          const sourceText = await source.innerText()
          const printedSource = page.locator('.visual-project-task-source-print [role="status"]')
          assert.equal(await printedSource.isVisible(), false)
          await page.emulateMedia({ media: 'print' })
          assert.equal(await printedSource.isVisible(), true)
          assert.equal(await printedSource.innerText(), sourceText, 'print preserves the complete grouped-read provenance')
          await page.emulateMedia({ media: 'screen' })
        }
        // Keep project primary/secondary links, actions and exact query scope.
        assert.equal(await page.locator('.visual-project-tabs-v2__primary a[aria-current]').innerText(), 'Work')
        assert.equal(await page.locator('.visual-project-tabs-v2__secondary a[aria-current="page"]').innerText(), 'Tasks')
        assert.ok(await page.getByRole('button', { name: /^Filters/ }).isVisible())
        assert.equal(new URL(page.url()).searchParams.get('group'), group)
        assert.equal(new URL(page.url()).searchParams.get('sort'), 'key:asc')
        await header.locator('button').first().focus(); await page.keyboard.press('Tab')
        assert.equal(await name.evaluate(el => document.activeElement === el), true)
        await page.getByRole('tooltip').filter({ hasText: fullName }).waitFor()
        await page.keyboard.press('Escape'); await name.blur(); await name.hover()
        await page.getByRole('tooltip').filter({ hasText: fullName }).waitFor()
        await page.mouse.move(0, 0)
      }
    }
    dto.pmName = 'SyntheticUnbrokenProjectManager'.repeat(6); dto.client = 'SyntheticUnbrokenClientName'.repeat(6)
    for (const width of [1440, 375]) {
      await page.setViewportSize({ width, height: 1000 }); await page.goto(url('deliverable')); await settle()
      assert.ok((await header.boundingBox()).height <= 72)
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
      assert.equal(await header.locator('.visual-project-meta span').evaluateAll(nodes => nodes.every(el => el.scrollWidth <= el.clientWidth + 1 || !!el.title)), true, 'clipped metadata keeps a full native title and Details text')
      assert.equal(await header.locator('.visual-project-meta span').last().evaluate(el => el.scrollWidth <= el.clientWidth + 1), true, 'target date stays complete')
      await header.getByRole('button', { name: 'Project details and actions', exact: true }).click()
      const details = page.locator('[data-visual-project-details]'); await details.waitFor()
      assert.equal(await details.evaluate(el => document.activeElement === el), true)
      assert.ok((await details.innerText()).includes(dto.pmName) && (await details.innerText()).includes(dto.client))
      await details.getByText('2026-11-30', { exact: true }).waitFor()
      assert.equal(await details.locator('dd,p').evaluateAll(nodes => nodes.every(el => el.scrollWidth <= el.clientWidth + 1)), true)
      await page.keyboard.press('Escape')
      await details.waitFor({ state: 'hidden' })
      await page.waitForFunction(label => {
        const header = document.querySelector('[data-visual-project-header]')
        return [...(header?.querySelectorAll('button[aria-label]') ?? [])].some(button => button.getAttribute('aria-label') === label && document.activeElement === button)
      }, 'Project details and actions')
      assert.equal(await header.getByRole('button', { name: 'Project details and actions', exact: true }).evaluate(el => document.activeElement === el), true)
      await name.focus(); await page.getByRole('tooltip').filter({ hasText: unbroken }).waitFor()
      assert.ok(await name.evaluate(el => el.clientWidth > 0))
    }
    const overrideBy = 'Synthetic reviewer with a deliberately long reported-by name '.repeat(4).trim()
    const overrideNote = 'Synthetic reported-health explanation retains the complete review basis and every condition. '.repeat(5).trim()
    const computedReason = 'Synthetic computed-health reason remains available through Why'
    dto.health = { ...dto.health, computed: 'Red', reported: 'Green', overrideActive: true, healthOverride: 'Green', overrideBy, healthOverrideExpiresAt: '2026-12-30T16:00:00Z', healthOverrideNote: overrideNote,
      reasons: [{ text: computedReason }] }
    dto.permissions = { ...dto.permissions, healthOverride: { ok: true, reason: null } }
    for (const width of [1440, 375]) {
      await page.setViewportSize({ width, height: width === 375 ? 812 : 1000 }); await page.goto(url('none')); await settle()
      const summary = header.locator('.visual-project-health-summary')
      await summary.waitFor()
      assert.ok((await header.boundingBox()).height <= 72, `override header stays<=72 at${width}`)
      assert.ok(await name.evaluate(el => el.clientWidth >= 24), 'override leaves usable project-name width')
      assert.equal(await page.getByRole('heading', { level: 1, name: unbroken, exact: true }).count(), 1)
      assert.equal(await name.getAttribute('title'), unbroken)
      assert.deepEqual(await summary.locator('.semantic-pill').evaluateAll(nodes => nodes.map(node => node.textContent.replace(/^[●▲■○✓◐]\s*/, ''))), ['Reported: Green', 'Computed: Red'])
      assert.equal(await summary.getAttribute('aria-describedby'), `project-health-summary-${project.id}`)
      assert.equal(await header.locator('.semantic-pill').filter({ hasText: 'Active' }).count(), 1)
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
      assert.equal(await header.evaluate(el => { const box = el.getBoundingClientRect(); return [...el.querySelectorAll('button,h1,.semantic-pill')].every(node => { const r = node.getBoundingClientRect(); return r.left >= box.left && r.right <= box.right + 1 && r.top >= box.top && r.bottom <= box.bottom + 1 }) }), true, 'override title, pills and controls fit inside the72px header')
      if (width === 1440) {
        const row = await taskRow(0).boundingBox()
        if (!row || row.y > 405) densityFailures.push({ group: 'none', name: 'override120', firstTaskY: row?.y })
      }
      await name.focus(); await page.getByRole('tooltip').filter({ hasText: unbroken }).waitFor()
      await page.keyboard.press('Escape'); await summary.focus(); await page.keyboard.press('Enter')
      const details = page.locator('[data-visual-project-details]'); await details.waitFor()
      assert.equal(await details.evaluate(el => document.activeElement === el), true)
      const by = details.getByRole('button', { name: /^reported by / })
      await by.waitFor(); assert.ok((await by.getAttribute('aria-label')).includes(overrideBy))
      assert.match(await by.getAttribute('aria-label'), /2026-12-30/)
      assert.equal(await details.getByRole('button', { name: 'Change override', exact: true }).count(), 1)
      assert.ok((await details.innerText()).includes(dto.pmName) && (await details.innerText()).includes(dto.client))
      await details.getByText('2026-11-30', { exact: true }).waitFor()
      assert.equal(await details.evaluate(el => el.scrollWidth <= el.clientWidth + 1), true, 'long override facts wrap in Details')
      await by.click(); await page.getByText(overrideNote, { exact: true }).waitFor()
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
      await page.keyboard.press('Escape')
      await page.getByText(overrideNote, { exact: true }).waitFor({ state: 'detached' })
      await page.waitForFunction(element => document.activeElement === element, await by.elementHandle())
      const computedTrigger = details.getByRole('button', { name: /^Why is health Red/ })
      await computedTrigger.click()
      await page.getByText(computedReason, { exact: true }).waitFor()
      await page.keyboard.press('Escape')
      await page.getByText(computedReason, { exact: true }).waitFor({ state: 'detached' })
      await page.waitForFunction(element => document.activeElement === element, await computedTrigger.elementHandle())
      assert.equal(await details.isVisible(), true, 'closing the inner explanation preserves Project details')
      const summaryElement = await summary.elementHandle()
      await page.keyboard.press('Escape')
      await details.waitFor({ state: 'detached' })
      await page.waitForFunction(element => document.activeElement === element, summaryElement)
    }
    Object.assign(dto, original)
    work = false
    await page.setViewportSize({ width: 1440, height: 1000 }); await page.goto(url('none')); await settle()
    assert.ok((await header.boundingBox()).height <= 72, 'header is independent of Work screens')
    visual = false; work = true
    await page.goto(url('none')); await settle()
    await page.waitForFunction(() => !document.documentElement.hasAttribute('data-visual'))
    assert.deepEqual(await legacyStyle(), legacy, 'flag-off header styles and legacy name/tab/metadata semantics are restored exactly')
    assert.equal(await page.locator('[data-visual-project-layout], [data-visual-project-details]').count(), 0)
    assert.deepEqual(writes, []); assert.deepEqual(errors, [])
    console.log(JSON.stringify({ observations, densityFailures, note: 'Grouped firstMeaningfulY is the actual group-summary row; child firstTaskY is reported separately. Mock API is not server permission evidence.' }))
    assert.deepEqual(densityFailures, [], 'settled first actual task row must be<=405 in BOTH ungrouped and grouped cases; a group-summary row does not satisfy that assertion')
    console.log('PASS: real project header120-char names/full hover+focus, pills/meta/Mono/details,72cap, strict ungrouped and grouped actual-task405, phone containment, independent flags and exact off restoration')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
