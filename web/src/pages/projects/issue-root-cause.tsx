import { useId, useRef, useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ErrorBanner, Field, Loading, Spinner, selectCls } from '@/components/hub/common'
import { useMe } from '@/lib/auth'
import { ApiError, typedRequest } from '@/lib/api'
import { today } from '@/lib/format'
import { t } from '@/lib/i18n'
import { useSafeForm } from '@/hooks/drafts'
import { useProjectRefresh } from '@/hooks/data'

type Schemas = components['schemas']
export function useIssueRootCauseChoices(enabled = true) {
  const me = useMe()
  return useQuery({ queryKey: ['reference', 'issue-root-causes', me.id, enabled], enabled, queryFn: async () =>
    (await typedRequest('get', '/api/v1/reference', {})).issueRootCauses ?? [] })
}

/** Preserve view preferences only. A project-wide count must not inherit hidden filters or a saved-view cohort. */
export function issueRootCauseDrilldown(current: URLSearchParams, cause: string) {
  const next = new URLSearchParams()
  for (const key of ['group', 'sort', 'cols']) for (const value of current.getAll(key)) next.append(key, value)
  next.set('status', 'Resolved'); next.set('rootCause', cause)
  return next
}

export function IssueRootCauseCounts({ projectId, onOpen }: { projectId: string; onOpen: (cause: string) => void }) {
  const me = useMe()
  const query = useQuery({ queryKey: ['issue-root-causes', projectId, me.id, true], queryFn: () =>
    typedRequest('get', '/api/v1/projects/{id}/issues/root-causes', { params: { id: projectId } }) })
  if (query.isError) return <ErrorBanner error={query.error} />
  if (!query.data) return <Loading />
  return <section className="space-y-2 rounded-md border p-3" aria-label={t('issueRootCause.title')}>
    <h2 className="text-sm font-semibold">{t('issueRootCause.title')}</h2><p className="text-xs text-muted-foreground">{t('issueRootCause.scope')}</p>
    {query.data.resolvedTotal === 0 ? <p className="text-sm text-muted-foreground">{t('issueRootCause.empty')}</p> : <div className="flex flex-wrap gap-2">
      {query.data.causes.map(cause => <Button key={cause.categoryId} variant="outline" size="sm" onClick={() => onOpen(cause.categoryId)}>
        {cause.name}{!cause.isActive && ` · ${t('issueRootCause.retired')}`} <span className="ml-2 tabular-nums">{cause.count}</span>
      </Button>)}
      {query.data.notRecordedCount > 0 && <Button variant="outline" size="sm" onClick={() => onOpen('not-recorded')}>{t('issueRootCause.notRecorded')} <span className="ml-2 tabular-nums">{query.data.notRecordedCount}</span></Button>}
    </div>}
  </section>
}

