import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, Spinner } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { PeoplePicker } from '@/components/hub/people'
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
  const shift = (n: number) => { const f = addDays(from, 7 * n); const n2 = new URLSearchParams(sp); n2.set('from', f); n2.set('to', addDays(f, 6)); setSp(n2, { replace: true }) }
  const days = Array.from({ length: 7 }, (_, i) => addDays(from, i))
  const byDate = new Map((q.data?.entries ?? []).reduce((m, e) => m.set(e.workDate, [...(m.get(e.workDate) ?? []), e]), new Map<string, Entry[]>()))
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  return (
    <Page title={t('nav.time')} subtitle={t('time.subtitle')}
      actions={<>
        <Button variant="ghost" size="sm" asChild><Link to={`/reports/task-hours${qs({ from, to, scope: scope ? 'team' : undefined, projectId: filters.projectId })}`}>{t('time.report')}</Link></Button>
        <ExportMenu path="time/export" params={filters} name="task-hours" />
        {canEnter && <Button onClick={() => setAdding(true)}><Plus className="size-4" />{t('time.add')}</Button>}
      </>}>
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="outline" size="sm" onClick={() => { const n = new URLSearchParams(sp); n.delete('from'); n.delete('to'); setSp(n, { replace: true }) }}>{t('time.thisWeek')}</Button>
        <Button variant="ghost" size="icon" className="size-8" aria-label={t('calendar.previous')} onClick={() => shift(-1)}><ChevronLeft className="size-4" /></Button>
        <Button variant="ghost" size="icon" className="size-8" aria-label={t('calendar.next')} onClick={() => shift(1)}><ChevronRight className="size-4" /></Button>
        <span className="text-sm font-medium" aria-live="polite">{fmtDate(from)} – {fmtDate(to)}</span>
        <div className="flex-1" />
        {q.data?.canReview && (
          <div className="inline-flex overflow-hidden rounded-md border bg-card" role="group" aria-label={t('time.whose')}>
            {[undefined, 'all'].map((s) => <button key={s ?? 'mine'} type="button" aria-pressed={scope === s} onClick={() => set('scope', s ?? null)}
              className={cn('h-8 px-3 text-sm', scope === s ? 'bg-accent font-medium' : 'hover:bg-muted')}>{t(s ? 'time.scope.all' : 'time.scope.mine')}</button>)}
          </div>
        )}
        <select className={cn(sel, 'max-w-52')} value={filters.projectId ?? ''} onChange={(e) => set('projectId', e.target.value)} aria-label={t('calendar.project')}>
          <option value="">{t('workload.anyProject')}</option>{projects.data?.items.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
        </select>
        {scope && <div className="w-44"><PeoplePicker value={filters.userId} onChange={(v) => set('userId', v)} placeholder={t('reports.anyone')} label={t('time.person')} /></div>}
      </div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={6} /> : (
        <div className="grid gap-4 lg:grid-cols-[1fr_18rem]">
          <div className="space-y-3">
            <div className="grid grid-cols-7 gap-1 rounded-lg border bg-card p-2 text-center text-xs" aria-label={t('time.dailyTotals')}>
              {days.map((d) => {
                const hours = q.data!.byDay.find((x) => x.date === d)?.hours ?? 0
                return <div key={d} className={cn('rounded px-1 py-1', d === today() && 'bg-accent')}>
                  <div className="text-muted-foreground">{t(`calendar.dow.${new Date(d + 'T00:00:00Z').getUTCDay()}`)} {d.slice(8)}</div>
                  <div className={cn('text-sm font-semibold tabular-nums', hours === 0 && 'text-muted-foreground')}>{h(hours)} h</div>
                </div>
              })}
            </div>
            {q.data!.entries.length === 0 ? <div className="rounded-lg border bg-card"><Empty action={canEnter && <Button onClick={() => setAdding(true)}>{t('time.add')}</Button>}>{t('time.empty')}</Empty></div> : (
              <div className="divide-y rounded-lg border bg-card">
                {days.filter((d) => byDate.has(d)).map((d) => (
                  <section key={d} aria-label={fmtDate(d)}>
                    <h2 className="flex justify-between bg-muted/40 px-4 py-1.5 text-sm font-semibold"><span>{fmtDate(d)}</span><span className="tabular-nums">{h(byDate.get(d)!.reduce((s, e) => s + e.hours, 0))} h</span></h2>
                    <ul className="divide-y">
                      {byDate.get(d)!.map((e) => (
                        <li key={e.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2 text-sm">
                          <span className="min-w-0 flex-1"><Key>{e.taskKey}</Key> <span className="font-medium">{e.taskName}</span>
                            <span className="block text-xs text-muted-foreground">{e.projectNumber} · {e.projectName}{scope ? ` · ${e.person}` : ''}{e.note ? ` · ${e.note}` : ''}</span></span>
                          <span className="w-16 text-right font-semibold tabular-nums">{h(e.hours)} h</span>
                          {e.canEdit && <span className="flex gap-1">
                            <Button variant="ghost" size="icon" className="size-7" aria-label={t('time.edit', { key: e.taskKey })} onClick={() => setEditing(e)}><Pencil className="size-3.5" /></Button>
                            <Button variant="ghost" size="icon" className="size-7 text-bad" aria-label={t('time.delete', { key: e.taskKey })} onClick={() => setRemoving(e)}><Trash2 className="size-3.5" /></Button>
                          </span>}
                        </li>
                      ))}
                    </ul>
                  </section>
                ))}
              </div>
            )}
          </div>
          <aside className="space-y-3" aria-label={t('time.totals')}>
            <div className="rounded-lg border bg-card p-3"><div className="text-xs text-muted-foreground">{t('time.weekTotal')}</div><div className="text-2xl font-semibold tabular-nums">{h(q.data!.total)} h</div></div>
            {q.data!.byProject.length > 0 && <Totals title={t('time.byProject')} rows={q.data!.byProject.map((x) => ({ id: x.projectId, label: `${x.projectNumber} ${x.projectName}`, hours: x.hours }))} />}
            {q.data!.byTask.length > 0 && <Totals title={t('time.byTask')} rows={q.data!.byTask.map((x) => ({ id: x.taskId, label: `${x.taskKey} ${x.taskName}`, hours: x.hours }))} />}
            <p className="text-xs text-muted-foreground">{t('time.separate')}</p>
          </aside>
        </div>
      )}
      {adding && <EntryDialog onClose={(ok) => { setAdding(false); if (ok) refresh() }} />}
      {editing && <EntryDialog entry={editing} onClose={(ok) => { setEditing(null); if (ok) refresh() }} />}
      {removing && <ConfirmDialog open onOpenChange={(o) => !o && setRemoving(null)} destructive reason={removing.editNeedsReason ? true : undefined}
        title={t('time.deleteTitle', { h: h(removing.hours), key: removing.taskKey })} body={t('time.deleteHint')} confirmLabel={t('time.deleteConfirm')}
        onConfirm={async (reason) => { await del(`time/${removing.id}`, { reason: reason || undefined }, removing.rowVersion); toast.success(t('time.deleted')); refresh() }} />}
    </Page>
  )
}

