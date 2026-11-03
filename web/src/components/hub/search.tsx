import { visibilityLabel } from '@/components/planner/labels'
import { useQuery } from '@tanstack/react-query'
import { Briefcase, CalendarCheck, CheckSquare, Flag, FileText, Info, Link2Off, MessageSquare, Scale, Search, User, type LucideIcon } from 'lucide-react'
import { useEffect, useId, useMemo, useRef, useState, type RefObject } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { useIsPhone } from '@/components/hub/common'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import type { components, paths } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export const GROUPS = ['projects', 'tasks', 'deliverables', 'milestones', 'decisions', 'handoffs', 'reviews', 'changes', 'submissions', 'allocations', 'design-basis', 'constraints', 'commitments', 'planning', 'comments', 'people'] as const
export type Group = (typeof GROUPS)[number]
type Schemas = components['schemas']
export type SearchResult = Schemas['SearchResponse']
type Rows = NonNullable<NonNullable<SearchResult['groups'][Group]>>
type AllKeys<U> = U extends unknown ? keyof U : never
type Loose<U, K extends PropertyKey = AllKeys<U>> = U extends unknown ? U & { [P in Exclude<K, keyof U>]?: undefined } : never
/** A row of any group that shares the key, name and status shape, with the fields only some of them carry read as optional: a field the server renames or drops fails the build here. */
type Row = Loose<NonNullable<SearchResult['groups'][Exclude<Group, 'projects' | 'people' | 'planning' | 'comments'>]>[number]>
export interface Hit { id: string; group?: Group; icon: LucideIcon; key?: string; title: string; sub: string; href: string | null; match?: string | null; person?: Schemas['SearchUserReference'] | null }
const PERSON_ROLE: Partial<Record<Group, string>> = { projects: 'pm', tasks: 'assignee', deliverables: 'owner', handoffs: 'receivingOwner', submissions: 'coordinator', allocations: 'person', 'design-basis': 'owner', constraints: 'removalOwner', commitments: 'performer', comments: 'author', planning: 'person', people: 'person' }
const hitKey = (hit: Hit) => `${hit.group ?? 'all'}:${hit.id}`

/** The server pairs the actual User ID/name; titles and item IDs never supply a Person identity. */
export function SearchPersonDetails({ hit, surface }: { hit: Hit; surface: 'palette' | 'page' }) {
  const enabled = useFlag('people_screens'), person = hit.person
  if (!enabled || !hit.group || !person?.id || !person.displayName || person.role !== PERSON_ROLE[hit.group]) return null
  return <PersonPanelTrigger userId={person.id} name={person.displayName} active={person.isActive}
    focusKey={`search:${surface}:${hit.group}:${hit.id}:${person.role}:details`} className="inline-flex min-h-9 items-center gap-1.5 text-xs">
    <Info className="size-4 shrink-0" aria-hidden /><span className="min-w-0 break-words">{t('common.details')}: {person.isActive ? person.displayName : t('common.inactiveSuffix', { name: person.displayName })}</span>
  </PersonPanelTrigger>
}

/** Item keys such as 1234-T0042 open the item directly (§18.1, FR-SRCH-02). */
const TAB: Record<string, string> = { Task: 'tasks', Deliverable: 'deliverables', Milestone: 'milestones', Decision: 'decisions', Risk: 'risks', Issue: 'issues', Action: 'meetings', Handoff: 'handoffs', ReviewPackage: 'reviews', ReviewFinding: 'reviews', ChangeNotice: 'changes', ChangeAssessment: 'changes', SubmissionCheck: 'submissions',
  WorkConstraint: 'readiness', OutputCommitment: 'readiness' }
