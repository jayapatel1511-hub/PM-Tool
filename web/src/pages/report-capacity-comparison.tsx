import { Component, useId, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Empty, Field, selectCls, tdCls, thCls } from '@/components/hub/common'
import type { components } from '@/gen/api'
import { fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'
import './report-capacity-comparison.css'

type Comparison = components['schemas']['ReportCapacityComparison']
type Row = components['schemas']['ReportCapacityComparisonRow']
type Quantity = components['schemas']['ReportUnitCell']
const PAGE_SIZE = 12
const measures = ['capacity', 'demand', 'deficit', 'confirmed', 'expected', 'possible'] as const
const chartMeasures = ['capacity', 'demand', 'deficit'] as const
const number = (n: number, unit = 'hours') => new Intl.NumberFormat(undefined, { minimumFractionDigits: unit === 'fte' ? 1 : 0, maximumFractionDigits: unit === 'fte' ? 4 : 3 }).format(n)
const unitName = (unit: string) => t(unit === 'fte' ? 'reportUnits.fteLabel' : 'reportChart.hours')
const valueText = (quantity: Quantity, unit: string) => quantity.value == null
  ? t(quantity.coverage === 'StandardUnavailable' ? 'reportUnits.unavailable' : 'reportChart.unavailable')
  : `${number(quantity.value, unit)} ${unitName(unit)}`

/** UI focus only. The canonical report/metric/export query and saved data scope never contain these keys. */
export function reportChartFocus(data: Comparison, params: URLSearchParams) {
  const requested = params.get('chartPeriod'), period = data.periods.find(period => period.key === requested) ?? data.periods[0]
  const all = data.rows.filter(row => row.periodKey === period?.key)
  const pages = Math.max(1, Math.ceil(all.length / PAGE_SIZE)), rawPage = params.get('chartPage')
  const parsed = rawPage && /^[1-9]\d*$/.test(rawPage) ? Number(rawPage) : 1
  const page = Number.isSafeInteger(parsed) && parsed >= 1 && parsed <= pages ? parsed : 1
  return { period, all, pages, page, rows: all.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    reset: requested != null && requested !== period?.key || rawPage != null && rawPage !== String(page) }
}

export function reportChartFocusParameters(data: Comparison | null | undefined, params: URLSearchParams): Record<string, string> {
  if (!data || data.groupBy !== 'homeDiscipline') return {}
  const focus = reportChartFocus(data, params)
  return { ...(params.has('chartPeriod') && focus.period ? { chartPeriod: focus.period.key } : {}), ...(params.has('chartPage') ? { chartPage: String(focus.page) } : {}) }
}

function Shortfall({ quantity, unit }: { quantity: Quantity; unit: string }) {
  if (quantity.value == null && quantity.coverage !== 'StandardUnavailable') return <span>{t('reportChart.shortfallUnknown')}</span>
  if (quantity.knownHours == null) return <span>{t('reportChart.shortfallUnknown')}</span>
  const label = quantity.knownHours > 0
    ? quantity.value != null && unit === 'fte' ? t('reportChart.shortFte', { hours: number(quantity.knownHours), fte: number(quantity.value, unit) }) : t('reportChart.short', { hours: number(quantity.knownHours) })
    : t('reportChart.noShortfall')
  return <span className={quantity.knownHours > 0 ? 'report-capacity-short' : undefined}>{quantity.knownHours > 0 && <span aria-hidden>△ </span>}{label}{quantity.coverage === 'StandardUnavailable' && ` · ${t('reportUnits.unavailable')}`}</span>
}

function QuantityValue({ quantity, unit }: { quantity: Quantity; unit: string }) {
  return <span className="report-capacity-value" data-report-comparison-cell={quantity.path} data-value={quantity.value ?? undefined} data-coverage={quantity.coverage}>
    <span>{valueText(quantity, unit)}</span>
    {quantity.value == null && quantity.knownHours != null && <span className="text-xs text-muted-foreground">{t(quantity.coverage === 'ProvisionalAvailability' ? 'reportChart.provisional' : quantity.coverage === 'StandardUnavailable' ? 'reportUnits.known' : 'reportChart.recorded', { hours: number(quantity.knownHours), n: number(quantity.knownHours) })}</span>}
  </span>
}

class ChartBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }
  static getDerivedStateFromError() { return { failed: true } }
  render() { return this.state.failed ? <p role="status">{t('reportChart.fallback')}</p> : this.props.children }
}

