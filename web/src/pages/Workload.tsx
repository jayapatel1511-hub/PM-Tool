import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarDays, ChevronDown, ChevronLeft, ChevronRight, EyeOff, Info, Lock, Monitor, Pencil } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, ActiveFilters, ChipToggle, ConfirmDialog, DensityToggle, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Missing, Notice, Page, Spinner, selectCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Chip, Key, StatusPill } from '@/components/hub/pills'
import { itemHref } from '@/components/hub/search'
import { ViewMenu } from '@/components/hub/views'
import { WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Skeleton } from '@/components/ui/skeleton'
import { useReference } from '@/hooks/data'
import { ApiError, get, patch, put, qs } from '@/lib/api'
import { addDays, fmtDate, hours, shortDate, today } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'
import { errorText } from './projects/Tasks'

interface Cell { week: string; hours: number; pct: number; available: number; confirmed: number; proposed: number; committed: number }
interface PersonRow {
  id: string; displayName: string; supervisorId?: string; supervisorName?: string; capacity: number; capacityOverride: boolean; cells: Cell[]; noDueDate: number
  openTasks: number; unestimated: number; overdue: number; projects: number; overAssigned: boolean; underAssigned: boolean; cluster: boolean; canSetCapacity: boolean; partialScope: boolean; indicator: string
}
interface Grid { weeks: string[]; today: string; currentWeek: string; defaultCapacity: number; people: PersonRow[] }
interface TaskLoadRow { id: string; key: string; name: string; status: string; dueDate?: string; estimatedHours?: number | null; progressPct: number; remaining?: number | null; overdue: boolean; noDueDate: number; rowVersion: number; weeks: number[]; canReassign: boolean }
interface PersonTasks { weeks: string[]; projects: { id: string; projectNumber: string; name: string; tasks: TaskLoadRow[] }[] }

const FILTERS = ['supervisorId', 'disciplineId', 'officeId', 'projectId', 'indicator', 'from', 'sort'] as const
const INDICATORS = ['over', 'under', 'cluster', 'unestimated'] as const

/** Heat shading by load with the number always shown (§13.11): light under 40 %, calm to 90 %, amber to 110 %, red above. */
const loadTone = (pct: number) => (pct > 110 ? 'bad' : pct > 90 ? 'warn' : pct >= 40 ? 'ok' : 'idle')
const SHADE = { bad: 'bg-bad-bg', warn: 'bg-warn-bg', ok: 'bg-ok-bg/50', idle: '' }
const METER = { bad: 'bg-bad', warn: 'bg-warn', ok: 'bg-ok', idle: 'bg-ok' }

/** Resource and Workload View (§12.15, §13.11, FR-CAP-04/06): per person and week, committed load against available
 *  capacity, with confirmed reservations, proposed requests and the remaining-work forecast kept apart. */
export function WorkloadPage() {
  const header = { eyebrow: t('nav.resources'), title: t('nav.workload'), subtitle: t('workload.subtitle') }
  return (
    <DesktopOnly notice={<Page {...header}><Notice icon={Monitor} title={t('app.phoneNotice')}
      action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('workload.phoneHint')}</Notice></Page>}>
      <Workload {...header} />
    </DesktopOnly>
  )
}

