const path = require('node:path')
const fs = require('node:fs')
const root = path.resolve(__dirname, '../../../..')
const { newMockPage, me, project } = require(path.join(root, 'web/tests/platform-fixtures.cjs'))
const { remainingFixture } = require(path.join(root, 'web/tests/person-remaining-surfaces.fixture.cjs'))
const f = remainingFixture(project), observations = []
;(async () => {
  Object.assign(me.flags, Object.fromEntries(Object.keys(me.flags).map(key => [key, false])), { people_screens: true, visual_system: true, capacity_screens: true, data_entry_safety: true })
  me.capabilities.workload = false
  const { page, browser } = await newMockPage('http://127.0.0.1:8853', { events: false })
  try {
    await page.route('**/api/**', async route => {
      const p = new URL(route.request().url()).pathname.replace('/api/v1/', '')
      if (p === 'home') return route.fulfill({ json: f.home })
      if (/^people\/[^/]+\/panel$/.test(p)) { const id = p.split('/')[1]; return route.fulfill({ json: f.person(id, id === me.id ? me.displayName : f.names.get(id)) }) }
      return route.fallback()
    })
    const capture = async stage => {
      observations.push(await page.evaluate(stage => ({ stage, url: location.href, state: history.state,
        active: { id: document.activeElement?.id, tag: document.activeElement?.tagName, text: document.activeElement?.textContent?.slice(0, 120) },
        account: document.querySelector('#account-menu-trigger')?.outerHTML, menus: document.querySelectorAll('[role="menu"]').length,
        dialogs: [...document.querySelectorAll('[role="dialog"]')].map(node => ({ state: node.getAttribute('data-state'), hidden: node.getAttribute('aria-hidden') })) }), stage))
    }
    await page.goto('http://127.0.0.1:8853/home?projects=all')
    const greeting = page.locator('[data-person-panel-trigger="' + me.id + '"]').first()
    await greeting.click(); await page.locator('[data-person-panel]').waitFor(); await page.keyboard.press('Escape')
    await page.waitForURL('http://127.0.0.1:8853/home?projects=all')
    await page.waitForFunction(id => document.activeElement?.id === id, await greeting.getAttribute('id'))
    await page.getByRole('button', { name: 'Account', exact: true }).click()
    await page.getByRole('menuitem', { name: me.displayName, exact: true }).click()
    await page.locator('[data-person-panel]').waitFor(); await page.getByRole('menu').waitFor({ state: 'detached' }); await capture('Person open from account')
    await page.keyboard.press('Escape'); await page.waitForURL('http://127.0.0.1:8853/home?projects=all')
    await page.waitForFunction(() => document.activeElement?.id === 'account-menu-trigger', null, { timeout: 3000 }).catch(() => {})
    await capture('Returned from account Person')
    fs.writeFileSync(path.join(__dirname, 'account-focus-repro1.json'), JSON.stringify(observations, null, 2) + '\n')
    console.log(JSON.stringify(observations))
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
