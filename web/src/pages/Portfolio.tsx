import { useQuery } from '@tanstack/react-query'
import { Monitor } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { AccentDot, ActiveFilters, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Notice, Page, SummaryTile, TableRegion, selectCls, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { get, qs } from '@/lib/api'
import { ago, fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { ProjectRow } from '@/lib/types'
import { cn } from '@/lib/utils'
import { ViewMenu } from '@/components/hub/views'

interface Row extends ProjectRow { office?: string; phase?: string; overrideBy?: string | null; overrideActive: boolean; trend: { weekEnding: string; computed?: string | null; reported?: string | null }[] }
interface Portfolio { ownOnly: boolean; tiles: { active: number; red: number; yellow: number; green: number; submissions14: number; overdueDecisions: number }; projects: Row[] }

const FILTERS = ['q', 'pmId', 'disciplineId', 'clientId', 'officeId', 'status', 'phaseId', 'health', 'projectTypeId', 'submissionWithinDays', 'mine'] as const
const STATUSES = ['Setup', 'Active', 'On Hold', 'Complete']
const HEALTHS = ['Red', 'Yellow', 'Green', 'Grey']
const SWATCH: Record<string, string> = { Red: 'var(--bad)', Yellow: 'var(--warn)', Green: 'var(--ok)', Grey: 'var(--idle)' }
/** Health keeps its status symbol and colour (§13.0); the tiles' pastel accent only groups the three health counts. */
const HEALTH_MARK: Record<string, [string, string]> = { Red: ['■', 'text-bad'], Yellow: ['▲', 'text-warn'], Green: ['●', 'text-ok'] }
// Table columns: label key and whether the column is numeric (right-aligned).
const COLS: [string, boolean][] = [['portfolio.project', false], ['portfolio.pm', false], ['portfolio.client', false], ['admin.office', false], ['field.PhaseId', false],
  ['portfolio.health', false], ['portfolio.nextMilestone', false], ['portfolio.nextSubmission', false], ['portfolio.overdue', true], ['portfolio.blocked', true],
  ['portfolio.decisionsOverdue', true], ['portfolio.highIssues', true], ['portfolio.attention', true], ['portfolio.trend', false], ['', false]]

/** Portfolio Dashboard (§13.12, FR-PORT-01): which projects need help this week and why, computed and reported health side
 *  by side. Desktop and tablet only (§13.0). */
export function PortfolioPage() {
  const header = { title: t('nav.portfolio'), subtitle: t('portfolio.subtitle') }
  return (
    <DesktopOnly notice={<Page {...header}><Notice icon={Monitor} title={t('app.phoneNotice')}
      action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('portfolio.phoneHint')}</Notice></Page>}>
      <Dashboard {...header} />
    </DesktopOnly>
  )
}

function Dashboard(header: { title: string; subtitle: string }) {
  const ref = useReference()
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)]))
  const q = useQuery({ queryKey: ['portfolio', filters], queryFn: () => get<Portfolio>(`portfolio${qs(filters)}`) })
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
    ['disciplineId', t('common.discipline'), name(ref.data?.disciplines, filters.disciplineId)],
    ['clientId', t('portfolio.client'), name(ref.data?.clients, filters.clientId)],
    ['officeId', t('admin.office'), name(ref.data?.offices, filters.officeId)],
    ['phaseId', t('field.PhaseId'), name(ref.data?.phases, filters.phaseId)],
    ['projectTypeId', t('projects.type'), name(ref.data?.projectTypes, filters.projectTypeId)],
    ['status', t('common.status'), filters.status && tv(filters.status)],
    ['health', t('portfolio.health'), filters.health && t(`health.${filters.health}`)],
    ['submissionWithinDays', t('portfolio.within'), filters.submissionWithinDays],
    ['mine', t('portfolio.mine'), filters.mine === 'false' ? t('common.no') : filters.mine],
  ] as const).filter(([k]) => filters[k])
  const clear = () => setSp(new URLSearchParams(), { replace: true })
  const healthLabel = (h: string, key: string) => (
    <span className="inline-flex items-center gap-1.5"><span aria-hidden className={HEALTH_MARK[h][1]}>{HEALTH_MARK[h][0]}</span>{t(key)}</span>
  )
  const tiles = q.data?.tiles
  return (
    <Page {...header} actions={<><ViewMenu listType="portfolio" /><ExportMenu path="portfolio/export" params={filters} name="portfolio" /></>}>
      {tiles && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 xl:grid-cols-6">
          <SummaryTile label={t('portfolio.active')} value={tiles.active} accent="blue" onClick={() => set('status', 'Active')} />
          <SummaryTile label={healthLabel('Red', 'portfolio.red')} value={tiles.red} accent="lavender" onClick={() => set('health', 'Red')} />
          <SummaryTile label={healthLabel('Yellow', 'portfolio.yellow')} value={tiles.yellow} accent="lavender" onClick={() => set('health', 'Yellow')} />
          <SummaryTile label={healthLabel('Green', 'portfolio.green')} value={tiles.green} accent="lavender" onClick={() => set('health', 'Green')} />
          <SummaryTile label={t('portfolio.submissions')} value={tiles.submissions14} accent="mint" onClick={() => set('submissionWithinDays', '14')} />
          <SummaryTile label={t('portfolio.decisions')} value={tiles.overdueDecisions} accent="peach" />
        </div>
      )}
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.search')} htmlFor="pf-q" className="w-full sm:w-52">
            <Input id="pf-q" key={filters.q ? 'q' : 'empty'} type="search" defaultValue={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} />
          </Field>
          <Field label={t('portfolio.pm')} htmlFor="pf-pm" className="w-full sm:w-52">
            <PeoplePicker id="pf-pm" value={filters.pmId} valueName={pmName} onChange={(v) => set('pmId', v)} placeholder={t('portfolio.anyPm')} label={t('portfolio.pm')} />
          </Field>
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
        <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={(k) => set(k, null)} onClear={clear} />
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={8} /></div> : !q.data ? null : q.data.projects.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('portfolio.empty')}</Empty></div>
      ) : (
        <TableRegion>
          <table className="w-full text-sm">
            <caption className="sr-only">{t('nav.portfolio')}</caption>
            <thead className="bg-muted">
              <tr>{COLS.map(([h, numeric], i) => <th key={i} scope="col" className={cn(thCls, numeric && 'text-right')}>{h ? t(h) : <span className="sr-only">{t('common.actions')}</span>}</th>)}</tr>
            </thead>
            <tbody>
              {q.data.projects.map((r) => (
                <tr key={r.id} className="border-t hover:bg-muted">
                  <td className={cn(tdCls, 'min-w-60')}>
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
                  <Count n={r.overdueTasks} bad />
                  <Count n={r.blockedTasks} bad />
                  <Count n={r.overdueDecisions} bad />
                  <Count n={r.highIssues} />
                  <td className={cn(tdCls, 'whitespace-nowrap text-right tabular-nums')}>
                    <span className={cn(r.attentionCritical > 0 && 'font-semibold text-bad')}><span aria-hidden>■</span><span className="sr-only">{tv('Critical')}</span> {r.attentionCritical}</span>
                    <span className={cn('ml-3', r.attentionWarning > 0 && 'font-semibold text-warn')}><span aria-hidden>▲</span><span className="sr-only">{tv('Warning')}</span> {r.attentionWarning}</span>
                  </td>
                  <td className={tdCls}><Trend points={r.trend} /></td>
                  <td className={cn(tdCls, 'whitespace-nowrap')}><Link className="text-primary underline underline-offset-4 hover:text-foreground" to={`/projects/${encodeURIComponent(r.projectNumber)}/coordination`}>{t('portfolio.coordination')}</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableRegion>
      )}
    </Page>
  )
}

