import type { components } from '@/gen/api'
import { useState } from 'react'
import { useLocation } from 'react-router'
import { useItemPanel } from '@/components/hub/panel-host'
import { ItemMark } from '@/components/hub/item-mark'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { addDays, fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'

export type DeadlineContext = NonNullable<components['schemas']['GridResult']['deadlines']>
type Deadline = DeadlineContext['dated'][number]

function DeadlineButton({ record, onOpened }: { record: Deadline; onOpened?: () => void }) {
  const open = useItemPanel(), location = useLocation()
  const kind = record.itemType === 'Milestone' ? 'Milestone' : 'Deliverable'
  return <button type="button" className="flex min-h-8 w-full items-start gap-2 rounded-md px-2 py-1 text-left text-xs font-normal hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring" onClick={(event) => {
    const origin = captureRecordContextOrigin(location, event.currentTarget)
    onOpened?.(); open(kind, record.id, origin)
  }}><ItemMark kind={kind} className="mt-1 shrink-0" /><span><span className="font-medium">{record.projectNumber} · {record.key}</span><span className="block">{record.name}</span><span className="block text-muted-foreground">{t(`itemType.${kind}`)} · {record.date ? fmtDate(record.date) : t('planner.deadlineNoDate')}</span></span></button>
}

function TypeMarker({ records, kind }: { records: Deadline[]; kind: 'Milestone' | 'Deliverable' }) {
  const [open, setOpen] = useState(false)
  const dates = [...new Set(records.map(record => record.date).filter((date): date is string => !!date))].map(fmtDate).join(', ')
  const label = t('planner.deadlineMarker', { type: t(`itemType.${kind}`), n: records.length, dates })
  return <Popover open={open} onOpenChange={setOpen}><PopoverTrigger asChild><Button variant="ghost" size="sm" className="h-auto min-h-8 justify-start px-1 text-xs font-normal" aria-label={label}><ItemMark kind={kind} />{t(`itemType.${kind}`)} · {records.length}</Button></PopoverTrigger><PopoverContent align="start" className="max-h-72 w-80 overflow-y-auto p-2" aria-label={label}><ul>{records.map(record => <li key={`${record.itemType}:${record.id}`}><DeadlineButton record={record} onOpened={() => setOpen(false)} /></li>)}</ul></PopoverContent></Popover>
}

/** A marker's dated list is the exact same authorized set and week(s) as its header. */
export function PlanningDeadlineMarkers({ context, weeks }: { context?: DeadlineContext | null; weeks: string[] }) {
  const records = context?.dated.filter(record => !!record.date && weeks.some(week => record.date! >= week && record.date! < addDays(week, 7))) ?? []
  if (!records.length) return null
  return <div className="mt-1 flex flex-col items-start" data-deadline-markers>{(['Milestone', 'Deliverable'] as const).map(kind => {
    const group = records.filter(record => record.itemType === kind)
    return group.length > 0 && <TypeMarker key={kind} records={group} kind={kind} />
  })}</div>
}

export function PlanningUndatedTray({ context }: { context?: DeadlineContext | null }) {
  if (!context?.undated.length) return null
  return <details className="rounded-lg border bg-card px-3 py-2"><summary className="cursor-pointer text-sm font-medium">{t('planner.undatedDeadlines', { n: context.undated.length })}</summary><ul className="mt-2 grid gap-1 sm:grid-cols-2 lg:grid-cols-3">{context.undated.map(record => <li key={`${record.itemType}:${record.id}`}><DeadlineButton record={record} /></li>)}</ul></details>
}

/** Allocations has a date-range table; this compact header strip adds week context without changing that table. */
export function AllocationDeadlineHeaders({ context }: { context: DeadlineContext }) {
  return <section aria-labelledby="allocation-deadline-title" className="space-y-2"><h2 id="allocation-deadline-title" className="text-sm font-medium">{t('planner.projectDeadlines')}</h2><p className="text-xs text-muted-foreground">{t('planner.projectDeadlineBasis')}</p><div className="overflow-auto rounded-lg border bg-card"><table className="w-full min-w-max text-sm"><caption className="sr-only">{t('planner.projectDeadlines')}</caption><thead><tr>{context.weeks.map(week => <th key={week} scope="col" className="min-w-36 border-r p-3 text-left align-top font-medium">{t('planner.weekOf')} {fmtDate(week)}<PlanningDeadlineMarkers context={context} weeks={[week]} /></th>)}</tr></thead></table></div><PlanningUndatedTray context={context} /></section>
}
