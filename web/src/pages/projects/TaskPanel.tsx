import { FieldGroup } from '@/components/hub/registers'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Check, ChevronDown, Eye, EyeOff, GitBranch, Link2, Lock, Plus, RotateCcw, Trash2, X } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, ConfirmDialog, Empty, ErrorBanner, Field, Loading, Notice, Spinner, selectCls } from '@/components/hub/common'
import { FieldRow, HistoryList, InlineDate, InlinePerson, InlineSelect, InlineText, SaveStatus, TabBar } from '@/components/hub/fields'
import { PANELS, useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Chip, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { RichText } from '@/components/hub/richtext'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProject, useProjectRefresh } from '@/hooks/data'
import { ApiError, del, get, post, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { fmtDate, fmtTime } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { CommentsSlot, ItemSlots, LinksSlot, RaiseSlot } from './slots-items'
import { PRIORITIES, TaskIndicators, errorText, moveFrom, readableDisabled, useProjectLists, useTaskActions, type Blocker, type TaskRow, type Transition } from './Tasks'

/** Sections added to the task panel by later packets (actual hours and Add Time, packet 024). */
export const TASK_SECTIONS: { id: string; render: (d: TaskDetailData, reload: () => void) => ReactNode }[] = []

export interface TaskDetailData {
  task: TaskRow; description?: string; onHoldReason?: string; cancelledReason?: string; previousStatus?: string; reviewRequestedAt?: string
  project: { id: string; projectNumber: string; name: string; status: string }
  collaborators: { id: string; displayName: string; isActive: boolean }[]; watchers: { id: string; displayName: string; source: string }[]
  permissions: {
    edit: P; assign: P; setReviewer: P; dueDate: P; dueNeedsReason: boolean; block: P; delete: P; restore: boolean; comment: boolean
    transitions: Transition[]; completeHint?: string; isReviewer: boolean; dependencies: boolean; enterTime: boolean; needsReason: boolean; allowSelfReview: boolean
    authoriseStart?: boolean
  }
}
interface P { ok: boolean; reason?: string | null }

const BLOCK_TYPES = ['Client', 'External Party', 'Internal', 'Decision', 'Information', 'Other']

/** Task detail (§13.3.1): header, blockers, fields grouped by purpose (people, details, dates, progress and hours), description,
 *  dependencies, manual block, links, comments and history. Every field saves on its own and says so (design §8). */
export function TaskDetail({ id, onClose }: { id: string; onClose?: () => void }) {
  const q = useQuery({ queryKey: ['task', id], queryFn: () => get<TaskDetailData>(`tasks/${id}`) })
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  const openPanel = useItemPanel()
  const me = useMe()
  const project = useProject(q.data?.project.projectNumber)
  const lists = useProjectLists(q.data?.project.id)
  const [tab, setTab] = useState<string>(ItemSlots.Comments ? 'comments' : 'history')
  const [blocking, setBlocking] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const reload = () => { qc.invalidateQueries({ queryKey: ['task', id] }); refresh(q.data?.project.id) }
  const actions = useTaskActions(reload)
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const d = q.data
  const r = d.task
  const s = r.state
  const perms = d.permissions
  const terminal = r.status === 'Complete' || r.status === 'Cancelled'
  const save = (body: Record<string, unknown>) => actions.save(r, body)
  const find = (to: string) => perms.transitions.find((x) => x.to === to && x.allowed)
  const act = (to: string) => { const x = find(to); if (x) actions.move(moveFrom(d, x)) }
  const milestoneName = (mid: string) => lists.milestones.find((m) => m.id === mid)
  const run = async (f: () => Promise<unknown>) => { setErr(null); try { await f(); reload() } catch (e) { setErr(e) } }

  return (
    <div>
      <div className="space-y-3 p-4">
        <div className="flex flex-wrap items-center gap-2">
          <Key>{r.key}</Key>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button type="button" className="inline-flex min-h-6 items-center gap-0.5 rounded-md hover:bg-muted" aria-label={t('task.changeStatus', { key: r.key, status: tv(r.status) })}>
                <StatusPill status={r.status} /><ChevronDown className="size-3.5 text-muted-foreground" aria-hidden />
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="max-w-80">
              {perms.transitions.map((x) => (
                <DropdownMenuItem key={x.to} disabled={!x.allowed} onSelect={() => actions.move(moveFrom(d, x))} className={cn('flex-col items-start gap-0', readableDisabled)}>
                  <span>{tv(x.to)}{x.via && <span className="ml-1 text-xs text-muted-foreground">{t('task.viaShort', { via: tv(x.via) })}</span>}</span>
                  {!x.allowed && x.reason && <span className="text-xs text-muted-foreground">{x.reason}</span>}
                </DropdownMenuItem>
              ))}
              {perms.completeHint && <DropdownMenuItem disabled className={cn('text-xs', readableDisabled)}>{perms.completeHint}</DropdownMenuItem>}
            </DropdownMenuContent>
          </DropdownMenu>
          <PriorityBadge priority={r.priority} />
          <span className="inline-flex items-center gap-1.5 text-xs/[18px] text-muted-foreground"><AccentDot id={d.project.id} />{d.project.projectNumber}</span>
        </div>
        <h2 aria-label={r.name} className="-mx-2 break-words text-2xl/8 font-semibold tracking-[-0.4px]"><InlineText value={r.name} disabled={!perms.edit.ok} title={perms.edit.ok ? t('common.name') : perms.edit.reason ?? undefined} onSave={(v) => save({ name: v })} /></h2>
        <TaskIndicators r={r} />
        <div className="flex flex-wrap items-center gap-2">
          {perms.isReviewer && r.status === 'Ready for Review' && find('In Review') && <Button size="sm" onClick={() => act('In Review')}>{t('task.startReview')}</Button>}
          {perms.isReviewer && r.status === 'In Review' && find('Complete') && <Button size="sm" onClick={() => act('Complete')}><Check className="size-4" aria-hidden />{t('task.approve')}</Button>}
          {perms.isReviewer && r.status === 'In Review' && find('Revision Required') && <Button size="sm" variant="outline" onClick={() => act('Revision Required')}>{t('task.requestRevision')}</Button>}
          {perms.edit.ok && <Button size="sm" variant="outline" asChild><Link to={`/projects/${d.project.projectNumber}/handoffs?targetTask=${r.id}`}>{t('handoff.new')}</Link></Button>}
          <Button size="sm" variant="outline" asChild><Link to={`/projects/${d.project.projectNumber}/changes?target=Task:${r.id}`}>{t('change.inputs')}</Link></Button>
          {perms.authoriseStart && <Button size="sm" variant="outline" onClick={() => actions.authoriseStart(r)}>{t('tasks.startAuthAuthorise')}</Button>}
          <RaiseSlot projectId={r.projectId} targetType="Task" targetId={r.id} targetKey={r.key} targetName={r.name} disciplineId={r.projectDisciplineId} />
        </div>
      </div>
      {!perms.edit.ok && perms.edit.reason && <div className="px-4 pb-4"><Notice icon={Lock} title={t('task.readOnlyTitle')}>{perms.edit.reason}</Notice></div>}
      {err != null && <div className="px-4 pb-4"><ErrorBanner error={err} retry={() => { setErr(null); reload() }} /></div>}

      {s && (s.isWaiting || s.isBlocked) && (
        <div role="status" className={cn('mx-4 mb-4 space-y-2 rounded-lg border px-4 py-3 text-sm', s.isBlocked ? 'border-bad/30 bg-bad-bg' : 'border-warn/40 bg-warn-bg')}>
          <p className={cn('font-semibold', s.isBlocked ? 'text-bad' : 'text-warn')}>
            <span aria-hidden>{s.isBlocked ? '■ ' : '▲ '}</span>{s.isBlocked ? (s.daysBlocked > 0 ? t('task.blockedFor', { n: s.daysBlocked }) : t('task.blockedToday')) : t('task.waitingOn')}
          </p>
          <ul className="space-y-1.5">{(s.blockedBy ?? []).map((b, i) => <BlockerLine key={i} b={b} open={openPanel} />)}</ul>
          {r.status === 'Not Started' && <p className="text-xs/[18px] text-muted-foreground">{t('task.startAnyway')}</p>}
          {s.affectedMilestoneIds.length > 0 && (
            <p className="text-xs/[18px]">{t('task.affects')} {s.affectedMilestoneIds.map((mid) => milestoneName(mid)).filter(Boolean).map((m) => `${m!.key} ${m!.name} (${fmtDate(m!.date)})`).join(', ')}</p>
          )}
        </div>
      )}

      <FieldGroup title={t('task.people')}>
        <FieldRow label={t('field.AssigneeId')}><InlinePerson value={r.assigneeId} name={r.assigneeName && !r.assigneeActive ? t('common.inactiveSuffix', { name: r.assigneeName }) : r.assigneeName} disabled={!perms.assign.ok} title={perms.assign.reason ?? undefined} onSave={(v) => save({ assigneeId: v })} /></FieldRow>
        <FieldRow label={t('field.ReviewerId')}><InlinePerson value={r.reviewerId} name={r.reviewerName} disabled={!perms.setReviewer.ok} title={perms.setReviewer.reason ?? undefined} onSave={(v) => save({ reviewerId: v })} /></FieldRow>
        <FieldRow label={t('field.RequiresReview')}>
          <SaveStatus current={r.requiresReview}>{(track) => (
            <div className="flex min-h-(--control-row-h) items-center px-2"><Checkbox checked={r.requiresReview} disabled={!perms.edit.ok} onCheckedChange={(c) => track(save({ requiresReview: !!c }), !!c)} aria-label={t('field.RequiresReview')} /></div>
          )}</SaveStatus>
        </FieldRow>
        <People d={d} me={me.id} canManage={perms.assign.ok} onChanged={reload} onError={setErr} />
      </FieldGroup>

      <FieldGroup title={t('common.details')}>
        <FieldRow label={t('common.priority')}><InlineSelect value={r.priority} disabled={!perms.edit.ok} options={PRIORITIES.map((x) => ({ value: x, label: tv(x) }))} onSave={(v) => save({ priority: v })} title={t('common.priority')} /></FieldRow>
        <FieldRow label={t('common.discipline')}><InlineSelect value={r.projectDisciplineId} disabled={!perms.edit.ok} title={t('common.discipline')}
          options={(project.data?.disciplines ?? []).filter((x) => x.isActive || x.id === r.projectDisciplineId).map((x) => ({ value: x.id, label: x.name }))} onSave={(v) => save({ projectDisciplineId: v })} /></FieldRow>
        <FieldRow label={t('task.deliverable')}><InlineSelect value={r.deliverableId} allowEmpty disabled={!perms.edit.ok} title={t('task.deliverable')}
          options={lists.deliverables.filter((x) => x.status !== 'Cancelled' || x.id === r.deliverableId).map((x) => ({ value: x.id, label: `${x.key} ${x.name}` }))} onSave={(v) => save({ deliverableId: v })} /></FieldRow>
        <FieldRow label={t('field.MilestoneId')}>
          {r.deliverableId
            ? <div className="flex min-h-(--control-row-h) flex-wrap items-center gap-x-2 px-2 py-1">{r.milestoneKey ? `${r.milestoneKey} ${r.milestoneName}` : t('common.dash')} <span className="text-xs/[18px] text-muted-foreground">{t('task.fromDeliverable')}</span></div>
            : <InlineSelect value={r.milestoneId} allowEmpty disabled={!perms.edit.ok} title={t('field.MilestoneId')} options={lists.milestones.map((m) => ({ value: m.id, label: `${m.key} ${m.name}` }))} onSave={(v) => save({ milestoneId: v })} />}
        </FieldRow>
        {r.status === 'On Hold' && d.onHoldReason && <FieldRow label={t('field.OnHoldReason')}><div className="px-2 py-1.5">{d.onHoldReason}</div></FieldRow>}
        {r.status === 'Cancelled' && d.cancelledReason && <FieldRow label={t('field.CancelledReason')}><div className="px-2 py-1.5">{d.cancelledReason}</div></FieldRow>}
      </FieldGroup>

      <FieldGroup title={t('task.group.dates')}>
        <FieldRow label={t('field.StartDate')}><InlineDate value={r.startDate} disabled={!perms.edit.ok} onSave={(v) => save({ startDate: v })} /></FieldRow>
        <FieldRow label={t('field.DueDate')}>
          <InlineDate value={r.dueDate} disabled={!perms.dueDate.ok} title={perms.dueDate.reason ?? (perms.dueNeedsReason ? t('task.dueReasonHint') : undefined)} onSave={(v) => save({ dueDate: v })} />
          {((r.originalDueDate && r.originalDueDate !== r.dueDate) || r.dueDateChangeCount >= 3) && (
            <div className="flex flex-wrap items-center gap-2 px-2 pt-1">
              {r.originalDueDate && r.originalDueDate !== r.dueDate && <span className="text-xs/[18px] text-muted-foreground tabular-nums">{t('deliverable.originally', { date: fmtDate(r.originalDueDate) })}</span>}
              {r.dueDateChangeCount >= 3 && <Chip tone="warn">{t('ind.dueMoved', { n: r.dueDateChangeCount })}</Chip>}
            </div>
          )}
        </FieldRow>
      </FieldGroup>

      <FieldGroup title={t('task.group.work')}>
        <FieldRow label={t('deliverable.progress')}><ProgressSlider value={r.progressPct} disabled={!perms.edit.ok || terminal} onCommit={(v) => actions.setProgress(r, v)} /></FieldRow>
        <FieldRow label={t('task.estimateHours')}><InlineText value={r.estimatedHours == null ? '' : String(r.estimatedHours)} disabled={!perms.edit.ok}
          onSave={(v) => save({ estimatedHours: v == null || v === '' ? null : Number(v) })} /></FieldRow>
        {TASK_SECTIONS.map((x) => <div key={x.id}>{x.render(d, reload)}</div>)}
      </FieldGroup>

      <FieldGroup title={t('common.description')}>
        {/* The section title is the visible label; the field keeps its own name for the editor. */}
        <FieldRow label={<span className="sr-only">{t('common.description')}</span>} className="gap-y-0 py-0 sm:grid-cols-1">
          <div className="-mx-2"><InlineText value={d.description} multiline disabled={!perms.edit.ok} placeholder={t('task.noDescription')}
            render={(v) => <RichText text={v} className="whitespace-normal" />} onSave={(v) => save({ description: v })} /></div>
        </FieldRow>
      </FieldGroup>

      <Dependencies task={r} canManage={perms.dependencies} onChanged={reload} />

      <FieldGroup title={t('task.manualBlock')}>
        {r.manualBlockType ? (
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Chip tone="bad">{tv(r.manualBlockType)}</Chip>
            <span className="min-w-0 flex-1 break-words">{r.manualBlockReason}</span>
            <span className="text-xs/[18px] text-muted-foreground tabular-nums">{t('task.blockSince', { when: fmtTime(r.manualBlockSetAt) })}</span>
            {perms.block.ok && <Button size="sm" variant="outline" onClick={() => run(() => post(`tasks/${r.id}/unblock`, {}, r.rowVersion))}>{t('task.clearBlock')}</Button>}
          </div>
        ) : perms.block.ok ? <Button size="sm" variant="outline" onClick={() => setBlocking(true)}><Ban className="size-4" aria-hidden />{t('task.setBlock')}</Button>
          : <p className="text-sm text-muted-foreground">{t('common.none')}</p>}
      </FieldGroup>

      {ItemSlots.Links && <FieldGroup title={t('common.links')}><LinksSlot type="Task" id={r.id} projectId={r.projectId} inheritedFrom={r.deliverableId} /></FieldGroup>}

      <div className="border-t">
        <TabBar tabs={[...(ItemSlots.Comments ? [{ id: 'comments', label: t('common.comments'), count: r.commentCount }] : []), { id: 'history', label: t('common.history') }]} value={tab} onChange={setTab} />
      </div>
      {tab === 'comments' && <CommentsSlot type="Task" id={r.id} projectId={r.projectId} />}
      {tab === 'history' && <HistoryList type="Task" id={r.id} />}

      {perms.delete.ok && (
        <div className="flex justify-end border-t p-4">
          <Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => setDeleting(true)}><Trash2 className="size-4" aria-hidden />{t('task.delete')}</Button>
        </div>
      )}
      {blocking && <BlockDialog r={r} onClose={(ok) => { setBlocking(false); if (ok) reload() }} />}
      {deleting && <DeleteDialog d={d} onMove={(m) => { setDeleting(false); actions.move(m) }} onClose={(gone) => { setDeleting(false); if (gone) { refresh(r.projectId); onClose?.() } }} />}
      {actions.dialogs}
    </div>
  )
}

