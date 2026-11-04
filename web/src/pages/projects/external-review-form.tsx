import { useId, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { DraftNotice } from '@/components/hub/drafts'
import { ErrorBanner, Field, Loading, Notice, Spinner, selectCls } from '@/components/hub/common'
import { FormNavigationScope } from '@/components/hub/form-navigation-context'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { components, paths } from '@/gen/api'
import { useSafeForm } from '@/hooks/drafts'
import { ApiError, typedRequest } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { addDays, fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { PersonLabel, RegisterPager, SelectField, disciplineChoices, peopleChoices, type CoordOptions } from './CoordinationForms'
import type { RegisterContext } from './CoordinationRegister'

type Schemas = components['schemas']
type IssuedSource = Schemas['ReviewIssuedSourceRead']
type Detail = Schemas['ReviewDetail']
type RecordBody = Schemas['ExternalReviewRecordBody']
type Party = Schemas['ExternalReviewPartyRead']
type ReturnCode = Schemas['ExternalReviewCodeRead']
type Comment = Schemas['ExternalReviewCommentBody'] & { key: string }
type ReceiptInput<B> = B extends unknown ? Omit<B, 'requestId'> : never
type ReceiptCommand<I> = I & { requestId: string }
type PackageBody = paths['/api/v1/projects/{projectId}/reviews']['post']['requestBody']['content']['application/json']
type RoundBody = paths['/api/v1/projects/{projectId}/reviews/{id}/rounds']['post']['requestBody']['content']['application/json']
const box = 'min-w-0 space-y-3 rounded-lg border p-4'
const check = 'mt-0.5 size-4 shrink-0 accent-(--primary)'

/** Resolve the owning review's live action after nested leave-dialog focus cleanup, including a rebuilt parent. */
function useReviewCloseFocus(onReturnFocus?: () => void) {
  const content = useRef<HTMLDivElement>(null), latest = useRef(onReturnFocus)
  latest.current = onReturnFocus
  return { ref: content, onCloseAutoFocus: (event: Event) => {
    if (!latest.current) return
    event.preventDefault()
    requestAnimationFrame(() => {
      // A quick successful reread may already have reopened this retained form. Keep its own focus then.
      if (content.current?.isConnected && content.current.dataset.state === 'open') return
      latest.current?.()
    })
  } }
}

/** A timeout or malformed success locks this exact body until the person explicitly retries it. */
function useReviewReceipt<I extends object>() {
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<unknown>(null)
  const sending = useRef(false), attempt = useRef<{ signature: string; body: ReceiptCommand<I>; uncertain: boolean } | null>(null)
  async function send(body: I, command: (body: ReceiptCommand<I>) => Promise<unknown>, expectedId?: string) {
    if (sending.current) return
    sending.current = true; setBusy(true); setError(null)
    try {
      if (!attempt.current?.uncertain) {
        const signature = JSON.stringify(body)
        if (!attempt.current || signature !== attempt.current.signature)
          attempt.current = { signature, body: { ...body, requestId: crypto.randomUUID() }, uncertain: false }
      }
      const result = await command(attempt.current!.body)
      if (!result || typeof result !== 'object' || Array.isArray(result)
        || !('id' in result) || typeof result.id !== 'string' || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(result.id)
        || (expectedId && result.id !== expectedId) || !('rowVersion' in result)
        || typeof result.rowVersion !== 'number' || !Number.isInteger(result.rowVersion) || result.rowVersion < 0)
        throw new Error(t('app.error'))
      return result.id
    } catch (caught) {
      if (attempt.current && (!(caught instanceof ApiError) || caught.status === 0 || caught.status === 408 || caught.status >= 500)) attempt.current.uncertain = true
      setUncertain(attempt.current?.uncertain ?? false); setError(caught)
    } finally { sending.current = false; setBusy(false) }
  }
  return { busy, uncertain, error, send }
}

function CommandErrors({ error, uncertain }: { error: unknown; uncertain: boolean }) {
  const refusals = error instanceof ApiError ? Object.entries(error.fieldErrors).flatMap(([field, messages]) => messages.map((message, index) => ({ key: `${field}-${index}`, message }))) : []
  return <>{error != null && <ErrorBanner error={error} />}
    {refusals.length > 0 && <ul aria-label={t('coord.refusalReasons')} className="list-disc space-y-1 rounded-md border border-bad/30 bg-bad-bg px-8 py-3 text-sm text-bad">{refusals.map(row => <li key={row.key}>{row.message}</li>)}</ul>}
    {error instanceof ApiError && error.status === 409 && <p role="status" className="rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn">{t('coord.stale')}</p>}
    {uncertain && <p role="status" className="rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn">{t('externalReview.uncertain')}</p>}
  </>
}

export function IssuedSourceLabel({ source }: { source: IssuedSource }) {
  return <span className="break-words"><span className="key">{source.deliverableKey}</span> · {source.revision ?? t('externalReview.revisionMissing')} · {t('externalReview.issuedDate')} {fmtDate(source.issuedDate)}</span>
}

function IssuedSourcePicker({ projectId, selected, onChange }: { projectId: string; selected: IssuedSource[]; onChange: (rows: IssuedSource[]) => void }) {
  const id = useId(), [search, setSearch] = useState(''), [page, setPage] = useState(1)
  const query = useQuery({ queryKey: ['review-issued-sources', projectId, search, page], queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/reviews/issued-sources', { params: { projectId }, query: { q: search, page, pageSize: 50 } }) })
  return <fieldset className={box} data-issued-source-picker="">
    <legend className="px-1 text-sm font-semibold">{t('externalReview.issuedSources')}</legend>
    <Field label={t('externalReview.sourceSearch')} htmlFor={id}><Input id={id} type="search" value={search} onKeyDown={event => { if (event.key === 'Enter') event.preventDefault() }} onChange={event => { setSearch(event.target.value); setPage(1) }} /></Field>
    <p className="text-xs text-muted-foreground">{t('externalReview.onePerDeliverable')}</p>
    <p className="text-xs text-muted-foreground">{t('externalReview.authorsUnknownOption')}</p>
    {selected.length > 0 && <div><h4 className="text-sm font-medium">{t('externalReview.selectedSources')}</h4><ul className="mt-2 space-y-2">{selected.map(source => <li key={source.deliverableIssueId} className="flex items-start justify-between gap-3 text-sm"><IssuedSourceLabel source={source} /><Button type="button" size="sm" variant="outline" aria-label={`${t('common.remove')} ${source.deliverableKey} · ${source.revision ?? t('externalReview.revisionMissing')}`} onClick={() => onChange(selected.filter(row => row.deliverableIssueId !== source.deliverableIssueId))}>{t('common.remove')}</Button></li>)}</ul></div>}
    {query.isPending ? <Loading rows={2} /> : query.isError ? <ErrorBanner error={query.error} retry={() => query.refetch()} /> : <>
      {!query.data.items.length && <Notice title={t('externalReview.noSources')} />}
      {query.data.items.map(source => { const chosen = selected.some(row => row.deliverableIssueId === source.deliverableIssueId); return <label key={source.deliverableIssueId} className="flex items-start gap-2 py-2 text-sm"><input type="checkbox" className={check} checked={chosen} disabled={!source.selectable || source.authorsState !== 'Captured' || (!chosen && (selected.length >= 200 || selected.some(row => row.deliverableId === source.deliverableId)))} onChange={event => onChange(event.target.checked ? [...selected.filter(row => row.deliverableIssueId !== source.deliverableIssueId), source] : selected.filter(row => row.deliverableIssueId !== source.deliverableIssueId))} /><span><IssuedSourceLabel source={source} />{!source.selectable && <span className="mt-1 block text-xs text-muted-foreground">{source.reason ?? t('externalReview.authorsUnknownOption')}</span>}</span></label> })}
      <div onClick={event => event.preventDefault()}><RegisterPager label={t('externalReview.issuedSources')} page={query.data.page} total={query.data.totalCount} pageSize={query.data.pageSize} onPage={setPage} /></div>
    </>}
  </fieldset>
}

/** External source mode is immutable for a package. SourceRevision IDs are never manufactured. */
export function IssuedReviewPackageForm({ options, projectId, refresh, open, close: onClose, existing, sourceModeControl, available = true, onCheck, onReturnFocus }: RegisterContext & { close: () => void; existing?: Detail; sourceModeControl?: ReactNode; available?: boolean; onCheck?: () => void; onReturnFocus?: () => void }) {
  const prefix = useId(), noticesOn = useFlag('capacity_screens')
  const closeFocus = useReviewCloseFocus(onReturnFocus)
  const packageRow = existing?.package, target = useRef({ projectId, id: packageRow?.id, rowVersion: packageRow?.rowVersion })
  const [value, setValue] = useState({ title: packageRow?.title ?? '', purpose: packageRow?.purpose ?? '', coordinatorId: packageRow?.coordinatorId ?? '', projectDisciplineId: packageRow?.projectDisciplineId ?? options.manageDisciplineIds[0] ?? '', reason: '', removalImpact: '', sources: [] as IssuedSource[], assignments: (existing?.assignments.filter(row => row.roundId === packageRow?.currentRoundId) ?? []).map(row => ({ projectDisciplineId: row.projectDisciplineId, reviewerId: row.reviewerId, dueDate: addDays(today(), 7) })) })
  const safe = useSafeForm(packageRow ? null : `review.issued.new.${projectId}`, value, setValue)
  const ids = value.sources.map(source => source.deliverableIssueId).sort().join(',')
  const guidance = useQuery({ queryKey: ['review-issued-assignment-guidance', projectId, ids], enabled: !!ids, queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/review-issued-assignment-guidance', { params: { projectId }, query: { issuedRevisionIds: ids } }) })
  const rules = guidance.isSuccess && !guidance.isError ? guidance.data : undefined
  const reviewers = peopleChoices(options, noticesOn).filter(person => rules && !rules.excludedReviewerIds.includes(person.value))
  const ready = available && !!rules?.sourcesAvailable && !guidance.isFetching && value.sources.length > 0 && value.assignments.length > 0 && value.assignments.every(row => row.reviewerId && !rules.excludedReviewerIds.includes(row.reviewerId))
  const receipt = useReviewReceipt<ReceiptInput<PackageBody | RoundBody>>()
  const close = () => { if (!receipt.busy) safe.close(onClose) }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (receipt.busy || !ready || (!receipt.uncertain && !event.currentTarget.reportValidity())) return
    const common = { purpose: value.purpose, sourceRevisionIds: [], issuedRevisionIds: value.sources.map(source => source.deliverableIssueId), assignments: value.assignments }
    const saved = target.current.id
      ? await receipt.send({ ...common, rowVersion: target.current.rowVersion!, reason: value.reason, removalImpact: value.removalImpact || null }, body => typedRequest('post', '/api/v1/projects/{projectId}/reviews/{id}/rounds', { params: { projectId: target.current.projectId, id: target.current.id! }, body: body as RoundBody }), target.current.id)
      : await receipt.send({ ...common, title: value.title, projectDisciplineId: value.projectDisciplineId, coordinatorId: value.coordinatorId, requiredForIssue: false, reason: value.reason || null }, body => typedRequest('post', '/api/v1/projects/{projectId}/reviews', { params: { projectId: target.current.projectId }, body: body as PackageBody }))
    if (saved) { safe.clear(); toast.success(t('coord.saved')); refresh(); onClose(); open(target.current.id ?? saved) }
  }
  const assign = (discipline: string, field: 'reviewerId' | 'dueDate', next: string) => setValue(old => ({ ...old, assignments: old.assignments.map(row => row.projectDisciplineId === discipline ? { ...row, [field]: next } : row) }))
  return <Dialog open onOpenChange={open => { if (!open) close() }}><DialogContent {...closeFocus} className="max-h-[90vh] overflow-y-auto sm:max-w-2xl" data-issued-review-package-form="">
    <DialogHeader><DialogTitle>{t(packageRow ? 'review.newRound' : 'reviews.new')}</DialogTitle><DialogDescription>{t('externalReview.supplementary')}</DialogDescription></DialogHeader><DraftNotice form={safe} />
    <form className="space-y-4" onSubmit={submit}>{packageRow && onCheck && <Button type="button" size="sm" variant="outline" disabled={receipt.busy} onClick={onCheck}>{t('externalReview.check')}</Button>}<fieldset className="min-w-0 space-y-4" disabled={receipt.busy || receipt.uncertain || !available}>
      {sourceModeControl}
      {!packageRow && <><Field label={t('coord.title')} htmlFor={`${prefix}-title`}><Input id={`${prefix}-title`} required maxLength={200} value={value.title} onChange={event => setValue(old => ({ ...old, title: event.target.value }))} /></Field><div className="grid gap-3 sm:grid-cols-2"><SelectField label={t('coord.discipline')} value={value.projectDisciplineId} onChange={projectDisciplineId => setValue(old => ({ ...old, projectDisciplineId }))} choices={disciplineChoices(options).filter(row => options.manageDisciplineIds.includes(row.value))} /><SelectField label={t('coord.coordinator')} value={value.coordinatorId} onChange={coordinatorId => setValue(old => ({ ...old, coordinatorId }))} choices={peopleChoices(options, noticesOn)} /></div></>}
      <Field label={t('review.purpose')} htmlFor={`${prefix}-purpose`}><Textarea id={`${prefix}-purpose`} required maxLength={2000} value={value.purpose} onChange={event => setValue(old => ({ ...old, purpose: event.target.value }))} /></Field>
      <IssuedSourcePicker projectId={projectId} selected={value.sources} onChange={sources => setValue(old => ({ ...old, sources }))} />
      <fieldset className={box}><legend className="px-1 text-sm font-semibold">{t('review.requiredDisciplines')}</legend><p className="text-xs text-muted-foreground">{t(rules?.allowSelfReview ? 'workReview.selfAllowed' : 'externalReview.independence')}</p>{guidance.error && <ErrorBanner error={guidance.error} retry={() => guidance.refetch()} />}
        {options.disciplines.map(discipline => { const row = value.assignments.find(item => item.projectDisciplineId === discipline.id); return <div key={discipline.id} className="border-b py-3 last:border-b-0"><label className="flex items-start gap-2 text-sm font-medium"><input type="checkbox" className={check} checked={!!row} onChange={event => setValue(old => ({ ...old, assignments: event.target.checked ? [...old.assignments, { projectDisciplineId: discipline.id, reviewerId: '', dueDate: addDays(today(), 7) }] : old.assignments.filter(item => item.projectDisciplineId !== discipline.id) }))} />{discipline.name}</label>{row && <div className="mt-3 grid gap-3 sm:grid-cols-2"><SelectField label={t('review.reviewer')} value={row.reviewerId} onChange={next => assign(discipline.id, 'reviewerId', next)} choices={reviewers} /><Field label={t('review.due')} htmlFor={`${prefix}-due-${discipline.id}`}><Input id={`${prefix}-due-${discipline.id}`} required type="date" value={row.dueDate} onChange={event => assign(discipline.id, 'dueDate', event.target.value)} /></Field></div>}</div> })}
      </fieldset>
      <Field label={t('coord.reason')} htmlFor={`${prefix}-reason`} optional={!packageRow}><Textarea id={`${prefix}-reason`} required={!!packageRow} minLength={packageRow ? 5 : undefined} value={value.reason} onChange={event => setValue(old => ({ ...old, reason: event.target.value }))} /></Field>
      {packageRow && <Field label={t('review.removalImpact')} htmlFor={`${prefix}-impact`} hint={t('review.removalHint')}><Textarea id={`${prefix}-impact`} value={value.removalImpact} onChange={event => setValue(old => ({ ...old, removalImpact: event.target.value }))} /></Field>}
    </fieldset><CommandErrors error={receipt.error} uncertain={receipt.uncertain} /><DialogFooter><Button type="button" variant="outline" disabled={receipt.busy} onClick={close}>{t('common.cancel')}</Button><Button type="submit" disabled={receipt.busy || !ready}>{receipt.busy && <Spinner />}{receipt.busy ? t('common.saving') : receipt.uncertain ? t('externalReview.retry') : t('coord.confirm')}</Button></DialogFooter></form>
  </DialogContent></Dialog>
}

const emptyComment = (): Comment => ({ key: crypto.randomUUID(), text: '', projectDisciplineId: '', resolverId: '', severity: 'Blocking', issueId: null })

export function ExternalReviewForm({ projectId, detail, options, available, onClose, onDone, onCheck, onReturnFocus }: { projectId: string; detail: Detail; options: CoordOptions; available: boolean; onClose: () => void; onDone: () => void; onCheck: () => void; onReturnFocus: () => void }) {
  const prefix = useId(), noticesOn = useFlag('capacity_screens')
  const closeFocus = useReviewCloseFocus(onReturnFocus)
  const target = useRef({ projectId, id: detail.package.id, rowVersion: detail.package.rowVersion, roundId: detail.package.currentRoundId })
  const [value, setValue] = useState({ source: null as IssuedSource | null, party: null as Party | null, code: null as ReturnCode | null, receivedDate: '', reason: '', comments: [emptyComment()] })
  const safe = useSafeForm(null, value, setValue), receipt = useReviewReceipt<ReceiptInput<RecordBody>>()
  const choices = useQuery({ queryKey: ['external-review-options', projectId, detail.package.id], enabled: available, queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/reviews/{id}/external-reviews/options', { params: { projectId, id: detail.package.id } }) })
  const issues = useQuery({ queryKey: ['review-issues', projectId], enabled: available, queryFn: () => typedRequest('get', '/api/v1/projects/{id}/issues', { params: { id: projectId } }) })
  const rows = choices.isSuccess && !choices.isError ? choices.data : undefined
  const current = available && detail.package.currentRoundId === target.current.roundId && !!rows?.canRecord && !choices.isFetching
  const validSelection = !!value.source && rows?.issuedRevisions.some(row => row.deliverableIssueId === value.source!.deliverableIssueId && row.selectable && row.authorsState === 'Captured') && !!value.party && rows?.parties.some(row => row.id === value.party!.id) && !!value.code && rows?.returnCodes.some(row => row.id === value.code!.id)
  const close = () => { if (!receipt.busy) safe.close(onClose) }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (receipt.busy || !current || (!receipt.uncertain && (!validSelection || !event.currentTarget.reportValidity()))) return
    const source = value.source!, party = value.party!, code = value.code!
    const saved = await receipt.send({ rowVersion: target.current.rowVersion, deliverableIssueId: source.deliverableIssueId, issueRowVersion: source.rowVersion,
      externalPartyId: party.id, partyRowVersion: party.rowVersion, receivedDate: value.receivedDate, returnCodeId: code.id, returnCodeRowVersion: code.rowVersion,
      reason: value.reason, comments: value.comments.map(({ text, projectDisciplineId, resolverId, severity, issueId }) => ({ text, projectDisciplineId, resolverId, severity, issueId: issueId || null })) },
    body => typedRequest('post', '/api/v1/projects/{projectId}/reviews/{id}/external-reviews', { params: { projectId: target.current.projectId, id: target.current.id }, body }))
    if (saved) { safe.clear(); toast.success(t('coord.saved')); onDone() }
  }
  const editComment = (key: string, field: keyof Omit<Comment, 'key'>, next: string) => setValue(old => ({ ...old, comments: old.comments.map(row => row.key === key ? { ...row, [field]: next } : row) }))
  return <FormNavigationScope available={available}><Dialog open onOpenChange={open => { if (!open) close() }}><DialogContent {...closeFocus} className="max-h-[90vh] overflow-y-auto sm:max-w-2xl" data-external-review-form={detail.package.id}>
    <DialogHeader><DialogTitle>{t('externalReview.record')}</DialogTitle><DialogDescription>{t('externalReview.recordHint')}</DialogDescription></DialogHeader>
    <p className="break-words text-sm"><span className="key">{detail.package.key}</span> · {detail.package.title} · {t('review.round')} {detail.package.roundNumber}</p>
    <form className="space-y-4" onSubmit={submit}>
      {choices.isPending ? <Loading rows={2} /> : !current && <p role="status" className="text-sm text-muted-foreground">{t('externalReview.unavailable')}</p>}
      {choices.error && <ErrorBanner error={choices.error} />}
      <div className="flex flex-wrap gap-2"><Button type="button" size="sm" variant="outline" disabled={receipt.busy || choices.isFetching} onClick={() => choices.refetch()}>{t('externalReview.recheck')}</Button><Button type="button" size="sm" variant="outline" disabled={receipt.busy} onClick={onCheck}>{t('externalReview.check')}</Button></div>
      <fieldset className="min-w-0 space-y-4" disabled={receipt.busy || receipt.uncertain || !current}>
        <Field label={t('review.source')} htmlFor={`${prefix}-source`}><select id={`${prefix}-source`} className={selectCls} required value={value.source?.deliverableIssueId ?? ''} onChange={event => setValue(old => ({ ...old, source: rows?.issuedRevisions.find(row => row.deliverableIssueId === event.target.value) ?? null }))}><option value="">{t('coord.choose')}</option>{rows?.issuedRevisions.map(row => <option key={row.deliverableIssueId} value={row.deliverableIssueId} disabled={!row.selectable || row.authorsState !== 'Captured'}>{row.deliverableKey} · {row.revision ?? t('externalReview.revisionMissing')} · {fmtDate(row.issuedDate)}{row.authorsState !== 'Captured' ? ` · ${t('externalReview.authorsUnknownOption')}` : ''}</option>)}</select></Field>
        <Field label={t('externalReview.party')} htmlFor={`${prefix}-party`}><select id={`${prefix}-party`} className={selectCls} required value={value.party?.id ?? ''} onChange={event => setValue(old => ({ ...old, party: rows?.parties.find(row => row.id === event.target.value) ?? null }))}><option value="">{t('coord.choose')}</option>{rows?.parties.map(row => <option key={row.id} value={row.id}>{row.name}{row.organisation ? ` · ${row.organisation}` : ''}</option>)}</select></Field>
        <Field label={t('externalReview.returnCode')} htmlFor={`${prefix}-code`}><select id={`${prefix}-code`} className={selectCls} required value={value.code?.id ?? ''} onChange={event => setValue(old => ({ ...old, code: rows?.returnCodes.find(row => row.id === event.target.value) ?? null }))}><option value="">{t('coord.choose')}</option>{rows?.returnCodes.map(row => <option key={row.id} value={row.id}>{row.code} · {row.name}</option>)}</select></Field>
        <Field label={t('externalReview.receivedDateLabel')} htmlFor={`${prefix}-date`}><Input id={`${prefix}-date`} required type="date" min={value.source?.issuedDate} max={today()} value={value.receivedDate} onChange={event => setValue(old => ({ ...old, receivedDate: event.target.value }))} /></Field>
        <Field label={t('externalReview.reason')} htmlFor={`${prefix}-reason`} hint={t('common.reasonHint')}><Textarea id={`${prefix}-reason`} required minLength={5} value={value.reason} onChange={event => { event.target.setCustomValidity(event.target.value.trim().length < 5 ? t('common.reasonHint') : ''); setValue(old => ({ ...old, reason: event.target.value })) }} /></Field>
        <fieldset className="min-w-0 space-y-4"><legend className="mb-3 text-sm font-semibold">{t('externalReview.commentsLabel')}</legend>{value.comments.map((row, index) => <fieldset key={row.key} className={box} data-external-comment=""><legend className="px-1 text-sm font-semibold">{t('externalReview.comment', { n: index + 1 })}</legend>
          <Field label={t('review.findingText')} htmlFor={`${prefix}-${row.key}-text`}><Textarea id={`${prefix}-${row.key}-text`} required maxLength={4000} value={row.text} onChange={event => editComment(row.key, 'text', event.target.value)} /></Field>
          <div className="grid gap-3 sm:grid-cols-2"><SelectField label={t('coord.discipline')} value={row.projectDisciplineId} onChange={next => editComment(row.key, 'projectDisciplineId', next)} choices={disciplineChoices(options)} /><SelectField label={t('review.resolver')} value={row.resolverId} onChange={next => editComment(row.key, 'resolverId', next)} choices={peopleChoices(options, noticesOn)} /></div>
          <SelectField label={t('review.severity')} value={row.severity} onChange={next => editComment(row.key, 'severity', next)} choices={['Blocking', 'Advisory'].map(severity => ({ value: severity, label: tv(severity) }))} />
          <SelectField label={t('review.linkedIssue')} required={false} value={row.issueId ?? ''} onChange={next => editComment(row.key, 'issueId', next)} choices={issues.isSuccess && !issues.isError ? issues.data.map(issue => ({ value: issue.id, label: `${issue.key} · ${issue.title}` })) : []} />
          {value.comments.length > 1 && <Button type="button" variant="outline" size="sm" onClick={() => setValue(old => ({ ...old, comments: old.comments.filter(item => item.key !== row.key) }))}>{t('externalReview.removeComment', { n: index + 1 })}</Button>}
        </fieldset>)}<Button type="button" variant="outline" disabled={value.comments.length >= 200} onClick={() => setValue(old => ({ ...old, comments: [...old.comments, emptyComment()] }))}>{t('externalReview.addComment')}</Button></fieldset>
      </fieldset>
      <CommandErrors error={receipt.error} uncertain={receipt.uncertain} />
      <DialogFooter><Button type="button" variant="outline" disabled={receipt.busy} onClick={close}>{t('common.cancel')}</Button><Button type="submit" disabled={receipt.busy || !current || (!receipt.uncertain && !validSelection)}>{receipt.busy && <Spinner />}{receipt.busy ? t('common.saving') : receipt.uncertain ? t('externalReview.retry') : t('coord.confirm')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog></FormNavigationScope>
}

/** Receipt party text is an external identity, never an internal Person link. */
export function ExternalFindingContext({ detail, finding, options }: { detail: Detail; finding: Schemas['ReviewFindingRow']; options: CoordOptions }) {
  const receipt = detail.externalReviews?.find(row => row.id === finding.externalReviewId)
  const original = detail.manifest.find(row => row.issuedSource?.deliverableIssueId === receipt?.deliverableIssueId)?.issuedSource
  const current = detail.manifest.find(row => row.roundId === finding.roundId && row.issuedSource?.deliverableIssueId === finding.deliverableIssueId)?.issuedSource
  return <div className="space-y-2 rounded-md border bg-muted/20 px-3 py-3 text-sm" data-external-finding-context={finding.id}>
    <dl className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-3 gap-y-2">
      <dt className="text-muted-foreground">{t('externalReview.originator')}</dt><dd className="break-words">{finding.originator?.kind === 'ExternalParty' ? finding.originator.name ?? t('common.unavailable') : receipt?.partyName ?? t('common.unavailable')}{receipt?.partyOrganisation ? ` · ${receipt.partyOrganisation}` : ''}{(finding.originator?.state === 'Inactive' || receipt?.partyState === 'Inactive') && <span className="ml-2 text-xs text-muted-foreground">{t('externalReview.inactiveParty')}</span>}</dd>
      <dt className="text-muted-foreground">{t('externalReview.originalSource')}</dt><dd>{original ? <IssuedSourceLabel source={original} /> : t('externalReview.sourceUnavailable')}</dd>
      <dt className="text-muted-foreground">{t('externalReview.currentSource')}</dt><dd>{current ? <IssuedSourceLabel source={current} /> : t('externalReview.sourceUnavailable')}</dd>
      {receipt && <><dt className="text-muted-foreground">{t('externalReview.receipt')}</dt><dd>{t('externalReview.received', { date: fmtDate(receipt.receivedDate), code: `${receipt.returnCode} · ${receipt.returnLabel}` })}</dd><dt className="text-muted-foreground">{t('externalReview.recordedBy')}</dt><dd><PersonLabel options={options} id={receipt.recordedBy} focusKey={`external-review:${receipt.id}:recorder`} /></dd></>}
    </dl>
  </div>
}