function Chart({ rows, all, unit, period }: { rows: Row[]; all: Row[]; unit: string; period: string }) {
  const maximum = Math.max(0, ...all.flatMap(row => chartMeasures.map(measure => row[measure].value ?? 0)))
  const scale = maximum > 0 ? maximum : 1
  return <figure aria-label={t('reportChart.figure')} data-report-capacity-chart>
    {rows.map(row => <div className="report-capacity-group" key={`${row.key}:${row.periodKey}`} data-report-chart-group={row.key} role="img" aria-label={t('reportChart.summary', {
      discipline: row.discipline, period, capacity: valueText(row.capacity, unit), demand: valueText(row.demand, unit), deficit: valueText(row.deficit, unit),
      coverage: row.partialPeople || row.provisionalPeople ? t('reportChart.affected', { partial: row.partialPeople, provisional: row.provisionalPeople }) : chartMeasures.some(measure => ['KnownSubtotal', 'ProvisionalAvailability'].includes(row[measure].coverage)) ? t('reportChart.partial') : t('reportChart.complete'),
    })}>
      <strong>{row.discipline}</strong>
      {chartMeasures.map(measure => { const amount = row[measure].value; return <div className="report-capacity-bar-row" key={measure} data-report-bar={measure} data-value={amount ?? undefined}>
        <span>{t(`reportChart.${measure}`)}</span><span className="report-capacity-bar-track" aria-hidden>
          {amount != null && <span className={`report-capacity-bar-fill report-capacity-bar-fill--${measure}`} style={{ width: `${amount / scale * 100}%` }}>
            {measure === 'demand' && <><span className="report-capacity-confirmed" style={{ width: `${row.demand.value ? (row.confirmed.value ?? 0) / row.demand.value * 100 : 0}%` }} /><span className="report-capacity-expected" style={{ width: `${row.demand.value ? (row.expected.value ?? 0) / row.demand.value * 100 : 0}%` }} /></>}
          </span>}
        </span><span className="tabular-nums">{valueText(row[measure], unit)}</span>
      </div> })}
      <Shortfall quantity={row.deficit} unit={unit} />
    </div>)}
  </figure>
}

export function ReportCapacityComparison({ data }: { data: Comparison }) {
  const [params, setParams] = useSearchParams(), id = useId()
  const focus = reportChartFocus(data, params), { period, rows, all, page, pages } = focus
  const setFocus = (key: string, value: string) => { const next = new URLSearchParams(params); next.set(key, value); if (key === 'chartPeriod') next.delete('chartPage'); setParams(next, { preventScrollReset: true }) }
  return <section className="report-capacity-comparison" aria-labelledby={`${id}-title`} data-report-capacity-comparison>
    <h2 id={`${id}-title`} className="font-semibold">{t('reportChart.title')}</h2>
    <p className="text-xs text-muted-foreground">{t('reportChart.scope')}</p>
    <p className="text-xs text-muted-foreground">{t('reportChart.rule')}</p>
    {data.scopeCoverage === 'VisibleOnly' && <p className="text-xs">{t('reportChart.partial')}</p>}
    {period && <><Field className="report-capacity-period" label={t('reportChart.period')} htmlFor={`${id}-period`}><select id={`${id}-period`} className={selectCls} value={period.key} onChange={event => setFocus('chartPeriod', event.target.value)}>{data.periods.map(period => <option key={period.key} value={period.key}>{period.label}</option>)}</select></Field>
      <p className="text-xs text-muted-foreground">{t('reportChart.basis', { period: period.label, weeks: period.weekCount, hours: period.denominatorHours ?? t('reportChart.unavailable'), asOf: fmtTime(data.asOf) })}</p></>}
    {focus.reset && <p role="status" className="text-xs">{t('reportChart.focusReset')}</p>}
    {data.truncated && <p className="text-xs" data-report-comparison-truncated>{t('reportChart.truncated', { returned: data.returnedGroups, total: data.totalGroups })}</p>}
    {!rows.length ? <Empty>{t('reportChart.empty')}</Empty> : <>
      <p className="text-xs tabular-nums" data-report-comparison-page>{t('reportChart.shown', { from: (page - 1) * PAGE_SIZE + 1, to: Math.min(page * PAGE_SIZE, all.length), returned: data.returnedGroups })}</p>
      <ChartBoundary key={`${period?.key}:${page}`}><Chart rows={rows} all={all} unit={data.unit} period={period?.label ?? ''} /></ChartBoundary>
      {/* Text-only values: the region takes focus to support keyboard horizontal scrolling. */}
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex */}
      <div role="region" aria-label={t('reportChart.table')} tabIndex={0} className="report-capacity-table scroll-region">
        <table data-report-comparison-table><caption className="sr-only">{t('reportChart.table')}</caption><thead><tr><th scope="col" className={thCls}>{t('reportChart.discipline')}</th>{measures.map(measure => <th key={measure} className={thCls} scope="col">{t(`reportChart.${measure}`)}</th>)}</tr></thead>
          <tbody>{rows.map(row => <tr key={`${row.key}:${row.periodKey}`} data-report-comparison-row={row.key}><th className={tdCls} scope="row">{row.discipline}<span className="block text-xs font-normal">{t('reportChart.affected', { partial: row.partialPeople, provisional: row.provisionalPeople })}</span></th>{measures.map(measure => <td key={measure} className={tdCls}><QuantityValue quantity={row[measure]} unit={data.unit} />{measure === 'deficit' && <span className="block text-xs"><Shortfall quantity={row.deficit} unit={data.unit} /></span>}</td>)}</tr>)}</tbody>
        </table>
      </div>
      <nav className="flex flex-wrap justify-between gap-2" aria-label={t('reportChart.title')}><Button type="button" variant="outline" size="sm" disabled={page <= 1} onClick={() => setFocus('chartPage', String(page - 1))}>{t('reportChart.previous')}</Button><Button type="button" variant="outline" size="sm" disabled={page >= pages} onClick={() => setFocus('chartPage', String(page + 1))}>{t('reportChart.next')}</Button></nav>
    </>}
    <p className="text-xs text-muted-foreground">{t('reportUnits.rounding')}</p>
    <p className="text-xs text-muted-foreground">{t('reportChart.export')}</p>
  </section>
}
