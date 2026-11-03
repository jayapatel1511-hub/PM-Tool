// @ts-check
/** Synthetic wire/receipt model for UI behavior. API tests own cohort, transaction and race proof. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const assert = require('node:assert/strict')
const { meView, referenceData } = require('./me.fixture.cjs')
const { myWork, myWorkPerson, myWorkProject } = require('./my-work.fixture.cjs')
const { taskRow, taskState, taskDetail, taskPermissions, taskPage } = require('./tasks.fixture.cjs')
const { projectDetail, projectDiscipline, projectRow, projectList } = require('./projects.fixture.cjs')
const { deliverableRow, deliverableState, deliverableDetail } = require('./deliverables.fixture.cjs')
const { comments } = require('./comments.fixture.cjs')
/** @param {number} n */
const id = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0')
const ids = { viewer: id(9100), pat: id(9101), other: id(9102), project: id(9110), discipline: id(9111), task: id(9120), review: id(9121), created: id(9122), zero: id(9123), successor: id(9124), blocker: id(9125), deliverable: id(9130), milestone: id(9131), decision: id(9132), action: id(9133), event: id(9140) }
const tokens = { assignee: id(9200), sameTaskReviewer: id(9201), reviewer: id(9202), deliverable: id(9203), zero: id(9204), replacement: id(9205), late: id(9206) }
const at = '2026-11-02T12:00:00Z'
const viewerName = 'Synthetic Assigned Viewer', patName = 'Synthetic Pat'
const project = projectDetail({ id: ids.project, projectNumber: 'P-SYN-SEEN', name: 'Synthetic seen-state project', projectManagerId: ids.pat, pmName: patName,
  disciplines: [projectDiscipline({ id: ids.discipline, disciplineId: ids.discipline, name: 'Synthetic Civil' })] })
