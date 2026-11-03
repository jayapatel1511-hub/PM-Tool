import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronRight, Info, UserPlus } from 'lucide-react'
import { Fragment, useEffect, useRef, useState, type CSSProperties } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, ActiveFilters, ConfirmDialog, Empty, ErrorBanner, Field, Loading, Missing, Page, Segmented, SummaryTile, selectCls, tdCls, thCls, TableRegion } from '@/components/hub/common'
import { CountCard } from '@/components/hub/count-card'
import { TitleBlock } from '@/components/hub/title-block'
import { Avatar, CapacityFigure, PeoplePicker, usePeopleCapacity, weekOf } from '@/components/hub/people'
import { ViewMenu } from '@/components/hub/views'
import { OneSignal, type Signal } from '@/components/hub/indicators'
import { HealthPill, Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { useProject, useReference } from '@/hooks/data'
import type { components } from '@/gen/api'
import { get, typedRequest } from '@/lib/api'
import { invalidateChange } from '@/lib/keys'
import { useFlag, useMe } from '@/lib/auth'
import { ago, fmtDate, shortDate, today } from '@/lib/format'
import { runtimeText, t, tv, type TextKey } from '@/lib/i18n'
import { cn, type Accent } from '@/lib/utils'
import { PersonFactsChips } from '@/components/hub/person-facts'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { staffCardDestination, staffCardIndicators, staffCardParams, staffCardView, type StaffCardIndicator } from './staff-count-card-view'
import './staff-count-card.css'

type StaffRow = components['schemas']['MyStaffPerson']
interface OrganisationUnits { includeIndirectReports?: boolean; units: { id: string; name: string; parentId?: string; leaderId?: string; closedOn?: string }[] }
type Assignment = components['schemas']['MyStaffAssignment']

const COLS: [keyof StaffRow | 'name', TextKey][] = [['name', 'staff.name'], ['projects', 'staff.projects'], ['open', 'dash.open'], ['overdue', 'ind.overdue'], ['blocked', 'ind.blocked'],
  ['blocking', 'staff.blocking'], ['reviews', 'staff.reviews'], ['deliverablesDue', 'dash.due14'], ['lastActivityAt', 'task.lastActivity']]


/** My Staff (§13.19): a manager's direct reports, their assignments and work counts, and staffing them on projects. */
export function StaffPage() {
  const small = useFlag('small_ux')
  const visualSystem = useFlag('visual_system')
  const peopleScreens = useFlag('people_screens')
  const [sp, setSp] = useSearchParams()
  const location = useLocation(), peopleRef = useRef<HTMLElement>(null)
  const ref = useReference()
  const me = useMe()
  const org = useQuery({ enabled: me.flags?.org_structure === true, queryKey: ['organisation'], queryFn: () => get<OrganisationUnits>('organisation') })
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const f = { scope: sp.get('scope') ?? undefined, showInactive: sp.get('inactive') === '1' || undefined, officeId: sp.get('office') ?? undefined,
    disciplineId: sp.get('discipline') ?? undefined, indicator: sp.get('indicator') ?? undefined }
  const q = useQuery({ queryKey: ['staff', f], queryFn: () => typedRequest('get', '/api/v1/staff', { query: f }) })
  const showCapacity = useMe().capabilities.workload
  const capacity = usePeopleCapacity(weekOf(today()), !!q.data, q.data?.people.map((p) => p.id) ?? [])
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [sort, setSort] = useState<{ key: string; desc: boolean } | null>(null)
  const indicators = visualSystem ? staffCardIndicators(sp.get('cardIndicators')) : []
  useEffect(() => {
    if (!visualSystem) return
    const next = staffCardParams(sp, staffCardIndicators(sp.get('cardIndicators')))
    if (next.toString() !== sp.toString()) setSp(next, { replace: true })
  }, [visualSystem, sp, setSp])
  useEffect(() => {
    if (visualSystem && location.hash === '#staff-people' && !q.isPending && !q.error) {
      peopleRef.current?.focus({ preventScroll: true })
      peopleRef.current?.scrollIntoView({ block: 'start' })
    }
  }, [visualSystem, location.key, location.hash, q.isPending, q.error])
  const header = { eyebrow: t('nav.resources'), title: t('nav.staff') }
  if (q.isPending) return <Page {...header}><Loading rows={8} /></Page>
  if (q.error) return <Page {...header}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const d = q.data
  const view = visualSystem ? staffCardView(d.people, indicators) : null
  const people = view?.rows ?? d.people
  const rows = sort ? [...people].sort((a, b) => {
    const va = sort.key === 'name' ? a.displayName : (a as any)[sort.key] ?? '', vb = sort.key === 'name' ? b.displayName : (b as any)[sort.key] ?? ''
    const r = typeof va === 'number' ? va - (vb as number) : String(va).localeCompare(String(vb))
    return sort.desc ? -r : r
  }) : people
  const office = (id?: string | null) => ref.data?.offices.find((o) => o.id === id)?.name
  const indicatorLabels = { overdue: t('ind.overdue'), blocked: t('ind.blocked'), reviewsStalled: t('staffCards.stalled') }
  const scopeName = d.scope === 'all' ? t('staff.scope.all') : d.scope === 'reports' ? t('staff.scope.reports') : d.scope.startsWith('unit:')
    ? org.data?.units.find(unit => `unit:${unit.id}` === d.scope)?.name ?? t('common.unavailable') : t('staff.scope.direct')
  const basis = [scopeName, office(f.officeId), ref.data?.disciplines.find(discipline => discipline.id === f.disciplineId)?.name,
    f.indicator && (indicatorLabels[f.indicator as StaffCardIndicator] ?? (f.indicator === 'reviews' ? t('staff.reviews') : f.indicator === 'blocking' ? t('staff.blocking') : t('common.unavailable')))].filter(Boolean).join(' · ')
  const setIndicators = (next: StaffCardIndicator[]) => setSp(staffCardParams(sp, next))
  const columns: typeof COLS = visualSystem ? [...COLS.slice(0, 7), ['reviewsStalled', 'staffCards.stalled'], ...COLS.slice(7)] : COLS
  const captureView = () => {
    const params = Object.fromEntries(staffCardParams(sp, indicators))
    delete params.view; delete params.panel
    return params
  }
  const subtitle = d.scope === 'all' ? t('staff.allSubtitle') : t('staff.directSubtitle')
  const views = small && <ViewMenu listType="staff" captureParams={visualSystem ? captureView : undefined} />
  const metricCards = visualSystem && view ? <div className="home-count-cards" data-staff-counts style={{ '--count-columns': 5 } as CSSProperties}>
    {([
      ['staff', 'staff.tile.staff', 'teal', 'rcv.staffDefinition', 'staffCards.peopleWhere', undefined],
      ['assignments', 'staff.tile.assignments', 'blue', 'rcv.assignmentsDefinition', 'staffCards.assignmentsWhere', undefined],
      ['withOverdue', 'staff.tile.overdue', 'red', 'rcv.overdueDefinition', 'staffCards.peopleWhere', 'overdue'],
      ['withBlocked', 'staff.tile.blocked', 'red', 'rcv.blockedDefinition', 'staffCards.peopleWhere', 'blocked'],
      ['reviewsStalled', 'staff.tile.reviews', 'amber', 'rcv.reviewsDefinition', 'staffCards.reviewsWhere', 'reviewsStalled'],
    ] as const).map(([key, label, tone, definition, where, indicator]) => <CountCard key={key} label={t(label)} value={view.tiles[key] ?? <Missing />} valueText={view.tiles[key] == null ? t('common.unavailable') : undefined}
      tone={tone} definition={t(definition)} where={t(where, { scope: basis, n: people.length })} to={staffCardDestination(sp, indicator)} />)}
  </div> : null
  // Summary tiles (§13.19) filter the table; the selected one is outlined and checked, never tinted alone.
  const tile = (label: string, n: number, accent: Accent, definition: string, indicator?: string) => {
    const on = !!indicator && f.indicator === indicator
    return <SummaryTile label={label} value={n} accent={accent} hint={definition} selected={indicator ? on : undefined} onClick={() => set('indicator', on ? undefined : indicator)} />
  }
    const roleChips = (r: StaffRow['roles']) => (['pm', 'dl', 'team', 'reviewer', 'viewer'] as const).filter((k) => r[k] > 0).map((k) => `${t(`staff.role.${k}`)} ${r[k]}`).join(' · ')
  const count = (p: StaffRow, n: number, section: string, bad = false) => <Link to={`/my-work?userId=${p.id}#mw-${section}`} className={cn('tabular-nums hover:underline', bad && n > 0 && 'font-medium text-bad')}>{n}</Link>
  const workName = (p: StaffRow) => <Link to={`/my-work?userId=${p.id}`} className="font-semibold hover:underline">{p.isActive ? p.displayName : t('common.inactiveSuffix', { name: p.displayName })}</Link>
  const signals = (p: StaffRow): Signal[] => {
    const cell = capacity(p.id)
    const result: Signal[] = []
    if (p.overdue) result.push({ key: 'overdue', tone: 'bad', label: t('ind.overdue') })
    if (p.blocked) result.push({ key: 'blocked', tone: 'bad', label: t('ind.blocked') })
    if (p.reviewsStalled) result.push({ key: 'reviews', tone: 'warn', label: t('staff.stalled', { n: p.reviewsStalled }) })
    if (cell?.overPlanned) result.push({ key: 'over', tone: 'warn', label: t('signal.withWeek', { label: t('planner.indicator.over'), week: shortDate(cell.week) }) })
    if (cell?.staleEntries) result.push({ key: 'stale', tone: 'warn', label: t('signal.withWeek', { label: t('staff.stalePlan', { n: cell.staleEntries }), week: shortDate(cell.week) }) })
    return result
  }
  return (
    <Page {...header} className={visualSystem ? 'staff-count-view' : undefined} overlap={visualSystem} titleBlock={!visualSystem && <TitleBlock asOf={(d as typeof d & { asOf?: string }).asOf} />}
      actions={visualSystem ? <><TitleBlock asOf={(d as typeof d & { asOf?: string }).asOf} />{views}</> : views} subtitle={visualSystem ? undefined : subtitle}>
      {metricCards}
      <div className={cn('flex flex-wrap items-end gap-3 rounded-lg border bg-card p-4 text-sm md:p-5', visualSystem && 'staff-count-view__filters')}>
        {visualSystem && <p className="text-sm font-medium">{subtitle}</p>}
        {(d.canSeeAll || org.data?.includeIndirectReports || (org.data?.units.some((u) => u.leaderId === me.id) ?? false)) && (
          <Segmented label={t('staff.scope')} value={f.scope ?? 'direct'} onChange={(s) => set('scope', s === 'direct' ? undefined : s)}
            options={[{ value: 'direct', label: t('staff.scope.direct') }, ...(d.canSeeAll ? [{ value: 'all', label: t('staff.scope.all') }] : []), ...(org.data?.includeIndirectReports ? [{ value: 'reports', label: t('staff.scope.reports') }] : []), ...(org.data?.units.filter((u) => u.leaderId === me.id && !u.closedOn).map((u) => ({ value: `unit:${u.id}`, label: u.name })) ?? [])]} />
        )}
        <Field label={t('field.OfficeId')} htmlFor="staff-office" className={cn('w-full sm:w-44', visualSystem && 'staff-count-view__field')}>
          <select id="staff-office" className={selectCls} value={f.officeId ?? ''} onChange={(e) => set('office', e.target.value || undefined)}>
            <option value="">{t('staff.anyOffice')}</option>{ref.data?.offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
          </select>
        </Field>
        <Field label={t('common.discipline')} htmlFor="staff-discipline" className={cn('w-full sm:w-48', visualSystem && 'staff-count-view__field')}>
          <select id="staff-discipline" className={selectCls} value={f.disciplineId ?? ''} onChange={(e) => set('discipline', e.target.value || undefined)}>
            <option value="">{t('projects.anyDiscipline')}</option>{ref.data?.disciplines.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <label className="flex min-h-(--control-h) items-center gap-2"><input type="checkbox" className="size-4 accent-(--primary)" checked={!!f.showInactive} onChange={(e) => set('inactive', e.target.checked ? '1' : undefined)} />{t('staff.showInactive')}</label>
        {visualSystem && f.indicator && <Button variant="outline" onClick={() => set('indicator')}>{t('staffCards.clearBase')}</Button>}
      </div>
      {visualSystem && view ? <>
        {indicators.length > 0 && <ActiveFilters tokens={indicators.map(key => ({ key, label: indicatorLabels[key], value: t('common.yes') }))}
          onRemove={key => setIndicators(indicators.filter(indicator => indicator !== key))} onClear={() => setIndicators([])} />}
        {view.unavailable > 0 && <p role="status" className="text-sm text-muted-foreground">{t('staffCards.unavailable', { n: view.unavailable })}</p>}
      </> : <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 xl:grid-cols-5">
        {tile(t('staff.tile.staff'), d.tiles.staff, 'blue', t('rcv.staffDefinition'))}
        {tile(t('staff.tile.assignments'), d.tiles.assignments, 'mint', t('rcv.assignmentsDefinition'))}
        {tile(t('staff.tile.overdue'), d.tiles.withOverdue, 'lavender', t('rcv.overdueDefinition'), 'overdue')}
        {tile(t('staff.tile.blocked'), d.tiles.withBlocked, 'peach', t('rcv.blockedDefinition'), 'blocked')}
        {tile(t('staff.tile.reviews'), d.tiles.reviewsStalled, 'rose', t('rcv.reviewsDefinition'), 'reviewsStalled')}
      </div>}
      <section ref={peopleRef} id={visualSystem ? 'staff-people' : undefined} role={visualSystem ? 'region' : undefined} aria-label={visualSystem ? t('nav.staff') : undefined} tabIndex={visualSystem ? -1 : undefined} className="min-w-0 space-y-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
      {rows.length > 0 && (
        <ul className="space-y-2 md:hidden" aria-label={t('nav.staff')}>
          {rows.map((p) => (
            <li key={p.id} data-staff-person={visualSystem ? p.id : undefined} className="rounded-lg border bg-card p-4 text-sm">
              <div className="flex items-center gap-2.5 font-semibold"><Avatar id={p.id} name={p.displayName} /><PersonPanelTrigger userId={p.id} name={p.displayName} active={p.isActive} staffScope={d.scope} className="min-w-0 break-words text-left font-semibold hover:underline">{p.isActive ? p.displayName : t('common.inactiveSuffix', { name: p.displayName })}</PersonPanelTrigger></div>
              <div className="text-xs text-muted-foreground">{[p.jobTitle, office(p.officeId), roleChips(p.roles)].filter(Boolean).join(' · ')}</div>
              <PersonFactsChips facts={p.personFacts} />
              {showCapacity && <div className="mt-1"><span className="mr-2 text-xs text-muted-foreground">{t('staff.thisWeek')}</span><CapacityFigure cell={capacity(p.id)} /></div>}
              <OneSignal signals={signals(p)} />
              <dl className="mt-2 grid grid-cols-4 gap-2 text-center text-xs">
                {visualSystem && <div><dt className="text-muted-foreground">{t('staff.projects')}</dt><dd data-staff-projects={p.id} className="text-base font-semibold tabular-nums">{Number.isFinite(p.projects) ? p.projects : <Missing />}</dd></div>}
                {([['dash.open', p.open, false], ['ind.overdue', p.overdue, true], ['ind.blocked', p.blocked, true], ['staff.reviews', p.reviews, false]] as const).map(([label, n, bad]) => (
                  <div key={label}><dt className="text-muted-foreground">{t(label)}</dt><dd className={cn('text-base font-semibold tabular-nums', bad && n > 0 && 'text-bad')}>{n}</dd></div>
                ))}
                {visualSystem && <div><dt className="text-muted-foreground">{t('staffCards.stalled')}</dt><dd data-staff-stalled={p.id} className="text-base font-semibold tabular-nums">{Number.isFinite(p.reviewsStalled) ? p.reviewsStalled : <Missing />}</dd></div>}
              </dl>
            </li>
          ))}
        </ul>
      )}
      {rows.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('staff.none')}</Empty></div> : (
        <TableRegion className="hidden md:block">
          <table className="w-full text-sm">
            <thead className="bg-muted text-left text-muted-foreground">
              <tr><th scope="col" className="w-12"><span className="sr-only">{t('common.details')}</span></th>{columns.map(([k, label]) => (
                <th key={k} scope="col" className={cn(thCls, k !== 'name' && k !== 'lastActivityAt' && 'text-right')} aria-sort={sort?.key === k ? (sort.desc ? 'descending' : 'ascending') : 'none'}>
                  <button className="inline-flex items-center gap-1 hover:text-foreground" onClick={() => setSort(sort?.key === k ? { key: k, desc: !sort.desc } : { key: k, desc: k !== 'name' })}>
                    {t(label)}{sort?.key === k && <span aria-hidden>{sort.desc ? '↓' : '↑'}</span>}</button>
                </th>))}{showCapacity && <th scope="col" className={thCls}>{t('staff.thisWeek')}</th>}<th scope="col" className={thCls}>{t('staff.roles')}</th></tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <Fragment key={p.id}>
                  <tr data-staff-person={visualSystem ? p.id : undefined} className={cn('border-t hover:bg-muted', !p.isActive && 'text-muted-foreground')}>
                    <td className="px-2"><button className="grid size-(--control-row-h) place-items-center rounded-md hover:bg-secondary" aria-expanded={open.has(p.id)} aria-label={t('staff.showAssignments', { name: p.displayName })}
                      onClick={() => setOpen((s) => { const n = new Set(s); if (n.has(p.id)) n.delete(p.id); else n.add(p.id); return n })}>
                      {open.has(p.id) ? <ChevronDown className="size-3.5" /> : <ChevronRight className="size-3.5" />}</button></td>
                    <td className={tdCls}>
                      <div className="flex min-w-56 items-center gap-2.5"><Avatar id={p.id} name={p.displayName} />
                        <div className="min-w-0">{peopleScreens ? <div className="flex items-center gap-2">{workName(p)}<PersonPanelTrigger userId={p.id} name={p.displayName} active={p.isActive} staffScope={d.scope} className="inline-grid min-h-8 min-w-8 place-items-center rounded hover:bg-muted focus-visible:outline-2"><Info className="size-4" aria-hidden /><span className="sr-only">{t('common.details')}: {p.displayName}</span></PersonPanelTrigger></div> : workName(p)}
                          <div className="text-xs/[18px] text-muted-foreground">{[p.jobTitle, office(p.officeId)].filter(Boolean).join(' · ')}</div><PersonFactsChips facts={p.personFacts} /><OneSignal signals={signals(p)} /></div></div>
                    </td>
                    <td data-staff-projects={visualSystem ? p.id : undefined} className="px-(--cell-px) py-(--cell-py) text-right tabular-nums">{visualSystem && !Number.isFinite(p.projects) ? <Missing /> : p.projects}</td>
                    <td className="px-(--cell-px) py-(--cell-py) text-right">{count(p, p.open, 'tasks')}</td>
                    <td className="px-(--cell-px) py-(--cell-py) text-right">{count(p, p.overdue, 'tasks', true)}</td>
                    <td className="px-(--cell-px) py-(--cell-py) text-right">{count(p, p.blocked, 'waiting', true)}</td>
                    <td className="px-(--cell-px) py-(--cell-py) text-right">{count(p, p.blocking, 'blocking', true)}</td>
                    <td className="px-(--cell-px) py-(--cell-py) text-right">{count(p, p.reviews, 'reviews')}{p.reviewsStalled > 0 && <span className="ml-1 text-xs text-warn">({t('staff.stalled', { n: p.reviewsStalled })})</span>}</td>
                    {visualSystem && <td data-staff-stalled={p.id} className="px-(--cell-px) py-(--cell-py) text-right tabular-nums">{Number.isFinite(p.reviewsStalled) ? p.reviewsStalled : <Missing />}</td>}
                    <td className="px-(--cell-px) py-(--cell-py) text-right">{count(p, p.deliverablesDue, 'deliverables')}</td>
                    <td className="whitespace-nowrap px-(--cell-px) py-(--cell-py) text-xs text-muted-foreground">{p.lastActivityAt ? ago(p.lastActivityAt) : t('common.dash')}</td>
                    {showCapacity && <td className={cn(tdCls, 'whitespace-nowrap')}><CapacityFigure cell={capacity(p.id)} /></td>}
                    <td className="whitespace-nowrap px-(--cell-px) py-(--cell-py) text-xs">{roleChips(p.roles) || t('common.dash')}</td>
                  </tr>
                  {open.has(p.id) && <tr className="border-t bg-muted"><td /><td colSpan={columns.length + 1 + Number(showCapacity)} className="px-(--cell-px) py-3"><Assignments person={p} scope={f.scope} onChanged={() => q.refetch()} /></td></tr>}
                </Fragment>
              ))}
            </tbody>
          </table>
        </TableRegion>
      )}
      </section>
    </Page>
  )
}

