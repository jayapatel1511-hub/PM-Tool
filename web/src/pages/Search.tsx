import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Section, selectCls } from '@/components/hub/common'
import { Key } from '@/components/hub/pills'
import { GROUPS, Highlight, SearchPersonDetails, searchQuery, toHits, type Group, type SearchResult } from '@/components/hub/search'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { useFlag, useMe } from '@/lib/auth'
import { t, tv } from '@/lib/i18n'
import { clearRecentSearches, isRecentSearchNavigation, readRecentSearches, rememberRecentSearch, type RecentSearchVisit } from '@/lib/recent-searches'
import { cn } from '@/lib/utils'

const SIZE = 50
const FILTER_KEYS = ['projectId', 'status', 'disciplineId'] as const
type SearchFilter = (typeof FILTER_KEYS)[number]

/** All results with type tabs (§18.1): counts per type, archived projects on request, 50 at a time. */
export function SearchPage() {
  const me = useMe(), people = useFlag('people_screens')
  const location = useLocation()
  const [sp, setSp] = useSearchParams()
  const q = sp.get('q') ?? ''
  const archived = sp.get('archived') === 'true'
  const projectId = people ? sp.get('projectId')?.trim() || undefined : undefined
  const status = people ? sp.get('status')?.trim() || undefined : undefined
  const disciplineId = people ? sp.get('disciplineId')?.trim() || undefined : undefined
  const personPanel = (sp.get('panel') ?? '').startsWith('Person:')
  const restoring = !!(location.state as { recordContextRestore?: unknown } | null)?.recordContextRestore
  const lastVisit = useRef<RecentSearchVisit | null>(null), loadedViewer = useRef<string | null>(null)
  const [history, setHistory] = useState<{ viewerId: string; items: string[]; notice?: 'cleared' | 'clearFailed' }>({ viewerId: me.id, items: [] })
  const recents = people && history.viewerId === me.id ? history.items : []
  useEffect(() => {
    const current = { viewerId: me.id, term: q }
    const remember = isRecentSearchNavigation(lastVisit.current, current, { enabled: people, personPanel, restoring })
    lastVisit.current = current
    if (!people) { loadedViewer.current = null; return }
    if (remember || loadedViewer.current !== me.id) {
      const items = remember ? rememberRecentSearch(me.id, q, true) : readRecentSearches(me.id, true)
      loadedViewer.current = me.id; setHistory({ viewerId: me.id, items })
    }
  }, [me.id, people, q, personPanel, restoring])
  const rememberSubmitted = (term: string) => {
    if (!people) return
    lastVisit.current = { viewerId: me.id, term }; loadedViewer.current = me.id
    setHistory({ viewerId: me.id, items: rememberRecentSearch(me.id, term, true) })
  }
  const clearHistory = () => {
    const cleared = clearRecentSearches(me.id, people)
    setHistory({ viewerId: me.id, items: cleared ? [] : readRecentSearches(me.id, people), notice: cleared ? 'cleared' : 'clearFailed' })
  }
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  // One navigation for the new term and the type reset: two set() calls in a row would each start from the old URL,
  // and the second would put the previous term back.
  const submit = (term: string) => {
    rememberSubmitted(term)
    const n = new URLSearchParams(sp)
    if (term) n.set('q', term); else n.delete('q')
    n.delete('type')
    // A new search begins with all types; a status cannot leave its explicit type.
    if (people) n.delete('status')
    setSp(n, { replace: true })
  }
  // No status here: these are the common-filter counts and the permission-scoped metadata bootstrap.
  // An empty on-flag query returns metadata only, never a blank-query record directory.
  const commonFilters = people ? { projectId, disciplineId } : {}
  const counts = useQuery({ queryKey: ['search-counts', q, archived, me.id, people, ...(people ? [projectId ?? null, disciplineId ?? null] : [])],
    queryFn: () => searchQuery(q, { includeArchived: archived, limit: 1, ...commonFilters }), enabled: people || q.trim().length > 0 })
  const countData = (!people || !counts.error) ? counts.data : undefined
  const liveScreen = people ? countData?.screen : undefined
  const heldScreen = useRef<{ viewerId: string; archived: boolean; value: NonNullable<SearchResult['screen']> } | null>(null)
  useEffect(() => {
    if (!people || counts.error || (counts.isSuccess && !liveScreen)) { heldScreen.current = null; return }
    if (liveScreen) heldScreen.current = { viewerId: me.id, archived, value: liveScreen }
  }, [people, me.id, archived, liveScreen, counts.error, counts.isSuccess])
  // Choices/capabilities are independent of term and selected filters. Keep an open filter
  // control mounted during that scope's next read, without reviving failed or other-viewer data.
  const screen = liveScreen ?? (people && counts.isPending && heldScreen.current?.viewerId === me.id && heldScreen.current.archived === archived ? heldScreen.current.value : undefined)
  const first = GROUPS.find(group => {
    if (!(countData?.counts[group] ?? 0)) return false
    if (!people) return true
    const capability = screen?.tabs.find(tab => tab.type === group)
    // All-type counts leave inapplicable groups unfiltered. Do not auto-select one and
    // then send an explicit rows request with a filter that group cannot support.
    return !!capability && (!projectId || capability.project) && (!disciplineId || capability.discipline)
  })
  const type = (sp.get('type') as Group | null) ?? first ?? 'projects'
  const tab = screen?.tabs.find(tab => tab.type === type)
  const rowsEnabled = q.trim().length > 0 && (!people || !!liveScreen)
  const rows = useInfiniteQuery({
    queryKey: ['search-page', q, archived, type, me.id, people, ...(people ? [projectId ?? null, status ?? null, disciplineId ?? null] : [])], enabled: rowsEnabled, initialPageParam: 1,
    queryFn: ({ pageParam }) => searchQuery(q, { includeArchived: archived, type, limit: SIZE, page: pageParam, ...(people ? { projectId, status, disciplineId } : {}) }),
    getNextPageParam: (last, all) => (all.length * SIZE < (last.counts[type] ?? 0) ? all.length + 1 : undefined),
  })
  const visiblePages = (!people || (liveScreen && !rows.error)) ? rows.data?.pages : undefined
  const hits = visiblePages?.flatMap((p) => toHits(type, p.groups[type])) ?? []
  const selectedCount = visiblePages?.[0] ? visiblePages[0].counts[type] ?? 0 : undefined
  const countFor = (group: Group) => people && group === type ? selectedCount : countData ? countData.counts[group] ?? 0 : undefined
  const selectedProject = projectId ? screen?.projects.find(project => project.id.toLowerCase() === projectId.toLowerCase()) : undefined
  const selectedDiscipline = disciplineId ? screen?.disciplines.find(discipline => discipline.id.toLowerCase() === disciplineId.toLowerCase()) : undefined
  const projectLabel = selectedProject ? `${selectedProject.projectNumber} · ${selectedProject.name}` : t('search.selectedProjectValue', { id: projectId })
  const disciplineLabel = selectedDiscipline ? selectedDiscipline.isActive ? selectedDiscipline.name : t('common.inactiveSuffix', { name: selectedDiscipline.name }) : t('search.selectedDisciplineValue', { id: disciplineId })
  const statusLabel = status && tab?.statusValues.includes(status) ? tv(status) : t('search.selectedStatusValue', { value: status })
  const tokens = [
    { key: 'projectId', label: t('common.project'), value: projectId ? projectLabel : null },
    { key: 'status', label: t('common.status'), value: status ? statusLabel : null },
    { key: 'disciplineId', label: t('common.discipline'), value: disciplineId ? disciplineLabel : null },
  ].flatMap(token => token.value ? [{ ...token, value: token.value }] : [])
  const changeFilter = (key: SearchFilter, value: string) => {
    const n = new URLSearchParams(sp)
    if (value) n.set(key, value); else n.delete(key)
    n.set('type', type) // Keep the inspected tab stable while common-filter counts change.
    setSp(n, { replace: true })
  }
  const clearFilters = () => { const n = new URLSearchParams(sp); FILTER_KEYS.forEach(key => n.delete(key)); setSp(n, { replace: true }) }
  const changeTab = (group: Group) => {
    if (!people) { set('type', group); return }
    const next = screen?.tabs.find(tab => tab.type === group), n = new URLSearchParams(sp)
    n.set('type', group); n.delete('status')
    if (!next?.project) n.delete('projectId')
    if (!next?.discipline) n.delete('disciplineId')
    setSp(n, { replace: true })
  }
  const error = rows.error ?? counts.error ?? (people && counts.isSuccess && !screen ? new Error(t('search.optionsUnavailable')) : null)
  const retry = () => { void counts.refetch(); if (rowsEnabled) void rows.refetch() }
  return (
    <Page title={t('search.title')}>
      <FilterBar quick={<label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={archived} onCheckedChange={(c) => set('archived', c ? 'true' : null)} />{t('search.includeArchived')}</label>}
        activeCount={people ? tokens.length : 0}
        active={people && tokens.length > 0 && <div className="min-w-0 max-w-full [&_strong]:min-w-0 [&_strong]:break-all [&_[role=group]>span]:min-w-0 [&_[role=group]>span]:max-w-full"><ActiveFilters tokens={tokens} onRemove={key => set(key, null)} onClear={clearFilters} /></div>}
        search={<form className="flex items-center gap-2 sm:w-[28rem]" onSubmit={(e) => { e.preventDefault(); submit(new FormData(e.currentTarget).get('q') as string) }}>
          <Field label={t('common.search')} htmlFor="search-q" className="min-w-0 flex-1">
            <Input key={q} id="search-q" name="q" type="search" defaultValue={q} placeholder={t(people && type === 'people' ? 'search.peoplePlaceholder' : 'top.search')} />
          </Field>
          <Button type="submit">{t('common.search')}</Button>
        </form>}>
        {people && screen && tab && <div className="grid min-w-0 gap-4 sm:grid-cols-3">
          {tab?.project && <Field label={t('common.project')} htmlFor="search-project-filter" className="min-w-0">
            <select id="search-project-filter" className={`${selectCls} max-w-full`} value={projectId ?? ''} onChange={event => changeFilter('projectId', event.target.value)} aria-describedby={screen.projectsTruncated ? 'search-project-choices-hint' : undefined}>
              <option value="">{t('search.anyProject')}</option>
              {projectId && !selectedProject && <option value={projectId}>{projectLabel}</option>}
              {screen.projects.map(project => <option key={project.id} value={selectedProject?.id === project.id ? projectId ?? project.id : project.id}>{project.projectNumber} · {project.name}</option>)}
            </select>
            {screen.projectsTruncated && <p id="search-project-choices-hint" className="text-xs text-muted-foreground">{t('search.projectChoicesLimited', { n: screen.projects.length })}</p>}
          </Field>}
          {tab?.status && <Field label={t('common.status')} htmlFor="search-status-filter" className="min-w-0">
            <select id="search-status-filter" className={`${selectCls} max-w-full`} value={status ?? ''} onChange={event => changeFilter('status', event.target.value)}>
              <option value="">{t('search.anyStatus')}</option>
              {status && !tab.statusValues.includes(status) && <option value={status}>{statusLabel}</option>}
              {tab.statusValues.map(value => <option key={value} value={value}>{tv(value)}</option>)}
            </select>
          </Field>}
          {tab?.discipline && <Field label={t('common.discipline')} htmlFor="search-discipline-filter" className="min-w-0">
            <select id="search-discipline-filter" className={`${selectCls} max-w-full`} value={disciplineId ?? ''} onChange={event => changeFilter('disciplineId', event.target.value)} aria-describedby={screen.disciplinesTruncated ? 'search-discipline-choices-hint' : undefined}>
              <option value="">{t('search.anyDiscipline')}</option>
              {disciplineId && !selectedDiscipline && <option value={disciplineId}>{disciplineLabel}</option>}
              {screen.disciplines.map(discipline => <option key={discipline.id} value={selectedDiscipline?.id === discipline.id ? disciplineId ?? discipline.id : discipline.id}>{discipline.isActive ? discipline.name : t('common.inactiveSuffix', { name: discipline.name })}</option>)}
            </select>
            {screen.disciplinesTruncated && <p id="search-discipline-choices-hint" className="text-xs text-muted-foreground">{t('search.disciplineChoicesLimited', { n: screen.disciplines.length })}</p>}
          </Field>}
        </div>}
      </FilterBar>
      {people && (projectId || disciplineId) && <p className="text-xs text-muted-foreground">{t('search.commonFiltersHint')}</p>}
      {people && status && tab?.status && <p className="text-xs text-muted-foreground">{t('search.statusCountsHint')}</p>}
      {people && error && <ErrorBanner error={error} retry={retry} />}
      {people && <section aria-labelledby="search-recents-title" aria-describedby="search-recents-hint" className="min-w-0 rounded-lg border bg-card px-4 py-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 id="search-recents-title" className="text-sm font-medium">{t('search.recentTitle')}</h2>
          <Button type="button" variant="ghost" size="sm" onClick={clearHistory} aria-label={t('search.clearRecent')}>{t('common.clear')}</Button>
        </div>
        <p id="search-recents-hint" className="text-xs text-muted-foreground">{t('search.recentBrowserOnly')}</p>
        {recents.length ? <ol className="mt-2 flex min-w-0 list-none flex-wrap gap-2">{recents.map(term => <li key={term} className="min-w-0 max-w-full">
          <Button type="button" variant="outline" size="sm" className="h-auto min-w-0 max-w-full whitespace-normal text-left" onClick={() => submit(term)}><span className="min-w-0 [overflow-wrap:anywhere]">{term}</span></Button>
        </li>)}</ol> : <p className="mt-2 text-sm text-muted-foreground">{t('search.recentEmpty')}</p>}
        <p role="status" className="mt-1 text-xs text-muted-foreground">{history.viewerId === me.id && history.notice ? t(history.notice === 'cleared' ? 'search.recentCleared' : 'search.recentClearFailed') : ''}</p>
      </section>}
      {!q.trim() ? <div className="rounded-lg border bg-card"><Empty>{t('search.prompt')}</Empty></div> : (
        <>
          <div role="tablist" aria-label={t('search.types')} className="flex min-w-0 flex-wrap gap-1 border-b">
            {GROUPS.map((g) => (
              <button key={g} type="button" role="tab" id={`search-tab-${g}`} aria-selected={type === g} aria-controls="search-results" onClick={() => changeTab(g)}
                className={cn('-mb-px inline-flex min-h-11 shrink-0 items-center whitespace-nowrap border-b-2 px-3 text-sm',
                  type === g ? 'border-primary font-semibold text-foreground' : 'border-transparent text-muted-foreground hover:bg-muted hover:text-foreground')}>
                {t(`search.group.${g}`)}<span className="ml-1.5 rounded-md bg-secondary px-1.5 text-xs/[18px] font-medium tabular-nums">{people && countFor(g) === undefined ? <Missing /> : countFor(g) ?? 0}</span>
              </button>
            ))}
          </div>
          {!people && error && <ErrorBanner error={error} retry={retry} />}
          <div role="tabpanel" id="search-results" aria-labelledby={`search-tab-${type}`}>
            <Section title={t(`search.group.${type}`)} count={people ? selectedCount : counts.data?.counts[type]}>
              {people && error ? <Empty>{t(screen ? 'search.resultsUnavailable' : 'search.optionsUnavailable')}</Empty> : rows.isPending ? <Loading rows={6} /> : hits.length === 0 ? <Empty>{t('search.none', { q })}</Empty> : (
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
                    if (!people || !h.person) return <li key={h.id}>{h.href ? <Link to={h.href} className={cn(cls, 'hover:bg-muted')}>{body}</Link> : <div className={cls}>{body}</div>}</li>
                    return <li key={h.id} data-search-page-hit={`${type}:${h.id}`} className="flex min-w-0 flex-wrap items-center">{h.href ? <Link to={h.href} className={cn(cls, 'min-w-0 flex-1 hover:bg-muted')}>{body}</Link> : <div className={cn(cls, 'min-w-0 flex-1')}>{body}</div>}
                      <div className="min-w-0 px-5 pb-2 sm:py-2"><SearchPersonDetails hit={h} surface="page" /></div>
                    </li>
                  })}
                </ul>
              )}
              {rows.hasNextPage && (!people || (!error && screen)) && <div className="border-t px-5 py-3"><Button variant="outline" size="sm" onClick={() => rows.fetchNextPage()} disabled={rows.isFetchingNextPage}>{t('search.more')}</Button></div>}
            </Section>
          </div>
        </>
      )}
    </Page>
  )
}
