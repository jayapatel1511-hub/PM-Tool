import { visibilityLabel } from '@/components/planner/labels'
import { useQuery } from '@tanstack/react-query'
import { Briefcase, CalendarCheck, CheckSquare, Flag, FileText, Link2Off, MessageSquare, Scale, Search, User, type LucideIcon } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type RefObject } from 'react'
import { useNavigate } from 'react-router'
import { ShellSlots } from '@/app/slots'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export const GROUPS = ['projects', 'tasks', 'deliverables', 'milestones', 'decisions', 'handoffs', 'reviews', 'changes', 'submissions', 'allocations', 'design-basis', 'constraints', 'commitments', 'planning', 'comments', 'people'] as const
export type Group = (typeof GROUPS)[number]
export interface SearchResult {
  q: string; exact: { type: string; id?: string | null; projectNumber: string; key: string } | null
  groups: Partial<Record<Group, any[]>>; counts: Partial<Record<Group, number>>
}
export interface Hit { id: string; icon: LucideIcon; key?: string; title: string; sub: string; href: string | null; match?: string | null }

/** Item keys such as 1234-T0042 open the item directly (§18.1, FR-SRCH-02). */
export const KEY_PATTERN = /^[A-Za-z0-9][\w-]*-(T|D|M|DEC|R|I|A|H|RV|CH|CT|WC|SUB|B)\d+$/i
const TAB: Record<string, string> = { Task: 'tasks', Deliverable: 'deliverables', Milestone: 'milestones', Decision: 'decisions', Risk: 'risks', Issue: 'issues', Action: 'meetings', Handoff: 'handoffs', ReviewPackage: 'reviews', ChangeNotice: 'changes',
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

export function toHits(group: Group, rows: any[] = []): Hit[] {
  const icon = ICON[group]
  if (group === 'projects') return rows.map((p) => ({ id: p.id, icon, key: p.projectNumber, title: p.name, sub: [p.client, p.pm, tv(p.status)].filter(Boolean).join(' · '), href: itemHref('Project', p.projectNumber) }))
  if (group === 'people') return rows.map((u) => ({ id: u.id, icon, title: u.isActive ? u.displayName : t('common.inactiveSuffix', { name: u.displayName }),
    sub: [u.jobTitle, u.email].filter(Boolean).join(' · '), href: u.canViewWork ? `/my-work?userId=${u.id}` : null }))
  if (group === 'planning') return rows.map((x) => ({ id: x.id, icon, title: x.name, sub: [visibilityLabel(x.status), fmtDate(x.dueDate)].join(' · '), href: x.status === 'Self' ? `/my-work?myWeek=${x.startWeek ?? x.dueDate}&planningEntry=${x.id}` : `/planner?personId=${x.personId}&entry=${x.id}` }))
  if (group === 'comments') return rows.map((c) => ({ id: c.id, icon, key: c.key, title: c.name, match: c.match, // FR-004: the comment's item, with the matching text
    sub: [c.projectNumber, c.author, fmtDate(c.createdAt?.slice(0, 10))].filter((v) => v && v !== '—').join(' · '), href: itemHref(c.itemType, c.projectNumber, c.itemId) }))
  return rows.map((x) => ({ id: x.id, icon, key: x.key, title: x.name, match: x.match,
    sub: [x.projectNumber, x.status ? tv(x.status) : x.isComplete ? tv('Complete') : null, x.assignee ?? x.owner, fmtDate(x.dueDate ?? x.date ?? x.requiredByDate)].filter((v) => v && v !== '—').join(' · '),
    href: itemHref(TYPE[group], x.projectNumber, x.id) }))
}

export const searchQuery = (q: string, extra: Record<string, unknown> = {}) => get<SearchResult>(`search${qs({ q, ...extra })}`)

/** Top-bar search (§18.1): top five per type as you type, arrow keys and Enter, "See all results" for the results page. */
function SearchBox({ inputRef }: { inputRef: RefObject<HTMLInputElement | null> }) {
  const navigate = useNavigate()
  const listId = useId()
  const [q, setQ] = useState('')
  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(-1)
  useEffect(() => { const h = setTimeout(() => setTerm(q.trim()), 200); return () => clearTimeout(h) }, [q])
  const res = useQuery({ queryKey: ['search', term], queryFn: () => searchQuery(term), enabled: term.length >= 2, staleTime: 30_000 })
  const sections = useMemo(() => GROUPS.map((g) => ({ g, hits: toHits(g, res.data?.groups[g]) })).filter((s) => s.hits.length > 0), [res.data])
  const flat = useMemo(() => [...sections.flatMap((s) => s.hits), { id: 'all', icon: Search, title: t('search.seeAll', { q: term }), sub: '', href: `/search?q=${encodeURIComponent(term)}` }], [sections, term])
  const go = (href: string) => { setOpen(false); setQ(''); setTerm(''); inputRef.current?.blur(); navigate(href) }
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
    <div className="relative w-full max-w-md lg:w-72 2xl:w-[400px]">
      <label className="relative flex items-center">
        <Search aria-hidden className="pointer-events-none absolute left-3 size-4 text-muted-foreground" />
        <span className="sr-only">{t('common.search')}</span>
        <input ref={inputRef} type="search" placeholder={t('top.search')} title={t('top.searchShortcut')} value={q} autoComplete="off"
          role="combobox" aria-expanded={show} aria-controls={listId} aria-autocomplete="list" aria-activedescendant={active >= 0 ? `${listId}-${active}` : undefined}
          onChange={(e) => { setQ(e.target.value); setOpen(true); setActive(-1) }} onFocus={() => setOpen(true)} onBlur={() => setTimeout(() => setOpen(false), 150)}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); setActive((i) => Math.min(i + 1, flat.length - 1)) }
            else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, -1)) }
            else if (e.key === 'Escape') { setOpen(false); setActive(-1) }
            else if (e.key === 'Enter') { e.preventDefault(); onEnter() }
          }}
          className="h-(--control-h) w-full rounded-md border border-input bg-card pl-9 pr-10 text-sm placeholder:text-placeholder focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring" />
        <kbd aria-hidden className="pointer-events-none absolute right-2.5 hidden rounded border bg-muted px-1.5 font-mono text-xs text-muted-foreground lg:block">/</kbd>
      </label>
      {show && (
        <div id={listId} role="listbox" aria-label={t('search.results')} className="absolute left-0 right-0 top-12 z-50 max-h-[70vh] overflow-y-auto rounded-lg border bg-popover py-1 text-sm shadow-popover sm:right-auto sm:w-[32rem] lg:left-auto lg:right-0">
          {res.isPending && <div className="px-3 py-2 text-muted-foreground">{t('search.searching')}</div>}
          {res.data && sections.length === 0 && <div className="px-3 py-2 text-muted-foreground">{t('search.none', { q: term })}</div>}
          {sections.map((s) => (
            <div key={s.g} role="group" aria-label={t(`search.group.${s.g}`)}>
              <div className="flex justify-between px-3 pb-0.5 pt-2 text-xs font-medium text-muted-foreground">
                <span>{t(`search.group.${s.g}`)}</span>{(res.data?.counts[s.g] ?? 0) > s.hits.length && <span>{t('search.ofTotal', { n: s.hits.length, total: res.data?.counts[s.g] })}</span>}
              </div>
              {s.hits.map((h) => { n++; return <Option key={h.id} h={h} id={`${listId}-${n}`} active={active === n} onPick={go} term={term} /> })}
            </div>
          ))}
          <div className="mt-1 border-t pt-1">{(() => { n++; return <Option h={flat[flat.length - 1]} id={`${listId}-${n}`} active={active === n} onPick={go} term={term} /> })()}</div>
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

function Option({ h, id, active, onPick, term }: { h: Hit; id: string; active: boolean; onPick: (href: string) => void; term: string }) {
  const Icon = h.icon
  return (
    <div id={id} role="option" tabIndex={-1} aria-selected={active} aria-disabled={!h.href} onMouseDown={(e) => { e.preventDefault(); if (h.href) onPick(h.href) }}
      className={cn('flex min-h-(--control-row-h) cursor-pointer items-center gap-2 px-3 py-1.5', active && 'bg-accent', !h.href && 'cursor-default text-muted-foreground')}>
      <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
      {h.key && <span className="key shrink-0 font-mono text-xs text-muted-foreground">{h.key}</span>}
      <span className="min-w-0 flex-1 truncate">{h.title}{h.match && <span className="block truncate text-xs text-muted-foreground"><Highlight text={h.match} term={term} /></span>}</span>
      {h.sub && <span className="hidden max-w-[45%] truncate text-xs text-muted-foreground sm:inline">{h.sub}</span>}
    </div>
  )
}

ShellSlots.Search = SearchBox
