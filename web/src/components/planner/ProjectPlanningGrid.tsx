import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { t } from '@/lib/i18n'
import { Link } from 'react-router'
import { fmtDate } from '@/lib/format'
import { useFlag } from '@/lib/auth'
import { confidenceLabel, hoursLabel, visibilityLabel, type PlannerEntry, type PlannerGridData } from './labels'
import { plannedPercentage, planningColumns, type PlannerUnit, type PlannerZoom } from './planning-display'
import { PlanningDeadlineMarkers } from './PlanningDeadlines'
import './planner-semantic.css'

export function ProjectPlanningGrid({ data, onEntry, projectId, kept = [], unit = 'hours', zoom = 'weeks' }: {
  kept?: string[]; data: PlannerGridData; projectId?: string; onEntry: (entry: PlannerEntry) => void; unit?: PlannerUnit; zoom?: PlannerZoom
}) {
  const enabled = useFlag('capacity_screens')
  const visual = useFlag('visual_system')
  const columns = planningColumns(data.weeks, enabled ? zoom : 'weeks')
  const rows = data.people.flatMap((person) => [
    ...person.entries.filter((entry) => entry.matchesFilter !== false || kept.includes(entry.id)).map((entry) => ({
      key: `entry-${entry.id}`, project: entry.projectName ?? t('planner.unlinkedWork'), person: person.displayName, personId: person.id, label: entry.label,
      confidence: confidenceLabel(entry.confidence), visibility: visibilityLabel(entry.visibility, entry.ownerKind === 'Self'),
      hours: data.weeks.map((week) => week >= entry.startWeek && week <= entry.endWeek ? entry.hoursPerWeek : 0),
      unestimated: undefined as number[] | undefined, entry: entry as PlannerEntry | undefined, capacity: person.cells,
    })),
    ...person.approvedAllocations.filter((allocation) => !projectId || allocation.projectId === projectId).map((allocation) => ({
      key: `allocation-${allocation.allocationId}`, project: allocation.projectName ?? '', person: person.displayName, personId: person.id, label: t('planner.approvedAllocations'),
      confidence: confidenceLabel('Confirmed'), visibility: t('planner.approval.confirmed'),
      hours: data.weeks.map((week) => allocation.weeks.find((cell) => cell.week === week)?.hours ?? 0),
      unestimated: undefined as number[] | undefined, entry: undefined as PlannerEntry | undefined, capacity: person.cells,
    })),
    ...(person.taskContexts ?? []).filter((context) => !projectId || context.projectId === projectId).map((context) => ({
      key: `context-${person.id}-${context.projectId}`, project: context.projectName, person: person.displayName, personId: person.id, label: t('planner.taskEstimates'),
      confidence: '—', visibility: '—', hours: data.weeks.map(week => context.weeks.find(cell => cell.week === week)?.hours ?? 0),
      unestimated: data.weeks.map(week => context.weeks.find(cell => cell.week === week)?.unestimatedTasks ?? 0),
      entry: undefined as PlannerEntry | undefined, capacity: person.cells,
    })),
  ]).sort((a, b) => a.project.localeCompare(b.project) || a.person.localeCompare(b.person))
  return <div className="overflow-auto rounded-lg border bg-card"><table className="w-full text-left text-sm">
    <caption className="p-3 text-left text-muted-foreground">{t('planner.projectsHint')}{data.evaluatedAt && ` ${t('planner.evaluated', { date: fmtDate(data.evaluatedAt.slice(0, 10)) })}`}{enabled && unit === 'percent' && <span className="block text-xs">{t('planner.projectPercentBasis')}</span>}</caption>
    <thead className="bg-muted"><tr>{(['planner.project', 'planner.person', 'planner.label', 'planner.confidence', 'planner.visibility'] as const).map(key => <th scope="col" className="p-3" key={key}>{t(key)}</th>)}{columns.map(column => <th scope="col" className="whitespace-nowrap p-3 align-top" key={column.key}>{zoom === 'months' && enabled ? column.label : fmtDate(column.weeks[0])}{enabled && <PlanningDeadlineMarkers context={data.deadlines} weeks={column.weeks} />}</th>)}</tr></thead>
    <tbody>{rows.map(row => <tr className={row.entry?.matchesFilter === false ? 'border-t bg-muted' : 'border-t'} data-kept={row.entry?.matchesFilter === false || undefined} key={row.key}>
      <td className="p-3 font-medium">{row.project}</td><td className="p-3"><PersonPanelTrigger userId={row.personId} name={row.person} focusKey={`project-planning:${row.key}:person`} /></td><td className="p-3">{row.entry ? <button className="text-primary underline" onClick={() => onEntry(row.entry!)}>{row.label}</button> : row.label}{row.entry?.matchesFilter === false && <span className="block text-xs text-muted-foreground">{t('planner.noLongerMatches')}</span>}</td><td className="p-3">{row.confidence}</td><td className="p-3">{row.visibility}</td>
      {columns.map(column => {
        const indexes = column.weeks.map(week => data.weeks.indexOf(week))
        const hours = indexes.reduce((sum, index) => sum + row.hours[index], 0)
        const unestimated = indexes.reduce((sum, index) => sum + (row.unestimated?.[index] ?? 0), 0)
        const selectedCells = row.capacity.filter(cell => column.weeks.includes(cell.week))
        const capacity = selectedCells.reduce((sum, cell) => sum + cell.capacity, 0)
        const provisional = selectedCells.some(cell => cell.provisional)
        const percentage = enabled && unit === 'percent' ? (provisional ? null : plannedPercentage(hours, capacity)) : null
        const value = enabled && unit === 'percent' ? percentage ?? t('planner.percentageUnavailable') : hours > 0 ? hoursLabel(hours) : '—'
        const known = enabled && unit === 'percent' ? percentage !== null : hours > 0
        return <td className="p-3 text-right tabular-nums" key={column.key}>{visual && known ? <span className="planner-semantic-quantity">{value}</span> : value}{unestimated > 0 && <span className="block text-xs text-muted-foreground">{t('planner.unestimatedContext', { n: unestimated })}</span>}</td>
      })}
    </tr>)}{data.unfilledRoles?.map(role => <tr className="border-t bg-muted/40" key={`role-${role.id}`}><td className="p-3 font-medium">{role.projectNumber} {role.projectName}</td><td className="p-3">{t('openRole.unfilled')}</td><td className="p-3">{role.canFill ? <Link className="text-primary underline" to={role.fillPath ?? ''}>{role.roleLabel}</Link> : <Link className="text-muted-foreground underline" to={role.fillPath ?? ''}>{role.roleLabel}</Link>}<span className="block text-xs text-muted-foreground">{role.disciplineName ?? t('planner.noDiscipline')}</span></td><td className="p-3">{t('openRole.unfilled')}</td><td className="p-3">{hoursLabel(role.plannedHours)}</td>{columns.map(column => {
      const hours = role.cells.filter(cell => column.weeks.includes(cell.week)).reduce((sum, cell) => sum + cell.hours, 0)
      return <td className="p-3 text-right tabular-nums" key={column.key}>{hours > 0 ? hoursLabel(hours) : '—'}</td>
    })}</tr>)}</tbody>
  </table>{enabled && unit === 'percent' && data.unfilledRoles?.length ? <p className="p-3 text-xs text-muted-foreground">{t('planner.roleHoursOnly')}</p> : null}{!rows.length && !data.unfilledRoles?.length && <p className="p-4">{t('planner.noBulkEntries')}</p>}</div>
}
