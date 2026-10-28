import { test } from 'node:test'
import assert from 'node:assert/strict'
import { invalidateChange, keysForChange, refreshProject } from './keys.ts'
import type { QueryClient } from '@tanstack/react-query'

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
    Discipline: ['planner-project-disciplines'],
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
    Task: [['wtasks', 'scope'], ['wboard', 'scope'], ['gantt', 'scope'], ['closeout', 'p1'], ['open-items', 'm1']],
    Dependency: [['task', 'i1', 'deps', '3'], ['task', 'i1', 'chain'], ['task', 'i1', 'candidates', 'predecessor', ''], ['gantt', 'scope']],
    DeliverableDependency: [['deliverable', 'i1'], ['gantt', 'scope']],
    Deliverable: [['gantt', 'scope'], ['open-items', 'm1']], DocumentLink: [['files', 'scope']],
    ApprovedLeave: [['availability-view', '2026-10-01', '2026-10-31'], ['planning-grid', 'scope'], ['my-week', 'scope']], PersonSchedule: [['availability-view', '2026-10-01', '2026-10-31']], User: [['hr-identities'], ['admin', 'users', '', false, false], ['admin', 'users', 'all']],
    ReferenceData: [['admin', 'offices'], ['admin', 'disciplines'], ['reference']], Setting: [['admin', 'settings'], ['me']], Holiday: [['admin', 'holidays', 2026, ''], ['availability-view', '2026-10-01', '2026-10-31'], ['workload', 'scope']],
    Member: [['p', 'p1', 'team'], ['open-items', 'm1']], Discipline: [['p', 'p1', 'team'], ['planner-project-disciplines', 'p1']],
    Risk: [['search-counts', 'scope'], ['search-page', 'scope']],
    IssueLocation: [['issue-locations', 'i1']], ReadinessAssessment: [['readiness-detail', 'p1', 'i1']],
    ResourceAllocation: [['allocations', 'p1', {}, 1], ['allocation-detail', 'p1', 'i1'], ['allocation-review-options', 'p1'], ['workload', 'scope']],
  }
  for (const [type, queries] of Object.entries(mounted)) for (const query of queries)
    assert.ok(keysForChange(type, 'p1').some(prefix => prefix.every((part, i) => query[i] === part)), `${type}: ${query}`)
})


test('workload changes refresh the pickup own-load hint', () => {
  for (const type of ['Task', 'ResourceAllocation', 'PlanningEntry', 'PersonSchedule', 'ApprovedLeave', 'PersonAvailabilityOverride', 'User', 'Project', 'Member'])
    assert.ok(keysForChange(type, 'p1').some(key => key[0] === 'pickup-own-load'), type)
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
