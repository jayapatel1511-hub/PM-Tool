import { invalidateChange } from '@/lib/keys'
import type { components } from '@/gen/api'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowUpDown, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, Ellipsis, EyeOff, Info, Lock, Monitor, Pencil } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, ActiveFilters, ChipToggle, ConfirmDialog, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Missing, Notice, Page, PageHelp, Section, Spinner, selectCls, tdCls, thCls, useDensity, type Density } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { OneSignal, type Signal } from '@/components/hub/indicators'
import { Avatar, PeoplePicker, weekOf } from '@/components/hub/people'
import { Key, StatusPill } from '@/components/hub/pills'
import { itemHref } from '@/components/hub/search'
import { ViewMenu } from '@/components/hub/views'
import { WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuLabel, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Skeleton } from '@/components/ui/skeleton'
import { useReference } from '@/hooks/data'
import { ApiError, get, patch, put, qs, typedRequest } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { addDays, fmtDate, hours, shortDate, today } from '@/lib/format'
import { runtimeText, t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'
import type { SavedViewKeys } from '@/gen/registry.gen'
import { GROUP_BY, GROUP_LABEL, groupPeople } from '@/components/planner/labels'
import { descendantUnitIds, useOrgPlacement } from '@/hooks/useOrgPlacement'
import { MenuSelect, errorText } from './projects/Tasks'

/** The server's load band for the week (FR-FIG-05), from the organisation's workload thresholds. */
type Cell = components['schemas']['LoadCell']
type Thresholds = components['schemas']['LoadThresholds']
type PersonRow = components['schemas']['LoadPerson']
type Unassigned = components['schemas']['UnassignedWork']
/** Open demand with no person named yet (FR-ROLE-04): shown apart from people, never in anyone's load. */
interface UnfilledRole { id: string; projectId: string; projectNumber: string; projectName: string; roleLabel: string; disciplineName?: string | null; fromDate: string; throughDate: string; plannedHours: number; cells: { week: string; hours: number }[] }
interface UnassignedTask { id: string; key: string; name: string; projectId: string; projectNumber: string; projectName: string; disciplineName?: string | null; startDate?: string | null; dueDate?: string | null; estimatedHours?: number | null; rowVersion: number; canAssign: boolean; editPath: string }
interface TaskLoadRow { id: string; key: string; name: string; status: string; dueDate?: string; estimatedHours?: number | null; progressPct: number; remaining?: number | null; overdue: boolean; noDueDate: number; rowVersion: number; weeks: number[]; canReassign: boolean }
interface PersonTasks { weeks: string[]; projects: { id: string; projectNumber: string; name: string; tasks: TaskLoadRow[] }[] }

const FILTERS = ['supervisorId', 'disciplineId', 'officeId', 'projectId', 'unitId', 'indicator', 'from', 'sort'] as const satisfies readonly (SavedViewKeys['workload'] | 'page' | 'panel' | 'draftAccessReason')[]
const INDICATORS = ['over', 'under', 'cluster', 'unestimated'] as const
const SORTS = ['load', 'overdue', 'tasks', 'name'] as const

/** The row's indicators, most severe first (§41 R7). Over-assigned names the week that tripped it (this or next week, from the
 *  server's `overWeek`); Under-assigned is judged on this week and next week together, so it names both. */
function personSignals(p: PersonRow, currentWeek: string): Signal[] {
  const overWeek = p.overWeek
  const underWeeks = `${shortDate(currentWeek)}, ${shortDate(addDays(currentWeek, 7))}`
  return ([
    p.overAssigned && { key: 'over', tone: 'bad', label: overWeek ? t('signal.withWeek', { label: t('workload.ind.over'), week: shortDate(overWeek) }) : t('workload.ind.over') },
    p.cluster && { key: 'cluster', tone: 'warn', label: t('workload.ind.cluster') },
    p.unestimated > 0 && { key: 'unestimated', tone: 'warn', label: t('workload.unestimatedN', { n: p.unestimated }) },
    p.underAssigned && { key: 'under', tone: 'idle', label: t('signal.withWeek', { label: t('workload.ind.under'), week: underWeeks }) },
  ] as (Signal | false)[]).filter(Boolean) as Signal[]
}

/** Heat shading by the server's load band with the number always shown (§13.11, FR-FIG-05); the thresholds are settings. */
const TONE = { over: 'bad', near: 'warn', within: 'ok', light: 'idle' } as const
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
  const q = useQuery({ queryKey: ['workload', filters], queryFn: () => typedRequest('get', '/api/v1/workload', { query: filters }) })
  const projects = useQuery({ queryKey: ['projects-pick', ''], queryFn: () => get<PageOf<{ id: string; projectNumber: string; name: string }>>('projects?pageSize=200') })
  const forbidden = (q.error as ApiError | null)?.status === 403
  const toggle = (id: string) => setOpen((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })
  const people = q.data?.people ?? []
  const org = useOrgPlacement(people.map(p => p.id))
  const groupBy = (GROUP_BY as readonly string[]).includes(sp.get('groupBy') ?? '') || (org.enabled && sp.get('groupBy') === 'homeUnit') ? sp.get('groupBy')! : 'supervisor'
  const unitIds = filters.unitId ? descendantUnitIds(org.units, filters.unitId) : null
  const scopedPeople = org.enabled && filters.unitId ? people.filter(p => org.placementMap.has(p.id) && unitIds!.has(org.placementMap.get(p.id) as string)) : people
  const unitName = (id: string | null | undefined) => id == null ? t('staff.group.notSet') : org.units.find(u => u.id === id)?.name ?? t('staff.group.unknown')
  const groups = groupBy === 'homeUnit' ? [...scopedPeople.reduce((m, p) => { const unit = org.placementMap.get(p.id); const key = unit === undefined ? t('staff.group.unknown') : unitName(unit); m.set(key, [...(m.get(key) ?? []), p]); return m }, new Map<string, PersonRow[]>())] : groupPeople(scopedPeople, groupBy, ref.data)
  const allPartial = people.length > 0 && people.every((p) => p.partialScope)
  const named = useFlag('figure_names')

  // Active filters as removable tokens (§13.0 Filters); sort is an order, not a filter.
  const supervisorName = people.find((p) => p.supervisorId === filters.supervisorId)?.supervisorName
  const tokens = ([
    ['supervisorId', t('workload.supervisor'), supervisorName ?? t('workload.selectedSupervisor')],
    ['disciplineId', t('common.discipline'), ref.data?.disciplines.find((d) => d.id === filters.disciplineId)?.name],
    ['officeId', t('admin.office'), ref.data?.offices.find((d) => d.id === filters.officeId)?.name],
    ['projectId', t('portfolio.project'), projects.data?.items.find((p) => p.id === filters.projectId)?.projectNumber],
    ['indicator', t('workload.indicator'), filters.indicator ? runtimeText(`workload.ind.${filters.indicator}`) : null],
    ['from', t('workload.from'), filters.from ? fmtDate(filters.from) : null],
    ['unitId', t('org.unit'), filters.unitId ? unitName(filters.unitId) : null],
  ] as const).filter(([k]) => filters[k])
  const unfilled = q.data?.unfilledRoles ?? []
  // One indicator at a time; choosing the selected chip again shows every person (the former "Any" chip).
  const indicatorChip = (x: (typeof INDICATORS)[number]) => <ChipToggle key={x} on={filters.indicator === x} onClick={() => set('indicator', filters.indicator === x ? null : x)}>{t(`workload.ind.${x}`)}</ChipToggle>
  const clear = () => { const n = new URLSearchParams(sp); for (const [k] of tokens) n.delete(k); setSp(n, { replace: true }) }
  const weeks = q.data?.weeks ?? []
  const shiftWeeks = (days: number) => set('from', addDays(weeks[0] ?? today(), days))

  return (
    // Without access the page holds only the notice, so the description and the method help, which explain data the person cannot see, are left out.
    <Page {...header} subtitle={forbidden ? undefined : header.subtitle} actions={forbidden ? undefined : <><Method thresholds={q.data?.thresholds} /><ViewMenu listType="workload" /><ExportMenu path="workload/export" params={filters} name="workload" /><DensityMenu /></>}>
      <WorkspaceTabs />
      {forbidden ? <Notice icon={Lock} title={t('workload.noAccessTitle')}
        action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('workload.noAccess')}</Notice> : <>
        {/* §41 R1: one toolbar row. Filters and the week start sit in the popover; Group and Sort are menus. */}
        <FilterBar activeCount={tokens.length}
          quick={<>
            <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t('workload.indicator')}>{INDICATORS.slice(0, 3).map(indicatorChip)}</div>
            <MenuSelect label={t('common.groupBy')} value={groupBy} onChange={(v) => set('groupBy', v === 'supervisor' ? null : v)} options={[...GROUP_BY.map((g) => ({ value: g, label: t(GROUP_LABEL[g]) })), ...(org.enabled ? [{ value: 'homeUnit', label: t('org.homeUnit') }] : [])]} />
            <MenuSelect label={t('common.sort')} icon={ArrowUpDown} value={filters.sort ?? 'load'} onChange={(v) => set('sort', v === 'load' ? null : v)} options={SORTS.map((v) => ({ value: v, label: t(`workload.sortBy.${v}`) }))} />
          </>}
          active={<ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={(k) => set(k, null)} onClear={clear} />}
          actions={<>
            {allPartial && <span role="status" className="inline-flex items-center gap-1.5 text-sm"><EyeOff className="size-4 text-muted-foreground" aria-hidden />{t('workload.partialTitle')}<PageHelp>{t('workload.partialAll')}</PageHelp></span>}
            <div role="group" aria-label={t('workload.weeks')} className="flex items-center gap-1">
              <Button variant="outline" size="icon" aria-label={t('workload.prevWeek')} onClick={() => shiftWeeks(-7)}><ChevronLeft className="size-5" /></Button>
              <span className="min-w-32 text-center text-sm font-semibold tabular-nums" aria-live="polite">
                {weeks.length ? t('workload.range', { from: shortDate(weeks[0]), to: shortDate(addDays(weeks[weeks.length - 1], 6)) }) : t('common.dash')}
              </span>
              <Button variant="outline" size="icon" aria-label={t('workload.nextWeek')} onClick={() => shiftWeeks(7)}><ChevronRight className="size-5" /></Button>
              <Button variant="outline" disabled={!filters.from} onClick={() => set('from', null)}>{t('workload.thisWeek')}</Button>
            </div>
          </>}>
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
            {org.enabled && <Field label={t('org.unit')} htmlFor="wl-unit" className="w-full sm:w-56"><select id="wl-unit" className={selectCls} value={filters.unitId ?? ''} onChange={(e) => set('unitId', e.target.value)}><option value="">{t('org.anyUnit')}</option>{org.units.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>}
            <Field label={t('workload.from')} htmlFor="wl-from" className="w-full sm:w-44">
              <Input id="wl-from" type="date" value={filters.from ?? ''} onChange={(e) => set('from', e.target.value)} />
            </Field>
          </div>
          <div className="flex flex-wrap items-center gap-2" role="group" aria-labelledby="wl-indicator">
            <span id="wl-indicator" className="mr-1 text-sm font-medium">{t('calm.moreQuick')}</span>
            {INDICATORS.slice(3).map(indicatorChip)}
          </div>
        </FilterBar>

        {filters.disciplineId && <p className="text-sm text-muted-foreground">{t('workload.disciplineScope')}</p>}
        {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
        {q.isPending ? <GridSkeleton /> : !q.data ? null : q.data.people.length === 0 ? (
          <div className="rounded-lg border bg-card">
            <Empty title={t('workload.emptyTitle')} action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('workload.empty')}</Empty>
          </div>
        ) : (
          // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- axe scrollable-region-focusable: keyboard users scroll the grid
          <div className="scroll-region max-h-[78dvh] overflow-auto rounded-lg border bg-card" role="region" aria-label={t('workload.grid')} aria-describedby={named ? 'workload-basis' : undefined} tabIndex={0}>
            {named && <p id="workload-basis" className="sticky left-0 max-w-xl px-3 py-2 text-xs text-muted-foreground">{t('workload.uncommittedBasis')}</p>}
            <table className="w-full min-w-max border-separate border-spacing-0 text-sm">
              <caption className="sr-only">{t('workload.gridCaption')}</caption>
              <WeekHead weeks={q.data.weeks} currentWeek={q.data.currentWeek} first={t('workload.person')} last={t('workload.summary')} />
              {groups.map(([g, list]) => (
                <tbody key={g}>
                  {groupBy !== 'none' && (
                    <tr>
                      <th scope="rowgroup" className="sticky left-0 z-10 border-b border-r bg-sidebar px-4 py-2 text-left text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
                        {g} · {list.length}
                      </th>
                      {/* Group subtotal (AC-GRP-02c): the sum of the rows below, week by week. */}
                      {q.data!.weeks.map((w, i) => {
                        const committed = list.reduce((n, p) => n + (p.cells[i]?.committed ?? 0), 0), available = list.reduce((n, p) => n + (p.cells[i]?.available ?? 0), 0)
                        return <td key={w} data-subtotal={committed} className="border-b border-r bg-sidebar px-(--cell-px) py-2 text-xs/[18px] text-muted-foreground tabular-nums">
                          <span className="font-semibold text-foreground">{hours(committed)}</span> <span aria-hidden>/</span><span className="sr-only">{t('workload.ofAvailable')}</span> {hours(available)}
                        </td>
                      })}
                      <td className="border-b bg-sidebar" />
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
                              <div className="mt-1.5 empty:hidden"><OneSignal signals={personSignals(p, q.data!.currentWeek)} /></div>
                              {p.canSetCapacity && (
                                <div className="mt-1 flex gap-1">
                                  <Button variant="ghost" size="icon-sm" title={t('workload.setCapacity', { name: p.displayName })} aria-label={t('workload.setCapacity', { name: p.displayName })} onClick={() => setCapacity(p)}><Pencil className="size-4" /></Button>
                                  <Button variant="ghost" size="icon-sm" title={t('workload.setDayAvailability', { name: p.displayName })} aria-label={t('workload.setDayAvailability', { name: p.displayName })} onClick={() => setAvailability(p)}><CalendarDays className="size-4" /></Button>
                                </div>
                              )}
                            </div>
                          </div>
                        </th>
                        {p.cells.map((c) => <td key={c.week} className="border-b border-r p-0 align-top"><WeekCell c={c} capacity={p.capacity} named={named} /></td>)}
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
        {q.data && unfilled.length > 0 && <UnfilledRoles roles={unfilled} weeks={q.data.weeks} currentWeek={q.data.currentWeek} />}
        {q.data?.unassigned && <UnassignedTasks data={q.data.unassigned} />}
      </>}
      {capacity && <CapacityDialog p={capacity} defaultCapacity={q.data?.defaultCapacity ?? 40} onClose={(ok) => { setCapacity(null); if (ok) q.refetch() }} />}
      {availability && <AvailabilityDialog p={availability} onClose={(ok) => { setAvailability(null); if (ok) q.refetch() }} />}
    </Page>
  )
}

function UnassignedTasks({ data }: { data: Unassigned }) {
  const qc = useQueryClient()
  const [state, setState] = useState<Record<string, RowState>>({})
  const assign = async (task: UnassignedTask, personId: string | null) => {
    if (!personId || !task.canAssign) return
    setState(s => ({ ...s, [task.id]: { status: 'saving' } }))
    try { await typedRequest('patch', '/api/v1/tasks/{id}', { params: { id: task.id }, body: { assigneeId: personId }, ifMatch: task.rowVersion }); toast.success(t('workload.assigned', { key: task.key })); setState(s => { const n = { ...s }; delete n[task.id]; return n }); void invalidateChange(qc, 'Task', task.projectId) }
    catch (e) { setState(s => ({ ...s, [task.id]: { status: 'failed', error: e } })) }
  }
  if (data.tasks.length === 0) return null
  return <Section id="wl-unassigned" title={t('workload.unassignedTitle')} count={data.totalCount} actions={data.totalCount > data.tasks.length && <Button asChild variant="outline" size="sm"><Link to={data.showAllPath}>{t('workload.showAll', { n: data.totalCount })}</Link></Button>}>
    <div className="overflow-x-auto rounded-lg border"><table className="w-full min-w-[48rem] text-sm"><thead><tr><th className={thCls}>{t('itemType.Task')}</th><th className={thCls}>{t('portfolio.project')}</th><th className={thCls}>{t('workload.taskDueLabel')}</th><th className={cn(thCls, 'text-right')}>{t('workload.estimate')}</th><th className={thCls}>{t('workload.assignee')}</th></tr></thead><tbody>{data.tasks.map(task => { const st = state[task.id]; return <tr key={task.id}><th className={tdCls + ' text-left font-normal'}><Link className="hover:underline" to={task.editPath}><Key>{task.key}</Key> {task.name}</Link></th><td className={tdCls}>{task.projectNumber} {task.projectName}{task.disciplineName ? ` · ${task.disciplineName}` : ''}</td><td className={tdCls}>{task.dueDate ? fmtDate(task.dueDate) : t('workload.sum.noDue')}</td><td className={cn(tdCls, 'text-right tabular-nums')}>{task.estimatedHours == null ? <Missing /> : hours(task.estimatedHours)}</td><td className={tdCls}>{task.canAssign ? <PeoplePicker capacityWeek={weekOf(task.startDate ?? task.dueDate ?? today())} value={null} onChange={v => void assign(task, v)} disabled={st?.status === 'saving'} placeholder={t('workload.assignTo')} label={t('workload.assignTask', { key: task.key })} /> : <span className="text-xs text-muted-foreground">{t('workload.cannotAssign')}</span>}{st?.status === 'failed' && <p role="alert" className="text-xs text-bad">{t('workload.reassignFailed')}: {errorText(st.error)}</p>}</td></tr> })}</tbody></table></div>
  </Section>
}

/** The week columns' header row, on one line so the first person sits higher (§41 AC-CALM-01). */
function WeekHead({ weeks, currentWeek, first, last }: { weeks: string[]; currentWeek: string; first: string; last?: string }) {
  return (
    <thead>
      <tr>
        <th scope="col" className="sticky left-0 top-0 z-30 w-60 min-w-60 border-b border-r bg-muted px-4 py-2 text-left font-medium text-muted-foreground">{first}</th>
        {weeks.map((w) => {
          const current = w === currentWeek
          return (
            <th key={w} scope="col" aria-current={current ? 'date' : undefined}
              className={cn('sticky top-0 z-20 min-w-36 whitespace-nowrap border-b border-r px-3 py-2 text-left font-medium', current ? 'bg-accent text-accent-foreground' : 'bg-muted text-muted-foreground')}>
              <span className="text-xs/[18px]">{current ? t('workload.thisWeek') : t('workload.weekOfLabel')}</span> <span className="text-sm font-semibold text-foreground">{shortDate(w)}</span>
            </th>
          )
        })}
        {last && <th scope="col" className="sticky top-0 z-20 min-w-56 border-b bg-muted px-4 py-2 text-left font-medium text-muted-foreground">{last}</th>}
      </tr>
    </thead>
  )
}

/** Unfilled roles (FR-ROLE-04, D3): open demand on visible projects, spread over working days like an allocation. They are
 *  listed apart from people and never enter anyone's load, percentage or indicator (AC-ROLE-02). */
function UnfilledRoles({ roles, weeks, currentWeek }: { roles: UnfilledRole[]; weeks: string[]; currentWeek: string }) {
  return (
    <Section id="wl-unfilled" title={t('openRole.unfilled')} count={roles.length} actions={<PageHelp label={t('openRole.unfilledHelp')}>{t('openRole.unfilledHint')}</PageHelp>}>
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- axe scrollable-region-focusable */}
      <div className="scroll-region overflow-x-auto" role="region" aria-label={t('openRole.unfilled')} tabIndex={0}>
        <table className="w-full min-w-max border-separate border-spacing-0 text-sm">
          <caption className="sr-only">{t('openRole.unfilled')}</caption>
          <WeekHead weeks={weeks} currentWeek={currentWeek} first={t('openRole.role')} last={t('openRole.planned')} />
          <tbody>{roles.map((r) => (
            <tr key={r.id}>
              <th scope="row" className="sticky left-0 z-10 w-60 min-w-60 border-b border-r bg-card px-4 py-2 text-left align-top font-normal">
                <div className="break-words font-semibold">{r.roleLabel}</div>
                <Link className="text-xs/[18px] text-muted-foreground hover:underline" to={`/projects/${encodeURIComponent(r.projectNumber)}/allocations?tab=roles`}>
                  <span className="whitespace-nowrap"><Key>{r.projectNumber}</Key></span> {r.projectName}{r.disciplineName && ` · ${r.disciplineName}`}
                </Link>
              </th>
              {weeks.map((w) => { const h = r.cells.find((c) => c.week === w)?.hours ?? 0; return <td key={w} className="border-b border-r px-3 py-2 text-right align-top tabular-nums">{h ? hours(h) : <span className="text-muted-foreground"><span aria-hidden>·</span><span className="sr-only">{hours(0)}</span></span>}</td> })}
              <td className="border-b px-4 py-2 align-top text-xs/[18px] text-muted-foreground tabular-nums">{hours(r.plannedHours)} · {fmtDate(r.fromDate)} – {fmtDate(r.throughDate)}</td>
            </tr>
          ))}</tbody>
        </table>
      </div>
    </Section>
  )
}

/** One person-week: committed load of available capacity, then what it is made of. Proposed requests sit outside the
 *  load (dotted), and a partial view never claims spare capacity (FR-CAP-04, FR-CAP-06). */
function WeekCell({ c, capacity, named }: { c: Cell; capacity: number; named: boolean }) {
  const tone = TONE[c.band as keyof typeof TONE] ?? 'idle'
  // The server's Uncommitted (FR-FIG-03): null on a partial row unless negative; "Remaining" until flag.figure_names is on.
  const figure = c.uncommitted == null ? null : t(named ? 'workload.uncommitted' : 'workload.remaining', { h: hours(c.uncommitted) })
  const scale = Math.max(c.available, c.committed + c.proposed, 1)
  return (
    <div data-band={c.band} className={cn('flex h-full min-h-28 flex-col gap-1.5 px-(--cell-px) py-(--cell-py)', SHADE[tone])}>
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
        {c.uncommitted != null && c.uncommitted < 0 ? <span className={cn('ml-1.5 font-semibold', tone === 'bad' ? 'text-bad' : 'text-warn')}><span aria-hidden>{tone === 'bad' ? '■' : '▲'} </span>{figure}</span>
          : figure && <span className="ml-1.5 text-muted-foreground">{figure}</span>}
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
      {/* The load is within range: said in words, not as a second indicator on the row (§41 R7, R8). */}
      {!p.overAssigned && !p.underAssigned && <p className="text-muted-foreground"><span aria-hidden>● </span>{t('workload.ind.ok')}</p>}
      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 tabular-nums">
        <dt className="text-muted-foreground">{t('workload.sum.open')}</dt><dd>{t('workload.sum.openValue', { n: p.openTasks, projects: p.projects })}</dd>
        <dt className="text-muted-foreground">{t('ind.overdue')}</dt><dd className={cn(p.overdue > 0 && 'font-semibold text-bad')}>{p.overdue}</dd>
        <dt className="text-muted-foreground">{t('workload.unestimated')}</dt><dd className={cn(p.unestimated > 0 && 'font-semibold text-warn')}>{p.unestimated}</dd>
        {p.noDueDate > 0 && <><dt className="text-muted-foreground">{t('workload.sum.noDue')}</dt><dd>{hours(p.noDueDate)}</dd></>}
      </dl>
    </div>
  )
}

function Legend({ thresholds: th = { overPct: 110, amberPct: 90, underPct: 40 } }: { thresholds?: Thresholds }) {
  return (
    <ul aria-label={t('workload.legend')} className="space-y-1.5 text-xs/[18px] text-muted-foreground">
      <li className="flex items-center gap-1.5"><span aria-hidden className="h-1.5 w-6 rounded-full bg-ok" />{t('workload.legend.committed')}</li>
      <li className="flex items-center gap-1.5"><span aria-hidden className="h-1.5 w-6 rounded-full border border-dotted border-work bg-work-bg" />{t('workload.legend.proposed')}</li>
      <li className="flex items-center gap-1.5"><span aria-hidden className="h-3 w-0.5 bg-foreground" />{t('workload.legend.available')}</li>
      <li className="flex items-center gap-1.5"><span aria-hidden>■ ▲</span>{t('workload.legend.over', { over: th.overPct })}</li>
      <li>{t('workload.legend.bands', { under: th.underPct, amber: th.amberPct, over: th.overPct })}</li>
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
                    <OneSignal signals={([tk.overdue && { key: 'overdue', tone: 'bad', label: t('ind.overdue') }, tk.estimatedHours == null && { key: 'unestimated', tone: 'warn', label: t('workload.unestimated') }] as (Signal | false)[]).filter(Boolean) as Signal[]} />
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
      : existing && hoursValue === '' && <Notice icon={Info} title={t('workload.existingAvailability', { h: existing.availableHours, category: runtimeText(`workload.availability.${existing.category}`) })} />}
    <Field label={t('workload.dayHours')} htmlFor="availability-hours" hint={t('workload.dayHoursHint')} error={invalid ? t('workload.dayHoursRange') : undefined}>
      <div className="flex items-center gap-2"><Input id="availability-hours" className="w-32" type="number" min={0} max={24} step={0.25} required value={hoursValue} aria-invalid={invalid || undefined} onChange={e => setHours(e.target.value)} /><span className="text-sm text-muted-foreground">h</span></div>
    </Field>
    <Field label={t('workload.dayCategory')} htmlFor="availability-category"><select id="availability-category" className={selectCls} value={category} onChange={e => setCategory(e.target.value)}>
      {['Unavailable', 'Reduced', 'Additional'].map(value => <option key={value} value={value}>{runtimeText(`workload.availability.${value}`)}</option>)}
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

/** Row density, the viewer's own display choice, kept one click away in the overflow menu (§41 R1). */
function DensityMenu() {
  const [density, setDensity] = useDensity()
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild><Button variant="outline" size="icon" aria-label={t('calm.moreActions')} title={t('calm.moreActions')}><Ellipsis className="size-4" aria-hidden /></Button></DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>{t('density.label')}</DropdownMenuLabel>
        <DropdownMenuRadioGroup value={density} onValueChange={(v) => setDensity(v as Density)}>
          {(['compact', 'comfortable'] as const).map((d) => <DropdownMenuRadioItem key={d} value={d}>{runtimeText(`density.${d}`)}</DropdownMenuRadioItem>)}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

/** The method in plain words (§12.15 step 4), so nobody over-reads the numbers. */
function Method({ thresholds }: { thresholds?: Thresholds }) {
  const named = useFlag('figure_names')
  return (
    <Popover>
      <PopoverTrigger asChild><Button variant="outline" size="icon" aria-label={t('workload.method')} title={t('workload.method')}><Info className="size-4" aria-hidden /></Button></PopoverTrigger>
      <PopoverContent className="w-[28rem] max-w-[calc(100vw-2rem)] space-y-2 text-sm" align="end">
        <p>{t('workload.about')}</p>
        <p className="font-semibold">{t('workload.methodTitle')}</p>
        <ul className="list-disc space-y-1.5 pl-4">{[1, 2, 3, 4, 5, 6].map((n) => <li key={n}>{runtimeText(n === 5 && named ? 'workload.method5Named' : `workload.method${n}`)}</li>)}</ul>
        <p className="pt-1 font-semibold">{t('workload.legend')}</p>
        <Legend thresholds={thresholds} />
      </PopoverContent>
    </Popover>
  )
}
