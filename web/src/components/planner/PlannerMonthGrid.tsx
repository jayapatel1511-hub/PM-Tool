import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { Fragment } from 'react'
import { useFlag } from '@/lib/auth'
import './planner-semantic.css'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { BareTable } from '@/components/ui/table'
import { PlanningBandBar } from './PlanningBandBar'
import { PlanningDeadlineMarkers } from './PlanningDeadlines'
import { plannedPercentage, planningColumns, planningTotals, type PlannerUnit } from './planning-display'
import { groupPeople, hoursLabel, type PlannerCell, type PlannerGridData, type PlannerPerson, type PeopleGroup } from './labels'

export function PlannerMonthGrid({ data, unit, groupBy, reference, unitByPerson, unitNames, onCell }: {
  data: PlannerGridData; unit: PlannerUnit; groupBy: string; onCell: (person: PlannerPerson, cell: PlannerCell) => void;
  reference?: { offices?: { id: string; name: string }[]; disciplines?: { id: string; name: string }[] };
  unitByPerson?: Map<string, string | null>; unitNames?: Map<string, string>
}) {
  const visual = useFlag('visual_system')
  const peopleOn = useFlag('people_screens')
  const columns = planningColumns(data.weeks, 'months')
  const groups: PeopleGroup<PlannerPerson>[] = groupBy === 'homeUnit' ? [...data.people.reduce((map, person) => {
    const unitId = unitByPerson?.get(person.id)
    const key = unitByPerson?.has(person.id) ? unitNames?.get(unitId ?? '') ?? t('staff.group.notSet') : t('staff.group.unknown')
    map.set(key, [...(map.get(key) ?? []), person]); return map
  }, new Map<string, PlannerPerson[]>())] : groupPeople(data.people, groupBy, reference, peopleOn)
  const cellsFor = (people: PlannerPerson[], weeks: string[]) => people.flatMap(person => person.cells.filter(cell => weeks.includes(cell.week)))
  const value = (total: ReturnType<typeof planningTotals>, partial: boolean) => unit === 'percent'
    ? (total.provisional ? null : plannedPercentage(total.confirmed + total.expected, total.capacity)) ?? t('planner.percentageUnavailable')
    : partial ? t('planner.remainingUnavailable') : hoursLabel(total.remaining)
  return <div className="overflow-auto rounded-lg border bg-card" role="region" aria-label={t('planner.monthGrid')}><BareTable className="w-full min-w-max text-sm"><caption className="p-3 text-left text-xs text-muted-foreground">{t('planner.monthBasis')}</caption><thead className="bg-muted"><tr><th scope="col" className="min-w-60 border-r px-4 py-3 text-left">{t('planner.person')}</th>{columns.map(column => <th key={column.key} scope="col" data-planner-month={column.key} className="min-w-56 border-r p-3 text-left align-top font-medium">{column.label}<span className="block text-xs font-normal text-muted-foreground">{t('planner.displayedWeeks', { n: column.weeks.length })}</span><PlanningDeadlineMarkers context={data.deadlines} weeks={column.weeks} /></th>)}</tr></thead>
    {groups.map(([name, people, supervisor, groupKey]) => <tbody key={groupKey ?? name}>{groupBy !== 'none' && <Fragment><tr><th scope="rowgroup" className="border-t border-r bg-sidebar px-4 py-2 text-left text-xs font-medium">{supervisor ? <PersonPanelTrigger userId={supervisor.id} name={supervisor.name} focusKey={`planner:month:group:${groupKey}:heading`}>{name}</PersonPanelTrigger> : name} · {people.length}</th>{columns.map(column => {
      const total = planningTotals(cellsFor(people, column.weeks))
      return <td key={column.key} data-subtotal={total.confirmed + total.expected + total.possible} className="border-t border-r bg-sidebar p-3 text-xs tabular-nums">{hoursLabel(total.confirmed + total.expected + total.possible)} / {hoursLabel(total.capacity)}<PlanningBandBar {...total} partial={people.some(person => person.partial)} /></td>
    })}</tr></Fragment>}
      {people.map(person => <tr key={person.id}><th scope="row" className="border-t border-r px-4 py-3 text-left align-top font-medium"><PersonPanelTrigger userId={person.id} name={person.displayName} focusKey={`planner:month:person:${person.id}`} />{person.partial && <span className="block text-xs font-normal text-muted-foreground">{t('planner.partialTitle')}</span>}</th>{columns.map(column => {
        const cells = cellsFor([person], column.weeks), total = planningTotals(cells)
        return <td key={column.key} className="border-t border-r p-3 align-top"><Popover><PopoverTrigger asChild><Button variant="ghost" className="h-auto w-full flex-col items-start gap-1 px-2 py-2 text-left" aria-label={`${t('planner.monthCell', { person: person.displayName, month: column.label })} · ${value(total, person.partial)}${visual && total.timeAway > 0 ? `; ${t('planner.semantic.timeOffHours', { hours: hoursLabel(total.timeAway) })}` : ''}${visual && total.provisional ? `; ${t('capacity.provisionalAvailability')}` : ''}`}><span data-planner-month-value className={`text-base tabular-nums ${visual && (unit === 'percent' ? !total.provisional && total.capacity > 0 : !person.partial) ? `planner-semantic-quantity ${(unit === 'percent' ? total.confirmed + total.expected > total.capacity : total.remaining < 0) ? 'planner-semantic-quantity--negative' : ''}` : ''}`}>{value(total, person.partial)}</span><span className="text-xs font-normal text-muted-foreground tabular-nums">{t('planner.monthPlanned', { planned: hoursLabel(total.confirmed + total.expected), capacity: hoursLabel(total.capacity) })}</span>{total.possible > 0 && <span className="text-xs font-normal text-muted-foreground">{t('planner.monthPossible', { hours: hoursLabel(total.possible) })}</span>}{total.timeAway > 0 && <span data-planner-away={visual ? 'month' : undefined} className={`text-xs font-normal text-muted-foreground ${visual ? 'planner-semantic-away' : ''}`}>{t(visual ? 'planner.semantic.timeOffHours' : 'planner.timeAwayHours', { hours: hoursLabel(total.timeAway) })}</span>}{total.provisional && <span className={`text-xs font-normal ${visual ? 'planner-semantic-provisional' : 'text-muted-foreground'}`}>{t('capacity.provisionalAvailability')}</span>}</Button></PopoverTrigger><PopoverContent className="w-72" aria-label={t('planner.chooseWeek')}><p className="mb-2 text-xs text-muted-foreground">{t('planner.chooseWeek')}</p>{cells.map(cell => <Button key={cell.week} variant="ghost" className="w-full justify-between text-xs" onClick={() => onCell(person, cell)}>{fmtDate(cell.week)}<span className="tabular-nums">{hoursLabel(cell.confirmed + cell.expected)} / {hoursLabel(cell.capacity)}</span></Button>)}</PopoverContent></Popover></td>
      })}</tr>)}
    </tbody>)}
    <tfoot><tr><th scope="row" className="border-t border-r bg-muted p-3 text-left">{t('planner.teamTotal')}<span className="block text-xs font-normal text-muted-foreground">{t('planner.teamTotalBands')}</span></th>{columns.map(column => {
      const total = planningTotals(cellsFor(data.people, column.weeks))
      return <td key={column.key} className="border-t border-r bg-muted p-3 tabular-nums">{hoursLabel(total.confirmed + total.expected)}<span className="block text-xs text-muted-foreground">{t('planner.remaining')}: {data.people.some(person => person.partial) ? t('planner.remainingUnavailable') : hoursLabel(total.remaining)}</span></td>
    })}</tr></tfoot>
  </BareTable>{visual && <p className="planner-semantic-legend p-3">{t('planner.semantic.bandLegend')} {t('planner.semantic.timeOffLegend')}</p>}</div>
}