function Workload(header: { eyebrow: string; title: string; subtitle: string }) {
  const ref = useReference()
  const [sp, setSp] = useSearchParams()
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [capacity, setCapacity] = useState<PersonRow | null>(null)
  const [availability, setAvailability] = useState<PersonRow | null>(null)
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)]))
  const q = useQuery({ queryKey: ['workload', filters], queryFn: () => get<Grid>(`workload${qs(filters)}`) })
  const projects = useQuery({ queryKey: ['projects-pick', ''], queryFn: () => get<PageOf<{ id: string; projectNumber: string; name: string }>>('projects?pageSize=200') })
  const forbidden = (q.error as ApiError | null)?.status === 403
  const toggle = (id: string) => setOpen((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })
  const groups = new Map<string, PersonRow[]>()
  for (const p of q.data?.people ?? []) { const g = p.supervisorName ? t('workload.reportsTo', { name: p.supervisorName }) : t('workload.noSupervisor'); groups.set(g, [...(groups.get(g) ?? []), p]) }
  const people = q.data?.people ?? []
  const allPartial = people.length > 0 && people.every((p) => p.partialScope)

  // Active filters as removable tokens (§13.0 Filters); sort is an order, not a filter.
  const supervisorName = people.find((p) => p.supervisorId === filters.supervisorId)?.supervisorName
  const tokens = ([
    ['supervisorId', t('workload.supervisor'), supervisorName ?? t('workload.selectedSupervisor')],
    ['disciplineId', t('common.discipline'), ref.data?.disciplines.find((d) => d.id === filters.disciplineId)?.name],
    ['officeId', t('admin.office'), ref.data?.offices.find((d) => d.id === filters.officeId)?.name],
    ['projectId', t('portfolio.project'), projects.data?.items.find((p) => p.id === filters.projectId)?.projectNumber],
    ['indicator', t('workload.indicator'), filters.indicator ? t(`workload.ind.${filters.indicator}`) : null],
    ['from', t('workload.from'), filters.from ? fmtDate(filters.from) : null],
  ] as const).filter(([k]) => filters[k])
  const clear = () => { const n = new URLSearchParams(sp); for (const [k] of tokens) n.delete(k); setSp(n, { replace: true }) }
  const weeks = q.data?.weeks ?? []
  const shiftWeeks = (days: number) => set('from', addDays(weeks[0] ?? today(), days))

  return (
    <Page {...header} actions={forbidden ? <Method /> : <><Method /><ViewMenu listType="workload" /><ExportMenu path="workload/export" params={filters} name="workload" /></>}>
      <WorkspaceTabs />
      {forbidden ? <Notice icon={Lock} title={t('workload.noAccessTitle')}
        action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('workload.noAccess')}</Notice> : <>
        <FilterBar>
          <div className="flex flex-wrap items-end gap-3">
            <Field label={t('workload.supervisor')} htmlFor="wl-supervisor" className="w-full sm:w-56">
              <PeoplePicker id="wl-supervisor" value={filters.supervisorId} valueName={supervisorName} onChange={(v) => set('supervisorId', v)} placeholder={t('workload.anySupervisor')} />
            </Field>
            <Field label={t('common.discipline')} htmlFor="wl-discipline" className="w-full sm:w-48">
              <select id="wl-discipline" className={selectCls} value={filters.disciplineId ?? ''} onChange={(e) => set('disciplineId', e.target.value)}>
                <option value="">{t('projects.anyDiscipline')}</option>{ref.data?.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </Field>
            <Field label={t('admin.office')} htmlFor="wl-office" className="w-full sm:w-44">
              <select id="wl-office" className={selectCls} value={filters.officeId ?? ''} onChange={(e) => set('officeId', e.target.value)}>
                <option value="">{t('reports.anyOffice')}</option>{ref.data?.offices.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </Field>
            <Field label={t('portfolio.project')} htmlFor="wl-project" className="w-full sm:w-64">
              <select id="wl-project" className={selectCls} value={filters.projectId ?? ''} onChange={(e) => set('projectId', e.target.value)}>
                <option value="">{t('workload.anyProject')}</option>{projects.data?.items.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
              </select>
            </Field>
            <Field label={t('common.sort')} htmlFor="wl-sort" className="w-full sm:w-48">
              <select id="wl-sort" className={selectCls} value={filters.sort ?? ''} onChange={(e) => set('sort', e.target.value)}>
                {[['', 'workload.sort.load'], ['overdue', 'workload.sort.overdue'], ['tasks', 'workload.sort.tasks'], ['name', 'workload.sort.name']].map(([v, l]) => <option key={v} value={v}>{t(l)}</option>)}
              </select>
            </Field>
          </div>
          <div className="flex flex-wrap items-center gap-2" role="group" aria-labelledby="wl-indicator">
            <span id="wl-indicator" className="mr-1 text-sm font-medium">{t('workload.indicator')}</span>
            {['', ...INDICATORS].map((x) => (
              <ChipToggle key={x || 'any'} on={(filters.indicator ?? '') === x} onClick={() => set('indicator', x)}>{x ? t(`workload.ind.${x}`) : t('workload.anyIndicator')}</ChipToggle>
            ))}
          </div>
          <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={(k) => set(k, null)} onClear={clear} />
        </FilterBar>

        <div className="flex flex-wrap items-center justify-between gap-3">
          <div role="group" aria-label={t('workload.weeks')} className="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="icon" aria-label={t('workload.prevWeek')} onClick={() => shiftWeeks(-7)}><ChevronLeft className="size-5" /></Button>
            <span className="min-w-40 text-center text-sm font-semibold tabular-nums" aria-live="polite">
              {weeks.length ? t('workload.range', { from: shortDate(weeks[0]), to: shortDate(addDays(weeks[weeks.length - 1], 6)) }) : t('common.dash')}
            </span>
            <Button variant="outline" size="icon" aria-label={t('workload.nextWeek')} onClick={() => shiftWeeks(7)}><ChevronRight className="size-5" /></Button>
            <Button variant="outline" disabled={!filters.from} onClick={() => set('from', null)}>{t('workload.thisWeek')}</Button>
            <label className="flex items-center gap-2 text-sm text-muted-foreground">{t('workload.from')}
              <Input type="date" className="w-40" value={filters.from ?? ''} onChange={(e) => set('from', e.target.value)} /></label>
          </div>
          <DensityToggle />
        </div>

        {filters.disciplineId && <p className="text-sm text-muted-foreground">{t('workload.disciplineScope')}</p>}
        {allPartial && <Notice icon={EyeOff} title={t('workload.partialTitle')}>{t('workload.partialAll')}</Notice>}
        {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
        <Legend />
        {q.isPending ? <GridSkeleton /> : !q.data ? null : q.data.people.length === 0 ? (
          <div className="rounded-lg border bg-card">
            <Empty title={t('workload.emptyTitle')} action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('workload.empty')}</Empty>
          </div>
        ) : (
          // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- axe scrollable-region-focusable: keyboard users scroll the grid
          <div className="scroll-region max-h-[78dvh] overflow-auto rounded-lg border bg-card" role="region" aria-label={t('workload.grid')} tabIndex={0}>
            <table className="w-full min-w-max border-separate border-spacing-0 text-sm">
              <caption className="sr-only">{t('workload.gridCaption')}</caption>
              <thead>
                <tr>
                  <th scope="col" className="sticky left-0 top-0 z-30 w-60 min-w-60 border-b border-r bg-muted px-4 py-3 text-left font-medium text-muted-foreground">{t('workload.person')}</th>
                  {q.data.weeks.map((w) => {
                    const current = w === q.data!.currentWeek
                    return (
                      <th key={w} scope="col" aria-current={current ? 'date' : undefined}
                        className={cn('sticky top-0 z-20 min-w-36 border-b border-r px-3 py-3 text-left font-medium', current ? 'bg-accent text-accent-foreground' : 'bg-muted text-muted-foreground')}>
                        <span className="block text-xs/[18px]">{current ? t('workload.thisWeek') : t('workload.weekOfLabel')}</span>
                        <span className="text-sm font-semibold text-foreground">{shortDate(w)}</span>
                      </th>
                    )
                  })}
                  <th scope="col" className="sticky top-0 z-20 min-w-56 border-b bg-muted px-4 py-3 text-left font-medium text-muted-foreground">{t('workload.summary')}</th>
                </tr>
              </thead>
              {[...groups.entries()].map(([g, list]) => (
                <tbody key={g}>
                  {groups.size > 1 && (
                    <tr>
                      <th scope="rowgroup" className="sticky left-0 z-10 border-b border-r bg-sidebar px-4 py-2 text-left text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
                        {g} · {list.length}
                      </th>
                      <td colSpan={q.data!.weeks.length + 1} className="border-b bg-sidebar" />
                    </tr>
                  )}
                  {list.map((p) => (
                    <Fragment key={p.id}>
                      <tr>
                        <th scope="row" className="sticky left-0 z-10 w-60 min-w-60 border-b border-r bg-card px-3 py-3 text-left align-top font-normal">
                          <div className="flex items-start gap-2">
                            <Button variant="ghost" size="icon-sm" aria-expanded={open.has(p.id)} aria-label={t('workload.showTasks', { name: p.displayName })} onClick={() => toggle(p.id)}>
                              {open.has(p.id) ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                            </Button>
                            <Avatar id={p.id} name={p.displayName} className="mt-0.5" />
                            <div className="min-w-0 flex-1">
                              <div className="break-words font-semibold">{p.displayName}</div>
                              <div className="text-xs/[18px] text-muted-foreground">
                                {t('workload.capacityPerWeek', { h: hours(p.capacity) })}{p.capacityOverride && ` · ${t('workload.custom')}`}
                              </div>
                              {p.partialScope && !allPartial && <span className="mt-1 inline-flex items-center gap-1 text-xs/[18px] text-muted-foreground" title={t('workload.partial')}><EyeOff className="size-3.5" aria-hidden />{t('workload.partialChip')}</span>}
                              {(p.overAssigned || p.underAssigned || p.cluster || p.unestimated > 0) && (
                                <div className="mt-1.5 flex flex-wrap gap-1">
                                  {p.overAssigned && <Chip tone="bad">{t('workload.ind.over')}</Chip>}
                                  {p.underAssigned && <Chip tone="idle">{t('workload.ind.under')}</Chip>}
                                  {p.cluster && <Chip tone="warn">{t('workload.ind.cluster')}</Chip>}
                                  {p.unestimated > 0 && <Chip tone="warn">{t('workload.unestimatedN', { n: p.unestimated })}</Chip>}
                                </div>
                              )}
                              {p.canSetCapacity && (
                                <div className="mt-1 flex gap-1">
                                  <Button variant="ghost" size="icon-sm" title={t('workload.setCapacity', { name: p.displayName })} aria-label={t('workload.setCapacity', { name: p.displayName })} onClick={() => setCapacity(p)}><Pencil className="size-4" /></Button>
                                  <Button variant="ghost" size="icon-sm" title={t('workload.setDayAvailability', { name: p.displayName })} aria-label={t('workload.setDayAvailability', { name: p.displayName })} onClick={() => setAvailability(p)}><CalendarDays className="size-4" /></Button>
                                </div>
                              )}
                            </div>
                          </div>
                        </th>
                        {p.cells.map((c) => <td key={c.week} className="border-b border-r p-0 align-top"><WeekCell c={c} capacity={p.capacity} partial={p.partialScope} /></td>)}
                        <td className="border-b px-4 py-3 align-top"><Summary p={p} /></td>
                      </tr>
                      {open.has(p.id) && <PersonTasksRows id={p.id} name={p.displayName} from={filters.from} weeks={q.data!.weeks.length} onChanged={() => q.refetch()} />}
                    </Fragment>
                  ))}
                </tbody>
              ))}
            </table>
          </div>
        )}
      </>}
      {capacity && <CapacityDialog p={capacity} defaultCapacity={q.data?.defaultCapacity ?? 40} onClose={(ok) => { setCapacity(null); if (ok) q.refetch() }} />}
      {availability && <AvailabilityDialog p={availability} onClose={(ok) => { setAvailability(null); if (ok) q.refetch() }} />}
    </Page>
  )
}

/** One person-week: committed load of available capacity, then what it is made of. Proposed requests sit outside the
 *  load (dotted), and a partial view never claims spare capacity (FR-CAP-04, FR-CAP-06). */
function WeekCell({ c, capacity, partial }: { c: Cell; capacity: number; partial: boolean }) {
  const tone = loadTone(c.pct)
  const remaining = c.available - c.committed
  const scale = Math.max(c.available, c.committed + c.proposed, 1)
  return (
    <div className={cn('flex h-full min-h-28 flex-col gap-1.5 px-(--cell-px) py-(--cell-py)', SHADE[tone])}>
      <p className="tabular-nums">
        <span className="text-lg/6 font-semibold">{hours(c.committed)}</span>
        <span className="text-xs text-muted-foreground"> <span aria-hidden>/</span><span className="sr-only">{t('workload.ofAvailable')}</span> {hours(c.available)}</span>
      </p>
      <div aria-hidden className="relative h-1.5 overflow-hidden rounded-full bg-border">
        <span data-meter-fill className={cn('absolute inset-y-0 left-0 rounded-full', METER[tone])} style={{ width: `${Math.min(c.committed, scale) / scale * 100}%` }} />
        {c.proposed > 0 && <span className="absolute inset-y-0 rounded-full border border-dotted border-work bg-work-bg" style={{ left: `${c.committed / scale * 100}%`, width: `${c.proposed / scale * 100}%` }} />}
        {c.available < scale && <span className="absolute inset-y-[-2px] w-0.5 bg-foreground" style={{ left: `${c.available / scale * 100}%` }} />}
      </div>
      <p className="text-xs/[18px] tabular-nums">
        <span className="text-muted-foreground">{c.available <= 0 ? t('workload.noAvailability') : t('workload.loadPct', { pct: c.pct })}</span>
        {remaining < 0 ? <span className={cn('ml-1.5 font-semibold', tone === 'bad' ? 'text-bad' : 'text-warn')}><span aria-hidden>{tone === 'bad' ? '■' : '▲'} </span>{t('workload.remaining', { h: hours(remaining) })}</span>
          : !partial && <span className="ml-1.5 text-muted-foreground">{t('workload.remaining', { h: hours(remaining) })}</span>}
      </p>
      {(c.confirmed > 0 || c.hours > 0) && (
        <p className="text-xs/[18px] text-muted-foreground tabular-nums">{[c.confirmed > 0 && t('workload.reserved', { h: hours(c.confirmed) }), c.hours > 0 && t('workload.forecast', { h: hours(c.hours) })].filter(Boolean).join(' · ')}</p>
      )}
      {c.proposed > 0 && <p className="w-fit rounded-md border border-dashed border-work bg-work-bg px-1.5 text-xs/[18px] text-work tabular-nums">{t('workload.proposedNotCounted', { h: hours(c.proposed) })}</p>}
      {c.available !== capacity && c.available > 0 && (
        <p className="text-xs/[18px] text-muted-foreground" title={t('workload.availabilityTitle', { a: hours(c.available), c: hours(capacity) })}>
          <span aria-hidden>{c.available < capacity ? '▾ ' : '▴ '}</span>{t(c.available < capacity ? 'workload.reducedAvailability' : 'workload.extraAvailability')}
        </p>
      )}
    </div>
  )
}

function Summary({ p }: { p: PersonRow }) {
  return (
    <div className="space-y-2 text-xs/[18px]">
      {!p.overAssigned && !p.underAssigned && <Chip tone="ok">{t('workload.ind.ok')}</Chip>}
      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 tabular-nums">
        <dt className="text-muted-foreground">{t('workload.sum.open')}</dt><dd>{t('workload.sum.openValue', { n: p.openTasks, projects: p.projects })}</dd>
        <dt className="text-muted-foreground">{t('ind.overdue')}</dt><dd className={cn(p.overdue > 0 && 'font-semibold text-bad')}>{p.overdue}</dd>
        <dt className="text-muted-foreground">{t('workload.unestimated')}</dt><dd className={cn(p.unestimated > 0 && 'font-semibold text-warn')}>{p.unestimated}</dd>
        {p.noDueDate > 0 && <><dt className="text-muted-foreground">{t('workload.sum.noDue')}</dt><dd>{hours(p.noDueDate)}</dd></>}
      </dl>
    </div>
  )
}

function Legend() {
  return (
    <ul aria-label={t('workload.legend')} className="flex flex-wrap gap-x-5 gap-y-1.5 text-xs/[18px] text-muted-foreground">
      <li className="flex items-center gap-1.5"><span aria-hidden className="h-1.5 w-6 rounded-full bg-ok" />{t('workload.legend.committed')}</li>
      <li className="flex items-center gap-1.5"><span aria-hidden className="h-1.5 w-6 rounded-full border border-dotted border-work bg-work-bg" />{t('workload.legend.proposed')}</li>
      <li className="flex items-center gap-1.5"><span aria-hidden className="h-3 w-0.5 bg-foreground" />{t('workload.legend.available')}</li>
      <li className="flex items-center gap-1.5"><span aria-hidden>■ ▲</span>{t('workload.legend.over')}</li>
    </ul>
  )
}

/** Skeleton rows keep the grid's shape while it loads. */
function GridSkeleton() {
  return (
    <div role="status" aria-label={t('app.loading')} className="overflow-hidden rounded-lg border bg-card">
      {Array.from({ length: 5 }, (_, r) => (
        <div key={r} className="flex gap-3 border-b p-3 last:border-b-0">
          <Skeleton className="h-12 w-56 shrink-0" />
          {Array.from({ length: 6 }, (_, c) => <Skeleton key={c} className="h-12 min-w-28 flex-1" />)}
        </div>
      ))}
    </div>
  )
}

type RowState = { status: 'saving' } | { status: 'failed'; error: unknown }

/** Person → Project → tasks, aligned with the week columns (§13.11), with reassignment where the viewer may (FR-006). */
function PersonTasksRows({ id, name, from, weeks, onChanged }: { id: string; name: string; from?: string | null; weeks: number; onChanged: () => void }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['workload', id, 'tasks', from], queryFn: () => get<PersonTasks>(`workload/${id}/tasks${qs({ from })}`) })
  const [state, setState] = useState<Record<string, RowState>>({})
  const forget = (taskId: string) => setState((s) => { const n = { ...s }; delete n[taskId]; return n })
  const row = (content: React.ReactNode) => <tr><td colSpan={weeks + 2} className="border-b bg-muted px-4 py-3"><div className="sticky left-4 w-fit max-w-[calc(100vw-8rem)]">{content}</div></td></tr>
  if (q.isPending) return row(<span className="flex items-center gap-2 text-sm text-muted-foreground"><Spinner />{t('workload.loadingTasks', { name })}</span>)
  if (q.error) return row(<ErrorBanner error={q.error} retry={() => q.refetch()} />)
  if (!q.data.projects.length) return row(<span className="text-sm text-muted-foreground">{t('workload.noTasks')}</span>)
  const reassign = async (task: TaskLoadRow, to: string | null) => {
    if (!to) return
    setState((s) => ({ ...s, [task.id]: { status: 'saving' } }))
    try {
      await patch(`tasks/${task.id}`, { assigneeId: to }, task.rowVersion)
      toast.success(t('workload.reassigned', { key: task.key }))
      forget(task.id)
      qc.invalidateQueries({ queryKey: ['workload'] }); onChanged()
    } catch (e) { setState((s) => ({ ...s, [task.id]: { status: 'failed', error: e } })) }
  }
  return (
    <>
      {q.data.projects.map((p) => (
        <Fragment key={p.id}>
          <tr>
            <th scope="rowgroup" colSpan={weeks + 2} className="border-b bg-muted px-0 py-2 text-left font-normal">
              <div className="sticky left-4 w-fit max-w-[min(60rem,calc(100vw-22rem))] px-4">
                <Link className="flex items-start gap-2 font-semibold hover:underline" to={`/projects/${encodeURIComponent(p.projectNumber)}/dashboard`}>
                  <AccentDot id={p.id} className="mt-1.5" />
                  <span className="shrink-0 whitespace-nowrap"><Key>{p.projectNumber}</Key></span><span className="break-words">{p.name}</span>
                </Link>
              </div>
            </th>
          </tr>
          {p.tasks.map((tk) => {
            const st = state[tk.id]
            return (
              <tr key={tk.id}>
                <th scope="row" className="sticky left-0 z-10 border-b border-r bg-card py-2.5 pl-10 pr-3 text-left align-top font-normal">
                  <Link className="hover:underline" to={itemHref('Task', p.projectNumber, tk.id)}><span className="whitespace-nowrap"><Key>{tk.key}</Key></span> <span className="break-words">{tk.name}</span></Link>
                  <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs/[18px] text-muted-foreground">
                    <StatusPill status={tk.status} />
                    <span>{tk.dueDate ? t('workload.taskDue', { date: fmtDate(tk.dueDate) }) : t('workload.sum.noDue')}</span>
                    <span>{t('workload.estimate')} {tk.estimatedHours == null ? <Missing /> : hours(tk.estimatedHours)}</span>
                    <span>{t('workload.left')} {tk.remaining == null ? <Missing /> : hours(tk.remaining)}</span>
                    {tk.overdue && <Chip tone="bad">{t('ind.overdue')}</Chip>}
                    {tk.estimatedHours == null && <Chip tone="warn">{t('workload.unestimated')}</Chip>}
                  </div>
                </th>
                {tk.weeks.map((h, i) => (
                  <td key={i} className="border-b border-r px-3 py-2.5 text-right align-top tabular-nums text-muted-foreground">
                    {h ? hours(h) : <span className="sr-only">{hours(0)}</span>}
                  </td>
                ))}
                <td className="border-b px-4 py-2 align-top">
                  {tk.canReassign ? (
                    <div className="w-52 space-y-1">
                      <PeoplePicker value={null} onChange={(v) => reassign(tk, v)} disabled={st?.status === 'saving'} placeholder={t('workload.reassignTo')} label={t('workload.reassignTask', { key: tk.key })} />
                      <div aria-live="polite" className="text-xs/[18px]">
                        {st?.status === 'saving' && <span className="flex items-center gap-1.5 text-muted-foreground"><Spinner className="size-3.5" />{t('workload.reassigning')}</span>}
                        {st?.status === 'failed' && <span role="alert" className="text-bad">
                          {t('workload.reassignFailed')}: {errorText(st.error)}
                          {(st.error as ApiError)?.code === 'concurrency_conflict' && <Button variant="link" size="xs" onClick={() => { forget(tk.id); q.refetch() }}>{t('error.reload')}</Button>}
                        </span>}
                      </div>
                    </div>
                  ) : <span className="inline-flex items-center gap-1.5 text-xs/[18px] text-muted-foreground"><Lock className="size-3.5" aria-hidden />{t('workload.cannotReassign')}</span>}
                </td>
              </tr>
            )
          })}
        </Fragment>
      ))}
    </>
  )
}

