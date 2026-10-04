import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Notice, Page, Section, selectCls } from '@/components/hub/common'
import { AddLink, LinkAnchor } from '@/components/hub/links'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key } from '@/components/hub/pills'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { plural, t } from '@/lib/i18n'
import { accentOf } from '@/lib/utils'

interface FileLink { id: string; title: string; url: string; linkType: string; isNetworkPath: boolean; addedBy?: string; addedAt: string }
interface FileProject {
  projectId: string; projectNumber: string; name: string; canAdd: boolean; count: number
  items: { itemType: string; itemId: string; key: string; name: string; links: FileLink[] }[]
}

/** Files (§36.8, FR-VIS-09): the selected projects' document links grouped by project and linked item, with search, a type
 *  filter and Copy path for network folders. A link library only: nothing is uploaded or stored. */
export function FilesPage() {
  const scope = useScope()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const [adding, setAdding] = useState<string | null>(null)
  const f = { q: sp.get('q') ?? undefined, type: sp.get('type') ?? undefined }
  // The box shows the applied search until someone types, so a removed token or Clear updates it too.
  const [draft, setDraft] = useState<string | null>(null)
  const term = draft ?? f.q ?? ''
  const q = useQuery({ queryKey: ['files', scope.api, f], enabled: scope.ready,
    queryFn: () => get<{ projects: FileProject[]; total: number; truncated: boolean; types: string[] }>(`files${qs({ projects: scope.api, ...f })}`) })
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const tokens = ([['q', t('common.search'), f.q && `“${f.q}”`], ['type', t('common.type'), f.type && t(`linkType.${f.type}`)]] as const).filter(([, , v]) => v)
  const clear = () => { const n = new URLSearchParams(sp); n.delete('q'); n.delete('type'); setSp(n, { replace: true }) }
  return (
    <Page title={t('nav.files')} subtitle={q.data ? plural(q.data.total, 'files.one', 'files.many', { n: q.data.total }) : scope.label}>
      <WorkspaceTabs />
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.search')} htmlFor="files-search" className="w-full sm:w-72">
            <form onSubmit={(e) => { e.preventDefault(); set('q', term.trim() || null); setDraft(null) }}>
              <Input id="files-search" type="search" placeholder={t('files.search')} value={term}
                onChange={(e) => { if (e.target.value) setDraft(e.target.value); else { setDraft(null); set('q', null) } }} />
            </form>
          </Field>
          <Field label={t('common.type')} htmlFor="files-type" className="w-full sm:w-48">
            <select id="files-type" className={selectCls} value={f.type ?? ''} onChange={(e) => set('type', e.target.value)}>
              <option value="">{t('files.anyType')}</option>{q.data?.types.map((x) => <option key={x} value={x}>{t(`linkType.${x}`)}</option>)}
            </select>
          </Field>
          <p className="text-sm text-muted-foreground sm:pb-2.5">{t('files.note')}</p>
        </div>
        <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value as string }))} onRemove={(k) => set(k, null)} onClear={clear} />
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.data?.truncated && <Notice title={t('ws.truncatedTitle')}>{t('files.truncated')}</Notice>}
      {!q.data ? (q.isPending && <div className="rounded-lg border bg-card"><Loading rows={6} /></div>) : q.data.projects.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('files.none')}</Empty></div>
      ) : q.data.projects.map((p) => (
        <Section key={p.projectId} id={`f-${p.projectId}`} accent={accentOf(p.projectId)} count={p.count}
          title={<Link className="hover:underline" to={`/projects/${p.projectNumber}`}><span className="key mr-2 text-(--acc-fg)">{p.projectNumber}</span>{p.name}</Link>}
          actions={p.canAdd && adding !== p.projectId && <Button size="sm" variant="outline" onClick={() => setAdding(p.projectId)}><Plus className="size-4" />{t('links.add')}</Button>}>
          {adding === p.projectId && <div className="border-b p-4"><AddLink path={`items/Project/${p.projectId}/links`} onDone={(ok) => { setAdding(null); if (ok) q.refetch() }} /></div>}
          {p.items.length === 0 ? <Empty>{t('files.noneHere')}</Empty> : (
            <ul className="divide-y">
              {p.items.map((i) => (
                <li key={`${i.itemType}-${i.itemId}`} className="px-5 py-3">
                  <p className="mb-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs/[18px] text-muted-foreground">
                    <span className="font-medium">{t(`itemType.${i.itemType}`)}</span>
                    {i.itemType === 'Project' ? <span>{i.name}</span>
                      : <button type="button" className="text-left text-sm text-foreground hover:underline" onClick={() => openPanel(i.itemType, i.itemId)}><Key>{i.key}</Key> {i.name}</button>}
                  </p>
                  <ul className="space-y-1">
                    {i.links.map((l) => (
                      <li key={l.id} className="flex min-h-8 flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                        <LinkAnchor l={l} />
                        <span className="text-xs/[18px] text-muted-foreground">{t(`linkType.${l.linkType}`)}</span>
                        <span className="ml-auto text-xs/[18px] text-muted-foreground tabular-nums">{t('files.added', { name: l.addedBy ?? '', date: fmtDate(l.addedAt.slice(0, 10)) })}</span>
                      </li>
                    ))}
                  </ul>
                </li>
              ))}
            </ul>
          )}
        </Section>
      ))}
    </Page>
  )
}
