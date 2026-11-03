import { useId, useRef, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Field, Spinner, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { components } from '@/gen/api'
import { useSafeForm } from '@/hooks/drafts'
import { ApiError, typedRequest } from '@/lib/api'
import { t, tv } from '@/lib/i18n'

type Schemas = components['schemas']
const outcomes = [
  ['Incorporated', 'reviewResponse.incorporated'],
  ['Not incorporated', 'reviewResponse.notIncorporated'],
  ['Clarified', 'reviewResponse.clarified'],
  ['Noted', 'reviewResponse.noted'],
] as const

export function reviewResponseOutcomeLabel(value?: string | null): string {
  return value && outcomes.some(([outcome]) => outcome === value) ? tv(value) : t('reviewResponse.notRecorded')
}

/** Responded remains subject to independent verification. The outcome is not a closure command. */
export function ReviewResponseForm({ projectId, packageId, finding, onClose, onDone }: {
  projectId: string
  packageId: string
  finding: Schemas['ReviewFindingRow']
  onClose: () => void
  onDone: (id: string) => void
}) {
  const prefix = useId()
  const [value, setValue] = useState({ outcome: '', reason: '', evidenceUrl: '' })
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<unknown>(null)
  const safe = useSafeForm(null, value, setValue)
  // A refetch while this form is open cannot promote its command to a version the person has not reviewed.
  const target = useRef({ projectId, id: packageId, findingId: finding.id, rowVersion: finding.rowVersion })
  const attempt = useRef<{ signature: string; body: Schemas['FindingAction']; uncertain: boolean } | null>(null)
  const sending = useRef(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (sending.current) return
    if (!attempt.current?.uncertain && !event.currentTarget.reportValidity()) return
    sending.current = true; setBusy(true); setError(null)
    try {
      if (!attempt.current?.uncertain) {
        const body = { rowVersion: target.current.rowVersion, action: 'Responded', reason: value.reason,
          evidenceUrl: value.evidenceUrl, ownerId: null, responseOutcome: value.outcome }
        const signature = JSON.stringify(body)
        if (!attempt.current || attempt.current.signature !== signature)
          attempt.current = { signature, body: { requestId: crypto.randomUUID(), ...body }, uncertain: false }
      }
      const result: unknown = await typedRequest('post', '/api/v1/projects/{projectId}/reviews/{id}/findings/{findingId}/action', {
        params: { projectId: target.current.projectId, id: target.current.id, findingId: target.current.findingId },
        body: attempt.current!.body,
      })
      if (!result || typeof result !== 'object' || Array.isArray(result)
        || !('id' in result) || typeof result.id !== 'string' || result.id !== target.current.findingId
        || !('rowVersion' in result) || typeof result.rowVersion !== 'number' || !Number.isInteger(result.rowVersion) || result.rowVersion < 0)
        throw new Error(t('app.error'))
      safe.clear(); toast.success(t('coord.saved')); onDone(result.id)
    } catch (caught) {
      // A refusal may happen before receipt lookup. Once uncertain, a later 403/404/409 cannot safely unlock this attempt.
      if (attempt.current && (!(caught instanceof ApiError) || caught.status === 0 || caught.status === 408 || caught.status >= 500))
        attempt.current.uncertain = true
      setUncertain(attempt.current?.uncertain ?? false); setError(caught)
    } finally { sending.current = false; setBusy(false) }
  }

  const refusals = error instanceof ApiError ? Object.entries(error.fieldErrors).flatMap(([field, messages]) => messages.map((message, i) => ({ key: `${field}-${i}`, message }))) : []
  const close = () => { if (!sending.current) safe.close(onClose) }
  return <Dialog open onOpenChange={open => { if (!open) close() }}><DialogContent className="sm:max-w-2xl" data-review-response-form={finding.id}>
    <DialogHeader><DialogTitle>{t('review.command.Responded')}</DialogTitle><DialogDescription>{t('reviewResponse.hint')}</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-4">
      <p className="whitespace-pre-wrap break-words rounded-md border bg-muted/30 px-3 py-2 text-sm">{finding.text}</p>
      <fieldset disabled={busy || uncertain} className="min-w-0 space-y-4">
        <Field label={t('reviewResponse.outcome')} htmlFor={`${prefix}-outcome`}>
          <select id={`${prefix}-outcome`} className={selectCls} required value={value.outcome} onChange={event => setValue(old => ({ ...old, outcome: event.target.value }))}>
            <option value="">{t('coord.choose')}</option>
            {outcomes.map(([outcome, key]) => <option key={outcome} value={outcome}>{t(key)}</option>)}
          </select>
        </Field>
        <Field label={t(value.outcome === 'Not incorporated' ? 'reviewResponse.notIncorporatedReason' : 'reviewResponse.reason')} htmlFor={`${prefix}-reason`} hint={t('common.reasonHint')}>
          <Textarea id={`${prefix}-reason`} required minLength={5} value={value.reason} onChange={event => {
            event.target.setCustomValidity(event.target.value.trim().length < 5 ? t('common.reasonHint') : '')
            setValue(old => ({ ...old, reason: event.target.value }))
          }} />
        </Field>
        <Field label={t('coord.evidenceUrl')} htmlFor={`${prefix}-evidence`}>
          <Input id={`${prefix}-evidence`} type="url" required maxLength={2000} value={value.evidenceUrl} onChange={event => {
            let valid = false
            try { const url = new URL(event.target.value.trim()); valid = url.protocol === 'https:' || url.protocol === 'http:' } catch { /* The canonical command validates absolute HTTP(S) evidence again. */ }
            event.target.setCustomValidity(valid ? '' : t('reviewResponse.invalidEvidence'))
            setValue(old => ({ ...old, evidenceUrl: event.target.value }))
          }} />
        </Field>
      </fieldset>
      {error != null && <ErrorBanner error={error} />}
      {refusals.length > 0 && <ul aria-label={t('coord.refusalReasons')} className="list-disc space-y-1 rounded-md border border-bad/30 bg-bad-bg px-8 py-3 text-sm text-bad">
        {refusals.map(refusal => <li key={refusal.key}>{refusal.message}</li>)}
      </ul>}
      {error instanceof ApiError && error.status === 409 && <p role="status" className="rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn">{t('coord.stale')}</p>}
      {uncertain && <p role="status" className="rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn">{t('reviewResponse.uncertain')}</p>}
      <DialogFooter>
        <Button type="button" variant="outline" disabled={busy} onClick={close}>{t('common.cancel')}</Button>
        <Button type="submit" disabled={busy}>{busy && <Spinner />}{busy ? t('common.saving') : uncertain ? t('reviewResponse.retry') : t('coord.confirm')}</Button>
      </DialogFooter>
    </form>
  </DialogContent></Dialog>
}
