import { test } from 'node:test'
import assert from 'node:assert/strict'
import { invalidateChange, invalidateNotifications, invalidatePreferences, invalidateWorkSeen, keysForChange, refreshComments, refreshHome, refreshProject, refreshWorkspaces } from './keys.ts'
import { QueryClient } from '@tanstack/react-query'

test('a contribution child refreshes mounted collaborator facts and complete history actors without pretending task workflow changed', async () => {
  const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
  const readers = [['task', 'task-id', 'viewer', true, true], ['task-history-actors', 'task-id', 'viewer', true], ['history', 'Task', 'task-id']] as const
  const unaffected = [['wtasks', { projectId: 'project' }], ['workload'], ['preferences']] as const
  try {
    for (const key of [...readers, ...unaffected]) qc.setQueryData(key, { cut: 'before' })
    await invalidateChange(qc, 'TaskContribution', 'project', 'contribution-id')
    for (const key of readers) {
      let reads = 0
      const data = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { cut: 'current contribution' } } })
      assert.equal(reads, 1, key.join(':')); assert.equal(data.cut, 'current contribution')
    }
    for (const key of unaffected) assert.equal(qc.getQueryState(key)?.isInvalidated, false, key.join(':'))
  } finally { qc.clear() }
})

test('notification action facts refresh on record, graph, access and untyped evaluation changes without broadening legacy readers', async () => {
  for (const type of ['ReviewFinding', 'DisciplineReview', 'FindingEvent', 'Decision', 'Issue', 'Handoff', 'SubmissionCheck', 'WorkConstraint', 'Dependency', 'SourceRevision', 'Project', 'Member', 'User', 'Setting', undefined]) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    const screen = ['notifications', 'screen', 'viewer', true, { needsAction: true }] as const
    const legacy = [['notifications', 'list', {}], ['notifications', 'recent'], ['preferences']] as const
    try {
      qc.setQueryData(screen, { totalCount: 1, items: [{ needsYourAction: true }] })
      for (const key of legacy) qc.setQueryData(key, { cut: 'before' })
      await invalidateChange(qc, type, 'muted-project')
      let reads = 0
      const current = await qc.fetchQuery({ queryKey: screen, queryFn: async () => { reads++; return { totalCount: 0, items: [] } } })
      assert.equal(reads, 1, type ?? 'untyped evaluation')
      assert.equal(current.totalCount, 0)
      for (const key of legacy) assert.equal(qc.getQueryState(key)?.isInvalidated, false, `${type}: ${key.join(':')}`)
    } finally { qc.clear() }
  }
})

test('decision, issue and template derived reads refresh after metadata and access changes', async () => {
  // The viewer and owning flags are in these actual mounted keys. A previously fresh
  // cohort must refetch after a command or visibility change, without clearing preferences.
  const readers: [readonly unknown[], string[]][] = [
    [['decision-screen', 'project', 'viewer', true], ['Decision', 'ExternalParty', 'ReferenceData', 'Setting', 'Project', 'Member', 'User']],
    [['issue-root-causes', 'project', 'viewer', true], ['Issue', 'ReferenceData', 'Setting', 'Project', 'Member', 'User']],
    [['reference', 'issue-root-causes', 'viewer', true], ['ReferenceData', 'Setting', 'User']],
    [['template-provenance', 'project', 'viewer', true], ['Template', 'Project', 'Member', 'User', 'Setting']],
    [['template', 'version', 'projects', 'viewer', true, 1], ['Template', 'Project', 'Member', 'User', 'Setting']],
    [['templates', 'viewer', true, false], ['Template', 'Project', 'Member', 'User', 'Setting']],
  ]
  for (const [key, changes] of readers) for (const type of changes) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    try {
      qc.setQueryData(key, { cut: 'before', visibleTotal: 9 })
      qc.setQueryData(['preferences'], { locale: 'en' })
      await invalidateChange(qc, type, 'project')
      let reads = 0
      const current = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { cut: 'after', visibleTotal: 8 } } })
      assert.equal(reads, 1, `${type}: ${key.join(':')}`)
      assert.equal(current.visibleTotal, 8)
      assert.equal(qc.getQueryState(['preferences'])?.isInvalidated, false)
    } finally { qc.clear() }
  }
})

