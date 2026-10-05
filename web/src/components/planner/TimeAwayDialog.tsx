import { useQuery } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { ErrorBanner, Field, Loading, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { ApiError, api, get, qs } from '@/lib/api'
import { t } from '@/lib/i18n'
import type { PlannerPerson } from './labels'

type Override = { workDate: string; rowVersion: number }
type AvailabilityResponse = Override[] | { days?: Override[]; Days?: Override[] }
export function TimeAwayDialog({ person, onClose, onChanged }: { person: PlannerPerson | null; onClose: () => void; onChanged: () => void }) {
  const [from, setFrom] = useState(''); const [through, setThrough] = useState(''); const [hours, setHours] = useState('0'); const [category, setCategory] = useState('Unavailable'); const [action, setAction] = useState('Record'); const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null); const [needsReload, setNeedsReload] = useState(false); const retryAction = useRef<() => Promise<void>>(async () => {}); const idempotencyKey = useRef<string | null>(null); const lastBody = useRef('')
  const validRange = !!from && !!through && from <= through
  const q = useQuery({ queryKey: ['availability', person?.id, from, through], queryFn: () => get<AvailabilityResponse>(`users/${person!.id}/availability${qs({ from, through })}`), enabled: !!person && validRange })
  const overrides = Array.isArray(q.data) ? q.data : q.data?.days ?? q.data?.Days ?? []
  const [reloading, setReloading] = useState(false)
  const reloadAvailability = async () => {
    if (reloading) return false
    setReloading(true)
    try {
      const next = await q.refetch()
      if (next.error || !next.data) { setNeedsReload(true); setError(next.error ?? new ApiError(404, { detail: t('app.notFound') })); return false }
      setNeedsReload(false); setError(null); idempotencyKey.current = null; lastBody.current = ''; return true
    } finally { setReloading(false) }
  }
  const submit = async () => {
    retryAction.current = submit
    if (!person || !validRange || q.isPending || q.isFetching || q.isError || needsReload || !q.data) return
    const body = { personId: person.id, from, through, action, availableHours: Number(hours), category, versions: overrides }
    const signature = JSON.stringify(body)
    if (lastBody.current !== signature) { idempotencyKey.current = null; lastBody.current = signature }
    idempotencyKey.current ??= crypto.randomUUID()
    setBusy(true); setError(null)
    try { await api('planning/time-away', { method: 'POST', idempotencyKey: idempotencyKey.current, body }); idempotencyKey.current = null; lastBody.current = ''; onChanged(); onClose() }
    catch (e) { if (e instanceof ApiError && (e.status === 409 || e.code === 'concurrency_conflict')) { setNeedsReload(true); idempotencyKey.current = null; lastBody.current = '' } setError(e) }
    finally { setBusy(false) }
  }
  const change = <T,>(setter: (value: T) => void, value: T) => { setter(value); if (!needsReload) setError(null) }
  const canSave = !busy && !reloading && validRange && !q.isPending && !q.isFetching && !q.isError && !needsReload && !!q.data && !(action === 'Record' && (Number(hours) < 0 || Number(hours) > 24))
  const displayError = error ?? q.error
  const retry = needsReload || q.error ? () => void reloadAvailability() : () => void retryAction.current()
  return <Dialog open={!!person} onOpenChange={(o) => !o && onClose()}><DialogContent className="max-w-md"><DialogHeader><DialogTitle>{t('planner.timeAwayTitle', { person: person?.displayName ?? '' })}</DialogTitle><DialogDescription>{t('planner.timeAwayHint')}</DialogDescription></DialogHeader>{displayError != null && <ErrorBanner error={displayError} retry={retry} retryLabel={needsReload ? t('error.reload') : undefined} />}{q.isPending && <Loading rows={1} />}<div className="grid gap-3 sm:grid-cols-2"><Field label={t('common.from')} htmlFor="away-from"><Input id="away-from" type="date" value={from} disabled={busy || reloading} onChange={(e) => change(setFrom, e.target.value)} /></Field><Field label={t('common.to')} htmlFor="away-through"><Input id="away-through" type="date" min={from || undefined} value={through} disabled={busy || reloading} onChange={(e) => change(setThrough, e.target.value)} /></Field><Field label={t('planner.action')} htmlFor="away-action"><select id="away-action" className="h-(--control-h) rounded-md border border-input bg-card px-3 text-sm" value={action} disabled={busy || reloading} onChange={(e) => change(setAction, e.target.value)}><option value="Record">{t('planner.record')}</option><option value="Clear">{t('planner.clear')}</option></select></Field><Field label={t('planner.category')} htmlFor="away-category"><select id="away-category" className="h-(--control-h) rounded-md border border-input bg-card px-3 text-sm" value={category} disabled={busy || reloading || action === 'Clear'} onChange={(e) => change(setCategory, e.target.value)}><option value="Unavailable">{t('planner.unavailable')}</option><option value="Reduced">{t('planner.reduced')}</option></select></Field>{action === 'Record' && <Field label={t('planner.availableHours')} htmlFor="away-hours"><Input id="away-hours" type="number" min="0" max="24" step="0.5" value={hours} disabled={busy || reloading} onChange={(e) => change(setHours, e.target.value)} /></Field>}</div><DialogFooter><Button variant="outline" onClick={onClose} disabled={busy || reloading}>{t('common.cancel')}</Button><Button onClick={() => void submit()} disabled={!canSave}>{busy && <Spinner />}{t('common.save')}</Button></DialogFooter></DialogContent></Dialog>
}
