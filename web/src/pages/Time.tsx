import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, ActiveFilters, ConfirmDialog, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Segmented, Spinner, SummaryTile, TableRegion, selectCls, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, del, get, patch, post, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { addDays, fmtDate, today } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'

interface Entry {
  id: string; projectId: string; projectNumber: string; projectName: string; taskId: string; taskKey: string; taskName: string; userId: string; person: string
  workDate: string; hours: number; note?: string | null; rowVersion: number; canEdit: boolean; editNeedsReason: boolean
}
interface TimeView {
  from: string; to: string; entries: Entry[]; total: number; byDay: { date: string; hours: number }[]
  byTask: { taskId: string; taskKey: string; taskName: string; projectNumber: string; hours: number }[]
  byProject: { projectId: string; projectNumber: string; projectName: string; hours: number }[]; canReview: boolean
}
const monday = (d: string) => addDays(d, -((new Date(d + 'T00:00:00Z').getUTCDay() + 6) % 7))
const h = (n: number) => (Number.isInteger(n) ? String(n) : String(Number(n.toFixed(2))))
const weekday = (d: string) => t(`calendar.dow.${new Date(d + 'T00:00:00Z').getUTCDay()}`)

/** Time (§36.8, FR-VIS-10): my task hours by day and week, Add Time, corrections, and — for PMs, leads and supervisors —
 *  the entries they may review. Totals are sums of the entries shown; hours never change estimates or progress. */
