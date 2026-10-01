import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useVirtualizer } from '@tanstack/react-virtual'
import { ArrowDown, ArrowUp, Columns3, KanbanSquare, List, Plus } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, Spinner, selectCls } from '@/components/hub/common'
import { InlineDate } from '@/components/hub/fields'
import { useItemPanel } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { Chip, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { Why, type Reason } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProjectRefresh } from '@/hooks/data'
import { ApiError, get, patch, post, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { addDays, ago, fmtDate, today } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import type { Page as PageOf, ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import type { DeliverableRow } from './Deliverables'
import type { MilestoneRow } from './Milestones'
import { useCurrentProject } from './ProjectLayout'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { StartAuthorisationDialog, type StartRequest } from './TaskStart'

// ---------- Shapes (GET /projects/{id}/tasks, /tasks/{id}) ----------

export interface Blocker { type: string; id?: string; key?: string; name?: string; status?: string; due?: string; overdue: boolean; ownerId?: string; reason?: string; blocking: boolean }
export interface TaskStateView {
  isOverdue: boolean; daysOverdue: number; isDueSoon: boolean; isWaiting: boolean; isBlocked: boolean; blockedSince?: string; daysBlocked: number
  blockedBy?: Blocker[] | null; isBlocking: boolean; blockingCount: number; blockingTaskIds: string[]; isStale: boolean; staleDays: number
  isUnassigned: boolean; isMissingDueDate: boolean; isDateInconsistent: boolean; inconsistencyDetail?: { text: string }[] | null
  isInactiveOwner: boolean; isHeldPastDue: boolean; isReviewStalled: boolean; affectedMilestoneIds: string[]; notes: string[]; evaluatedAt: string
}
export interface TaskRow {
  id: string; projectId: string; projectNumber: string; projectName: string; projectStatus: string; key: string; name: string
  projectDisciplineId: string; disciplineName: string; disciplineCode: string; disciplineColour?: string; disciplineOrder: number
  deliverableId?: string; deliverableKey?: string; deliverableName?: string; deliverableDueDate?: string; deliverableStatus?: string; deliverableOrder?: number
  milestoneId?: string; effectiveMilestoneId?: string; milestoneKey?: string; milestoneName?: string; milestoneDate?: string; milestoneDerived: boolean
  assigneeId?: string; assigneeName?: string; assigneeActive: boolean; reviewerId?: string; reviewerName?: string; requiresReview: boolean
  priority: string; startDate?: string; dueDate?: string; originalStartDate?: string; originalDueDate?: string; status: string; lane: string
  progressPct: number; estimatedHours?: number | null; reviewRound: number; dueDateChangeCount: number; lastActivityAt: string; statusChangedAt?: string; completedAt?: string
  createdAt: string; createdBy?: string; createdByName?: string; sortOrder: number; rowVersion: number
  manualBlockType?: string; manualBlockReason?: string; manualBlockSetAt?: string; commentCount: number; predecessorCount: number; successorCount: number
  collaboratorIds: string[]; state?: TaskStateView | null
}
export interface Transition { to: string; allowed: boolean; reason?: string | null; needsReason: boolean; via?: string | null }
export interface MoveRequest { id: string; key: string; rowVersion: number; to: string; needsReason: boolean; needsReviewer?: boolean; via?: string | null }

export const TASK_STATUSES = ['Not Started', 'In Progress', 'Ready for Review', 'In Review', 'Revision Required', 'Complete', 'On Hold', 'Cancelled']
export const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']
const PROGRESS = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100]

/** Blockers as "Why?" reasons: the predecessor, decision or manual block and its state (§13.3.1 blockers box). */
export function blockerReasons(s?: TaskStateView | null): Reason[] {
  return (s?.blockedBy ?? []).map((b) => ({
    text: b.type === 'manual' ? t('task.manualBlocker', { type: tv(b.name ?? ''), reason: b.reason ?? '' })
      : t('task.itemBlocker', { key: b.key ?? '', name: b.name ?? '', status: tv(b.status ?? ''), due: fmtDate(b.due) }) + (b.overdue ? ` · ${t('ind.overdue')}` : ''),
    colour: b.blocking ? 'Red' : 'Yellow', rule: b.blocking ? t('ind.blocked') : t('ind.waiting'),
  }))
}

const NOTE_LABEL: Record<string, string> = {
  predecessor_cancelled: 'ind.predecessorCancelled', decision_cancelled: 'ind.decisionCancelled', deliverable_issued: 'ind.deliverableIssued', assignee_not_on_project: 'ind.notOnProject',
}

/** Indicator chips (§10.3) from the stored derived state; the blocked chip opens its blockers without opening the panel (§13.3 UX). */
export function TaskIndicators({ r }: { r: TaskRow }) {
  const s = r.state
  return (
    <span className="flex flex-wrap gap-1">
      {s?.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: s.daysOverdue })}</Chip>}
      {s?.isDueSoon && !s.isOverdue && <Chip tone="warn">{t('ind.dueSoon')}</Chip>}
      {s?.isBlocked && <Why reasons={blockerReasons(s)} title={t('task.blockersOf', { key: r.key })}><Chip tone="bad">{s.daysBlocked > 0 ? t('ind.blockedD', { n: s.daysBlocked }) : t('ind.blocked')}</Chip></Why>}
      {s?.isWaiting && !s.isBlocked && <Why reasons={blockerReasons(s)} title={t('task.blockersOf', { key: r.key })}><Chip tone="warn">{t('ind.waiting')}</Chip></Why>}
      {s?.isBlocking && <Chip tone={s.isOverdue ? 'bad' : 'warn'}>{t('ind.blocking', { n: s.blockingCount })}</Chip>}
      {s?.isStale && <Chip tone="idle">{t('ind.staleD', { n: s.staleDays })}</Chip>}
      {s?.isUnassigned && <Chip tone="warn">{t('ind.unassigned')}</Chip>}
      {s?.isMissingDueDate && <Chip tone="idle">{t('ind.noDueDate')}</Chip>}
      {s?.isDateInconsistent && <Chip tone="warn" title={s.inconsistencyDetail?.map((x) => x.text).join('\n')}>{t('ind.dateInconsistent')}</Chip>}
      {s?.isInactiveOwner && <Chip tone="bad">{t('ind.inactiveOwner')}</Chip>}
      {s?.isHeldPastDue && <Chip tone="warn">{t('ind.heldPastDue')}</Chip>}
      {s?.isReviewStalled && <Chip tone="warn">{t('ind.reviewStalled')}</Chip>}
      {r.reviewRound > 0 && <Chip tone="work">{t('ind.reviewRound', { n: r.reviewRound })}</Chip>}
      {r.dueDateChangeCount >= 3 && <Chip tone="warn">{t('ind.dueMoved', { n: r.dueDateChangeCount })}</Chip>}
      {s?.notes.filter((n) => NOTE_LABEL[n]).map((n) => <Chip key={n} tone="warn">{t(NOTE_LABEL[n])}</Chip>)}
    </span>
  )
}

