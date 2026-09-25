import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronRight, Info, Pencil } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { PeoplePicker } from '@/components/hub/people'
import { Chip, Key, StatusPill } from '@/components/hub/pills'
import { itemHref } from '@/components/hub/search'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useReference } from '@/hooks/data'
import { get, patch, put, qs } from '@/lib/api'
import { fmtDate, shortDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'
import { errorText } from './projects/Tasks'
import { WorkspaceTabs } from '@/components/hub/workspace'
import { ViewMenu } from '@/components/hub/views'

interface Cell { week: string; hours: number; pct: number }
interface PersonRow {
  id: string; displayName: string; supervisorId?: string; supervisorName?: string; capacity: number; capacityOverride: boolean; cells: Cell[]; noDueDate: number
  openTasks: number; unestimated: number; overdue: number; projects: number; overAssigned: boolean; underAssigned: boolean; cluster: boolean; canSetCapacity: boolean; indicator: string
}
interface Grid { weeks: string[]; today: string; currentWeek: string; defaultCapacity: number; people: PersonRow[] }
interface TaskLoadRow { id: string; key: string; name: string; status: string; dueDate?: string; estimatedHours?: number | null; progressPct: number; remaining?: number | null; overdue: boolean; noDueDate: number; rowVersion: number; weeks: number[]; canReassign: boolean }
interface PersonTasks { weeks: string[]; projects: { id: string; projectNumber: string; name: string; tasks: TaskLoadRow[] }[] }

const FILTERS = ['supervisorId', 'disciplineId', 'officeId', 'projectId', 'indicator', 'from', 'sort'] as const

/** Shading by load with the number always shown (§13.11): light under 40 %, calm to 90 %, amber to 110 %, red above. */
const shade = (pct: number) => pct > 110 ? 'bg-bad-bg text-bad font-semibold' : pct > 90 ? 'bg-warn-bg text-warn' : pct >= 40 ? 'bg-ok-bg text-ok' : 'bg-muted/40 text-muted-foreground'
const hrs = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(1))