export function TimePage() {
  const canEnter = !useMe().capabilities.readOnly // read-only people see hours but record none
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const from = sp.get('from') ?? monday(today())
  const to = sp.get('to') ?? addDays(from, 6)
  const scope = sp.get('scope') === 'all' ? 'all' : undefined
  const filters = { from, to, scope, projectId: sp.get('projectId'), userId: sp.get('userId'), taskId: sp.get('taskId') }
  const q = useQuery({ queryKey: ['time', filters], queryFn: () => get<TimeView>(`time${qs(filters)}`) })
  const projects = useQuery({ queryKey: ['projects-pick', ''], queryFn: () => get<PageOf<{ id: string; projectNumber: string; name: string }>>('projects?pageSize=200') })
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<Entry | null>(null)
  const [removing, setRemoving] = useState<Entry | null>(null)
  const refresh = () => qc.invalidateQueries({ queryKey: ['time'] })
  const reloadEntry = async (id: string) => {
    const next = await q.refetch()
    if (next.error) throw next.error
    return next.data?.entries.find((e) => e.id === id) ?? null
  }
  const shift = (n: number) => { const f = addDays(from, 7 * n); const n2 = new URLSearchParams(sp); n2.set('from', f); n2.set('to', addDays(f, 6)); setSp(n2, { replace: true }) }
  const days = Array.from({ length: 7 }, (_, i) => addDays(from, i))
  const byDate = new Map((q.data?.entries ?? []).reduce((m, e) => m.set(e.workDate, [...(m.get(e.workDate) ?? []), e]), new Map<string, Entry[]>()))
  const now = today()
  // Filters a link may carry (a task's hours) show as removable tokens beside the visible ones (§13.0).
  const entry = (k: 'userId' | 'taskId') => q.data?.entries.find((e) => e[k] === filters[k])
  const tokens = ([
    ['projectId', t('calendar.project'), projects.data?.items.find((p) => p.id === filters.projectId)?.projectNumber],
    ['userId', t('time.person'), entry('userId')?.person],
    ['taskId', t('time.task'), entry('taskId')?.taskKey],
  ] as const).filter(([k]) => filters[k])
  const clear = () => { const n = new URLSearchParams(sp); for (const [k] of tokens) n.delete(k); setSp(n, { replace: true }) }
  const cols = scope ? 6 : 5
  return (
    <Page title={t('nav.time')} subtitle={t('time.subtitle')}
      actions={<>
        <Button variant="outline" asChild><Link to={`/reports/task-hours${qs({ from, to, scope: scope ? 'team' : undefined, projectId: filters.projectId })}`}>{t('time.report')}</Link></Button>
        <ExportMenu path="time/export" params={filters} name="task-hours" />
        {canEnter && <Button onClick={() => setAdding(true)}><Plus className="size-4" />{t('time.add')}</Button>}
      </>}>
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="outline" size="icon" aria-label={t('calendar.previous')} onClick={() => shift(-1)}><ChevronLeft className="size-5" /></Button>
        <span className="min-w-52 text-center text-sm font-semibold tabular-nums" aria-live="polite">{fmtDate(from)} – {fmtDate(to)}</span>
        <Button variant="outline" size="icon" aria-label={t('calendar.next')} onClick={() => shift(1)}><ChevronRight className="size-5" /></Button>
        <Button variant="outline" onClick={() => { const n = new URLSearchParams(sp); n.delete('from'); n.delete('to'); setSp(n, { replace: true }) }}>{t('time.thisWeek')}</Button>
      </div>
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          {q.data?.canReview && (
            <div className="space-y-1.5">
              <p aria-hidden className="text-sm font-medium">{t('time.whose')}</p>
              <Segmented label={t('time.whose')} value={scope ?? 'mine'} onChange={(s) => set('scope', s === 'all' ? 'all' : null)}
                options={[{ value: 'mine', label: t('time.scope.mine') }, { value: 'all', label: t('time.scope.all') }]} />
            </div>
          )}
          <Field label={t('calendar.project')} htmlFor="time-project" className="w-full sm:w-64">
            <select id="time-project" className={selectCls} value={filters.projectId ?? ''} onChange={(e) => set('projectId', e.target.value)}>
              <option value="">{t('workload.anyProject')}</option>{projects.data?.items.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
            </select>
          </Field>
          {scope && <Field label={t('time.person')} htmlFor="time-person" className="w-full sm:w-56">
            <PeoplePicker id="time-person" value={filters.userId} valueName={entry('userId')?.person} onChange={(v) => set('userId', v)} placeholder={t('reports.anyone')} label={t('time.person')} />
          </Field>}
        </div>
        <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? <Missing /> }))} onRemove={(k) => set(k, null)} onClear={clear} />
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={6} /></div> : q.data && (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <div className="min-w-0 space-y-4">
            <ul aria-label={t('time.dailyTotals')} className="grid grid-cols-7 overflow-hidden rounded-lg border bg-card text-center">
              {days.map((d) => {
                const hours = q.data!.byDay.find((x) => x.date === d)?.hours ?? 0
                return (
                  <li key={d} aria-current={d === now ? 'date' : undefined} className={cn('border-l px-1 py-2.5 first:border-l-0', d === now && 'bg-accent')}>
                    <span className={cn('block text-xs/[18px]', d === now ? 'font-semibold text-accent-foreground' : 'text-muted-foreground')}>{d === now ? t('common.today') : weekday(d)} {d.slice(8)}</span>
                    <span className={cn('mt-0.5 block text-base/6 tabular-nums', hours === 0 ? 'text-muted-foreground' : 'font-semibold')}>{h(hours)} h</span>
                  </li>
                )
              })}
            </ul>
            {q.data.entries.length === 0 ? <div className="rounded-lg border bg-card"><Empty action={canEnter && <Button variant="outline" onClick={() => setAdding(true)}>{t('time.add')}</Button>}>{t('time.empty')}</Empty></div> : (
              <TableRegion>
                <table className="w-full text-sm">
                  <caption className="sr-only">{t('time.entries')}</caption>
                  <thead className="bg-muted">
                    <tr>
                      <th scope="col" className={thCls}>{t('time.task')}</th>
                      <th scope="col" className={thCls}>{t('calendar.project')}</th>
                      {scope && <th scope="col" className={thCls}>{t('time.person')}</th>}
                      <th scope="col" className={thCls}>{t('time.note')}</th>
                      <th scope="col" className={cn(thCls, 'text-right')}>{t('time.hours')}</th>
                      <th scope="col" className={thCls}><span className="sr-only">{t('common.actions')}</span></th>
                    </tr>
                  </thead>
                  {days.filter((d) => byDate.has(d)).map((d) => (
                    <tbody key={d}>
                      <tr className="border-t bg-muted">
                        <th scope="rowgroup" colSpan={cols - 2} className="px-(--cell-px) py-2 text-left font-semibold">{weekday(d)} <span className="tabular-nums">{fmtDate(d)}</span></th>
                        <td className="whitespace-nowrap px-(--cell-px) py-2 text-right font-semibold tabular-nums">{h(byDate.get(d)!.reduce((s, e) => s + e.hours, 0))} h</td>
                        <td />
                      </tr>
                      {byDate.get(d)!.map((e) => (
                        <tr key={e.id} className="border-t hover:bg-muted">
                          <td className={cn(tdCls, 'min-w-48')}><Key>{e.taskKey}</Key> <span className="font-medium">{e.taskName}</span></td>
                          <td className={cn(tdCls, 'min-w-40')}>
                            <span className="inline-flex items-center gap-2"><AccentDot id={e.projectId} /><Key>{e.projectNumber}</Key></span>
                            <span className="block text-xs/[18px] text-muted-foreground">{e.projectName}</span>
                          </td>
                          {scope && <td className={cn(tdCls, 'whitespace-nowrap')}><span className="inline-flex items-center gap-2"><Avatar id={e.userId} name={e.person} />{e.person}</span></td>}
                          <td className={cn(tdCls, 'min-w-40 text-muted-foreground')}>{e.note}</td>
                          <td className={cn(tdCls, 'whitespace-nowrap text-right font-semibold tabular-nums')}>{h(e.hours)} h</td>
                          <td className={cn(tdCls, 'whitespace-nowrap py-1 text-right')}>{e.canEdit && <>
                            <Button variant="ghost" size="icon-sm" aria-label={t('time.edit', { key: e.taskKey })} onClick={() => setEditing(e)}><Pencil className="size-4" /></Button>
                            <Button variant="ghost" size="icon-sm" className="text-bad hover:text-bad" aria-label={t('time.delete', { key: e.taskKey })} onClick={() => setRemoving(e)}><Trash2 className="size-4" /></Button>
                          </>}</td>
                        </tr>
                      ))}
                    </tbody>
                  ))}
                </table>
              </TableRegion>
            )}
          </div>
          <aside className="space-y-4" aria-label={t('time.totals')}>
            <SummaryTile label={t('time.weekTotal')} value={`${h(q.data.total)} h`} accent="blue" />
            {q.data.byProject.length > 0 && <Totals title={t('time.byProject')} rows={q.data.byProject.map((x) => ({ id: x.projectId, title: `${x.projectNumber} ${x.projectName}`, hours: x.hours,
              label: <><AccentDot id={x.projectId} className="mr-2" /><Key>{x.projectNumber}</Key> {x.projectName}</> }))} />}
            {q.data.byTask.length > 0 && <Totals title={t('time.byTask')} rows={q.data.byTask.map((x) => ({ id: x.taskId, title: `${x.taskKey} ${x.taskName}`, hours: x.hours,
              label: <><Key>{x.taskKey}</Key> {x.taskName}</> }))} />}
            <p className="text-xs/[18px] text-muted-foreground">{t('time.separate')}</p>
          </aside>
        </div>
      )}
      {adding && <EntryDialog onClose={(ok) => { setAdding(false); if (ok) refresh() }} />}
      {editing && <EntryDialog entry={editing} onReload={reloadEntry} onClose={(ok) => { setEditing(null); if (ok) refresh() }} />}
      {removing && <ConfirmDialog open onOpenChange={(o) => !o && setRemoving(null)} destructive reason={removing.editNeedsReason ? true : undefined}
        title={t('time.deleteTitle', { h: h(removing.hours), key: removing.taskKey })} body={t('time.deleteHint')} confirmLabel={t('time.deleteConfirm')}
        onConfirm={async (reason) => { await del(`time/${removing.id}`, { reason: reason || undefined }, removing.rowVersion); toast.success(t('time.deleted')); refresh() }} />}
    </Page>
  )
}

