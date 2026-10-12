// Finisher pass browser checks for 036-D5 (activity labels) and 036-D6 (chip lag wording). Read-only.
//   TUESDAY_UI_BASE_URL=http://localhost:5199 node finisher-checks.cjs
const fs = require('fs')
const { base, EVID, open, api, shot, settle, close } = require('./lib.cjs')
const RAW = /\b(itemType|field)\.[A-Z]/

;(async () => {
  const out = {}
  // 036-D5, project Activity tab: open-role rows name the item type, the role field and the filling allocation.
  let page = await open('priya', { width: 1280, height: 900 })
  await page.goto(`${base}/projects/SYN-104/activity?itemType=OpenRole`); await settle(page, 1200)
  const project = await page.locator('main table tbody').innerText()
  out.projectActivity = { rawKeys: project.match(new RegExp(RAW, 'g')) ?? [], openRole: project.includes('Open role'), role: /Role:/.test(project),
    filledBy: (project.match(/Filled by: [^\n]+/g) ?? []).slice(0, 3) }
  await shot(page, '036-D5-fixed-project-activity')
  await page.context().close()
  // 036-D5, Admin activity: the home discipline change reads 'Home discipline' with the discipline's name.
  page = await open('jordan', { width: 1280, height: 900 })
  await page.goto(`${base}/admin/activity`); await settle(page, 1200)
  let admin = await page.locator('main table tbody').innerText()
  for (let n = 1; n < 30 && !admin.includes('Home discipline'); n++) { await page.getByRole('button', { name: 'Next' }).click(); await settle(page, 600); admin = await page.locator('main table tbody').innerText() }
  await page.getByText(/Home discipline:/).first().scrollIntoViewIfNeeded().catch(() => {})
  out.adminActivity = { rawKeys: admin.match(new RegExp(RAW, 'g')) ?? [], homeDiscipline: (admin.match(/Home discipline: [^\n]+/g) ?? []).slice(0, 3) }
  await shot(page, '036-D5-fixed-admin-activity')
  await page.context().close()
  // 036-D6: the Finish-to-Finish +1 link on SYN-105 (L6) says 'lag 1 day' in its title and screen-reader text.
  const tasks = (await api('priya', `projects/${(await api('priya', 'projects?q=SYN-105')).body.items[0].id}/tasks?pageSize=200`)).body.items
  const l6 = tasks.find((x) => x.name === 'Drawing checklist')
  page = await open('priya', { width: 1440, height: 1000 })
  await page.goto(`${base}/projects/SYN-105/tasks?panel=Task:${l6.id}`); await settle(page, 1200)
  out.chip = await page.locator('[role=dialog] span[title]', { has: page.locator('span.sr-only') }).evaluateAll((xs) => xs.map((x) => ({ title: x.getAttribute('title'), sr: x.querySelector('.sr-only')?.textContent })))
  await page.context().close()
  console.log(JSON.stringify(out, null, 2))
  fs.writeFileSync(`${EVID}/finisher-checks.json`, JSON.stringify({ base, at: new Date().toISOString(), ...out }, null, 2))
  await close()
})().catch(async (e) => { console.error(e); await close(); process.exitCode = 1 })
