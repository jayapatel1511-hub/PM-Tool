import { useQuery } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Field, Loading, Spinner } from '@/components/hub/common'
import { StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, get, post } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'

/** A task start the server refused for readiness (with the transition `body` to retry), or, without one, a PM/lead
 *  recording a start authorisation for the performer (FR-RDY-02). */
export interface StartRequest { id: string; key: string; to?: string; body?: Record<string, unknown> }

interface StartReadiness {
  readinessState: string; assessed: boolean; unknown: string[]; blocked: string[]; note?: string | null
  needsAuthorisation: boolean; canAuthorise: boolean
  authorisation?: Authorisation | null; unusableAuthorisation?: Authorisation | null
}
interface Authorisation { id: string; authorisedByName?: string | null; reason: string; createdAt: string }

/** Readiness warning for a start that is Not Ready or Needs Assessment: the starter acknowledges it and gives a reason;
 *  a PM or Discipline Lead authorises inline, anyone else needs the authorisation recorded for this readiness. */
export function StartAuthorisationDialog({ req, onClose }: { req: StartRequest; onClose: (done: boolean) => void }) {
  const q = useQuery({ queryKey: ['task-start', req.id], queryFn: () => get<StartReadiness>(`tasks/${req.id}/start-readiness`), gcTime: 0 })
  const [ack, setAck] = useState(false), [reason, setReason] = useState(''), [busy, setBusy] = useState(false), [err, setErr] = useState<unknown>(null)
  const receipt = useRef<{ signature: string; id: string } | null>(null)
  const starting = !!req.body, r = q.data
  const valid = ack && reason.trim().length >= 5
  const permitted = !!r && r.needsAuthorisation && (r.canAuthorise || (starting && !!r.authorisation))
  const run = async (f: () => Promise<unknown>) => {
    setBusy(true); setErr(null)
    try { await f(); onClose(true) } catch (e) { setErr(e); q.refetch() } finally { setBusy(false) }
  }
  const start = () => run(() => post(`tasks/${req.id}/transition`, r?.needsAuthorisation ? { ...req.body, acknowledgeReadiness: true, reason: reason.trim() } : req.body))
  const record = () => run(async () => {
    const payload = { acknowledged: true, reason: reason.trim() }, signature = JSON.stringify(payload)
    if (receipt.current?.signature !== signature) receipt.current = { signature, id: crypto.randomUUID() }
    await post(`tasks/${req.id}/start-authorisations`, { ...payload, requestId: receipt.current.id })
    toast.success(t('tasks.startAuthRecorded', { key: req.key }))
  })
  return (
    <Dialog open onOpenChange={(o) => !o && !busy && onClose(false)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t(starting ? 'tasks.startAuthTitle' : 'tasks.startAuthRecordTitle', { key: req.key })}</DialogTitle>
          <DialogDescription>{t(starting ? 'tasks.startAuthHint' : 'tasks.startAuthRecordHint')}</DialogDescription>
        </DialogHeader>
        {q.isPending ? <Loading rows={3} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : r && (
          <div className="space-y-3 text-sm">
            <p className="flex items-center gap-2">{t('tasks.startAuthState')}: <StatusPill status={r.readinessState} /></p>
            {!r.assessed && <p>{t('tasks.startAuthNotAssessed')}</p>}
            {r.note && <p>{t('tasks.startAuthNote', { note: r.note })}</p>}
            {r.blocked.length > 0 && <p>{t('tasks.startAuthBlocked', { list: r.blocked.map(tv).join(', ') })}</p>}
            {r.unknown.length > 0 && <p>{t('tasks.startAuthUnknown', { list: r.unknown.map(tv).join(', ') })}</p>}
            {!r.needsAuthorisation ? <p role="status">{t(starting ? 'tasks.startAuthNowReady' : 'tasks.startAuthNotNeeded', { state: tv(r.readinessState) })}</p> : <>
              {r.authorisation && <p>{t('tasks.startAuthExisting', { name: r.authorisation.authorisedByName ?? t('coord.unavailable'), date: fmtDate(r.authorisation.createdAt), reason: r.authorisation.reason })}</p>}
              {r.unusableAuthorisation && <p className="text-warn">{t('tasks.startAuthUnusable', { name: r.unusableAuthorisation.authorisedByName ?? t('coord.unavailable'), date: fmtDate(r.unusableAuthorisation.createdAt) })}</p>}
              {!permitted && <p role="status" className="text-warn">{t('tasks.startAuthAsk')}</p>}
              {permitted && <>
                <div className="flex items-start gap-2">
                  <Checkbox id="start-ack" className="mt-0.5" autoFocus checked={ack} onCheckedChange={(v) => setAck(v === true)} />
                  <Label htmlFor="start-ack" className="font-normal leading-snug">{t('tasks.startAuthAcknowledge', { state: tv(r.readinessState) })}</Label>
                </div>
                <Field label={t(starting ? 'tasks.startAuthReason' : 'tasks.startAuthRecordReason')} htmlFor="start-reason" hint={t('common.reasonHint')}>
                  <Textarea id="start-reason" rows={3} maxLength={2000} value={reason} onChange={(e) => setReason(e.target.value)} />
                </Field>
              </>}
            </>}
          </div>
        )}
        {err != null && <ErrorBanner error={err} />}
        {err instanceof ApiError && Object.keys(err.fieldErrors).length > 0 && <ul className="list-disc space-y-1 pl-6 text-sm text-bad" aria-label={t('coord.refusalReasons')}>
          {Object.entries(err.fieldErrors).flatMap(([field, messages]) => messages.map((m, i) => <li key={`${field}-${i}`}>{m}</li>))}
        </ul>}
        <DialogFooter>
          <Button variant="outline" disabled={busy} onClick={() => onClose(false)}>{t('common.cancel')}</Button>
          {r && starting && (permitted || !r.needsAuthorisation) && <Button disabled={busy || (permitted && !valid)} onClick={start}>
            {busy && <Spinner />}{t(permitted ? 'tasks.startAuthStart' : 'tasks.startAuthStartNow')}</Button>}
          {r && !starting && permitted && <Button disabled={busy || !valid} onClick={record}>{busy && <Spinner />}{t('tasks.startAuthRecord')}</Button>}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
