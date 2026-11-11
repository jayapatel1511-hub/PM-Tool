import { useId, useRef, useState } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { Empty, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { BareTable } from '@/components/ui/table'
import { ApiError, typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import './time-week.css'

type Week = components['schemas']['TimeWeekView']
type Task = components['schemas']['TimeWeekTask']
type Entry = components['schemas']['TimeRow']
const hours = (value: number) => String(Number(value.toFixed(2)))
const DAYS = ['calendar.dow.0', 'calendar.dow.1', 'calendar.dow.2', 'calendar.dow.3', 'calendar.dow.4', 'calendar.dow.5', 'calendar.dow.6'] as const
const weekday = (date: string) => t(DAYS[new Date(`${date}T00:00:00Z`).getUTCDay()])

/** Own actuals only. Suggestions are blank; neither focus nor rendering creates an entry. */
export function TimeWeekGrid({ data, onEdit, onRemove, onSaved }: { data: Week; onEdit: (entry: Entry) => void; onRemove: (entry: Entry) => void; onSaved: () => Promise<unknown> }) {
  const [details, setDetails] = useState<{ task: Task; date: string; entries: Entry[] } | null>(null)
  return <section className="min-w-0 space-y-2" aria-label={t('timeWeek.title')}>
    <p className="text-sm font-semibold" data-time-week-capacity>{data.capacityHours == null ? t('timeWeek.capacityUnknown') : t('timeWeek.capacity', { hours: hours(data.capacityHours) })}</p>
    <TableRegion role="region" aria-label={t('timeWeek.region')}><BareTable className="time-week-table w-full text-sm">
      <caption className="sr-only">{t('timeWeek.region')}</caption><thead><tr>
        <th scope="col" className={thCls}>{t('time.task')}</th>
        {data.days.map(day => <th key={day.date} scope="col" className={thCls} data-time-day={day.date} data-time-away={day.timeAwayHours != null && day.timeAwayHours > 0 ? 'true' : undefined}>
          <span className="block font-medium">{weekday(day.date)}</span><span className="block whitespace-nowrap font-normal tabular-nums">{fmtDate(day.date)}</span>
          <span className="mt-1 block font-normal text-muted-foreground">{day.unavailable || day.availableHours == null ? t('timeWeek.unavailable') : t(day.provisional ? 'timeWeek.provisionalAvailable' : 'timeWeek.available', { hours: hours(day.availableHours) })}</span>
          {day.timeAwayHours != null && day.timeAwayHours > 0 && <span className="block font-normal">{t('timeWeek.away', { hours: hours(day.timeAwayHours) })}</span>}
          {day.holidayHours != null && day.holidayHours > 0 && <span className="block font-normal">{t('timeWeek.holiday', { hours: hours(day.holidayHours) })}</span>}
          {day.additionalHours != null && day.additionalHours > 0 && <span className="block font-normal">{t('timeWeek.additional', { hours: hours(day.additionalHours) })}</span>}
        </th>)}
        <th scope="col" className={thCls}>{t('timeWeek.actualTotal')}</th>
      </tr></thead><tbody>
        {data.tasks.map(task => <tr key={task.taskId} className="border-t" data-time-week-task={task.taskId}>
          <th scope="row" className={`${tdCls} text-left align-top font-normal`}><Key>{task.taskKey}</Key><span className="mt-1 block font-medium">{task.taskName}</span><span className="mt-1 block text-xs text-muted-foreground">{task.projectNumber} {task.projectName}</span>
            <span className="block text-xs text-muted-foreground">{[task.assigned && t('timeWeek.assigned'), task.previousWeek && t('timeWeek.previous'), task.deleted && t('timeWeek.deletedTask')].filter(Boolean).join(' · ')}</span>
          </th>
          {data.days.map(day => {
            const entries = task.entries.filter(entry => entry.workDate === day.date)
            const label = t('timeWeek.cell', { key: task.taskKey, task: task.taskName, date: fmtDate(day.date) })
            const sum = entries.reduce((value, entry) => value + entry.hours, 0)
            return <td key={day.date} className={`${tdCls} align-top`} data-time-cell={day.date} data-time-away={day.timeAwayHours != null && day.timeAwayHours > 0 ? 'true' : undefined}>
              {entries.length > 1 ? <Button variant="outline" size="sm" className="h-auto min-h-9 w-full flex-col gap-0.5 whitespace-normal" aria-label={t('timeWeek.multiple', { label, hours: hours(sum), n: entries.length })} onClick={() => setDetails({ task, date: day.date, entries })}><span className="tabular-nums">{hours(sum)} h</span><span className="text-xs">{t('timeWeek.entryCount', { n: entries.length })}</span></Button>
                : entries.length === 1 && !entries[0].canEdit ? <span className="block py-2 text-center tabular-nums" aria-label={label}>{hours(sum)} h</span>
                  : entries.length === 1 || task.canCreate ? <TimeCell key={`${task.taskId}:${day.date}:${entries.map(entry => `${entry.id}:${entry.rowVersion}`).join(',')}`} task={task} date={day.date} entry={entries[0]} label={label} onSaved={onSaved} />
                    : <span className="block py-2 text-center text-muted-foreground" aria-label={`${label} · ${t('timeWeek.noCreate')}`}>—</span>}
              {entries.length === 1 && <Button variant="ghost" size="sm" className="mt-1 h-auto min-h-6 w-full whitespace-normal px-0 text-xs" aria-label={t('timeWeek.detailsFor', { label })} onClick={() => setDetails({ task, date: day.date, entries })}>{t('timeWeek.details')}</Button>}
            </td>
          })}
          <td className={`${tdCls} text-right align-top font-medium tabular-nums`} data-time-task-total>{hours(task.entries.reduce((value, entry) => value + entry.hours, 0))} h</td>
        </tr>)}
      </tbody><tfoot><tr className="border-t"><th scope="row" className={`${thCls} text-left`}>{t('time.dailyTotals')}</th>{data.days.map(day => <td key={day.date} className={`${tdCls} text-center font-medium tabular-nums`} data-time-day-total={day.date}>{hours(data.tasks.reduce((value, task) => value + task.entries.filter(entry => entry.workDate === day.date).reduce((sum, entry) => sum + entry.hours, 0), 0))} h</td>)}<td className={`${tdCls} text-right font-semibold tabular-nums`} data-time-week-total>{hours(data.tasks.reduce((value, task) => value + task.entries.reduce((sum, entry) => sum + entry.hours, 0), 0))} h</td></tr></tfoot>
    </BareTable></TableRegion>
    {!data.tasks.length && <Empty>{t('timeWeek.empty')}</Empty>}
    <p className="text-xs text-muted-foreground">{t('timeWeek.hint')} {data.capacityHours == null && t('timeWeek.unknownHint')}</p>
    {details && <Dialog open onOpenChange={open => !open && setDetails(null)}><DialogContent className="sm:max-w-lg"><DialogHeader><DialogTitle>{t('timeWeek.detailsTitle', { key: details.task.taskKey, date: fmtDate(details.date) })}</DialogTitle><DialogDescription>{t('timeWeek.multipleHint')}</DialogDescription></DialogHeader>
      <ul className="space-y-3">{details.entries.map(entry => <li key={entry.id} className="flex items-start gap-2 rounded-md border p-3"><div className="min-w-0 flex-1"><p className="font-medium tabular-nums">{hours(entry.hours)} h</p><p className="break-words text-sm text-muted-foreground">{entry.note || t('timeWeek.noNote')}</p></div>{entry.canEdit && <><Button variant="ghost" size="icon-sm" aria-label={t('time.edit', { key: entry.taskKey })} onClick={() => { setDetails(null); onEdit(entry) }}><Pencil className="size-4" /></Button><Button variant="ghost" size="icon-sm" aria-label={t('time.delete', { key: entry.taskKey })} onClick={() => { setDetails(null); onRemove(entry) }}><Trash2 className="size-4" /></Button></>}</li>)}</ul>
    </DialogContent></Dialog>}
  </section>
}

function TimeCell({ task, date, entry, label, onSaved }: { task: Task; date: string; entry?: Entry; label: string; onSaved: () => Promise<unknown> }) {
  const feedbackId = useId()
  const [value, setValue] = useState(entry ? hours(entry.hours) : '')
  const [error, setError] = useState<unknown>(null), [busy, setBusy] = useState(false), [conflict, setConflict] = useState(false), [unknownCreate, setUnknownCreate] = useState(false)
  const [acknowledged, setAcknowledged] = useState(false)
  const dirty = useRef(false), inFlight = useRef(false), saved = useRef(false), receipt = useRef<{ signature: string; key: string } | null>(null)
  async function reload() {
    if (inFlight.current) return
    inFlight.current = true; setBusy(true); setError(null)
    try { await onSaved() } catch (failure) { setError(failure) }
    finally { inFlight.current = false; setBusy(false) }
  }
  async function commit() {
    if (!dirty.current || inFlight.current || conflict || saved.current) return
    const number = Number(value)
    if (!value.trim() || !Number.isFinite(number) || number <= 0 || number > 24 || Math.round(number * 100) / 100 !== number) { setError(t('timeWeek.hoursRule')); return }
    if (entry && number === entry.hours) { dirty.current = false; setError(null); return }
    inFlight.current = true; setBusy(true); setError(null)
    try {
      if (entry) await typedRequest('patch', '/api/v1/time/{id}', { params: { id: entry.id }, body: { hours: number }, ifMatch: entry.rowVersion })
      else {
        const body = { taskId: task.taskId, workDate: date, hours: number, note: null }
        const signature = JSON.stringify(body)
        if (receipt.current?.signature !== signature) receipt.current = { signature, key: crypto.randomUUID() }
        await typedRequest('post', '/api/v1/time', { body, idempotencyKey: receipt.current.key })
      }
      // The entry/version-keyed component must be replaced by an authoritative read before accepting another edit.
      // A known successful POST followed by a failed/delayed refresh can never fall back into the blank POST path.
      saved.current = true; setAcknowledged(true); setUnknownCreate(false)
      dirty.current = false; toast.success(t('common.saved')); await onSaved()
    } catch (failure) {
      setError(failure)
      if (!saved.current && entry && failure instanceof ApiError && [403, 404, 409, 428].includes(failure.status)) setConflict(true)
      if (!saved.current && !entry) setUnknownCreate(failure instanceof ApiError && (failure.status === 0 || failure.status >= 500 || failure.code === 'idempotency_in_progress'))
    }
    finally { inFlight.current = false; setBusy(false) }
  }
  const message = typeof error === 'string' ? error : error instanceof ApiError ? Object.values(error.fieldErrors).flat().join(' ') || error.message : error instanceof Error ? error.message : ''
  return <div className="space-y-1"><Input className="min-w-20 text-center tabular-nums" type="number" inputMode="decimal" min="0.01" max="24" step="0.01" aria-label={label} aria-invalid={error ? true : undefined} aria-describedby={message ? feedbackId : undefined} value={value} disabled={busy || conflict || unknownCreate || acknowledged} onChange={event => { setValue(event.target.value); dirty.current = true; setError(null) }} onBlur={() => void commit()} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); void commit(); event.currentTarget.blur() } }} />
    {busy && !acknowledged && <span role="status" className="block text-xs text-muted-foreground">{t('common.saving')}</span>}
    {acknowledged && <p role="status" className="text-xs text-muted-foreground">{t('timeWeek.savedAwaiting')}</p>}
    {message && <p id={feedbackId} role="alert" className="break-words text-xs text-bad">{message}</p>}
    {unknownCreate && <p role="status" className="text-xs text-muted-foreground">{t('timeWeek.retryHint')}</p>}
    {(error != null || acknowledged) && <Button variant="outline" size="sm" className="h-auto min-h-7 w-full whitespace-normal px-1 text-xs" disabled={busy} onClick={() => void (conflict || acknowledged ? reload() : commit())}>{t(conflict || acknowledged ? 'error.reload' : 'app.retry')}</Button>}
  </div>
}