test('file reference readers refresh after revision, parent, author, scope and owning-flag changes', async () => {
  // AC-SCR-77: revision and missing-link figures must not retain the previously observed cohort.
  for (const type of ['Task', 'Deliverable', 'User', 'Member', 'Discipline', 'ReferenceData', 'Setting', 'DocumentLink', 'Project']) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    const key = ['files', 'viewer', true, 'project', 'discipline', 'author', true] as const
    try {
      qc.setQueryData(key, { revision: 'C', missingTotal: 1 })
      qc.setQueryData(['preferences'], { digestEnabled: true })
      await invalidateChange(qc, type, 'project')
      let reads = 0
      const data = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { revision: 'D', missingTotal: 0 } } })
      assert.equal(reads, 1, type); assert.equal(data.revision, 'D', type); assert.equal(data.missingTotal, 0, type)
      assert.equal(qc.getQueryState(['preferences'])?.isInvalidated, false, type)
    } finally { qc.clear() }
  }
})

test('a personal seen commit refreshes its readers while preserving unrelated business and preference caches', async () => {
  const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
  try {
    const readers = [['mywork', 'viewer'], ['my-work-follow-up', 'viewer', 'watching'], ['workspace-coordination'], ['p', 'project', 'discipline-coordination'], ['p', 'project', 'attention', {}], ['task', 'task']] as const
    const unrelated = [['p', 'project', 'budget'], ['p', 'project', 'files'], ['preferences'], ['notifications', 'inbox']] as const
    for (const key of [...readers, ...unrelated]) qc.setQueryData(key, { cut: 'before' })
    await invalidateWorkSeen(qc)
    for (const key of readers) {
      let reads = 0
      const value = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { cut: 'committed' } } })
      assert.equal(reads, 1, key.join(':')); assert.equal(value.cut, 'committed')
    }
    for (const key of unrelated) assert.equal(qc.getQueryState(key)?.isInvalidated, false, key.join(':'))
  } finally { qc.clear() }
})

test('follow-up rows refetch typed owners and activity after flags, assignment and comment changes', async () => {
  for (const [type, view] of [['Setting', 'watching'], ['Decision', 'requested'], ['Comment', 'watching']] as const) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    const key = ['my-work-follow-up', 'synthetic-viewer', view, true, true]
    try {
      qc.setQueryData(key, { items: [{ id: 'synthetic-decision', ownerName: 'Previous owner' }] })
      qc.setQueryData(['unrelated-query'], { label: 'unchanged' })
      await invalidateChange(qc, type, 'synthetic-project')
      let reads = 0
      const current = await qc.fetchQuery({ queryKey: key, queryFn: async () => {
        reads++; return { items: [{ id: 'synthetic-decision', ownerName: 'Current owner', ownerKind: 'User', commentCount: 3 }] }
      } })
      assert.equal(reads, 1)
      assert.equal(current.items[0].ownerKind, 'User')
      assert.equal(current.items[0].ownerName, 'Current owner')
      assert.equal(current.items[0].commentCount, 3)
      assert.equal(qc.getQueryState(['unrelated-query'])?.isInvalidated, false)
    } finally { qc.clear() }
  }
})

test('person fact commands refresh mounted staff, both Teams, editor and date-only picker facts', async () => {
  const cases: [string, readonly unknown[]][] = [
    ['UserCertification', ['staff', { scope: 'direct' }]],
    ['UserCertification', ['wteam', 'visible-projects', { certificationTagId: 'synthetic-tag' }]],
    ['UserCertification', ['p', 'synthetic-project', 'team']],
    ['UserCertification', ['person-facts', 'synthetic-person']],
    ['CertificationTag', ['certification-tags']],
    ['CertificationTag', ['staff', { scope: 'unit' }]],
    ['PersonFacts', ['people', 'synthetic search', 'synthetic-project']],
    ['PersonFacts', ['admin', 'users', 'all']],
  ]
  for (const [type, key] of cases) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    try {
      qc.setQueryData(key, { label: 'before' }); qc.setQueryData(['unrelated-query'], { label: 'unchanged' })
      await invalidateChange(qc, type)
      let reads = 0
      const current = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { label: 'after' } } })
      assert.equal(current.label, 'after'); assert.equal(reads, 1)
      assert.equal(qc.getQueryState(['unrelated-query'])?.isInvalidated, false)
    } finally { qc.clear() }
  }
})

