import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Loading, Missing, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { BareTable } from '@/components/ui/table'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { fmtDate, fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'
import { captureRecordContextOrigin, recordContextReturn } from '@/lib/record-context-navigation'

type Schemas = components['schemas']
type Metric = 'unestimated' | 'open-undated'
const inputKeys = ['taskInput', 'inputPage', 'inputPageSize']
export function clearReportInputState(params: URLSearchParams) { inputKeys.forEach(key => params.delete(key)); return params }
const metricOf = (value: string | null): Metric | null => value === 'unestimated' || value === 'open-undated' ? value : null
const positive = (value: string | null, fallback: number, maximum: number) => value && /^\d+$/.test(value) && Number(value) > 0 ? Math.min(Number(value), maximum) : fallback
const metricTitle = (metric: Metric) => t(metric === 'unestimated' ? 'reportInputs.metric.unestimated' : 'reportInputs.metric.open-undated')
const basisText = (basis: string) => {
  switch (basis) {
    case 'ReportTaskRows': return t('reportInputs.basis')
    case 'WorkloadPeopleContext': return t('reportInputs.basis.WorkloadPeopleContext')
    case 'PlannerPeopleContext': return t('reportInputs.basis.PlannerPeopleContext')
    case 'CurrentRecordedTaskContext': return t('reportInputs.basis.CurrentRecordedTaskContext')
    default: return t('reportInputs.unavailable')
  }
}
const scopeText = (coverage: string) => t(coverage === 'VisibleOnly' ? 'reportInputs.visibleOnly' : coverage === 'CompleteForVisibleParameters' ? 'reportInputs.scope' : 'reportInputs.unavailable')
const sameParameters = (a: Record<string, string>, b: Record<string, string>) => Object.keys(a).length === Object.keys(b).length && Object.entries(a).every(([key, value]) => b[key] === value)

/** The summary belongs to this report's complete task source, before the result table's display cap. */
export function ReportTaskInputs({ code, summary, applied }: { code: string; summary: Schemas['ReportTaskInputs']; applied: Schemas['ReportView']['parameters'] }) {
  const location = useLocation(), navigate = useNavigate(), [sp] = useSearchParams()
  const links = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const metric = metricOf((location.state as { reportInputFocus?: string } | null)?.reportInputFocus ?? null)
    if (metric) links.current?.querySelector<HTMLElement>(`[data-report-task-input="${metric}"]`)?.focus({ preventScroll: true })
  }, [location.key, location.state])
  const link = (metric: Metric, count: Schemas['ReportTaskInputCount']) => {
    const target = new URL(count.href, window.location.origin)
    for (const key of ['catalogue', 'viewProject', 'view', 'chartPeriod', 'chartPage']) if (sp.has(key)) target.searchParams.set(key, sp.get(key)!)
    const href = `${target.pathname}${target.search}`
    return <span key={metric} className="inline-flex flex-wrap items-baseline gap-x-2">
      <Link data-report-task-input={metric} className="font-medium text-foreground underline underline-offset-4" to={href} onClick={event => {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.altKey || event.shiftKey) return
        event.preventDefault(); navigate(href, { state: { recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
      }}>{count.count == null ? metricTitle(metric) : t(metric === 'unestimated' ? 'reportInputs.unestimated' : 'reportInputs.openUndated', { n: count.count })}</Link>
      {count.count == null && <span>{t('reportInputs.known', { n: count.knownCount })}</span>}
    </span>
  }
  return <>
    <div className="report-inputs" ref={links}>
      <div className="flex flex-wrap gap-x-5 gap-y-1 text-sm">{link('unestimated', summary.unestimated)}{link('open-undated', summary.openUndated)}</div>
      <p className="text-xs text-muted-foreground">{basisText(summary.basis)}{summary.scopeCoverage === 'VisibleOnly' && <> {scopeText(summary.scopeCoverage)}</>}</p>
      {summary.unavailableReferences > 0 ? <p className="text-xs text-muted-foreground">{t('reportInputs.unavailableReferences', { n: summary.unavailableReferences })}</p> :
        (summary.unestimated.count == null || summary.openUndated.count == null) && <p className="text-xs text-muted-foreground">{t('reportInputs.unavailable')}</p>}
    </div>
    {sp.has('taskInput') && <ReportTaskInputDialog code={code} summary={summary} applied={applied} />}
  </>
}

function ReportTaskInputDialog({ code, summary, applied }: { code: string; summary: Schemas['ReportTaskInputs']; applied: Schemas['ReportView']['parameters'] }) {
  const location = useLocation(), navigate = useNavigate(), [sp] = useSearchParams(), openPanel = useItemPanel()
  const sourceTask = useRef<string | null>(null)
  const metric = metricOf(sp.get('taskInput')), page = positive(sp.get('inputPage'), 1, 2147483647), size = positive(sp.get('inputPageSize'), 50, 200)
  const query = useQuery({
    queryKey: ['report', code, summary.parameters, 'task-inputs', metric, page, size],
    enabled: metric != null,
    queryFn: () => typedRequest('get', '/api/v1/reports/{code}/task-inputs/{metric}', { params: { code, metric: metric! }, query: { ...summary.parameters, page, pageSize: size } as never }),
  })
  // No previous page/count survives a new read or refusal. The server owns permission and source evaluation.
  const data = !query.isFetching && !query.error && query.data?.code === code && query.data.metric === metric && sameParameters(query.data.parameters, summary.parameters) ? query.data : undefined
  const panelOpen = sp.has('panel')
  useEffect(() => {
    if (panelOpen || !data || !sourceTask.current) return
    const frame = requestAnimationFrame(() => document.getElementById(`report-input-task-${sourceTask.current}`)?.focus({ preventScroll: true }))
    return () => cancelAnimationFrame(frame)
  }, [panelOpen, data, location.key])
  const close = () => {
    const returned = recordContextReturn(location.state)
    if (returned) { navigate(returned.url, { replace: true, state: { ...returned.state, reportInputFocus: metric } }); return }
    const next = clearReportInputState(new URLSearchParams(sp)); next.delete('panel')
    navigate(`${location.pathname}${next.size ? `?${next}` : ''}${location.hash}`, { replace: true, state: { ...location.state, reportInputFocus: metric } })
  }
  const go = (nextPage: number) => {
    sourceTask.current = null
    const next = new URLSearchParams(sp); next.set('inputPage', String(nextPage)); next.set('inputPageSize', String(size))
    navigate(`${location.pathname}?${next}${location.hash}`, { replace: true, state: location.state })
  }
  const pages = data ? Math.max(1, Math.ceil((data.total ?? data.knownTotal) / data.pageSize)) : 1
  return <Dialog open={!panelOpen} onOpenChange={open => { if (!open) close() }}>
    <DialogContent className="sm:max-w-4xl" onOpenAutoFocus={event => { if (sourceTask.current && document.getElementById(`report-input-task-${sourceTask.current}`)) event.preventDefault() }} onCloseAutoFocus={event => event.preventDefault()}>
      <DialogHeader><DialogTitle>{metric ? metricTitle(metric) : t('reportInputs.title')}</DialogTitle><DialogDescription>{t('reportInputs.live')}</DialogDescription></DialogHeader>
      <p className="text-sm text-muted-foreground">{basisText(data?.basis ?? summary.basis)} {scopeText(data?.scopeCoverage ?? summary.scopeCoverage)}</p>
      {!metric ? <p role="alert">{t('reportInputs.unavailable')}</p> : query.error ? <ErrorBanner error={query.error} retry={() => query.refetch()} /> : query.isPending || query.isFetching ? <Loading rows={5} /> : !data ? <p role="alert">{t('reportInputs.unavailable')}</p> : <>
        <div className="flex flex-wrap items-baseline gap-x-5 gap-y-1 text-sm" data-report-input-total={data.knownTotal}>
          <span className="font-semibold tabular-nums">{data.total == null ? t('reportInputs.known', { n: data.knownTotal }) : t(metric === 'unestimated' ? 'reportInputs.unestimated' : 'reportInputs.openUndated', { n: data.total })}</span>
          <time dateTime={data.asOf} className="text-muted-foreground">{t('reports.asOf', { time: fmtTime(data.asOf) })}</time>
        </div>
        {data.unavailableReferences > 0 ? <p className="text-sm">{t('reportInputs.unavailableReferences', { n: data.unavailableReferences })}</p> : data.total == null && <p className="text-sm">{t('reportInputs.unavailable')}</p>}
        {!!applied.length && <dl className="flex flex-wrap gap-x-5 gap-y-1 text-xs text-muted-foreground">{applied.map(parameter => <div key={parameter.label}><dt className="inline">{parameter.label}: </dt><dd className="inline font-medium text-foreground">{parameter.value}</dd></div>)}</dl>}
        {!data.items.length ? <Empty>{t(data.total == null ? 'reportInputs.noneKnown' : 'reportInputs.none')}</Empty> : <TableRegion>
          <BareTable className="w-full text-sm" data-report-input-table={metric}><caption className="sr-only">{metricTitle(metric)}</caption>
            <thead className="bg-muted"><tr>{[t('common.name'), t('common.project'), t('common.discipline'), t('common.status'), t('field.EstimatedHours'), t('field.DueDate')].map(label => <th key={label} className={thCls} scope="col">{label}</th>)}</tr></thead>
            <tbody>{data.items.map(row => <tr key={row.id} className="border-t hover:bg-muted">
              <td className={tdCls}><button type="button" id={`report-input-task-${row.id}`} className="inline-flex items-baseline gap-2 text-left hover:underline focus-visible:outline-2 focus-visible:outline-ring" onClick={event => { sourceTask.current = row.id; openPanel('Task', row.id, captureRecordContextOrigin(location, event.currentTarget)) }}><Key>{row.key}</Key><span>{row.name}</span></button></td>
              <td className={tdCls}><Key>{row.projectNumber}</Key><span className="ml-2">{row.projectName}</span></td>
              <td className={tdCls}>{row.disciplineName ?? <Missing />}</td><td className={tdCls}><StatusPill status={row.status} /></td>
              <td className={tdCls}>{row.estimatedHours == null ? <Missing /> : row.estimatedHours}</td><td className={tdCls}>{row.dueDate ? fmtDate(row.dueDate) : <Missing />}</td>
            </tr>)}</tbody>
          </BareTable>
        </TableRegion>}
        <nav aria-label={t('reportInputs.title')} className="flex flex-wrap items-center justify-between gap-2">
          <span className="text-sm tabular-nums">{t(data.total == null ? 'reportInputs.knownPage' : 'reportInputs.page', { page: data.page, pages })}</span>
          <div className="flex gap-2"><Button variant="outline" size="sm" disabled={data.page <= 1} onClick={() => go(data.page - 1)}>{t('reportInputs.previous')}</Button><Button variant="outline" size="sm" disabled={data.page >= pages} onClick={() => go(data.page + 1)}>{t('reportInputs.next')}</Button></div>
        </nav>
      </>}
      <div className="flex justify-end"><Button type="button" variant="outline" onClick={close}>{t('reportInputs.close')}</Button></div>
    </DialogContent>
  </Dialog>
}
