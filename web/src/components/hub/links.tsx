import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, Cloud, Copy, ExternalLink, Files, FolderOpen, Globe, Link2, MessagesSquare, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, ErrorBanner, Field, Loading, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { del, get, post } from '@/lib/api'
import { t } from '@/lib/i18n'
import { ItemSlots, type ItemPartProps } from '@/pages/projects/slots-items'

// Document links (§12.7): where the files live. The Hub stores pointers only, never files (FR-DOC-03).

interface LinkRow { id: string; title: string; url: string; linkType: string; isNetworkPath: boolean; addedByName?: string; addedAt: string; canChange: boolean }
interface LinkList { canAdd: boolean; links: LinkRow[]; inherited: LinkRow[] }

const LINK_TYPES = ['SharePoint', 'OneDrive', 'Teams', 'Network Folder', 'External DMS', 'Client Portal', 'Other']
export const LINK_ICON: Record<string, typeof Link2> = {
  SharePoint: Files, OneDrive: Cloud, Teams: MessagesSquare, 'Network Folder': FolderOpen, 'External DMS': Archive, 'Client Portal': Globe, Other: Link2,
}

/** A UNC path cannot be opened by a browser, so it offers "Copy path" instead of a link (AC-DOC-02). */
export function LinkAnchor({ l }: { l: Pick<LinkRow, 'title' | 'url' | 'linkType' | 'isNetworkPath'> }) {
  const Icon = LINK_ICON[l.linkType] ?? Link2
  if (l.isNetworkPath || l.url.startsWith('\\\\')) return (
    <span className="inline-flex min-w-0 items-center gap-1.5">
      <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
      <span className="truncate" title={l.url}>{l.title}</span>
      <Button size="sm" variant="outline" className="h-6 px-1.5 text-xs" onClick={() => { navigator.clipboard?.writeText(l.url); toast.success(t('links.pathCopied')) }}>
        <Copy className="size-3" />{t('links.copyPath')}
      </Button>
    </span>
  )
  return (
    <a href={l.url} target="_blank" rel="noreferrer noopener" className="inline-flex min-w-0 items-center gap-1.5 text-primary hover:underline" title={l.url}>
      <Icon className="size-4 shrink-0" aria-hidden /><span className="truncate">{l.title}</span><ExternalLink className="size-3 shrink-0 opacity-60" aria-hidden />
    </a>
  )
}

export function LinksPanel({ type, id }: ItemPartProps) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['links', type, id], queryFn: () => get<LinkList>(`items/${type}/${id}/links`) })
  const [adding, setAdding] = useState(false)
  const [removing, setRemoving] = useState<LinkRow | null>(null)
  const reload = () => { qc.invalidateQueries({ queryKey: ['links', type, id] }); qc.invalidateQueries({ queryKey: ['history', type, id] }) }
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <ErrorBanner error={q.error} />
  const { links, inherited, canAdd } = q.data
  return (
    <div className="space-y-2 text-sm">
      {links.length === 0 && inherited.length === 0 && !adding && <p className="text-muted-foreground">{t('links.none')}</p>}
      <ul className="space-y-1.5">
        {links.map((l) => (
          <li key={l.id} className="flex items-center gap-2">
            <LinkAnchor l={l} />
            <span className="ml-auto shrink-0 text-xs text-muted-foreground">{t(`linkType.${l.linkType}`)}</span>
            {l.canChange && <Button variant="ghost" size="icon" className="size-7" aria-label={t('links.remove', { title: l.title })} onClick={() => setRemoving(l)}><Trash2 className="size-3.5" /></Button>}
          </li>
        ))}
        {inherited.map((l) => (
          <li key={l.id} className="flex items-center gap-2 text-muted-foreground">
            <LinkAnchor l={l} /><span className="ml-auto shrink-0 text-xs">{t('links.inherited')}</span>
          </li>
        ))}
      </ul>
      {adding ? <AddLink onDone={(ok) => { setAdding(false); if (ok) reload() }} path={`items/${type}/${id}/links`} />
        : canAdd && <Button size="sm" variant="outline" onClick={() => setAdding(true)}><Plus className="size-3.5" />{t('links.add')}</Button>}
      {removing && (
        <ConfirmDialog open title={t('links.removeTitle', { title: removing.title })} body={t('links.removeBody')} confirmLabel={t('common.remove')}
          onOpenChange={(o) => !o && setRemoving(null)} onConfirm={async () => { await del(`links/${removing.id}`); setRemoving(null); reload() }} />
      )}
    </div>
  )
}

/** Title defaults to the last path segment and the type is detected from the address (DOC-02, FR-DOC-02). */
export function AddLink({ path, onDone }: { path: string; onDone: (ok: boolean) => void }) {
  const [url, setUrl] = useState('')
  const [title, setTitle] = useState('')
  const [type, setType] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const detected = detect(url)
  return (
    <form className="grid gap-2 rounded-md border p-3 sm:grid-cols-2" onSubmit={async (e) => {
      e.preventDefault(); setErr(null)
      try { await post(path, { url: url.trim(), title: title.trim() || undefined, linkType: type || undefined }); onDone(true) } catch (x) { setErr(x) }
    }}>
      <Field label={t('links.address')} htmlFor="link-url" className="sm:col-span-2" hint={url ? t('links.detected', { type: t(`linkType.${detected}`) }) : t('links.addressHint')}>
        <Input id="link-url" required autoFocus placeholder={t('links.urlPlaceholder')} value={url} onChange={(e) => setUrl(e.target.value)} />
      </Field>
      <Field label={t('field.Title')} htmlFor="link-title" hint={t('common.optional')}><Input id="link-title" placeholder={t('links.titlePlaceholder')} value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
      <Field label={t('common.type')} htmlFor="link-type">
        <select id="link-type" className={selectCls} value={type} onChange={(e) => setType(e.target.value)}>
          <option value="">{t('links.autoType')}</option>{LINK_TYPES.map((x) => <option key={x} value={x}>{t(`linkType.${x}`)}</option>)}
        </select>
      </Field>
      {err != null && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
      <div className="flex justify-end gap-2 sm:col-span-2">
        <Button type="button" size="sm" variant="ghost" onClick={() => onDone(false)}>{t('common.cancel')}</Button>
        <Button type="submit" size="sm" disabled={!url.trim()}>{t('common.add')}</Button>
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

ItemSlots.Links = LinksPanel