test('successful discussion refreshes its originating project task count without a live event', async () => {
  const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
  const origin = ['p', 'synthetic-project', 'tasks', { page: 2, sort: 'key:asc' }] as const
  const otherProject = ['p', 'other-project', 'tasks', {}] as const
  let reads = 0
  try {
    qc.setQueryData(origin, { commentCount: 3 })
    qc.setQueryData(otherProject, { commentCount: 7 })
    // This is the command callback used by CommentsPanel after create/edit/delete, independent of SSE.
    await refreshComments(qc, 'synthetic-project')
    const row = await qc.fetchQuery({ queryKey: origin, queryFn: async () => { reads++; return { commentCount: 4 } } })
    assert.equal(row.commentCount, 4)
    assert.equal(reads, 1)
    assert.equal(qc.getQueryState(otherProject)?.isInvalidated, false)
  } finally { qc.clear() }
})

test('activity, own-week candidates and report inputs refetch otherwise fresh cached facts', async () => {
  const changes: [string, readonly unknown[]][] = [
    ['Comment', ['wtasks', 'visible-projects', { page: 2 }]],
    ['Comment', ['mywork-tasks', { tab: 'completed' }]],
    ['Task', ['time', 'week', { from: '2026-09-14' }]],
    ['Task', ['time-tasks', 'synthetic search']],
    ['Task', ['report', 'overdue-tasks', { minDaysOverdue: '3' }, 'task-inputs', 'unestimated', 2, 50]],
    ...['TimeEntry', 'PlanningEntry', 'OpenRole', 'ResourceAllocation', 'User', 'ReferenceData', 'Setting', 'Holiday', 'ApprovedLeave', 'PersonAvailabilityOverride', 'PersonSchedule', 'OrgUnit', 'OrgLevel'].map(type => [type, ['report', 'team-capacity', { unitId: 'synthetic-unit', weeks: '12' }, 'task-inputs', 'unestimated', 1, 50]] as [string, readonly unknown[]]),
    ...['ApprovedLeave', 'PersonAvailabilityOverride', 'PersonSchedule', 'Holiday', 'User', 'Setting', 'Member', 'Project'].map(type => [type, ['time', 'week', { from: '2026-09-14' }]] as [string, readonly unknown[]]),
  ]
  for (const [type, key] of changes) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    let reads = 0
    try {
      qc.setQueryData(key, { revision: 'before' })
      qc.setQueryData(['unrelated-query'], { revision: 'unrelated' })
      await invalidateChange(qc, type, 'synthetic-project')
      const result = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { revision: 'after' } } })
      assert.equal(result.revision, 'after', `${type} must refresh ${key[0]}`)
      assert.equal(reads, 1)
      assert.equal(qc.getQueryState(['unrelated-query'])?.isInvalidated, false)
    } finally { qc.clear() }
  }
})

test('a project colour change refreshes an already cached workspace Team identity', async () => {
  const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
  const team = ['wteam', 'my'] as const // WorkspaceTeam's actual scoped query, not the project Team tab.
  const unrelated = ['preferences'] as const
  let reads = 0
  try {
    qc.setQueryData(team, { projectId: 'synthetic-project', identityColour: '#ec6a3e' })
    qc.setQueryData(unrelated, { locale: 'en' })
    await invalidateChange(qc, 'Project', 'synthetic-project')
    const current = await qc.fetchQuery({ queryKey: team, queryFn: async () => {
      reads++
      return { projectId: 'synthetic-project', identityColour: '#1597cc' }
    } })
    assert.equal(reads, 1, 'an otherwise fresh Team cache must read the newly saved project identity')
    assert.equal(current.identityColour, '#1597cc')
    assert.equal(qc.getQueryState(unrelated)?.isInvalidated, false)
  } finally { qc.clear() }
})

