import { useId } from 'react'
import { Link } from 'react-router'
import type { components } from '@/gen/api'
import { ItemMark } from '@/components/hub/item-mark'
import { resolveIdentityColour } from '@/components/hub/identity-colour'
import { scoped, type Scope } from '@/components/hub/workspace'
import { useFlag } from '@/lib/auth-context'
import { t, runtimeText } from '@/lib/i18n'
import './home-visual.css'

type HomeData = components['schemas']['HomeRead']
export type HomeVisualLane = { lane: string; statuses: string; count: number; name: string; def: string; tone: string }

/** One compound path also handles a single nonzero status without a degenerate 360° arc. */
function ringPath(start: number, share: number) {
  const angle = Math.min(share * Math.PI * 2, Math.PI * 2)
  const point = (r: number, a: number) => `${100 + r * Math.cos(a)},${100 + r * Math.sin(a)}`
  if (share >= 1) return `M100,20 A80,80 0 1 1 100,180 A80,80 0 1 1 100,20 M100,46 A54,54 0 1 0 100,154 A54,54 0 1 0 100,46 Z`
  const end = start + angle, large = angle > Math.PI ? 1 : 0
  return `M${point(80, start)} A80,80 0 ${large} 1 ${point(80, end)} L${point(54, end)} A54,54 0 ${large} 0 ${point(54, start)} Z`
}

/** Visible list values remain the chart's text equivalent. Every nonzero slice is a real drill-down link. */
export function HomeStatusDonut({ lanes, scope }: { lanes: HomeVisualLane[]; scope: Scope }) {
  const hatch = `home-hold-${useId().replace(/:/g, '')}`
  const total = lanes.reduce((n, l) => n + l.count, 0)
  let offset = 0
  return <div className="home-status-chart">
    <div className="home-donut">
      <svg viewBox="0 0 200 200" aria-label={t('home.byStatus')} role="group">
        <defs><pattern id={hatch} patternUnits="userSpaceOnUse" width="6" height="6" patternTransform="rotate(135)">
          <rect width="6" height="6" fill="var(--hatch-light, #d5dae3)" /><rect width="3" height="6" fill="var(--hatch-strong, #94a0b4)" />
        </pattern></defs>
        {total === 0 && <circle cx="100" cy="100" r="67" fill="none" stroke="var(--border)" strokeWidth="26" aria-hidden="true" />}
        {lanes.filter(l => l.count > 0).map(l => {
          const start = offset / total * Math.PI * 2 - Math.PI / 2
          offset += l.count
          return <Link key={l.lane} to={scoped(scope, '/tasks', { status: l.statuses })} aria-label={t('home.segment', { lane: l.name, n: l.count })}>
            <path data-home-donut-lane={l.lane} d={ringPath(start, l.count / total)} fill={l.lane === 'On Hold' ? `url(#${hatch})` : l.tone} fillRule="evenodd"><title>{l.def}: {l.count}</title></path>
          </Link>
        })}
      </svg>
      <div className="home-donut__total"><Link to={scoped(scope, '/tasks', { status: lanes.map(l => l.statuses).join(',') })} aria-label={t('home.segment', { lane: t('home.chartTotal'), n: total })}><strong>{total}</strong><span>{t('home.chartTotal')}</span></Link></div>
    </div>
    <ul className="home-status-chart__legend">
      {lanes.map(l => <li key={l.lane}><Link to={scoped(scope, '/tasks', { status: l.statuses })} title={l.def} aria-label={t('home.segment', { lane: l.name, n: l.count })}>
        <span className="home-chart-swatch" style={{ background: l.tone }} aria-hidden="true" /><span>{l.name}</span><strong>{l.count}</strong>
      </Link></li>)}
    </ul>
  </div>
}

/** The server supplies exact totals and at most four item locators per day, independently of the capped list. */
export function HomeDeadlineRunway({ d, scope, projectStatus, openItem }: {
  d: HomeData; scope: Scope; projectStatus?: string; openItem: (kind: string, id: string, source: Element) => void
}) {
  const visual = useFlag('visual_system')
  const runway = d.runway
  if (!runway) return null
  const dayLabel = (date: string) => new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`))
  const dayLink = (date: string) => `${scoped(scope, '/home', { from: date, to: date, projectStatus, view: 'deadlines' })}#h-deadlines`
  return <div className="home-runway" data-home-runway>
    <div className="home-runway__scroll" role="region" aria-label={t('home.runway')} tabIndex={0}>
      <div className="home-runway__grid" style={{ gridTemplateColumns: `minmax(80px, 1.1fr) repeat(${runway.days.length}, minmax(72px, 1fr))` }}>
        <Link className="home-runway__before" to={scoped(scope, '/tasks', { overdue: 'true' })} aria-label={t('home.runwayOverdue', { n: d.metrics.overdueTasks })}>
          <span>{t('home.beforeToday')}</span><strong data-alert={d.metrics.overdueTasks > 0 || undefined}>{d.metrics.overdueTasks}</strong><span>{t('home.overdueTasks')}</span>
        </Link>
        {runway.days.map(day => <div key={day.date} className="home-runway__day" data-date={day.date} data-nonworking={day.nonWorking || undefined} data-today={day.date === d.today || undefined}>
          <Link className="home-runway__date" to={dayLink(day.date)} aria-current={day.date === d.today ? 'date' : undefined} aria-label={t('home.runwayDay', { date: dayLabel(day.date), n: day.total })}>
            <span>{dayLabel(day.date)}</span><strong>{day.total}</strong>{day.date === d.today && <small>{t('home.runwayToday')}</small>}
          </Link>
          <ul>{day.items.map(item => <li key={`${item.type}-${item.id}`}><button type="button" onClick={e => openItem(item.type, item.id, e.currentTarget)} aria-label={t('home.runwayItem', { project: item.projectNumber, type: runtimeText(`itemType.${item.type}`), key: item.key, name: item.name, date: dayLabel(day.date) })} title={`${item.projectNumber} · ${item.key} · ${item.name}`}>
            <ItemMark kind={item.type as 'Task' | 'Deliverable' | 'Milestone'} colour={resolveIdentityColour(visual, item.identityColour)} />
            <span><span className="home-runway__project">{item.projectNumber}</span><span className="home-runway__key">{item.key.startsWith(`${item.projectNumber}-`) ? item.key.slice(item.projectNumber.length + 1) : item.key}</span></span>
          </button></li>)}</ul>
          {day.total > day.items.length && <Link className="home-runway__more" to={dayLink(day.date)} aria-label={t('home.runwayMore', { n: day.total - day.items.length, date: dayLabel(day.date) })}>+{day.total - day.items.length}</Link>}
        </div>)}
      </div>
    </div>
    <div className="home-runway__legend">
      {(['Task', 'Deliverable', 'Milestone'] as const).map(kind => <span key={kind}><ItemMark kind={kind} />{runtimeText(`itemType.${kind}`)}</span>)}
      <span><i className="home-chart-swatch home-runway__nonworking-key" aria-hidden="true" />{t('home.nonWorkingScope')}</span>
    </div>
    {runway.truncatedRange && <p className="home-runway__notice">{t('home.runwayLimited', { from: runway.from, to: runway.to })}</p>}
  </div>
}
