import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { ErrorBanner, Loading, Notice, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { approvalLabel, confidenceLabel, hoursLabel, sourceLabel, visibilityLabel, type PlannerEntry } from './labels'

type CellDetails = {
  personId: string; personName: string; week: string; partialView: boolean
  capacity: { weeklyCapacity: number; capacityOverride: boolean; workingDays: number; capacity: number; timeAway: number; additionalAvailability: number; days: { date: string; normal: number; available: number; category?: string; timeAway: number; additional: number }[] }
  approvedAllocations: { allocationId: string; projectId: string; projectNumber: string; projectName: string; hours: number; approvalStatus: string; canOpen: boolean }[]
  entries: (PlannerEntry & { weekHours: number; countedHours: number; coveredHours: number })[]
  bands: { confirmed: number; expected: number; possible: number }; remaining: number
  taskEstimates: { hours: number; unestimatedTasks: number }
  indicators: { code: string; rule: string; evaluated: boolean; active: boolean; threshold: number; explanation: string }[]
}

export function ContributionList({ personId, week, includeMyDrafts = true, draftAccessReason, onClose }: { personId: string; week: string; includeMyDrafts?: boolean; draftAccessReason?: string; onClose: () => void }) {
  const q = useQuery({ queryKey: ['planning-cell', personId, week, includeMyDrafts, draftAccessReason], queryFn: () => get<CellDetails>(`planning/cells/${personId}/${week}${qs({ includeMyDrafts: String(includeMyDrafts), draftAccessReason })}`) })
  const d = q.data
  return <Dialog open onOpenChange={(open) => !open && onClose()}><DialogContent className="max-h-[90dvh] max-w-2xl overflow-y-auto"><DialogHeader><DialogTitle>{t('planner.contributions')}</DialogTitle><DialogDescription>{d ? `${d.personName} · ${fmtDate(d.week)}` : t('planner.contributions')}</DialogDescription></DialogHeader>
    {q.isPending ? <Loading rows={5} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : d && <div className="space-y-5 text-sm">
      {d.partialView && <Notice title={t('planner.partialTitle')}>{t('planner.partialHint')}</Notice>}
      <section className="grid grid-cols-2 gap-3 sm:grid-cols-3"><Metric label={t('planner.weeklyCapacity')} value={hoursLabel(d.capacity.weeklyCapacity)} /><Metric label={t('planner.capacity')} value={hoursLabel(d.capacity.capacity)} /><Metric label={t('planner.workingDays')} value={String(d.capacity.workingDays)} /><Metric label={t('planner.timeAway')} value={hoursLabel(d.capacity.timeAway)} /><Metric label={t('planner.additionalAvailability')} value={hoursLabel(d.capacity.additionalAvailability)} /><Metric label={t('planner.remaining')} value={hoursLabel(d.remaining)} bad={d.remaining < 0} /><Metric label={t('planner.taskEstimates')} value={hoursLabel(d.taskEstimates.hours)} /><Metric label={t('planner.unestimatedTasks')} value={String(d.taskEstimates.unestimatedTasks)} /></section>
      <section><h3 className="mb-2 text-base/6 font-semibold">{t('planner.dayCapacity')}</h3><TableRegion role="region" aria-label={t('planner.dayCapacity')}><table className="w-full text-sm"><thead><tr><th className={thCls} scope="col">{t('common.date')}</th><th className={cn(thCls, 'text-right')} scope="col">{t('planner.normal')}</th><th className={cn(thCls, 'text-right')} scope="col">{t('planner.available')}</th><th className={thCls} scope="col">{t('planner.category')}</th></tr></thead><tbody>{d.capacity.days.map((day) => <tr key={day.date}><td className={tdCls}>{fmtDate(day.date)}</td><td className={cn(tdCls, 'text-right tabular-nums')}>{hoursLabel(day.normal)}</td><td className={cn(tdCls, 'text-right tabular-nums')}>{hoursLabel(day.available)}</td><td className={tdCls}>{day.category ? tv(day.category) : t('planner.noAvailabilityOverride')}</td></tr>)}</tbody></table></TableRegion></section>
      <section><h3 className="mb-2 text-base/6 font-semibold">{t('planner.bands')}</h3><div className="grid gap-2 sm:grid-cols-3"><Metric label={confidenceLabel('Confirmed')} value={hoursLabel(d.bands.confirmed)} /><Metric label={confidenceLabel('Expected')} value={hoursLabel(d.bands.expected)} /><Metric label={confidenceLabel('Possible')} value={hoursLabel(d.bands.possible)} /></div></section>
      <section><h3 className="mb-2 text-base/6 font-semibold">{t('planner.entries')}</h3><ul className="divide-y rounded-lg border">{d.entries.map((entry) => <li key={entry.id} className="space-y-1 px-3 py-3"><div className="flex flex-wrap items-center gap-2"><strong className="break-words">{entry.label}</strong><span className="tabular-nums">{hoursLabel(entry.countedHours)}</span></div><p className="text-xs text-muted-foreground">{sourceLabel(entry.sourceCategory)} · {confidenceLabel(entry.confidence)} · {visibilityLabel(entry.visibility, entry.ownerKind === 'Self')}</p><p className="text-xs">{t('planner.owner')}: {entry.ownerName} · {t(`planner.ownerKind.${entry.ownerKind}`)}</p>{entry.coveredHours > 0 && <p className="text-xs text-muted-foreground">{t('planner.coveredHours', { hours: hoursLabel(entry.coveredHours) })}</p>}<p className="text-xs text-muted-foreground">{entry.projectNumber} {entry.projectName ?? t('planner.noProject')} · {entry.disciplineName ?? t('planner.noDiscipline')} · {fmtDate(entry.startWeek)}–{fmtDate(entry.endWeek)}{entry.lastValidatedAt ? ` · ${t('planner.lastValidated')} ${fmtDate(entry.lastValidatedAt.slice(0, 10))}` : ''}</p>{(entry.warnings ?? []).map((warning) => <p key={warning} className="text-xs text-warn">{t(`planner.warning.${warning}`)}</p>)}</li>)}{d.entries.length === 0 && <li className="px-3 py-3">{t('planner.noEntriesForWeek')}</li>}</ul></section>
      {d.approvedAllocations.length > 0 && <section><h3 className="mb-2 text-base/6 font-semibold">{t('planner.approvedAllocations')}</h3><ul className="divide-y rounded-lg border">{d.approvedAllocations.map((a) => <li key={a.allocationId} className="flex flex-wrap justify-between gap-2 px-3 py-3">{a.canOpen ? <Link className="text-primary underline-offset-4 hover:underline" to={`/projects/${a.projectId}/allocations?allocation=${a.allocationId}`}>{a.projectNumber} {a.projectName}</Link> : <span>{a.projectNumber} {a.projectName}</span>}<span className="tabular-nums">{hoursLabel(a.hours)} · {approvalLabel(a.approvalStatus)}</span></li>)}</ul></section>}
      {d.indicators.map((i) => <p key={i.code} className={cn('rounded-md border px-3 py-2', i.active && 'border-warn/40 bg-warn-bg')}>{i.explanation}</p>)}
    </div>}
  </DialogContent></Dialog>
}

function Metric({ label, value, bad }: { label: string; value: string; bad?: boolean }) { return <div className="rounded-md border bg-muted p-3"><span className="block text-xs text-muted-foreground">{label}</span><strong className={cn('block tabular-nums', bad && 'text-bad')}>{value}</strong></div> }
