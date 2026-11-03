// Synthetic /me capability contracts; UI probes do not establish server admission.
const main = ['/my-work', '/home', '/projects', '/tasks', '/planner', '/time', '/reports', '/team']
const profile = (name, roles, capabilities) => ({ name, roles, capabilities,
  hrefs: [...main.filter(path => path !== '/planner' || capabilities.workload), '/files', ...(capabilities.staff ? ['/staff'] : []), ...(capabilities.templates ? ['/templates'] : []), ...(capabilities.admin ? ['/admin'] : [])] })
const profiles = [
  profile('Admin', ['Admin'], { admin: true, staff: true, templates: true, workload: true, portfolio: true }),
  profile('Executive', ['Executive'], { staff: true, workload: true, portfolio: true }),
  profile('Supervisor', ['Supervisor'], { staff: true, workload: true, portfolio: true }),
  profile('Project Manager', ['ProjectManager'], { workload: true, portfolio: true }),
  profile('Team member', [], {}),
  profile('Read Only', ['ReadOnly'], { readOnly: true }),
  profile('Unit leader capability', [], { staff: true, workload: true }),
  profile('Template editor capability', [], { templates: true }),
  profile('Read Only project manager', ['ProjectManager', 'ReadOnly'], { workload: true, portfolio: true, readOnly: true }),
]
const projectNumber = 'NAV-001'
const personId = '00000000-0000-0000-0000-000000000002'
const taskId = '00000000-0000-0000-0000-000000000001'
const cases = [
  { pattern: '', path: '/', settled: '/my-work', parent: '/my-work' },
  ...[['my-work', '/my-work'], ['home', '/home'], ['projects', '/projects'], ['boards', '/tasks'], ['tasks', '/tasks'], ['timeline', '/tasks'], ['calendar', '/tasks'], ['files', '/files'], ['team', '/team'], ['time', '/time'], ['reports', '/reports'], ['organisation', '/team'], ['pickup', '/tasks']].map(([pattern, parent]) => ({ pattern, path: `/${pattern}`, parent })),
  { pattern: 'tasks/:id', path: `/tasks/${taskId}`, parent: '/tasks' },
  { pattern: 'reports/:code', path: '/reports/overdue-tasks', parent: '/reports' },
  ...['planner', 'workload'].map(pattern => ({ pattern, path: `/${pattern}`, parent: '/planner', capability: 'workload' })),
  { pattern: 'vacation', path: '/vacation', vacation: true },
  { pattern: 'portfolio', path: '/portfolio', parent: '/projects', capability: 'portfolio' },
  { pattern: 'staff', path: '/staff', parent: '/staff', capability: 'staff' },
  { pattern: 'people/:id/reassign-work', path: `/people/${personId}/reassign-work`, parent: '/staff', capability: 'staff' },
  ...['templates', 'templates/:id'].map(pattern => ({ pattern, path: pattern.includes(':id') ? `/templates/${personId}` : '/templates', parent: '/templates', capability: 'templates' })),
  ...['admin', 'admin/reference/:kind', 'admin/settings', 'admin/users', 'admin/activity', 'admin/operations', 'admin/holidays', 'admin/structure', 'admin/audit-chains', 'admin/review'].map(pattern => ({ pattern, path: `/${pattern.replace(':kind', 'disciplines')}`, parent: '/admin', capability: 'admin' })),
  // Jay confirmed 052-D12: these utility pages intentionally select no sidebar item.
  ...['notifications', 'search', 'preferences'].map(pattern => ({ pattern, path: `/${pattern}`, parent: null, utilityNoParent: true })),
  { pattern: '*', path: '/unregistered-route-probe', parent: null, errorSurface: true },
  { pattern: 'projects/:number', path: `/projects/${projectNumber}`, settled: `/projects/${projectNumber}/dashboard`, parent: '/projects', group: 'Overview' },
]
const groups = {
  Overview: ['dashboard'], Work: ['tasks', 'board', 'timeline', 'calendar', 'milestones'],
  Deliverables: ['deliverables', 'submissions', 'reviews', 'readiness'],
  Coordination: ['coordination', 'handoffs', 'changes', 'design-basis', 'decisions', 'risks', 'issues', 'meetings'],
  People: ['team', 'allocations'], Files: ['files'], Activity: ['activity'], Settings: ['settings'],
}
for (const [group, children] of Object.entries(groups)) for (const child of children) cases.push({ pattern: `projects/:number/${child}`, path: `/projects/${projectNumber}/${child}`, parent: '/projects', group })
cases.push({ pattern: 'projects/:number/vacation', path: `/projects/${projectNumber}/vacation`, parent: '/projects', group: 'People', vacation: true })
module.exports = { profiles, cases, projectNumber, personId, taskId }
