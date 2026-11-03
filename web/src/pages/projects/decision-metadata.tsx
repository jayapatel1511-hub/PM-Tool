import { useQuery } from '@tanstack/react-query'
import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import type { components } from '@/gen/api'
import type { ProjectDetail } from '@/lib/types'
import { ApiError, typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { t } from '@/lib/i18n'
import { today } from '@/lib/format'
import { useSafeForm } from '@/hooks/drafts'
import { Empty, ErrorBanner, Field, Loading, Section, Spinner, selectCls } from '@/components/hub/common'
import { PeoplePicker } from '@/components/hub/people'
import { DraftNotice } from '@/components/hub/drafts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'

type Schemas = components['schemas']
type DecisionContextValue = Pick<Schemas['DecisionMetadataDetail'], 'categoryId' | 'optionsConsidered'>
type DecisionLinkChoice = Schemas['DecisionLinkInput'] & { key: string; name: string }
type DecisionOwnerValue = Pick<Schemas['DecisionCreateBody'], 'ownerUserId' | 'ownerExternalPartyId'> & { ownerName: string }
const uncertainRequest = (error: unknown) => !(error instanceof ApiError) || error.status === 0 || error.status === 408 || error.status >= 500 || error.code === 'idempotency_in_progress'
const correctionRefused = (error: unknown) => error instanceof ApiError && error.status === 400 && (error.fieldErrors.reason?.length ?? 0) > 0
const validId = (value: unknown): value is string => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) && value !== '00000000-0000-0000-0000-000000000000'
const versioned = (value: unknown): value is { id: string; rowVersion: number } => !!value && typeof value === 'object' && 'id' in value && validId(value.id)
  && 'rowVersion' in value && typeof value.rowVersion === 'number' && Number.isInteger(value.rowVersion) && value.rowVersion >= 0
function DecisionRefusal({ error }: { error: unknown }) {
  return <><ErrorBanner error={error} />{error instanceof ApiError && Object.values(error.fieldErrors).flat().length > 0 && <ul className="list-disc pl-5 text-sm text-bad" aria-label={t('coord.refusalReasons')}>{Object.values(error.fieldErrors).flat().map((message, index) => <li key={index}>{message}</li>)}</ul>}</>
}

export function useDecisionScreen(projectId: string) {
  const me = useMe(), enabled = useFlag('register_screens')
  return useQuery({ queryKey: ['decision-screen', projectId, me.id, enabled], enabled: enabled && !!projectId,
    queryFn: () => typedRequest('get', '/api/v1/projects/{id}/decisions/screen', { params: { id: projectId } }) })
}

export function DecisionOrganisationLag({ projectId }: { projectId: string }) {
  const enabled = useFlag('register_screens'), query = useDecisionScreen(projectId)
  if (!enabled) return null
  const data = query.isSuccess ? query.data : undefined, lag = data?.lag
  const coherent = lag && lag.basis === 'CurrentRegisteredValuesAllDecidedInProject' && lag.total === lag.attributed + lag.invalidDateCount + lag.unattributedCount
    && lag.attributed === lag.groups.reduce((sum, group) => sum + group.decidedCount, 0)
  return <Section id="decision-organisation-lag" title={t('decisionMetadata.lagTitle')}>
    <p className="border-b p-4 text-sm text-muted-foreground">{t('decisionMetadata.lagBasis')}</p>
    {query.isPending ? <Loading rows={2} /> : query.error || !lag || !coherent ? <ErrorBanner error={query.error ?? new Error(t('decisionMetadata.unavailable'))} retry={() => query.refetch()} /> : <div className="space-y-3 p-4">
      <p data-decision-lag-denominator className="text-xs text-muted-foreground">{t('decisionMetadata.cohort', { total: lag.total, attributed: lag.attributed, invalid: lag.invalidDateCount, unattributed: lag.unattributedCount })}</p>
      {!lag.groups.length ? <Empty>{t('decisionMetadata.noSamples')}</Empty> : <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {lag.groups.map(group => <li key={group.ownerKind + ':' + group.organisation} data-decision-lag-kind={group.ownerKind} data-decision-lag-organisation={group.organisation} className="min-w-0 rounded-md border p-3 text-sm">
          <p className="break-words font-medium">{group.ownerKind === 'Internal' ? t('decisionMetadata.internal') : group.organisation}</p>
          {group.ownerKind === 'External' && <p className="text-xs text-muted-foreground">{t('decisionMetadata.external')}</p>}
          <p>{t('decisionMetadata.median', { days: group.medianDays })}</p><p className="text-xs text-muted-foreground">{t('decisionMetadata.samples', { n: group.decidedCount })}</p>
        </li>)}
      </ul>}
    </div>}
  </Section>
}