test('item changes keep the previous project and item-specific invalidations (AC-PLAT-19)', () => {
  const before: Record<string, string[]> = {
    OpenRole: ['allocations', 'open-roles', 'workload', 'planning-grid', 'my-week'],
    ResourceAllocation: ['allocations', 'workload'], Task: ['task', 'mywork', 'p', 'open-work'],
    Decision: ['decision', 'coordination'], Action: ['action', 'coordination', 'mywork'],
    Risk: ['risk', 'coordination'], Issue: ['issue', 'coordination'], Milestone: ['milestone', 'p'],
    Deliverable: ['deliverable', 'p'], Handoff: ['handoffs', 'handoff-detail', 'handoff-options', 'search'],
    SubmissionPackage: ['submissions', 'submission-detail', 'coord-options'],
    PersonAvailabilityOverride: ['availability', 'availability-view', 'planning-grid', 'my-week'],
    ReviewRound: ['reviews', 'review-detail', 'changes', 'change-detail', 'coord-options', 'input-uses', 'handoffs', 'handoff-detail', 'submissions', 'submission-detail', 'workspace-coordination', 'deliverable'],
    PlanningEntry: ['planning-grid', 'planning-entries'], DesignBasisEntry: ['design-basis', 'design-basis-detail'], Member: ['staff'], SavedView: ['views'], ReferenceData: ['reference', 'admin'], Setting: ['me', 'admin'], Holiday: ['admin', 'availability-view', 'workload'],
    Dependency: ['task', 'gantt', 'coordination', 'workload', 'mywork-tasks'], DeliverableDependency: ['deliverable', 'gantt', 'coordination', 'coord-options'],
    Discipline: ['planner-project-disciplines'], Template: ['templates', 'template', 'template-preview', 'template-pack'],
    ApprovedLeave: ['availability-view', 'planning-grid', 'my-week'], PersonSchedule: ['availability-view', 'planning-grid', 'my-week'], // the Vacation page's own refresh after leave, schedule and import commands
  }
  for (const root of ['history', 'comments', 'links']) assert.ok(keysForChange('Risk', 'p1').some(k => k[0] === root))
  for (const [type, roots] of Object.entries(before)) {
    const keys = keysForChange(type, 'p1', 'i1')
    for (const root of [...roots, 'project', 'projects', 'mywork']) assert.ok(keys.some(k => k[0] === root), `${type}: ${root}`)
    assert.ok(keys.some(k => k.join('/') === `history/${type}/i1`))
  }
})

test('project refresh retains its 1.5 second derived-state pass', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  const calls: string[] = []
  const qc = { invalidateQueries: ({ queryKey }: { queryKey: string[] }) => { calls.push(queryKey.join('/')); return Promise.resolve() } } as unknown as QueryClient
  refreshProject(qc, 'p1')
  assert.ok(calls.includes('projects')); assert.ok(calls.includes('p/p1'))
  calls.length = 0
  t.mock.timers.tick(1499); assert.equal(calls.length, 0)
  t.mock.timers.tick(1); assert.deepEqual(calls, ['project', 'mywork', 'p/p1'])
})


test('child audit types refresh mounted details and dependent pickers; reconnect repairs missed events', () => {
  const required: Record<string, string[]> = {
    IssueLocation: ['issue', 'issue-locations'], IssueDocumentReference: ['issue-documents', 'issue-reference-impacts'],
    IssueVerification: ['issue-verification'], IssueReferenceImpactAssessment: ['issue-reference-impacts'], IssueAffectedDiscipline: ['issue', 'review-issues'],
    ReadinessCheck: ['readiness-detail', 'readiness-constraints', 'readiness-prerequisites', 'readiness-assumptions', 'task-start'],
    TaskStartAuthorisation: ['task-start'], BasisUse: ['readiness-assumptions'], Decision: ['basis-decision-options'],
    Milestone: ['submission-milestones'], SubmissionManifestItem: ['submission-choices'],
    TaskPickupOffer: ['pickup-offers', 'pickup-own-load', 'task', 'mywork-tasks', 'workload'],
    OrgUnit: ['admin', 'organisation', 'organisation-placements', 'staff', 'workload', 'planning-grid'],
    OrgLevel: ['admin', 'organisation'], CommitmentMissCategory: ['admin', 'weekly-promise'],
  }
  for (const [type, roots] of Object.entries(required)) for (const root of roots)
    assert.ok(keysForChange(type, 'p1').some(key => key[0] === root), `${type}: ${root}`)
  for (const root of ['task', 'risk', 'issue', ...Object.values(required).flat()])
    assert.ok(keysForChange(undefined, 'p1').some(key => key[0] === root), `reconnect: ${root}`)
})


