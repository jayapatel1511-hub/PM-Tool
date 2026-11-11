import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, Cloud, Copy, ExternalLink, Files, FolderOpen, Globe, Link2, MessagesSquare, Plus, Trash2 } from 'lucide-react'
import { useId, useRef, useState } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, ErrorBanner, Field, Loading, Spinner, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { emptyReference, isLinkResponse, LinkReferenceEditor, LinkReferenceFields, LinkReferenceSummary, referenceInput, uncertainLinkRequest } from '@/components/hub/document-link-reference'
import { useSafeForm } from '@/hooks/drafts'
import { useFlag, useMe } from '@/lib/auth'
import { ApiError, typedRequest } from '@/lib/api'
import { t , runtimeText } from '@/lib/i18n'
import type { components } from '@/gen/api'
import { invalidateChange } from '@/lib/keys'
import type { ItemPartProps } from '@/pages/projects/slots-items'

// Document links (§12.7): where the files live. The Hub stores pointers only, never files (FR-DOC-03).

type LinkRow = components['schemas']['LinkRow']
type LinkList = components['schemas']['LinkListResponse']

const LINK_TYPES = ['SharePoint', 'OneDrive', 'Teams', 'Network Folder', 'External DMS', 'Client Portal', 'Other'] as const
export const LINK_ICON: Record<string, typeof Link2> = {
  SharePoint: Files, OneDrive: Cloud, Teams: MessagesSquare, 'Network Folder': FolderOpen, 'External DMS': Archive, 'Client Portal': Globe, Other: Link2,
}
/** A UNC path cannot be opened by a browser, so it offers "Copy path" instead of a link (AC-DOC-02). */
export function LinkAnchor({ l }: { l: Pick<LinkRow, 'title' | 'url' | 'linkType' | 'isNetworkPath'> }) {
  const Icon = LINK_ICON[l.linkType] ?? Link2
  if (l.isNetworkPath || l.url.startsWith('\\\\')) return (
    <span className="inline-flex min-w-0 items-center gap-2">
      <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
      <span className="truncate" title={l.url}>{l.title}</span>
      <Button size="xs" variant="outline" onClick={() => { navigator.clipboard?.writeText(l.url); toast.success(t('links.pathCopied')) }}>
        <Copy />{t('links.copyPath')}
      </Button>
    </span>
  )
  return (
    <a href={l.url} target="_blank" rel="noreferrer noopener" className="inline-flex min-w-0 items-center gap-2 text-primary underline underline-offset-4 hover:text-foreground" title={l.url}>
      <Icon className="size-4 shrink-0" aria-hidden /><span className="truncate">{l.title}</span><ExternalLink className="size-3.5 shrink-0" aria-hidden />
    </a>
  )
}
export function LinksPanel({ type, id }: ItemPartProps) {
  const qc = useQueryClient()
  const referencesOn = useFlag('people_screens'), me = useMe()
  const q = useQuery<LinkList>({ queryKey: referencesOn ? ['links', type, id, me.id, true] : ['links', type, id], queryFn: () => typedRequest('get', '/api/v1/items/{type}/{id}/links', { params: { type, id } }) })
  const [adding, setAdding] = useState(false)
  const [removing, setRemoving] = useState<LinkRow | null>(null)
  const [editing, setEditing] = useState<{ viewer: string; type: string; ownerId: string; link: LinkRow } | null>(null)
  const reload = () => { void invalidateChange(qc, 'DocumentLink') }
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <ErrorBanner error={q.error} />
  const { links, inherited, canAdd } = q.data
  const editor = referencesOn && editing?.viewer === me.id && editing.type === type && editing.ownerId === id && links.some(link => link.id === editing.link.id && link.canChange) ? editing : null
  return (
    <div className="space-y-3 text-sm">
      {links.length === 0 && inherited.length === 0 && !adding && <p className="text-muted-foreground">{t('links.none')}</p>}
      {links.length + inherited.length > 0 && <ul className="divide-y rounded-md border">
        {links.map((l) => (
          <li key={l.id} data-document-link={referencesOn ? l.id : undefined} className={referencesOn ? 'flex min-h-(--row-min) flex-wrap items-center gap-2 px-3 py-1' : 'flex min-h-(--row-min) items-center gap-2 px-3 py-1'}>
            <LinkAnchor l={l} />
            <span className="ml-auto shrink-0 text-xs/[18px] text-muted-foreground">{runtimeText(`linkType.${l.linkType}`)}</span>
            {l.canChange && <Button variant="ghost" size="icon-sm" className="text-bad hover:text-bad" aria-label={t('links.remove', { title: l.title })} onClick={() => setRemoving(l)}><Trash2 className="size-4" /></Button>}
            {referencesOn && <div className="w-full min-w-0 space-y-2 pb-2"><LinkReferenceSummary context={l.referenceContext} focusKey={`links:${type}:${id}:${l.id}:own`} />{l.canChange && <Button size="sm" variant="outline" onClick={() => setEditing({ viewer: me.id, type, ownerId: id, link: l })}>{t('linkReference.edit')}</Button>}</div>}
          </li>
        ))}
        {inherited.map((l) => (
          <li key={l.id} data-document-link-inherited={referencesOn ? l.id : undefined} className={referencesOn ? 'flex min-h-(--row-min) flex-wrap items-center gap-2 bg-muted px-3 py-1' : 'flex min-h-(--row-min) items-center gap-2 bg-muted px-3 py-1'}>
            <LinkAnchor l={l} /><span className="ml-auto shrink-0 text-xs/[18px] text-muted-foreground">{t('links.inherited')}</span>
            {referencesOn && <div className="w-full min-w-0 pb-2"><LinkReferenceSummary context={l.referenceContext} focusKey={`links:${type}:${id}:${l.id}:inherited`} /></div>}
          </li>
        ))}
      </ul>}
      {adding ? <AddLink onDone={() => setAdding(false)} type={type} id={id} />
        : canAdd && <Button size="sm" variant="outline" onClick={() => setAdding(true)}><Plus className="size-4" />{t('links.add')}</Button>}
      {removing && (
        <ConfirmDialog open title={t('links.removeTitle', { title: removing.title })} body={t('links.removeBody')} confirmLabel={t('common.remove')}
          onOpenChange={(o) => !o && setRemoving(null)} onConfirm={async () => { await typedRequest('delete', '/api/v1/links/{id}', { params: { id: removing.id }, ifMatch: removing.rowVersion }); setRemoving(null); reload() }} />
      )}
      {editor && <LinkReferenceEditor key={`${editor.viewer}:${type}:${id}:${editor.link.id}`} link={editor.link} onClose={() => setEditing(null)} onDone={() => { setEditing(null); reload() }} />}
    </div>
  )
}

