// FR/AC-SCR-34..36 and Portfolio CountCard semantics. Synthetic mocked API only.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { projectRow } = require('./projects.fixture.cjs')
const { emptyPortfolio } = require('./portfolio.fixture.cjs')
const { referenceData, referenceClient, referenceOffice } = require('./me.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const asOf = '2026-10-31T14:24:05Z', officeId = '00000000-0000-0000-0010-000000000081'
const clients = [1, 2].map(n => ({ id: `00000000-0000-0000-0010-00000000000${n}`, name: `Synthetic municipality ${n}` }))
const staffing = { openRoles: 2, overCapacityPeople: 1, limitedScope: false, partialView: false, ownPlannerOnly: false, lookaheadWeeks: 4, from: '2026-10-26', through: '2026-11-22' }
const rows = Array.from({ length: 72 }, (_, i) => ({ ...projectRow({ id: i ? `00000000-0000-0000-0010-${String(i + 1).padStart(12, '0')}` : project.id,
  projectNumber: i ? `P-REVIEW-${String(i + 1).padStart(3, '0')}` : project.projectNumber, name: `Synthetic weekly corridor ${i + 1}`, client: clients[i % 2], officeId, office: 'Synthetic office',
  computedHealth: i === 0 ? 'Red' : i === 1 ? 'Yellow' : 'Green', reportedHealth: i === 0 ? 'Red' : i === 1 || i === 3 ? 'Yellow' : 'Green',
  ...(i === 3 ? { overrideActive: true, healthOverrideNote: 'Synthetic override explanation retained in print: ' + 'SYNTHETICREFERENCE'.repeat(8), overrideBy: 'Synthetic reviewer', healthOverrideAt: '2026-10-30T12:00:00Z' } : {}),
  overdueDecisions: i === 0 ? 3 : i === 1 ? 2 : 0,
  staffing: i === 2 ? { ...staffing, overCapacityPeople: null, limitedScope: true, partialView: true } : staffing,
  progressFacts: { today: '2026-10-31', elapsedPct: 60, progressPct: 50 },
  nextSubmission: i === 0 ? { id: '00000000-0000-0000-0010-000000000082', key: 'M-001', name: 'Synthetic submission', date: '2026-11-05', status: 'Upcoming' } : null }), trend: [],
  statusNote: i === 0 || i === 3 || i === 71 ? { text: `Synthetic weekly status note ${i + 1}: full text must survive printing with closed on-screen details`, authorId: me.id, author: 'Synthetic reviewer', at: '2026-10-30T12:00:00Z', header: { computedHealth: 'Green', reportedHealth: null, nextMilestone: null, openAttention: 0 } } : null }))
const refs = selected => selected.map(({ id, projectNumber, name }) => ({ id, projectNumber, name }))
const changes = { days: 7, fromDate: '2026-10-24', through: asOf, scopeProjects: rows.length, scopeCeilingReached: false,
  health: { count: 1, observed: 1, unavailableProjects: 0, projects: refs(rows.slice(0, 1)) },
  milestones: { count: 2, observed: 2, unavailableProjects: 0, projects: refs(rows.slice(0, 2)) },
  status: { count: 0, observed: 0, unavailableProjects: 0, projects: [] } }
const payload = { ...emptyPortfolio(), asOf, projects: rows, changes,
  tiles: { active: rows.length, red: 1, yellow: 1, green: rows.length - 2, submissions14: 1, overdueDecisions: 5 },
  cardFacts: { active: { projects: rows.length, clients: 2 }, red: { projects: 1, clients: 1 }, yellow: { projects: 1, clients: 1 }, green: { projects: rows.length - 2, clients: 2 }, submissions14: { projects: 1, clients: 1 }, overdueDecisions: { projects: 2, clients: 2 } } }
;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui, errors = []
  page.setDefaultTimeout(15000)
  page.on('pageerror', error => errors.push(error.message)); page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  try {
    me.capabilities.portfolio = true; me.capabilities.workload = true
    me.flags = { ...me.flags, visual_system: false, project_screens: false, status_rollups: true, small_ux: false, project_status_note: true }
    await page.route('**/api/v1/portfolio?**', route => {
      if (new URL(route.request().url()).searchParams.get('overdueDecisions') !== 'true') return route.fulfill({ json: payload })
      const selected = rows.filter(row => row.overdueDecisions > 0)
      return route.fulfill({ json: { ...payload, projects: selected, changes: { ...payload.changes, scopeProjects: selected.length },
        tiles: { ...payload.tiles, active: selected.length, green: 0, overdueDecisions: selected.reduce((sum, row) => sum + row.overdueDecisions, 0) },
        cardFacts: { ...payload.cardFacts, active: { projects: selected.length, clients: 2 }, green: { projects: 0, clients: 0 }, overdueDecisions: { projects: selected.length, clients: 2 } } } })
    })
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ clients: clients.map(client => referenceClient(client.id, client.name)), offices: [referenceOffice(officeId, 'Synthetic office')] }) }))
    const url = `${base}/portfolio?group=client&officeId=${officeId}&submissionWithinDays=7`
    await page.goto(url); await page.getByRole('link', { name: /P-MOCK Synthetic weekly corridor 1/ }).waitFor()
    assert.equal(await page.locator('[data-count-card]').count(), 0)
    assert.equal(await page.getByRole('columnheader', { name: 'Staffing', exact: true }).count(), 0)
    assert.equal(await page.getByRole('button', { name: 'Print weekly review', exact: true }).count(), 0)
    assert.equal(await page.locator('.portfolio-change-line').count(), 0)

    me.flags.project_screens = true; me.flags.visual_system = true; await page.reload()
    const cards = page.locator('.portfolio-count-cards [data-count-card]')
    await cards.first().waitFor(); assert.equal(await cards.count(), 6)
    assert.deepEqual(await cards.evaluateAll(nodes => nodes.map(node => node.tagName)), Array(6).fill('A'), 'whole cards have one destination')
    assert.ok(await cards.first().getAttribute('aria-labelledby').then(value => value.split(' ').length === 3), 'name includes label, value and where')
    await cards.first().getByText('Across 72 projects · 2 clients', { exact: true }).waitFor()
    await cards.first().focus(); await page.getByRole('tooltip').first().waitFor(); await page.keyboard.press('Escape')
    const targets = await cards.evaluateAll(nodes => nodes.map(node => ({ href: node.href, tone: node.className })))
    assert.equal(new URL(targets[0].href).searchParams.get('status'), 'Active')
    assert.equal(new URL(targets[1].href).searchParams.get('computedHealth'), 'Red'); assert.equal(new URL(targets[1].href).searchParams.get('status'), 'Active')
    assert.equal(new URL(targets[4].href).searchParams.get('submissionWithinDays'), '7', 'a narrower date filter is preserved rather than widening the counted cohort')
    assert.equal(new URL(targets[5].href).searchParams.get('overdueDecisions'), 'true')
    assert.ok(targets.every(target => new URL(target.href).searchParams.get('officeId') === officeId && new URL(target.href).searchParams.get('group') === 'client'))
    assert.deepEqual(targets.map(target => target.tone.match(/count-card--(blue|red|amber|green)/)[1]), ['blue', 'red', 'amber', 'green', 'amber', 'red'])
    assert.equal(await cards.nth(5).locator('.count-card__value').innerText(), '5')
    await cards.nth(5).getByText('Across 2 projects · 2 clients', { exact: true }).waitFor()
    const decisionDestination = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/portfolio' && new URL(response.url()).searchParams.get('overdueDecisions') === 'true')
    await cards.nth(5).click(); await decisionDestination
    await page.waitForFunction(() => document.querySelectorAll('.portfolio-screen-table [data-project-staffing]').length === 2)
    const filteredUrl = new URL(page.url())
    assert.equal(filteredUrl.searchParams.get('overdueDecisions'), 'true'); assert.equal(filteredUrl.searchParams.get('officeId'), officeId)
    assert.equal(filteredUrl.searchParams.get('group'), 'client'); assert.equal(filteredUrl.searchParams.get('submissionWithinDays'), '7')
    for (const [i, expected] of [3, 2].entries()) {
      const row = page.locator('.portfolio-screen-table tbody tr').filter({ has: page.locator(`a[href="/projects/${rows[i].projectNumber}/dashboard"]`) })
      assert.equal(await row.count(), 1)
      assert.equal(await row.locator(`a[href="/projects/${rows[i].projectNumber}/decisions?indicator=overdue"]`).innerText(), String(expected))
    }
    const retainedCounts = await page.locator('.portfolio-screen-table a[href$="/decisions?indicator=overdue"]').allTextContents()
    assert.deepEqual(retainedCounts.map(Number).sort((a, b) => a - b), [2, 3]); assert.equal(retainedCounts.reduce((sum, text) => sum + Number(text), 0), 5)
    assert.equal(await cards.nth(5).locator('.count-card__value').innerText(), '5')
    await cards.nth(5).getByText('Across 2 projects · 2 clients', { exact: true }).waitFor()
    await page.goto(url); await cards.first().getByText('Across 72 projects · 2 clients', { exact: true }).waitFor()
    const first = page.locator(`.portfolio-screen-table [data-project-staffing="${project.id}"]`)
    await first.getByText('2 open roles', { exact: true }).waitFor()
    const planner = first.locator('a[href^="/planner?"]'); assert.equal(new URL(await planner.getAttribute('href'), base).searchParams.get('teamProjectId'), project.id)
    await page.getByRole('link', { name: 'Capacity by discipline', exact: true }).waitFor()
    assert.equal(await page.getByRole('link', { name: 'Capacity by discipline', exact: true }).getAttribute('href'), '/planner?groupBy=homeDiscipline')
    const expectedColumns = await page.locator('.portfolio-screen-table thead th').count()
    const spans = await page.locator('.portfolio-screen-table [data-portfolio-group]').evaluateAll(nodes => nodes.map(row => [...row.cells].reduce((n, cell) => n + cell.colSpan, 0)))
    assert.deepEqual(spans, [expectedColumns, expectedColumns])

    const moved = page.locator('.portfolio-change-line')
    await moved.getByRole('button', { name: '1 project changed health', exact: true }).click()
    assert.equal(await moved.getByRole('region').getByRole('link').count(), 1)
    assert.equal(await moved.getByRole('region').getByRole('link').getAttribute('href'), `/projects/${project.projectNumber}/dashboard`)
    await moved.getByRole('button', { name: '2 milestones slipped', exact: true }).click(); assert.equal(await moved.getByRole('region').getByRole('link').count(), 2)
    assert.equal(page.url(), url, 'opening a scoped inline list keeps every source filter')
    payload.changes = { ...changes, health: { count: 72, observed: 72, unavailableProjects: 0, projects: refs(rows) } }
    await page.reload(); await moved.getByRole('button', { name: '72 projects changed health', exact: true }).click()
    assert.equal(await moved.getByRole('region').getByRole('link').count(), 72, 'inline list has no derived-ID URL or fifty-ID truncation')
    assert.ok((await moved.getByRole('region').getByRole('link').evaluateAll(nodes => nodes.map(node => node.href))).every(href => !href.includes('?ids=')))

    // The output uses the response instant, not the computer/browser date.
    const time = page.locator('.visual-title-block time'); assert.equal(await time.getAttribute('datetime'), asOf)
    assert.equal(await time.innerText(), '2026-10-31 11:24')
    const screenViewport = page.viewportSize()
    // Match the physical Letter-landscape content area (10mm margins) before
    // measuring text; a wide desktop viewport can hide word breaks seen in PDF.
    await page.setViewportSize({ width: Math.floor((11 - 20 / 25.4) * 96), height: Math.floor((8.5 - 20 / 25.4) * 96) })
    await page.emulateMedia({ media: 'print' }); await page.evaluate(() => document.fonts.ready)
    assert.equal(await page.getByRole('navigation').count(), 0)
    assert.equal(await page.locator('.portfolio-screen-table').isVisible(), false)
    assert.equal(await page.locator('.portfolio-print-table').isVisible(), true)
    assert.equal(await page.getByRole('button', { name: 'Print weekly review', exact: true }).isVisible(), false)
    assert.equal(await page.locator('.portfolio-print-table thead').evaluate(node => getComputedStyle(node).display), 'table-header-group')
    assert.equal(await page.locator('.portfolio-review .visual-page-band').evaluate(node => getComputedStyle(node).backgroundColor), 'rgb(27, 42, 78)')
    assert.equal(await page.locator('.portfolio-review').evaluate(node => getComputedStyle(node).getPropertyValue('page')), 'portfolio-review')
    assert.deepEqual(await page.locator('html,body,#root').evaluateAll(nodes => nodes.map(node => getComputedStyle(node).getPropertyValue('page'))), ['portfolio-review', 'portfolio-review', 'portfolio-review'], 'the complete containing print document has one named page')
    const printWidths = await page.locator('.portfolio-print-table thead th').evaluateAll(nodes => Object.fromEntries(nodes.map(node => [node.dataset.portfolioColumn, { width: node.getBoundingClientRect().width, fontSize: parseFloat(getComputedStyle(node).fontSize) }])))
    assert.equal(Object.keys(printWidths).length, expectedColumns - 1, 'every data column remains; only the coordination action is absent')
    assert.ok(printWidths.project.width > printWidths.overdue.width * 3, 'project descriptions remain proportionate after short headers gain room')
    assert.ok(printWidths.staffing.width > printWidths.blocked.width * 3, 'staffing facts remain proportionate after short headers gain room')
    assert.ok(Object.values(printWidths).every(column => column.fontSize >= 9.3), 'print table type remains at least the original seven points')
    const wordBreaks = await page.locator('.portfolio-print-table thead th, .portfolio-print-table .portfolio-print-health .semantic-pill').evaluateAll(nodes => {
      const problems = []
      for (const node of nodes) {
        const cell = node.closest('th,td'), bounds = cell.getBoundingClientRect(), style = getComputedStyle(cell)
        const left = bounds.left + parseFloat(style.paddingLeft), right = bounds.right - parseFloat(style.paddingRight)
        const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT)
        while (walker.nextNode()) for (const match of walker.currentNode.textContent.matchAll(/[A-Za-z]+/g)) {
          const range = document.createRange(); range.setStart(walker.currentNode, match.index); range.setEnd(walker.currentNode, match.index + match[0].length)
          const rects = [...range.getClientRects()].filter(rect => rect.width > 0 && rect.height > 0)
          if (rects.length !== 1 || rects.some(rect => rect.left < left - 1 || rect.right > right + 1)) problems.push({ column: cell.dataset.portfolioColumn, word: match[0], fragments: rects.length })
        }
      }
      return problems
    })
    assert.deepEqual(wordBreaks, [], 'each header/short health word stays whole and inside its print column')
    const bodyAudit = await page.locator('.portfolio-print-table tbody td').evaluateAll(cells => {
      const problems = []; let examined = 0
      for (const cell of cells) {
        const bounds = cell.getBoundingClientRect(), walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT)
        while (walker.nextNode()) {
          if (walker.currentNode.parentElement.closest('.sr-only')) continue
          for (const match of walker.currentNode.textContent.matchAll(/\S+/g)) {
            const range = document.createRange(); range.setStart(walker.currentNode, match.index); range.setEnd(walker.currentNode, match.index + match[0].length)
            const rects = [...range.getClientRects()].filter(rect => rect.width > 0 && rect.height > 0)
            if (!rects.length) continue
            examined++
            if (rects.some(rect => rect.left < bounds.left - 1 || rect.right > bounds.right + 1)) problems.push({ column: cell.dataset.portfolioColumn, text: match[0] })
          }
        }
      }
      return { examined, problems }
    })
    assert.ok(bodyAudit.examined > rows.length, 'the print containment audit examines rendered text ranges')
    assert.deepEqual(bodyAudit.problems, [], 'printed project/client/office/health-note text stays within its own column')
    assert.ok((await page.locator('.portfolio-print-table .portfolio-print-health .semantic-pill').evaluateAll(nodes => nodes.map(node => ({ whiteSpace: getComputedStyle(node).whiteSpace, flexWrap: getComputedStyle(node).flexWrap })))).every(style => style.whiteSpace === 'nowrap' && style.flexWrap === 'nowrap'), 'health symbol and value stay together')
    const printedHealthNote = page.locator('.portfolio-print-table [data-portfolio-health-note]')
    await printedHealthNote.getByText(/Synthetic override explanation retained in print/).waitFor()
    assert.equal(await printedHealthNote.locator('..').locator('..').getAttribute('colspan'), String(expectedColumns - 1), 'health rationale uses the full print row')
    assert.equal(await page.locator('.portfolio-print-table tbody tr').filter({ has: page.locator('section:visible').filter({ hasText: 'full text must survive printing' }) }).count(), 3)
    assert.equal(await page.locator('.portfolio-print-table tbody tr').count(), rows.length + 5, 'all project rows, two groups and three full note rows print, with health rationale sharing its project status-note row')
    const pdf = await page.pdf({ path: process.env.TUESDAY_PORTFOLIO_PDF ?? '/private/tmp/tuesday-portfolio-weekly-review.pdf', preferCSSPageSize: true, printBackground: true })
    const mediaBoxes = [...pdf.toString('latin1').matchAll(/\/MediaBox\s*\[\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\]/g)]
    assert.ok([...pdf.toString('latin1').matchAll(/\/Type\s*\/Page\b/g)].length >= 2, 'the synthetic weekly review genuinely spans multiple PDF pages')
    assert.ok(mediaBoxes.length > 0, 'generated PDF records its page dimensions')
    assert.ok(mediaBoxes.every(box => Number(box[3]) - Number(box[1]) > Number(box[4]) - Number(box[2])), 'every generated page is landscape')
    await page.emulateMedia({ media: 'screen' })
    if (screenViewport) await page.setViewportSize(screenViewport)

    payload.changes = { ...changes, health: { count: null, observed: 0, unavailableProjects: 72, projects: [] }, milestones: { ...changes.milestones, count: null, unavailableProjects: 1 } }
    await page.reload(); await moved.getByRole('button', { name: 'Health history unavailable', exact: true }).waitFor()
    assert.equal(await moved.getByRole('button', { name: '0 projects changed health', exact: true }).count(), 0)
    await moved.getByRole('button', { name: 'At least 2 milestones slipped', exact: true }).click()
    await moved.getByText('Projects with incomplete history: 1. This list contains only confirmed moves from available records.', { exact: true }).waitFor()

    // The print part also works independently when visual_system is rolled back.
    me.flags.visual_system = false; await page.reload(); await page.emulateMedia({ media: 'print' })
    assert.equal(await page.locator('.portfolio-print-band time').getAttribute('datetime'), asOf)
    assert.equal(await page.locator('.portfolio-print-band time').innerText(), '2026-10-31 11:24')
    assert.equal(await page.locator('.portfolio-print-band').evaluate(node => getComputedStyle(node).backgroundColor), 'rgb(27, 42, 78)', 'the independent print part retains the approved navy even without visual tokens')
    assert.equal(await page.locator('.portfolio-print-table').isVisible(), true)
    // Client-side navigation keeps the route stylesheet loaded. Its named page
    // must not turn another screen's printout into a landscape weekly review.
    await page.emulateMedia({ media: 'screen' })
    await page.locator('nav a[href="/projects"]').first().click()
    await page.waitForURL(url => url.pathname === '/projects')
    await page.getByRole('heading', { name: 'Projects', level: 1, exact: true }).waitFor()
    await page.getByRole('table', { name: 'Projects', exact: true }).getByRole('link', { name: project.name, exact: true }).waitFor()
    await page.emulateMedia({ media: 'print' })
    assert.equal(await page.locator('.portfolio-review').count(), 0)
    const otherPdf = await page.pdf({ preferCSSPageSize: true, printBackground: true })
    const otherBoxes = [...otherPdf.toString('latin1').matchAll(/\/MediaBox\s*\[\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\]/g)]
    assert.ok(otherBoxes.length > 0)
    assert.ok(otherBoxes.every(box => Number(box[3]) - Number(box[1]) < Number(box[4]) - Number(box[2])), 'Portfolio CSS does not alter Projects print orientation after SPA navigation')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: Portfolio scoped staffing/change evidence/CountCard destinations and independent landscape weekly-review print')
  } catch (error) { console.error(await page.locator('body').innerText(), mock.unknownGets, mock.apiErrors, errors); throw error } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
