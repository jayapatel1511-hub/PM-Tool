import type { components } from '@/gen/api'
import { fmtDate, fmtTime, hours } from '@/lib/format'
import { runtimeText, t, tv } from '@/lib/i18n'
import type { TaskRow } from './Tasks'

export type TaskListGroup = components['schemas']['TaskListGroup']
type Metrics = Pick<TaskListGroup, 'rowCount' | 'estimatedHours' | 'unestimatedCount' | 'overdueCount' | 'unavailableStateCount' | 'progressPct' | 'progressBasis' | 'progressTaskCount' | 'nextDueDate' | 'undatedOpenCount'>
type GroupPage = { groups?: TaskListGroup[] | null; asOf?: string | null }
export const taskGroupOptions = ['none', 'discipline', 'deliverable', 'milestone', 'assignee', 'status', 'due'] as const
export const taskGroupLabel = (key: string, values?: Record<string, string | number>) => runtimeText(`taskGroups.${key}`, values)
export const taskGroupChoices = () => taskGroupOptions.map(value => ({ value, label: runtimeText(({ none: 'tgroup.none', discipline: 'common.discipline', deliverable: 'task.deliverable', milestone: 'field.MilestoneId', assignee: 'field.AssigneeId', status: 'common.status', due: 'common.due' })[value]) }))

export function groupHeading(group: TaskListGroup) {
  if (!group.referenceAvailable) return taskGroupLabel('referenceUnavailable')
  if (group.dimension === 'project') return `${group.projectNumber} · ${group.projectName}`
  if (group.dimension === 'status') return tv(group.value ?? '')
  if (group.dimension === 'due') return group.value === 'unavailable' ? taskGroupLabel('unavailableDates') : runtimeText(`tbucket.${group.value}`)
  return group.label ?? t(group.dimension === 'deliverable' ? 'task.noDeliverable' : group.dimension === 'milestone' ? 'task.noMilestone' : group.dimension === 'assignee' ? 'ind.unassigned' : 'common.unavailable')
}

/** The first full read remains the metric basis; changed cohorts across infinite pages are explicitly unavailable. */
export function mergeTaskGroupPages(pages: GroupPage[]) {
  if (pages.length === 0 || pages.some(page => !Array.isArray(page.groups))) return { groups: undefined, unavailable: new Set<string>(), asOf: undefined }
  const groups = new Map<string, TaskListGroup>(), fingerprints = new Map<string, string>(), unavailable = new Set<string>()
  for (const page of pages) for (const group of page.groups!) {
    if (!Array.isArray(group.pageTaskIds)) { unavailable.add(group.key); continue }
    const fingerprint = JSON.stringify({ ...group, pageTaskIds: undefined, pageShown: undefined })
    if (fingerprints.has(group.key) && fingerprints.get(group.key) !== fingerprint) unavailable.add(group.key)
    if (!groups.has(group.key)) { groups.set(group.key, { ...group, pageTaskIds: [...group.pageTaskIds] }); fingerprints.set(group.key, fingerprint) }
    else {
      const prior = groups.get(group.key)!, ids = [...new Set([...prior.pageTaskIds, ...group.pageTaskIds])]
      groups.set(group.key, { ...prior, pageTaskIds: ids, pageShown: ids.length })
    }
  }
  return { groups: [...groups.values()], unavailable, asOf: pages[0].asOf ?? undefined }
}

export type TaskGroupEntry = { kind: 'group'; group: TaskListGroup; shown: number; unavailable: boolean } | { kind: 'row'; row: TaskRow }
export function taskGroupEntries(rows: TaskRow[], groups?: TaskListGroup[] | null, unavailable = new Set<string>()) {
  const byId = new Map(rows.map(row => [row.id, row])), seen = new Set<string>(), entries: TaskGroupEntry[] = []
  if (Array.isArray(groups)) for (const group of groups) {
    const ids = Array.isArray(group.pageTaskIds) ? [...new Set(group.pageTaskIds)].filter(id => byId.has(id)) : []
    if (ids.length === 0) continue
    entries.push({ kind: 'group', group, shown: ids.length, unavailable: unavailable.has(group.key) })
    if (group.dimension !== 'project') for (const row of rows) if (ids.includes(row.id) && !seen.has(row.id)) { seen.add(row.id); entries.push({ kind: 'row', row }) }
  }
  for (const row of rows) if (!seen.has(row.id)) entries.push({ kind: 'row', row })
  return { entries, complete: Array.isArray(groups) && rows.every(row => seen.has(row.id)) }
}