/** A count column: right-aligned figures; a non-zero overdue or blocked count is emphasised (its header names it). */
function Count({ n, bad }: { n: number; bad?: boolean }) {
  return <td className={cn(tdCls, 'text-right tabular-nums', bad && n > 0 && 'font-semibold text-bad')}>{n}</td>
}

/** §16.4: when they differ, both values are shown, with the PM's note and its age. */
function Health({ r }: { r: Row }) {
  const differs = r.reportedHealth !== r.computedHealth
  return (
    <div className="space-y-1">
      <Why reasons={r.healthReasons} title={`${r.projectNumber} · ${t('health.whyTitle', { health: t(`health.${r.computedHealth}`) })}`}>
        {differs ? <span className="flex flex-wrap gap-1"><HealthPill health={r.computedHealth} label={t('health.computed')} /><HealthPill health={r.reportedHealth} label={t('health.reported')} /></span>
          : <HealthPill health={r.reportedHealth} />}
      </Why>
      {differs && r.healthOverrideNote && <p className="max-w-56 text-xs/[18px] text-muted-foreground">“{r.healthOverrideNote}” — {t('portfolio.noteBy', { name: r.overrideBy ?? '', when: ago(r.healthOverrideAt) })}</p>}
    </div>
  )
}

/** FR-HLT-03: eight weeks of computed health, oldest first. */
function Trend({ points }: { points: Row['trend'] }) {
  const label = points.map((p) => `${fmtDate(p.weekEnding)}: ${p.computed ? t(`health.${p.computed}`) : t('portfolio.noSnapshot')}`).join('; ')
  return (
    <span className="flex gap-0.5" role="img" aria-label={t('portfolio.trendLabel', { list: label })} title={label}>
      {points.map((p) => <span key={p.weekEnding} className={cn('inline-block h-4 w-2 rounded-[2px]', !p.computed && 'border border-dashed border-input')} style={p.computed ? { background: SWATCH[p.computed] } : undefined} />)}
    </span>
  )
}
