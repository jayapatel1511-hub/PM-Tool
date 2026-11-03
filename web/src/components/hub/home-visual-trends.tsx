import type { ReactNode } from 'react'
import type { components } from '@/gen/api'
import { TableRegion } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatReferenceTime } from '@/components/hub/title-block'
import { useMe } from '@/lib/auth-context'
import { t } from '@/lib/i18n'
import './home-visual-trends.css'

type Trends = components['schemas']['HomeTrendsRead']
type Metric = components['schemas']['HomeTrendMetric']

function label(id: string, dueDays: number) {
  if (id === 'ActiveProjects') return t('home.activeProjects')
  if (id === 'OpenTasks') return t('home.openTasks')
  if (id === 'OverdueTasks') return t('home.overdueTasks')
  if (id === 'DueWithin') return t('home.dueWithin', { n: dueDays })
  return t('home.onTrack')
}

const value = (metric: Metric, v?: number | null) => v == null ? t('common.unavailable') : `${v}${metric.id === 'OnTrack' ? '%' : ''}`
function change(metric: Metric) {
  if (metric.delta == null || metric.state !== 'Comparable') return t('home.trends.unavailable')
  const n = Math.abs(metric.delta)
  return `${metric.delta > 0 ? '↑' : metric.delta < 0 ? '↓' : '±'} ${n}${metric.id === 'OnTrack' ? ` ${t('home.trends.pp')}` : ''}`
}

export function HomeTrendChip({ metric }: { metric?: Metric }) {
  if (!metric) return null
  return <span className="home-trend-chip" data-trend-metric={metric.id} data-trend-state={metric.state} data-trend-tone={metric.tone}>{change(metric)}</span>
}

/** The existing card focus/hover definition also describes its comparison, so no button is nested in a whole-card link. */
export function HomeTrendDefinition({ definition, metric, trends }: { definition?: ReactNode; metric?: Metric; trends?: Trends | null }) {
  return <>{definition}{metric && trends && <span className="mt-2 block">{metric.state === 'Comparable'
    ? t('home.trends.definition', { current: value(metric, metric.current), previous: value(metric, metric.previous), included: trends.projects.length, excluded: trends.excludedProjects })
    : t('home.trends.unavailable')} {t('home.trends.inspectHint')}</span>}</>
}

export function HomeTrendReferences({ trends }: { trends?: Trends | null }) {
  const me = useMe()
  if (!trends?.compareFrom) return null
  return <p data-home-trend-reference className="text-xs text-muted-foreground">{t('home.trends.compareWith')} {formatReferenceTime(trends.compareFrom, me.settings.orgTimeZone)}
    {trends.compareThrough && trends.compareThrough !== trends.compareFrom && <> – {formatReferenceTime(trends.compareThrough, me.settings.orgTimeZone)}</>}</p>
}

export function HomeTrendInspection({ trends, dueDays, open, onClose }: { trends: Trends; dueDays: number; open: boolean; onClose: () => void }) {
  const me = useMe()
  return <Dialog open={open} onOpenChange={next => !next && onClose()}><DialogContent className="max-h-[85dvh] max-w-3xl overflow-y-auto">
    <DialogHeader><DialogTitle>{t('home.trends.details')}</DialogTitle><DialogDescription>{t('home.trends.basis', { target: trends.targetDate, days: trends.compareDays, window: dueDays })}</DialogDescription></DialogHeader>
    <p className="text-sm">{t('home.trends.cohort', { included: trends.projects.length, excluded: trends.excludedProjects })}</p>
    <TableRegion role="region" aria-label={t('home.trends.details')} className="rounded-none border-0"><table className="w-full min-w-[32rem] text-left text-sm"><thead><tr>
      <th scope="col" className="p-2">{t('home.trends.metric')}</th><th scope="col" className="p-2">{t('home.trends.current')}</th><th scope="col" className="p-2">{t('home.trends.previous')}</th><th scope="col" className="p-2">{t('home.trends.change')}</th>
    </tr></thead><tbody>{trends.metrics.map(metric => <tr key={metric.id} className="border-t">
      <th scope="row" className="p-2 font-medium">{label(metric.id, dueDays)}</th><td className="p-2 font-mono">{value(metric, metric.current)}</td><td className="p-2 font-mono">{value(metric, metric.previous)}</td><td className="p-2"><HomeTrendChip metric={metric} /></td>
    </tr>)}</tbody></table></TableRegion>
    <p className="text-xs text-muted-foreground">{t('home.trends.exclusions')}</p>
    {trends.metrics.some(metric => metric.reason === 'FilteredScope') && <p className="text-sm">{t('home.trends.filteredScope')}</p>}
    {trends.projects.length > 0 && <TableRegion role="region" aria-label={t('home.trends.projects')} className="rounded-none border-0"><table className="w-full min-w-[26rem] text-left text-sm"><caption className="pb-2 text-left font-medium">{t('home.trends.projects')}</caption><thead><tr><th scope="col" className="p-2">{t('common.project')}</th><th scope="col" className="p-2">{t('home.trends.snapshotDate')}</th><th scope="col" className="p-2">{t('home.trends.capturedAt')}</th></tr></thead><tbody>
      {trends.projects.map(project => <tr key={project.projectNumber} className="border-t"><th scope="row" className="p-2 font-mono font-medium">{project.projectNumber}</th><td className="p-2 font-mono">{project.snapshotDate}</td><td className="p-2 font-mono"><time dateTime={project.capturedAt}>{formatReferenceTime(project.capturedAt, me.settings.orgTimeZone) ?? t('common.unavailable')}</time></td></tr>)}
    </tbody></table></TableRegion>}
    <DialogFooter><Button variant="outline" onClick={onClose}>{t('common.close')}</Button></DialogFooter>
  </DialogContent></Dialog>
}