function Assignments({ person, scope, onChanged }: { person: StaffRow; scope?: string; onChanged: () => void }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['staff', person.id, 'assignments', scope], queryFn: () => typedRequest('get', '/api/v1/staff/{userId}/assignments', { params: { userId: person.id }, query: { scope } }) })
  const [assigning, setAssigning] = useState(false)
  const [removing, setRemoving] = useState<Assignment | null>(null)
  const reload = () => { invalidateChange(qc, 'Member'); onChanged() }
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <ErrorBanner error={q.error} />
  return (
    <div className="space-y-2">
      {q.data.length === 0 ? <p className="text-sm text-muted-foreground">{t('staff.noAssignments')}</p> : (
        <ul className="divide-y rounded-lg border bg-card">
          {q.data.map((a) => (
            <li key={a.memberId} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5 text-sm">
              <Link to={`/projects/${a.projectNumber}`} className="flex min-w-0 flex-1 items-center gap-2 hover:underline">
                <AccentDot id={a.projectId} /><Key>{a.projectNumber}</Key><span className="break-words">{a.name}</span></Link>
              <HealthPill health={a.reportedHealth} />
              <span className="text-xs">{a.roles.map((r) => runtimeText(`role.${r}`)).join(', ')}{a.leads.length > 0 && ` (${a.leads.join(', ')})`}</span>
              <span className="text-xs text-muted-foreground">{a.primaryDiscipline ?? t('common.dash')} · {t('staff.added', { date: fmtDate(a.addedAt) })}</span>
              <span className="text-xs tabular-nums">{t('staff.openOverdue', { open: a.open, overdue: a.overdue })}</span>
              <span className="text-xs text-muted-foreground">{a.nextDue ? `${a.nextDue.key} ${fmtDate(a.nextDue.dueDate)}` : ''}</span>
              <span className="text-xs text-muted-foreground">{tv(a.status)}</span>
              {a.canRemove && <Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => setRemoving(a)}>{t('staff.remove')}</Button>}
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={() => setAssigning(true)}><UserPlus className="size-3.5" />{t('staff.assign')}</Button>
        <Button size="sm" variant="ghost" asChild><Link to={`/people/${person.id}/reassign-work`}>{t('reassign.action')}</Link></Button>
      </div>
      {assigning && <AssignDialog person={person} scope={scope} onClose={(ok) => { setAssigning(false); if (ok) { q.refetch(); reload() } }} />}
      {removing && <RemoveDialog person={person} scope={scope} a={removing} onClose={(ok) => { setRemoving(null); if (ok) { q.refetch(); reload() } }} />}
    </div>
  )
}

/** ASG-10: a supervisor adds a direct report to a Setup, Active or On Hold project they can view, as Team Member with a primary discipline. */
function AssignDialog({ person, scope, onClose }: { person: StaffRow; scope?: string; onClose: (ok: boolean) => void }) {
  const projects = useQuery({ queryKey: ['projects', 'staffable'], queryFn: () => typedRequest('get', '/api/v1/projects', { query: { Status: 'Setup,Active,On Hold', PageSize: 200 } }) })
  const [number, setNumber] = useState('')
  const [discipline, setDiscipline] = useState('')
  const detail = useProject(number || undefined)
  const list = projects.data?.items ?? []
  const titleParts = t('staff.assignTitle', { name: '\uFFFC' }).split('\uFFFC')
  const title = person.id && person.displayName.trim() && titleParts.length === 2
    ? <>{titleParts[0]}<PersonPanelTrigger userId={person.id} name={person.displayName} active={person.isActive} staffScope={scope} focusKey={`staff:${scope ?? 'default'}:${person.id}:assign:title`}>{person.displayName}</PersonPanelTrigger>{titleParts[1]}</>
    : t('staff.assignTitle', { name: person.displayName })
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={title} body={t('staff.assignBody')}
      confirmLabel={t('staff.assign')} busy={!number || !discipline}
      onConfirm={async () => {
        await typedRequest('post', '/api/v1/projects/{id}/members', { params: { id: detail.data!.id }, body: { userId: person.id, roles: ['TeamMember'], primaryDisciplineId: discipline } })
        toast.success(t('staff.assigned', { name: person.displayName, number })); onClose(true)
      }}>
      <Field label={t('nav.projects')} htmlFor="as-project">
        <select id="as-project" className={selectCls} value={number} onChange={(e) => { setNumber(e.target.value); setDiscipline('') }}>
          <option value="">{t('common.selectPlaceholder')}</option>{list.map((p) => <option key={p.id} value={p.projectNumber}>{p.projectNumber} {p.name} ({tv(p.status)})</option>)}
        </select>
      </Field>
      <Field label={t('field.PrimaryDisciplineId')} htmlFor="as-disc">
        <select id="as-disc" className={selectCls} value={discipline} disabled={!detail.data} onChange={(e) => setDiscipline(e.target.value)}>
          <option value="">{t('common.selectPlaceholder')}</option>{detail.data?.disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
      </Field>
    </ConfirmDialog>
  )
}

/** TM-04, E-29: open work is reassigned within the project or left flagged, with a reason; the PM is told. */
function RemoveDialog({ person, scope, a, onClose }: { person: StaffRow; scope?: string; a: Assignment; onClose: (ok: boolean) => void }) {
  const items = useQuery({ queryKey: ['open-items', a.memberId], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/members/{memberId}/open-items', { params: { id: a.projectId, memberId: a.memberId } }) })
  const [to, setTo] = useState<string | null>(null)
  const titleParts = t('staff.removeTitle', { name: '\uFFFC', number: a.projectNumber }).split('\uFFFC')
  const title = person.id && person.displayName.trim() && titleParts.length === 2
    ? <>{titleParts[0]}<PersonPanelTrigger userId={person.id} name={person.displayName} active={person.isActive} staffScope={scope} focusKey={`staff:${scope ?? 'default'}:${person.id}:member:${a.memberId}:remove:title`}>{person.displayName}</PersonPanelTrigger>{titleParts[1]}</>
    : t('staff.removeTitle', { name: person.displayName, number: a.projectNumber })
  return (
    <ConfirmDialog open destructive reason onOpenChange={(o) => !o && onClose(false)} title={title} confirmLabel={t('staff.remove')}
      body={items.data ? (items.data.count > 0 ? t('staff.openItems', { n: items.data.count }) : t('staff.noOpenItems')) : undefined}
      onConfirm={async (reason) => { await typedRequest('delete', '/api/v1/projects/{id}/members/{memberId}', { params: { id: a.projectId, memberId: a.memberId }, query: { reassignTo: to ?? undefined, reason }, ifMatch: a.rowVersion }); toast.success(t('staff.removed')); onClose(true) }}>
      {items.data && items.data.count > 0 && (
        <Field label={t('staff.reassignTo')} hint={t('staff.reassignHint')}><PeoplePicker value={to} onChange={setTo} exclude={[person.id]} /></Field>
      )}
    </ConfirmDialog>
  )
}
