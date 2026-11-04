import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Field, Loading } from '@/components/hub/common'
import { DateReasonSelect, useDateReasons } from '@/components/hub/date-reason'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useUnsavedGuard } from '@/hooks/drafts'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { invalidateChange } from '@/lib/keys'

type Deadline = components['schemas']['CalendarScreenDeadline']
export function DeadlineDateDialog({ source, initialDate, projectIds, onClose, onSaved, onReload }: {
  source: Deadline; initialDate: string; projectIds: string; onClose: () => void; onSaved: (date: string) => void; onReload: () => Promise<void>
}) {
  const viewer = useMe(); const qc = useQueryClient()
  const [date, setDate] = useState(initialDate), [reason, setReason] = useState('')
  const [error, setError] = useState<unknown>(null), [pending, setPending] = useState(false), [uncertain, setUncertain] = useState(false)
  useUnsavedGuard(() => !pending && (reason.length > 0 || date !== initialDate), onClose)
  const preview = useQuery({ queryKey: ['calendar-date-preview', viewer.id, projectIds, source.sourceType, source.id, source.rowVersion, date],
    enabled: !!date && !uncertain, retry: false,
    queryFn: () => typedRequest('get', '/api/v1/calendar/screen/{type}/{id}/date-preview', { params: { type: source.sourceType, id: source.id },
      query: { dueDate: date, rowVersion: source.rowVersion, projectIds } }) })
  const current = !preview.error && preview.data?.source.id === source.id && preview.data.source.rowVersion === source.rowVersion && preview.data.dueDate === date ? preview.data : undefined
  const needsReason = source.needsReason || current?.source.needsReason === true
  const [category, setCategory] = useState(''), needsWhy = useDateReasons().required && source.sourceType !== 'Task' && !!source.dueDate // FR-SCR-65
  const save = async () => {
    if (!current || !current.source.canMove || pending || uncertain || needsReason && !reason.trim() || needsWhy && !category) return
    setPending(true); setError(null)
    try {
      if (source.sourceType === 'Task') await typedRequest('patch', '/api/v1/tasks/{id}', { params: { id: source.id }, body: { dueDate: date, reason: reason || undefined }, ifMatch: source.rowVersion })
      else if (source.sourceType === 'Deliverable') await typedRequest('patch', '/api/v1/deliverables/{id}', { params: { id: source.id }, body: { dueDate: date, reason: reason || undefined, ...(category ? { dateReasonCategoryId: category } : {}) }, ifMatch: source.rowVersion })
      else if (source.sourceType === 'Milestone') await typedRequest('post', '/api/v1/milestones/{id}/change-date', { params: { id: source.id },
        body: { newDate: date, reason: reason || null, cascadeDeliverables: false, includeTasks: false, dryRun: false, rowVersion: source.rowVersion, ...(category ? { dateReasonCategoryId: category } : {}) } })
      else throw new Error(t('calendarScreen.cannotMove'))
      await invalidateChange(qc, source.sourceType, source.projectId, source.id)
      toast.success(t('calendarScreen.saved', { key: source.key })); onSaved(date)
    } catch (failure) {
      setError(failure)
      if (!(failure instanceof ApiError) || [0, 403, 404, 409].includes(failure.status) || failure.status >= 500) setUncertain(true)
    } finally { setPending(false) }
  }
  return <Dialog open onOpenChange={open => { if (!open && !pending) onClose() }}>
    <DialogContent className="sm:max-w-xl">
      <DialogHeader><DialogTitle>{t('calendarScreen.moveTitle', { key: source.key })}</DialogTitle>
        <DialogDescription>{t('calendarScreen.onlyDue')}</DialogDescription></DialogHeader>
      <div className="space-y-4" data-calendar-date-dialog data-source-version={source.rowVersion}>
        <p className="break-words text-sm">{source.title} · {source.dueDate ? fmtDate(source.dueDate) : t('calendarScreen.undated')} → {date ? fmtDate(date) : '—'}</p>
        <Field label={t('calendarScreen.targetDate')} htmlFor="calendar-target-date"><Input id="calendar-target-date" type="date" value={date} disabled={pending || uncertain} onChange={event => setDate(event.target.value)} /></Field>
        {preview.isPending && !uncertain && <Loading rows={2} />}
        {preview.error && <ErrorBanner error={preview.error} />}
        {error != null && <ErrorBanner error={error} />}
        {current && <div className="space-y-2 text-sm" data-calendar-date-preview>
          {current.context?.dates.newInconsistencies.map((warning, index) => <p className="text-warn" key={`own-${index}`}>{warning.text}</p>)}
          <ul className="space-y-1 text-warn">{[...(current.context?.dates.successors ?? []), ...(current.context?.tasks ?? [])].map(item => <li key={item.id}>{item.key}: {item.reason.text}</li>)}</ul>
          {current.context?.availability?.map(item => <p className="text-warn" key={item.date}>{fmtDate(item.date)}: {item.message}{item.nextAvailableDate ? ` · ${fmtDate(item.nextAvailableDate)}` : ''}</p>)}
          {current.milestone?.inconsistent.map(item => <p className="text-warn" key={item.key}>{t('calendarScreen.childAfter', { key: item.key, date: item.dueDate ? fmtDate(item.dueDate) : '—' })}</p>)}
        </div>}
        {needsWhy && <DateReasonSelect id="calendar-move-why" value={category} onChange={setCategory} />}
        <Field label={t('calendarScreen.reason')} htmlFor="calendar-move-reason" optional={!needsReason}>
          <Textarea id="calendar-move-reason" value={reason} disabled={pending} onChange={event => setReason(event.target.value)} />
        </Field>
        {uncertain && <p className="text-sm text-muted-foreground" role="status">{t('calendarScreen.restartRequired')}</p>}
      </div>
      <DialogFooter>
        <Button variant="outline" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
        {(uncertain || preview.error instanceof ApiError && preview.error.status === 409) && <Button variant="outline" disabled={pending} onClick={async () => { setPending(true); try { await onReload() } finally { setPending(false) } }}>{t('calendarScreen.reloadRestart')}</Button>}
        <Button disabled={pending || uncertain || !current?.source.canMove || !date || date === source.dueDate || needsReason && !reason.trim() || needsWhy && !category} onClick={save}>{t('calendarScreen.confirm')}</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
}