/** A read-only total per project or task: sums of the entries shown, right-aligned in hours. */
function Totals({ title, rows }: { title: string; rows: { id: string; label: ReactNode; title: string; hours: number }[] }) {
  return (
    <section className="overflow-hidden rounded-lg border bg-card">
      <h2 className="border-b px-4 py-2.5 text-sm font-semibold">{title}</h2>
      <table className="w-full text-sm">
        <tbody>{rows.map((r) => (
          <tr key={r.id} className="border-t first:border-t-0">
            <th scope="row" className="w-full max-w-0 truncate px-4 py-2 text-left font-normal" title={r.title}>{r.label}</th>
            <td className="whitespace-nowrap px-4 py-2 text-right font-medium tabular-nums">{h(r.hours)} h</td>
          </tr>
        ))}</tbody>
      </table>
    </section>
  )
}

/** Add Time or correct an entry (FR-002, FR-003): one task, a work date, positive hours up to 24, an optional note; a PM
 *  correcting someone else's entry gives a reason. */
function EntryDialog({ entry, onReload, onClose }: { entry?: Entry; onReload?: (id: string) => Promise<Entry | null>; onClose: (ok: boolean) => void }) {
  const [currentEntry, setCurrentEntry] = useState(entry)
  const [task, setTask] = useState<{ id: string; label: string } | null>(currentEntry ? { id: currentEntry.taskId, label: `${currentEntry.taskKey} ${currentEntry.taskName}` } : null)
  const [term, setTerm] = useState('')
  const [f, setF] = useState({ workDate: currentEntry?.workDate ?? today(), hours: currentEntry ? String(currentEntry.hours) : '', note: currentEntry?.note ?? '', reason: '' })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const [reloaded, setReloaded] = useState(false)
  const [reloading, setReloading] = useState(false)
  const [hasConflict, setHasConflict] = useState(false)
  const tasks = useQuery({ queryKey: ['time-tasks', term], enabled: !entry, queryFn: () => get<PageOf<{ id: string; key: string; name: string; projectNumber: string; status: string }>>(`tasks${qs({ q: term, pageSize: 20 })}`) })
  const fe = err?.fieldErrors ?? {}
  const reload = async () => {
    if (!currentEntry || !onReload) return
    setReloading(true)
    try {
      const latest = await onReload(currentEntry.id)
      if (!latest) throw new ApiError(404, { detail: t('app.notFound') })
      setCurrentEntry(latest)
      setTask({ id: latest.taskId, label: `${latest.taskKey} ${latest.taskName}` })
      setF((old) => ({ ...old, workDate: latest.workDate, hours: String(latest.hours), note: latest.note ?? '' }))
      setReloaded(true)
      setHasConflict(false)
      setErr(null)
    } catch (e) {
      setErr(e as ApiError)
    } finally {
      setReloading(false)
    }
  }
  const save = async () => {
    if (busy || reloading || hasConflict || (currentEntry && !currentEntry.canEdit) || (!currentEntry && !task) || !f.hours) return
    setBusy(true); setErr(null)
    try {
      if (currentEntry) await patch(`time/${currentEntry.id}`, { workDate: f.workDate, hours: Number(f.hours), note: f.note || null, reason: f.reason || undefined }, currentEntry.rowVersion)
      else await post('time', { taskId: task!.id, workDate: f.workDate, hours: Number(f.hours), note: f.note || null })
      toast.success(t('common.saved')); onClose(true)
    } catch (e) {
      const apiErr = e as ApiError
      if (apiErr.status === 409 || apiErr.code === 'concurrency_conflict') setHasConflict(true)
      setErr(apiErr)
    } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose(false)}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader><DialogTitle>{currentEntry ? t('time.editTitle') : t('time.add')}</DialogTitle><DialogDescription>{t('time.dialogHint')}</DialogDescription></DialogHeader>
        {hasConflict && <p role="status" className="rounded-md border border-warn/40 bg-warn-bg px-3 py-2 text-sm text-warn">{t('time.reloadHint')}</p>}
        {err && !Object.keys(fe).length && <ErrorBanner error={err} retry={hasConflict ? () => { if (!reloading) void reload() } : undefined} retryLabel={hasConflict ? t('error.reload') : undefined} />}
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); save() }}>
          {currentEntry ? <p className="text-sm"><span className="text-muted-foreground">{t('time.task')}:</span> {task?.label}</p> : (
            <Field label={t('time.task')} htmlFor="te-task" error={fe.taskId}>
              {task ? <div className="flex min-h-(--control-h) items-center gap-2 rounded-md bg-muted px-3 text-sm"><span className="flex-1 truncate">{task.label}</span><Button type="button" variant="ghost" size="sm" onClick={() => setTask(null)}>{t('time.changeTask')}</Button></div> : <>
                <Input id="te-task" type="search" autoFocus placeholder={t('task.searchPlaceholder')} value={term} onChange={(e) => setTerm(e.target.value)} />
                <ul className="max-h-48 overflow-y-auto rounded-md border" role="listbox" aria-label={t('time.task')}>
                  {(tasks.data?.items ?? []).map((x) => (
                    <li key={x.id}><button type="button" role="option" aria-selected={false} className="flex min-h-(--control-row-h) w-full items-center gap-2 px-3 py-1.5 text-left text-sm hover:bg-muted" onClick={() => setTask({ id: x.id, label: `${x.key} ${x.name}` })}>
                      <Key>{x.key}</Key><span className="min-w-0 flex-1 truncate">{x.name}</span><span className="text-xs/[18px] text-muted-foreground">{x.projectNumber}</span></button></li>
                  ))}
                </ul>
              </>}
            </Field>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('time.workDate')} htmlFor="te-date" error={fe.workDate}><Input id="te-date" type="date" required value={f.workDate} onChange={(e) => setF({ ...f, workDate: e.target.value })} /></Field>
            <Field label={t('time.hours')} htmlFor="te-hours" error={fe.hours} hint={t('time.hoursHint')}>
              <div className="flex items-center gap-2"><Input id="te-hours" type="number" required min={0.25} max={24} step={0.25} value={f.hours} onChange={(e) => setF({ ...f, hours: e.target.value })} /><span aria-hidden className="text-sm text-muted-foreground">h</span></div>
            </Field>
          </div>
          <Field label={t('time.note')} htmlFor="te-note" optional><Textarea id="te-note" rows={2} maxLength={1000} value={f.note} onChange={(e) => setF({ ...f, note: e.target.value })} /></Field>
          {currentEntry?.editNeedsReason && <Field label={t('common.reason')} htmlFor="te-reason" error={fe.reason} hint={t('time.reasonHint', { name: currentEntry.person })}><Textarea id="te-reason" rows={2} required value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} /></Field>}
          {err && Object.keys(fe).length > 0 && <ErrorBanner error={err} />}
          {reloaded && <p role="status" className="rounded-md border border-warn/40 bg-warn-bg px-3 py-2 text-sm text-warn">{currentEntry?.canEdit ? t('time.reloaded') : t('time.noLongerEditable')}</p>}
          <DialogFooter><Button type="button" variant="outline" onClick={() => onClose(false)}>{t('common.cancel')}</Button>{currentEntry?.canEdit !== false && <Button type="submit" disabled={busy || reloading || hasConflict || (!currentEntry && !task) || !f.hours}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.save')}</Button>}</DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
