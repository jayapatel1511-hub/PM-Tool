// @ts-check
/** Synthetic transport data only. Provider tests own actual contribution persistence, permissions and audit isolation. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView } = require('./me.fixture.cjs')
const { taskRow, taskDetail, taskPermissions, taskPage, taskTransition } = require('./tasks.fixture.cjs')
const { projectDetail, projectDiscipline, projectList, projectRow } = require('./projects.fixture.cjs')
const { taskDependencies, dependencyEdge, dependencyTask } = require('./dependencies.fixture.cjs')
/** @param {number} n */
const id = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0')
const ids = { viewer: id(98101), owner: id(98102), pat: id(98103), inactive: id(98104), project: id(98110), discipline: id(98111), task: id(98120), secondTask: id(98121), dependency: id(98122), membership: id(98130), otherMembership: id(98131) }
const stamp = '2026-11-02T15:00:00Z'
const project = projectDetail({ id: ids.project, projectNumber: 'P-SYN-S16', name: 'Synthetic task sections', projectManagerId: ids.owner, pmName: 'Synthetic Alex Owner', disciplines: [projectDiscipline({ id: ids.discipline, disciplineId: ids.discipline, name: 'Synthetic Civil' })] })
/** @param {Partial<Schemas['TaskContributionRead']>} [overrides] @returns {Schemas['TaskContributionRead']} */
const contribution = (overrides = {}) => ({ collaboratorId: ids.membership, rowVersion: 0, markedAt: null, note: null, canMark: true, canWithdraw: false, refusalReason: null, ...overrides })
/** @param {{work?:boolean, people?:boolean, small?:boolean, own?:boolean, readonly?:boolean}} [options] */
function world(options = {}) {
  const flags = { work_screens: options.work !== false, people_screens: options.people !== false, small_ux: options.small !== false,
    data_entry_safety: true, screen_patterns: false, capacity_screens: false, visual_system: false, task_pickup: false, live_events: false, onboarding: false, org_structure: false }
  const actor = meView({ id: ids.viewer, displayName: 'Synthetic Jill Collaborator', email: 'synthetic.jill@example.test', systemRoles: ['StandardUser'], flags,
    capabilities: { readOnly: options.readonly ?? false, workload: false }, settings: { today: '2026-11-02', allowSelfReview: false } })
  let state = contribution(options.readonly ? { canMark: false, refusalReason: 'Synthetic project is read-only' } : {})
  let own = options.own !== false, complete = false, parentName = 'Synthetic section task', parentVersion = 17, portalActions = false
  /** @type {Schemas['TaskContributionSetBody'][]} */
  const commands = []
  /** @param {string} [taskId] @returns {Schemas['TaskDetailResponse']} */
  const detail = (taskId = ids.task) => taskDetail({ task: taskRow({ id: taskId, projectId: ids.project, projectNumber: project.projectNumber, projectName: project.name,
    projectDisciplineId: ids.discipline, disciplineName: 'Synthetic Civil', key: taskId === ids.task ? 'S16-T001' : 'S16-T002', name: parentName, assigneeId: ids.owner, assigneeName: 'Synthetic Alex Owner',
    reviewerId: ids.pat, reviewerName: 'Synthetic Pat Reviewer', requiresReview: true, estimatedHours: 12.5, progressPct: 30, status: 'In Progress', projectStatus: complete ? 'Complete' : 'Active', rowVersion: parentVersion, commentCount: 1,
    startDate: '2026-11-02', dueDate: '2026-11-20', collaboratorIds: own ? [ids.viewer, ids.pat] : [ids.pat] }),
    description: Array.from({ length: 12 }, (_, index) => `Synthetic design context ${index + 1}: retain source assumptions, discipline coordination and the current project record.`).join('\n\n'),
    permissions: taskPermissions({ needsReason: complete, delete: { ok: false, reason: 'Synthetic delete not in scope' }, transitions: portalActions ? [taskTransition('On Hold', { needsReason: true }), taskTransition('Cancelled', { needsReason: true })] : [], dependencies: false }),
    collaborators: [...(own ? [{ id: ids.viewer, displayName: actor.displayName, isActive: true, ...(flags.work_screens ? { contribution: { ...state } } : {}) }] : []),
      { id: ids.pat, displayName: 'Synthetic Pat Reviewer', isActive: true, ...(flags.work_screens ? { contribution: contribution({ collaboratorId: ids.otherMembership, rowVersion: 3, markedAt: stamp, note: 'Synthetic peer contribution', canMark: false, canWithdraw: false, refusalReason: 'Only your own contribution can change' }) } : {}) }],
  })
  /** @param {Schemas['TaskContributionSetBody']} body @returns {Schemas['TaskContributionRead']} */
  const apply = body => {
    commands.push({ ...body })
    state = contribution({ collaboratorId: state.collaboratorId, rowVersion: state.rowVersion + 1, markedAt: body.marked ? stamp : null, note: body.marked ? body.note?.trim() || null : null, canMark: !body.marked, canWithdraw: body.marked })
    return { ...state }
  }
  /** @param {string} user @param {string} name @returns {Schemas['PersonPanelRead']} */
  const person = (user, name) => ({ id: user, displayName: name, isActive: user !== ids.inactive, jobTitle: 'Synthetic project colleague', today: '2026-11-02', week: '2026-11-02', asOf: stamp,
    organisationFacts: { homeDiscipline: null, office: null, supervisor: null, unit: null }, projects: { items: [], page: 1, pageSize: 25, total: 0 } })
  const dependencies = taskDependencies([dependencyEdge({ dependencyId: id(98140), task: dependencyTask({ id: ids.dependency, key: 'S16-T000', name: 'Synthetic preceding design input', assigneeId: ids.pat, assigneeName: 'Synthetic Pat Reviewer' }) })])
  /** @type {Schemas['CommentListResponse']} */
  const comments = { canComment: false, commentReason: 'Synthetic discussion is read-only in this scenario', items: [{ id: id(98141), authorId: ids.pat, authorName: 'Synthetic Pat Reviewer', commentKind: 'General', reviewRound: null,
    editedAt: null, createdAt: stamp, deleted: false, deletedByPm: false, body: 'Synthetic retained task discussion with actual author and text.', mentions: [], canEdit: false, canDelete: false }] }
  /** @type {Schemas['LinkListResponse']} */
  const links = { canAdd: false, inherited: [], links: [{ id: id(98142), title: 'Synthetic design reference', url: 'https://example.test/design-reference', linkType: 'Other', rowVersion: 1,
    isNetworkPath: false, addedBy: ids.pat, addedByName: 'Synthetic Pat Reviewer', addedAt: stamp, canChange: false }] }
  return { actor, flags, project, dependencies, comments, links, detail, apply, commands, person,
    tasks: () => taskPage([detail().task, detail(ids.secondTask).task]),
    projects: projectList([projectRow({ id: project.id, projectNumber: project.projectNumber, name: project.name })]),
    current: () => ({ ...state }),
    /** @param {Partial<Schemas['TaskContributionRead']>} change */
    change: change => { state = contribution({ ...state, ...change }) },
    /** @param {boolean} present */
    membership: present => { own = present },
    /** @param {boolean} value */
    correction: value => { complete = value },
    /** @param {boolean} value */
    portalCommands: value => { portalActions = value },
    /** @param {number} value */
    version: value => { parentVersion = value },
    parent: () => ({ parentName, parentVersion, status: detail().task.status, assigneeId: ids.owner, progressPct: 30 }),
  }
}
module.exports = { world, ids, id, stamp, contribution }
