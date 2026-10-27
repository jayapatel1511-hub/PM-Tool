import { ProjectStatusNote } from '@/components/hub/project-status-note'
import type { components } from '@/gen/api'
import { useQuery } from '@tanstack/react-query'
import { Monitor } from 'lucide-react'
import { Fragment, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { AccentDot, ActiveFilters, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Notice, Page, PageHelp, SummaryTile, TableRegion, selectCls, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { useOrgPlacement } from '@/hooks/useOrgPlacement'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { ago, fmtDate } from '@/lib/format'
import { runtimeText, t, tv, type TextKey } from '@/lib/i18n'
import type { ProjectRow } from '@/lib/types'
import { cn } from '@/lib/utils'
import { ViewMenu } from '@/components/hub/views'
import type { SavedViewKeys } from '@/gen/registry.gen'

interface Row extends ProjectRow { officeId?: string; projectTypeId?: string; projectType?: string; overdueDeliverables?: number; office?: string; phase?: string; overrideBy?: string | null; overrideActive: boolean; trend: { weekEnding: string; computed?: string | null; reported?: string | null }[] }
// The envelope and tiles are generated; the rows stay hand-typed until the Projects list row (their source) is a record.
type Portfolio = Omit<components['schemas']['PortfolioView'], 'projects'> & { projects: Row[] }

const FILTERS = ['q', 'pmId', 'leadUnitId', 'disciplineId', 'clientId', 'officeId', 'status', 'phaseId', 'health', 'computedHealth', 'projectTypeId', 'submissionWithinDays', 'mine', 'overdueDecisions'] as const satisfies readonly (SavedViewKeys['portfolio'] | 'page' | 'panel' | 'draftAccessReason')[]
const STATUSES = ['Setup', 'Active', 'On Hold', 'Complete']
const HEALTHS = ['Red', 'Yellow', 'Green', 'Grey'] as const
type Health = (typeof HEALTHS)[number]
const HEALTH_TILE_LABELS: Record<Exclude<Health, 'Grey'>, TextKey> = { Red: 'portfolio.red', Yellow: 'portfolio.yellow', Green: 'portfolio.green' }
const GROUP_OPTIONS = [
  { value: 'none', label: 'rcv.groupNone' },
  { value: 'client', label: 'rcv.groupClient' },
  { value: 'office', label: 'rcv.groupOffice' },
  { value: 'projectType', label: 'rcv.groupProjectType' },
] as const
const SWATCH: Record<string, string> = { Red: 'var(--bad)', Yellow: 'var(--warn)', Green: 'var(--ok)', Grey: 'var(--idle)' }
/** Health keeps its status symbol and colour (§13.0); the tiles' pastel accent only groups the three health counts. */
const HEALTH_MARK: Record<Exclude<Health, 'Grey'>, [string, string]> = { Red: ['■', 'text-bad'], Yellow: ['▲', 'text-warn'], Green: ['●', 'text-ok'] }
// Table columns: label key and whether the column is numeric (right-aligned).
const COLS: [TextKey | '', boolean][] = [['portfolio.project', false], ['portfolio.pm', false], ['portfolio.client', false], ['admin.office', false], ['field.PhaseId', false],
  ['portfolio.health', false], ['portfolio.nextMilestone', false], ['portfolio.nextSubmission', false], ['portfolio.overdue', true], ['portfolio.blocked', true],
  ['portfolio.decisionsOverdue', true], ['portfolio.highIssues', true], ['portfolio.attention', true], ['portfolio.trend', false], ['', false]]

/** Portfolio Dashboard (§13.12, FR-PORT-01): which projects need help this week and why, computed and reported health side
 *  by side. Desktop and tablet only (§13.0). */
export function PortfolioPage() {
  const header = { title: t('nav.portfolio'), subtitle: <>{t('sig.pfScope')}<PageHelp>{t('sig.pfHelp')}</PageHelp></> }
  return (
    <DesktopOnly notice={<Page {...header}><Notice icon={Monitor} title={t('app.phoneNotice')}
      action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('portfolio.phoneHint')}</Notice></Page>}>
      <Dashboard {...header} />
    </DesktopOnly>
  )
}

function Dashboard(header: { title: string; subtitle: ReactNode }) {
  const rollups = useFlag('status_rollups')
  const small = useFlag('small_ux')
  const me = useMe()
  const planner = me.capabilities.workload
  // FR-ADR-07: the Admin review strip is for Executives (and Admins), and only while flag.admin_review is on.
  const adminStrip = useFlag('admin_review') && (me.capabilities.admin || me.systemRoles.includes('Executive'))
  const ref = useReference()
  const org = useOrgPlacement([])
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const group = rollups ? sp.get('group') ?? 'none' : 'none'
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)]))
  const q = useQuery({ queryKey: ['portfolio', filters], queryFn: async () => await typedRequest('get', '/api/v1/portfolio', { query: filters }) as Portfolio })
  const refSelect = (key: string, label: string, items: { id: string; name: string; isActive?: boolean }[] | undefined, any: string) => (
    <Field label={label} htmlFor={`pf-${key}`} className="w-full sm:w-44">
      <select id={`pf-${key}`} className={selectCls} value={filters[key] ?? ''} onChange={(e) => set(key, e.target.value)}>
        <option value="">{any}</option>{items?.filter((x) => x.isActive !== false).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
      </select>
    </Field>
  )
  const name = (items: { id: string; name: string }[] | undefined, id: string | null) => items?.find((x) => x.id === id)?.name
  const pmName = q.data?.projects.find((p) => p.pm.id === filters.pmId)?.pm.displayName

  // Active filters as removable tokens (§13.0 Filters); Clear empties the URL as before.
  const tokens = ([
    ['q', t('common.search'), filters.q],
    ['pmId', t('portfolio.pm'), pmName ?? t('portfolio.selectedPm')],
    ['leadUnitId', t('org.leadUnit'), org.units.find(u => u.id === filters.leadUnitId)?.name],
    ['disciplineId', t('common.discipline'), name(ref.data?.disciplines, filters.disciplineId)],
    ['clientId', t('portfolio.client'), name(ref.data?.clients, filters.clientId)],
    ['officeId', t('admin.office'), name(ref.data?.offices, filters.officeId)],
    ['phaseId', t('field.PhaseId'), name(ref.data?.phases, filters.phaseId)],
    ['projectTypeId', t('projects.type'), name(ref.data?.projectTypes, filters.projectTypeId)],
    ['status', t('common.status'), filters.status && tv(filters.status)],
    ['health', t('portfolio.health'), filters.health && runtimeText(`health.${filters.health}`)],
    ['computedHealth', t('projects.filter.computedHealth'), filters.computedHealth?.split(',').map((h) => runtimeText(`health.${h}`)).join(', ')],
    ['submissionWithinDays', t('portfolio.within'), filters.submissionWithinDays],
    ['overdueDecisions', t('portfolio.decisions'), t('common.yes')],
    ['mine', t('portfolio.mine'), filters.mine === 'false' ? t('common.no') : filters.mine],
  ] as const).filter(([k]) => filters[k])
  const clear = () => setSp(new URLSearchParams(), { replace: true })
  const healthLabel = (h: Exclude<Health, 'Grey'>, key: TextKey) => (
    <span className="inline-flex items-center gap-1.5"><span aria-hidden className={HEALTH_MARK[h][1]}>{HEALTH_MARK[h][0]}</span>{t(key)}</span>
  )
  const tiles = q.data?.tiles
  // AC-CNT-01: a health tile counts Active projects by computed health, so it opens exactly those rows.
  const healthLink = (h: string) => { const n = new URLSearchParams(sp); n.set('computedHealth', h); n.set('status', 'Active'); return `?${n}` }
  const rowLinks = (r: Row) => {
    const base = `/projects/${encodeURIComponent(r.projectNumber)}`
    return { overdue: `${base}/tasks?overdue=true`, blocked: `${base}/tasks?blocked=true`, decisions: `${base}/decisions?indicator=overdue`,
      issues: `${base}/issues?indicator=open&severity=High`, critical: `${base}/dashboard?severity=Critical#attention`, warning: `${base}/dashboard?severity=Warning#attention`, deliverables: `${base}/deliverables?indicator=overdue` }
  }
  const groupKey = (r: Row) => group === 'client' ? r.client.id : group === 'office' ? r.officeId ?? '' : group === 'projectType' ? r.projectTypeId ?? '' : ''
  const groupName = (r: Row) => group === 'client' ? r.client.name : group === 'office' ? r.office ?? t('rcv.notSet') : r.projectType ?? name(ref.data?.projectTypes, r.projectTypeId ?? null) ?? t('rcv.notSet')
  const groups = new Map<string, Row[]>()
  for (const r of q.data?.projects ?? []) { const key = groupKey(r); groups.set(key, [...(groups.get(key) ?? []), r]) }
  const grouped = [...groups.entries()].sort(([a, ar], [b, br]) => !a ? 1 : !b ? -1 : groupName(ar[0]).localeCompare(groupName(br[0])))
  const groupLink = (key: string, health?: string) => `?${new URLSearchParams({ ...Object.fromEntries(sp), [group === 'client' ? 'clientId' : group === 'office' ? 'officeId' : 'projectTypeId']: key, ...(health ? { computedHealth: health } : {}) })}`
  return (
    <Page {...header} actions={<>{small && planner && <Button asChild variant="outline"><Link to="/planner?groupBy=homeDiscipline">{t('interaction.capacityByDiscipline')}</Link></Button>}<ViewMenu listType="portfolio" /><ExportMenu path="portfolio/export" params={filters} name="portfolio" /></>}>
      {tiles && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 xl:grid-cols-6">
          <SummaryTile label={t('portfolio.active')} value={tiles.active} accent="blue" hint={t('sig.def.pfActive')} onClick={() => set('status', 'Active')} />
          {(['Red', 'Yellow', 'Green'] as const).map((h) => (
            <SummaryTile key={h} label={healthLabel(h, HEALTH_TILE_LABELS[h])} value={tiles[h.toLowerCase() as 'red']} accent="lavender" hint={t('sig.def.pfHealth', { health: runtimeText(`health.${h}`) })} to={healthLink(h)} />
          ))}
          <SummaryTile label={t('portfolio.submissions')} value={tiles.submissions14} accent="mint" hint={t('sig.def.pfSubmissions')} onClick={() => set('submissionWithinDays', '14')} />
          <SummaryTile label={t('portfolio.decisions')} value={tiles.overdueDecisions} accent="peach" hint={t('sig.def.pfDecisions')} onClick={() => set('overdueDecisions', 'true')} />
        </div>
      )}
      {adminStrip && <AdminReviewStrip />}
      {rollups && <Field label={t('rcv.group')} htmlFor="pf-group" className="w-48"><select id="pf-group" className={selectCls} value={group} onChange={(e) => set('group', e.target.value === 'none' ? null : e.target.value)}>
        {GROUP_OPTIONS.map(({ value, label }) => <option key={value} value={value}>{t(label)}</option>)}
      </select></Field>}
      <FilterBar activeCount={tokens.length}
        search={<Field label={t('common.search')} htmlFor="pf-q">
          <Input id="pf-q" key={filters.q ? 'q' : 'empty'} type="search" placeholder={t('common.search')} defaultValue={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} />
        </Field>}
        active={<ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={(k) => set(k, null)} onClear={clear} />}>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('portfolio.pm')} htmlFor="pf-pm" className="w-full sm:w-52">
            <PeoplePicker id="pf-pm" value={filters.pmId} valueName={pmName} onChange={(v) => set('pmId', v)} placeholder={t('portfolio.anyPm')} label={t('portfolio.pm')} />
          </Field>
          {org.enabled && <Field label={t('org.leadUnit')} htmlFor="pf-lead-unit" className="w-full sm:w-52"><select id="pf-lead-unit" className={selectCls} value={filters.leadUnitId ?? ''} onChange={(e) => set('leadUnitId', e.target.value)}><option value="">{t('org.anyUnit')}</option><option value="00000000-0000-0000-0000-000000000000">{t('organisation.noUnit')}</option>{org.units.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>}
          {refSelect('disciplineId', t('common.discipline'), ref.data?.disciplines, t('projects.anyDiscipline'))}
          {refSelect('clientId', t('portfolio.client'), ref.data?.clients, t('portfolio.anyClient'))}
          {refSelect('officeId', t('admin.office'), ref.data?.offices, t('reports.anyOffice'))}
          {refSelect('phaseId', t('field.PhaseId'), ref.data?.phases, t('portfolio.anyPhase'))}
          {refSelect('projectTypeId', t('projects.type'), ref.data?.projectTypes, t('portfolio.anyType'))}
          <Field label={t('common.status')} htmlFor="pf-status" className="w-full sm:w-48">
            <select id="pf-status" className={selectCls} value={filters.status ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('portfolio.liveStatuses')}</option>{STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
            </select>
          </Field>
          <Field label={t('portfolio.health')} htmlFor="pf-health" className="w-full sm:w-40">
            <select id="pf-health" className={selectCls} value={filters.health ?? ''} onChange={(e) => set('health', e.target.value)}>
              <option value="">{t('portfolio.anyHealth')}</option>{HEALTHS.map((h) => <option key={h} value={h}>{t(`health.${h}`)}</option>)}
            </select>
          </Field>
          <Field label={t('portfolio.within')} htmlFor="pf-within" className="w-full sm:w-48">
            <Input id="pf-within" type="number" min={1} className="tabular-nums" value={filters.submissionWithinDays ?? ''} onChange={(e) => set('submissionWithinDays', e.target.value)} />
          </Field>
          {q.data && (q.data.ownOnly || filters.mine) && (
            <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={q.data.ownOnly} onCheckedChange={(c) => set('mine', c ? null : 'false')} />{t('portfolio.mine')}</label>
          )}
        </div>
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={8} /></div> : !q.data ? null : q.data.projects.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('portfolio.empty')}</Empty></div>
      ) : (
        <TableRegion>
          <table className="w-full text-sm">
            <caption className="sr-only">{t('nav.portfolio')}</caption>
            <thead className="bg-muted">
              <tr>{(rollups ? [...COLS.slice(0, 12), ['rcv.overdueDeliverables', true] as [TextKey, boolean], ...COLS.slice(12)] : COLS).map(([h, numeric], i) => <th key={i} scope="col" className={cn(thCls, numeric && 'text-right')}>{h ? t(h) : <span className="sr-only">{t('common.actions')}</span>}</th>)}</tr>
            </thead>
            <tbody>
              {(group === 'none' ? [['', q.data.projects] as [string, Row[]]] : grouped).map(([key, rows]) => <Fragment key={key}>
                {group !== 'none' && <tr className="border-t bg-muted" data-portfolio-group={key}><th scope="rowgroup" colSpan={8} className={thCls}>
                  {groupName(rows[0])} · {key ? <Link to={groupLink(key)} className="underline underline-offset-2">{rows.length} {t('nav.projects')}</Link> : <span title={t('rcv.notSetDefinition')}>{rows.length} {t('nav.projects')}</span>}
                  <span className="ml-3 inline-flex gap-3">{(['Red', 'Yellow', 'Green'] as const).map((health) => <span key={health}>{key ? <Link to={groupLink(key, health)}>{t(`health.${health}`)} {rows.filter(r => r.computedHealth === health).length}</Link> : <span title={t('rcv.notSetDefinition')}>{t(`health.${health}`)} {rows.filter(r => r.computedHealth === health).length}</span>}</span>)}</span>
                </th>{(['overdueTasks', 'blockedTasks', 'overdueDecisions', 'highIssues', 'overdueDeliverables'] as const).map((column) => <td key={column} className={cn(tdCls, 'text-right tabular-nums')}>{key ? <Link to={groupLink(key)} aria-label={`${rows.reduce((sum,r)=>sum+(r[column]??0),0)} · ${t('rcv.subtotalDefinition')}`} title={t('rcv.subtotalDefinition')} className="underline underline-offset-2">{rows.reduce((sum,r)=>sum+(r[column]??0),0)}</Link> : <span title={t('rcv.notSetDefinition')}>{rows.reduce((sum,r)=>sum+(r[column]??0),0)}</span>}</td>)}
                <td className={cn(tdCls, 'text-right tabular-nums')}><span title={t('rcv.subtotalDefinition')}>■ {rows.reduce((sum,r)=>sum+r.attentionCritical,0)} · ▲ {rows.reduce((sum,r)=>sum+r.attentionWarning,0)}</span></td><td colSpan={2} /></tr>}
              {rows.map((r) => (
                <tr key={r.id} className="border-t hover:bg-muted">
                  <td className={cn(tdCls, 'min-w-60')}>
                    {r.statusNote && <details className="mb-2"><summary>{t('statusNote.title')}</summary><ProjectStatusNote note={r.statusNote} /></details>}
                    <div className="flex items-start gap-2">
                      <AccentDot id={r.id} className="mt-1.5" />
                      <div className="min-w-0">
                        <Link to={`/projects/${encodeURIComponent(r.projectNumber)}/dashboard`} className="hover:underline"><Key>{r.projectNumber}</Key> <span className="font-semibold">{r.name}</span></Link>
                        {r.status !== 'Active' && <div className="mt-1"><StatusPill status={r.status} /></div>}
                      </div>
                    </div>
                  </td>
                  <td className={tdCls}><span className="flex items-center gap-2 whitespace-nowrap"><Avatar id={r.pm.id} name={r.pm.displayName} />{r.pm.displayName}</span></td>
                  <td className={tdCls}>{r.client.name}</td>
                  <td className={cn(tdCls, 'whitespace-nowrap')}>{r.office || <Missing />}</td>
                  <td className={cn(tdCls, 'whitespace-nowrap')}>{r.phase || <Missing />}</td>
                  <td className={cn(tdCls, 'min-w-44')}><Health r={r} /></td>
                  <td className={cn(tdCls, 'min-w-40')}>{r.nextMilestone ? <><div>{r.nextMilestone.name}</div>
                    <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs/[18px] text-muted-foreground tabular-nums">{fmtDate(r.nextMilestone.date)} {r.nextMilestone.status && <StatusPill status={r.nextMilestone.status} />}</div></> : <Missing />}</td>
                  <td className={cn(tdCls, 'min-w-36')}>{r.nextSubmission ? <><div>{r.nextSubmission.name}</div><div className="mt-0.5 text-xs/[18px] text-muted-foreground tabular-nums">{fmtDate(r.nextSubmission.date)}</div></> : <Missing />}</td>
                  <Count n={r.overdueTasks} to={rowLinks(r).overdue} bad />
                  <Count n={r.blockedTasks} to={rowLinks(r).blocked} bad />
                  <Count n={r.overdueDecisions} to={rowLinks(r).decisions} bad />
                  <Count n={r.highIssues} to={rowLinks(r).issues} />
                  {rollups && <Count n={r.overdueDeliverables ?? 0} to={rowLinks(r).deliverables} bad />}
                  <td className={cn(tdCls, 'whitespace-nowrap text-right tabular-nums')}>
                    <Link to={rowLinks(r).critical} className={cn(r.attentionCritical > 0 && 'font-semibold text-bad')}><span aria-hidden>■</span><span className="sr-only">{tv('Critical')}</span> {r.attentionCritical}</Link>
                    <Link to={rowLinks(r).warning} className={cn('ml-3', r.attentionWarning > 0 && 'font-semibold text-warn')}><span aria-hidden>▲</span><span className="sr-only">{tv('Warning')}</span> {r.attentionWarning}</Link>
                  </td>
                  <td className={tdCls}><Trend points={r.trend} /></td>
                  <td className={cn(tdCls, 'whitespace-nowrap')}><Link className="text-primary underline underline-offset-4 hover:text-foreground" to={`/projects/${encodeURIComponent(r.projectNumber)}/coordination`}>{t('portfolio.coordination')}</Link></td>
                </tr>
              ))}</Fragment>)}
            </tbody>
          </table>
        </TableRegion>
      )}
    </Page>
  )
}

