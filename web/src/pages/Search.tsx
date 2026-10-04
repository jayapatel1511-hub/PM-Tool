import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Field, FilterBar, Loading, Page, Section } from '@/components/hub/common'
import { Key } from '@/components/hub/pills'
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
  // One navigation for the new term and the type reset: two set() calls in a row would each start from the old URL,
  // and the second would put the previous term back.
  const submit = (term: string) => { const n = new URLSearchParams(sp); if (term) n.set('q', term); else n.delete('q'); n.delete('type'); setSp(n, { replace: true }) }
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
      <FilterBar>
        <form className="flex flex-wrap items-end gap-3" onSubmit={(e) => { e.preventDefault(); submit(new FormData(e.currentTarget).get('q') as string) }}>
          <Field label={t('common.search')} htmlFor="search-q" className="w-full sm:w-96">
            <Input key={q} id="search-q" name="q" type="search" defaultValue={q} placeholder={t('top.search')} />
          </Field>
          <Button type="submit">{t('common.search')}</Button>
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={archived} onCheckedChange={(c) => set('archived', c ? 'true' : null)} />{t('search.includeArchived')}</label>
        </form>
      </FilterBar>
      {!q.trim() ? <div className="rounded-lg border bg-card"><Empty>{t('search.prompt')}</Empty></div> : (
        <>
          <div role="tablist" aria-label={t('search.types')} className="scroll-region scroll-thin flex gap-1 overflow-x-auto border-b">
            {GROUPS.map((g) => (
              <button key={g} type="button" role="tab" id={`search-tab-${g}`} aria-selected={type === g} aria-controls="search-results" onClick={() => set('type', g)}
                className={cn('-mb-px inline-flex min-h-11 shrink-0 items-center whitespace-nowrap border-b-2 px-3 text-sm',
                  type === g ? 'border-primary font-semibold text-foreground' : 'border-transparent text-muted-foreground hover:bg-muted hover:text-foreground')}>
                {t(`search.group.${g}`)}<span className="ml-1.5 rounded-md bg-secondary px-1.5 text-xs/[18px] font-medium tabular-nums">{counts.data?.counts[g] ?? 0}</span>
              </button>
            ))}
          </div>
          {(rows.error || counts.error) && <ErrorBanner error={rows.error ?? counts.error} retry={() => { rows.refetch(); counts.refetch() }} />}
          <div role="tabpanel" id="search-results" aria-labelledby={`search-tab-${type}`}>
            <Section title={t(`search.group.${type}`)} count={counts.data?.counts[type]}>
              {rows.isPending ? <Loading rows={6} /> : hits.length === 0 ? <Empty>{t('search.none', { q })}</Empty> : (
                <ul className="divide-y">
                  {hits.map((h) => {
                    const Icon = h.icon
                    const cls = 'flex min-h-(--row-min) items-start gap-3 px-5 py-(--cell-py) text-sm'
                    const body = <>
                      <Icon className="mt-0.5 size-[18px] shrink-0 text-muted-foreground" aria-hidden />
                      <span className="min-w-0 flex-1">
                        <span className="flex flex-wrap items-baseline gap-x-2">{h.key && <Key>{h.key}</Key>}<span className="min-w-0 break-words font-medium">{h.title}</span></span>
                        {h.sub && <span className="block text-xs/[18px] text-muted-foreground">{h.sub}</span>}
                        {h.match && <span className="block text-xs/[18px] text-muted-foreground"><Highlight text={h.match} term={q} /></span>}
                        {!h.href && <span className="block text-xs/[18px] text-muted-foreground">{t('search.noWorkAccess')}</span>}
                      </span>
                    </>
                    return <li key={h.id}>{h.href ? <Link to={h.href} className={cn(cls, 'hover:bg-muted')}>{body}</Link> : <div className={cls}>{body}</div>}</li>
                  })}
                </ul>
              )}
              {rows.hasNextPage && <div className="border-t px-5 py-3"><Button variant="outline" size="sm" onClick={() => rows.fetchNextPage()} disabled={rows.isFetchingNextPage}>{t('search.more')}</Button></div>}
            </Section>
          </div>
        </>
      )}
    </Page>
  )
}
