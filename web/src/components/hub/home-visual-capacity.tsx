import { Link } from 'react-router'
import type { components } from '@/gen/api'
import { Section } from '@/components/hub/common'
import { formatReferenceTime } from '@/components/hub/title-block'
import { useMe } from '@/lib/auth-context'
import { fmtDate, hours } from '@/lib/format'
import { t } from '@/lib/i18n'
import './home-visual.css'

type Capacity = components['schemas']['HomeCapacity']
type Totals = components['schemas']['HomeCapacityTotals']
const displayHours = (n?: number | null) => n == null ? t('common.unavailable') : hours(Math.round(n * 10) / 10)

/** An exact planner projection. The unfilled part of a partial view is neutral, never labelled spare room. */
function CapacityBar({ totals, compact = false }: { totals: Totals; compact?: boolean }) {
  const { plannedHours: planned, availableHours: available, timeAwayHours: away, grossHours: gross, freeHours: free } = totals
  if (planned == null || available == null || away == null || gross == null) return <span className="home-capacity__bar-unavailable">{t('home.capacity.barUnavailable')}</span>
  const scale = compact ? Math.max(available, planned, 1) : Math.max(gross, planned + away, 1)
  const within = Math.min(planned, available)
  const over = Math.max(0, planned - available)
  const remaining = Math.max(0, available - planned)
  const width = (value: number) => `${value / scale * 100}%`
  return <div className={`home-capacity__bar${compact ? ' home-capacity__bar--compact' : ''}`} role="img" aria-label={compact ? t('home.capacity.groupBarLabel', { planned: displayHours(planned), available: displayHours(available), free: displayHours(free) }) : t('home.capacity.barLabel', { planned: displayHours(planned), away: displayHours(away), free: displayHours(free), gross: displayHours(gross) })}>
    <span className="home-capacity__planned" style={{ width: width(within) }} />
    {!compact && <span className="home-capacity__away" style={{ width: width(away) }} />}
    <span className={free == null ? 'home-capacity__unknown' : 'home-capacity__free'} style={{ width: width(remaining) }} />
    <span className="home-capacity__over" style={{ width: width(over) }} />
    <span className="home-capacity__limit" style={{ left: width(compact ? available : gross) }} aria-hidden="true" />
  </div>
}

function CapacityNotes({ totals }: { totals: Totals }) {
  return <>
    {totals.partialPeople > 0 && <p className="home-capacity__notice">{t('home.capacity.partial', { n: totals.partialPeople })}</p>}
    {totals.provisionalPeople > 0 && <p className="home-capacity__notice">{t('home.capacity.provisional', { n: totals.provisionalPeople })}</p>}
    {totals.missingWeeks > 0 && <p className="home-capacity__notice">{t('home.capacity.missing', { n: totals.missingWeeks })}</p>}
  </>
}

export function HomeWeeklyCapacity({ capacity }: { capacity: Capacity }) {
  const me = useMe()
  const v = capacity.totals
  const asOf = formatReferenceTime(capacity.asOf, me.settings.orgTimeZone)
  const value = (key: string, label: string, number: number | null | undefined, mark?: string) => <div key={key} data-capacity-value={key}>
    <dt>{mark && <i className={`home-chart-swatch ${mark}`} aria-hidden="true" />}{label}</dt>
    <dd>{number == null ? t('common.unavailable') : <Link to={capacity.plannerPath}>{displayHours(number)}</Link>}</dd>
  </div>
  return <Section id="h-capacity" title={t('home.capacity.title')} actions={<Link className="text-sm underline underline-offset-4" to={capacity.plannerPath}>{t('home.capacity.openPlanner')}</Link>}>
    <div className="home-capacity" data-home-capacity>
      <p className="home-capacity__scope">{t('home.capacity.scope', { n: v.people })} <span>{fmtDate(capacity.week)} – {fmtDate(capacity.through)}</span></p>
      <div className="home-capacity__headline">
        <strong data-capacity-percentage>{v.pct == null ? t('home.capacity.percentageUnavailable') : `${v.pct}%`}</strong>
        <span>{v.partialPeople > 0 ? t('home.capacity.visiblePlanned') : t('home.capacity.sharePlanned')}</span>
        <span className="home-capacity__equation">{t('home.capacity.equation', { planned: displayHours(v.plannedHours), available: displayHours(v.availableHours) })}</span>
      </div>
      <CapacityBar totals={v} />
      <dl className="home-capacity__values">
        {value('planned', t('home.capacity.planned'), v.plannedHours, 'home-capacity__planned')}
        {value('away', t('home.capacity.away'), v.timeAwayHours, 'home-capacity__away')}
        {value('free', t('home.capacity.free'), v.freeHours, 'home-capacity__free')}
        {value('gross', t('home.capacity.gross'), v.grossHours)}
        {value('available', t('home.capacity.available'), v.availableHours)}
        {value('possible', t('home.capacity.possible'), v.possibleHours)}
      </dl>
      <p className="home-capacity__basis">{t('home.capacity.basis')}</p>
      <CapacityNotes totals={v} />
      <p className="home-capacity__above"><Link to={capacity.above100Path}>{t(v.partialPeople + v.provisionalPeople + v.missingWeeks > 0 ? 'home.capacity.aboveVisible' : 'home.capacity.above', { n: v.above100People })}</Link></p>
      <p className="home-capacity__basis">{t('home.capacity.mark100')}</p>
      <div className="home-capacity__disciplines">
        <div className="home-capacity__discipline-heading" aria-hidden="true"><span>{t('home.capacity.homeDiscipline')}</span><span>{t('home.capacity.people')}</span><span>{t('home.capacity.sharePlanned')}</span></div>
        {capacity.disciplines.map(group => <div className="home-capacity__discipline" key={group.disciplineId ?? 'not-set'}>
          <Link to={group.plannerPath} className="home-capacity__discipline-name">{group.disciplineName ?? (group.disciplineId ? t('common.unavailable') : t('home.capacity.notSet'))}</Link>
          <Link to={group.plannerPath} className="home-capacity__people" aria-label={t('home.capacity.groupPeople', { name: group.disciplineName ?? (group.disciplineId ? t('common.unavailable') : t('home.capacity.notSet')), n: group.totals.people })}>{group.totals.people}</Link>
          <div className="home-capacity__discipline-bar"><CapacityBar totals={group.totals} compact /><span className="home-capacity__group-pct">{group.totals.pct == null ? t('home.capacity.percentageUnavailable') : `${group.totals.pct}%`}</span></div>
          <div className="home-capacity__group-notes"><p className="home-capacity__basis">{t('home.capacity.groupBasis', { planned: displayHours(group.totals.plannedHours), available: displayHours(group.totals.availableHours), free: displayHours(group.totals.freeHours) })}</p><CapacityNotes totals={group.totals} /></div>
        </div>)}
      </div>
      <p className="home-capacity__basis">{t('home.capacity.drafts')}</p>
      {asOf && <p className="home-capacity__asof">{t('home.capacity.asOf')} <time dateTime={capacity.asOf}>{asOf}</time></p>}
    </div>
  </Section>
}