function Totals({ title, rows }: { title: string; rows: { id: string; label: string; hours: number }[] }) {
  return (
    <div className="rounded-lg border bg-card">
      <h3 className="border-b px-3 py-1.5 text-xs font-semibold">{title}</h3>
      <ul className="divide-y text-sm">{rows.map((r) => <li key={r.id} className="flex gap-2 px-3 py-1.5"><span className="min-w-0 flex-1 truncate" title={r.label}>{r.label}</span><span className="tabular-nums">{h(r.hours)} h</span></li>)}</ul>
    </div>
  )
}

/** Add Time or correct an entry (FR-002, FR-003): one task, a work date, positive hours up to 24, an optional note; a PM
 *  correcting someone else's entry gives a reason. */
function EntryDialog({ entry, onClose }: { entry?: Entry; onClose: (ok: boolean) => void }) {
  const [task, setTask] = useState<{ id: string; label: string } | null>(entry ? { id: entry.taskId, label: `${entry.taskKey} ${entry.taskName}` } : null)
  const [term, setTerm] = useState('')
  const [f, setF] = useState({ workDate: entry?.workDate ?? today(), hours: entry ? String(entry.hours) : '', note: entry?.note ?? '', reason: '' })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const tasks = useQuery({ queryKey: ['time-tasks', term], enabled: !entry, queryFn: () => get<PageOf<{ id: string; key: string; name: string; projectNumber: string; status: string }>>(`tasks${qs({ q: term, pageSize: 20 })}`) })
  const fe = err?.fieldErrors ?? {}
  const save = async () => {
    setBusy(true); setErr(null)
    try {
      if (entry) await patch(`time/${entry.id}`, { workDate: f.workDate, hours: Number(f.hours), note: f.note || null, reason: f.reason || undefined }, entry.rowVersion)
      else await post('time', { taskId: task!.id, workDate: f.workDate, hours: Number(f.hours), note: f.note || null })
      toast.success(t('common.saved')); onClose(true)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose(false)}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader><DialogTitle>{entry ? t('time.editTitle') : t('time.add')}</DialogTitle><DialogDescription>{t('time.dialogHint')}</DialogDescription></DialogHeader>
        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); save() }}>
          {entry ? <p className="text-sm"><span className="text-muted-foreground">{t('time.task')}:</span> {task?.label}</p> : (
            <Field label={t('time.task')} htmlFor="te-task" error={fe.taskId}>
              {task ? <div className="flex items-center gap-2 text-sm"><span className="flex-1 truncate">{task.label}</span><Button type="button" variant="ghost" size="sm" onClick={() => setTask(null)}>{t('time.changeTask')}</Button></div> : <>
                <Input id="te-task" type="search" autoFocus placeholder={t('task.searchPlaceholder')} value={term} onChange={(e) => setTerm(e.target.value)} />
                <ul className="max-h-40 overflow-y-auto rounded border" role="listbox" aria-label={t('time.task')}>
                  {(tasks.data?.items ?? []).map((x) => (
                    <li key={x.id}><button type="button" role="option" aria-selected={false} className="flex w-full items-center gap-2 px-2 py-1 text-left text-sm hover:bg-muted" onClick={() => setTask({ id: x.id, label: `${x.key} ${x.name}` })}>
                      <Key>{x.key}</Key><span className="min-w-0 flex-1 truncate">{x.name}</span><span className="text-xs text-muted-foreground">{x.projectNumber}</span></button></li>
                  ))}
                </ul>
              </>}
            </Field>
          )}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('time.workDate')} htmlFor="te-date" error={fe.workDate}><Input id="te-date" type="date" required value={f.workDate} onChange={(e) => setF({ ...f, workDate: e.target.value })} /></Field>
            <Field label={t('time.hours')} htmlFor="te-hours" error={fe.hours} hint={t('time.hoursHint')}><Input id="te-hours" type="number" required min={0.25} max={24} step={0.25} value={f.hours} onChange={(e) => setF({ ...f, hours: e.target.value })} /></Field>
          </div>
          <Field label={t('time.note')} htmlFor="te-note"><Textarea id="te-note" rows={2} maxLength={1000} value={f.note} onChange={(e) => setF({ ...f, note: e.target.value })} /></Field>
          {entry?.editNeedsReason && <Field label={t('common.reason')} htmlFor="te-reason" error={fe.reason} hint={t('time.reasonHint', { name: entry.person })}><Textarea id="te-reason" rows={2} required value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} /></Field>}
          {err && !Object.keys(fe).length && <ErrorBanner error={err} />}
          <DialogFooter><Button type="button" variant="outline" onClick={() => onClose(false)}>{t('common.cancel')}</Button><Button type="submit" disabled={busy || (!entry && !task) || !f.hours}>{busy && <Spinner />}{t('common.save')}</Button></DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
