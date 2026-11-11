import type { components } from '@/gen/api'
import { TableRegion, tdCls, thCls } from '@/components/hub/common'
import { BareTable } from '@/components/ui/table'
import { hours, shortDate } from '@/lib/format'
import { t } from '@/lib/i18n'

export function AllocationDisciplineComparison({ data }: { data: components['schemas']['DisciplineComparison'] }) {
  const partial = data.allocationCoverage !== 'Complete'
  const allocationHours = (value: number | null, known: number, unavailable: number) => value == null
    ? t('allocationComparison.knownUnavailable', { hours: hours(known), n: unavailable })
    : partial ? t('allocationComparison.atLeast', { hours: hours(value) }) : hours(value)
  return <section className="space-y-2"><h2 id="allocation-comparison-title" className="text-sm font-medium">{t('allocationComparison.title')}</h2>
    <p className="max-w-5xl text-xs text-muted-foreground">{t('allocationComparison.basis')}</p>
    {partial && <p role="status" className="text-xs text-muted-foreground">{t('allocationComparison.partial')}</p>}
    <TableRegion role="region" aria-labelledby="allocation-comparison-title"><BareTable className="w-full min-w-max text-sm"><caption className="sr-only">{t('allocationComparison.title')}</caption><thead><tr><th scope="col" className={thCls}>{t('common.discipline')}</th>{data.weeks.map(week => <th key={week} scope="col" className={thCls}>{t('planner.weekOf')} {shortDate(week)}</th>)}</tr></thead><tbody>
      {data.disciplines.map(row => <tr key={row.projectDisciplineId ?? 'unattributed'}><th scope="row" className={`${tdCls} text-left font-medium`}>
        {row.unattributed ? t('allocationComparison.unattributed') : row.disciplineName ?? t('common.unavailable')}
        {row.unattributedAllocations > 0 && <span className="mt-1 block max-w-60 text-xs font-normal text-muted-foreground">{t('allocationComparison.unattributedN', { n: row.unattributedAllocations })}</span>}
        {row.undatedTasks > 0 && <span className="mt-1 block text-xs font-normal text-muted-foreground">{t('allocationComparison.undatedTasks', { n: row.undatedTasks })}</span>}
      </th>{row.weeks.map(week => <td key={week.week} className={tdCls}><dl className="grid grid-cols-[auto_auto] gap-x-4 gap-y-1 text-xs tabular-nums">
        <dt className="text-muted-foreground">{t('allocationComparison.confirmed')}</dt><dd className="text-right" data-allocation-confirmed>{allocationHours(week.confirmedHours, week.knownConfirmedHours, week.unavailableConfirmed)}</dd>
        <dt className="text-muted-foreground">{t('allocationComparison.proposed')}</dt><dd className="text-right" data-allocation-proposed>{allocationHours(week.proposedHours, week.knownProposedHours, week.unavailableProposed)}</dd>
        <dt className="text-muted-foreground">{t('allocationComparison.estimates')}</dt><dd className="text-right" data-allocation-estimate>{week.remainingEstimateHours == null ? t('allocationComparison.knownUnestimated', { hours: hours(week.knownRemainingEstimateHours), n: week.unestimatedTasks }) : hours(week.remainingEstimateHours)}</dd>
      </dl></td>)}</tr>)}
    </tbody></BareTable></TableRegion>
  </section>
}