function BlockerLine({ b, open }: { b: Blocker; open: (type: string, id: string) => void }) {
  if (b.type === 'manual') return <li>{t('task.manualBlocker', { type: tv(b.name ?? ''), reason: b.reason ?? '' })}</li>
  const type = b.type === 'task' ? 'Task' : b.type === 'decision' ? 'Decision' : 'Deliverable'
  return (
    <li className="flex flex-wrap items-center gap-2">
      <button type="button" className="text-left hover:underline" onClick={() => b.id && open(type, b.id)}><Key>{b.key}</Key> {b.name}</button>
      <StatusPill status={b.status} />
      <span className="text-xs/[18px] text-muted-foreground tabular-nums">{t('common.due')} {fmtDate(b.due)}</span>
      {b.overdue && <Chip tone="bad">{t('ind.overdue')}</Chip>}
    </li>
  )
}

/** Progress in 10 % steps; saved when the slider is released (T-20), with the pending state shown beneath it. */
function ProgressSlider({ value, disabled, onCommit }: { value: number; disabled?: boolean; onCommit: (v: number) => Promise<unknown> | void }) {
  const [v, setV] = useState(value)
  const changed = useRef(false)
  useEffect(() => { setV(value); changed.current = false }, [value])
  return (
    <SaveStatus current={value}>{(track) => {
      const commit = () => {
        if (v === value || !changed.current) return
        changed.current = false
        track(onCommit(v), v)
      }
      return (
        <div className="flex min-h-(--control-row-h) items-center gap-3 px-2">
          <input type="range" min={0} max={100} step={10} value={v} disabled={disabled} aria-label={t('deliverable.progress')} aria-valuetext={`${v}%`}
            className="h-6 w-full max-w-56 accent-(--primary)" onChange={(e) => { changed.current = true; setV(Number(e.target.value)) }} onPointerUp={commit} onKeyUp={commit} onBlur={commit} />
          <span className="w-12 text-right text-sm font-medium tabular-nums">{v}%</span>
        </div>
      )
    }}</SaveStatus>
  )
}