/** Overview groups already hold the complete actual displayed task arrays. Deduplicate repeated bucket memberships. */
export function summarizeTaskRows(input: TaskRow[]): Metrics {
  const rows = [...new Map(input.map(row => [row.id, row])).values()]
  const progress = rows.filter(row => row.status !== 'Cancelled'), weighted = progress.every(row => row.estimatedHours != null)
  // Stored task estimates have precision(7,1); integer tenths avoid a false45% at an exact50% boundary.
  const tenths = (value: number | null | undefined) => Math.round((value ?? 0) * 10)
  const denominator = weighted ? progress.reduce((sum, row) => sum + tenths(row.estimatedHours), 0) : progress.length
  const complete = progress.filter(row => row.status === 'Complete'), numerator = weighted ? complete.reduce((sum, row) => sum + tenths(row.estimatedHours), 0) : complete.length
  const open = rows.filter(row => row.status !== 'Complete' && row.status !== 'Cancelled')
  return { rowCount: rows.length, estimatedHours: rows.reduce((sum, row) => sum + tenths(row.estimatedHours), 0) / 10, unestimatedCount: rows.filter(row => row.estimatedHours == null).length,
    overdueCount: rows.filter(row => row.state?.isOverdue === true).length, unavailableStateCount: rows.filter(row => typeof row.state?.isOverdue !== 'boolean').length,
    progressPct: progress.length === 0 || denominator === 0 ? null : Math.floor(numerator * 20 / denominator) * 5,
    progressBasis: progress.length === 0 ? 'NoTasks' : weighted ? 'Hours' : 'Count', progressTaskCount: progress.length,
    nextDueDate: open.map(row => row.dueDate).filter((date): date is string => !!date).sort()[0] ?? null, undatedOpenCount: open.filter(row => !row.dueDate).length }
}

export function TaskGroupSummary({ summary, shown, unavailable = false }: { summary?: Metrics | null; shown?: number; unavailable?: boolean }) {
  if (unavailable || !summary || ![summary.rowCount, summary.estimatedHours, summary.unestimatedCount, summary.overdueCount, summary.unavailableStateCount, summary.progressTaskCount, summary.undatedOpenCount].every(value => typeof value === 'number' && Number.isFinite(value))
    || !['Hours', 'Count', 'NoTasks'].includes(summary.progressBasis) || !('progressPct' in summary) || !('nextDueDate' in summary))
    return <span className="text-xs font-normal text-warn" data-task-group-unavailable>{taskGroupLabel('totalsUnavailable')}</span>
  return <span role="group" aria-label={taskGroupLabel('summary')} data-task-group-summary className="inline-flex flex-wrap items-center gap-x-3 gap-y-1 text-xs font-normal text-muted-foreground tabular-nums">
    <span>{taskGroupLabel('shown', { shown: shown ?? summary.rowCount, total: summary.rowCount })}</span>
    <span>{taskGroupLabel(summary.unavailableStateCount > 0 ? 'knownOverdue' : 'overdue', { n: summary.overdueCount })}{summary.unavailableStateCount > 0 && ` · ${taskGroupLabel('stateUnavailable', { n: summary.unavailableStateCount })}`}</span>
    <span>{taskGroupLabel('estimates', { hours: hours(summary.estimatedHours), n: summary.unestimatedCount })}</span>
    <span>{summary.progressPct == null ? taskGroupLabel('progressUnavailable', { basis: taskGroupLabel(`basis${summary.progressBasis}`) }) : taskGroupLabel('progress', { n: summary.progressPct, basis: taskGroupLabel(`basis${summary.progressBasis}`) })}{summary.progressTaskCount < summary.rowCount && ` · ${taskGroupLabel('excludesCancelled')}`}</span>
    <span>{summary.nextDueDate ? taskGroupLabel('nextDue', { date: fmtDate(summary.nextDueDate) }) : taskGroupLabel('noDatedOpen')} · {taskGroupLabel('undated', { n: summary.undatedOpenCount })}</span>
  </span>
}

export function TaskGroupReadStatus({ asOf, complete, changed = false }: { asOf?: string | null; complete: boolean; changed?: boolean }) {
  return <p role="status" className="text-xs text-muted-foreground">{complete && asOf ? taskGroupLabel('readAt', { time: fmtTime(asOf) }) : taskGroupLabel('totalsUnavailable')}{changed && ` · ${taskGroupLabel('changedCohort')}`}</p>
}