export function errorText(e: unknown) {
  const err = e as ApiError
  return err?.code === 'concurrency_conflict' && err.body.changedBy ? t('error.conflict', { who: err.body.changedBy, when: ago(err.body.changedAt) }) : err?.message ?? t('app.error')
}

/** Client-side hints that mirror the server's permission checks (the server still decides, §8.5). */
export function useTaskHints(p: Pick<ProjectDetail, 'status' | 'permissions'>) {
  const me = useMe()
  const writable = !['Archived', 'Cancelled'].includes(p.status)
  const manages = (r: TaskRow) => writable && (p.permissions.isPm || p.permissions.leadOf.includes(r.projectDisciplineId))
  return {
    me: me.id,
    manages,
    edit: (r: TaskRow) => manages(r) || (writable && (r.assigneeId === me.id || r.collaboratorIds.includes(me.id))),
    due: (r: TaskRow) => manages(r) || (writable && r.assigneeId === me.id),
  }
}

/** FR-RDY-02: the server refused a start that is not Ready until it is acknowledged, reasoned and authorised. */
const needsStartAuthorisation = (e: unknown) => e instanceof ApiError && e.code === 'start_authorisation_required'

/** Saves, transitions and the follow-up offers shared by the list, board and panel (T-16 reasons, T-20 progress offers, G-07 conflicts). */
/** `onSaved` hears about every change that was actually saved, including ones completed in a dialog (meeting tray, §12.13). */
export function useTaskActions(onChanged: () => void, onSaved?: (id: string, key: string, what: string) => void) {
  const [ask, setAsk] = useState<{ title: string; run: (reason: string) => Promise<unknown> } | null>(null)
  const [moving, setMoving] = useState<MoveRequest | null>(null)
  const [offer, setOffer] = useState<{ id: string; key: string; rowVersion: number; to: string } | null>(null)
  const [starting, setStarting] = useState<StartRequest | null>(null)

  const save = async (r: { id: string; key: string; rowVersion: number }, body: Record<string, unknown>): Promise<number | null> => {
    try {
      const res = await patch(`tasks/${r.id}`, body, r.rowVersion)
      onSaved?.(r.id, r.key, describe(body))
      onChanged()
      return res.rowVersion as number
    } catch (e) {
      const err = e as ApiError
      if (err.fieldErrors?.reason && !body.reason) {
        setAsk({ title: t('task.reasonTitle', { key: r.key }), run: async (reason) => { await patch(`tasks/${r.id}`, { ...body, reason }, r.rowVersion); onSaved?.(r.id, r.key, describe(body)); onChanged() } })
        return null
      }
      toast.error(Object.values(err.fieldErrors ?? {})[0]?.[0] ?? errorText(e))
      onChanged()
      return null
    }
  }

  /** true when saved, false when refused, null when a dialog opened to collect what the move needs. */
  const move = async (m: MoveRequest): Promise<boolean | null> => {
    if (m.needsReason || m.to === 'Revision Required' || m.needsReviewer) { setMoving(m); return null }
    const body = { toStatus: m.to, rowVersion: m.rowVersion }
    try { await post(`tasks/${m.id}/transition`, body); onSaved?.(m.id, m.key, `→ ${tv(m.to)}`); onChanged(); return true }
    catch (e) {
      if (needsStartAuthorisation(e)) { setStarting({ id: m.id, key: m.key, to: m.to, body }); return null } // FR-RDY-02
      toast.error(Object.values((e as ApiError).fieldErrors ?? {})[0]?.[0] ?? errorText(e)); onChanged(); return false
    }
  }
  /** A PM or Discipline Lead records a start authorisation that the performer uses (FR-RDY-02). */
  const authoriseStart = (r: { id: string; key: string }) => setStarting({ id: r.id, key: r.key })

  // FR-010: progress above 0 on a Not Started task offers In Progress; 100 offers Complete or Ready for Review.
  const setProgress = async (r: TaskRow, pct: number) => {
    const v = await save(r, { progressPct: pct })
    if (v == null) return
    if (pct > 0 && r.status === 'Not Started') setOffer({ id: r.id, key: r.key, rowVersion: v, to: 'In Progress' })
    else if (pct === 100 && ['Not Started', 'In Progress', 'Revision Required'].includes(r.status))
      setOffer({ id: r.id, key: r.key, rowVersion: v, to: r.requiresReview ? 'Ready for Review' : 'Complete' })
  }

  const dialogs = (
    <>
      {ask && <ConfirmDialog open title={ask.title} body={t('task.reasonBody')} reason onOpenChange={(o) => !o && setAsk(null)} onConfirm={(reason) => ask.run(reason)} />}
      {moving && <TransitionDialog m={moving} onStart={(body) => setStarting({ id: moving.id, key: moving.key, to: moving.to, body })}
        onClose={(done) => { const m = moving; setMoving(null); if (done) { onSaved?.(m.id, m.key, `→ ${tv(m.to)}`); onChanged() } }} />}
      {starting && <StartAuthorisationDialog req={starting} onClose={(done) => {
        const st = starting; setStarting(null)
        if (done && st.to) onSaved?.(st.id, st.key, `→ ${tv(st.to)}`)
        if (done) onChanged()
      }} />}
      {offer && (
        <ConfirmDialog open title={t('task.offerTitle', { key: offer.key, to: tv(offer.to) })} confirmLabel={tv(offer.to)} onOpenChange={(o) => !o && setOffer(null)}
          onConfirm={async () => {
            const o = offer
            setOffer(null)
            if (o.to === 'Ready for Review') {
              const d = await get(`tasks/${o.id}`)
              await move({ id: o.id, key: o.key, rowVersion: d.task.rowVersion, to: o.to, needsReason: false, needsReviewer: !d.task.reviewerId })
            } else await move({ ...o, needsReason: false })
          }} />
      )}
    </>
  )
  return { save, move, setProgress, authoriseStart, dialogs }
}