/** Title defaults to the last path segment and the type is detected from the address (DOC-02, FR-DOC-02). */
export function AddLink(props: { type: string; id: string; onDone: (ok: boolean) => void }) {
  const referencesOn = useFlag('people_screens'), me = useMe()
  return referencesOn ? <RegisteredAddLink key={`${me.id}:${props.type}:${props.id}`} {...props} /> : <LegacyAddLink {...props} />
}

function RegisteredAddLink({ type, id, onDone }: Parameters<typeof AddLink>[0]) {
  const prefix = useId(), qc = useQueryClient()
  const [value, setValue] = useState(() => ({ url: '', title: '', linkType: '', register: false, reference: emptyReference() }))
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<unknown>(null)
  const safe = useSafeForm(null, value, setValue), sending = useRef(false)
  const target = useRef({ type, id })
  const intent = useRef<{ signature: string; key: string; body: components['schemas']['DocumentLinkAddBody']; uncertain: boolean } | null>(null)
  const detected = detect(value.url)
  return <form data-document-link-add={`${type}:${id}`} className="grid gap-3 rounded-lg border bg-card p-4 sm:grid-cols-2" onSubmit={async event => {
    event.preventDefault(); if (sending.current) return
    if (!uncertain && !event.currentTarget.reportValidity()) return
    sending.current = true; setBusy(true); setError(null)
    try {
      if (!intent.current?.uncertain) {
        const body: components['schemas']['DocumentLinkAddBody'] = { url: value.url.trim(), title: value.title.trim() || undefined, linkType: value.linkType || undefined,
          ...(value.register ? { reference: referenceInput(value.reference) } : {}) }
        const signature = JSON.stringify(body)
        if (!intent.current || intent.current.signature !== signature) intent.current = { signature, key: crypto.randomUUID(), body, uncertain: false }
      }
      const command = intent.current!
      const result: unknown = await typedRequest('post', '/api/v1/items/{type}/{id}/links', { params: target.current, body: command.body, idempotencyKey: command.key })
      if (!isLinkResponse(result) || result.url !== command.body.url) throw new Error(t('app.error'))
      safe.clear(); onDone(true); void invalidateChange(qc, 'DocumentLink')
    } catch (caught) {
      if (intent.current && uncertainLinkRequest(caught)) intent.current.uncertain = true
      setUncertain(intent.current?.uncertain ?? false); setError(caught)
    } finally { sending.current = false; setBusy(false) }
  }}>
    <fieldset disabled={busy || uncertain} className="grid min-w-0 gap-3 sm:col-span-2 sm:grid-cols-2">
      <Field label={t('links.address')} htmlFor={`${prefix}-url`} className="sm:col-span-2" hint={value.url ? t('links.detected', { type: t(`linkType.${detected}`) }) : t('links.addressHint')}>
        <Input id={`${prefix}-url`} required autoFocus maxLength={2000} placeholder={t('links.urlPlaceholder')} value={value.url} onChange={event => setValue(old => ({ ...old, url: event.target.value }))} />
      </Field>
      <Field label={t('field.Title')} htmlFor={`${prefix}-title`} optional><Input id={`${prefix}-title`} maxLength={200} value={value.title} onChange={event => setValue(old => ({ ...old, title: event.target.value }))} /></Field>
      <Field label={t('common.type')} htmlFor={`${prefix}-type`}><select id={`${prefix}-type`} className={selectCls} value={value.linkType} onChange={event => setValue(old => ({ ...old, linkType: event.target.value }))}>
        <option value="">{t('links.autoType')}</option>{LINK_TYPES.map(x => <option key={x} value={x}>{t(`linkType.${x}`)}</option>)}
      </select></Field>
      <label className="flex items-start gap-2 sm:col-span-2"><input type="checkbox" checked={value.register} onChange={event => setValue(old => ({ ...old, register: event.target.checked }))} />{t('linkReference.enable')}</label>
      {value.register && <div className="sm:col-span-2"><LinkReferenceFields prefix={prefix} value={value.reference} onChange={reference => setValue(old => ({ ...old, reference }))} task={type === 'Task'} /></div>}
    </fieldset>
    {error != null && <div className="sm:col-span-2"><ErrorBanner error={error} /></div>}
    {error instanceof ApiError && Object.values(error.fieldErrors).flat().length > 0 && <ul className="list-disc pl-5 text-sm text-bad sm:col-span-2" aria-label={t('coord.refusalReasons')}>{Object.values(error.fieldErrors).flat().map((message, index) => <li key={index}>{message}</li>)}</ul>}
    {uncertain && <p role="status" className="text-sm text-warn sm:col-span-2">{t('linkReference.uncertain')}</p>}
    <div className="flex flex-wrap justify-end gap-2 sm:col-span-2"><Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => safe.close(() => onDone(false))}>{t('common.cancel')}</Button>
      <Button type="submit" size="sm" disabled={busy || (!uncertain && !value.url.trim())}>{busy && <Spinner />}{busy ? t('common.saving') : uncertain ? t('linkReference.retry') : t('common.add')}</Button></div>
  </form>
}

