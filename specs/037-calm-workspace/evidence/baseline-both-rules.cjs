// Re-measures section 2's baseline columns on a UI under both data rules: the appendix A rule (tables, grids, dialogs)
// and the fix-pass rule (also articles and list items outside navigation). Read-only.
const { chromium } = require(require('path').resolve(__dirname, '../../../web/node_modules/playwright'))
const fs = require('fs')
const [base, who, outFile] = process.argv.slice(2)
const P = 'SYN-101'
const SCREENS = ['/home', '/my-work', '/boards', '/projects', '/tasks', '/calendar', '/portfolio', '/workload', '/planner', '/reports', '/team', `/projects/${P}/dashboard`, `/projects/${P}/tasks`, `/projects/${P}/deliverables`, `/projects/${P}/timeline`, `/projects/${P}/allocations`, `/projects/${P}/coordination`]
;(async () => {
  const browser = await chromium.launch({ headless: true })
  const rows = []
  for (const path of SCREENS) {
    const ctx = await browser.newContext({ viewport: { width: 1600, height: 900 }, reducedMotion: 'reduce' })
    await ctx.addInitScript((e) => { sessionStorage.setItem('hub.devUser', e); sessionStorage.setItem('hub.signedIn', '1') }, `${who}@hub.test`)
    const page = await ctx.newPage()
    await page.goto(`${base}${path}`); await page.locator('main').waitFor(); await page.waitForLoadState('networkidle').catch(() => {}); await page.waitForTimeout(1500)
    const r = await page.evaluate(() => {
      const H = innerHeight
      const vis = el => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && s.opacity !== '0' }
      const main = document.querySelector('main') || document.body
      const fold = el => { const r = el.getBoundingClientRect(); return r.top < H && r.bottom > 0 }
      const rules = {
        old: el => !!el.closest('table, [role=grid], [role=table], [role=treegrid], [role=dialog]'),
        new: el => !!el.closest('table, [role=grid], [role=table], [role=treegrid], [role=dialog]') || (!!el.closest('article, li, [role=listitem]') && !el.closest('nav')),
      }
      const out = {}
      for (const [k, inData] of Object.entries(rules)) {
        const all = [...main.querySelectorAll('button, a[href], input, select, textarea, [role=button], [role=tab], [role=checkbox], [role=combobox], [role=switch]')].filter(vis)
        out[`${k}Controls`] = all.filter(e => !inData(e)).filter(fold).length
        let cw = 0; const tw = document.createTreeWalker(main, NodeFilter.SHOW_TEXT)
        while (tw.nextNode()) { const n = tw.currentNode, p = n.parentElement; if (!p || !vis(p) || inData(p) || !fold(p)) continue; cw += n.textContent.split(/\s+/).filter(Boolean).length }
        out[`${k}Words`] = cw
      }
      return out
    })
    rows.push({ path, ...r }); await ctx.close()
  }
  await browser.close()
  console.table(rows)
  if (outFile) fs.writeFileSync(outFile, JSON.stringify({ base, who, at: new Date().toISOString(), rows }, null, 2))
})()
