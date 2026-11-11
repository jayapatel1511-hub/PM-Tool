import type { components } from '@/gen/api'
import { TableRegion, tdCls, thCls } from '@/components/hub/common'
import { BareTable } from '@/components/ui/table'
import { fmtDate, hours } from '@/lib/format'
import { t } from '@/lib/i18n'

export function ProposalCapacityPreview({ data }: { data: components['schemas']['ProposalCapacityRead'] }) {
  if (data.state === 'OutsideScope') return null
  const incomplete = data.state === 'Incomplete'
  const amount = (value: number | null) => value == null ? t('common.unavailable') : incomplete ? t('allocationCapacity.knownHours', { hours: hours(value) }) : hours(value)
  return <section className="space-y-2" aria-live="polite" data-allocation-capacity>
    <h3 id="allocation-capacity-title" className="text-sm font-medium">{t('allocationCapacity.title')}</h3>
    <p className="text-xs text-muted-foreground">{t('allocationCapacity.basis', { from: fmtDate(data.fromDate), through: fmtDate(data.throughDate) })}</p>
    {data.state === 'Partial' && <p role="status" className="text-xs text-muted-foreground">{t('allocationCapacity.partial')}</p>}
    {data.state === 'Unavailable' && <p role="status" className="text-xs text-muted-foreground">{t('allocationCapacity.unavailable')}</p>}
    {incomplete && <p role="status" className="text-xs text-muted-foreground">{t('allocationCapacity.incomplete', { unestimated: data.unestimatedTasks ?? 0, undated: data.undatedTasks ?? 0 })}</p>}
    {data.weeks.some(week => week.provisional) && <p role="status" className="text-xs text-muted-foreground">{t('allocationCapacity.provisional')}</p>}
    {!data.includesProposal && <p className="text-xs text-muted-foreground">{t('allocationCapacity.completeProposal')}</p>}
    <TableRegion role="region" aria-labelledby="allocation-capacity-title"><BareTable className="w-full min-w-max text-xs tabular-nums"><caption className="sr-only">{t('allocationCapacity.title')}</caption><thead><tr>
      <th scope="col" className={thCls}>{t('planner.weekOf')}</th><th scope="col" className={thCls}>{t('allocationCapacity.available')}</th><th scope="col" className={thCls}>{t('allocationCapacity.before')}</th><th scope="col" className={thCls}>{t('allocationCapacity.after')}</th><th scope="col" className={thCls}>{t('allocationCapacity.assessment')}</th>
    </tr></thead><tbody>{data.weeks.map(week => <tr key={week.week} data-allocation-capacity-week={week.week}>
      <th scope="row" className={`${tdCls} text-left font-medium`}>{fmtDate(week.week)}</th>
      <td className={tdCls} data-capacity-available>{week.availableHours == null ? t('common.unavailable') : hours(week.availableHours)}</td>
      <td className={tdCls} data-capacity-before>{amount(week.beforeHours)}</td><td className={tdCls} data-capacity-after>{data.includesProposal ? amount(week.afterHours) : '—'}</td>
      <td className={`${tdCls} ${week.overCapacity === true ? 'font-medium text-bad' : 'text-muted-foreground'}`} data-capacity-assessment>{week.overCapacity == null ? t('common.unavailable') : t(week.overCapacity ? 'allocationCapacity.over' : 'allocationCapacity.within')}</td>
    </tr>)}</tbody></BareTable></TableRegion>
  </section>
}