function DecisionContextFields({ value, onChange, categories, prefix }: { value: DecisionContextValue; onChange: (value: DecisionContextValue) => void; categories: Schemas['DecisionCategoryChoice'][]; prefix: string }) {
  const selected = categories.find(category => category.id === value.categoryId)
  return <div className="space-y-3">
    <Field label={t('decisionMetadata.category')} htmlFor={prefix + '-category'} optional>
      <select id={prefix + '-category'} className={selectCls} value={value.categoryId ?? ''} onChange={event => onChange({ ...value, categoryId: event.target.value || null })}>
        <option value="">{t('decisionMetadata.notRecorded')}</option>
        {value.categoryId && !selected && <option value={value.categoryId}>{t('decisionMetadata.unknownCategory', { id: value.categoryId })}</option>}
        {categories.filter(category => category.isActive || category.id === value.categoryId).map(category => <option key={category.id} value={category.id}>{category.isActive ? category.name : t('decisionMetadata.inactive', { name: category.name })}</option>)}
      </select>
    </Field>
    <fieldset className="space-y-2"><legend className="text-sm font-medium">{t('decisionMetadata.options')}</legend>
      <p className="text-xs text-muted-foreground">{t('decisionMetadata.optionsHint')}</p>
      {value.optionsConsidered?.map((option, index) => <div key={index} className="flex items-end gap-2">
        <Field label={t('decisionMetadata.option', { n: index + 1 })} htmlFor={prefix + '-option-' + index} className="min-w-0 flex-1"><Input id={prefix + '-option-' + index} required maxLength={500} value={option}
          onChange={event => onChange({ ...value, optionsConsidered: value.optionsConsidered?.map((old, position) => position === index ? event.target.value : old) ?? [] })} /></Field>
        <Button type="button" variant="outline" onClick={() => onChange({ ...value, optionsConsidered: value.optionsConsidered?.filter((_, position) => position !== index) ?? [] })} aria-label={t('decisionMetadata.removeOption', { n: index + 1 })}>{t('common.remove')}</Button>
      </div>)}
      <div className="flex flex-wrap gap-2"><Button type="button" variant="outline" size="sm" disabled={(value.optionsConsidered?.length ?? 0) >= 5} onClick={() => onChange({ ...value, optionsConsidered: [...(value.optionsConsidered ?? []), ''] })}>{t('decisionMetadata.addOption')}</Button>
        <Button type="button" variant="ghost" size="sm" onClick={() => onChange({ ...value, optionsConsidered: value.optionsConsidered === null ? [] : null })}>{t(value.optionsConsidered === null ? 'decisionMetadata.emptyOptions' : 'decisionMetadata.notRecorded')}</Button></div>
    </fieldset>
  </div>
}