/** A short description of a saved change for the meeting tray. */
function describe(body: Record<string, unknown>): string {
  return Object.entries(body).filter(([k]) => k !== 'reason').map(([k, v]) =>
    k === 'dueDate' ? `${t('field.DueDate')} ${fmtDate(v as string | null)}` : k === 'assigneeId' ? t('meeting.reassigned') : k === 'progressPct' ? `${t('deliverable.progress')} ${v}%`
      : k === 'priority' ? `${t('common.priority')} ${tv(v as string)}` : t(`field.${k.charAt(0).toUpperCase()}${k.slice(1)}`)).join(', ')
}

/** Status change with the reason, review comment, reviewer or status note it needs (R-03, AC-TSK-03, G-09, C-08). */
export function TransitionDialog({ m, onClose, onStart }: { m: MoveRequest; onClose: (done: boolean) => void; onStart?: (body: Record<string, unknown>) => void }) {
  const revision = m.to === 'Revision Required'
  const [comment, setComment] = useState('')
  const [reviewer, setReviewer] = useState<string | null>(null)
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('task.moveTitle', { key: m.key, to: tv(m.to) })}
      body={m.via ? t('task.via', { via: tv(m.via) }) : undefined} confirmLabel={tv(m.to)}
      reason={m.needsReason ? true : undefined} busy={(revision && !comment.trim()) || (!!m.needsReviewer && !reviewer)}
      onConfirm={async (reason) => {
        const body = { toStatus: m.to, reason: reason || undefined, comment: comment.trim() || undefined, reviewerId: reviewer ?? undefined, rowVersion: m.rowVersion }
        try { await post(`tasks/${m.id}/transition`, body) }
        catch (e) { if (onStart && needsStartAuthorisation(e)) { onStart(body); return } throw e } // FR-RDY-02: the readiness dialog takes over
        onClose(true)
      }}>
      {m.needsReviewer && <Field label={t('field.ReviewerId')} hint={t('task.reviewerNeeded')}><PeoplePicker value={reviewer} onChange={setReviewer} /></Field>}
      <Field label={revision ? t('task.revisionComment') : t('deliverable.statusNote')} htmlFor="tt-comment" hint={revision ? t('task.revisionHint') : t('common.optional')}>
        <Textarea id="tt-comment" rows={3} value={comment} onChange={(e) => setComment(e.target.value)} />
      </Field>
    </ConfirmDialog>
  )
}

export function moveFrom(detail: any, x: Transition): MoveRequest {
  return { id: detail.task.id, key: detail.task.key, rowVersion: detail.task.rowVersion, to: x.to, needsReason: x.needsReason, via: x.via,
    needsReviewer: x.to === 'Ready for Review' && detail.task.requiresReview && !detail.task.reviewerId }
}

/** Status pill that lists only the transitions the server allows for this user, fetched when opened (FR-004). */
export function StatusMenu({ r, onMove, disabled }: { r: TaskRow; onMove: (m: MoveRequest) => void; disabled?: boolean }) {
  const [open, setOpen] = useState(false)
  const q = useQuery({ queryKey: ['task', r.id, r.rowVersion], queryFn: () => get(`tasks/${r.id}`), enabled: open })
  if (disabled) return <StatusPill status={r.status} />
  const list: Transition[] = q.data?.permissions.transitions ?? []
  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <button className="rounded-full focus-visible:outline-2" aria-label={t('task.changeStatus', { key: r.key, status: tv(r.status) })}><StatusPill status={r.status} /></button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="max-w-80">
        {q.isPending ? <div className="p-2"><Spinner /></div> : list.map((x) => (
          <DropdownMenuItem key={x.to} disabled={!x.allowed} onSelect={() => onMove(moveFrom(q.data, x))} className="flex-col items-start gap-0">
            <span>{tv(x.to)}{x.via && <span className="ml-1 text-xs text-muted-foreground">{t('task.viaShort', { via: tv(x.via) })}</span>}</span>
            {!x.allowed && x.reason && <span className="text-xs text-muted-foreground">{x.reason}</span>}
          </DropdownMenuItem>
        ))}
        {q.data?.permissions.completeHint && <DropdownMenuItem disabled className="text-xs">{q.data.permissions.completeHint}</DropdownMenuItem>}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

// ---------- Filters shared by list and board (URL-encoded, §13.3 quick chips) ----------

export const QUICK = ['mine', 'overdue', 'blocked', 'blocking', 'dueThisWeek', 'unassigned', 'readyForReview'] as const

export function useTaskFilters() {
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); n.delete('panel'); setSp(n, { replace: true }) }
  const filters: Record<string, string | undefined> = {}
  for (const k of FILTER_KEYS) filters[k] = sp.get(k) ?? undefined
  const clear = () => { const n = new URLSearchParams(sp); for (const k of FILTER_KEYS) n.delete(k); setSp(n, { replace: true }) }
  return { sp, set, filters, clear, active: FILTER_KEYS.some((k) => sp.has(k)) }
}
/** Filters that links from the dashboard and elsewhere may carry beyond the visible controls; shown as removable tokens. */
const EXTRA = ['waiting', 'open', 'stale', 'dueSoon', 'noDueDate', 'dateInconsistent', 'heldPastDue', 'dueFrom', 'dueTo', 'milestoneId', 'assigneeId', 'ids']
const FILTER_KEYS = ['q', 'disciplineId', 'deliverableId', 'status', 'priority', ...QUICK, ...EXTRA]