export function IssueRootCauseResolveDialog({ issue, onClose }: { issue: Schemas['IssueRow']; onClose: () => void }) {
  const prefix = useId(), done = useProjectRefresh(), choices = useIssueRootCauseChoices()
  const [value, setValue] = useState({ resolution: '', resolvedDate: today(), cause: '', reason: '' })
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<unknown>(null)
  const target = useRef({ id: issue.id, projectId: issue.projectId, rowVersion: issue.rowVersion })
  const attempt = useRef<{ signature: string; key: string; body: Schemas['IssueMove']; uncertain: boolean } | null>(null)
  const sending = useRef(false), safe = useSafeForm(null, value, setValue)
  const active = choices.isError ? [] : choices.data?.filter(choice => choice.isActive) ?? []
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (sending.current) return
    if (!attempt.current?.uncertain && !event.currentTarget.reportValidity()) return
    sending.current = true; setBusy(true); setError(null)
    try {
      if (!attempt.current?.uncertain) {
        const body: Schemas['IssueMove'] = { toStatus: 'Resolved', resolution: value.resolution, resolvedDate: value.resolvedDate,
          reason: value.reason || null, rowVersion: null, rootCauseCategoryId: value.cause }
        const signature = JSON.stringify(body)
        if (!attempt.current || attempt.current.signature !== signature) attempt.current = { signature, key: crypto.randomUUID(), body, uncertain: false }
      }
      const result: unknown = await typedRequest('post', '/api/v1/issues/{id}/transition', { params: { id: target.current.id },
        ifMatch: target.current.rowVersion, body: attempt.current!.body, idempotencyKey: attempt.current!.key })
      if (!result || typeof result !== 'object' || Array.isArray(result) || Object.keys(result).length !== 3
        || !('id' in result) || result.id !== target.current.id || !('status' in result) || result.status !== 'Resolved'
        || !('rowVersion' in result) || typeof result.rowVersion !== 'number' || !Number.isInteger(result.rowVersion) || result.rowVersion <= target.current.rowVersion)
        throw new Error(t('app.error'))
      safe.clear(); done(target.current.projectId); toast.success(t('issue.resolvedToast', { key: issue.key })); onClose()
    } catch (caught) {
      if (attempt.current && (!(caught instanceof ApiError) || caught.status === 0 || caught.status === 408 || caught.status >= 500
        || caught.code === 'idempotency_in_progress')) attempt.current.uncertain = true
      setUncertain(attempt.current?.uncertain ?? false); setError(caught)
    } finally { sending.current = false; setBusy(false) }
  }
  const close = () => { if (!sending.current) safe.close(onClose) }
  return <Dialog open onOpenChange={open => { if (!open) close() }}><DialogContent data-issue-root-cause-form={issue.id}>
    <DialogHeader><DialogTitle>{t('issue.resolveTitle', { key: issue.key })}</DialogTitle><DialogDescription>{issue.title}</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-3">
      <fieldset disabled={busy || uncertain} className="min-w-0 space-y-3">
        <Field label={t('issueRootCause.label')} htmlFor={`${prefix}-cause`}>
          <select id={`${prefix}-cause`} className={selectCls} required value={value.cause} onChange={event => setValue(old => ({ ...old, cause: event.target.value }))}>
            <option value="">{t('issueRootCause.choose')}</option>{active.map(choice => <option key={choice.id} value={choice.id}>{choice.name}</option>)}
          </select>
        </Field>
        {!choices.isError && choices.data && active.length === 0 && <p role="status" className="text-sm text-muted-foreground">{t('issueRootCause.noCategories')}</p>}
        <Field label={t('issue.resolution')} htmlFor={`${prefix}-resolution`}><Textarea id={`${prefix}-resolution`} required maxLength={8000} value={value.resolution} onChange={event => setValue(old => ({ ...old, resolution: event.target.value }))} /></Field>
        <Field label={t('issue.resolvedDate')} htmlFor={`${prefix}-date`}><Input id={`${prefix}-date`} type="date" required max={today()} value={value.resolvedDate} onChange={event => setValue(old => ({ ...old, resolvedDate: event.target.value }))} /></Field>
        <Field label={t('issueRootCause.reason')} htmlFor={`${prefix}-reason`} hint={t('issueRootCause.reasonHint')}><Textarea id={`${prefix}-reason`} value={value.reason} onChange={event => setValue(old => ({ ...old, reason: event.target.value }))} /></Field>
      </fieldset>
      {choices.isError && <ErrorBanner error={choices.error} />}
      {error != null && <ErrorBanner error={error} />}
      {error instanceof ApiError && Object.entries(error.fieldErrors).flatMap(([field, messages]) => messages.map((message, index) => <p key={`${field}-${index}`} role="alert" className="text-sm text-bad">{message}</p>))}
      {uncertain && <p role="status" className="text-sm text-warn">{t('issueRootCause.uncertain')}</p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={close}>{t('common.cancel')}</Button>
        <Button type="submit" disabled={busy || (!uncertain && (choices.isError || !value.cause || !value.resolution.trim() || active.length === 0))}>
          {busy && <Spinner />}{busy ? t('common.saving') : uncertain ? t('issueRootCause.retry') : t('issue.resolve')}
        </Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}
