import { useState } from 'react'
import { useLocation } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Missing, selectCls, Spinner } from '@/components/hub/common'
import { DraftNotice } from '@/components/hub/drafts'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key, StatusPill } from '@/components/hub/pills'
import { DateText, TITLE_LINK } from '@/components/hub/registers'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useReference } from '@/hooks/data'
import { useSafeForm } from '@/hooks/drafts'
import { ApiError, typedRequest } from '@/lib/api'
import { today, fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import type { components } from '@/gen/api'

type Row = components['schemas']['DeliverableRow']
type Issue = components['schemas']['DeliverableIssueRead']
type Register = components['schemas']['DeliverableRegisterInfo']

export function ClientNumber({ value }: { value?: Register | null }) {
  return <span className="block"><span>{value?.clientDrawingNumber || <Missing />}</span>{value?.numberMatchesPattern === false &&
    <span className="mt-1 block text-xs text-warn">{t('deliverableRegister.numberMismatch', { pattern: value.numberPattern ?? '' })}</span>}</span>
}

export function RecipientReturn({ code, label }: { code?: string | null; label?: string | null }) {
  return code ? <span title={label ?? undefined}>{code}{label && <span className="ml-1 text-muted-foreground">· {label}</span>}</span> : <Missing />
}

export function ClientNumberEditor({ deliverable, canEdit, needsReason, onSaved }: { deliverable: Row; canEdit: boolean; needsReason: boolean; onSaved: () => void }) {
  const [editing, setEditing] = useState<Row | null>(null)
  return <>
    {canEdit ? <button type="button" className="min-h-(--control-row-h) w-full rounded-md px-2 py-1.5 text-left hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring" aria-label={t('deliverableRegister.clientNumber')} onClick={() => setEditing(deliverable)}><ClientNumber value={deliverable.registerInfo} /></button> : <div className="px-2 py-1.5"><ClientNumber value={deliverable.registerInfo} /></div>}
    {editing && <ClientNumberDialog deliverable={editing} needsReason={needsReason} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); onSaved() }} />}
  </>
}