// ---------- Dependencies and chain view (packet 005: FR-DEP-01..08, D-03, D-10) ----------

interface Edge { dependencyId: string; note?: string; lagDays: number; satisfied: boolean
  task: { id: string; key: string; name: string; status: string; dueDate?: string; startDate?: string; assigneeName?: string; overdue: boolean; blocked: boolean; waiting: boolean } }

function Dependencies({ task, canManage, onChanged }: { task: TaskRow; canManage: boolean; onChanged: () => void }) {
  const q = useQuery({ queryKey: ['task', task.id, 'deps', task.rowVersion], queryFn: () => get<{ dependsOn: Edge[]; blocks: Edge[] }>(`tasks/${task.id}/dependencies`) })
  const openPanel = useItemPanel()
  const [adding, setAdding] = useState<'predecessor' | 'successor' | null>(null)
  const [chain, setChain] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const remove = async (e: Edge) => {
    setErr(null)
    try { await del(`dependencies/${e.dependencyId}`); q.refetch(); onChanged() } catch (x) { setErr(x) }
  }
  const list = (edges: Edge[], empty: string) => edges.length === 0 ? <p className="text-sm text-muted-foreground">{empty}</p> : (
    <ul className="divide-y rounded-md border">
      {edges.map((e) => (
        <li key={e.dependencyId} className="flex flex-wrap items-center gap-x-2 gap-y-1 px-3 py-1.5 text-sm">
          {e.satisfied ? <Check className="size-4 shrink-0 text-done" role="img" aria-label={t('task.satisfied')} /> : <Link2 className="size-4 shrink-0 text-muted-foreground" aria-hidden />}
          <button type="button" className="min-w-0 flex-1 break-words text-left hover:underline" onClick={() => openPanel('Task', e.task.id)}><Key>{e.task.key}</Key> {e.task.name}</button>
          {e.task.overdue && <Chip tone="bad">{t('ind.overdue')}</Chip>}
          {e.lagDays > 0 && <Chip tone="idle">{t('dep.lagN', { n: e.lagDays })}</Chip>}
          <span className="hidden text-xs/[18px] text-muted-foreground sm:inline">{e.task.assigneeName ?? t('ind.unassigned')} · <span className="tabular-nums">{fmtDate(e.task.dueDate)}</span></span>
          <StatusPill status={e.task.status} />
          {canManage && <Button variant="ghost" size="icon-sm" aria-label={t('task.removeDependency', { key: e.task.key })} onClick={() => remove(e)}><X className="size-4" /></Button>}
        </li>
      ))}
    </ul>
  )
  return (
    <FieldGroup title={t('deliverable.dependencies')} actions={<Button size="sm" variant="ghost" onClick={() => setChain(true)}><GitBranch className="size-4" aria-hidden />{t('task.showChain')}</Button>}>
      {err != null && <div className="mb-2"><ErrorBanner error={err} /></div>}
      {q.isPending ? <Loading rows={2} className="p-0" /> : q.data && (
        <div className="grid gap-4">
          <div className="space-y-1.5">
            <div className="flex min-h-8 items-center justify-between gap-2 text-sm font-medium">{t('deliverable.dependsOn')}
              {canManage && <Button size="sm" variant="ghost" onClick={() => setAdding('predecessor')}><Plus className="size-4" aria-hidden />{t('task.addPredecessor')}</Button>}</div>
            {list(q.data.dependsOn, t('task.noPredecessors'))}
          </div>
          <div className="space-y-1.5">
            <div className="flex min-h-8 items-center justify-between gap-2 text-sm font-medium">{t('deliverable.blocks')}
              {canManage && <Button size="sm" variant="ghost" onClick={() => setAdding('successor')}><Plus className="size-4" aria-hidden />{t('task.addSuccessor')}</Button>}</div>
            {list(q.data.blocks, t('task.noSuccessors'))}
          </div>
        </div>
      )}
      {adding && <AddDependency task={task} direction={adding} onClose={(ok) => { setAdding(null); if (ok) { q.refetch(); onChanged() } }} />}
      {chain && <ChainDialog task={task} onClose={() => setChain(false)} />}
    </FieldGroup>
  )
}

