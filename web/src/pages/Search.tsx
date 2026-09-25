import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { GROUPS, Highlight, searchQuery, toHits, type Group } from '@/components/hub/search'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

const SIZE = 50

/** All results with type tabs (§18.1): counts per type, archived projects on request, 50 at a time. */
export function SearchPage() {
  const [sp, setSp] = useSearchParams()
  const q = sp.get('q') ?? ''
  const archived = sp.get('archived') === 'true'
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const counts = useQuery({ queryKey: ['search-counts', q, archived], queryFn: () => searchQuery(q, { includeArchived: archived, limit: 1 }), enabled: q.trim().length > 0 })
  const first = GROUPS.find((g) => (counts.data?.counts[g] ?? 0) > 0)
  const type = (sp.get('type') as Group | null) ?? first ?? 'projects'
  const rows = useInfiniteQuery({
    queryKey: ['search-page', q, archived, type], enabled: q.trim().length > 0, initialPageParam: 1,
    queryFn: ({ pageParam }) => searchQuery(q, { includeArchived: archived, type, limit: SIZE, page: pageParam }),
    getNextPageParam: (last, all) => (all.length * SIZE < (last.counts[type] ?? 0) ? all.length + 1 : undefined),
  })
  const hits = rows.data?.pages.flatMap((p) => toHits(type, p.groups[type])) ?? []
  return (
    <Page title={t('search.title')}>
      <form className="flex flex-wrap items-center gap-3" onSubmit={(e) => { e.preventDefault(); set('q', new FormData(e.currentTarget).get('q') as string); set('type', null) }}>
        <Input key={q} name="q" type="search" defaultValue={q} className="h-9 w-72" aria-label={t('common.search')} placeholder={t('top.search')} />
        <Button type="submit" size="sm">{t('common.search')}</Button>
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={archived} onCheckedChange={(c) => set('archived', c ? 'true' : null)} />{t('search.includeArchived')}</label>
      </form>
      {!q.trim() ? <Empty>{t('search.prompt')}</Empty> : (
        <>
          <div role="tablist" aria-label={t('search.types')} className="flex gap-1 overflow-x-auto border-b">
            {GROUPS.map((g) => (
              <button key={g} role="tab" aria-selected={type === g} onClick={() => set('type', g)}
                className={cn('whitespace-nowrap border-b-2 border-transparent px-3 py-2 text-sm text-muted-foreground hover:text-foreground', type === g && 'border-primary font-medium text-foreground')}>
                {t(`search.group.${g}`)} <span className="ml-1 rounded-full bg-muted px-1.5 text-xs">{counts.data?.counts[g] ?? 0}</span>
              </button>
            ))}
          </div>
          {(rows.error || counts.error) && <ErrorBanner error={rows.error ?? counts.error} />}
          {rows.isPending ? <Loading rows={6} /> : hits.length === 0 ? <Empty>{t('search.none', { q })}</Empty> : (
            <ul className="divide-y rounded-lg border bg-card">
              {hits.map((h) => {
                const Icon = h.icon
                const body = <>
                  <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                  {h.key && <span className="key shrink-0 font-mono text-xs text-muted-foreground">{h.key}</span>}
                  <span className="min-w-0 flex-1"><span className="block truncate font-medium">{h.title}</span>
                    {h.match && <span className="block truncate text-xs text-muted-foreground"><Highlight text={h.match} term={q} /></span>}</span>
                  <span className="hidden truncate text-xs text-muted-foreground sm:inline">{h.sub}</span>
                </>
                return <li key={h.id}>{h.href
                  ? <Link to={h.href} className="flex items-center gap-2 px-4 py-2 text-sm hover:bg-muted/40">{body}</Link>
                  : <div className="flex items-center gap-2 px-4 py-2 text-sm" title={t('search.noWorkAccess')}>{body}</div>}</li>
              })}
            </ul>
          )}
          {rows.hasNextPage && <Button variant="outline" onClick={() => rows.fetchNextPage()} disabled={rows.isFetchingNextPage}>{t('search.more')}</Button>}
        </>
      )}
    </Page>
  )
}
