import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronRight, MoreHorizontal, Plus } from 'lucide-react'
import { Fragment, useState } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, Section, selectCls } from '@/components/hub/common'
import { FieldRow, HistoryList, InlineText, TabBar } from '@/components/hub/fields'
import { PANELS, useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { Key, ProgressBar, StatusPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProjectRefresh, useReference } from '@/hooks/data'
import { ApiError, get, patch, post, qs } from '@/lib/api'
import { daysBetween, fmtDate, relative, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import { CommentsSlot, ItemSlots } from './slots-items'
import { useCurrentProject } from './ProjectLayout'
import { ExportMenu } from '@/components/hub/export'

export interface MilestoneRow {
  id: string; projectId: string; key: string; name: string; milestoneType: string; date?: string; originalDate?: string; description?: string
  projectDisciplineId?: string; completesPhaseId?: string; isClientFacing: boolean; isComplete: boolean; completedDate?: string; isCancelled: boolean
  cancelledReason?: string; rowVersion: number; isSubmission: boolean; status?: string | null; statusReasons?: any[] | null; slipDays: number
  daysRemaining?: number | null; deliverableTotal: number; deliverableIssued: number; taskTotal: number; taskComplete: number; taskOpen: number
  taskOverdue: number; taskBlocked: number
}

const TYPES = ['Kickoff', 'Field Work', 'Design Submission', 'Client Workshop', 'Permit Submission', 'Tender', 'Construction', 'IFC', 'Record Drawings', 'Closeout', 'Other']

export function milestoneStatusLabel(m: MilestoneRow) {
  if (!m.date && !m.isComplete && !m.isCancelled) return t('milestone.undated')
  return m.status ?? t('milestone.notEvaluated')
}

/** Milestone View (§13.7): strip with slip ghosts, table with readiness, and the PM's lifecycle actions. */
export function MilestonesTab() {
  const p = useCurrentProject()
  const ref = useReference()
  const [showCompleted, setShowCompleted] = useState(false)
  const [type, setType] = useState('')
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [dialog, setDialog] = useState<{ kind: 'new' | 'edit' | 'date' | 'complete' | 'cancel' | 'reopen'; m?: MilestoneRow } | null>(null)
  const openPanel = useItemPanel()
  const q = useQuery({ queryKey: ['p', p.id, 'milestones', showCompleted, type], queryFn: () => get<MilestoneRow[]>(`projects/${p.id}/milestones${qs({ showCompleted, type })}`) })
  const can = p.permissions.manageMilestones.ok
  const rows = q.data ?? []
  const disc = (id?: string) => p.disciplines.find((d) => d.id === id)?.name
  return (
    <Page title={t('ptab.milestones')} actions={can && <Button onClick={() => setDialog({ kind: 'new' })}><Plus className="size-4" />{t('milestone.new')}</Button>}>
      {!can && p.permissions.manageMilestones.reason && <p className="text-sm text-muted-foreground">{p.permissions.manageMilestones.reason}</p>}
      <MilestoneStrip rows={rows} onOpen={(m) => openPanel('Milestone', m.id)} />
      <div className="flex flex-wrap items-center gap-3">
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={type} onChange={(e) => setType(e.target.value)} aria-label={t('common.type')}>
          <option value="">{t('milestone.anyType')}</option>{TYPES.map((x) => <option key={x} value={x}>{t(`mtype.${x}`)}</option>)}
        </select>
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={showCompleted} onCheckedChange={(c) => setShowCompleted(!!c)} />{t('milestone.showCompleted')}</label>
        <div className="flex-1" />
        <ExportMenu path={`projects/${p.id}/milestones/export`} params={{ showCompleted, type }} name={`${p.projectNumber}-milestones`} />
      </div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading /> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={can && <Button onClick={() => setDialog({ kind: 'new' })}>{t('milestone.new')}</Button>}>{t('milestone.empty')}</Empty></div>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-[13px]">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr>{['', 'milestone.key', 'common.name', 'common.type', 'common.date', 'milestone.original', 'milestone.slip', 'common.status', 'milestone.remaining', 'milestone.deliverables', 'milestone.tasks', 'common.discipline', 'milestone.clientFacing', '']
                .map((h, i) => <th key={i} className="whitespace-nowrap px-3 py-2 font-medium">{h && t(h)}</th>)}</tr>
            </thead>
            <tbody>
              {rows.map((m) => (
                <Fragment key={m.id}>
                  <tr className="border-t hover:bg-muted/30">
                    <td className="px-2"><button aria-label={t('common.details')} aria-expanded={open.has(m.id)} className="rounded p-1 hover:bg-muted"
                      onClick={() => { const n = new Set(open); if (n.has(m.id)) n.delete(m.id); else n.add(m.id); setOpen(n) }}>
                      {open.has(m.id) ? <ChevronDown className="size-3.5" /> : <ChevronRight className="size-3.5" />}</button></td>
                    <td className="whitespace-nowrap px-3 py-1.5"><Key>{m.key}</Key></td>
                    <td className="px-3 py-1.5 font-medium"><button className="text-left hover:underline" onClick={() => openPanel('Milestone', m.id)}>{m.isSubmission && '◆ '}{m.name}</button></td>
                    <td className="px-3 py-1.5">{t(`mtype.${m.milestoneType}`)}</td>
                    <td className="whitespace-nowrap px-3 py-1.5">{fmtDate(m.date)}</td>
                    <td className="whitespace-nowrap px-3 py-1.5 text-muted-foreground">{fmtDate(m.originalDate)}</td>
                    <td className={cn('px-3 py-1.5 tabular-nums', m.slipDays > 0 && 'text-warn')}>{m.slipDays ? `${m.slipDays > 0 ? '+' : ''}${m.slipDays}d` : '0'}</td>
                    <td className="px-3 py-1.5"><Why reasons={m.statusReasons} title={t('milestone.statusWhy', { key: m.key })}><StatusPill status={milestoneStatusLabel(m)} /></Why></td>
                    <td className="whitespace-nowrap px-3 py-1.5">{m.isComplete ? fmtDate(m.completedDate) : m.date ? relative(m.date) : t('common.dash')}</td>
                    <td className="whitespace-nowrap px-3 py-1.5 tabular-nums">{t('milestone.issuedOf', { n: m.deliverableIssued, total: m.deliverableTotal })}</td>
                    <td className="whitespace-nowrap px-3 py-1.5 tabular-nums">{m.taskComplete}/{m.taskTotal}{m.taskOverdue > 0 && <span className="ml-1 text-bad">· {t('ind.overdueN', { n: m.taskOverdue })}</span>}{m.taskBlocked > 0 && <span className="ml-1 text-bad">· {t('ind.blockedN', { n: m.taskBlocked })}</span>}</td>
                    <td className="px-3 py-1.5">{disc(m.projectDisciplineId) ?? t('common.dash')}</td>
                    <td className="px-3 py-1.5">{m.isClientFacing ? t('common.yes') : t('common.no')}</td>
                    <td className="px-2 py-1.5 text-right">{can && <MilestoneMenu m={m} onPick={(kind) => setDialog({ kind, m })} />}</td>
                  </tr>
                  {open.has(m.id) && <tr className="border-t bg-muted/20"><td colSpan={14} className="px-8 py-2"><TargetedDeliverables milestoneId={m.id} /></td></tr>}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {dialog && <MilestoneDialogs p={p} kind={dialog.kind} m={dialog.m} phases={ref.data?.phases ?? []} onClose={() => setDialog(null)} />}
    </Page>
  )
}

function MilestoneMenu({ m, onPick }: { m: MilestoneRow; onPick: (k: 'edit' | 'date' | 'complete' | 'cancel' | 'reopen') => void }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild><Button variant="ghost" size="icon" className="size-7" aria-label={t('common.more')}><MoreHorizontal className="size-4" /></Button></DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={() => onPick('edit')}>{t('common.edit')}</DropdownMenuItem>
        {!m.isComplete && !m.isCancelled && m.date && <DropdownMenuItem onSelect={() => onPick('date')}>{t('milestone.changeDate')}</DropdownMenuItem>}
        {!m.isComplete && !m.isCancelled && <DropdownMenuItem onSelect={() => onPick('complete')}>{t('milestone.complete')}</DropdownMenuItem>}
        {m.isComplete && <DropdownMenuItem onSelect={() => onPick('reopen')}>{t('milestone.reopen')}</DropdownMenuItem>}
        {!m.isComplete && !m.isCancelled && <DropdownMenuItem onSelect={() => onPick('cancel')}>{t('milestone.cancel')}</DropdownMenuItem>}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function TargetedDeliverables({ milestoneId }: { milestoneId: string }) {
  const q = useQuery({ queryKey: ['milestone', milestoneId], queryFn: () => get(`milestones/${milestoneId}`) })
  const openPanel = useItemPanel()
  if (q.isPending) return <Loading rows={2} />
  const list: any[] = q.data?.deliverables ?? []
  if (!list.length) return <p className="py-2 text-sm text-muted-foreground">{t('milestone.noDeliverables')}</p>
  return (
    <table className="w-full text-[13px]">
      <tbody>
        {list.map((d) => (
          <tr key={d.id} className="border-b last:border-0">
            <td className="py-1 pr-3"><Key>{d.key}</Key></td>
            <td className="py-1 pr-3"><button className="hover:underline" onClick={() => openPanel('Deliverable', d.id)}>{d.name}</button></td>
            <td className="py-1 pr-3"><StatusPill status={d.status} /></td>
            <td className="py-1 pr-3">{d.ownerName}</td>
            <td className="py-1 pr-3">{fmtDate(d.dueDate)}</td>
            <td className="py-1 pr-3"><ProgressBar pct={d.state?.progressPct} /></td>
            <td className="py-1 text-xs text-muted-foreground">{d.state ? t('milestone.taskCounts', { open: d.state.taskOpen, overdue: d.state.taskOverdue, blocked: d.state.taskBlocked }) : ''}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/** Strip of milestones on a date axis with a today marker; hollow ghosts mark original dates when slipped (§13.7). */
export function MilestoneStrip({ rows, onOpen, limit }: { rows: MilestoneRow[]; onOpen: (m: MilestoneRow) => void; limit?: number }) {
  const dated = rows.filter((m) => m.date && !m.isCancelled).slice(0, limit ?? 100)
  if (!dated.length) return null
  const dates = [...dated.flatMap((m) => [m.date!, m.originalDate ?? m.date!]), today()]
  const min = dates.reduce((a, b) => (a < b ? a : b)), max = dates.reduce((a, b) => (a > b ? a : b))
  const span = Math.max(daysBetween(min, max), 1)
  const x = (d: string) => 4 + (daysBetween(min, d) / span) * 92
  const tone = (m: MilestoneRow) => m.isComplete ? 'var(--done)' : m.status === 'Overdue' ? 'var(--bad)' : m.status === 'At Risk' ? 'var(--warn)' : m.status === 'On Track' ? 'var(--ok)' : 'var(--idle)'
  return (
    <div className="rounded-lg border bg-card px-4 py-3" role="group" aria-label={t('milestone.stripLabel', { n: dated.length })}>
      <div className="relative h-16">
        <div className="absolute inset-x-0 top-6 h-px bg-border" />
        <div className="absolute top-1 h-10 border-l-2 border-dashed border-primary" style={{ left: `${x(today())}%` }} title={t('common.today')}>
          <span className="absolute -top-1 left-1 text-[10px] font-medium text-primary">{t('common.today')}</span>
        </div>
        {dated.map((m) => (
          <Fragment key={m.id}>
            {m.originalDate && m.originalDate !== m.date && (
              <span aria-hidden className="absolute top-[18px] size-3 rotate-45 border-2 bg-card" style={{ left: `calc(${x(m.originalDate)}% - 6px)`, borderColor: tone(m) }} title={`${m.name}: ${t('milestone.original')} ${fmtDate(m.originalDate)}`} />
            )}
            <button className="group absolute top-[16px] -translate-x-1/2" style={{ left: `${x(m.date!)}%` }} onClick={() => onOpen(m)}
              title={`${m.key} ${m.name} · ${fmtDate(m.date)} · ${milestoneStatusLabel(m)}`} aria-label={`${m.key} ${m.name}, ${fmtDate(m.date)}, ${tv(milestoneStatusLabel(m))}`}>
              <span className="block size-4 rotate-45 border border-white shadow" style={{ background: tone(m) }} />
              <span className="absolute left-1/2 top-5 max-w-28 -translate-x-1/2 truncate whitespace-nowrap text-[11px] text-muted-foreground group-hover:text-foreground">{m.name}</span>
            </button>
          </Fragment>
        ))}
      </div>
    </div>
  )
}

function MilestoneDialogs({ p, kind, m, phases, onClose }: { p: ProjectDetail; kind: string; m?: MilestoneRow; phases: { id: string; name: string; isActive: boolean }[]; onClose: () => void }) {
  const refresh = useProjectRefresh()
  const qc = useQueryClient()
  const done = () => { qc.invalidateQueries({ queryKey: ['p', p.id] }); qc.invalidateQueries({ queryKey: ['milestone'] }); refresh(p.id); onClose() }
  if (kind === 'new' || kind === 'edit') return <MilestoneForm p={p} m={m} phases={phases} onClose={onClose} onDone={done} />
  if (kind === 'date') return <ChangeDateDialog m={m!} onClose={onClose} onDone={done} />
  if (kind === 'complete') return <CompleteDialog p={p} m={m!} onClose={onClose} onDone={done} />
  if (kind === 'reopen') return <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('milestone.reopenTitle', { key: m!.key })} reason
    onConfirm={async (reason) => { await post(`milestones/${m!.id}/reopen`, { reason, rowVersion: m!.rowVersion }); done() }} />
  return <CancelDialog p={p} m={m!} onClose={onClose} onDone={done} />
}

function MilestoneForm({ p, m, phases, onClose, onDone }: { p: ProjectDetail; m?: MilestoneRow; phases: { id: string; name: string; isActive: boolean }[]; onClose: () => void; onDone: () => void }) {
  const [f, setF] = useState<Record<string, any>>(m ? { name: m.name, milestoneType: m.milestoneType, description: m.description ?? '', projectDisciplineId: m.projectDisciplineId ?? '',
    completesPhaseId: m.completesPhaseId ?? '', isClientFacing: m.isClientFacing, date: m.date ?? '' } : { milestoneType: 'Design Submission', isClientFacing: true })
  const [err, setErr] = useState<ApiError | null>(null)
  const submit = async () => {
    setErr(null)
    try {
      const body: Record<string, any> = { ...f, projectDisciplineId: f.projectDisciplineId || null, completesPhaseId: f.completesPhaseId || null }
      if (m) { if (m.date) delete body.date; await patch(`milestones/${m.id}`, body, m.rowVersion) } else await post(`projects/${p.id}/milestones`, body)
      toast.success(t('common.saved')); onDone()
    } catch (e) { setErr(e as ApiError) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>{m ? t('milestone.edit', { key: m.key }) : t('milestone.new')}</DialogTitle></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('common.name')} htmlFor="m-name" error={err?.fieldErrors.name} className="sm:col-span-2"><Input id="m-name" required value={f.name ?? ''} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Field label={t('common.type')} htmlFor="m-type">
            <select id="m-type" className={selectCls} value={f.milestoneType} onChange={(e) => setF({ ...f, milestoneType: e.target.value })}>{TYPES.map((x) => <option key={x} value={x}>{t(`mtype.${x}`)}</option>)}</select>
          </Field>
          {(!m || !m.date) && <Field label={t('common.date')} htmlFor="m-date" error={err?.fieldErrors.date}><Input id="m-date" type="date" required value={f.date ?? ''} onChange={(e) => setF({ ...f, date: e.target.value })} /></Field>}
          <Field label={t('milestone.disciplineTag')} htmlFor="m-disc">
            <select id="m-disc" className={selectCls} value={f.projectDisciplineId} onChange={(e) => setF({ ...f, projectDisciplineId: e.target.value })}>
              <option value="">{t('common.none')}</option>{p.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label={t('field.CompletesPhaseId')} htmlFor="m-phase">
            <select id="m-phase" className={selectCls} value={f.completesPhaseId} onChange={(e) => setF({ ...f, completesPhaseId: e.target.value })}>
              <option value="">{t('common.none')}</option>{phases.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <label className="flex items-center gap-2 text-sm sm:col-span-2"><Checkbox checked={!!f.isClientFacing} onCheckedChange={(c) => setF({ ...f, isClientFacing: !!c })} />{t('milestone.clientFacing')}</label>
          <Field label={t('common.description')} htmlFor="m-desc" className="sm:col-span-2"><Textarea id="m-desc" rows={2} value={f.description ?? ''} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
          {err && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2"><Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit">{t('common.save')}</Button></DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** M-02..M-04 with a preview; the cascade optionally reaches tasks (packet 018). */
export function ChangeDateDialog({ m, onClose, onDone, initialDate }: { m: MilestoneRow; onClose: () => void; onDone: () => void; initialDate?: string }) {
  const [date, setDate] = useState(initialDate ?? m.date ?? '')
  const [cascade, setCascade] = useState(false)
  const [tasks, setTasks] = useState(false)
  const preview = useQuery({ queryKey: ['ms-preview', m.id, date, cascade, tasks], enabled: !!date && date !== m.date,
    queryFn: () => post(`milestones/${m.id}/change-date`, { newDate: date, cascadeDeliverables: cascade, includeTasks: tasks, dryRun: true }) })
  const pv = preview.data
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('milestone.changeDateTitle', { key: m.key, name: m.name })} reason busy={!date || date === m.date}
      confirmLabel={t('milestone.changeDate')}
      onConfirm={async (reason) => { await post(`milestones/${m.id}/change-date`, { newDate: date, reason, cascadeDeliverables: cascade, includeTasks: tasks, rowVersion: m.rowVersion }); onDone() }}>
      <div className="space-y-3 text-sm">
        <Field label={t('milestone.newDate')} htmlFor="md-date"><Input id="md-date" type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        {pv && <p>{t('milestone.slipPreview', { from: fmtDate(pv.milestone.oldDate), to: fmtDate(pv.milestone.newDate), delta: pv.milestone.delta, slip: pv.milestone.slipDays })}</p>}
        <label className="flex items-center gap-2"><Checkbox checked={cascade} onCheckedChange={(c) => { setCascade(!!c); if (!c) setTasks(false) }} />{t('milestone.cascade')}</label>
        {cascade && <label className="ml-6 flex items-center gap-2"><Checkbox checked={tasks} onCheckedChange={(c) => setTasks(!!c)} />{t('milestone.cascadeTasks')}</label>}
        {pv && cascade && (
          <div className="max-h-44 overflow-y-auto rounded border p-2 text-xs">
            {pv.deliverables.length + pv.tasks.length === 0 ? t('milestone.nothingToShift') : (
              <ul className="space-y-0.5">
                {pv.deliverables.map((x: any) => <li key={x.id}><span className="key">{x.key}</span> {x.name}: {fmtDate(x.oldDue)} → {fmtDate(x.newDue)}</li>)}
                {pv.tasks.map((x: any) => <li key={x.id} className="pl-3"><span className="key">{x.key}</span> {x.name}: {fmtDate(x.oldDue)} → {fmtDate(x.newDue)}</li>)}
              </ul>
            )}
          </div>
        )}
        {pv && pv.inconsistent.length > 0 && <p className="text-warn">▲ {t('milestone.willBeInconsistent', { n: pv.inconsistent.length })}</p>}
      </div>
    </ConfirmDialog>
  )
}

function CompleteDialog({ p, m, onClose, onDone }: { p: ProjectDetail; m: MilestoneRow; onClose: () => void; onDone: () => void }) {
  const [date, setDate] = useState(today())
  const [confirmList, setConfirmList] = useState<any[] | null>(null)
  const [phase, setPhase] = useState<{ id: string; name: string } | null>(null)
  if (phase) return (
    <ConfirmDialog open onOpenChange={(o) => { if (!o) onDone() }} title={t('milestone.advancePhase', { phase: phase.name })} body={t('milestone.advancePhaseHint')} confirmLabel={t('common.yes')}
      onConfirm={async () => { await patch(`projects/${p.id}`, { phaseId: phase.id }, p.rowVersion); onDone() }} />
  )
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('milestone.completeTitle', { key: m.key, name: m.name })} confirmLabel={t('milestone.complete')}
      body={confirmList ? t('milestone.openDeliverables', { n: confirmList.length }) : undefined}
      onConfirm={async () => {
        try {
          const r = await post(`milestones/${m.id}/complete`, { completedDate: date, confirm: !!confirmList, rowVersion: m.rowVersion })
          toast.success(t('milestone.completed'))
          if (r.suggestPhase) setPhase(r.suggestPhase); else onDone()
        } catch (e) {
          if ((e as ApiError).code === 'confirm_open_deliverables') { setConfirmList((e as ApiError).body.deliverables); throw new ApiError(409, { detail: t('milestone.confirmAgain') }) }
          throw e
        }
      }}>
      <Field label={t('milestone.completedDate')} htmlFor="mc-date" hint={t('milestone.completedHint')}><Input id="mc-date" type="date" max={today()} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
      {confirmList && <ul className="max-h-32 overflow-y-auto rounded border p-2 text-xs">{confirmList.map((x) => <li key={x.id}><span className="key">{x.key}</span> {x.name} · {x.status}</li>)}</ul>}
    </ConfirmDialog>
  )
}

/** M-07, E-17: cancel keeps links; the PM then retargets the deliverables in one dialog. */
function CancelDialog({ p, m, onClose, onDone }: { p: ProjectDetail; m: MilestoneRow; onClose: () => void; onDone: () => void }) {
  const [retarget, setRetarget] = useState<any[] | null>(null)
  const [target, setTarget] = useState('')
  const others = useQuery({ queryKey: ['p', p.id, 'milestones', false, ''], queryFn: () => get<MilestoneRow[]>(`projects/${p.id}/milestones`) })
  if (retarget && retarget.length > 0) return (
    <ConfirmDialog open onOpenChange={(o) => { if (!o) onDone() }} title={t('milestone.retargetTitle', { n: retarget.length })} confirmLabel={t('milestone.retarget')} busy={!target}
      onConfirm={async () => { await post(`projects/${p.id}/deliverables/bulk`, { ids: retarget.map((x) => x.id), operation: 'setMilestone', params: { milestoneId: target } }); onDone() }}>
      <ul className="max-h-32 overflow-y-auto rounded border p-2 text-xs">{retarget.map((x) => <li key={x.id}><span className="key">{x.key}</span> {x.name}</li>)}</ul>
      <select className={selectCls} value={target} onChange={(e) => setTarget(e.target.value)} aria-label={t('milestone.retargetTo')}>
        <option value="">{t('milestone.retargetTo')}</option>{(others.data ?? []).filter((x) => x.id !== m.id && !x.isCancelled).map((x) => <option key={x.id} value={x.id}>{x.key} {x.name}</option>)}
      </select>
    </ConfirmDialog>
  )
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} destructive reason title={t('milestone.cancelTitle', { key: m.key, name: m.name })} body={t('milestone.cancelHint')}
      confirmLabel={t('milestone.cancel')}
      onConfirm={async (reason) => { const r = await post(`milestones/${m.id}/cancel`, { reason, rowVersion: m.rowVersion }); if (r.retarget?.length) setRetarget(r.retarget); else onDone() }} />
  )
}

// ---------- Milestone panel (§13.7) ----------

function MilestonePanel({ id }: PanelProps) {
  const q = useQuery({ queryKey: ['milestone', id], queryFn: () => get(`milestones/${id}`) })
  const [tab, setTab] = useState<'deliverables' | 'comments' | 'history'>('deliverables')
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  const ref = useReference()
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const m: MilestoneRow = q.data.milestone
  const can = q.data.permissions.manage.ok
  const save = (body: object) => patch(`milestones/${m.id}`, body, m.rowVersion).then(() => { qc.invalidateQueries({ queryKey: ['milestone', id] }); refresh(m.projectId) })
  return (
    <div>
      <div className="space-y-2 border-b p-4">
        <div className="flex items-center gap-2"><Key>{m.key}</Key><StatusPill status={milestoneStatusLabel(m)} /></div>
        <h2 className="text-lg font-semibold">{m.isSubmission && '◆ '}{m.name}</h2>
        {m.statusReasons?.length ? <Why reasons={m.statusReasons} title={t('milestone.statusWhy', { key: m.key })}><span className="text-xs text-muted-foreground">{t('common.why')}</span></Why> : null}
      </div>
      <div className="px-4 py-2">
        <FieldRow label={t('common.name')}><InlineText value={m.name} disabled={!can} onSave={(v) => save({ name: v })} /></FieldRow>
        <FieldRow label={t('common.type')}><div className="px-2 py-1.5">{t(`mtype.${m.milestoneType}`)}</div></FieldRow>
        <FieldRow label={t('common.date')}><div className="px-2 py-1.5">{fmtDate(m.date)} {m.date && <span className="text-muted-foreground">({relative(m.date)})</span>}</div></FieldRow>
        <FieldRow label={t('milestone.original')}><div className="px-2 py-1.5">{fmtDate(m.originalDate)}{m.slipDays > 0 && <span className="ml-2 text-warn">▲ {t('milestone.slipped', { n: m.slipDays })}</span>}</div></FieldRow>
        <FieldRow label={t('milestone.readiness')}><div className="px-2 py-1.5">{t('milestone.issuedOf', { n: m.deliverableIssued, total: m.deliverableTotal })} · {t('milestone.tasksComplete', { n: m.taskComplete, total: m.taskTotal })}</div></FieldRow>
        <FieldRow label={t('field.CompletesPhaseId')}><div className="px-2 py-1.5">{q.data.completesPhase?.name ?? t('common.dash')}</div></FieldRow>
        <FieldRow label={t('common.description')}><InlineText value={m.description} multiline disabled={!can} onSave={(v) => save({ description: v })} /></FieldRow>
        {m.isCancelled && <p className="rounded bg-idle-bg p-2 text-xs">{t('milestone.cancelledNote', { reason: m.cancelledReason ?? '' })}</p>}
        {!ref.data && null}
      </div>
      <TabBar tabs={[{ id: 'deliverables' as const, label: t('milestone.deliverables'), count: q.data.deliverables.length }, ...(ItemSlots.Comments ? [{ id: 'comments' as const, label: t('common.comments') }] : []), { id: 'history' as const, label: t('common.history') }]} value={tab} onChange={setTab} />
      {tab === 'deliverables' && <div className="p-4"><TargetedDeliverables milestoneId={m.id} /></div>}
      {tab === 'comments' && <CommentsSlot type="Milestone" id={m.id} projectId={m.projectId} />}
      {tab === 'history' && <HistoryList type="Milestone" id={m.id} />}
    </div>
  )
}

PANELS.Milestone = { component: MilestonePanel }

export { Section }