test('invalidation prefixes match actual mounted screen query keys', () => {
  const mounted: Record<string, string[][]> = {
    Task: [['wtasks', 'scope'], ['wboard', 'scope'], ['gantt', 'scope'], ['closeout', 'p1'], ['open-items', 'm1'], ['p', 'p1', 'tasks', {}], ['p', 'p1', 'board', {}], ['task', 'i1'], ['task', 'i1', 'delete-preview'], ['task', 'i1', 'schedule'], ['mywork-tasks', {}]],
    Dependency: [['task', 'i1', 'deps', '3'], ['task', 'i1', 'chain'], ['task', 'i1', 'candidates', 'predecessor', ''], ['gantt', 'scope']],
    DeliverableDependency: [['deliverable', 'i1'], ['gantt', 'scope']],
    Deliverable: [['gantt', 'scope'], ['open-items', 'm1']], DocumentLink: [['files', 'scope']],
    ApprovedLeave: [['availability-view', '2026-10-01', '2026-10-31'], ['planning-grid', 'scope'], ['my-week', 'scope']], PersonSchedule: [['availability-view', '2026-10-01', '2026-10-31']], User: [['hr-identities'], ['admin', 'users', '', false, false], ['admin', 'users', 'all']],
    ReferenceData: [['admin', 'offices'], ['admin', 'disciplines'], ['reference']], Setting: [['admin', 'settings'], ['me']], Holiday: [['admin', 'holidays', 2026, ''], ['availability-view', '2026-10-01', '2026-10-31'], ['workload', 'scope']],
    Project: [['projects', {}], ['projects-pick', ''], ['projects-pick-ids', 'p1'], ['projects-mine-live'], ['planner-projects'], ['planner-projects-filter'], ['scope-projects', 'mine'], ['p', 'p1', 'date-review'], ['closeout', 'p1']],
    Member: [['p', 'p1', 'team'], ['open-items', 'm1']], Discipline: [['p', 'p1', 'team'], ['planner-project-disciplines', 'p1']],
    Risk: [['search-counts', 'scope'], ['search-page', 'scope'], ['risk', 'i1'], ['p', 'p1', 'risks', {}], ['coordination']],
    Issue: [['issue', 'i1'], ['p', 'p1', 'issues', {}], ['issue-documents', 'i1'], ['issue-verification', 'i1'], ['issue-reference-impacts', 'i1'], ['review-issues', 'p1'], ['coordination']],
    Action: [['action', 'i1'], ['p', 'p1', 'actions', {}], ['mywork']], Meeting: [['p', 'p1', 'meetings']],
    Milestone: [['milestone', 'i1'], ['p', 'p1', 'milestones', false, ''], ['p', 'p1', 'milestones', true, ''], ['submission-milestones', 'p1']],
    Template: [['templates'], ['template', 'i1'], ['template', 'i1', 'preview', ''], ['template-preview', { id: 'i1' }], ['template-pack', 'p1', 'i1']],
    IssueLocation: [['issue-locations', 'i1']], ReadinessAssessment: [['readiness-detail', 'p1', 'i1']],
    ResourceAllocation: [['allocations', 'p1', {}, 1], ['allocation-detail', 'p1', 'i1'], ['allocation-review-options', 'p1'], ['workload', 'scope']],
  }
  for (const [type, queries] of Object.entries(mounted)) for (const query of queries)
    assert.ok(keysForChange(type, 'p1').some(prefix => prefix.every((part, i) => query[i] === part)), `${type}: ${query}`)
})