export function DecisionMetadataSection({ detail, onSaved }: { detail: Schemas['DecisionDetailResponse']; onSaved: () => void }) {
  const enabled = useFlag('register_screens'), screen = useDecisionScreen(detail.project.id), [editing, setEditing] = useState<Schemas['DecisionCategoryChoice'][] | null>(null)
  if (!enabled) return null
  const metadata = detail.metadata, category = detail.decision.metadata?.category
  return <section className="m-4 space-y-3 rounded-md border p-4" aria-label={t('decisionMetadata.title')}>
    <h3 className="font-medium">{t('decisionMetadata.title')}</h3>
    {!metadata ? <ErrorBanner error={new Error(t('decisionMetadata.unavailable'))} /> : <>
      <dl className="space-y-2 text-sm"><div><dt className="text-xs text-muted-foreground">{t('decisionMetadata.category')}</dt><dd>{category ? category.isActive ? category.name : t('decisionMetadata.inactive', { name: category.name }) : metadata.categoryId ? t('decisionMetadata.unknownCategory', { id: metadata.categoryId }) : t('decisionMetadata.notRecorded')}</dd></div>
        <div><dt className="text-xs text-muted-foreground">{t('decisionMetadata.options')}</dt><dd>{metadata.optionsConsidered === null ? t('decisionMetadata.notRecorded') : metadata.optionsConsidered.length ? <ol className="list-decimal space-y-1 pl-5">{metadata.optionsConsidered.map((option, index) => <li key={index} className="break-words">{option}</li>)}</ol> : t('decisionMetadata.emptyOptions')}</dd></div>
        {detail.decision.status === 'Decided' && <div><dt className="text-xs text-muted-foreground">{t('decisionMetadata.rationale')}</dt><dd className="whitespace-pre-wrap break-words">{metadata.rationale ?? t('decisionMetadata.notRecorded')}</dd></div>}
      </dl>
      {detail.permissions.edit.ok && <Button id={'decision-context-' + detail.decision.id} size="sm" variant="outline" disabled={!screen.isSuccess} onClick={() => { if (screen.isSuccess) setEditing(screen.data.categories) }}>{t('decisionMetadata.edit')}</Button>}
      {editing && <DecisionContextEditor key={detail.decision.id} detail={detail} categories={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); onSaved() }} />}
    </>}
  </section>
}