function ClientNumberDialog({ deliverable, needsReason, onClose, onSaved }: { deliverable: Row; needsReason: boolean; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState(() => ({ value: deliverable.registerInfo?.clientDrawingNumber ?? '', reason: '', rowVersion: deliverable.rowVersion }))
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<ApiError | null>(null)
  const safe = useSafeForm(null, f, setF)
  const conflict = err?.code === 'concurrency_conflict'
  const submit = async () => {
    if (busy || conflict || needsReason && f.reason.trim().length < 5) return
    setBusy(true); setErr(null)
    try {
      await typedRequest('patch', '/api/v1/deliverables/{id}', { params: { id: deliverable.id }, ifMatch: f.rowVersion,
        body: { clientDrawingNumber: f.value || null, reason: f.reason || null } })
      safe.clear(); onSaved()
    } catch (error) { setErr(error as ApiError) } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={value => !value && !busy && safe.close(onClose)}><DialogContent>
    <DialogHeader><DialogTitle>{t('deliverableRegister.clientNumber')}</DialogTitle><DialogDescription>{deliverable.key} · {deliverable.name}</DialogDescription></DialogHeader>
    <form className="space-y-3" onSubmit={event => { event.preventDefault(); void submit() }}>
      <Field label={t('deliverableRegister.clientNumber')} optional htmlFor="edit-client-number" error={err?.fieldErrors.clientDrawingNumber}><Input id="edit-client-number" maxLength={120} disabled={busy} value={f.value} onChange={event => setF({ ...f, value: event.target.value })} /></Field>
      {needsReason && <Field label={t('common.reason')} htmlFor="edit-client-number-reason" error={err?.fieldErrors.reason}><Textarea id="edit-client-number-reason" required maxLength={2000} value={f.reason} disabled={busy} onChange={event => setF({ ...f, reason: event.target.value })} /></Field>}
      {err && <ErrorBanner error={err} />}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button><Button type="submit" disabled={busy || conflict || needsReason && f.reason.trim().length < 5}>{busy && <Spinner />}{t('common.save')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}

export function openDeliverable(row: Row) { return !['Issued', 'Accepted', 'Cancelled'].includes(row.status) }

/** The tray and dated groups partition one authorised, AND-filtered response. No second widened query. */
export function DeliverableDates({ rows, rangeExcluded, removeRange, showUndated }: { rows: Row[]; rangeExcluded: boolean; removeRange: () => void; showUndated: () => void }) {
  const location = useLocation(), open = useItemPanel()
  const current = rows.filter(openDeliverable)
  const undated = current.filter(row => !row.dueDate)
  const groups = new Map<string, Row[]>()
  for (const row of current.filter(row => row.dueDate).sort((a, b) => a.dueDate!.localeCompare(b.dueDate!) || a.key.localeCompare(b.key))) {
    const date = row.dueDate!; groups.set(date, [...(groups.get(date) ?? []), row])
  }
  const list = (items: Row[]) => <ul className="divide-y">{items.map(row => <li key={row.id} data-register-date-id={row.id} className="flex min-h-(--row-min) flex-wrap items-center gap-x-3 gap-y-1 px-3 py-3 text-sm">
    <Key>{row.key}</Key><button id={`deliverable-date-${row.id}`} className={`${TITLE_LINK} min-w-0 flex-1 break-words`} onClick={event => open('Deliverable', row.id, captureRecordContextOrigin(location, event.currentTarget))}>{row.name}</button>
    <StatusPill status={row.status} />
  </li>)}</ul>
  return <section className="space-y-3" aria-label={t('deliverableRegister.openDates')}>
    <p className="text-sm text-muted-foreground">{t('deliverableRegister.dateScope')}</p>
    <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,2fr)_minmax(16rem,1fr)]">
      <div className="space-y-3" data-register-dated>
        {groups.size ? [...groups].map(([date, items]) => <section className="overflow-hidden rounded-lg border bg-card" key={date} aria-label={fmtDate(date)}>
          <h3 className="border-b bg-muted px-3 py-2 text-sm font-semibold tabular-nums"><DateText date={date} /> <span className="font-normal text-muted-foreground">({items.length})</span></h3>{list(items)}
        </section>) : <div className="rounded-lg border bg-card"><Empty>{t('deliverableRegister.noDated')}</Empty></div>}
      </div>
      <section className="min-w-0 overflow-hidden rounded-lg border bg-card" aria-label={t('deliverableRegister.noDueDate')} data-register-undated>
        <h3 className="border-b bg-muted px-3 py-2 text-sm font-semibold tabular-nums">{t('deliverableRegister.noDueCount', { n: undated.length })}</h3>
        {rangeExcluded && <div className="space-y-2 border-b px-3 py-3 text-sm"><p>{t('deliverableRegister.rangeExcludes')}</p><Button variant="outline" size="sm" onClick={removeRange}>{t('deliverableRegister.removeRange')}</Button></div>}
        {undated.length ? list(undated) : <Empty>{t('deliverableRegister.noUndated')}</Empty>}
        <div className="border-t p-3"><Button variant="outline" size="sm" className="h-auto min-h-9 whitespace-normal text-left" onClick={showUndated}>{t('deliverableRegister.showUndated')}</Button></div>
      </section>
    </div>
  </section>
}

export function IssueResponseDialog({ deliverable, issue, needsReason, onClose, onSaved }: { deliverable: Row; issue: Issue; needsReason: boolean; onClose: () => void; onSaved: () => void }) {
  const ref = useReference()
  const info = issue.registerInfo!
  // Both versions belong to the snapshot opened, even if the panel/ref data refetches while typing.
  const [f, setF] = useState(() => ({ code: info.returnCodeId ?? '', date: info.responseDate ?? '', reason: '', clear: false,
    rowVersion: deliverable.rowVersion, issueRowVersion: info.rowVersion }))
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<ApiError | null>(null)
  const safe = useSafeForm(null, f, setF)
  const codes = ref.data?.issueReturnCodes ?? []
  const active = codes.filter(code => code.isActive)
  const retained = info.returnCodeId ? codes.find(code => code.id === info.returnCodeId) : undefined
  const changed = f.clear || f.code !== (info.returnCodeId ?? '') || f.date !== (info.responseDate ?? '')
  const correction = !!(info.returnCodeId || info.responseDate) && changed
  const reasonRequired = correction || needsReason
  const conflict = err?.code === 'concurrency_conflict'
  const valid = f.clear ? !!info.returnCodeId : !!f.code && !!f.date && (active.some(code => code.id === f.code) || f.code === info.returnCodeId)
  const submit = async () => {
    if (busy || conflict || !valid || reasonRequired && f.reason.trim().length < 5) return
    setBusy(true); setErr(null)
    try {
      await typedRequest('patch', '/api/v1/deliverables/{id}/issues/{issueId}/response', { params: { id: deliverable.id, issueId: issue.id }, body: {
        returnCodeId: f.clear ? null : f.code, responseDate: f.clear ? null : f.date,
        rowVersion: f.rowVersion, issueRowVersion: f.issueRowVersion, reason: f.reason || null } })
      safe.clear(); toast.success(t('deliverableRegister.responseSaved')); onSaved()
    } catch (error) { setErr(error as ApiError) } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={value => !value && !busy && safe.close(onClose)}>
    <DialogContent className="max-w-xl">
      <DialogHeader><DialogTitle>{t('deliverableRegister.responseFor', { revision: issue.revision ?? t('common.dash'), date: fmtDate(issue.issuedDate) })}</DialogTitle><DialogDescription>{t('deliverableRegister.responseHint')}</DialogDescription></DialogHeader>
      <DraftNotice form={safe} />
      <form className="space-y-3" onSubmit={event => { event.preventDefault(); void submit() }}>
        <Field label={t('deliverableRegister.returnCode')} htmlFor="issue-response-code" error={err?.fieldErrors.returnCodeId}>
          <select id="issue-response-code" className={selectCls} value={f.code} disabled={busy || f.clear} required={!f.clear} onChange={event => setF({ ...f, code: event.target.value })}>
            <option value="">{t('deliverableRegister.chooseReturn')}</option>
            {info.returnCodeId && !retained?.isActive && <option value={info.returnCodeId}>{t('deliverableRegister.inactive', { name: `${info.returnCode ?? ''} · ${info.returnLabel ?? ''}` })}</option>}
            {active.map(code => <option key={code.id} value={code.id}>{code.id === info.returnCodeId ? info.returnCode ?? code.code : code.code} · {code.id === info.returnCodeId ? info.returnLabel ?? code.name : code.name}</option>)}
          </select>
        </Field>
        {!ref.isPending && !active.length && !info.returnCodeId && <p role="status" className="text-sm text-warn">{t('deliverableRegister.noReturns')}</p>}
        {ref.error && <ErrorBanner error={ref.error} />}
        <Field label={t('deliverableRegister.responseDate')} htmlFor="issue-response-date" error={err?.fieldErrors.responseDate}>
          <Input id="issue-response-date" type="date" min={issue.issuedDate} max={today()} required={!f.clear} disabled={busy || f.clear} value={f.date} onChange={event => setF({ ...f, date: event.target.value })} />
        </Field>
        {info.returnCodeId && <label className="flex items-center gap-2 text-sm"><Checkbox disabled={busy} checked={f.clear} onCheckedChange={value => setF({ ...f, clear: value === true })} />{t('deliverableRegister.clearResponse')}</label>}
        <Field label={t(correction ? 'deliverableRegister.correction' : 'common.reason')} optional={!reasonRequired} htmlFor="issue-response-reason" error={err?.fieldErrors.reason}>
          <Textarea id="issue-response-reason" maxLength={2000} required={reasonRequired} disabled={busy} value={f.reason} onChange={event => setF({ ...f, reason: event.target.value })} />
        </Field>
        {err && <ErrorBanner error={err} />}
        {conflict && <p role="alert" className="text-sm text-warn">{t('deliverableRegister.responseConflict')}</p>}
        <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button><Button type="submit" disabled={busy || conflict || !valid || reasonRequired && f.reason.trim().length < 5}>{busy && <Spinner />}{t('common.save')}</Button></DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
}