/** Choose a predecessor or successor; loop-creating tasks are listed but disabled (FR-DEP-02). */
function AddDependency({ task, direction, onClose }: { task: TaskRow; direction: 'predecessor' | 'successor'; onClose: (ok: boolean) => void }) {
  const [term, setTerm] = useState('')
  const [note, setNote] = useState('')
  const [lag, setLag] = useState('0')
  const [choice, setChoice] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const q = useQuery({ queryKey: ['task', task.id, 'candidates', direction, term], queryFn: () => get<{ id: string; key: string; name: string; status: string; dueDate?: string; disabled: boolean; reason?: string }[]>(`tasks/${task.id}/dependency-candidates${qs({ q: term, direction })}`) })
  const save = async () => {
    setErr(null); setBusy(true)
    try {
      const extra = { note: note || null, lagDays: Number(lag) || 0 }
      await post(`tasks/${task.id}/dependencies`, direction === 'predecessor' ? { predecessorTaskId: choice, ...extra } : { successorTaskId: choice, ...extra })
      onClose(true)
    } catch (e) {
      const x = e as ApiError
      setErr(x.code === 'dependency_cycle' ? t('task.cycle', { path: (x.body.cyclePath ?? []).join(' → ') }) : errorText(e))
    } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose(false)}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{direction === 'predecessor' ? t('task.addPredecessorTitle', { key: task.key }) : t('task.addSuccessorTitle', { key: task.key })}</DialogTitle>
          <DialogDescription>{t('task.fsHint')}</DialogDescription>
        </DialogHeader>
        <Field label={t('common.search')} htmlFor="dep-search"><Input id="dep-search" type="search" autoFocus placeholder={t('task.searchPlaceholder')} value={term} onChange={(e) => setTerm(e.target.value)} /></Field>
        <div className="scroll-region max-h-64 overflow-y-auto rounded-md border" role="listbox" aria-label={t('task.candidates')}>
          {q.isPending ? <Loading rows={3} /> : (q.data ?? []).length === 0 ? <Empty>{t('task.noCandidates')}</Empty> : q.data!.map((c) => (
            <button key={c.id} type="button" role="option" aria-selected={choice === c.id} disabled={c.disabled} onClick={() => setChoice(c.id)}
              className={cn('flex min-h-(--control-row-h) w-full items-center gap-2 px-3 py-1.5 text-left text-sm', choice === c.id ? 'bg-accent shadow-[inset_3px_0_0_var(--primary)]' : 'hover:bg-muted', c.disabled && 'cursor-not-allowed text-muted-foreground')}>
              <Key>{c.key}</Key><span className="min-w-0 flex-1 truncate">{c.name}</span>
              {c.disabled ? <span className="text-xs/[18px]">{c.reason}</span> : choice === c.id ? <Check className="size-4 text-primary" aria-hidden /> : <StatusPill status={c.status} />}
            </button>
          ))}
        </div>
        <Field label={t('dep.lag')} htmlFor="dep-lag" hint={t('dep.lagHint')}><Input id="dep-lag" type="number" min={0} max={365} className="w-28" value={lag} onChange={(e) => setLag(e.target.value)} /></Field>
        <Field label={t('common.notes')} htmlFor="dep-note" optional><Textarea id="dep-note" rows={2} value={note} onChange={(e) => setNote(e.target.value)} /></Field>
        {err && <div role="alert" className="rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad">{err}</div>}
        <DialogFooter>
          <Button variant="outline" onClick={() => onClose(false)}>{t('common.cancel')}</Button>
          <Button disabled={!choice || busy} onClick={save}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.add')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** Chain view: transitive predecessors above and successors below with their states (FR-DEP-07). */
function ChainDialog({ task, onClose }: { task: TaskRow; onClose: () => void }) {
  const q = useQuery({ queryKey: ['task', task.id, 'chain'], queryFn: () => get<{ task: TaskRow; depth: number; predecessors: { level: number; task: TaskRow }[]; successors: { level: number; task: TaskRow }[] }>(`tasks/${task.id}/chain`) })
  const openPanel = useItemPanel()
  const row = (r: TaskRow, level: number, self = false) => (
    <li key={`${r.id}-${level}`} className={cn('flex flex-wrap items-center gap-2 rounded-md px-3 py-1.5 text-sm', self && 'bg-accent font-medium shadow-[inset_3px_0_0_var(--primary)]')} style={{ marginLeft: Math.min(level, 8) * 14 }}>
      <button type="button" className="text-left hover:underline" aria-current={self || undefined} onClick={() => { onClose(); openPanel('Task', r.id) }}><Key>{r.key}</Key> {r.name}</button>
      <StatusPill status={r.status} />
      <span className="text-xs/[18px] text-muted-foreground tabular-nums">{fmtDate(r.dueDate)}</span>
      <TaskIndicators r={r} />
    </li>
  )
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader><DialogTitle>{t('task.chainTitle', { key: task.key })}</DialogTitle>
          <DialogDescription>{q.data ? t('task.chainHint', { n: q.data.depth }) : ' '}</DialogDescription></DialogHeader>
        {q.isPending ? <Loading rows={4} /> : q.data && (
          <ol className="scroll-region max-h-[60vh] space-y-0.5 overflow-y-auto" aria-label={t('task.chainTitle', { key: task.key })}>
            {[...q.data.predecessors].sort((a, b) => b.level - a.level).map((x) => row(x.task, 0))}
            {q.data.predecessors.length > 0 && <li className="px-3 text-xs/[18px] text-muted-foreground" aria-hidden>↓</li>}
            {row(q.data.task, 0, true)}
            {q.data.successors.length > 0 && <li className="px-3 text-xs/[18px] text-muted-foreground" aria-hidden>↓</li>}
            {q.data.successors.map((x) => row(x.task, x.level))}
          </ol>
        )}
      </DialogContent>
    </Dialog>
  )
}

