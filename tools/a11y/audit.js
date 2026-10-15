// Accessibility audit (packet 011 FR-002): WCAG 2.0/2.1 A and AA rules from axe-core on each screen.
// Run in the browser console of the development app (npm run dev, signed in), e.g. once per role:
//   const m = await import('/@fs' + '/<repo>/tools/a11y/audit.js'); await m.audit(m.ROUTES)
// or paste the file's body and call audit(ROUTES). Results list each route with its violations, or OK.
export const ROUTES = ['/my-work', '/my-work?tab=today', '/my-work?tab=inbox', '/home', '/boards', '/tasks', '/timeline', '/calendar', '/calendar?mode=agenda',
  '/files', '/team', '/time', '/reports', '/reports/tasks-due-this-week', '/projects', '/notifications', '/search?q=road', '/preferences', '/portfolio', '/workload',
  '/staff', '/templates', '/admin/users', '/admin/settings', '/admin/activity', '/admin/operations', '/admin/holidays']

export async function audit(routes, project) {
  if (!window.axe) await new Promise((ok, fail) => { const s = document.createElement('script'); s.src = '/node_modules/axe-core/axe.min.js'; s.onload = ok; s.onerror = fail; document.head.appendChild(s) })
  const all = project ? [...routes, ...['dashboard', 'coordination', 'tasks', 'board', 'deliverables', 'milestones', 'timeline', 'decisions', 'decisions?view=log', 'risks', 'issues', 'meetings',
    'files', 'calendar', 'team', 'activity', 'settings'].map((t) => `/projects/${project}/${t}`)] : routes
  const out = []
  for (const r of all) {
    history.pushState({}, '', r); dispatchEvent(new PopStateEvent('popstate'))
    await new Promise((ok) => setTimeout(ok, 1500))
    const res = await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] }, resultTypes: ['violations'] })
    out.push(`${r} => ${res.violations.map((v) => `${v.id} (${v.impact}) x${v.nodes.length}: ${v.nodes[0].target.join(' ')}`).join('; ') || 'OK'}`)
  }
  return out
}

// The packet acceptance command is also runnable from Node against the local synthetic preview.
// Browser-console imports retain the audit() API above.
if (typeof window === 'undefined') {
  const { createRequire } = await import('node:module')
  const { fileURLToPath } = await import('node:url')
  const { dirname, resolve } = await import('node:path')
  const require = createRequire(import.meta.url)
  const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
  const { chromium } = require(resolve(root, 'web/node_modules/playwright'))
  const axePath = require.resolve(resolve(root, 'web/node_modules/axe-core/axe.min.js'))
  const origin = process.env.AUDIT_BASE ?? 'http://localhost:5173'
  const url = new URL(origin)
  if (!['localhost', '127.0.0.1'].includes(url.hostname)) throw new Error('Accessibility CLI is restricted to a local preview')
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const results = []
  try {
    for (const user of (process.env.AUDIT_USERS ?? 'priya,sam,lena,jordan,alex,rita').split(',')) {
      for (const width of [1440, 1024, 375]) {
        const context = await browser.newContext({ viewport: { width, height: 900 } })
        await context.addInitScript((who) => { sessionStorage.setItem('hub.devUser', who + '@hub.test'); sessionStorage.setItem('hub.signedIn', '1') }, user)
        const page = await context.newPage(); const errors = []
        page.on('pageerror', (error) => errors.push(error.message))
        for (const route of (process.argv.slice(2).length ? process.argv.slice(2) : ['/planner', '/my-work'])) {
          errors.length = 0
          await page.goto(origin + route); await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {})
          await page.addScriptTag({ path: axePath })
          const violations = await page.evaluate(async () => (await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] } })).violations.map((v) => ({ id: v.id, targets: v.nodes.map((n) => n.target) })))
          results.push({ user, width, route, errors: [...errors], violations })
        }
        await context.close()
      }
    }
  } finally { await browser.close() }
  console.log(JSON.stringify(results, null, 2))
  if (results.some((r) => r.errors.length || r.violations.length)) process.exitCode = 1
}