function DecisionContextEditor({ detail, categories, onClose, onSaved }: { detail: Schemas['DecisionDetailResponse']; categories: Schemas['DecisionCategoryChoice'][]; onClose: () => void; onSaved: () => void }) {
  const prefix = useId(), target = useRef({ id: detail.decision.id, version: detail.decision.rowVersion, canRationale: detail.metadata?.canEditRationale === true })
  const [value, setValue] = useState({ categoryId: detail.metadata?.categoryId ?? null, optionsConsidered: detail.metadata?.optionsConsidered ?? null, rationale: detail.metadata?.rationale ?? '', reason: '' })
  const safe = useSafeForm(null, value, setValue), sending = useRef(false)
  const [busy, setBusy] = useState(false), [stale, setStale] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<unknown>(null)
  const [reasonRequired, setReasonRequired] = useState(detail.metadata?.correctionReasonRequired === true)
  const intent = useRef<{ categoryId: string | null; optionsConsidered: string[] | null; rationale?: string | null; reason?: string } | null>(null)
  return <Dialog open onOpenChange={open => !open && !sending.current && safe.close(onClose)}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
    <DialogHeader><DialogTitle>{t('decisionMetadata.edit')}</DialogTitle><DialogDescription>{detail.decision.subject}</DialogDescription></DialogHeader>
    <form className="space-y-4" onSubmit={async event => {
      event.preventDefault(); if (sending.current || stale) return; sending.current = true; setBusy(true); setError(null)
      try {
        if (!uncertain) intent.current = { categoryId: value.categoryId, optionsConsidered: value.optionsConsidered, ...(target.current.canRationale ? { rationale: value.rationale.trim() || null } : {}), ...(value.reason.trim() ? { reason: value.reason.trim() } : {}) }
        const result: unknown = await typedRequest('patch', '/api/v1/decisions/{id}', { params: { id: target.current.id }, body: intent.current, ifMatch: target.current.version })
        if (!versioned(result) || result.id !== target.current.id) throw new Error(t('app.error'))
        safe.clear(); onSaved()
      } catch (caught) { setUncertain(old => old || uncertainRequest(caught)); if (!uncertain && correctionRefused(caught)) setReasonRequired(true); if (caught instanceof ApiError && caught.status === 409 && caught.code !== 'idempotency_in_progress') setStale(true); setError(caught) }
      finally { sending.current = false; setBusy(false) }
    }}>
      <fieldset disabled={busy || stale || uncertain} className="space-y-3"><DecisionContextFields value={value} onChange={next => setValue(old => ({ ...old, ...next }))} categories={categories} prefix={prefix} />
        {target.current.canRationale && <Field label={t('decisionMetadata.rationale')} htmlFor={prefix + '-rationale'} optional hint={t('decisionMetadata.rationaleHint')}><Textarea id={prefix + '-rationale'} maxLength={2000} value={value.rationale} onChange={event => setValue(old => ({ ...old, rationale: event.target.value }))} /></Field>}
        {reasonRequired && <Field label={t('common.reason')} htmlFor={prefix + '-reason'} hint={t('common.reasonHint')}><Textarea id={prefix + '-reason'} required minLength={5} value={value.reason} onChange={event => setValue(old => ({ ...old, reason: event.target.value }))} /></Field>}
      </fieldset>
      {error != null && <DecisionRefusal error={error} />}{stale && <p role="status">{t('decisionMetadata.stale')}</p>}{uncertain && <p role="status">{t('decisionMetadata.uncertain')}</p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button><Button disabled={busy || stale}>{busy && <Spinner />}{t(uncertain ? 'decisionMetadata.retry' : 'common.save')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}

export function DecisionRegisteredCreate({ p, onClose, onCreated, renderItems, renderOwner }: { p: ProjectDetail; onClose: () => void; onCreated: (value: Schemas['DecisionCreated']) => void;
  renderItems: (excluded: string[], pick: (value: DecisionLinkChoice) => void) => ReactNode;
  renderOwner: (value: DecisionOwnerValue, change: (value: { user: string | null; userName?: string | null; party: string | null }) => void) => ReactNode }) {
  const prefix = useId(), screen = useDecisionScreen(p.id), viewer = useMe()
  const [value, setValue] = useState({ subject: '', description: '', requestedById: null as string | null, ownerUserId: null as string | null, ownerName: '', ownerExternalPartyId: null as string | null, dateRequested: today(), requiredByDate: '', impactLevel: 'Medium', impactDescription: '' })
  const [context, setContext] = useState<DecisionContextValue>({ categoryId: null, optionsConsidered: null }), [links, setLinks] = useState<DecisionLinkChoice[]>([])
  const draft = { value, context, links }, safe = useSafeForm('decision.registered.new.' + viewer.id + '.' + p.id, draft, next => { setValue(next.value); setContext(next.context); setLinks(next.links) })
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<unknown>(null), sending = useRef(false)
  const intent = useRef<{ project: string; body: Schemas['DecisionCreateBody']; key: string; signature: string } | null>(null)
  return <Dialog open onOpenChange={open => !open && !sending.current && safe.close(onClose)}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t('decision.new')}</DialogTitle><DialogDescription>{t('decision.newHint')}</DialogDescription></DialogHeader>
    <DraftNotice form={safe} />
    <form data-decision-create className="space-y-4" onSubmit={async event => {
      event.preventDefault(); if (sending.current) return; sending.current = true; setBusy(true); setError(null)
      try {
        if (!uncertain) {
          const body: Schemas['DecisionCreateBody'] = { subject: value.subject, description: value.description, requestedById: value.requestedById, ownerUserId: value.ownerUserId, ownerExternalPartyId: value.ownerExternalPartyId,
            dateRequested: value.dateRequested, requiredByDate: value.requiredByDate, impactLevel: value.impactLevel, impactDescription: value.impactDescription,
            links: links.map(link => ({ targetType: link.targetType, targetId: link.targetId })), categoryId: context.categoryId, optionsConsidered: context.optionsConsidered }
          const signature = JSON.stringify(body); if (!intent.current || intent.current.signature !== signature) intent.current = { project: p.id, body, signature, key: crypto.randomUUID() }
        }
        const command = intent.current!
        const result: unknown = await typedRequest('post', '/api/v1/projects/{id}/decisions', { params: { id: command.project }, body: command.body, idempotencyKey: command.key })
        if (!versioned(result) || !('key' in result) || typeof result.key !== 'string' || !result.key.trim()) throw new Error(t('app.error'))
        safe.clear(); onCreated({ id: result.id, rowVersion: result.rowVersion, key: result.key })
      } catch (caught) { setUncertain(old => old || uncertainRequest(caught)); setError(caught) } finally { sending.current = false; setBusy(false) }
    }}>
      <fieldset disabled={busy || uncertain} className="space-y-3">
        <Field label={t('decision.subject')} htmlFor={prefix + '-subject'}><Input id={prefix + '-subject'} autoFocus required maxLength={300} value={value.subject} onChange={event => setValue(old => ({ ...old, subject: event.target.value }))} /></Field>
        <Field label={t('common.description')} htmlFor={prefix + '-description'}><Textarea id={prefix + '-description'} required maxLength={8000} value={value.description} onChange={event => setValue(old => ({ ...old, description: event.target.value }))} /></Field>
        {renderOwner(value, next => setValue(old => ({ ...old, ownerUserId: next.user, ownerName: next.userName ?? '', ownerExternalPartyId: next.party })))}
        <PeoplePicker value={value.requestedById} label={t('decision.requestedBy')} placeholder={t('decision.me')} onChange={id => setValue(old => ({ ...old, requestedById: id }))} />
        <div className="grid gap-3 sm:grid-cols-2"><Field label={t('decision.requested')} htmlFor={prefix + '-requested'}><Input id={prefix + '-requested'} type="date" required value={value.dateRequested} onChange={event => setValue(old => ({ ...old, dateRequested: event.target.value }))} /></Field><Field label={t('decision.requiredBy')} htmlFor={prefix + '-required'}><Input id={prefix + '-required'} type="date" required value={value.requiredByDate} onChange={event => setValue(old => ({ ...old, requiredByDate: event.target.value }))} /></Field></div>
        <Field label={t('decision.impact')} htmlFor={prefix + '-impact'}><select id={prefix + '-impact'} className={selectCls} value={value.impactLevel} onChange={event => setValue(old => ({ ...old, impactLevel: event.target.value }))}>{['High', 'Medium', 'Low'].map(impact => <option key={impact}>{impact}</option>)}</select></Field>
        <Field label={t('decision.impactDescription')} htmlFor={prefix + '-impact-description'}><Input id={prefix + '-impact-description'} required maxLength={2000} value={value.impactDescription} onChange={event => setValue(old => ({ ...old, impactDescription: event.target.value }))} /></Field>
        {screen.isSuccess ? <DecisionContextFields value={context} onChange={setContext} categories={screen.data.categories} prefix={prefix} /> : screen.error ? <ErrorBanner error={screen.error} /> : <Loading rows={2} />}
        <p className="text-sm font-medium">{t('decision.links')}</p>
        {links.map(link => <div key={link.targetType + ':' + link.targetId} className="flex items-center justify-between gap-2 text-sm"><span>{link.key} {link.name}</span><Button type="button" size="sm" variant="ghost" onClick={() => setLinks(old => old.filter(value => value !== link))}>{t('common.remove')}</Button></div>)}
        {renderItems(links.map(link => link.targetId), link => setLinks(old => [...old, link]))}
      </fieldset>
      {error != null && <DecisionRefusal error={error} />}{uncertain && <p role="status">{t('decisionMetadata.uncertain')}</p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button><Button disabled={busy || (!uncertain && !screen.isSuccess)}>{busy && <Spinner />}{t(uncertain ? 'decisionMetadata.retry' : 'decision.raise')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}

export function DecisionRegisteredOutcome({ id, onClose, onSaved }: { id: string; onClose: (ok: boolean) => void; onSaved: (project: string, id: string) => void }) {
  const me = useMe(), workOn = useFlag('work_screens'), peopleOn = useFlag('people_screens')
  const query = useQuery({ queryKey: ['decision', id, me.id, workOn, peopleOn, true], queryFn: () => typedRequest('get', '/api/v1/decisions/{id}', { params: { id } }) })
  const target = useRef<{ id: string; version: number; project: string } | null>(null)
  useEffect(() => { if (query.isSuccess && !target.current) target.current = { id: query.data.decision.id, version: query.data.decision.rowVersion, project: query.data.project.id } }, [query.isSuccess, query.data])
  const [value, setValue] = useState({ text: '', date: today(), rationale: '', notify: true, reason: '' }), safe = useSafeForm(null, value, setValue)
  const [refusedReason, setRefusedReason] = useState(false)
  const reasonRequired = refusedReason || query.isSuccess && query.data.metadata?.correctionReasonRequired === true
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [stale, setStale] = useState(false), [error, setError] = useState<unknown>(null), sending = useRef(false)
  const intent = useRef<{ target: NonNullable<typeof target.current>; body: Schemas['DecisionTransitionBody']; key: string } | null>(null)
  const allowed = query.isSuccess && query.data.permissions.transitions.some(step => step.to === 'Decided' && step.ok)
  return <Dialog open onOpenChange={open => !open && !sending.current && safe.close(() => onClose(false))}><DialogContent className="max-h-[90vh] overflow-y-auto">
    <DialogHeader><DialogTitle>{query.data ? t('decision.recordTitle', { key: query.data.decision.key }) : t('decision.record')}</DialogTitle><DialogDescription>{query.data?.decision.subject}</DialogDescription></DialogHeader>
    <form data-decision-outcome className="space-y-3" onSubmit={async event => {
      event.preventDefault(); if (sending.current || stale || (!uncertain && !allowed) || !target.current) return
      sending.current = true; setBusy(true); setError(null)
      try {
        if (!uncertain) intent.current = { target: target.current, key: crypto.randomUUID(), body: { toStatus: 'Decided', decisionText: value.text, decisionDate: value.date, rationale: value.rationale.trim() || null, notifyAssignees: value.notify, rowVersion: target.current.version, ...(value.reason.trim() ? { reason: value.reason.trim() } : {}) } }
        const command = intent.current!
        const result: unknown = await typedRequest('post', '/api/v1/decisions/{id}/transition', { params: { id: command.target.id }, body: command.body, ifMatch: command.target.version, idempotencyKey: command.key })
        if (!versioned(result) || result.id !== command.target.id || !('status' in result) || result.status !== 'Decided') throw new Error(t('app.error'))
        safe.clear(); onSaved(command.target.project, command.target.id); onClose(true)
      } catch (caught) { setUncertain(old => old || uncertainRequest(caught)); if (!uncertain && correctionRefused(caught)) setRefusedReason(true); if (caught instanceof ApiError && caught.status === 409 && caught.code !== 'idempotency_in_progress') setStale(true); setError(caught) }
      finally { sending.current = false; setBusy(false) }
    }}>
      {query.error && <ErrorBanner error={query.error} />}{query.isPending && <Loading rows={2} />}
      <fieldset disabled={busy || uncertain || stale || !allowed} className="space-y-3">
        <Field label={t('decision.text')} htmlFor="decision-outcome-text"><Textarea id="decision-outcome-text" autoFocus required maxLength={8000} value={value.text} onChange={event => setValue(old => ({ ...old, text: event.target.value }))} /></Field>
        <Field label={t('decision.date')} htmlFor="decision-outcome-date"><Input id="decision-outcome-date" type="date" required max={today()} value={value.date} onChange={event => setValue(old => ({ ...old, date: event.target.value }))} /></Field>
        <Field label={t('decisionMetadata.rationale')} htmlFor="decision-outcome-rationale" optional hint={t('decisionMetadata.rationaleHint')}><Textarea id="decision-outcome-rationale" maxLength={2000} value={value.rationale} onChange={event => setValue(old => ({ ...old, rationale: event.target.value }))} /></Field>
        {reasonRequired && <Field label={t('common.reason')} htmlFor="decision-outcome-reason" hint={t('common.reasonHint')}><Textarea id="decision-outcome-reason" required minLength={5} value={value.reason} onChange={event => setValue(old => ({ ...old, reason: event.target.value }))} /></Field>}
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={value.notify} onChange={event => setValue(old => ({ ...old, notify: event.target.checked }))} />{t('decision.notifyAssignees', { n: query.data?.links.filter(link => link.targetType === 'Task').length ?? 0 })}</label>
      </fieldset>
      {error != null && <DecisionRefusal error={error} />}{stale && <p role="status">{t('decisionMetadata.stale')}</p>}{uncertain && <p role="status">{t('decisionMetadata.uncertain')}</p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(() => onClose(false))}>{t('common.cancel')}</Button><Button disabled={busy || stale || (!uncertain && !allowed)}>{busy && <Spinner />}{t(uncertain ? 'decisionMetadata.retry' : 'decision.record')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}