export function TaskFilterBar({ p, deliverables, extra }: { p: ProjectDetail; deliverables: DeliverableRow[]; extra?: ReactNode }) {
  const { sp, set, filters, clear, active } = useTaskFilters()
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <Input key={filters.q ? 'q' : 'empty'} className="h-8 w-52" type="search" placeholder={t('task.searchPlaceholder')} defaultValue={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} aria-label={t('common.search')} />
        <select className={sel} value={filters.disciplineId ?? ''} onChange={(e) => set('disciplineId', e.target.value)} aria-label={t('common.discipline')}>
          <option value="">{t('projects.anyDiscipline')}</option>{p.disciplines.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <select className={cn(sel, 'max-w-56')} value={filters.deliverableId ?? ''} onChange={(e) => set('deliverableId', e.target.value)} aria-label={t('task.deliverable')}>
          <option value="">{t('task.anyDeliverable')}</option>{deliverables.map((d) => <option key={d.id} value={d.id}>{d.key} {d.name}</option>)}
        </select>
        <select className={sel} value={filters.status ?? ''} onChange={(e) => set('status', e.target.value)} aria-label={t('common.status')}>
          <option value="">{t('task.anyOpenStatus')}</option>{TASK_STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
        </select>
        <select className={sel} value={filters.priority ?? ''} onChange={(e) => set('priority', e.target.value)} aria-label={t('common.priority')}>
          <option value="">{t('task.anyPriority')}</option>{PRIORITIES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
        </select>
        <div className="flex-1" />
        {extra}
      </div>
      <div className="flex flex-wrap gap-1.5" role="group" aria-label={t('task.quickFilters')}>
        {QUICK.map((k) => (
          <button key={k} type="button" aria-pressed={sp.get(k) === 'true'} onClick={() => set(k, sp.get(k) === 'true' ? null : 'true')}
            className={cn('rounded-full border px-2.5 py-0.5 text-xs', sp.get(k) === 'true' ? 'border-primary bg-primary text-primary-foreground' : 'bg-card hover:bg-muted')}>{t(`tquick.${k}`)}</button>
        ))}
        {EXTRA.filter((k) => sp.has(k)).map((k) => (
          <button key={k} type="button" onClick={() => set(k, null)} className="rounded-full border border-primary bg-accent px-2.5 py-0.5 text-xs" aria-label={t('task.removeFilter', { name: t(`tfilter.${k}`) })}>
            {t(`tfilter.${k}`)}{['dueFrom', 'dueTo'].includes(k) ? `: ${sp.get(k)}` : k === 'ids' ? `: ${sp.get(k)!.split(',').length}` : ''} ×
          </button>
        ))}
        {active && <button type="button" className="px-2 text-xs text-muted-foreground hover:text-foreground" onClick={clear}>{t('common.clear')}</button>}
      </div>
    </div>
  )
}

/** List and board share scope and filters: switching keeps the query string (FR-VIS-04). */
export function ViewSwitch({ view, paths }: { view: 'tasks' | 'board'; paths?: { tasks: string; board: string } }) {
  const [sp] = useSearchParams()
  const n = new URLSearchParams(sp); n.delete('panel')
  const cls = (on: boolean) => cn('inline-flex h-8 items-center gap-1 px-2.5 text-sm', on ? 'bg-accent font-medium' : 'hover:bg-muted')
  return (
    <div className="inline-flex overflow-hidden rounded-md border bg-card" role="group" aria-label={t('task.view')}>
      <Link to={{ pathname: paths?.tasks ?? '../tasks', search: n.toString() }} relative="path" className={cls(view === 'tasks')} aria-current={view === 'tasks' ? 'page' : undefined}><List className="size-4" />{t('task.listView')}</Link>
      <Link to={{ pathname: paths?.board ?? '../board', search: n.toString() }} relative="path" className={cls(view === 'board')} aria-current={view === 'board' ? 'page' : undefined}><KanbanSquare className="size-4" />{t('ptab.board')}</Link>
    </div>
  )
}

export function useProjectLists(projectId: string | undefined) {
  const deliverables = useQuery({ queryKey: ['p', projectId, 'deliverables', {}], queryFn: () => get<DeliverableRow[]>(`projects/${projectId}/deliverables`), enabled: !!projectId })
  const milestones = useQuery({ queryKey: ['p', projectId, 'milestones', false, ''], queryFn: () => get<MilestoneRow[]>(`projects/${projectId}/milestones`), enabled: !!projectId })
  return { deliverables: deliverables.data ?? [], milestones: milestones.data ?? [] }
}

// ---------- Task list (§13.3) ----------

interface Col { id: string; label: string; sort?: string; optional?: boolean; cls?: string; cell: (r: TaskRow) => ReactNode }
type Flat = { kind: 'group'; id: string; label: string; count: number; deliverableId?: string; status?: string; progress?: number | null } | { kind: 'row'; r: TaskRow }

const GROUPS = ['deliverable', 'discipline', 'milestone', 'assignee', 'status', 'due', 'none']
export const BUCKETS = ['overdue', 'today', 'thisWeek', 'nextWeek', 'later', 'noDate']
const COLS_KEY = 'hub.taskColumns'

export function dueBucket(r: TaskRow, now: string) {
  if (r.state?.isOverdue) return 'overdue'
  if (!r.dueDate) return 'noDate'
  const dow = (new Date(now + 'T00:00:00Z').getUTCDay() + 6) % 7
  const weekEnd = addDays(now, 6 - dow)
  if (r.dueDate === now) return 'today'
  if (r.dueDate <= weekEnd) return 'thisWeek'
  if (r.dueDate <= addDays(weekEnd, 7)) return 'nextWeek'
  return 'later'
}

function groupRows(rows: TaskRow[], by: string, deliverables: Map<string, DeliverableRow>): Flat[] {
  if (by === 'none') return rows.map((r) => ({ kind: 'row', r }))
  const now = today()
  const key = (r: TaskRow): [string, string, number | string] => {
    switch (by) {
      case 'discipline': return [r.projectDisciplineId, r.disciplineName, r.disciplineOrder]
      case 'milestone': return [r.effectiveMilestoneId ?? '', r.milestoneKey ? `${r.milestoneKey} ${r.milestoneName}` : t('task.noMilestone'), r.milestoneDate ?? '9999']
      case 'assignee': return [r.assigneeId ?? '', r.assigneeName ?? t('ind.unassigned'), r.assigneeName ?? '']
      case 'status': return [r.status, tv(r.status), TASK_STATUSES.indexOf(r.status)]
      case 'due': { const b = dueBucket(r, now); return [b, t(`tbucket.${b}`), BUCKETS.indexOf(b)] }
      default: return [r.deliverableId ?? '', r.deliverableKey ? `${r.deliverableKey} ${r.deliverableName}` : t('task.noDeliverable'), r.deliverableOrder ?? 1e9]
    }
  }
  const groups = new Map<string, { label: string; order: number | string; rows: TaskRow[] }>()
  for (const r of rows) {
    const [id, label, order] = key(r)
    const g = groups.get(id) ?? { label, order, rows: [] }
    g.rows.push(r)
    groups.set(id, g)
  }
  const out: Flat[] = []
  for (const [id, g] of [...groups.entries()].sort((a, b) => (a[1].order < b[1].order ? -1 : a[1].order > b[1].order ? 1 : a[1].label.localeCompare(b[1].label)))) {
    const d = by === 'deliverable' && id ? deliverables.get(id) : undefined
    out.push({ kind: 'group', id, label: g.label, count: g.rows.length, deliverableId: by === 'deliverable' ? id || undefined : undefined, status: d?.status, progress: d?.state?.progressPct })
    for (const r of g.rows) out.push({ kind: 'row', r })
  }
  return out
}

function readCols(): string[] | null {
  try { const v = localStorage.getItem(COLS_KEY); return v ? JSON.parse(v) : null } catch { return null }
}

export function TasksTab() {
  const p = useCurrentProject()
  const openPanel = useItemPanel()
  const refresh = useProjectRefresh()
  const hints = useTaskHints(p)
  const { sp, set, filters } = useTaskFilters()
  const { deliverables, milestones } = useProjectLists(p.id)
  const byId = useMemo(() => new Map(deliverables.map((d) => [d.id, d])), [deliverables])
  const [creating, setCreating] = useState<{ deliverableId?: string } | null>(null)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [bulk, setBulk] = useState<string | null>(null)
  const [visible, setVisible] = useState<string[] | null>(readCols)
  const sort = sp.get('sort') ?? ''
  const group = sp.get('group') ?? 'deliverable'
  const params = { ...filters, sort: sort || undefined, pageSize: 200 }
  const q = useInfiniteQuery({
    queryKey: ['p', p.id, 'tasks', params],
    queryFn: ({ pageParam }) => get<PageOf<TaskRow>>(`projects/${p.id}/tasks${qs({ ...params, page: pageParam })}`),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
  })
  // Pages load in the background so grouping and selection see every row; only visible rows render (E-22).
  useEffect(() => { if (q.hasNextPage && !q.isFetchingNextPage) q.fetchNextPage() }, [q.hasNextPage, q.isFetchingNextPage, q.fetchNextPage])
  const rows = useMemo(() => q.data?.pages.flatMap((x) => x.items) ?? [], [q.data])
  const total = q.data?.pages[0]?.totalCount ?? 0
  const flat = useMemo(() => groupRows(rows, group, byId), [rows, group, byId])
  const reload = () => { q.refetch(); refresh(p.id) }
  const actions = useTaskActions(reload)
  const canCreate = p.permissions.createTaskIn.length > 0
  const toggle = (id: string) => setSelected((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })

  const all: Col[] = [
    { id: 'key', label: 'milestone.key', sort: 'key', cls: 'whitespace-nowrap', cell: (r) => <button className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button> },
    { id: 'name', label: 'task.name', sort: 'name', cls: 'min-w-56', cell: (r) => <button className="text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button> },
    { id: 'deliverable', label: 'task.deliverable', cell: (r) => r.deliverableId ? <button className="block max-w-44 truncate text-left hover:underline" title={`${r.deliverableKey} ${r.deliverableName}`} onClick={() => openPanel('Deliverable', r.deliverableId!)}>{r.deliverableName}</button> : t('common.dash') },
    { id: 'discipline', label: 'common.discipline', cls: 'whitespace-nowrap', cell: (r) => <><span className="mr-1 inline-block size-2 rounded-sm" style={{ background: r.disciplineColour }} aria-hidden />{r.disciplineName}</> },
    { id: 'assignee', label: 'field.AssigneeId', cls: 'min-w-36', cell: (r) => hints.manages(r)
      ? <PeoplePicker compact value={r.assigneeId} valueName={r.assigneeActive ? r.assigneeName : t('common.inactiveSuffix', { name: r.assigneeName ?? '' })} placeholder={t('ind.unassigned')} label={t('task.assigneeOf', { key: r.key })} onChange={(id) => actions.save(r, { assigneeId: id })} />
      : <span className={cn(!r.assigneeActive && 'text-muted-foreground')}>{r.assigneeName ? (r.assigneeActive ? r.assigneeName : t('common.inactiveSuffix', { name: r.assigneeName })) : <span className="text-muted-foreground">{t('ind.unassigned')}</span>}</span> },
    { id: 'reviewer', label: 'field.ReviewerId', cls: 'whitespace-nowrap', cell: (r) => r.reviewerName ?? (r.requiresReview ? <span className="text-warn">{t('task.noReviewer')}</span> : t('common.dash')) },
    { id: 'status', label: 'common.status', sort: 'status', cell: (r) => <StatusMenu r={r} onMove={actions.move} disabled={p.status === 'Archived' || p.status === 'Cancelled'} /> },
    { id: 'indicators', label: 'deliverable.indicators', cls: 'min-w-32', cell: (r) => <TaskIndicators r={r} /> },
    { id: 'priority', label: 'common.priority', sort: 'priority', cell: (r) => hints.edit(r)
      ? <select className="h-7 rounded border border-transparent bg-transparent px-1 text-[13px] hover:border-border" value={r.priority} aria-label={t('task.priorityOf', { key: r.key })} onChange={(e) => actions.save(r, { priority: e.target.value })}>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select>
      : <PriorityBadge priority={r.priority} /> },
    { id: 'start', label: 'field.StartDate', sort: 'startDate', cls: 'whitespace-nowrap', cell: (r) => fmtDate(r.startDate) },
    { id: 'due', label: 'common.due', sort: 'dueDate', cls: 'whitespace-nowrap', cell: (r) => (
      <span className={cn(r.state?.isOverdue && 'font-medium text-bad', r.state?.isDueSoon && !r.state.isOverdue && 'text-warn')}>
        <InlineDate value={r.dueDate} disabled={!hints.due(r)} title={t('task.dueOf', { key: r.key })} onSave={(v) => actions.save(r, { dueDate: v })} />
      </span>) },
    { id: 'progress', label: 'deliverable.progress', sort: 'progress', cell: (r) => hints.edit(r) && r.status !== 'Complete' && r.status !== 'Cancelled'
      ? <select className="h-7 rounded border border-transparent bg-transparent px-1 text-[13px] tabular-nums hover:border-border" value={r.progressPct} aria-label={t('task.progressOf', { key: r.key })} onChange={(e) => actions.setProgress(r, Number(e.target.value))}>{PROGRESS.map((x) => <option key={x} value={x}>{x}%</option>)}</select>
      : <span className="tabular-nums">{r.progressPct}%</span> },
    { id: 'estimate', label: 'task.estimate', sort: 'estimate', cls: 'text-right tabular-nums', cell: (r) => r.estimatedHours ?? t('common.dash') },
    { id: 'lastActivity', label: 'task.lastActivity', sort: 'lastActivity', cls: 'whitespace-nowrap text-muted-foreground', cell: (r) => <span title={r.lastActivityAt}>{ago(r.lastActivityAt)}</span> },
    { id: 'milestone', label: 'field.MilestoneId', optional: true, cls: 'whitespace-nowrap', cell: (r) => r.milestoneKey ? <span title={r.milestoneName}>{r.milestoneKey}{r.milestoneDerived && <span className="text-muted-foreground"> ↳</span>}</span> : t('common.dash') },
    { id: 'created', label: 'task.createdCol', sort: 'created', optional: true, cls: 'whitespace-nowrap', cell: (r) => fmtDate(r.createdAt) },
    { id: 'completed', label: 'task.completedCol', sort: 'completed', optional: true, cls: 'whitespace-nowrap', cell: (r) => fmtDate(r.completedAt) },
    { id: 'blocking', label: 'task.blockingCount', optional: true, cls: 'text-right tabular-nums', cell: (r) => r.state?.blockingCount || t('common.dash') },
    { id: 'reviewRound', label: 'task.reviewRound', optional: true, cls: 'text-right tabular-nums', cell: (r) => r.reviewRound || t('common.dash') },
    { id: 'dueChanges', label: 'task.dueChanges', optional: true, cls: 'text-right tabular-nums', cell: (r) => r.dueDateChangeCount || t('common.dash') },
  ]
  const urlCols = sp.get('cols')?.split(',') // a saved view or shared link carries its columns
  const shown = all.filter((c) => (urlCols ? urlCols.includes(c.id) || c.id === 'key' || c.id === 'name' : visible ? visible.includes(c.id) : !c.optional))
  const setCols = (ids: string[]) => { setVisible(ids); try { localStorage.setItem(COLS_KEY, JSON.stringify(ids)) } catch { /* per-viewer convenience only */ } if (urlCols) set('cols', null) }

  const scroller = useRef<HTMLDivElement>(null)
  const virtual = useVirtualizer({ count: flat.length, getScrollElement: () => scroller.current, estimateSize: () => 37, overscan: 15 })
  const items = virtual.getVirtualItems()
  const padTop = items[0]?.start ?? 0
  const padBottom = items.length ? virtual.getTotalSize() - items[items.length - 1].end : 0
  const sortBy = (field: string) => set('sort', sort === field ? `${field}:desc` : sort === `${field}:desc` ? null : field)

  return (
    <Page title={t('ptab.tasks')} subtitle={q.data ? plural(total, 'decision.task1', 'task.count') : undefined}
      actions={<><ViewMenu listType="tasks" projectId={p.id} extra={() => ({ cols: shown.map((c) => c.id).join(',') })} /><ViewSwitch view="tasks" />{canCreate && <Button onClick={() => setCreating({})}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <TaskFilterBar p={p} deliverables={deliverables} extra={<>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={group} onChange={(e) => set('group', e.target.value === 'deliverable' ? null : e.target.value)} aria-label={t('common.groupBy')}>
          {GROUPS.map((g) => <option key={g} value={g}>{t(`tgroup.${g}`)}</option>)}
        </select>
        <DropdownMenu>
          <DropdownMenuTrigger asChild><Button variant="outline" size="sm"><Columns3 className="size-4" />{t('task.columns')}</Button></DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {all.filter((c) => c.id !== 'key' && c.id !== 'name').map((c) => (
              <DropdownMenuCheckboxItem key={c.id} checked={shown.includes(c)} onSelect={(e) => e.preventDefault()}
                onCheckedChange={(on) => setCols((on ? [...shown.map((x) => x.id), c.id] : shown.map((x) => x.id).filter((x) => x !== c.id)))}>{t(c.label)}</DropdownMenuCheckboxItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        <ExportMenu path={`projects/${p.id}/tasks/export`} params={{ ...filters, sort: sort || undefined }} name={`${p.projectNumber}-tasks`} />
      </>} />
      {selected.size > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-md border bg-accent px-3 py-1.5 text-sm" role="region" aria-label={t('bulk.actions')}>
          <span>{t('bulk.selected', { n: selected.size })}</span>
          {['assign', 'setDueDate', 'shiftDueDates', 'setPriority', 'setDeliverable', 'hold', 'cancel'].map((op) => (
            <Button key={op} size="sm" variant="outline" onClick={() => setBulk(op)}>{t(`tbulk.${op}`)}</Button>
          ))}
          <Button size="sm" variant="ghost" onClick={() => setSelected(new Set())}>{t('common.clear')}</Button>
        </div>
      )}
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={8} /> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={canCreate && <Button onClick={() => setCreating({})}>{t('task.new')}</Button>}>{t('task.empty')}</Empty></div>
      ) : (
        <div ref={scroller} className="max-h-[calc(100dvh-17rem)] min-h-64 overflow-auto rounded-lg border bg-card">
          <table className="w-full text-[13px]" aria-rowcount={flat.length + 1}>
            <thead className="sticky top-0 z-10 bg-muted text-left text-xs text-muted-foreground">
              <tr>
                <th className="w-8 px-2"><Checkbox aria-label={t('bulk.selectAll')} checked={rows.length > 0 && selected.size === rows.length} onCheckedChange={(c) => setSelected(c ? new Set(rows.map((r) => r.id)) : new Set())} /></th>
                {shown.map((c) => {
                  const dir = sort === c.sort ? 'ascending' : sort === `${c.sort}:desc` ? 'descending' : undefined
                  return (
                    <th key={c.id} className="whitespace-nowrap px-2 py-2 font-medium" aria-sort={c.sort ? dir ?? 'none' : undefined}>
                      {c.sort ? <button className="inline-flex items-center gap-1 hover:text-foreground" onClick={() => sortBy(c.sort!)}>{t(c.label)}
                        {dir === 'ascending' && <ArrowUp className="size-3" />}{dir === 'descending' && <ArrowDown className="size-3" />}</button> : t(c.label)}
                    </th>
                  )
                })}
              </tr>
            </thead>
            <tbody>
              {padTop > 0 && <tr aria-hidden style={{ height: padTop }} />}
              {items.map((vi) => {
                const f = flat[vi.index]
                if (f.kind === 'group') return (
                  <tr key={`g-${f.id}`} data-index={vi.index} ref={virtual.measureElement} className="border-t bg-muted/40">
                    <th colSpan={shown.length + 1} scope="colgroup" className="px-3 py-1.5 text-left text-sm font-semibold">
                      <span className="inline-flex flex-wrap items-center gap-2">{f.label}<span className="text-xs font-normal text-muted-foreground">{f.count}</span>
                        {f.status && <StatusPill status={f.status} />}{f.progress != null && <span className="text-xs font-normal text-muted-foreground">{f.progress}%</span>}
                        {group === 'deliverable' && canCreate && <button className="inline-flex items-center gap-0.5 text-xs font-normal text-primary hover:underline" onClick={() => setCreating({ deliverableId: f.deliverableId })}><Plus className="size-3" />{t('task.addHere')}</button>}
                      </span>
                    </th>
                  </tr>
                )
                const r = f.r
                return (
                  <tr key={r.id} data-index={vi.index} ref={virtual.measureElement} className={cn('border-t hover:bg-muted/30', selected.has(r.id) && 'bg-accent/40')}>
                    <td className="px-2"><Checkbox aria-label={`${t('bulk.select')} ${r.key}`} checked={selected.has(r.id)} onCheckedChange={() => toggle(r.id)} /></td>
                    {shown.map((c) => <td key={c.id} className={cn('px-2 py-1', c.cls)}>{c.cell(r)}</td>)}
                  </tr>
                )
              })}
              {padBottom > 0 && <tr aria-hidden style={{ height: padBottom }} />}
            </tbody>
          </table>
          {q.isFetchingNextPage && <div className="flex items-center gap-2 p-2 text-xs text-muted-foreground"><Spinner />{t('task.loadingMore', { n: rows.length, total })}</div>}
        </div>
      )}
      {creating && <CreateTask p={p} deliverables={deliverables} milestones={milestones} defaults={creating} onClose={(id) => { setCreating(null); if (id) { reload(); openPanel('Task', id) } }} />}
      {bulk && <TaskBulkDialog p={p} op={bulk} ids={[...selected]} deliverables={deliverables} onClose={(ok) => { setBulk(null); if (ok) { setSelected(new Set()); reload() } }} />}
      {actions.dialogs}
    </Page>
  )
}

/** Bulk actions with per-row permission checks and one summary (FR-TSK-09, T-23, E-20). */
function TaskBulkDialog({ p, op, ids, deliverables, onClose }: { p: ProjectDetail; op: string; ids: string[]; deliverables: DeliverableRow[]; onClose: (ok: boolean) => void }) {
  const [v, setV] = useState<string | null>(op === 'setPriority' ? 'Medium' : '')
  const needs = op === 'shiftDueDates' || op === 'hold' || op === 'cancel'
  const ready = op === 'hold' || op === 'cancel' || op === 'setDeliverable' || (v != null && v !== '')
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t(`tbulk.${op}`)} body={t('bulk.selected', { n: ids.length })}
      reason={needs ? true : op === 'setDueDate' ? 'optional' : undefined} busy={!ready} destructive={op === 'cancel'}
      onConfirm={async (reason) => {
        const params = op === 'assign' ? { assigneeId: v } : op === 'setDueDate' ? { dueDate: v } : op === 'shiftDueDates' ? { days: Number(v) }
          : op === 'setPriority' ? { priority: v } : op === 'setDeliverable' ? { deliverableId: v || null } : {}
        const r = await post(`projects/${p.id}/tasks/bulk`, { taskIds: ids, operation: op, params, reason: reason || undefined })
        const skipped = r.skipped as { key: string; reason: string }[]
        toast.success(t('bulk.result', { updated: r.updated, skipped: skipped.length }), { description: skipped.slice(0, 6).map((s) => `${s.key}: ${s.reason}`).join('\n') || undefined, duration: skipped.length ? 12000 : 4000 })
        onClose(true)
      }}>
      {op === 'assign' && <PeoplePicker value={v} onChange={setV} />}
      {op === 'setDueDate' && <Field label={t('field.DueDate')} htmlFor="tb-due"><Input id="tb-due" type="date" value={v ?? ''} onChange={(e) => setV(e.target.value)} /></Field>}
      {op === 'shiftDueDates' && <Field label={t('bulk.days')} htmlFor="tb-days"><Input id="tb-days" type="number" value={v ?? ''} onChange={(e) => setV(e.target.value)} /></Field>}
      {op === 'setPriority' && <select className={selectCls} value={v ?? ''} onChange={(e) => setV(e.target.value)} aria-label={t('common.priority')}>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select>}
      {op === 'setDeliverable' && <select className={selectCls} value={v ?? ''} onChange={(e) => setV(e.target.value)} aria-label={t('task.deliverable')}>
        <option value="">{t('common.none')}</option>{deliverables.map((d) => <option key={d.id} value={d.id}>{d.key} {d.name}</option>)}</select>}
    </ConfirmDialog>
  )
}

/** Create task (FR-TSK-01, Workflow 4): discipline, deliverable or milestone, people, dates, estimate. */
export function CreateTask({ p, deliverables, milestones, defaults, onClose }: {
  p: ProjectDetail; deliverables: DeliverableRow[]; milestones: MilestoneRow[]; defaults?: { deliverableId?: string; projectDisciplineId?: string }; onClose: (createdId?: string) => void
}) {
  const allowed = p.disciplines.filter((d) => d.isActive && p.permissions.createTaskIn.includes(d.id))
  const fromDeliverable = deliverables.find((d) => d.id === defaults?.deliverableId)
  const [f, setF] = useState<Record<string, any>>({
    priority: 'Medium', requiresReview: false, deliverableId: defaults?.deliverableId ?? '',
    projectDisciplineId: defaults?.projectDisciplineId ?? (fromDeliverable && allowed.some((d) => d.id === fromDeliverable.projectDisciplineId) ? fromDeliverable.projectDisciplineId : allowed[0]?.id),
  })
  const [assignee, setAssignee] = useState<string | null>(null)
  const [reviewer, setReviewer] = useState<string | null>(null)
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const up = (k: string, v: unknown) => setF((x) => ({ ...x, [k]: v }))
  const chooseDeliverable = (id: string) => {
    const d = deliverables.find((x) => x.id === id)
    setF((x) => ({ ...x, deliverableId: id, milestoneId: '', projectDisciplineId: d && allowed.some((a) => a.id === d.projectDisciplineId) ? d.projectDisciplineId : x.projectDisciplineId }))
  }
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      const r = await post(`projects/${p.id}/tasks`, {
        name: f.name, description: f.description || null, projectDisciplineId: f.projectDisciplineId, deliverableId: f.deliverableId || null,
        milestoneId: f.deliverableId ? null : f.milestoneId || null, assigneeId: assignee, reviewerId: reviewer, requiresReview: f.requiresReview,
        priority: f.priority, startDate: f.startDate || null, dueDate: f.dueDate || null, estimatedHours: f.estimatedHours === '' || f.estimatedHours == null ? null : Number(f.estimatedHours),
      })
      toast.success(t('task.created', { key: r.key }))
      onClose(r.id)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const fe = err?.fieldErrors ?? {}
  const dl = deliverables.find((d) => d.id === f.deliverableId)
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader><DialogTitle>{t('task.new')}</DialogTitle></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('task.name')} htmlFor="t-name" error={fe.name} className="sm:col-span-2"><Input id="t-name" required autoFocus value={f.name ?? ''} onChange={(e) => up('name', e.target.value)} /></Field>
          <Field label={t('common.discipline')} htmlFor="t-disc" error={fe.projectDisciplineId}>
            <select id="t-disc" className={selectCls} value={f.projectDisciplineId ?? ''} onChange={(e) => up('projectDisciplineId', e.target.value)}>{allowed.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
          </Field>
          <Field label={t('task.deliverable')} htmlFor="t-del" error={fe.deliverableId}>
            <select id="t-del" className={selectCls} value={f.deliverableId ?? ''} onChange={(e) => chooseDeliverable(e.target.value)}>
              <option value="">{t('common.none')}</option>{deliverables.filter((d) => !['Cancelled'].includes(d.status)).map((d) => <option key={d.id} value={d.id}>{d.key} {d.name}</option>)}
            </select>
          </Field>
          {!f.deliverableId && (
            <Field label={t('field.MilestoneId')} htmlFor="t-ms" hint={t('task.milestoneHint')} error={fe.milestoneId}>
              <select id="t-ms" className={selectCls} value={f.milestoneId ?? ''} onChange={(e) => up('milestoneId', e.target.value)}>
                <option value="">{t('common.none')}</option>{milestones.map((m) => <option key={m.id} value={m.id}>{m.key} {m.name}</option>)}
              </select>
            </Field>
          )}
          <Field label={t('common.priority')} htmlFor="t-prio">
            <select id="t-prio" className={selectCls} value={f.priority} onChange={(e) => up('priority', e.target.value)}>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select>
          </Field>
          <Field label={t('field.AssigneeId')} error={fe.assigneeId} hint={t('task.assigneeHint')}><PeoplePicker value={assignee} onChange={setAssignee} /></Field>
          <Field label={t('field.ReviewerId')} error={fe.reviewerId}><PeoplePicker value={reviewer} onChange={setReviewer} exclude={assignee ? [assignee] : []} /></Field>
          <Field label={t('field.StartDate')} htmlFor="t-start" error={fe.startDate}><Input id="t-start" type="date" value={f.startDate ?? ''} onChange={(e) => up('startDate', e.target.value)} /></Field>
          <Field label={t('field.DueDate')} htmlFor="t-due" error={fe.dueDate} hint={dl?.dueDate ? t('task.deliverableDue', { date: fmtDate(dl.dueDate) }) : undefined}>
            <Input id="t-due" type="date" value={f.dueDate ?? ''} onChange={(e) => up('dueDate', e.target.value)} />
          </Field>
          <Field label={t('task.estimateHours')} htmlFor="t-est" error={fe.estimatedHours}><Input id="t-est" type="number" min={0} step={0.5} value={f.estimatedHours ?? ''} onChange={(e) => up('estimatedHours', e.target.value)} /></Field>
          <label className="flex items-center gap-2 self-end pb-2 text-sm"><Checkbox checked={f.requiresReview} onCheckedChange={(c) => up('requiresReview', !!c)} />{t('field.RequiresReview')}</label>
          <Field label={t('common.description')} htmlFor="t-desc" hint={t('task.descriptionHint')} className="sm:col-span-2"><Textarea id="t-desc" rows={3} value={f.description ?? ''} onChange={(e) => up('description', e.target.value)} /></Field>
          {err && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2">
            <Button type="button" variant="outline" onClick={() => onClose()}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy || !f.name?.trim() || !f.projectDisciplineId}>{busy && <Spinner />}{t('task.create')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
