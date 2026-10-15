import { useRef, useState } from 'react'
import { ErrorBanner, Field, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { ApiError, api } from '@/lib/api'
import { t } from '@/lib/i18n'

export type LeaveRecord = { id: string; fromDate: string; throughDate: string; hoursPerDay: number | null; status: string; sourceKind: string; sourceSystem?: string; externalId?: string; sourceUpdatedAt: string; importedAt: string; rowVersion: number; canEdit: boolean }
export function TimeAwayDialog({ person, record, onClose, onChanged }: { person: { id: string; displayName: string } | null; record?: LeaveRecord; onClose: () => void; onChanged: () => void }) {
  const [from, setFrom] = useState(record?.fromDate ?? ''); const [through, setThrough] = useState(record?.throughDate ?? '')
  const [source, setSource] = useState(record?.sourceSystem ?? ''); const [reference, setReference] = useState(record?.externalId ?? '')
  const [fullDay, setFullDay] = useState(record?.hoursPerDay == null); const [hours, setHours] = useState(String(record?.hoursPerDay ?? 4)); const [status, setStatus] = useState(record?.status ?? 'Approved')
  const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null); const [stale, setStale] = useState(false)
  const receipt = useRef({ signature: '', key: '' })
  const submit = async () => {
    if (!person || stale || busy) return
    const body = { personId: person.id, sourceSystem: source.trim(), externalId: reference.trim(), fromDate: from, throughDate: through, hoursPerDay: fullDay ? null : Number(hours), status, rowVersion: record?.rowVersion ?? 0 }
    const signature = JSON.stringify(body); if (receipt.current.signature !== signature) receipt.current = { signature, key: crypto.randomUUID() }
    setBusy(true); setError(null)
    try { await api('planning/leave/manual', { body, idempotencyKey: receipt.current.key }); onChanged(); onClose() }
    catch (e) { setError(e); if (e instanceof ApiError && e.status === 409) setStale(true) }
    finally { setBusy(false) }
  }
  return <Dialog open={!!person} onOpenChange={(open) => !open && !busy && onClose()}><DialogContent className="max-w-lg"><DialogHeader><DialogTitle>{t('availability.manualTitle', { person: person?.displayName ?? '' })}</DialogTitle><DialogDescription>{t('availability.manualHint')}</DialogDescription></DialogHeader>
    {error != null && <ErrorBanner error={error} retry={stale ? () => { onChanged(); onClose() } : submit} retryLabel={stale ? t('error.reload') : undefined} />}
    <fieldset disabled={busy || stale} className="grid gap-3 sm:grid-cols-2">
      <Field label={t('availability.source')} htmlFor="away-source"><Input id="away-source" maxLength={120} value={source} disabled={!!record} onChange={(e) => setSource(e.target.value)} /></Field>
      <Field label={t('availability.reference')} htmlFor="away-reference"><Input id="away-reference" maxLength={120} value={reference} disabled={!!record} onChange={(e) => setReference(e.target.value)} /></Field>
      <Field label={t('common.from')} htmlFor="away-from"><Input id="away-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
      <Field label={t('common.to')} htmlFor="away-through"><Input id="away-through" type="date" min={from} value={through} onChange={(e) => setThrough(e.target.value)} /></Field>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={fullDay} onChange={(e) => setFullDay(e.target.checked)} />{t('availability.fullDay')}</label>
      {!fullDay && <Field label={t('availability.absentHours')} htmlFor="away-hours"><Input id="away-hours" type="number" min="0.5" max="24" step="0.5" value={hours} onChange={(e) => setHours(e.target.value)} /></Field>}
      {record && <Field label={t('common.status')} htmlFor="away-status"><select id="away-status" className="h-(--control-h) rounded border px-3 text-sm" value={status} onChange={(e) => setStatus(e.target.value)}><option value="Approved">{t('availability.approved')}</option><option value="Cancelled">{t('availability.cancelled')}</option></select></Field>}
    </fieldset><DialogFooter><Button variant="outline" onClick={onClose} disabled={busy}>{t('common.cancel')}</Button><Button onClick={submit} disabled={busy || stale || !source.trim() || !reference.trim() || !from || !through || through < from || !fullDay && (!(Number(hours) > 0) || Number(hours) > 24)}>{busy && <Spinner />}{t('common.save')}</Button></DialogFooter>
  </DialogContent></Dialog>
}