function AvailabilityDialog({ p, onClose }: { p: PersonRow; onClose: (ok: boolean) => void }) {
  const [date, setDate] = useState(today())
  const [hoursValue, setHours] = useState('')
  const [category, setCategory] = useState('Reduced')
  const q = useQuery({ queryKey: ['availability', p.id, date], queryFn: () => get<{ workDate: string; availableHours: number; category: string; rowVersion: number }[]>(
    `users/${p.id}/availability${qs({ from: date, through: date })}`), enabled: !!date })
  const existing = q.data?.[0]
  const invalid = hoursValue !== '' && !(Number(hoursValue) >= 0 && Number(hoursValue) <= 24)
  const chooseDate = (value: string) => { setDate(value); setHours(''); setCategory('Reduced') }
  return <ConfirmDialog open onOpenChange={o => !o && onClose(false)} title={t('workload.dayAvailabilityTitle', { name: p.displayName })}
    confirmLabel={t('common.save')} body={t('workload.dayAvailabilityHint')} busy={invalid}
    onConfirm={async () => {
      if (!date || hoursValue === '') throw new Error(t('workload.dayRequired'))
      await put(`users/${p.id}/availability/${date}`, { expectedRowVersion: existing?.rowVersion ?? 0,
        availableHours: Number(hoursValue), category })
      toast.success(t('common.saved')); onClose(true)
    }}>
    <Field label={t('workload.dayDate')} htmlFor="availability-date"><Input id="availability-date" type="date" required value={date} onChange={e => chooseDate(e.target.value)} /></Field>
    {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
    {q.isPending ? <p className="flex items-center gap-2 text-sm text-muted-foreground"><Spinner />{t('app.loading')}</p>
      : existing && hoursValue === '' && <Notice icon={Info} title={t('workload.existingAvailability', { h: existing.availableHours, category: t(`workload.availability.${existing.category}`) })} />}
    <Field label={t('workload.dayHours')} htmlFor="availability-hours" hint={t('workload.dayHoursHint')} error={invalid ? t('workload.dayHoursRange') : undefined}>
      <div className="flex items-center gap-2"><Input id="availability-hours" className="w-32" type="number" min={0} max={24} step={0.25} required value={hoursValue} aria-invalid={invalid || undefined} onChange={e => setHours(e.target.value)} /><span className="text-sm text-muted-foreground">h</span></div>
    </Field>
    <Field label={t('workload.dayCategory')} htmlFor="availability-category"><select id="availability-category" className={selectCls} value={category} onChange={e => setCategory(e.target.value)}>
      {['Unavailable', 'Reduced', 'Additional'].map(value => <option key={value} value={value}>{t(`workload.availability.${value}`)}</option>)}
    </select></Field>
  </ConfirmDialog>
}

function CapacityDialog({ p, defaultCapacity, onClose }: { p: PersonRow; defaultCapacity: number; onClose: (ok: boolean) => void }) {
  const [value, setValue] = useState(p.capacityOverride ? String(p.capacity) : '')
  const invalid = value !== '' && !(Number(value) >= 0 && Number(value) <= 80) // the API's limit (Workload.cs)
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('workload.capacityTitle', { name: p.displayName })} confirmLabel={t('common.save')}
      body={t('workload.capacityHint', { h: defaultCapacity })} busy={invalid}
      onConfirm={async () => { await put(`users/${p.id}/capacity`, { hours: value === '' ? null : Number(value) }); toast.success(t('common.saved')); onClose(true) }}>
      <Field label={t('workload.hoursPerWeek')} htmlFor="cap-h" optional error={invalid ? t('workload.capacityRange') : undefined}>
        <div className="flex items-center gap-2"><Input id="cap-h" className="w-32" type="number" min={0} max={80} step={0.5} value={value} placeholder={String(defaultCapacity)} aria-invalid={invalid || undefined} onChange={(e) => setValue(e.target.value)} /><span className="text-sm text-muted-foreground">{t('workload.hoursUnit')}</span></div>
      </Field>
    </ConfirmDialog>
  )
}

/** The method in plain words (§12.15 step 4), so nobody over-reads the numbers. */
function Method() {
  return (
    <Popover>
      <PopoverTrigger asChild><Button variant="outline"><Info className="size-4" />{t('workload.method')}</Button></PopoverTrigger>
      <PopoverContent className="w-[28rem] max-w-[calc(100vw-2rem)] space-y-2 text-sm" align="end">
        <p className="font-semibold">{t('workload.methodTitle')}</p>
        <ul className="list-disc space-y-1.5 pl-4">{[1, 2, 3, 4, 5, 6].map((n) => <li key={n}>{t(`workload.method${n}`)}</li>)}</ul>
      </PopoverContent>
    </Popover>
  )
}
