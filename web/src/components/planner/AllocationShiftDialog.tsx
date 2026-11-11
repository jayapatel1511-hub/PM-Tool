import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import type { components } from '@/gen/api'
import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Field, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { BareTable } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'

type Preview = components['schemas']['AllocationShiftPreview']
export type AllocationShiftMode = 'Selected' | 'WholeProject'

export function AllocationShiftDialog({ projectId, mode, selectedIds, onClose, onDone }: {
  projectId: string; mode: AllocationShiftMode; selectedIds: string[]; onClose: () => void; onDone: () => void
}) {
  const [weeks, setWeeks] = useState('2'), [reason, setReason] = useState('')
  const [result, setResult] = useState<{ key: string; data: Preview } | null>(null)
  const [error, setError] = useState<unknown>(null), [pending, setPending] = useState(false), [saving, setSaving] = useState(false), [uncertain, setUncertain] = useState(false)
  const sequence = useRef(0), receipt = useRef<{ signature: string; id: string } | null>(null)
  useEffect(() => () => { sequence.current++ }, [])
  const count = Number(weeks), validWeeks = weeks.trim() !== '' && Number.isInteger(count) && count !== 0 && count >= -2147483648 && count <= 2147483647
  const ids = mode === 'Selected' ? [...selectedIds].sort() : []
  const key = JSON.stringify({ projectId, mode, weeks, ids })
  const preview = result?.key === key ? result.data : null
  const busy = pending || saving
  async function inspect() {
    if (!validWeeks || busy) return
    const current = ++sequence.current
    setPending(true); setError(null); setResult(null); setUncertain(false)
    try {
      const data = await typedRequest('post', '/api/v1/projects/{projectId}/allocations/shift-preview', { params: { projectId }, body: { mode, weeks: count, allocationIds: ids } })
      if (current === sequence.current) setResult({ key, data })
    } catch (failure) { if (current === sequence.current) setError(failure) }
    finally { if (current === sequence.current) setPending(false) }
  }
  async function apply() {
    if (!preview || reason.trim().length < 5 || busy || !preview.allocations.length) return
    const body = { mode: preview.mode, weeks: preview.weeks, allocations: preview.allocations.map(row => ({ id: row.id, rowVersion: row.rowVersion })), reason: reason.trim() }
    const signature = JSON.stringify(body)
    if (receipt.current?.signature !== signature) receipt.current = { signature, id: crypto.randomUUID() }
    setSaving(true); setError(null); setUncertain(false)
    try {
      await typedRequest('post', '/api/v1/projects/{projectId}/allocations/shift', { params: { projectId }, body: { ...body, requestId: receipt.current.id } })
      toast.success(t('allocationShift.saved')); onDone()
    } catch (failure) {
      setError(failure)
      if (failure instanceof ApiError && [400, 403, 404, 409, 422, 428].includes(failure.status)) setResult(null)
      else setUncertain(true) // Keep the exact preview and receipt so an unknown-result retry cannot move twice.
    } finally { setSaving(false) }
  }
  const fieldErrors = error instanceof ApiError ? Object.values(error.fieldErrors).flat() : []
  return <Dialog open onOpenChange={open => !open && !busy && onClose()}><DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-4xl">
    <DialogHeader><DialogTitle>{t('allocationShift.title')}</DialogTitle><DialogDescription>{t(mode === 'WholeProject' ? 'allocationShift.wholeHint' : 'allocationShift.selectedHint', { n: selectedIds.length })}</DialogDescription></DialogHeader>
    <div className="space-y-4">
      {error != null && <ErrorBanner error={error} details={fieldErrors.length > 0 ? <ul className="list-disc pl-4">{fieldErrors.map((message, index) => <li key={index}>{message}</li>)}</ul> : undefined} />}
      {uncertain && <p role="status" className="text-sm text-muted-foreground">{t('allocationShift.retryHint')}</p>}
      <div className="flex flex-wrap items-end gap-3"><Field label={t('allocationShift.weeks')} htmlFor="allocation-shift-weeks" hint={t('allocationShift.weeksHint')}><Input id="allocation-shift-weeks" className="w-32" type="number" required step="1" value={weeks} disabled={busy} onChange={event => setWeeks(event.target.value)} /></Field>
        <Button type="button" variant="outline" disabled={!validWeeks || busy} onClick={() => void inspect()}>{pending ? t('allocationShift.previewing') : t('allocationShift.preview')}</Button>
      </div>
      {preview && <section className="space-y-2" aria-live="polite"><h3 id="allocation-shift-preview-title" className="text-sm font-medium">{t('allocationShift.previewTitle', { n: preview.allocations.length })}</h3><p className="text-xs text-muted-foreground">{t('allocationShift.childrenHint')}</p>
        <TableRegion role="region" aria-label={t('allocationShift.previewRegion')}><BareTable className="w-full min-w-max text-sm"><caption className="sr-only">{t('allocationShift.previewRegion')}</caption><thead><tr><th scope="col" className={thCls}>{t('workload.person')}</th><th scope="col" className={thCls}>{t('allocationShift.before')}</th><th scope="col" className={thCls}>{t('allocationShift.after')}</th><th scope="col" className={thCls}>{t('allocation.status')}</th></tr></thead>
          <tbody>{preview.allocations.map(row => <tr key={row.id} data-shift-preview-row={row.id}><th scope="row" className={`${tdCls} text-left font-medium`}><PersonPanelTrigger userId={row.personId} name={row.personName} focusKey={`allocation-shift:${projectId}:${row.id}:preview-person`} />
            {row.unchangedRoles.map(role => <span key={role.id} className="mt-2 block max-w-72 whitespace-normal text-xs font-normal text-muted-foreground">{t('allocationShift.roleUnchanged', { role: role.label, from: fmtDate(role.fromDate), through: fmtDate(role.throughDate) })}</span>)}
          </th><td className={`${tdCls} tabular-nums`}>{fmtDate(row.fromDate)} – {fmtDate(row.throughDate)}</td><td className={`${tdCls} tabular-nums`}>{fmtDate(row.newFromDate)} – {fmtDate(row.newThroughDate)}</td><td className={tdCls}>{row.requiresConfirmation ? t('allocationShift.reconfirm') : tv(row.status)}</td></tr>)}</tbody>
        </BareTable></TableRegion>
      </section>}
      <Field label={t('common.reason')} htmlFor="allocation-shift-reason" hint={t('common.reasonHint')}><Textarea id="allocation-shift-reason" required minLength={5} disabled={busy} value={reason} onChange={event => setReason(event.target.value)} /></Field>
    </div>
    <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>{t('common.cancel')}</Button><Button type="button" disabled={busy || !preview?.allocations.length || reason.trim().length < 5} onClick={() => void apply()}>{saving ? t('common.saving') : t('allocationShift.apply')}</Button></DialogFooter>
  </DialogContent></Dialog>
}