test('a person\'s own workspaces and dashboard layout refresh the screens that read them', async () => {
  const calls: string[] = []
  const qc = { invalidateQueries: ({ queryKey }: { queryKey: string[] }) => { calls.push(queryKey.join('/')); return Promise.resolve() } } as unknown as QueryClient
  await refreshWorkspaces(qc); await refreshHome(qc)
  assert.deepEqual(calls, ['workspaces', 'home'])
  // Home is mounted under ['home', scope, from, to]; any item change reaches it, and so do the Files library and the project-scope menu's workspaces.
  for (const type of ['Task', 'Deliverable', 'Milestone', 'Member', 'Project', 'Risk'])
    assert.ok(keysForChange(type, 'p1').some(prefix => prefix.every((part, i) => ['home', 'mine', undefined, undefined][i] === part)), `${type}: home`)
  assert.ok(keysForChange('DocumentLink', 'p1').some(prefix => prefix.every((part, i) => ['files', 'mine', {}][i] === part)), 'DocumentLink: files')
})


test('workload changes refresh the pickup own-load hint', () => {
  for (const type of ['Task', 'ResourceAllocation', 'PlanningEntry', 'PersonSchedule', 'ApprovedLeave', 'PersonAvailabilityOverride', 'User', 'Project', 'Member'])
    assert.ok(keysForChange(type, 'p1').some(key => key[0] === 'pickup-own-load'), type)
})


test('notification and preference commands refresh the bell, the centre, the Following tab and the preferences page', async () => {
  const calls: string[] = []
  const qc = { invalidateQueries: ({ queryKey }: { queryKey: string[] }) => { calls.push(queryKey.join('/')); return Promise.resolve() } } as unknown as QueryClient
  await invalidateNotifications(qc)
  assert.deepEqual(calls, ['notifications', 'following', 'unread']) // the queries' roots: ['notifications', 'list' | 'recent' | 'inbox'], ['following', id, important], ['unread', stamp]
  calls.length = 0
  await invalidatePreferences(qc)
  assert.deepEqual(calls, ['preferences'])
})

test('new batch mutations refresh their mounted views and await active refetches', async () => {
  for (const [type, query] of [['OutputCommitment', ['commitment-reliability', 'p1']], ['OutputCommitment', ['weekly-promise', 'p1']], ['OutputCommitment', ['p', 'p1', 'readiness-window']], ['UserPreferences', ['onboarding', 'u1']]] as const)
    assert.ok(keysForChange(type, 'p1').some(prefix => prefix.every((part, i) => query[i] === part)), type)
  let release!: () => void
  const pending = new Promise<void>(resolve => { release = resolve })
  const qc = { invalidateQueries: () => pending } as unknown as QueryClient
  let finished = false
  const refresh = invalidateChange(qc, 'UserPreferences').then(() => { finished = true })
  await Promise.resolve()
  assert.equal(finished, false)
  release()
  await refresh
  assert.equal(finished, true)
})

test('person date and setting changes refresh all authorised candidate sources; submission events refresh workspace dates', async () => {
  const noticeSources = ['coord-options', 'issue-verifier-eligible', 'pickup-offers', 'handoff-options', 'organisation', 'workspace-coordination', 'hr-identities']
  const changes: [string, readonly unknown[]][] = [
    ...['PersonFacts', 'User', 'Setting'].flatMap(type => noticeSources.map(source => [type, [source, 'synthetic-scope']] as [string, readonly unknown[]])),
    ...['SubmissionPackage', 'SubmissionManifestItem', 'SubmissionIssue', 'Deliverable'].map(type => [type, ['wtasks', 'visible-projects', { page: 2, cols: 'submissionDate' }]] as [string, readonly unknown[]]),
  ]
  for (const [type, key] of changes) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    try {
      qc.setQueryData(key, { version: 'before' }); qc.setQueryData(['unrelated-query'], { version: 'same' })
      await invalidateChange(qc, type, 'synthetic-project')
      let reads = 0
      const row = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { version: 'after' } } })
      assert.equal(row.version, 'after', `${type}: ${key[0]}`); assert.equal(reads, 1)
      assert.equal(qc.getQueryState(['unrelated-query'])?.isInvalidated, false)
    } finally { qc.clear() }
  }
})

