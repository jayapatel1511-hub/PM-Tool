// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

const WIDGETS = ['metrics', 'tasksByStatus', 'tasksByProject', 'upcomingDeadlines']

/**
 * A named workspace of the caller's: one version, nothing hidden by access and no projects unless given.
 * @param {Pick<Schemas['WorkspaceRow'], 'id' | 'name'> & Partial<Schemas['WorkspaceRow']>} r
 * @returns {Schemas['WorkspaceRow']}
 */
const workspaceRow = (r) => ({ projectIds: [], hidden: 0, rowVersion: 1, ...r })

/**
 * The overview layout in the default order with the given widgets hidden.
 * @param {string[]} [hidden]
 * @returns {Schemas['Widget'][]}
 */
const widgetLayout = (hidden = []) => WIDGETS.map((id) => ({ id, hidden: hidden.includes(id) }))

/**
 * What the layout commands answer: the layout and whether it is still the default.
 * @param {Schemas['Widget'][]} layout
 * @param {boolean} isDefault
 * @returns {Schemas['LayoutAnswer']}
 */
const layoutAnswer = (layout, isDefault) => ({ layout, isDefault })

/**
 * One lane of the overview: the lane, the statuses it counts and how many tasks are in it.
 * @param {string} lane
 * @param {number} count
 * @returns {Schemas['HomeLane']}
 */
const homeLane = (lane, count) => ({ lane, statuses: lane, count })

/**
 * One due date on the overview. A milestone has no owner, and a task or deliverable with no state carries no status.
 * @param {Pick<Schemas['HomeDeadline'], 'type' | 'id' | 'projectId' | 'key' | 'name' | 'date'> & Partial<Schemas['HomeDeadline']>} r
 * @returns {Schemas['HomeDeadline']}
 */
const homeDeadline = (r) => ({ projectNumber: 'P-MOCK', status: null, owner: null, overdue: false, ...r })

/**
 * The overview for the three-project scope: figures that add up, one project with its lanes and the deadlines given, the default layout unless given.
 * @param {Partial<Schemas['HomeRead']> & { projectId: string }} r
 * @returns {Schemas['HomeRead']}
 */
const homeRead = ({ projectId, ...r }) => ({
  today: '2026-10-03', from: '2026-10-03', to: '2026-10-16', asOf: '2026-10-03T13:00:00+00:00',
  metrics: { activeProjects: 3, openTasks: 12, overdueTasks: 2, onTrack: { green: 2, evaluated: 3, pct: 66 }, teamMembers: 5 },
  tasksByStatus: [homeLane('To Do', 5), homeLane('In Progress', 4), homeLane('Review', 1), homeLane('Done', 7)],
  onHold: { status: 'On Hold', count: 2 },
  tasksByProject: [{ projectId, projectNumber: 'P-MOCK', name: 'Synthetic live project', lanes: [homeLane('To Do', 5), homeLane('In Progress', 4), homeLane('Review', 1), homeLane('Done', 0)], total: 10 }],
  upcomingDeadlines: { total: 0, truncated: false, items: [] },
  layout: widgetLayout(), isDefault: true, ...r,
})

/**
 * A project's links in the library: the project's own link, with the project the caller may add to.
 * @param {Pick<Schemas['FileProjectRow'], 'projectId'> & Partial<Schemas['FileProjectRow']>} r
 * @returns {Schemas['FileProjectRow']}
 */
const fileProject = (r) => ({ projectNumber: 'P-MOCK', name: 'Synthetic live project', canAdd: false, count: 0, items: [], ...r })

/**
 * A link in the library. A network path is flagged, and a link whose adder is gone has no name.
 * @param {Pick<Schemas['FileLinkRow'], 'id' | 'title' | 'url'> & Partial<Schemas['FileLinkRow']>} r
 * @returns {Schemas['FileLinkRow']}
 */
const fileLink = (r) => ({ linkType: 'Other', isNetworkPath: false, addedBy: 'Priya Nair', addedAt: '2026-10-01T09:00:00+00:00', ...r })

/**
 * The library for the scope: the projects given, the number of links shown and the link types the filter offers.
 * @param {Partial<Schemas['FilesRead']>} [r]
 * @returns {Schemas['FilesRead']}
 */
const filesRead = (r = {}) => ({ projects: [], total: 0, truncated: false, types: ['SharePoint', 'OneDrive', 'Teams', 'Network Folder', 'External DMS', 'Client Portal', 'Other'], ...r })

/**
 * A person on the scope's team: no job title unless given, with the projects they are active on.
 * @param {Pick<Schemas['TeamPersonRow'], 'userId' | 'displayName'> & Partial<Schemas['TeamPersonRow']>} r
 * @returns {Schemas['TeamPersonRow']}
 */
const teamPerson = (r) => ({ jobTitle: null, email: `${r.displayName.toLowerCase().replace(/\W+/g, '.')}@example.test`, projects: [], ...r })

/**
 * The team for the scope.
 * @param {Schemas['TeamPersonRow'][]} people
 * @param {number} projects
 * @returns {Schemas['TeamRead']}
 */
const teamRead = (people, projects) => ({ people, projects })

module.exports = { workspaceRow, widgetLayout, layoutAnswer, homeLane, homeDeadline, homeRead, fileProject, fileLink, filesRead, teamPerson, teamRead }