function LegacyAddLink({ type, id, onDone }: { type: string; id: string; onDone: (ok: boolean) => void }) {
  const [url, setUrl] = useState('')
  const [title, setTitle] = useState('')
  const [linkType, setLinkType] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const qc = useQueryClient()
  const detected = detect(url)
  return (
    <form className="grid gap-3 rounded-lg border bg-card p-4 sm:grid-cols-2" onSubmit={async (e) => {
      e.preventDefault(); setErr(null); setBusy(true)
      try { await typedRequest('post', '/api/v1/items/{type}/{id}/links', { params: { type, id }, body: { url: url.trim(), title: title.trim() || undefined, linkType: linkType || undefined } }); await invalidateChange(qc, 'DocumentLink'); onDone(true) } catch (x) { setErr(x) } finally { setBusy(false) }
    }}>
      <Field label={t('links.address')} htmlFor="link-url" className="sm:col-span-2" hint={url ? t('links.detected', { type: t(`linkType.${detected}`) }) : t('links.addressHint')}>
        <Input id="link-url" required autoFocus placeholder={t('links.urlPlaceholder')} value={url} onChange={(e) => setUrl(e.target.value)} />
      </Field>
      <Field label={t('field.Title')} htmlFor="link-title" optional><Input id="link-title" value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
      <Field label={t('common.type')} htmlFor="link-type">
        <select id="link-type" className={selectCls} value={linkType} onChange={(e) => setLinkType(e.target.value)}>
          <option value="">{t('links.autoType')}</option>{LINK_TYPES.map((x) => <option key={x} value={x}>{runtimeText(`linkType.${x}`)}</option>)}
        </select>
      </Field>
      {err != null && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
      <div className="flex justify-end gap-2 sm:col-span-2">
        <Button type="button" size="sm" variant="outline" onClick={() => onDone(false)}>{t('common.cancel')}</Button>
        <Button type="submit" size="sm" disabled={busy || !url.trim()}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.add')}</Button>
      </div>
    </form>
  )
}

/** Mirrors the server's detection so the form can say what it found (the server decides). */
function detect(url: string) {
  const u = url.trim()
  if (/^\\\\[^\\/:*?"<>|]+\\[^\\/:*?"<>|]+/.test(u)) return 'Network Folder'
  let host = ''
  try { host = new URL(u).hostname.toLowerCase() } catch { return 'Other' }
  if (host.endsWith('-my.sharepoint.com') || host === 'onedrive.live.com' || host === '1drv.ms') return 'OneDrive'
  if (host.endsWith('.sharepoint.com')) return 'SharePoint'
  if (host === 'teams.microsoft.com' || host === 'teams.live.com' || host.endsWith('.teams.microsoft.com')) return 'Teams'
  return 'Other'
}