/** Resource and Workload View (§12.15, §13.11): estimated remaining hours per person per week, across projects. */
export function WorkloadPage() {
  const ref = useReference()
  const [sp, setSp] = useSearchParams()
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [capacity, setCapacity] = useState<PersonRow | null>(null)
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)]))
  const q = useQuery({ queryKey: ['workload', filters], queryFn: () => get<Grid>(`workload${qs(filters)}`) })
  const projects = useQuery({ queryKey: ['projects-pick', ''], queryFn: () => get<PageOf<{ id: string; projectNumber: string; name: string }>>('projects?pageSize=200') })
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  const toggle = (id: string) => setOpen((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })
  const groups = new Map<string, PersonRow[]>()
  for (const p of q.data?.people ?? []) { const g = p.supervisorName ?? t('workload.noSupervisor'); groups.set(g, [...(groups.get(g) ?? []), p]) }
  return (
    <Page title={t('nav.workload')} subtitle={t('workload.subtitle')} actions={<><Method /><ViewMenu listType="workload" /><ExportMenu path="workload/export" params={filters} name="workload" /></>}>
      <WorkspaceTabs />
      <div className="flex flex-wrap items-center gap-2">
        <div className="w-44"><PeoplePicker value={filters.supervisorId} onChange={(v) => set('supervisorId', v)} placeholder={t('workload.anySupervisor')} label={t('param.supervisorId')} /></div>
        <select className={cn(sel, 'max-w-44')} value={filters.disciplineId ?? ''} onChange={(e) => set('disciplineId', e.target.value)} aria-label={t('common.discipline')}>
          <option value="">{t('projects.anyDiscipline')}</option>{ref.data?.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <select className={cn(sel, 'max-w-40')} value={filters.officeId ?? ''} onChange={(e) => set('officeId', e.target.value)} aria-label={t('admin.office')}>
          <option value="">{t('reports.anyOffice')}</option>{ref.data?.offices.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <select className={cn(sel, 'max-w-52')} value={filters.projectId ?? ''} onChange={(e) => set('projectId', e.target.value)} aria-label={t('portfolio.project')}>
          <option value="">{t('workload.anyProject')}</option>{projects.data?.items.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
        </select>
        <select className={sel} value={filters.indicator ?? ''} onChange={(e) => set('indicator', e.target.value)} aria-label={t('workload.indicator')}>
          <option value="">{t('workload.anyIndicator')}</option>{['over', 'under', 'cluster', 'unestimated'].map((x) => <option key={x} value={x}>{t(`workload.ind.${x}`)}</option>)}
        </select>
        <label className="text-xs text-muted-foreground">{t('workload.from')} <Input type="date" className="inline-flex h-8 w-36" value={filters.from ?? ''} onChange={(e) => set('from', e.target.value)} /></label>
        <select className={sel} value={filters.sort ?? ''} onChange={(e) => set('sort', e.target.value)} aria-label={t('common.sort')}>
          {[['', 'workload.sort.load'], ['overdue', 'workload.sort.overdue'], ['tasks', 'workload.sort.tasks'], ['name', 'workload.sort.name']].map(([v, l]) => <option key={v} value={v}>{t(l)}</option>)}
        </select>
      </div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={8} /> : q.data!.people.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('workload.empty')}</Empty></div> : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-[13px]">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr>
                <th className="w-8"><span className="sr-only">{t('common.details')}</span></th><th scope="col" className="px-3 py-2 font-medium">{t('workload.person')}</th>
                {q.data!.weeks.map((w) => <th key={w} scope="col" className={cn('whitespace-nowrap px-2 py-2 text-center font-medium', w === q.data!.currentWeek && 'text-foreground')}>{shortDate(w)}</th>)}
                <th scope="col" className="whitespace-nowrap px-3 py-2 font-medium">{t('workload.summary')}</th>
              </tr>
            </thead>
            {[...groups.entries()].map(([g, people]) => (
              <tbody key={g}>
                {groups.size > 1 && <tr className="border-t bg-muted/30"><th colSpan={q.data!.weeks.length + 3} className="px-3 py-1.5 text-left text-xs font-semibold">{t('workload.reportsTo', { name: g })}</th></tr>}
                {people.map((p) => (
                  <Fragment key={p.id}>
                    <tr className="border-t">
                      <td className="px-2"><button className="rounded p-1 hover:bg-muted" aria-expanded={open.has(p.id)} aria-label={t('workload.showTasks', { name: p.displayName })} onClick={() => toggle(p.id)}>
                        {open.has(p.id) ? <ChevronDown className="size-3.5" /> : <ChevronRight className="size-3.5" />}</button></td>
                      <td className="min-w-[12rem] px-3 py-1.5">
                        <div className="font-medium">{p.displayName}</div>
                        <div className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
                          {t('workload.capacity', { h: hrs(p.capacity) })}{p.capacityOverride && ` (${t('workload.custom')})`}
                          {p.canSetCapacity && <button type="button" className="rounded p-0.5 hover:bg-muted" aria-label={t('workload.setCapacity', { name: p.displayName })} onClick={() => setCapacity(p)}><Pencil className="size-3" /></button>}
                        </div>
                      </td>
                      {p.cells.map((c) => (
                        <td key={c.week} className="px-1 py-1 text-center">
                          <span className={cn('block rounded px-1 py-1 tabular-nums', shade(c.pct))} title={t('workload.cellTitle', { h: hrs(c.hours), cap: hrs(p.capacity), pct: c.pct })}>
                            {hrs(c.hours)}<span className="text-[10px]">/{hrs(p.capacity)}</span>
                          </span>
                        </td>
                      ))}
                      <td className="min-w-[14rem] px-3 py-1.5 text-xs">
                        <div className="flex flex-wrap gap-1">
                          {p.overAssigned && <Chip tone="bad">{t('workload.ind.over')}</Chip>}
                          {p.underAssigned && <Chip tone="idle">{t('workload.ind.under')}</Chip>}
                          {p.cluster && <Chip tone="warn">{t('workload.ind.cluster')}</Chip>}
                        </div>
                        <div className="mt-0.5 text-muted-foreground">{t('workload.counts', { open: p.openTasks, projects: p.projects, overdue: p.overdue })}
                          {p.unestimated > 0 && <span className="ml-1 font-medium text-warn">· {t('workload.unestimatedN', { n: p.unestimated })}</span>}
                          {p.noDueDate > 0 && <span className="ml-1">· {t('workload.noDue', { h: hrs(p.noDueDate) })}</span>}</div>
                      </td>
                    </tr>
                    {open.has(p.id) && <PersonTasksRows id={p.id} from={filters.from} weeks={q.data!.weeks.length} onChanged={() => q.refetch()} />}
                  </Fragment>
                ))}
              </tbody>
            ))}
          </table>
        </div>
      )}
      {capacity && <CapacityDialog p={capacity} defaultCapacity={q.data?.defaultCapacity ?? 40} onClose={(ok) => { setCapacity(null); if (ok) q.refetch() }} />}
    </Page>
  )
}

