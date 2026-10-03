import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, BarChart3, ChevronsUpDown, ListFilter } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Loading, Page, selectCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { useItemPanel } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { Key, StatusPill } from '@/components/hub/pills'
import { itemHref } from '@/components/hub/search'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useReference } from '@/hooks/data'
import { get, qs } from '@/lib/api'
import { fmtDate, fmtTime } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'

interface Param { key: string; label: string; type: string; default?: string | null; options?: { value: string; label: string }[] | null }
interface Column { path: string; header: string; type: string }
interface ReportDef { code: string; title: string; description: string; params: Param[]; columns: Column[]; itemType?: string | null }
interface ReportRun { code: string; title: string; itemType?: string | null; columns: Column[]; rows: Record<string, any>[]; total: number; totalIsLowerBound?: boolean; truncated: boolean; listLink?: string | null; parameters: { label: string; value: string }[] }

const useCatalogue = () => useQuery({ queryKey: ['reports'], queryFn: () => get<ReportDef[]>('reports'), staleTime: 5 * 60_000 })

/** Reports catalogue (§13.18, §19): deterministic reports with a description each. */
export function ReportsPage() {
  const q = useCatalogue()
  return (
    <Page title={t('nav.reports')} subtitle={t('reports.subtitle')}>
      {q.error && <ErrorBanner error={q.error} />}
      {q.isPending ? <Loading rows={6} /> : (
        <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {q.data!.map((r) => (
            <li key={r.code}>
              <Link to={r.code} className="flex h-full gap-3 rounded-lg border bg-card p-4 hover:border-primary hover:bg-muted/30">
                <BarChart3 className="mt-0.5 size-5 shrink-0 text-muted-foreground" aria-hidden />
                <span><span className="block font-medium">{r.title}</span><span className="text-sm text-muted-foreground">{r.description}</span></span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Page>
  )
}

/** One report: parameters in the URL, results as a table, export and "Open as filtered list" (§19). */
export function ReportPage() {
  const { code } = useParams()
  const cat = useCatalogue()
  const def = cat.data?.find((r) => r.code === code)
  const [sp, setSp] = useSearchParams()
  const params = useMemo(() => Object.fromEntries([...sp.entries()].filter(([k]) => k !== 'panel')), [sp])
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); n.delete('panel'); setSp(n, { replace: true }) }
  const needsProject = def?.params.some((p) => p.type === 'project') && !params.projectId
  const run = useQuery({ queryKey: ['report', code, params], queryFn: () => get<ReportRun>(`reports/${code}${qs(params)}`), enabled: !!def && !needsProject })
  if (cat.isPending) return <Loading rows={6} />
  if (!def) return <Page title={t('nav.reports')}><Empty>{t('reports.unknown')}</Empty></Page>
  return (
    <Page title={def.title} subtitle={def.description}
      actions={<>
        <Button variant="ghost" size="sm" asChild><Link to="/reports"><ArrowLeft className="size-4" />{t('nav.reports')}</Link></Button>
        {run.data?.listLink && <Button variant="outline" size="sm" asChild><Link to={run.data.listLink}><ListFilter className="size-4" />{t('reports.openList')}</Link></Button>}
        {run.data && <ExportMenu path={`reports/${code}/export`} params={params} name={code!} />}
      </>}>
      <form className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3" onSubmit={(e) => e.preventDefault()} aria-label={t('reports.parameters')}>
        {def.params.map((p) => <ParamInput key={p.key} p={p} value={params[p.key]} onChange={(v) => set(p.key, v)} />)}
        {Object.keys(params).length > 0 && <Button type="button" variant="ghost" size="sm" onClick={() => setSp(new URLSearchParams(), { replace: true })}>{t('common.clear')}</Button>}
      </form>
      {needsProject ? <Empty>{t('reports.chooseProject')}</Empty> : run.error ? <ErrorBanner error={run.error} retry={() => run.refetch()} /> : run.isPending ? <Loading rows={8} /> : (
        <ReportTable r={run.data} itemType={def.itemType} />
      )}
    </Page>
  )
}

function ParamInput({ p, value, onChange }: { p: Param; value?: string; onChange: (v: string | null) => void }) {
  const ref = useReference()
  const id = `rp-${p.key}`
  const box = (child: ReactNode) => <div className="space-y-1"><label htmlFor={id} className="block text-xs text-muted-foreground">{p.label}</label>{child}</div>
  switch (p.type) {
    case 'projects': return box(<ProjectsPicker id={id} value={value} onChange={onChange} />)
    case 'project': return box(<ProjectsPicker id={id} value={value} onChange={onChange} single />)
    case 'discipline': return box(<select id={id} className={cn(selectCls, 'h-8 w-44')} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">{t('projects.anyDiscipline')}</option>{ref.data?.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>)
    case 'office': return box(<select id={id} className={cn(selectCls, 'h-8 w-44')} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">{t('reports.anyOffice')}</option>{ref.data?.offices.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>)
    case 'person': return box(<div className="w-52"><PeoplePicker id={id} value={value} onChange={(v) => onChange(v)} placeholder={t('reports.anyone')} label={p.label} /></div>)
    case 'date': return box(<Input id={id} type="date" className="h-8 w-40" value={value ?? ''} onChange={(e) => onChange(e.target.value || null)} />)
    case 'number': return box(<Input id={id} type="number" min={0} className="h-8 w-28" placeholder={p.default ?? ''} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)} />)
    case 'bool': return <label className="flex h-8 items-center gap-2 text-sm"><Checkbox checked={value === 'true'} onCheckedChange={(c) => onChange(c ? 'true' : null)} />{p.label}</label>
    default: return box(<select id={id} className={cn(selectCls, 'h-8 w-44')} value={value ?? p.default ?? ''} onChange={(e) => onChange(e.target.value || null)}>
      {!p.default && <option value="">{t('reports.any')}</option>}{p.options?.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select>)
  }
}

/** Visible projects to scope a report; none chosen means every project the viewer can see. */
export function ProjectsPicker({ id, value, onChange, single, placeholder }: { id: string; value?: string; onChange: (v: string | null) => void; single?: boolean; placeholder?: string }) {
  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const q = useQuery({ queryKey: ['projects-pick', term], queryFn: () => get<PageOf<{ id: string; projectNumber: string; name: string }>>(`projects${qs({ q: term, pageSize: 50 })}`) })
  const chosen = (value ?? '').split(',').filter(Boolean)
  const names = useQuery({ queryKey: ['projects-pick-ids', value], enabled: chosen.length > 0, queryFn: () => get<PageOf<{ id: string; projectNumber: string }>>(`projects${qs({ ids: value, includeArchived: true, pageSize: 50 })}`) })
  const label = chosen.length === 0 ? placeholder ?? (single ? t('reports.chooseProject') : t('reports.allProjects'))
    : (names.data?.items ?? []).filter((p) => chosen.includes(p.id)).map((p) => p.projectNumber).join(', ') || t('reports.nProjects', { n: chosen.length })
  const toggle = (pid: string) => {
    if (single) { onChange(pid); setOpen(false); return }
    const next = chosen.includes(pid) ? chosen.filter((x) => x !== pid) : [...chosen, pid]
    onChange(next.length ? next.join(',') : null)
  }
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" size="sm" className="h-8 w-56 justify-between font-normal"><span className="truncate">{label}</span><ChevronsUpDown className="size-3.5 opacity-50" /></Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-2" align="start">
        <Input type="search" className="mb-2 h-8" placeholder={t('common.search')} value={term} onChange={(e) => setTerm(e.target.value)} aria-label={t('common.search')} />
        <ul className="max-h-60 overflow-y-auto text-sm">
          {(q.data?.items ?? []).map((p) => (
            <li key={p.id}><label className="flex cursor-pointer items-center gap-2 rounded px-1 py-1 hover:bg-muted">
              {single ? <input type="radio" name={id} checked={chosen.includes(p.id)} onChange={() => toggle(p.id)} /> : <Checkbox checked={chosen.includes(p.id)} onCheckedChange={() => toggle(p.id)} />}
              <span className="key font-mono text-xs text-muted-foreground">{p.projectNumber}</span><span className="truncate">{p.name}</span>
            </label></li>
          ))}
        </ul>
        {!single && chosen.length > 0 && <Button variant="ghost" size="sm" className="mt-1 w-full" onClick={() => onChange(null)}>{placeholder ? t('scope.clearChoice') : t('reports.allProjects')}</Button>}
      </PopoverContent>
    </Popover>
  )
}

const at = (row: Record<string, any>, path: string) => path.split('.').reduce<any>((v, k) => (v == null ? v : v[k]), row)

function ReportTable({ r, itemType }: { r: ReportRun; itemType?: string | null }) {
  const openPanel = useItemPanel()
  if (!r.rows.length) return <div className="rounded-lg border bg-card"><Empty>{t('reports.empty')}</Empty></div>
  const cell = (row: Record<string, any>, c: Column) => {
    const v = at(row, c.path)
    if (v == null || v === '') return <span className="text-muted-foreground">{t('common.dash')}</span>
    switch (c.type) {
      case 'key': {
        const type = row.itemType ?? itemType
        return type && row.id && row.projectNumber
          ? <Link to={itemHref(type, row.projectNumber, row.id)} onClick={(e) => { if (location.pathname.startsWith(`/projects/${row.projectNumber}`)) { e.preventDefault(); openPanel(type, row.id) } }}><Key>{v}</Key></Link>
          : <Key>{v}</Key>
      }
      case 'date': return fmtDate(v)
      case 'datetime': return fmtTime(v)
      case 'percent': return `${v}%`
      default: return c.path === 'status' || c.path === 'projectStatus' ? <StatusPill status={v} /> : typeof v === 'string' ? tv(v) : String(v)
    }
  }
  return (
    <div className="space-y-2">
      <p className="text-sm text-muted-foreground">{r.truncated ? t(r.totalIsLowerBound ? 'reports.truncatedLowerBound' : 'reports.truncated', { n: r.rows.length, total: r.total }) : plural(r.total, 'reports.row1', 'reports.rows')}
        {r.parameters.length > 0 && <> · {r.parameters.map((p) => `${p.label}: ${p.value}`).join(' · ')}</>}</p>
      <div className="overflow-x-auto rounded-lg border bg-card focus-visible:ring-2 focus-visible:ring-ring" tabIndex={0} role="region" aria-label={r.title}>
        <table className="w-full text-[13px]">
          <thead className="bg-muted/60 text-left text-xs text-muted-foreground"><tr>{r.columns.map((c) => <th key={c.path} scope="col" className={cn('whitespace-nowrap px-3 py-2 font-medium', ['number', 'percent'].includes(c.type) && 'text-right')}>{c.header}</th>)}</tr></thead>
          <tbody>
            {r.rows.map((row, i) => (
              <tr key={row.id ?? i} className="border-t hover:bg-muted/30">
                {r.columns.map((c) => <td key={c.path} className={cn('px-3 py-1.5', ['number', 'percent'].includes(c.type) ? 'text-right tabular-nums' : c.type === 'text' ? 'min-w-[8rem]' : 'whitespace-nowrap')}>{cell(row, c)}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
