import { useId, useRef, useState, type FormEvent } from 'react'
import { AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { ErrorBanner, Field, Spinner } from '@/components/hub/common'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useSafeForm } from '@/hooks/drafts'
import { useFlag } from '@/lib/auth'
import { ApiError, typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'

type Schemas = components['schemas']
export type ReferenceFields = { sourceSystem: string; stableSourceId: string; declaredRevision: string }
export const emptyReference = (): ReferenceFields => ({ sourceSystem: '', stableSourceId: '', declaredRevision: '' })
const validId = (id: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id) && id !== '00000000-0000-0000-0000-000000000000'
export function isLinkResponse(value: unknown): value is Schemas['LinkResponse'] {
  return !!value && typeof value === 'object' && 'id' in value && typeof value.id === 'string' && validId(value.id)
    && 'rowVersion' in value && typeof value.rowVersion === 'number' && Number.isInteger(value.rowVersion) && value.rowVersion >= 0
    && 'title' in value && typeof value.title === 'string' && 'url' in value && typeof value.url === 'string'
    && 'linkType' in value && typeof value.linkType === 'string'
}
export const uncertainLinkRequest = (error: unknown) => !(error instanceof ApiError) || error.status === 0 || error.status === 408 || error.status >= 500 || error.code === 'idempotency_in_progress'
export const referenceInput = (value: ReferenceFields): Schemas['DocumentLinkReferenceInput'] => ({
  // The server rejects controls before trimming; keep raw text so client trimming cannot hide an invalid input.
  sourceSystem: value.sourceSystem, stableSourceId: value.stableSourceId || null, declaredRevision: value.declaredRevision || null,
})

/** The registrar is an actual known User, separate from the link's original author. No remote verification is implied. */
export function LinkReferenceSummary({ context, focusKey }: { context?: Schemas['DocumentLinkReferenceContext'] | null; focusKey: string }) {
  const on = useFlag('people_screens')
  if (!on) return null
  const reference = context?.reference, comparison = context?.comparison
  const state = reference ? comparison?.state ?? 'NotRecorded' : 'NotRecorded'
  const declared = comparison?.declaredRevision?.trim(), current = comparison?.currentRevision?.trim()
  const comparable = !!reference?.declaredRevision?.trim() && !!context?.target && !!declared && !!current
  const mismatch = state === 'Different' && comparable
  const registrar = reference?.registeredByUser
  let message: string
  switch (state) {
    case 'NoDeliverable': message = t('linkReference.noDeliverable'); break
    case 'TargetChanged': message = t('linkReference.targetChanged'); break
    case 'TargetUnavailable': message = t('linkReference.targetUnavailable'); break
    case 'CurrentNotRecorded': message = t('linkReference.currentNotRecorded'); break
    case 'Matches': case 'Different': message = comparable ? t('linkReference.comparison', { declared, current }) : t('linkReference.notRecorded'); break
    default: message = t('linkReference.notRecorded')
  }
  return <div data-link-reference-state={state} className="min-w-0 space-y-2 text-xs text-muted-foreground">
    <p role={mismatch ? 'status' : undefined} className={mismatch ? 'flex items-start gap-2 rounded-md border border-warn/40 bg-warn-bg p-2 text-warn' : 'break-words'}>
      {mismatch && <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />}
      <span>{mismatch && <span className="font-semibold">{t('linkReference.mismatch')}: </span>}{message}</span>
    </p>
    {reference && <>
      <p>{reference.registrationKind === 'Manual' ? t('linkReference.manual') : t('linkReference.unknown')} · {t('linkReference.hint')}</p>
      <dl className="grid min-w-0 grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-3 gap-y-1">
        <dt>{t('linkReference.sourceSystem')}</dt><dd className="break-words">{reference.sourceSystem || t('linkReference.unknown')}</dd>
        <dt>{t('linkReference.stableId')}</dt><dd className="break-words">{reference.stableSourceId || t('linkReference.unknown')}</dd>
        <dt>{t('linkReference.declared')}</dt><dd className="break-words">{reference.declaredRevision || t('linkReference.unknown')}</dd>
        <dt>{t('linkReference.registeredBy')}</dt><dd>{registrar && validId(registrar.id) && registrar.id === reference.registeredBy && registrar.displayName.trim()
          ? <PersonPanelTrigger userId={registrar.id} name={registrar.displayName} active={registrar.isActive} focusKey={`${focusKey}:registrar:${registrar.id}`} /> : t('linkReference.unknown')}</dd>
        <dt>{t('linkReference.registeredAt')}</dt><dd>{reference.registeredAt && Number.isFinite(Date.parse(reference.registeredAt)) ? fmtDate(reference.registeredAt) : t('linkReference.unknown')}</dd>
      </dl>
    </>}
  </div>
}

export function LinkReferenceFields({ value, onChange, prefix, task = false }: { value: ReferenceFields; onChange: (value: ReferenceFields) => void; prefix: string; task?: boolean }) {
  return <div className="min-w-0 space-y-3">
    <p className="text-xs text-muted-foreground">{t(task ? 'linkReference.taskHint' : 'linkReference.hint')}</p>
    <Field label={t('linkReference.sourceSystem')} htmlFor={`${prefix}-source`}><Input id={`${prefix}-source`} required maxLength={200} value={value.sourceSystem} onChange={event => onChange({ ...value, sourceSystem: event.target.value })} /></Field>
    <Field label={t('linkReference.stableId')} htmlFor={`${prefix}-stable`} optional><Input id={`${prefix}-stable`} maxLength={200} value={value.stableSourceId} onChange={event => onChange({ ...value, stableSourceId: event.target.value })} /></Field>
    <Field label={t('linkReference.declared')} htmlFor={`${prefix}-revision`} optional><Input id={`${prefix}-revision`} maxLength={50} value={value.declaredRevision} onChange={event => onChange({ ...value, declaredRevision: event.target.value })} /></Field>
  </div>
}

/** Caller mounts this for an own, canChange link only; inherited rows never provide this action. */
export function LinkReferenceEditor({ link, onClose, onDone }: { link: Pick<Schemas['LinkRow'], 'id' | 'title' | 'rowVersion' | 'referenceContext'>; onClose: () => void; onDone: () => void }) {
  const on = useFlag('people_screens'), prefix = useId()
  const target = useRef({ id: link.id, rowVersion: link.rowVersion })
  const [value, setValue] = useState(() => ({ mode: 'replace', fields: {
    sourceSystem: link.referenceContext?.reference?.sourceSystem ?? '', stableSourceId: link.referenceContext?.reference?.stableSourceId ?? '', declaredRevision: link.referenceContext?.reference?.declaredRevision ?? '',
  } }))
  const safe = useSafeForm(null, value, setValue)
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [stale, setStale] = useState(false), [error, setError] = useState<unknown>(null)
  const sending = useRef(false), intent = useRef<{ body: { reference: Schemas['DocumentLinkReferenceInput'] | null }; uncertain: boolean } | null>(null)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!on || sending.current || stale) return
    if (!uncertain && !event.currentTarget.reportValidity()) return
    sending.current = true; setBusy(true); setError(null)
    try {
      if (!intent.current?.uncertain) intent.current = { body: { reference: value.mode === 'clear' ? null : referenceInput(value.fields) }, uncertain: false }
      const result: unknown = await typedRequest('patch', '/api/v1/links/{id}', { params: { id: target.current.id }, body: intent.current!.body, ifMatch: target.current.rowVersion })
      if (!isLinkResponse(result) || result.id !== target.current.id) throw new Error(t('app.error'))
      safe.clear(); toast.success(t('linkReference.saved')); onDone()
    } catch (caught) {
      if (intent.current && uncertainLinkRequest(caught)) intent.current.uncertain = true
      const pending = intent.current?.uncertain ?? false
      setUncertain(pending); if (!pending && caught instanceof ApiError && caught.status === 409) setStale(true)
      setError(caught)
    } finally { sending.current = false; setBusy(false) }
  }
  if (!on) return null
  const close = () => { if (!sending.current) safe.close(onClose) }
  return <Dialog open onOpenChange={open => !open && close()}><DialogContent className="sm:max-w-xl" data-link-reference-editor={link.id}>
    <DialogHeader><DialogTitle>{t('linkReference.editorTitle', { title: link.title })}</DialogTitle><DialogDescription>{t('linkReference.hint')}</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-4">
      <fieldset disabled={busy || uncertain || stale} className="min-w-0 space-y-3">
        <legend className="sr-only">{t('linkReference.edit')}</legend>
        <label className="flex items-start gap-2"><input type="radio" name={`${prefix}-mode`} value="replace" checked={value.mode === 'replace'} onChange={() => setValue(old => ({ ...old, mode: 'replace' }))} />{t('linkReference.replace')}</label>
        <label className="flex items-start gap-2"><input type="radio" name={`${prefix}-mode`} value="clear" checked={value.mode === 'clear'} onChange={() => setValue(old => ({ ...old, mode: 'clear' }))} />{t('linkReference.clear')}</label>
        {value.mode === 'replace' ? <LinkReferenceFields prefix={prefix} value={value.fields} onChange={fields => setValue(old => ({ ...old, fields }))} task /> : <p className="text-sm text-muted-foreground">{t('linkReference.clearHint')}</p>}
      </fieldset>
      {error != null && <ErrorBanner error={error} />}
      {error instanceof ApiError && Object.values(error.fieldErrors).flat().length > 0 && <ul className="list-disc pl-5 text-sm text-bad" aria-label={t('coord.refusalReasons')}>{Object.values(error.fieldErrors).flat().map((message, index) => <li key={index}>{message}</li>)}</ul>}
      {stale && <p role="status" className="text-sm text-warn">{t('coord.stale')}</p>}
      {uncertain && <p role="status" className="text-sm text-warn">{t('linkReference.uncertain')}</p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={close}>{t('common.cancel')}</Button><Button type="submit" disabled={busy || stale}>{busy && <Spinner />}{busy ? t('common.saving') : uncertain ? t('linkReference.retry') : t('common.save')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}