/** Person → Project → tasks, aligned with the week columns (§13.11), with reassignment where the viewer may (FR-006). */
function PersonTasksRows({ id, from, weeks, onChanged }: { id: string; from?: string | null; weeks: number; onChanged: () => void }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['workload', id, 'tasks', from], queryFn: () => get<PersonTasks>(`workload/${id}/tasks${qs({ from })}`) })
  if (q.isPending) return <tr><td /><td colSpan={weeks + 2}><Loading rows={2} /></td></tr>
  if (q.error) return <tr><td /><td colSpan={weeks + 2}><ErrorBanner error={q.error} /></td></tr>
  if (!q.data.projects.length) return <tr><td /><td colSpan={weeks + 2} className="px-3 py-2 text-xs text-muted-foreground">{t('workload.noTasks')}</td></tr>
  const reassign = async (task: TaskLoadRow, to: string | null) => {
    if (!to) return
    try { await patch(`tasks/${task.id}`, { assigneeId: to }, task.rowVersion); toast.success(t('workload.reassigned', { key: task.key })); qc.invalidateQueries({ queryKey: ['workload'] }); onChanged() }
    catch (e) { toast.error(errorText(e)) }
  }
  return (
    <>
      {q.data.projects.map((p) => (
        <Fragment key={p.id}>
          <tr className="border-t bg-muted/20 text-xs"><td /><td colSpan={weeks + 2} className="px-3 py-1 font-semibold"><Link className="hover:underline" to={`/projects/${encodeURIComponent(p.projectNumber)}/dashboard`}><Key>{p.projectNumber}</Key> {p.name}</Link></td></tr>
          {p.tasks.map((tk) => (
            <tr key={tk.id} className="border-t text-xs">
              <td />
              <td className="px-3 py-1 pl-6">
                <Link className="hover:underline" to={itemHref('Task', p.projectNumber, tk.id)}><Key>{tk.key}</Key> {tk.name}</Link>
                <div className="flex flex-wrap items-center gap-1 text-muted-foreground"><StatusPill status={tk.status} />{t('workload.taskMeta', { due: fmtDate(tk.dueDate), est: tk.estimatedHours ?? '—', left: tk.remaining ?? '—' })}
                  {tk.overdue && <Chip tone="bad">{t('ind.overdue')}</Chip>}{tk.estimatedHours == null && <Chip tone="warn">{t('workload.unestimated')}</Chip>}</div>
              </td>
              {tk.weeks.map((h, i) => <td key={i} className="px-1 py-1 text-center tabular-nums text-muted-foreground">{h ? hrs(h) : ''}</td>)}
              <td className="px-3 py-1">{tk.canReassign && <div className="w-44"><PeoplePicker compact value={null} onChange={(v) => reassign(tk, v)} placeholder={t('workload.reassignTo')} label={t('workload.reassignTask', { key: tk.key })} /></div>}</td>
            </tr>
          ))}
        </Fragment>
      ))}
    </>
  )
}

function CapacityDialog({ p, defaultCapacity, onClose }: { p: PersonRow; defaultCapacity: number; onClose: (ok: boolean) => void }) {
  const [value, setValue] = useState(p.capacityOverride ? String(p.capacity) : '')
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('workload.capacityTitle', { name: p.displayName })} confirmLabel={t('common.save')}
      body={t('workload.capacityHint', { h: defaultCapacity })}
      onConfirm={async () => { await put(`users/${p.id}/capacity`, { hours: value === '' ? null : Number(value) }); toast.success(t('common.saved')); onClose(true) }}>
      <Field label={t('workload.hoursPerWeek')} htmlFor="cap-h"><Input id="cap-h" type="number" min={0} max={80} step={0.5} value={value} placeholder={String(defaultCapacity)} onChange={(e) => setValue(e.target.value)} /></Field>
    </ConfirmDialog>
  )
}

/** The method in plain words (§12.15 step 4), so nobody over-reads the numbers. */
function Method() {
  return (
    <Popover>
      <PopoverTrigger asChild><Button variant="outline" size="sm"><Info className="size-4" />{t('workload.method')}</Button></PopoverTrigger>
      <PopoverContent className="w-[26rem] space-y-2 text-sm" align="end">
        <p className="font-semibold">{t('workload.methodTitle')}</p>
        <ul className="list-disc space-y-1 pl-4">{[1, 2, 3, 4, 5, 6].map((n) => <li key={n}>{t(`workload.method${n}`)}</li>)}</ul>
      </PopoverContent>
    </Popover>
  )
}