// ---------- People on the task (FR-TSK-08, T-15, FR-013) ----------

function People({ d, me, canManage, onChanged, onError }: { d: TaskDetailData; me: string; canManage: boolean; onChanged: () => void; onError: (e: unknown) => void }) {
  const r = d.task
  const canCollab = canManage || r.assigneeId === me
  const watching = d.watchers.some((w) => w.id === me)
  const run = async (f: () => Promise<unknown>) => { try { await f(); onChanged() } catch (e) { onError(e) } }
  const chip = (id: string, label: string, name: string, remove?: () => void) => (
    <span key={id} className="inline-flex min-h-7 items-center gap-1.5 rounded-md border bg-card py-0.5 pl-0.5 pr-2 text-sm">
      <Avatar id={id} name={name} className="size-6" />{label}
      {remove && <button type="button" className="-mr-1 grid size-6 place-items-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground" aria-label={t('task.removeCollaborator', { name })} onClick={remove}><X className="size-3.5" /></button>}
    </span>
  )
  return (
    <>
      <FieldRow label={t('task.collaborators')}>
        <div className="flex min-h-(--control-row-h) flex-wrap items-center gap-1.5 px-2 py-1">
          {d.collaborators.length === 0 && <span className="text-muted-foreground">{t('common.none')}</span>}
          {d.collaborators.map((c) => chip(c.id, c.isActive ? c.displayName : t('common.inactiveSuffix', { name: c.displayName }), c.displayName,
            canCollab || c.id === me ? () => run(() => del(`tasks/${r.id}/collaborators/${c.id}`)) : undefined))}
          {canCollab && <div className="w-48"><PeoplePicker compact value={null} placeholder={t('task.addCollaborator')} label={t('task.addCollaborator')}
            exclude={[...d.collaborators.map((c) => c.id), ...(r.assigneeId ? [r.assigneeId] : [])]} onChange={(id) => id && run(() => post(`tasks/${r.id}/collaborators`, { userId: id }))} /></div>}
        </div>
        <p className="px-2 text-xs/[18px] text-muted-foreground">{t('task.collaboratorHint')}</p>
      </FieldRow>
      <FieldRow label={t('task.watchers')}>
        <div className="flex min-h-(--control-row-h) flex-wrap items-center gap-1.5 px-2 py-1">
          {d.watchers.length === 0 ? <span className="text-muted-foreground">{t('common.none')}</span> : d.watchers.map((w) => chip(w.id, w.displayName, w.displayName))}
          <Button size="sm" variant="ghost" onClick={() => run(() => watching ? del(`items/Task/${r.id}/watchers/${me}`) : post(`items/Task/${r.id}/watchers`, { userId: me }))}>
            {watching ? <><EyeOff className="size-4" aria-hidden />{t('task.unwatch')}</> : <><Eye className="size-4" aria-hidden />{t('task.watch')}</>}
          </Button>
        </div>
      </FieldRow>
    </>
  )
}