const TYPE: Record<string, string> = { tasks: 'Task', deliverables: 'Deliverable', milestones: 'Milestone', decisions: 'Decision', handoffs: 'Handoff', reviews: 'ReviewPackage', changes: 'ChangeNotice', submissions: 'SubmissionPackage', allocations: 'ResourceAllocation', 'design-basis': 'DesignBasisEntry', constraints: 'WorkConstraint', commitments: 'OutputCommitment', planning: 'PlanningEntry' }
const ICON: Record<Group, LucideIcon> = { projects: Briefcase, tasks: CheckSquare, deliverables: FileText, milestones: Flag, decisions: Scale, comments: MessageSquare, people: User, handoffs: FileText, reviews: FileText, changes: FileText, submissions: FileText, allocations: CalendarCheck, 'design-basis': FileText, constraints: Link2Off, commitments: CalendarCheck, planning: CalendarCheck }

export function itemHref(type: string, projectNumber: string, id?: string | null) {
  const base = `/projects/${encodeURIComponent(projectNumber)}`
  if (id && type === 'ResourceAllocation') return `${base}/allocations?allocation=${id}`
  if (id && type === 'DesignBasisEntry') return `${base}/design-basis?basis=${id}`
  if (id && type === 'SubmissionPackage') return `${base}/submissions?panel=SubmissionPackage:${id}`
  if (id && type === 'PlanningEntry') return `/planner?entry=${id}`
  return type === 'Project' || !id ? base : `${base}/${TAB[type] ?? 'dashboard'}?panel=${type}:${id}`
}
export function toHits(group: Group, rows?: Rows | null): Hit[] {
  const list = rows ?? []
  return baseHits(group, list).map((hit, index) => {
    const row = list[index]
    return { ...hit, group, person: row && 'person' in row ? row.person : undefined }
  })
}
function baseHits(group: Group, rows: Rows): Hit[] {
  const icon = ICON[group], list = rows ?? []
  if (group === 'projects') return (list as Schemas['SearchProjectHit'][]).map((p) => ({ id: p.id, icon, key: p.projectNumber, title: p.name, sub: [p.client, p.pm, tv(p.status)].filter(Boolean).join(' · '), href: itemHref('Project', p.projectNumber) }))
  if (group === 'people') return (list as Schemas['SearchPersonHit'][]).map((u) => ({ id: u.id, icon, title: u.isActive ? u.displayName : t('common.inactiveSuffix', { name: u.displayName }),
    sub: [u.jobTitle, u.email].filter(Boolean).join(' · '), href: u.canViewWork ? `/my-work?userId=${u.id}` : null }))
  if (group === 'planning') return (list as Schemas['SearchPlanningHit'][]).map((x) => ({ id: x.id, icon, title: x.name, sub: [visibilityLabel(x.status), fmtDate(x.dueDate)].join(' · '), href: x.status === 'Self' ? `/my-work?myWeek=${x.startWeek ?? x.dueDate}&planningEntry=${x.id}` : `/planner?personId=${x.personId}&entry=${x.id}` }))
  if (group === 'comments') return (list as Schemas['SearchCommentHit'][]).map((c) => ({ id: c.id, icon, key: c.key, title: c.name, match: c.match, // FR-004: the comment's item, with the matching text
    sub: [c.projectNumber, c.author, fmtDate(c.createdAt?.slice(0, 10))].filter((v) => v && v !== '—').join(' · '), href: itemHref(c.itemType, c.projectNumber, c.itemId) }))
  return (list as Row[]).map((x) => ({ id: x.id, icon, key: x.key, title: x.name, match: x.match,
    sub: [x.projectNumber, x.status ? tv(x.status) : x.isComplete ? tv('Complete') : null, x.assignee ?? x.owner, fmtDate(x.dueDate ?? x.date ?? x.requiredByDate)].filter((v) => v && v !== '—').join(' · '),
    href: itemHref(TYPE[group], x.projectNumber, x.id) }))
}

type SearchQueryOptions = Omit<NonNullable<paths['/api/v1/search']['get']['parameters']['query']>, 'q' | 'type'> & { type?: Group }
export const searchQuery = (q: string, extra: SearchQueryOptions = {}) => typedRequest('get', '/api/v1/search', { query: { q, ...extra } })