/** FR-ADR-07: the last recorded Admin review's date and counts, nothing else; it never opens a list. */
function AdminReviewStrip() {
  const q = useQuery({ queryKey: ['portfolio', 'admin-review'], queryFn: () => typedRequest('get', '/api/v1/portfolio/admin-review', {}) })
  if (!q.data) return null
  return (
    <section aria-label={t('portfolio.adminReview')} data-admin-review-strip className="flex flex-wrap items-baseline gap-x-5 gap-y-1 rounded-lg border bg-card px-4 py-2.5 text-sm">
      <h2 className="font-semibold">{t('portfolio.adminReview')}</h2>
      {q.data.reviewedAt
        ? <><span className="text-muted-foreground">{t('portfolio.adminReviewLast', { date: fmtDate(q.data.reviewedAt) })}</span>
          {q.data.checks.map((c) => <span key={c.key}>{c.title} <span className="font-semibold tabular-nums">{c.count}</span></span>)}</>
        : <span className="text-muted-foreground">{t('portfolio.adminReviewNone')}</span>}
    </section>
  )
}

/** A count column: right-aligned figures; a non-zero overdue or blocked count is emphasised (its header names it). */
function Count({ n, bad, to }: { n: number; bad?: boolean; to: string }) {
  return <td className={cn(tdCls, 'text-right tabular-nums', bad && n > 0 && 'font-semibold text-bad')}><Link to={to} className="hover:underline">{n}</Link></td>
}