function BlockDialog({ r, onClose }: { r: TaskRow; onClose: (ok: boolean) => void }) {
  const [type, setType] = useState('Client')
  const [reason, setReason] = useState('')
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('task.setBlockTitle', { key: r.key })} body={t('task.setBlockBody')} busy={!reason.trim()} confirmLabel={t('task.setBlock')}
      onConfirm={async () => { await post(`tasks/${r.id}/block`, { type, reason, rowVersion: r.rowVersion }); onClose(true) }}>
      <Field label={t('common.type')} htmlFor="blk-type"><select id="blk-type" className={selectCls} value={type} onChange={(e) => setType(e.target.value)}>{BLOCK_TYPES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select></Field>
      <Field label={t('common.reason')} htmlFor="blk-reason"><Textarea id="blk-reason" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
    </ConfirmDialog>
  )
}

/** Delete with the dependency list and a Cancel-first suggestion once work exists (FR-016, D-09, T-09). */
function DeleteDialog({ d, onMove, onClose }: { d: TaskDetailData; onMove: (m: ReturnType<typeof moveFrom>) => void; onClose: (gone: boolean) => void }) {
  const r = d.task
  const q = useQuery({ queryKey: ['task', r.id, 'delete-preview'], queryFn: () => get<{ dependencies: { id: string; key: string; name: string; direction: string }[]; suggestCancel: boolean; canDelete: boolean; canCancel: boolean }>(`tasks/${r.id}/delete-preview`) })
  const cancel = d.permissions.transitions.find((x) => x.to === 'Cancelled' && x.allowed)
  return (
    <ConfirmDialog open destructive onOpenChange={(o) => !o && onClose(false)} title={t('task.deleteTitle', { key: r.key, name: r.name })} confirmLabel={t('common.delete')} busy={!q.data?.canDelete}
      body={q.isPending ? <Spinner /> : q.data && <>
        <p>{t('task.deleteBody')}</p>
        {q.data.dependencies.length > 0 && <>
          <p>{t('task.deleteDeps', { n: q.data.dependencies.length })}</p>
          <ul className="list-disc pl-5">{q.data.dependencies.map((x) => <li key={x.id}>{t(x.direction === 'blocks' ? 'task.depBlocks' : 'task.depDependsOn', { key: x.key, name: x.name })}</li>)}</ul>
        </>}
        {q.data.suggestCancel && <p className="font-medium">{t('task.suggestCancel')}</p>}
      </>}
      onConfirm={async () => {
        await del(`tasks/${r.id}`)
        toast.success(t('task.deleted', { key: r.key }), d.permissions.restore
          ? { action: { label: t('task.undo'), onClick: () => { post(`tasks/${r.id}/restore`).then(() => toast.success(t('task.restored', { key: r.key }))).catch((e) => toast.error(errorText(e))) } }, duration: 10000 }
          : undefined)
        onClose(true)
      }}>
      {q.data?.suggestCancel && q.data.canCancel && cancel && (
        <Button variant="outline" onClick={() => onMove(moveFrom(d, cancel))}><RotateCcw className="size-4" aria-hidden />{t('task.cancelInstead')}</Button>
      )}
    </ConfirmDialog>
  )
}

function TaskPanel({ id, onClose }: PanelProps) { return <TaskDetail id={id} onClose={onClose} /> }

/** Full-page task (§13.3.1 "open full page"). */
export function TaskPage() {
  const { id } = useParams()
  return <div className="mx-auto w-full max-w-3xl p-4 md:p-6 xl:p-8"><div className="overflow-hidden rounded-xl border bg-card"><TaskDetail id={id!} /></div></div>
}

PANELS.Task = { component: TaskPanel, fullPage: (id) => `/tasks/${id}` }
