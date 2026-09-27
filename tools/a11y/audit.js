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
