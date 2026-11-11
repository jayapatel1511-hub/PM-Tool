import type { components } from '@/gen/api'
import { hours, shortDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { TableRegion, tdCls, thCls } from '@/components/hub/common'
import { BareTable } from '@/components/ui/table'

/** FR-SCR-47: three independent figures, with coverage instead of an invented spare-capacity total. */
export function WorkloadDisciplineContext({ rows, weeks }: {
  rows: components['schemas']['DisciplineCapacity'][]; weeks: string[]
}) {
  return <section className="space-y-3">
    <div><h2 id="workload-discipline-context" className="text-base font-medium">{t('capacity.disciplineContext')}</h2><p className="max-w-4xl text-sm text-muted-foreground">{t('capacity.disciplineBasis')}</p></div>
    <TableRegion role="region" aria-labelledby="workload-discipline-context"><BareTable className="w-full min-w-max text-sm"><caption className="sr-only">{t('capacity.disciplineContext')}</caption><thead><tr><th scope="col" className={thCls}>{t('capacity.homeDiscipline')}</th>{weeks.map((week) => <th key={week} scope="col" className={thCls}>{t('planner.weekOf')} {shortDate(week)}</th>)}</tr></thead><tbody>{rows.map((row) => <tr key={row.disciplineId ?? 'none'}>
      <th scope="row" className={`${tdCls} align-top font-normal`}><span className="font-medium">{row.disciplineName ?? t('capacity.disciplineNotSet')}</span><p className="text-xs text-muted-foreground">{t('capacity.peopleInScope', { n: row.people })}</p>{row.undatedUnassignedTasks > 0 && <p className="text-xs text-muted-foreground">{t('capacity.undatedDemand', { n: row.undatedUnassignedTasks })}</p>}</th>
      {row.weeks.map((week) => <td key={week.week} className={`${tdCls} align-top`}><dl className="grid grid-cols-[auto_auto] gap-x-4 gap-y-1 text-xs tabular-nums">
        <dt className="text-muted-foreground">{t('capacity.freeToPlan')}</dt><dd className="text-right">{week.freeHours == null ? t('common.unavailable') : t('capacity.hoursValue', { h: hours(week.freeHours) })}</dd>
        <dt className="text-muted-foreground">{t('capacity.openRoleHours')}</dt><dd className="text-right">{t('capacity.hoursValue', { h: hours(week.unfilledRoleHours) })}</dd>
        <dt className="text-muted-foreground">{t('capacity.unassignedTaskHours')}</dt><dd className="text-right">{t('capacity.hoursValue', { h: hours(week.knownUnassignedTaskHours) })}{week.unestimatedTasks > 0 && <> · {t('capacity.unestimatedDemand', { n: week.unestimatedTasks })}</>}</dd>
      </dl>{week.unavailablePeople > 0 && <p className="mt-2 text-xs text-muted-foreground">{t('capacity.unverifiedSupply', { n: week.unavailablePeople })}</p>}{week.shortfallHours != null && week.shortfallHours > 0 && <p className="mt-2 text-xs font-medium">{t('capacity.shortBy', { h: hours(week.shortfallHours) })}</p>}</td>)}
    </tr>)}</tbody></BareTable></TableRegion>
  </section>
}