test('board leads and thresholds, Team organisation labels and candidate capacity refresh cached reads', async () => {
  const changes: [string, readonly unknown[]][] = [
    ...['Setting', 'User', 'Discipline', 'Project', 'Member', 'ReferenceData'].flatMap(type =>
      ['task', 'wtasks', 'wboard'].map(root => [type, [root, 'authorised-scope', { swim: 'discipline' }]] as [string, readonly unknown[]])),
    ...['User', 'ReferenceData', 'Member', 'Discipline', 'OrgUnit', 'Setting'].flatMap(type =>
      [['wteam', 'authorised-scope'], ['p', 'project-one', 'team']].map(key => [type, key] as [string, readonly unknown[]])),
    ...['Setting', 'User', 'Task', 'PlanningEntry', 'ResourceAllocation', 'ApprovedLeave', 'PersonSchedule', 'PersonAvailabilityOverride', 'Holiday'].map(type =>
      [type, ['people-capacity', 'viewer', '2026-11-02', ['candidate']]] as [string, readonly unknown[]]),
  ]
  for (const [type, key] of changes) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    try {
      qc.setQueryData(key, { version: 'before' }); qc.setQueryData(['unrelated-query'], { version: 'same' })
      await invalidateChange(qc, type, 'project-one')
      let reads = 0
      const row = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { version: 'after' } } })
      assert.equal(row.version, 'after', `${type}: ${key[0]}`); assert.equal(reads, 1)
      assert.equal(qc.getQueryState(['unrelated-query'])?.isInvalidated, false)
    } finally { qc.clear() }
  }
})


test('person panels refetch after membership, scope and permitted-fact changes without touching unrelated personal state', async () => {
  const key = ['person-panel', 'viewer', 'person', 'direct', 1, 25, true, true] as const
  for (const type of ['Member', 'Project', 'User', 'Discipline', 'ReferenceData', 'OrgLevel', 'OrgUnit', 'PersonFacts', 'UserCertification', 'CertificationTag', 'Setting']) {
    const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
    try {
      qc.setQueryData(key, { displayName: 'Previously authorised name' })
      qc.setQueryData(['preferences'], { digestEnabled: true })
      await invalidateChange(qc, type, 'project')
      let reads = 0
      const read = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { displayName: 'Current authorised name' } } })
      assert.equal(reads, 1, type)
      assert.equal(read.displayName, 'Current authorised name', type)
      assert.equal(qc.getQueryState(['preferences'])?.isInvalidated, false, type)
    } finally { qc.clear() }
  }
})

test('an owning flag change refreshes the cached reference vocabulary', async () => {
  const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
  try {
    qc.setQueryData(['reference'], { issuePurposes: [] })
    await invalidateChange(qc, 'Setting')
    let reads = 0
    const result = await qc.fetchQuery({ queryKey: ['reference'], queryFn: async () => { reads++; return { issuePurposes: [{ id: 'purpose', name: 'For review' }] } } })
    assert.equal(reads, 1)
    assert.equal(result.issuePurposes[0].name, 'For review')
  } finally { qc.clear() }
})

test('coordination readers refresh current linked work and meeting agenda without treating a contribution as a workflow change', async () => {
  const readers = [
    ['handoff-readiness-links', 'viewer', 'project', 'handoff', true, 2],
    ['weekly-readiness-agenda', 'viewer', 'project', true, '2026-11-02', '2026-11-08', null, 2, 1],
  ] as const
  const qc = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, gcTime: Infinity } } })
  try {
    for (const key of readers) qc.setQueryData(key, { total: 3 })
    await invalidateChange(qc, 'TaskContribution', 'project', 'task')
    for (const key of readers) assert.equal(qc.getQueryState(key)?.isInvalidated, false)
    for (const type of ['Task', 'Handoff', 'WorkConstraint', 'OutputCommitment', 'Member', 'Setting']) {
      for (const key of readers) qc.setQueryData(key, { total: 3 })
      await invalidateChange(qc, type, 'project')
      for (const key of readers) {
        let reads = 0
        const current = await qc.fetchQuery({ queryKey: key, queryFn: async () => { reads++; return { total: 2 } } })
        assert.equal(reads, 1, type); assert.equal(current.total, 2, type)
      }
    }
  } finally { qc.clear() }
})