/** §16.4: when they differ, both values are shown, with the PM's note and its age. */
function Health({ r }: { r: Row }) {
  const differs = r.reportedHealth !== r.computedHealth
  return (
    <div className="space-y-1">
      <Why reasons={r.healthReasons} title={`${r.projectNumber} · ${t('health.whyTitle', { health: runtimeText(`health.${r.computedHealth}`) })}`}>
        {differs ? <span className="flex flex-wrap gap-1"><HealthPill health={r.computedHealth} label={t('health.computed')} /><HealthPill health={r.reportedHealth} label={t('health.reported')} /></span>
          : <HealthPill health={r.reportedHealth} />}
      </Why>
      {differs && r.healthOverrideNote && <p className="max-w-56 text-xs/[18px] text-muted-foreground">“{r.healthOverrideNote}” — {t('portfolio.noteBy', { name: r.overrideBy ?? '', when: ago(r.healthOverrideAt) })}</p>}
    </div>
  )
}

/** FR-HLT-03: eight weeks of computed health, oldest first. */
function Trend({ points }: { points: Row['trend'] }) {
  const label = points.map((p) => `${fmtDate(p.weekEnding)}: ${p.computed ? runtimeText(`health.${p.computed}`) : t('portfolio.noSnapshot')}`).join('; ')
  return (
    <span className="flex gap-0.5" role="img" aria-label={t('portfolio.trendLabel', { list: label })} title={label}>
      {points.map((p) => <span key={p.weekEnding} className={cn('inline-block h-4 w-2 rounded-[2px]', !p.computed && 'border border-dashed border-input')} style={p.computed ? { background: SWATCH[p.computed] } : undefined} />)}
    </span>
  )
}