/** Top-bar search (§18.1): top five per type as you type, arrow keys and Enter, "See all results" for the results page. */
export function SearchBox({ inputRef, onNavigate }: { inputRef: RefObject<HTMLInputElement | null>; onNavigate?: () => void }) {
  const phone = useIsPhone()
  const me = useMe(), people = useFlag('people_screens'), location = useLocation()
  const navigate = useNavigate()
  const listId = useId()
  const root = useRef<HTMLDivElement>(null)
  const blurTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const personOpen = useRef(false)
  personOpen.current = people && new URLSearchParams(location.search).get('panel')?.startsWith('Person:') === true
  const [q, setQ] = useState('')
  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const [activeKey, setActiveKey] = useState<string | null>(null)
  useEffect(() => () => clearTimeout(blurTimer.current), [])
  useEffect(() => { const h = setTimeout(() => setTerm(q.trim()), 200); return () => clearTimeout(h) }, [q])
  const res = useQuery({ queryKey: ['search', term, me.id, people], queryFn: () => searchQuery(term), enabled: term.length >= 2, staleTime: 30_000 })
  const sections = useMemo(() => GROUPS.map((g) => ({ g, hits: toHits(g, res.data?.groups[g]) })).filter((s) => s.hits.length > 0), [res.data])
  const flat = useMemo<Hit[]>(() => [...sections.flatMap((s) => s.hits), { id: 'all', icon: Search, title: t('search.seeAll', { q: term }), sub: '', href: `/search?q=${encodeURIComponent(term)}` }], [sections, term])
  const active = flat.findIndex(hit => hitKey(hit) === activeKey)
  const detailHits = people ? flat.filter(hit => hit.person?.id && hit.person.displayName && hit.group && hit.person.role === PERSON_ROLE[hit.group]) : []
  const detailHit = active >= 0 ? detailHits.find(hit => hitKey(hit) === activeKey) : detailHits[0]
  const move = (index: number) => setActiveKey(index < 0 ? null : hitKey(flat[index]))
  const go = (href: string) => { setOpen(false); setQ(''); setTerm(''); inputRef.current?.blur(); onNavigate?.(); navigate(href) }
  const onEnter = async () => {
    const value = q.trim()
    if (!value) return
    if (active >= 0 && flat[active]?.href) return go(flat[active].href!)
    const data = term === value && res.data ? res.data : await searchQuery(value).catch(() => null)
    if (data?.exact) return go(itemHref(data.exact.type, data.exact.projectNumber, data.exact.id))
    go(`/search?q=${encodeURIComponent(value)}`)
  }
  const show = open && term.length >= 2
  let n = -1
  return (
    <div ref={root} data-search-palette className="relative w-full max-w-md lg:w-72 2xl:w-[400px]" onBlur={() => {
      clearTimeout(blurTimer.current)
      blurTimer.current = setTimeout(() => {
        if (!personOpen.current && !root.current?.contains(document.activeElement)) setOpen(false)
      }, 150)
    }}>
      <label className="relative flex items-center">
        <Search aria-hidden className="pointer-events-none absolute left-3 size-4 text-muted-foreground" />
        <span className="sr-only">{t('common.search')}</span>
        <input ref={inputRef} type="search" placeholder={t(phone ? 'common.search' : 'top.search')} title={t('top.searchShortcut')} value={q} autoComplete="off"
          role="combobox" aria-expanded={show} aria-controls={listId} aria-autocomplete="list" aria-activedescendant={active >= 0 ? `${listId}-${active}` : undefined}
          onChange={(e) => { setQ(e.target.value); setOpen(true); setActiveKey(null) }} onFocus={() => setOpen(true)}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); move(Math.min(active + 1, flat.length - 1)) }
            else if (e.key === 'ArrowUp') { e.preventDefault(); move(Math.max(active - 1, -1)) }
            else if (e.key === 'Escape') { setOpen(false); setActiveKey(null) }
            else if (e.key === 'Enter') { e.preventDefault(); onEnter() }
          }}
          className="h-(--control-h) w-full rounded-md border border-input bg-card pl-9 pr-3 text-sm placeholder:text-placeholder focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring lg:pr-10" />
        <kbd aria-hidden className="pointer-events-none absolute right-2.5 hidden rounded border bg-muted px-1.5 font-mono text-xs text-muted-foreground lg:block">/</kbd>
      </label>
      {show && (
        <div data-search-popup className="absolute left-0 right-auto top-12 z-50 max-h-[70vh] w-[calc(100vw-2rem)] overflow-y-auto rounded-lg border bg-popover py-1 text-sm shadow-popover sm:w-[32rem] lg:left-auto lg:right-0">
          <div id={listId} role="listbox" aria-label={t('search.results')}>
          {res.isPending && <div className="px-3 py-2 text-muted-foreground">{t('search.searching')}</div>}
          {res.data && sections.length === 0 && <div className="px-3 py-2 text-muted-foreground">{t('search.none', { q: term })}</div>}
          {sections.map((s) => (
            <div key={s.g} role="group" aria-label={t(`search.group.${s.g}`)}>
              <div className="flex justify-between px-3 pb-0.5 pt-2 text-xs font-medium text-muted-foreground">
                <span>{t(`search.group.${s.g}`)}</span>{(res.data?.counts[s.g] ?? 0) > s.hits.length && <span>{t('search.ofTotal', { n: s.hits.length, total: res.data?.counts[s.g] })}</span>}
              </div>
              {s.hits.map((h) => { n++; return <Option key={hitKey(h)} h={h} id={`${listId}-${n}`} active={active === n} onPick={go} onActivate={people ? () => setActiveKey(hitKey(h)) : undefined} term={term} /> })}
            </div>
          ))}
          <div className="mt-1 border-t pt-1">{(() => { n++; return <Option h={flat[flat.length - 1]} id={`${listId}-${n}`} active={active === n} onPick={go} term={term} /> })()}</div>
          </div>
          {people && detailHits.length > 0 && <div data-search-person-actions className="sticky bottom-0 space-y-1 border-t bg-popover px-3 py-2">
            <select aria-label={t('search.results')} value={detailHit?.person ? hitKey(detailHit) : ''} onChange={event => setActiveKey(event.target.value)}
              className="min-h-9 w-full min-w-0 rounded border border-input bg-card px-2 text-xs">
              {!detailHit?.person && <option value="">{t('search.results')}</option>}
              {detailHits.map(hit => <option key={hitKey(hit)} value={hitKey(hit)}>{t(`search.group.${hit.group!}`)} · {hit.key ? `${hit.key} · ` : ''}{hit.title}</option>)}
            </select>
            {detailHit && <SearchPersonDetails hit={detailHit} surface="palette" />}
          </div>}
        </div>
      )}
    </div>
  )
}
/** The search words marked inside a snippet (FR-004). */
export function Highlight({ text, term }: { text: string; term: string }) {
  const q = term.trim()
  if (!q) return <>{text}</>
  const parts = text.split(new RegExp(`(${q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'ig'))
  return <>{parts.map((p, i) => (i % 2 === 1 ? <mark key={i} className="rounded bg-warn-bg px-0.5 text-foreground">{p}</mark> : p))}</>
}

function Option({ h, id, active, onPick, onActivate, term }: { h: Hit; id: string; active: boolean; onPick: (href: string) => void; onActivate?: () => void; term: string }) {
  const Icon = h.icon
  return (
    <div id={id} data-search-hit={hitKey(h)} role="option" tabIndex={-1} aria-selected={active} aria-disabled={!h.href} onPointerMove={event => { if (event.pointerType === 'mouse') onActivate?.() }} onMouseDown={(e) => { e.preventDefault(); if (h.href) onPick(h.href) }}
      className={cn('flex min-h-(--control-row-h) cursor-pointer items-center gap-2 px-3 py-1.5', active && 'bg-accent', !h.href && 'cursor-default text-muted-foreground')}>
      <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
      {h.key && <span className="key shrink-0 font-mono text-xs text-muted-foreground">{h.key}</span>}
      <span className="min-w-0 flex-1 truncate">{h.title}{h.match && <span className="block truncate text-xs text-muted-foreground"><Highlight text={h.match} term={term} /></span>}</span>
      {h.sub && <span className="hidden max-w-[45%] truncate text-xs text-muted-foreground sm:inline">{h.sub}</span>}
    </div>
  )
}
