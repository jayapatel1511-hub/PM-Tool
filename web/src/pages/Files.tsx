import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { AddLink, LinkAnchor } from '@/components/hub/links'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key } from '@/components/hub/pills'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { plural, t } from '@/lib/i18n'

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
  const [term, setTerm] = useState(sp.get('q') ?? '')
  const [adding, setAdding] = useState<string | null>(null)
  const f = { q: sp.get('q') ?? undefined, type: sp.get('type') ?? undefined }
  const q = useQuery({ queryKey: ['files', scope.api, f], enabled: scope.ready,
    queryFn: () => get<{ projects: FileProject[]; total: number; truncated: boolean; types: string[] }>(`files${qs({ projects: scope.api, ...f })}`) })
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  return (
    <Page title={t('nav.files')} subtitle={q.data ? plural(q.data.total, 'files.one', 'files.many', { n: q.data.total }) : scope.label}>
      <WorkspaceTabs />
      <div className="flex flex-wrap items-center gap-2">
        <form onSubmit={(e) => { e.preventDefault(); set('q', term.trim() || null) }}>
          <Input type="search" className="h-8 w-64" placeholder={t('files.search')} aria-label={t('common.search')} value={term} onChange={(e) => { setTerm(e.target.value); if (!e.target.value) set('q', null) }} />
        </form>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={f.type ?? ''} onChange={(e) => set('type', e.target.value)} aria-label={t('common.type')}>
          <option value="">{t('files.anyType')}</option>{q.data?.types.map((x) => <option key={x} value={x}>{t(`linkType.${x}`)}</option>)}
        </select>
        <p className="text-xs text-muted-foreground">{t('files.note')}</p>
      </div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {!q.data ? (q.isPending && <Loading rows={6} />) : q.data.projects.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('files.none')}</Empty></div> : q.data.projects.map((p) => (
        <section key={p.projectId} aria-labelledby={`f-${p.projectId}`} className="rounded-lg border bg-card">
          <div className="flex flex-wrap items-center gap-2 border-b px-4 py-2">
            <h2 id={`f-${p.projectId}`} className="text-sm font-semibold"><Link className="hover:underline" to={`/projects/${p.projectNumber}`}><span className="key font-mono font-normal text-muted-foreground">{p.projectNumber}</span> {p.name}</Link></h2>
            <span className="text-xs text-muted-foreground">{p.count}</span>
            <div className="flex-1" />
            {p.canAdd && adding !== p.projectId && <Button size="sm" variant="outline" onClick={() => setAdding(p.projectId)}><Plus className="size-3.5" />{t('links.add')}</Button>}
          </div>
          {adding === p.projectId && <div className="border-b p-3"><AddLink path={`items/Project/${p.projectId}/links`} onDone={(ok) => { setAdding(null); if (ok) q.refetch() }} /></div>}
          {p.items.length === 0 ? <Empty>{t('files.noneHere')}</Empty> : (
            <ul className="divide-y">
              {p.items.map((i) => (
                <li key={`${i.itemType}-${i.itemId}`} className="px-4 py-2">
                  <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                    <span>{t(`itemType.${i.itemType}`)}</span>
                    {i.itemType === 'Project' ? <span>{i.name}</span>
                      : <button type="button" className="hover:underline" onClick={() => openPanel(i.itemType, i.itemId)}><Key>{i.key}</Key> {i.name}</button>}
                  </div>
                  <ul className="space-y-1 text-sm">
                    {i.links.map((l) => (
                      <li key={l.id} className="flex flex-wrap items-center gap-x-3 gap-y-0.5">
                        <LinkAnchor l={l} />
                        <span className="text-xs text-muted-foreground">{t(`linkType.${l.linkType}`)}</span>
                        <span className="ml-auto text-xs text-muted-foreground">{t('files.added', { name: l.addedBy ?? '', date: fmtDate(l.addedAt.slice(0, 10)) })}</span>
                      </li>
                    ))}
                  </ul>
                </li>
              ))}
            </ul>
          )}
        </section>
      ))}
      {q.data?.truncated && <p className="text-xs text-muted-foreground">{t('files.truncated')}</p>}
    </Page>
  )
}
