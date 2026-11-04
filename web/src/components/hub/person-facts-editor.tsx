import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { ConfirmDialog, ErrorBanner, Field, Loading, selectCls } from '@/components/hub/common'
import { CertificationChip, PersonDateNotice, type RecordedCertification } from '@/components/hub/person-facts'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { invalidateChange } from '@/lib/keys'
import { t } from '@/lib/i18n'

const readOwn = () => typedRequest('get', '/api/v1/me/certifications', {})
type Editor = Awaited<ReturnType<typeof readOwn>>
type Tag = components['schemas']['ReferenceRow']
type CreateDraft = { certificationTagId: string; expiresOn: string | null; rowVersion: number; reason: string | null }
function isSavedAcknowledgement(value: unknown): value is { id: string; rowVersion: number } {
  if (value == null || typeof value !== 'object') return false
  const response = value as Record<string, unknown>
  return typeof response.id === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(response.id)
    && response.id !== '00000000-0000-0000-0000-000000000000' && typeof response.rowVersion === 'number'
    && Number.isInteger(response.rowVersion) && response.rowVersion >= 0 && response.rowVersion <= 2147483647
}

/** Self certifications or the narrow Admin correction section. No full person profile or qualification assertion. */
export function PersonFactsEditor({ adminUserId, onUserVersion }: { adminUserId?: string; onUserVersion?: (nextVersion: number, priorVersion: number) => void }) {
  const enabled = useFlag('capacity_screens'), me = useMe(), qc = useQueryClient(), targetId = adminUserId ?? me.id
  const q = useQuery({ queryKey: ['person-facts', targetId, adminUserId ? 'admin' : 'self'], enabled,
    queryFn: () => adminUserId ? typedRequest('get', '/api/v1/admin/users/{userId}/certifications', { params: { userId: adminUserId } }) : readOwn() })
  const [tagId, setTagId] = useState(''), [expires, setExpires] = useState(''), [reason, setReason] = useState('')
  const [editing, setEditing] = useState<RecordedCertification | null>(null), [editDate, setEditDate] = useState(''), [removing, setRemoving] = useState<RecordedCertification | null>(null)
  const [error, setError] = useState<unknown>(null), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [knownSaved, setKnownSaved] = useState(false)
  const create = useRef<{ signature: string; key: string; body: CreateDraft } | null>(null), savedId = useRef<string | null>(null), inFlight = useRef(false)
  useEffect(() => {
    if (savedId.current && q.data?.facts.certifications.some(record => record.id === savedId.current)) {
      savedId.current = null; setKnownSaved(false); setUncertain(false); setTagId(''); setExpires(''); create.current = null
    }
  }, [knownSaved, q.data])
  if (!enabled) return null
  const reload = async () => {
    if (inFlight.current) return
    setEditing(null); setError(null); setBusy(true)
    try { await q.refetch({ throwOnError: true }) } catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  if (q.isPending) return <Loading rows={3} />
  if (q.error instanceof ApiError && [403, 404].includes(q.error.status)) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  if (q.error && !q.data) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  const data = q.data!
  const locked = busy || uncertain || knownSaved || !data.canEdit, reasonOK = !adminUserId || reason.trim().length >= 5
  const refresh = async (type: string) => { await invalidateChange(qc, type); await q.refetch({ throwOnError: true }) }
  async function add() {
    if (inFlight.current || knownSaved || !q.data?.canEdit || !tagId || !reasonOK) return
    const fresh = { certificationTagId: tagId, expiresOn: expires || null, rowVersion: q.data.rowVersion, reason: adminUserId ? reason.trim() : null }
    const signature = JSON.stringify(fresh)
    if (!uncertain && create.current?.signature !== signature) create.current = { signature, key: crypto.randomUUID(), body: fresh }
    const command = create.current
    if (!command) return
    inFlight.current = true; setBusy(true); setError(null)
    let acknowledged = false
    try {
      const response = adminUserId
        ? await typedRequest('post', '/api/v1/admin/users/{userId}/certifications', { params: { userId: adminUserId }, body: command.body, ifMatch: command.body.rowVersion, idempotencyKey: command.key })
        : await typedRequest('post', '/api/v1/me/certifications', { body: command.body, ifMatch: command.body.rowVersion, idempotencyKey: command.key })
      if (!isSavedAcknowledgement(response)) throw new Error(t('personFacts.responseUnknown'))
      acknowledged = true; savedId.current = response.id; setKnownSaved(true); setUncertain(false); toast.success(t('common.saved')); await refresh('UserCertification')
    } catch (failure) { setError(failure); if (!acknowledged) setUncertain(!(failure instanceof ApiError) || failure.status === 0 || failure.status >= 500 || failure.code === 'idempotency_in_progress') }
    finally { inFlight.current = false; setBusy(false) }
  }
  async function saveExpiry() {
    if (!editing || inFlight.current || locked || !reasonOK) return
    inFlight.current = true; setBusy(true); setError(null)
    try {
      const body = { expiresOn: editDate || null, reason: adminUserId ? reason.trim() : null }
      if (adminUserId) await typedRequest('patch', '/api/v1/admin/users/{userId}/certifications/{id}', { params: { userId: adminUserId, id: editing.id }, body, ifMatch: editing.rowVersion })
      else await typedRequest('patch', '/api/v1/me/certifications/{id}', { params: { id: editing.id }, body, ifMatch: editing.rowVersion })
      setEditing(null); toast.success(t('common.saved')); await refresh('UserCertification')
    } catch (failure) { setError(failure) } finally { inFlight.current = false; setBusy(false) }
  }
  const fieldErrors = error instanceof ApiError ? Object.values(error.fieldErrors).flat() : []
  return <div role="region" aria-label={t('personFacts.certifications')} className="space-y-3" data-certifications-editor>
    <p className="text-sm text-muted-foreground">{t('personFacts.fixedHint')}</p>
    <PersonDateNotice notices={data.facts.notices} />
    {error != null && <ErrorBanner error={error} retry={() => void reload()} retryLabel={t('error.reload')} details={fieldErrors.length ? <ul>{fieldErrors.map((message, index) => <li key={index}>{message}</li>)}</ul> : undefined} />}
    {knownSaved && <p role="status" className="text-sm">{t('personFacts.savedReload')} <Button size="sm" variant="outline" disabled={busy} onClick={() => void reload()}>{t('error.reload')}</Button></p>}
    {uncertain && <p role="status" className="text-sm">{t('personFacts.retryHint')} <Button size="sm" variant="outline" disabled={busy} onClick={() => void add()}>{t('app.retry')}</Button></p>}
    {adminUserId && <PersonDatesForm key={`${targetId}:${data.rowVersion}`} userId={targetId} data={data} disabled={locked} onReload={reload} onSaved={async (nextVersion, priorVersion) => { onUserVersion?.(nextVersion, priorVersion); await refresh('PersonFacts') }} />}
    {adminUserId && data.canEdit && <Field label={t('personFacts.certReason')} htmlFor={`certification-reason-${targetId}`} hint={t('common.reasonHint')}><Textarea id={`certification-reason-${targetId}`} value={reason} minLength={5} disabled={locked} onChange={event => setReason(event.target.value)} /></Field>}
    {!data.facts.certifications.length && <p className="text-sm text-muted-foreground">{t('personFacts.noCertifications')}</p>}
    <ul className="space-y-2">{data.facts.certifications.map(record => <li key={record.id} className="flex flex-wrap items-center gap-2 rounded-md border p-2"><div className="min-w-0 flex-1"><CertificationChip record={record} /></div>{data.canEdit && <><Button size="sm" variant="outline" disabled={locked} aria-label={t('personFacts.editNamed', { name: record.tagName })} onClick={() => { setEditing(record); setEditDate(record.expiresOn ?? ''); setError(null) }}>{t('common.edit')}</Button><Button size="sm" variant="ghost" disabled={locked} aria-label={t('personFacts.removeNamed', { name: record.tagName })} onClick={() => setRemoving(record)}>{t('common.remove')}</Button></>}</li>)}</ul>
    {editing && <div className="space-y-2 rounded-md border p-3"><Field label={t('personFacts.expiryFor', { name: editing.tagName })} htmlFor={`certification-edit-${editing.id}`} optional><Input id={`certification-edit-${editing.id}`} type="date" value={editDate} disabled={locked} onChange={event => setEditDate(event.target.value)} /></Field><div className="flex flex-wrap gap-2"><Button size="sm" disabled={locked || !reasonOK} onClick={() => void saveExpiry()}>{t('common.save')}</Button><Button size="sm" variant="outline" disabled={locked} onClick={() => setEditing(null)}>{t('common.cancel')}</Button></div></div>}
    {data.canEdit && <fieldset disabled={locked} className="space-y-2 rounded-md border p-3"><legend className="px-1 text-sm font-medium">{t('personFacts.add')}</legend><div className="flex flex-wrap items-end gap-3"><Field label={t('personFacts.tag')} htmlFor={`certification-tag-${targetId}`} className="min-w-48 flex-1"><select id={`certification-tag-${targetId}`} className={selectCls} value={tagId} onChange={event => setTagId(event.target.value)}><option value="">{t('common.selectPlaceholder')}</option>{data.choices.filter(tag => tag.isActive && !data.facts.certifications.some(record => record.tagId === tag.id)).map(tag => <option key={tag.id} value={tag.id}>{tag.name}</option>)}</select></Field><Field label={t('personFacts.expiry')} htmlFor={`certification-expiry-${targetId}`} optional><Input id={`certification-expiry-${targetId}`} type="date" value={expires} onChange={event => setExpires(event.target.value)} /></Field><Button disabled={!tagId || !reasonOK} onClick={() => void add()}>{t('common.add')}</Button></div></fieldset>}
    {removing && <ConfirmDialog open onOpenChange={open => !open && setRemoving(null)} destructive title={t('personFacts.removeNamed', { name: removing.tagName })} body={t('personFacts.removeHint')} confirmLabel={t('common.remove')} reason={adminUserId ? true : undefined}
      onConfirm={async correctionReason => { const body = { reason: adminUserId ? correctionReason || null : null, rowVersion: removing.rowVersion }; if (adminUserId) await typedRequest('delete', '/api/v1/admin/users/{userId}/certifications/{id}', { params: { userId: adminUserId, id: removing.id }, body, ifMatch: removing.rowVersion }); else await typedRequest('delete', '/api/v1/me/certifications/{id}', { params: { id: removing.id }, body, ifMatch: removing.rowVersion }); setRemoving(null); await refresh('UserCertification') }} />}
  </div>
}

function PersonDatesForm({ userId, data, disabled, onSaved, onReload }: { userId: string; data: Editor; disabled: boolean; onSaved: (nextVersion: number, priorVersion: number) => Promise<void>; onReload: () => Promise<unknown> }) {
  const [type, setType] = useState(data.facts.notices.personType ?? ''), [start, setStart] = useState(data.facts.notices.startDate ?? ''), [end, setEnd] = useState(data.facts.notices.endDate ?? ''), [reason, setReason] = useState('')
  const [error, setError] = useState<unknown>(null), [busy, setBusy] = useState(false), [saved, setSaved] = useState(false)
  const dirty = type !== (data.facts.notices.personType ?? '') || start !== (data.facts.notices.startDate ?? '') || end !== (data.facts.notices.endDate ?? '')
  const fieldErrors = error instanceof ApiError ? Object.values(error.fieldErrors).flat() : []
  async function save() {
    if (busy || saved || disabled || !dirty || reason.trim().length < 5) return
    const priorVersion = data.rowVersion
    setBusy(true); setError(null)
    try { const result = await typedRequest('patch', '/api/v1/admin/users/{userId}/person-facts', { params: { userId }, body: { personType: type || null, personStartDate: start || null, personEndDate: end || null, reason: reason.trim() }, ifMatch: priorVersion }); setSaved(true); await onSaved(result.rowVersion, priorVersion); toast.success(t('common.saved')) }
    catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  return <div className="space-y-2"><fieldset disabled={disabled || busy || saved} className="space-y-2 rounded-md border p-3"><legend className="px-1 text-sm font-medium">{t('personFacts.typeDates')}</legend>
    {error != null && <ErrorBanner error={error} details={fieldErrors.length ? <ul>{fieldErrors.map((message, index) => <li key={index}>{message}</li>)}</ul> : undefined} />}
    <Field label={t('personFacts.typeLabel')} htmlFor={`person-type-${userId}`}><select id={`person-type-${userId}`} className={selectCls} value={type} onChange={event => setType(event.target.value)}><option value="">{t('personFacts.unrecorded')}</option>{['Employee', 'Co-op', 'Contractor'].map(value => <option key={value} value={value}>{value}</option>)}</select></Field>
    <div className="flex flex-wrap gap-3"><Field label={t('personFacts.start')} htmlFor={`person-start-${userId}`} optional><Input id={`person-start-${userId}`} type="date" value={start} onChange={event => setStart(event.target.value)} /></Field><Field label={t('personFacts.end')} htmlFor={`person-end-${userId}`} optional><Input id={`person-end-${userId}`} type="date" value={end} onChange={event => setEnd(event.target.value)} /></Field></div>
    <Field label={t('common.reason')} htmlFor={`person-facts-reason-${userId}`} hint={t('common.reasonHint')}><Textarea id={`person-facts-reason-${userId}`} value={reason} minLength={5} onChange={event => setReason(event.target.value)} /></Field>
    <Button size="sm" disabled={!dirty || reason.trim().length < 5} onClick={() => void save()}>{t('personFacts.saveFacts')}</Button>{saved && <p role="status" className="text-sm">{t('personFacts.savedReload')}</p>}
  </fieldset>{(saved || error != null) && <Button size="sm" variant="outline" disabled={busy} onClick={() => void onReload()}>{t('error.reload')}</Button>}</div>
}

export function CertificationVocabulary() {
  const enabled = useFlag('capacity_screens'), qc = useQueryClient(), [open, setOpen] = useState(false)
  const q = useQuery({ queryKey: ['admin', 'certificationTags'], enabled: enabled && open, queryFn: () => typedRequest('get', '/api/v1/admin/certificationTags', {}) })
  const [name, setName] = useState(''), [error, setError] = useState<unknown>(null), [busy, setBusy] = useState(false)
  const receipt = useRef<{ name: string; key: string } | null>(null)
  if (!enabled) return null
  const refresh = () => invalidateChange(qc, 'CertificationTag')
  async function add() {
    const value = name.trim(); if (!value || busy) return
    if (receipt.current?.name !== value) receipt.current = { name: value, key: crypto.randomUUID() }
    setBusy(true); setError(null)
    try { await typedRequest('post', '/api/v1/admin/certificationTags', { body: { name: value }, idempotencyKey: receipt.current.key }); setName(''); receipt.current = null; await refresh() }
    catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  return <><Button variant="outline" onClick={() => setOpen(true)}>{t('personFacts.vocabulary')}</Button>{open && <Dialog open onOpenChange={setOpen}><DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{t('personFacts.vocabulary')}</DialogTitle></DialogHeader><p className="text-sm text-muted-foreground">{t('personFacts.vocabularyHint')}</p>{error != null && <ErrorBanner error={error} />}{q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}{q.isPending ? <Loading rows={3} /> : <ul className="space-y-3">{q.data?.map(tag => <VocabularyRow key={`${tag.id}:${tag.rowVersion}`} tag={tag} onSaved={refresh} />)}</ul>}<div className="flex items-end gap-2"><Field label={t('personFacts.tagName')} htmlFor="new-certification-tag" className="min-w-0 flex-1"><Input id="new-certification-tag" maxLength={120} value={name} disabled={busy} onChange={event => setName(event.target.value)} /></Field><Button disabled={busy || !name.trim()} onClick={() => void add()}>{t('common.add')}</Button></div></DialogContent></Dialog>}</>
}
function VocabularyRow({ tag, onSaved }: { tag: Tag; onSaved: () => Promise<unknown> }) {
  const [name, setName] = useState(tag.name), [active, setActive] = useState(tag.isActive), [order, setOrder] = useState(String(tag.sortOrder)), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(null)
  async function save() { if (busy) return; setBusy(true); setError(null); try { await typedRequest('patch', '/api/v1/admin/certificationTags/{id}', { params: { id: tag.id }, body: { name: name.trim(), isActive: active, sortOrder: Number(order) }, ifMatch: tag.rowVersion }); await onSaved() } catch (failure) { setError(failure) } finally { setBusy(false) } }
  return <li className="space-y-2 rounded-md border p-3">{error != null && <ErrorBanner error={error} />}<div className="flex flex-wrap items-end gap-2"><Field label={t('personFacts.tagName')} htmlFor={`tag-name-${tag.id}`} className="min-w-48 flex-1"><Input id={`tag-name-${tag.id}`} maxLength={120} value={name} disabled={busy} onChange={event => setName(event.target.value)} /></Field><Field label={t('personFacts.order')} htmlFor={`tag-order-${tag.id}`} className="w-24"><Input id={`tag-order-${tag.id}`} type="number" step={1} value={order} disabled={busy} onChange={event => setOrder(event.target.value)} /></Field><label className="flex min-h-9 items-center gap-2 text-sm"><Checkbox checked={active} disabled={busy} onCheckedChange={checked => setActive(checked === true)} />{t('common.active')}</label><Button size="sm" disabled={busy || !name.trim() || !Number.isInteger(Number(order))} onClick={() => void save()}>{t('common.save')}</Button></div></li>
}