const reference = referenceData()
const projectRows = projectList([projectRow({ id: project.id, projectNumber: project.projectNumber, name: project.name, pm: { id: ids.pat, displayName: patName } })])
/** @param {Partial<Schemas['WorkActivityFact']>} [r] @returns {Schemas['WorkActivityFact']} */
const activity = (r = {}) => ({ eventId: ids.event, occurredAt: '2026-11-02T10:00:00Z', actorId: ids.pat, actorName: patName, actorType: 'User', commentable: true, commentCount: 3, ...r })
/** @param {{enrolled?:boolean, readOnly?:boolean, nativeDialog?:boolean, duplicateWireTokens?:boolean}} [options] */
function world(options = {}) {
  const actor = meView({ id: ids.viewer, displayName: viewerName, email: 'synthetic.viewer@example.test', systemRoles: ['StandardUser'],
    capabilities: { readOnly: options.readOnly ?? false, workload: false, createTask: false },
    settings: { today: '2026-11-02', allowSelfReview: true }, flags: { work_screens: true, people_screens: true, visual_system: false, screen_patterns: false,
      capacity_screens: false, capacity_polish: false, home_overview: false, my_work_follow_up: false, task_pickup: false, live_events: false, onboarding: false, org_structure: false } })
  const acknowledged = new Set()
  /** @type {Map<string, Schemas['WorkSeenObservedToken'][]>} */
  const current = new Map([
    ['Task:' + ids.task, [{ token: tokens.assignee, role: 'Assignee', sourceVersion: 1 }, { token: tokens.sameTaskReviewer, role: 'Reviewer', sourceVersion: 1 }]],
    ['Task:' + ids.review, [{ token: tokens.reviewer, role: 'Reviewer', sourceVersion: 1 }]],
    ['Task:' + ids.zero, [{ token: tokens.zero, role: 'Assignee', sourceVersion: 0 }]],
    ['Deliverable:' + ids.deliverable, [{ token: tokens.deliverable, role: 'Owner', sourceVersion: 1 }]],
  ])
  /** @type {Map<string,{signature:string,result:Schemas['WorkSeenCommandResult']}>} */
  const receipts = new Map()
  let firstVisit = options.enrolled === false ? null : '2026-11-01T12:00:00Z'
  let lastVisit = firstVisit, revision = 0, tick = 0
  /** @type {string | null} */
  let previousVisit = null
  /** @type {string | null} */
  let lastMarkAll = null
  /** @param {string} type @param {string} itemId @returns {Schemas['WorkSeenDecoration'] | undefined} */
  const seen = (type, itemId) => {
    if (!actor.flags?.work_screens) return undefined
    const observed = current.get(type + ':' + itemId)
    return observed ? { state: !firstVisit ? 'Unenrolled' : observed.some(t => !acknowledged.has(t.token)) ? 'New' : 'Seen',
      observedTokens: options.duplicateWireTokens ? [...observed.slice().reverse(), observed[0]] : observed } : undefined
  }
  /** @param {string} type @param {string} itemId @param {boolean} [own] */
  const decorate = (type, itemId, own = true) => actor.flags?.work_screens ? { ...(own && seen(type, itemId) ? { seen: seen(type, itemId) } : {}),
    workActivity: activity(type === 'Project' ? { commentable: false, commentCount: null, actorType: 'System', actorId: null, actorName: null } : itemId === ids.created ? { commentCount: 0 } : {}) } : {}
  /** @param {string} itemId @param {boolean} [own] */
  const task = (itemId, own = true) => {
    const creator = itemId === ids.created, zero = itemId === ids.zero, review = itemId === ids.review
    const observed = current.get('Task:' + itemId), sourceVersion = zero ? 0 : Math.max(1, ...(observed?.map(t => t.sourceVersion) ?? []))
    return { ...taskRow({ id: itemId, projectId: ids.project, key: creator ? 'S-CREATED' : zero ? 'S-ZERO' : review ? 'S-REVIEW' : 'S-ASSIGNED',
      name: creator ? 'Synthetic creator-only work' : zero ? 'Synthetic legacy zero-version work' : review ? 'Synthetic reviewer work' : 'Synthetic assigned work', projectNumber: project.projectNumber,
      projectName: project.name, projectDisciplineId: ids.discipline, disciplineName: 'Synthetic Civil', dueDate: '2026-11-02', status: creator || zero ? 'Not Started' : 'In Review',
      assigneeId: creator || review ? ids.pat : ids.viewer, assigneeName: creator || review ? patName : viewerName,
      reviewerId: creator || zero ? null : ids.viewer, reviewerName: creator || zero ? null : viewerName, requiresReview: !creator && !zero,
      createdBy: ids.viewer, createdByName: viewerName, rowVersion: sourceVersion,
      // Deliberately unrelated cached time/actor: FR15 must use the exact event envelope instead.
      lastActivityAt: '2026-10-01T00:00:00Z', lastActivityActorName: 'Synthetic cached actor must not appear',
      can: { assign: false, edit: false, changeDue: false, block: !!options.nativeDialog },
      state: taskState({ isUnassigned: false, isMissingDueDate: false, isWaiting: itemId === ids.task, isBlocked: itemId === ids.task, isBlocking: itemId === ids.task,
        blockedBy: itemId === ids.task ? [{ type: 'Task', id: ids.blocker, key: 'S-BLOCKER', name: 'Synthetic blocker', ownerId: ids.pat, blocking: true, overdue: false }] : [],
        blockingTaskIds: itemId === ids.task ? [ids.successor] : [] }) }), ...decorate('Task', itemId, own) }
  }
  /** @param {boolean} [own] */
  const deliverable = (own = true) => ({ ...deliverableRow({ id: ids.deliverable, projectId: ids.project, projectDisciplineId: ids.discipline, key: 'S-DRAWING', name: 'Synthetic drawing discussion',
    ownerId: ids.viewer, ownerName: viewerName, dueDate: '2026-11-02', state: deliverableState() }), ...decorate('Deliverable', ids.deliverable, own) })
  /** @param {string | null} [subject] @returns {Schemas['MyWorkView']} */
  const work = (subject = null) => {
    const own = !subject || subject === ids.viewer
    return myWork({ person: myWorkPerson(own ? ids.viewer : subject, own ? viewerName : 'Synthetic other person'), readOnly: !own || !!actor.capabilities.readOnly,
      today: '2026-11-02', tasks: [task(ids.task, own), task(ids.created, own)], reviews: { tasks: [task(ids.task, own), task(ids.review, own)], deliverables: [] },
      deliverables: [deliverable(own)], people: { [ids.pat]: patName },
      successors: [{ id: ids.successor, key: 'S-SUCCESSOR', name: 'Synthetic successor', status: 'Not Started', dueDate: null, assigneeName: patName,
        ...(actor.flags?.people_screens ? { assigneeId: ids.pat } : {}) }],
      projects: [{ ...myWorkProject({ id: project.id, projectNumber: project.projectNumber, name: project.name }), ...decorate('Project', ids.project, own) }],
      milestones: [{ id: ids.milestone, projectId: ids.project, projectNumber: project.projectNumber, key: 'S-MILESTONE', name: 'Synthetic unknown event', date: '2026-11-05', status: 'Not Started', milestoneType: 'Internal',
        ...(actor.flags?.work_screens ? { workActivity: activity({ eventId: null, occurredAt: null, actorId: null, actorName: null, actorType: null, commentCount: null }) } : {}) }] })
  }
  /** @param {string} path @param {Record<string,any>} body @returns {Schemas['WorkSeenCommandResult']} */
  const command = (path, body) => {
    assert.match(body.requestId, /^[0-9a-f-]{36}$/)
    const opened = path.endsWith('/opened')
    assert.deepEqual(Object.keys(body).sort(), opened ? ['itemId', 'itemType', 'observedTokens', 'requestId'] : ['requestId'])
    const normalized = opened ? { itemType: body.itemType, itemId: body.itemId, observedTokens: [...new Set(body.observedTokens)].sort() } : {}
    const signature = JSON.stringify({ path, ...normalized }), prior = receipts.get(body.requestId)
    if (prior) { assert.equal(signature, prior.signature, 'Request ID never changes payload/action'); return prior.result }
    const now = new Date(Date.parse(at) + (++tick) * 1000).toISOString()
    if (path.endsWith('/visits')) {
      if (!firstVisit) { for (const token of [...current.values()].flat()) acknowledged.add(token.token); firstVisit = now }
      previousVisit = lastVisit; lastVisit = now
    } else if (path.endsWith('/seen-all')) {
      assert.ok(firstVisit)
      for (const token of [...current.values()].flat()) acknowledged.add(token.token)
      lastMarkAll = now
    } else {
      const eligible = current.get(body.itemType + ':' + body.itemId) ?? []
      for (const token of body.observedTokens) { assert.ok(eligible.some(t => t.token === token), 'Only displayed current tokens can be acknowledged'); acknowledged.add(token) }
    }
    const result = { requestId: body.requestId, committedAt: now, revision: ++revision, firstVisitAt: firstVisit, previousVisitAt: previousVisit, lastVisitAt: lastVisit, lastMarkAllAt: lastMarkAll }
    receipts.set(body.requestId, { signature, result }); return result
  }
  /** @param {string} token */
  const replace = token => current.set('Task:' + ids.task, [{ token, role: 'Assignee', sourceVersion: 2 }, ...(current.get('Task:' + ids.task) ?? []).filter(t => t.role === 'Reviewer')])
  const thread = { ...comments, canComment: false, commentReason: 'Synthetic read-only discussion', items: Array.from({ length: 3 }, (_, n) => ({ ...comments.items[0], id: id(9300 + n), authorId: ids.pat,
    authorName: patName, body: 'Synthetic exact drawing comment ' + (n + 1), mentions: [], canEdit: false, canDelete: false, deleted: false })) }
  return { actor, current, receipts, acknowledged, seen, work, task, deliverable, command, replace, thread,
    list: (created = false, own = true) => taskPage(created ? [task(ids.created, own)] : [task(ids.task, own), task(ids.review, own), task(ids.created, own)]),
    taskDetail: (/** @type {string} */ itemId) => ({ ...taskDetail({ task: task(itemId), permissions: taskPermissions({ edit: { ok: false, reason: 'Synthetic read only' }, assign: { ok: false, reason: null }, setReviewer: { ok: false, reason: null },
      dueDate: { ok: false, reason: null }, block: { ok: !!options.nativeDialog, reason: null }, delete: { ok: false, reason: null }, dependencies: false, restore: false, enterTime: false, comment: false }) }), ...(seen('Task', itemId) ? { seen: seen('Task', itemId) } : {}) }),
    deliverableDetail: () => ({ ...deliverableDetail({ deliverable: deliverable(), project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: 'Active' } }), ...(seen('Deliverable', ids.deliverable) ? { seen: seen('Deliverable', ids.deliverable) } : {}) }) }
}
module.exports = { id, ids, tokens, at, viewerName, patName, project, reference, projectRows, activity, world }
