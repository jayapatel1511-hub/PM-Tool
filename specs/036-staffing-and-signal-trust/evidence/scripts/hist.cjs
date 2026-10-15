const fs = require('node:fs')
const { open, shot, settle, close, base, api } = require('./lib.cjs')
;(async () => {
  const h = (await api('jordan', 'admin/activity?itemType=User&pageSize=200')).body
  const change = h.items.flatMap((a) => (a.changes ?? []).filter((c) => c.field === 'HomeDisciplineId').map((c) => ({ item: a.itemName ?? a.itemId, action: a.action, old: c.old, new: c.new, at: a.occurredAt })))
  const page = await open('jordan', { width: 1600, height: 1000 })
  await page.goto(`${base}/admin/activity?itemType=User`); await settle(page, 1800)
  const txt = await page.locator('main').innerText()
  await shot(page, 'AC-GRP-01a-history')
  console.log(JSON.stringify({ apiChanges: change, pageMentions: /Home discipline|HomeDiscipline/i.test(txt), snippet: txt.slice(txt.search(/Home discipline/i) - 150, txt.search(/Home discipline/i) + 150) }))
  const file = `${__dirname}/results-part2.json`; const r = JSON.parse(fs.readFileSync(file, 'utf8'))
  r['AC-GRP-01a-B'].history = { apiChanges: change, pageShowsIt: /Home discipline|HomeDiscipline/i.test(txt), screenshot: 'AC-GRP-01a-history.png' }
  fs.writeFileSync(file, JSON.stringify(r, null, 1))
  await close()
})()
