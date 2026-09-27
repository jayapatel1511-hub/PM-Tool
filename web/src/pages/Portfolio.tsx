import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { PeoplePicker } from '@/components/hub/people'
import { HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
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

/** Portfolio Dashboard (§13.12, FR-PORT-01): which projects need help this week and why, computed and reported health side by side. */
export function PortfolioPage() {
  const ref = useReference()
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)]))
  const q = useQuery({ queryKey: ['portfolio', filters], queryFn: () => get<Portfolio>(`portfolio${qs(filters)}`) })
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  const refSelect = (key: string, items: { id: string; name: string; isActive?: boolean }[] | undefined, any: string) => (
    <select className={cn(sel, 'max-w-44')} value={filters[key] ?? ''} onChange={(e) => set(key, e.target.value)} aria-label={any}>
      <option value="">{any}</option>{items?.filter((x) => x.isActive !== false).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
    </select>
  )
  const tiles = q.data?.tiles
  return (
    <Page title={t('nav.portfolio')} subtitle={t('portfolio.subtitle')} actions={<><ViewMenu listType="portfolio" /><ExportMenu path="portfolio/export" params={filters} name="portfolio" /></>}>
      {tiles && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <Tile label={t('portfolio.active')} value={tiles.active} onClick={() => set('status', 'Active')} />
          <Tile label={t('portfolio.red')} value={tiles.red} tone="Red" onClick={() => set('health', 'Red')} />
          <Tile label={t('portfolio.yellow')} value={tiles.yellow} tone="Yellow" onClick={() => set('health', 'Yellow')} />
          <Tile label={t('portfolio.green')} value={tiles.green} tone="Green" onClick={() => set('health', 'Green')} />
          <Tile label={t('portfolio.submissions')} value={tiles.submissions14} onClick={() => set('submissionWithinDays', '14')} />
          <Tile label={t('portfolio.decisions')} value={tiles.overdueDecisions} />
        </div>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <Input key={filters.q ? 'q' : 'empty'} type="search" className="h-8 w-48" placeholder={t('common.search')} defaultValue={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} aria-label={t('common.search')} />
        <div className="w-44"><PeoplePicker value={filters.pmId} onChange={(v) => set('pmId', v)} placeholder={t('portfolio.anyPm')} label={t('portfolio.pm')} /></div>
        {refSelect('disciplineId', ref.data?.disciplines, t('projects.anyDiscipline'))}
        {refSelect('clientId', ref.data?.clients, t('portfolio.anyClient'))}
        {refSelect('officeId', ref.data?.offices, t('reports.anyOffice'))}
        {refSelect('phaseId', ref.data?.phases, t('portfolio.anyPhase'))}
        {refSelect('projectTypeId', ref.data?.projectTypes, t('portfolio.anyType'))}
        <select className={sel} value={filters.status ?? ''} onChange={(e) => set('status', e.target.value)} aria-label={t('common.status')}>
          <option value="">{t('portfolio.liveStatuses')}</option>{STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
        </select>
        <select className={sel} value={filters.health ?? ''} onChange={(e) => set('health', e.target.value)} aria-label={t('portfolio.health')}>
          <option value="">{t('portfolio.anyHealth')}</option>{HEALTHS.map((h) => <option key={h} value={h}>{t(`health.${h}`)}</option>)}
        </select>
        <label className="text-xs text-muted-foreground">{t('portfolio.within')} <Input type="number" min={1} className="inline-flex h-8 w-20" value={filters.submissionWithinDays ?? ''} onChange={(e) => set('submissionWithinDays', e.target.value)} /></label>
        {q.data && (q.data.ownOnly || filters.mine) && (
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={q.data.ownOnly} onCheckedChange={(c) => set('mine', c ? null : 'false')} />{t('portfolio.mine')}</label>
        )}
        {FILTERS.some((k) => sp.has(k)) && <button type="button" className="px-2 text-xs text-muted-foreground hover:text-foreground" onClick={() => setSp(new URLSearchParams(), { replace: true })}>{t('common.clear')}</button>}
      </div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={8} /> : q.data!.projects.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('portfolio.empty')}</Empty></div> : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-[13px]">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr>{['portfolio.project', 'portfolio.pm', 'portfolio.client', 'admin.office', 'field.PhaseId', 'portfolio.health', 'portfolio.nextMilestone', 'portfolio.nextSubmission',
                'portfolio.overdue', 'portfolio.blocked', 'portfolio.decisionsOverdue', 'portfolio.highIssues', 'portfolio.attention', 'portfolio.trend', ''].map((h, i) =>
                <th key={i} scope="col" className="whitespace-nowrap px-3 py-2 font-medium">{h && t(h)}</th>)}</tr>
            </thead>
            <tbody>
              {q.data!.projects.map((r) => (
                <tr key={r.id} className="border-t align-top hover:bg-muted/30">
                  <td className="min-w-[13rem] px-3 py-2"><Link to={`/projects/${encodeURIComponent(r.projectNumber)}/dashboard`} className="hover:underline"><Key>{r.projectNumber}</Key> <span className="font-medium">{r.name}</span></Link>
                    {r.status !== 'Active' && <div className="mt-0.5"><StatusPill status={r.status} /></div>}</td>
                  <td className="whitespace-nowrap px-3 py-2">{r.pm.displayName}</td>
                  <td className="px-3 py-2">{r.client.name}</td>
                  <td className="whitespace-nowrap px-3 py-2">{r.office}</td>
                  <td className="whitespace-nowrap px-3 py-2">{r.phase ?? t('common.dash')}</td>
                  <td className="min-w-[11rem] px-3 py-2"><Health r={r} /></td>
                  <td className="min-w-[9rem] px-3 py-2">{r.nextMilestone ? <><div>{r.nextMilestone.name}</div><div className="flex items-center gap-1 text-xs text-muted-foreground">{fmtDate(r.nextMilestone.date)} {r.nextMilestone.status && <StatusPill status={r.nextMilestone.status} />}</div></> : t('common.dash')}</td>
                  <td className="min-w-[8rem] px-3 py-2">{r.nextSubmission ? <><div>{r.nextSubmission.name}</div><div className="text-xs text-muted-foreground">{fmtDate(r.nextSubmission.date)}</div></> : t('common.dash')}</td>
                  <td className={cn('px-3 py-2 tabular-nums', r.overdueTasks > 0 && 'font-medium text-bad')}>{r.overdueTasks}</td>
                  <td className={cn('px-3 py-2 tabular-nums', r.blockedTasks > 0 && 'font-medium text-bad')}>{r.blockedTasks}</td>
                  <td className={cn('px-3 py-2 tabular-nums', r.overdueDecisions > 0 && 'font-medium text-bad')}>{r.overdueDecisions}</td>
                  <td className="px-3 py-2 tabular-nums">{r.highIssues}</td>
                  <td className="whitespace-nowrap px-3 py-2 text-xs"><span className={cn(r.attentionCritical > 0 && 'font-medium text-bad')}>■ {r.attentionCritical}</span> <span className={cn('ml-1', r.attentionWarning > 0 && 'text-warn')}>▲ {r.attentionWarning}</span></td>
                  <td className="px-3 py-2"><Trend points={r.trend} /></td>
                  <td className="whitespace-nowrap px-3 py-2 text-xs"><Link className="underline" to={`/projects/${encodeURIComponent(r.projectNumber)}/coordination`}>{t('portfolio.coordination')}</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Page>
  )
}

function Tile({ label, value, tone, onClick }: { label: string; value: number; tone?: string; onClick?: () => void }) {
  const body = <><div className="text-2xl font-semibold tabular-nums" style={tone ? { color: SWATCH[tone] } : undefined}>{value}</div><div className="text-xs text-muted-foreground">{label}</div></>
  return onClick
    ? <button type="button" onClick={onClick} className="rounded-lg border bg-card p-3 text-left hover:border-primary">{body}</button>
    : <div className="rounded-lg border bg-card p-3">{body}</div>
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
      {differs && r.healthOverrideNote && <p className="max-w-56 text-xs text-muted-foreground">“{r.healthOverrideNote}” — {t('portfolio.noteBy', { name: r.overrideBy ?? '', when: ago(r.healthOverrideAt) })}</p>}
    </div>
  )
}

/** FR-HLT-03: eight weeks of computed health, oldest first. */
function Trend({ points }: { points: Row['trend'] }) {
  const label = points.map((p) => `${fmtDate(p.weekEnding)}: ${p.computed ? t(`health.${p.computed}`) : t('portfolio.noSnapshot')}`).join('; ')
  return (
    <span className="flex gap-0.5" role="img" aria-label={t('portfolio.trendLabel', { list: label })} title={label}>
      {points.map((p) => <span key={p.weekEnding} className={cn('inline-block h-4 w-2 rounded-[2px]', !p.computed && 'border border-dashed')} style={p.computed ? { background: SWATCH[p.computed] } : undefined} />)}
    </span>
  )
}
