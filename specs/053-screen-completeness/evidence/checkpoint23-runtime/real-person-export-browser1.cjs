// Built SPA joins to the owned synthetic QA23 API. Downloads may write the existing Exported audit only.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { execFileSync } = require('node:child_process')
const root = path.resolve(__dirname, '../../../..')
const { chromium } = require(path.join(root, 'web/node_modules/playwright'))
const m = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-seen-setup1.json'), 'utf8'))
const s = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-person-export-setup1.json'), 'utf8'))
assert.equal(m.database, 'hub_qa_checkpoint23'); assert.equal(s.status, 'PASS_NORMAL_COMMAND_SETUP')
const checks = [], writes = [], personal = [], errors = [], downloads = []
function observe(page, surface) {
  page.on('pageerror', error => errors.push(error.message))
  page.on('request', request => {
    const u = new URL(request.url())
    if (!u.pathname.startsWith('/api/') || ['GET', 'HEAD', 'OPTIONS'].includes(request.method()) || u.pathname === '/api/v1/me/sign-in') return
    if (request.method() === 'POST' && /^\/api\/v1\/me\/work\/(opened|visits|seen-all)$/.test(u.pathname)) personal.push({ surface, path: u.pathname, body: request.postDataJSON() })
    else writes.push({ method: request.method(), path: u.pathname })
  })
}
async function api(who, route) {
  const r = await fetch(m.apiBase + '/api/v1/' + route, { headers: { 'X-Dev-User': who } }); assert.ok(r.ok, route + ': ' + r.status); return r.json()
}
function inspect(file, format) {
  return JSON.parse(execFileSync('python3', ['-c', `import sys,csv,json,zipfile,xml.etree.ElementTree as E
p,fmt=sys.argv[1:];date_cell=None
if fmt=='csv':
 raw=open(p,encoding='utf-8-sig').read();rows=list(csv.reader(raw.splitlines()))
else:
 z=zipfile.ZipFile(p);raw='\\n'.join(z.read(n).decode('utf-8') for n in z.namelist() if n.endswith('.xml'))
 ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'};doc=E.fromstring(z.read('xl/worksheets/sheet1.xml'));rows=[]
 date=doc.find('.//s:c[@r="F2"]',ns);styles=E.fromstring(z.read('xl/styles.xml')).findall('s:cellXfs/s:xf',ns)
 date_cell={'type':date.get('t'),'format':styles[int(date.get('s','0'))].get('numFmtId'),'value':date.find('s:v',ns).text}
 for row in doc.findall('.//s:row',ns):
  values=[]
  for cell in row.findall('s:c',ns):
   text=''.join(t.text or '' for t in cell.findall('.//s:t',ns));v=cell.find('s:v',ns);values.append(text if text else (v.text if v is not None else ''))
  rows.append(values)
print(json.dumps({'rows':rows,'text':raw,'dateCell':date_cell}))`, file, format], { encoding: 'utf8' }))
}
;(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' })
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce', acceptDownloads: true })
    await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'alex@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    const page = await context.newPage(); page.setDefaultTimeout(15000)
    observe(page, 'report')
    const url = m.uiBase + '/reports/task-hours?' + s.reportQuery
    await page.goto(url)
    const triggers = page.locator('table tbody [data-person-panel-trigger="' + m.people.alex.id + '"]')
    await triggers.nth(1).waitFor()
    const ids = await triggers.evaluateAll(nodes => nodes.map(node => node.id))
    assert.equal(ids.length, 2); assert.equal(new Set(ids).size, 2)
    for (const [index, width] of [[0, 1440], [1, 375]]) {
      await page.setViewportSize({ width, height: 1000 })
      const trigger = triggers.nth(index), id = ids[index]
      await trigger.scrollIntoViewIfNeeded(); await trigger.focus(); await page.keyboard.press('Enter')
      await page.locator('[data-person-panel="' + m.people.alex.id + '"]').waitFor()
      await page.keyboard.press('Escape'); await page.waitForURL(url)
      await page.waitForFunction(id => document.activeElement?.id === id, id)
      assert.equal(await trigger.getAttribute('id'), id)
      await page.screenshot({ path: path.join(__dirname, 'real-report-person-' + width + '.png'), fullPage: true })
    }
    checks.push('Two actual time entries use distinct persisted-User report openers and natural exact focus return at1440/375')
    await context.close()
    const exports = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce', acceptDownloads: true })
    await exports.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'priya@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    const exportPage = await exports.newPage(); exportPage.setDefaultTimeout(15000)
    observe(exportPage, 'project-register')
    const before = await api('priya@hub.test', 'deliverables/' + s.deliverable.id)
    await exportPage.goto(m.uiBase + '/projects/' + m.projectNumber + '/deliverables?ownerId=' + m.people.alex.id + '&group=none')
    await exportPage.getByRole('button', { name: 'Synthetic QA23 client drawing', exact: true }).waitFor()
    for (const [format, label] of [['csv', 'CSV (.csv)'], ['xlsx', 'Excel (.xlsx)']]) {
      await exportPage.getByRole('button', { name: 'Export', exact: true }).click()
      const received = exportPage.waitForResponse(r => new URL(r.url()).pathname.endsWith('/deliverables/client-export') && new URL(r.url()).searchParams.get('format') === format)
      const pending = exportPage.waitForEvent('download')
      await exportPage.getByRole('menuitem', { name: 'Client register · ' + label, exact: true }).click()
      const response = await received, download = await pending
      assert.ok(response.ok()); assert.equal(new URL(response.url()).searchParams.get('ownerId'), m.people.alex.id)
      const file = path.join(__dirname, 'real-client-register.' + format); await download.saveAs(file)
      const data = inspect(file, format)
      assert.deepEqual(data.rows[0], ['Number', 'Client drawing number', 'Title', 'Revision', 'Purpose of issue', 'Issue date', 'Return code', 'Status'])
      assert.equal(data.rows.length, 2); assert.deepEqual(data.rows[1].slice(0, 5), [s.deliverable.key, '00123', 'Synthetic QA23 client drawing', 'A', 'For review'])
      assert.deepEqual(data.rows[1].slice(6), ['B', 'Issued'])
      if (format === 'csv') assert.equal(data.rows[1][5], m.today)
      else {
        const serial = String((Date.parse(m.today + 'T00:00:00Z') - Date.UTC(1899, 11, 30)) / 86400000)
        assert.equal(data.rows[1][5], serial)
        assert.ok(data.dateCell.type == null || data.dateCell.type === 'n')
        assert.equal(data.dateCell.format, '14'); assert.equal(data.dateCell.value, serial)
      }
      for (const secret of ['PRIVATE', m.people.alex.name, m.people.marc.name, m.people.jill.name, 'userReferences', 'sourceKey', m.people.alex.id]) assert.equal(data.text.includes(secret), false, secret + ' absent from client download')
      downloads.push({ format, file: path.basename(file), rows: data.rows.length, filterPreserved: true })
    }
    assert.deepEqual(await api('priya@hub.test', 'deliverables/' + s.deliverable.id), before, 'Exports preserve canonical detail/issue/version/activity facts')
    await exportPage.screenshot({ path: path.join(__dirname, 'real-client-register-desktop.png'), fullPage: true })
    checks.push('Actual filtered CSV/XLSX client menu downloads contain exactly eight allowed columns and recorded issue bundle, preserving00123 and excluding internal metadata')
    await exportPage.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    assert.deepEqual(writes, []); assert.deepEqual(errors, [])
    assert.ok(personal.every(row => row.surface === 'project-register' && row.path.endsWith('/opened') && row.body.itemType === 'Project' && row.body.itemId === m.projectId), 'Only the actual PM Project header may acknowledge its own assignment; Report Person never opens work')
    const result = { status: 'PASS', checks, downloads, browserNonGetBusinessRequests: writes, personalCommands: personal, errors,
      boundary: 'Actual builtSPA and syntheticQA23 API; existing Exported audit may be appended; no atomic import or human acceptance claim' }
    fs.writeFileSync(path.join(__dirname, 'real-person-export-browser1.json'), JSON.stringify(result, null, 2) + '\n')
    console.log(JSON.stringify({ status: 'PASS', checks, downloads: downloads.length, browserNonGetBusinessRequests: 0, personalCommands: personal.length }))
  } finally { await browser.close() }
})().catch(error => { fs.writeFileSync(path.join(__dirname, 'real-person-export-browser1-failure.json'), JSON.stringify({ error: error.message, checks, downloads, writes, personal, errors }, null, 2)); console.error(error); process.exitCode = 1 })
