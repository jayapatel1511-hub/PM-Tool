import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { useWorkSeenDetailOwner, WorkSeenDetailNotice } from '@/components/hub/work-seen-detail-owner'
import type { WorkSeenFields } from '@/components/hub/work-seen'
import { personOptionLabel } from '@/components/hub/person-option-label'
import { PickerProject } from '@/components/hub/people'
import { CreateDeliverable } from './Deliverables'
import { DateHint } from '@/components/hub/date-context'
import { FieldGroup } from '@/components/hub/registers'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Check, ChevronDown, Eye, EyeOff, GitBranch, Link2, Lock, Plus, RotateCcw, Trash2, X } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, ConfirmDialog, Empty, ErrorBanner, Field, Loading, Notice, Spinner, selectCls } from '@/components/hub/common'
import { FieldRow, HistoryList, InlineDate, InlinePerson, InlineSelect, InlineText, SaveStatus, TabBar, useRecordTab } from '@/components/hub/fields'
import { useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { Avatar, CapacityWeek, PeoplePicker, weekOf } from '@/components/hub/people'
import { DepTypeChip, DepTypeField } from '@/components/hub/indicators'
import { Chip, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { RichText } from '@/components/hub/richtext'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProject, useProjectRefresh } from '@/hooks/data'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { invalidateChange } from '@/lib/keys'
import { useFlag, useMe } from '@/lib/auth'
import { fmtDate, fmtTime, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { CommentsSlot, ItemSlots, LinksSlot, RaiseSlot } from './slots-items'
import { RecordContext } from '@/components/hub/record-context'
import { WorkReadinessFacts, WorkSourceUses, WorkWaitingInputs } from './WorkInputs'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { PRIORITIES, TaskIndicators, blockersOf, errorText, moveFrom, readableDisabled, useProjectLists, useTaskActions, type Blocker, type PendingTaskAction, type TaskRow } from './Tasks'
import { TaskSection, TaskSectionNavigation } from './task-section-navigation'
import { TaskContribution } from './task-contribution'
import { runtimeText } from '@/lib/i18n'
import { FormNavigationScope } from '@/components/hub/form-navigation-context'
import { preservesTaskDetailNavigation } from '@/lib/drafts'

/** Sections added to the task panel by later packets (actual hours and Add Time, packet 024). */
export const TASK_SECTIONS: { id: string; render: (d: TaskDetailData, reload: () => void) => ReactNode }[] = []

export type TaskDetailData = components['schemas']['TaskDetailResponse']
/** GET /tasks/{id}: the row, the people and every permission the panel and the status menu read. */
export const readTask = (id: string) => typedRequest('get', '/api/v1/tasks/{id}', { params: { id } })

const BLOCK_TYPES = ['Client', 'External Party', 'Internal', 'Decision', 'Information', 'Other']

/** Current read authority gates a held dialog; its command still owns the original target, draft and version. */
function taskActionAllowed(detail: TaskDetailData, action: PendingTaskAction) {
  if (action.id !== detail.task.id) return false
  const permissions = detail.permissions
  if (action.kind === 'transition') return permissions.transitions.some(transition => transition.to === action.to && transition.allowed)
  if (action.kind === 'authoriseStart') return permissions.authoriseStart
  return action.fields.length > 0 && action.fields.every(field => {
    if (field === 'assigneeId') return permissions.assign.ok
    if (field === 'reviewerId') return permissions.setReviewer.ok
    if (field === 'dueDate') return permissions.dueDate.ok
    return ['name', 'description', 'requiresReview', 'priority', 'estimatedHours', 'progressPct', 'startDate', 'projectDisciplineId', 'deliverableId', 'milestoneId'].includes(field) && permissions.edit.ok
  })
}

/** Task detail (§13.3.1): header, blockers, fields grouped by purpose (people, details, dates, progress and hours), description,
 *  dependencies, manual block, links, comments and history. Every field saves on its own and says so (design §8). */
export function TaskDetail({ id, onClose, heading: Heading = 'h2' }: { id: string; onClose?: () => void; heading?: 'h1' | 'h2' }) {
  const noticesOn = useFlag('capacity_screens')
  const me = useMe()
  const workScreens = useFlag('work_screens'), peopleOn = useFlag('people_screens')
  const q = useQuery({ queryKey: ['task', id, me.id, workScreens, peopleOn], queryFn: async () => await readTask(id) as TaskDetailData & WorkSeenFields })
  const contextOn = useFlag('screen_patterns')
  const pickupEnabled = useFlag('task_pickup')
  const pickup = useQuery({ queryKey: ['pickup-offers', id], enabled: pickupEnabled, queryFn: () => typedRequest('get', '/api/v1/tasks/{id}/pickup-offer', { params: { id } }) })
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  const openPanel = useItemPanel()
  const location = useLocation()
  const project = useProject(q.data?.project.projectNumber)
  const lists = useProjectLists(q.data?.project.id)
  const [sectionParams, setSectionParams] = useSearchParams()
  const [tab, setTab] = useRecordTab('Task', id, (peopleOn || workScreens) && sectionParams.get('taskSection') === 'history' ? 'history' : ItemSlots.Comments ? 'comments' : 'history', [...(ItemSlots.Comments ? ['comments'] : []), 'history'])
  const changeTab = (value: string) => {
    setTab(value)
    if ((workScreens || peopleOn) && sectionParams.get('panel') === `Task:${id}` && sectionParams.has('taskSection')) {
      // One scoped replacement also clears an explicit CommentCount target on a manual tab change.
      const next = new URLSearchParams(sectionParams); next.delete('taskSection')
      next.set('recordTab', value); next.set('recordTabItem', `Task:${id}`)
      setSectionParams(next, { replace: true, state: location.state })
    }
  }
  const [blocking, setBlocking] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const [offerOpen, setOfferOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [keepWatching, setKeepWatching] = useState(true)
  const [assignTo, setAssignTo] = useState('')
  const [pickupHours, setPickupHours] = useState(''), [pickupNeededBy, setPickupNeededBy] = useState(''), [pickupNote, setPickupNote] = useState('')
  const reload = () => refresh(q.data?.project.id) // the project refresh reaches ['task', id] through keys.ts
  const small = useFlag('small_ux')
  const [creatingDeliverable, setCreatingDeliverable] = useState<TaskRow | null>(null)
  const actions = useTaskActions(reload)
  // The full page can remain mounted beneath its own Task inspector; only the current owner is exposed.
  const detailTarget = Heading === 'h1'
    ? location.pathname.replace(/\/$/, '') === `/tasks/${id}` && !sectionParams.has('panel')
    : sectionParams.get('panel') === `Task:${id}`
  const currentTaskAvailable = !!q.data && !q.isError && detailTarget
  const currentActionAllowed = !actions.pendingAction || !!q.data && taskActionAllowed(q.data, actions.pendingAction)
  const currentBlockAllowed = q.data?.permissions.block.ok === true
  const currentDeleteAllowed = q.data?.permissions.delete.ok === true
  const currentCreateAllowed = !!creatingDeliverable && !project.isError && project.data?.permissions.createDeliverableIn.includes(creatingDeliverable.projectDisciplineId) === true
  const currentTerminal = q.data?.task.status === 'Complete' || q.data?.task.status === 'Cancelled'
  const currentPickupOfferAllowed = pickupEnabled && q.data?.permissions.offerPickup === true && !currentTerminal && pickup.isSuccess && !pickup.data
  const visibleDialog = workScreens ? currentTaskAvailable && (blocking && currentBlockAllowed || deleting && currentDeleteAllowed
    || !!creatingDeliverable && currentCreateAllowed || offerOpen && currentPickupOfferAllowed || actions.dialogOpen && currentActionAllowed)
    : blocking || deleting || !!creatingDeliverable || offerOpen || actions.dialogOpen
  const seenOwner = useWorkSeenDetailOwner({ itemType: 'Task', itemId: q.data?.task.id ?? id,
    sourceVersion: q.data?.task.rowVersion, seen: q.data?.seen, read: q,
    visible: detailTarget && (!workScreens || currentTaskAvailable) && !visibleDialog })
  if (q.isPending) return <Loading />
  if (q.error && (!workScreens || !q.data)) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  if (!q.data) return <Loading />
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
  const openOffer = pickup.data
  const offer = async () => { await typedRequest('post', '/api/v1/tasks/{id}/pickup-offer', { params: { id }, body: { reason, keepWatching, hoursRemaining: pickupHours === '' ? null : Number(pickupHours), neededBy: pickupNeededBy || null, note: pickupNote || null } }); setOfferOpen(false); setReason(''); pickup.refetch(); reload() }
  const pickUp = async () => { await typedRequest('post', '/api/v1/tasks/{id}/pickup', { params: { id }, body: { rowVersion: openOffer?.rowVersion ?? null } }); invalidateChange(qc, 'TaskPickupOffer'); reload() }
  const withdraw = async () => { await typedRequest('delete', '/api/v1/tasks/{id}/pickup-offer', { params: { id } }); pickup.refetch(); reload() }
  const assign = async () => { if (!assignTo) return; await typedRequest('post', '/api/v1/tasks/{id}/pickup-offer/assign', { params: { id }, body: { userId: assignTo, rowVersion: openOffer?.rowVersion ?? null } }); setAssignTo(''); pickup.refetch(); reload() }

  return (
    <PickerProject.Provider value={r.projectId}><FormNavigationScope preservesNavigation={workScreens ? preservesTaskDetailNavigation(r.id, id) : undefined} available={workScreens ? currentTaskAvailable : undefined}><div>
      {workScreens && q.error && <div className="space-y-3 p-4" data-task-detail-error={r.id}><ErrorBanner error={q.error} /><Button size="sm" variant="outline" disabled={q.isFetching} onClick={() => { void q.refetch() }}>{q.isFetching && <Spinner />}{runtimeText('taskContribution.checkCurrent')}</Button></div>}
      <div ref={seenOwner.surfaceRef} data-task-detail-body={r.id} hidden={workScreens && q.isError} inert={workScreens && q.isError ? true : undefined} aria-hidden={workScreens && q.isError ? true : undefined}>
      <WorkSeenDetailNotice state={seenOwner.state} />
      {workScreens && <TaskSectionNavigation taskId={r.id} surface={seenOwner.surfaceRef} owner={detailTarget} inspector={Heading !== 'h1'} paused={visibleDialog} unavailable={[...(!ItemSlots.Comments ? ['comments' as const] : []), ...(!ItemSlots.Links ? ['links' as const] : [])]} />}
      <TaskSection taskId={r.id} name="details" enabled={workScreens}>
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
          {pickupEnabled && openOffer && <span className="rounded-md border border-warn/40 bg-warn-bg px-2 py-0.5 text-xs font-medium text-warn">{t('pickup.title')}</span>}
          <span className="inline-flex items-center gap-1.5 text-xs/[18px] text-muted-foreground"><AccentDot id={d.project.id} />{d.project.projectNumber}</span>
        </div>
        <Heading aria-label={r.name} className="-mx-2 break-words text-2xl/8 font-semibold tracking-[-0.4px]"><InlineText value={r.name} disabled={!perms.edit.ok} needsReason={perms.needsReason} title={perms.edit.ok ? t('common.name') : perms.edit.reason ?? undefined} onSave={(v) => save({ name: v })} /></Heading>
        {contextOn && d.context && <RecordContext context={d.inputEffects && (s?.isWaiting || s?.isBlocked) ? { ...d.context, inputs: [], unavailableInputCount: null } : d.context} projectNumber={d.project.projectNumber} meId={me.id} openPanel={openPanel} />}
        <TaskIndicators r={r} all />
        {d.relatedDeliverables?.length ? <p className="text-sm">{t('interaction.relatedDeliverables')}: {d.relatedDeliverables.map(x=><button className="ml-2 underline underline-offset-2" key={x.id} onClick={e=>openPanel('Deliverable',x.id,contextOn ? captureRecordContextOrigin(location,e.currentTarget) : undefined)}>{x.key} {x.name}</button>)}</p> : null}
        <div className="flex flex-wrap items-center gap-2">
          {perms.isReviewer && r.status === 'Ready for Review' && find('In Review') && <Button size="sm" onClick={() => act('In Review')}>{t('task.startReview')}</Button>}
          {perms.isReviewer && r.status === 'In Review' && find('Complete') && <Button size="sm" onClick={() => act('Complete')}><Check className="size-4" aria-hidden />{t('task.approve')}</Button>}
          {perms.isReviewer && r.status === 'In Review' && find('Revision Required') && <Button size="sm" variant="outline" onClick={() => act('Revision Required')}>{t('task.requestRevision')}</Button>}
          {small && project.data?.permissions.createDeliverableIn.includes(r.projectDisciplineId) && <Button size="sm" variant="outline" onClick={()=>setCreatingDeliverable({ ...r })}>{t('interaction.deliverableFromTask')}</Button>}
          {perms.edit.ok && <Button size="sm" variant="outline" asChild><Link to={`/projects/${d.project.projectNumber}/handoffs?targetTask=${r.id}`}>{t('handoff.new')}</Link></Button>}
          <Button size="sm" variant="outline" asChild><Link to={`/projects/${d.project.projectNumber}/changes?target=Task:${r.id}`}>{t('change.inputs')}</Link></Button>
          {perms.authoriseStart && <Button size="sm" variant="outline" onClick={() => actions.authoriseStart(r)}>{t('tasks.startAuthAuthorise')}</Button>}
          {pickupEnabled && d.permissions.offerPickup && !terminal && !openOffer && <Button size="sm" variant="outline" onClick={() => setOfferOpen(true)}>{t('pickup.offer')}</Button>}
          {pickupEnabled && openOffer?.allowed?.withdraw && <Button size="sm" variant="outline" onClick={() => run(withdraw)}>{t('pickup.withdraw')}</Button>}
          {pickupEnabled && openOffer?.allowed?.pickUp && <Button size="sm" variant="outline" onClick={() => run(pickUp)}>{t('pickup.pick')}</Button>}
          {pickupEnabled && openOffer?.allowed?.assign && <><select className="h-(--control-row-h) rounded-md border bg-card px-2 text-sm" aria-label={t('pickup.assignOffer')} value={assignTo} onChange={(e) => setAssignTo(e.target.value)}><option value="">{t('pickup.assignTo')}</option>{(openOffer.candidates ?? []).map((c) => <option key={c.id} value={c.id}>{personOptionLabel(c.displayName ?? t('common.unavailable'), c.personNotices, noticesOn)}</option>)}</select><Button size="sm" disabled={!assignTo} onClick={() => run(assign)}>{t('pickup.assign')}</Button></>}
          <RaiseSlot projectId={r.projectId} targetType="Task" targetId={r.id} targetKey={r.key} targetName={r.name} disciplineId={r.projectDisciplineId} />
        </div>
      </div>
      <FormNavigationScope available={workScreens ? currentPickupOfferAllowed : undefined}><Dialog open={offerOpen} onOpenChange={setOfferOpen}>
        <DialogContent><DialogHeader><DialogTitle>{t('pickup.offerTitle', { key: r.key })}</DialogTitle><DialogDescription>{t('pickup.offerHint')}</DialogDescription></DialogHeader>
          <Field label={t('common.reason')} htmlFor="pickup-reason"><Textarea id="pickup-reason" rows={3} maxLength={2000} value={reason} onChange={(e) => setReason(e.target.value)} required /></Field>
          <div className="grid gap-3 sm:grid-cols-2"><Field label={t('pickup.hoursRemaining')} htmlFor="pickup-hours" optional><Input id="pickup-hours" type="number" min="0" max="99999.99" step="0.01" value={pickupHours} onChange={e => setPickupHours(e.target.value)} /></Field><Field label={t('pickup.neededBy')} htmlFor="pickup-needed-by" optional><Input id="pickup-needed-by" type="date" value={pickupNeededBy} onChange={e => setPickupNeededBy(e.target.value)} /></Field></div>
          <Field label={t('common.notes')} htmlFor="pickup-note" optional><Textarea id="pickup-note" maxLength={4000} value={pickupNote} onChange={e => setPickupNote(e.target.value)} /></Field>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={keepWatching} onCheckedChange={(v) => setKeepWatching(!!v)} /> {t('pickup.keepWatching')}</label>
          <DialogFooter><Button variant="outline" onClick={() => setOfferOpen(false)}>{t('common.cancel')}</Button><Button disabled={!reason.trim()} onClick={() => run(offer)}>{t('pickup.offer')}</Button></DialogFooter>
        </DialogContent>
      </Dialog></FormNavigationScope>
      {!perms.edit.ok && perms.edit.reason && <div className="px-4 pb-4"><Notice icon={Lock} title={t('task.readOnlyTitle')}>{perms.edit.reason}</Notice></div>}
      {err != null && <div className="px-4 pb-4"><ErrorBanner error={err} retry={() => { setErr(null); reload() }} /></div>}

      {s && (s.isWaiting || s.isBlocked) && (
        contextOn && d.context && d.inputEffects ? <WorkWaitingInputs context={d.context} effects={d.inputEffects} manualType={r.manualBlockType} manualReason={r.manualBlockReason} number={d.project.projectNumber} meId={me.id}>
          <p className={cn('text-xs', s.isBlocked ? 'text-bad' : 'text-warn')}>{s.isBlocked ? (s.daysBlocked > 0 ? t('task.blockedFor', { n: s.daysBlocked }) : t('task.blockedToday')) : t('task.waitingOn')}</p>
          {r.status === 'Not Started' && <p className="text-xs text-muted-foreground">{t('task.startAnyway')}</p>}
          {s.affectedMilestoneIds.length > 0 && <p className="text-xs">{t('task.affects')} {s.affectedMilestoneIds.map(mid => milestoneName(mid)).filter(Boolean).map(m => `${m!.key} ${m!.name} (${fmtDate(m!.date)})`).join(', ')}</p>}
        </WorkWaitingInputs> :
        <div role="status" className={cn('mx-4 mb-4 space-y-2 rounded-lg border px-4 py-3 text-sm', s.isBlocked ? 'border-bad/30 bg-bad-bg' : 'border-warn/40 bg-warn-bg')}>
          <p className={cn('font-semibold', s.isBlocked ? 'text-bad' : 'text-warn')}>
            <span aria-hidden>{s.isBlocked ? '■ ' : '▲ '}</span>{s.isBlocked ? (s.daysBlocked > 0 ? t('task.blockedFor', { n: s.daysBlocked }) : t('task.blockedToday')) : t('task.waitingOn')}
          </p>
          <ul className="space-y-1.5">{blockersOf(s).map((b, i) => <BlockerLine key={i} b={b} open={openPanel} />)}</ul>
          {r.status === 'Not Started' && <p className="text-xs/[18px] text-muted-foreground">{t('task.startAnyway')}</p>}
          {s.affectedMilestoneIds.length > 0 && (
            <p className="text-xs/[18px]">{t('task.affects')} {s.affectedMilestoneIds.map((mid) => milestoneName(mid)).filter(Boolean).map((m) => `${m!.key} ${m!.name} (${fmtDate(m!.date)})`).join(', ')}</p>
          )}
        </div>
      )}
      {contextOn && d.readinessInputs && <WorkReadinessFacts readiness={d.readinessInputs} number={d.project.projectNumber} meId={me.id} />}
      {contextOn && d.sourceUses && d.sourceUses.length > 0 && <WorkSourceUses sources={d.sourceUses} number={d.project.projectNumber} />}

      <FieldGroup title={t('task.people')}>
        {/* FR-PICK-02: the owner picker shows free hours for the week the task starts, else is due, else this week. */}
        <CapacityWeek value={weekOf(r.startDate ?? r.dueDate ?? today())}><FieldRow label={t(contextOn ? 'workReview.performer' : 'field.AssigneeId')}><InlinePerson focusKey={`task:${r.id}:assignee:detail`} value={r.assigneeId} name={r.assigneeName && !r.assigneeActive ? t('common.inactiveSuffix', { name: r.assigneeName }) : r.assigneeName} disabled={!perms.assign.ok} title={perms.assign.reason ?? undefined} onSave={(v) => save({ assigneeId: v })} /></FieldRow></CapacityWeek>
        {!workScreens && <><FieldRow label={t(contextOn && !perms.allowSelfReview ? 'workReview.reviewer' : 'field.ReviewerId')}>
          <InlinePerson focusKey={`task:${r.id}:reviewer:detail`} value={r.reviewerId} name={r.reviewerName} disabled={!perms.setReviewer.ok} title={perms.setReviewer.reason ?? undefined} exclude={contextOn && !perms.allowSelfReview && r.assigneeId ? [r.assigneeId] : undefined} onSave={(v) => save({ reviewerId: v })} />
          {contextOn && !perms.allowSelfReview && <p className="mt-1 text-xs text-muted-foreground">{t('workReview.independentHint')}</p>}
          {contextOn && perms.allowSelfReview && r.assigneeId && r.reviewerId === r.assigneeId && <p className="mt-1 text-xs text-muted-foreground">{t('workReview.selfAllowed')}</p>}
        </FieldRow>
        <FieldRow label={t('field.RequiresReview')}>
          <SaveStatus current={r.requiresReview}>{(track) => (
            <div className="flex min-h-(--control-row-h) items-center px-2"><Checkbox checked={r.requiresReview} disabled={!perms.edit.ok} onCheckedChange={(c) => track(save({ requiresReview: !!c }), !!c)} aria-label={t('field.RequiresReview')} /></div>
          )}</SaveStatus>
        </FieldRow></>}
        <People d={d} me={me.id} canManage={perms.assign.ok} onChanged={reload} onError={setErr} contribution={workScreens} currentAvailable={!q.isError} readCurrent={async () => { const read = await q.refetch({ throwOnError: true }); if (!read.data) throw new Error(t('common.unavailable')); return read.data }} />
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
        <FieldRow label={t('field.StartDate')}><InlineDate hint={(draft) => <DateHint type="Task" id={r.id} startDate={draft} dueDate={r.dueDate} />} value={r.startDate} disabled={!perms.edit.ok} onSave={(v) => save({ startDate: v })} /></FieldRow>
        <FieldRow label={t('field.DueDate')}>
          <InlineDate hint={(draft) => <DateHint type="Task" id={r.id} startDate={r.startDate} dueDate={draft} />} value={r.dueDate} disabled={!perms.dueDate.ok} title={perms.dueDate.reason ?? (perms.dueNeedsReason ? t('task.dueReasonHint') : undefined)} onSave={(v) => save({ dueDate: v })} />
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
        {!workScreens && <><FieldRow label={t('task.estimateHours')}><InlineText value={r.estimatedHours == null ? '' : String(r.estimatedHours)} disabled={!perms.edit.ok} needsReason={perms.needsReason}
          onSave={(v) => save({ estimatedHours: v == null || v === '' ? null : Number(v) })} /></FieldRow>
        {TASK_SECTIONS.map((x) => <div key={x.id}>{x.render(d, reload)}</div>)}</>}
      </FieldGroup>

      <FieldGroup title={t('common.description')}>
        {/* The section title is the visible label; the field keeps its own name for the editor. */}
        <FieldRow label={<span className="sr-only">{t('common.description')}</span>} name={t('common.description')} className="gap-y-0 py-0 sm:grid-cols-1">
          <div className="-mx-2"><InlineText value={d.description} multiline disabled={!perms.edit.ok} needsReason={perms.needsReason} placeholder={t('task.noDescription')}
            render={(v) => <RichText text={v} className="whitespace-normal" />} onSave={(v) => save({ description: v })} /></div>
        </FieldRow>
      </FieldGroup>

      {!workScreens && <Dependencies task={r} canManage={perms.dependencies} onChanged={reload} />}

      <FieldGroup title={t('task.manualBlock')}>
        {r.manualBlockType ? (
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Chip tone="bad">{tv(r.manualBlockType)}</Chip>
            <span className="min-w-0 flex-1 break-words">{r.manualBlockReason}</span>
            <span className="text-xs/[18px] text-muted-foreground tabular-nums">{t('task.blockSince', { when: fmtTime(r.manualBlockSetAt) })}</span>
            {perms.block.ok && <Button size="sm" variant="outline" onClick={() => run(() => typedRequest('post', '/api/v1/tasks/{id}/unblock', { params: { id: r.id }, body: {}, ifMatch: r.rowVersion }))}>{t('task.clearBlock')}</Button>}
          </div>
        ) : perms.block.ok ? <Button size="sm" variant="outline" onClick={() => setBlocking(true)}><Ban className="size-4" aria-hidden />{t('task.setBlock')}</Button>
          : <p className="text-sm text-muted-foreground">{t('common.none')}</p>}
      </FieldGroup>

      </TaskSection>
      {workScreens && <>
        <TaskSection taskId={r.id} name="dependencies" enabled><Dependencies task={r} canManage={perms.dependencies} onChanged={reload} /></TaskSection>
        <TaskSection taskId={r.id} name="review" enabled><FieldGroup title={runtimeText('taskSections.review')}>
          <FieldRow label={t(contextOn && !perms.allowSelfReview ? 'workReview.reviewer' : 'field.ReviewerId')}>
            <InlinePerson focusKey={`task:${r.id}:reviewer:detail`} value={r.reviewerId} name={r.reviewerName} disabled={!perms.setReviewer.ok} title={perms.setReviewer.reason ?? undefined} exclude={contextOn && !perms.allowSelfReview && r.assigneeId ? [r.assigneeId] : undefined} onSave={(v) => save({ reviewerId: v })} />
            {contextOn && !perms.allowSelfReview && <p className="mt-1 text-xs text-muted-foreground">{t('workReview.independentHint')}</p>}
            {contextOn && perms.allowSelfReview && r.assigneeId && r.reviewerId === r.assigneeId && <p className="mt-1 text-xs text-muted-foreground">{t('workReview.selfAllowed')}</p>}
          </FieldRow>
          <FieldRow label={t('field.RequiresReview')}><SaveStatus current={r.requiresReview}>{track => <div className="flex min-h-(--control-row-h) items-center px-2"><Checkbox checked={r.requiresReview} disabled={!perms.edit.ok} onCheckedChange={checked => track(save({ requiresReview: !!checked }), !!checked)} aria-label={t('field.RequiresReview')} /></div>}</SaveStatus></FieldRow>
        </FieldGroup></TaskSection>
        <TaskSection taskId={r.id} name="time" enabled><FieldGroup title={runtimeText('taskSections.time')}>
          <FieldRow label={t('task.estimateHours')}><InlineText value={r.estimatedHours == null ? '' : String(r.estimatedHours)} disabled={!perms.edit.ok} needsReason={perms.needsReason} onSave={v => save({ estimatedHours: v == null || v === '' ? null : Number(v) })} /></FieldRow>
          <Link className="inline-flex min-h-9 items-center text-sm text-primary underline underline-offset-2" to={`/time?taskId=${encodeURIComponent(r.id)}`}>{runtimeText('taskSections.timeLink')}</Link>
          {TASK_SECTIONS.map(extension => <div key={extension.id}>{extension.render(d, reload)}</div>)}
        </FieldGroup></TaskSection>
        <TaskSection taskId={r.id} name="comments" enabled><FieldGroup title={t('common.comments')}>{ItemSlots.Comments ? <div data-work-comments-section={r.id}><CommentsSlot type="Task" id={r.id} projectId={r.projectId} /></div> : <p className="text-sm text-muted-foreground">{runtimeText('taskSections.unavailable', { section: t('common.comments') })}</p>}</FieldGroup></TaskSection>
        <TaskSection taskId={r.id} name="links" enabled><FieldGroup title={t('common.links')}>{ItemSlots.Links ? <LinksSlot type="Task" id={r.id} projectId={r.projectId} inheritedFrom={r.deliverableId ?? undefined} /> : <p className="text-sm text-muted-foreground">{runtimeText('taskSections.unavailable', { section: t('common.links') })}</p>}</FieldGroup></TaskSection>
        <TaskSection taskId={r.id} name="history" enabled><FieldGroup title={t('common.history')}><HistoryList type="Task" id={r.id} projectId={r.projectId} taskActorFilter /></FieldGroup></TaskSection>
      </>}

      {!workScreens && ItemSlots.Links && <FieldGroup title={t('common.links')}><LinksSlot type="Task" id={r.id} projectId={r.projectId} inheritedFrom={r.deliverableId ?? undefined} /></FieldGroup>}

      {!workScreens && <><div className="border-t">
        <TabBar tabs={[...(ItemSlots.Comments ? [{ id: 'comments', label: t('common.comments'), count: r.commentCount }] : []), { id: 'history', label: t('common.history') }]} value={tab} onChange={changeTab} />
      </div>
      {tab === 'comments' && <CommentsSlot type="Task" id={r.id} projectId={r.projectId} />}
      {tab === 'history' && <HistoryList type="Task" id={r.id} />}</>}

      {perms.delete.ok && (
        <div className="flex justify-end border-t p-4">
          <Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => setDeleting(true)}><Trash2 className="size-4" aria-hidden />{t('task.delete')}</Button>
        </div>
      )}
      {blocking && <FormNavigationScope available={workScreens ? currentBlockAllowed : undefined}><BlockDialog r={r} onClose={(ok) => { setBlocking(false); if (ok) reload() }} /></FormNavigationScope>}
      {deleting && <FormNavigationScope available={workScreens ? currentDeleteAllowed : undefined}><DeleteDialog d={d} onMove={(m) => { setDeleting(false); actions.move(m) }} onClose={(gone) => { setDeleting(false); if (gone) { refresh(r.projectId); onClose?.() } }} /></FormNavigationScope>}
      {creatingDeliverable && project.data && <FormNavigationScope available={workScreens ? currentCreateAllowed : undefined}><CreateDeliverable p={project.data} milestones={lists.milestones} fromTask={{ ...creatingDeliverable, milestoneId: creatingDeliverable.milestoneId ?? undefined, startDate: creatingDeliverable.startDate ?? undefined, dueDate: creatingDeliverable.dueDate ?? undefined }} onClose={()=>{setCreatingDeliverable(null);reload()}} /></FormNavigationScope>}
      <FormNavigationScope available={workScreens ? currentActionAllowed : undefined}>{actions.dialogs}</FormNavigationScope>
    </div></div></FormNavigationScope></PickerProject.Provider>
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

type Edge = components['schemas']['TaskDependencyEdge']
/** The chain's own shape comes from the contract; its task rows keep the page type of the file that owns them until that conversion lands. */
interface Chain extends Omit<components['schemas']['TaskChain'], 'task' | 'predecessors' | 'successors'> {
  task: TaskRow; predecessors: { level: number; task: TaskRow }[]; successors: { level: number; task: TaskRow }[]
}

function Dependencies({ task, canManage, onChanged }: { task: TaskRow; canManage: boolean; onChanged: () => void }) {
  const q = useQuery({ queryKey: ['task', task.id, 'deps', task.rowVersion], queryFn: () => typedRequest('get', '/api/v1/tasks/{id}/dependencies', { params: { id: task.id } }) })
  const openPanel = useItemPanel()
  const contextOn = useFlag('screen_patterns')
  const location = useLocation()
  const [adding, setAdding] = useState<'predecessor' | 'successor' | null>(null)
  const [chain, setChain] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const remove = async (e: Edge) => {
    setErr(null)
    try { await typedRequest('delete', '/api/v1/dependencies/{id}', { params: { id: e.dependencyId } }); q.refetch(); onChanged() } catch (x) { setErr(x) }
  }
  const list = (edges: Edge[], empty: string) => edges.length === 0 ? <p className="text-sm text-muted-foreground">{empty}</p> : (
    <ul className="divide-y rounded-md border">
      {edges.map((e) => (
        <li key={e.dependencyId} className="flex flex-wrap items-center gap-x-2 gap-y-1 px-3 py-1.5 text-sm">
          {e.satisfied ? <Check className="size-4 shrink-0 text-done" role="img" aria-label={t('task.satisfied')} /> : <Link2 className="size-4 shrink-0 text-muted-foreground" aria-hidden />}
          <button type="button" className="min-w-48 flex-1 break-words text-left hover:underline" onClick={event => openPanel('Task', e.task.id, contextOn ? captureRecordContextOrigin(location, event.currentTarget) : undefined)}><Key>{e.task.key}</Key> {e.task.name}</button>
          {e.task.overdue && <Chip tone="bad">{t('ind.overdue')}</Chip>}
          <DepTypeChip type={e.type} lag={e.lagDays} />
          <span className="hidden text-xs/[18px] text-muted-foreground sm:inline">{e.task.assigneeId && e.task.assigneeName ? <PersonPanelTrigger userId={e.task.assigneeId} name={e.task.assigneeName} focusKey={`task:${task.id}:dependency:${e.dependencyId}:assignee`} /> : e.task.assigneeName ?? t('ind.unassigned')} · <span className="tabular-nums">{fmtDate(e.task.dueDate)}</span></span>
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
  const [type, setType] = useState('FinishToStart')
  const [choice, setChoice] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const q = useQuery({ queryKey: ['task', task.id, 'candidates', direction, term], queryFn: () => typedRequest('get', '/api/v1/tasks/{id}/dependency-candidates', { params: { id: task.id }, query: { q: term, direction } }) })
  const save = async () => {
    setErr(null); setBusy(true)
    try {
      await typedRequest('post', '/api/v1/tasks/{id}/dependencies', { params: { id: task.id }, body: {
        predecessorTaskId: direction === 'predecessor' ? choice : null, successorTaskId: direction === 'successor' ? choice : null, note: note || null, lagDays: Number(lag) || 0, type } })
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
          <DialogTitle>{direction === 'predecessor' ? t('dep.addPredecessorTitle', { key: task.key }) : t('dep.addSuccessorTitle', { key: task.key })}</DialogTitle>
          <DialogDescription>{t('dep.dialogHint')}</DialogDescription>
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
        <DepTypeField id="dep-type" value={type} onChange={setType} />
        <Field label={t('dep.lag')} htmlFor="dep-lag" hint={t('dep.lagHintTyped')}><Input id="dep-lag" type="number" min={0} max={365} className="w-28" value={lag} onChange={(e) => setLag(e.target.value)} /></Field>
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
  const q = useQuery({ queryKey: ['task', task.id, 'chain'], queryFn: async () => (await typedRequest('get', '/api/v1/tasks/{id}/chain', { params: { id: task.id } })) as unknown as Chain })
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

function People({ d, me, canManage, onChanged, onError, contribution, currentAvailable, readCurrent }: { d: TaskDetailData; me: string; canManage: boolean; onChanged: () => void; onError: (e: unknown) => void; contribution: boolean; currentAvailable: boolean; readCurrent: () => Promise<TaskDetailData> }) {
  const r = d.task
  const canCollab = canManage || r.assigneeId === me
  const watching = d.watchers.some((w) => w.id === me)
  const ownCollaborator = d.collaborators.find(person => person.id === me)
  const run = async (f: () => Promise<unknown>) => { try { await f(); onChanged() } catch (e) { onError(e) } }
  const chip = (id: string, label: string, name: string, role: 'collaborator' | 'watcher', remove?: () => void) => (
    <span key={id} className="inline-flex min-h-7 items-center gap-1.5 rounded-md border bg-card py-0.5 pl-0.5 pr-2 text-sm">
      <Avatar id={id} name={name} className="size-6" /><PersonPanelTrigger userId={id} name={name} focusKey={`task:${r.id}:${role}:${id}`}>{label}</PersonPanelTrigger>
      {remove && <button type="button" className="-mr-1 grid size-6 place-items-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground" aria-label={t('task.removeCollaborator', { name })} onClick={remove}><X className="size-3.5" /></button>}
    </span>
  )
  return (
    <>
      <FieldRow label={t('task.collaborators')}>
        <div className="flex min-h-(--control-row-h) flex-wrap items-center gap-1.5 px-2 py-1">
          {d.collaborators.length === 0 && <span className="text-muted-foreground">{t('common.none')}</span>}
          {d.collaborators.filter(person => !contribution || person.id !== me).map((c) => <div key={c.id} className={contribution ? 'w-full min-w-0 space-y-2 rounded-md border p-2' : 'contents'}>
            {chip(c.id, c.isActive ? c.displayName : t('common.inactiveSuffix', { name: c.displayName }), c.displayName, 'collaborator', canCollab || c.id === me ? () => run(() => typedRequest('delete', '/api/v1/tasks/{id}/collaborators/{userId}', { params: { id: r.id, userId: c.id } })) : undefined)}
            {contribution && <TaskContribution taskId={r.id} userId={c.id} value={c.contribution} currentAvailable={currentAvailable} needsReason={d.permissions.needsReason} readCurrent={readCurrent} onChanged={onChanged} />}
          </div>)}
          {contribution && <div className={ownCollaborator ? 'w-full min-w-0 space-y-2 rounded-md border p-2' : 'contents'}>
            {ownCollaborator && chip(ownCollaborator.id, ownCollaborator.isActive ? ownCollaborator.displayName : t('common.inactiveSuffix', { name: ownCollaborator.displayName }), ownCollaborator.displayName, 'collaborator', () => run(() => typedRequest('delete', '/api/v1/tasks/{id}/collaborators/{userId}', { params: { id: r.id, userId: ownCollaborator.id } })))}
            <TaskContribution taskId={r.id} userId={me} value={ownCollaborator?.contribution} memberPresent={!!ownCollaborator} currentAvailable={currentAvailable} needsReason={d.permissions.needsReason} readCurrent={readCurrent} onChanged={onChanged} />
          </div>}
          {canCollab && <div className="w-48"><PeoplePicker compact value={null} placeholder={t('task.addCollaborator')} label={t('task.addCollaborator')}
            exclude={[...d.collaborators.map((c) => c.id), ...(r.assigneeId ? [r.assigneeId] : [])]} onChange={(id) => id && run(() => typedRequest('post', '/api/v1/tasks/{id}/collaborators', { params: { id: r.id }, body: { userId: id } }))} /></div>}
        </div>
        <p className="px-2 text-xs/[18px] text-muted-foreground">{t('task.collaboratorHint')}</p>
      </FieldRow>
      <FieldRow label={t('task.watchers')}>
        <div className="flex min-h-(--control-row-h) flex-wrap items-center gap-1.5 px-2 py-1">
          {d.watchers.length === 0 ? <span className="text-muted-foreground">{t('common.none')}</span> : d.watchers.map((w) => chip(w.id, w.displayName, w.displayName, 'watcher'))}
          <Button size="sm" variant="ghost" onClick={() => run(() => watching ? typedRequest('delete', '/api/v1/items/{type}/{id}/watchers/{userId}', { params: { type: 'Task', id: r.id, userId: me } }) : typedRequest('post', '/api/v1/items/{type}/{id}/watchers', { params: { type: 'Task', id: r.id }, body: { userId: me } }))}>
            {watching ? <><EyeOff className="size-4" aria-hidden />{t('task.unwatch')}</> : <><Eye className="size-4" aria-hidden />{t('task.watch')}</>}
          </Button>
        </div>
      </FieldRow>
    </>
  )
}

export function BlockDialog({ r, onClose }: { r: TaskRow; onClose: (ok: boolean) => void }) {
  const workOn = useFlag('work_screens'), captured = useRef({ ...r }).current
  const task = workOn ? captured : r
  const [type, setType] = useState('Client')
  const [reason, setReason] = useState('')
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('task.setBlockTitle', { key: task.key })} body={t('task.setBlockBody')} busy={!reason.trim()} confirmLabel={t('task.setBlock')}
      onConfirm={async () => { await typedRequest('post', '/api/v1/tasks/{id}/block', { params: { id: task.id }, body: { type, reason, rowVersion: task.rowVersion } }); onClose(true) }}>
      <Field label={t('common.type')} htmlFor="blk-type"><select id="blk-type" className={selectCls} value={type} onChange={(e) => setType(e.target.value)}>{BLOCK_TYPES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select></Field>
      <Field label={t('common.reason')} htmlFor="blk-reason"><Textarea id="blk-reason" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
    </ConfirmDialog>
  )
}

/** Delete with the dependency list and a Cancel-first suggestion once work exists (FR-016, D-09, T-09). */
function DeleteDialog({ d, onMove, onClose }: { d: TaskDetailData; onMove: (m: ReturnType<typeof moveFrom>) => void; onClose: (gone: boolean) => void }) {
  const workOn = useFlag('work_screens'), captured = useRef(d).current
  const original = workOn ? captured : d
  const r = original.task
  const q = useQuery({ queryKey: ['task', r.id, 'delete-preview'], queryFn: () => typedRequest('get', '/api/v1/tasks/{id}/delete-preview', { params: { id: r.id } }) })
  const cancel = d.permissions.transitions.find((x) => x.to === 'Cancelled' && x.allowed)
  return (
    <ConfirmDialog open destructive onOpenChange={(o) => !o && onClose(false)} title={t('task.deleteTitle', { key: r.key, name: r.name })} confirmLabel={t('common.delete')} busy={!q.data?.canDelete || workOn && (q.isError || !d.permissions.delete.ok)}
      body={q.isPending ? <Spinner /> : (!workOn || !q.isError) && q.data && <>
        <p>{t('task.deleteBody')}</p>
        {q.data.dependencies.length > 0 && <>
          <p>{t('task.deleteDeps', { n: q.data.dependencies.length })}</p>
          <ul className="list-disc pl-5">{q.data.dependencies.map((x) => <li key={x.id}>{t(x.direction === 'blocks' ? 'task.depBlocks' : 'task.depDependsOn', { key: x.key, name: x.name })}</li>)}</ul>
        </>}
        {q.data.suggestCancel && <p className="font-medium">{t('task.suggestCancel')}</p>}
      </>}
      onConfirm={async () => {
        await typedRequest('delete', '/api/v1/tasks/{id}', { params: { id: r.id } })
        toast.success(t('task.deleted', { key: r.key }), d.permissions.restore
          ? { action: { label: t('task.undo'), onClick: () => { typedRequest('post', '/api/v1/tasks/{id}/restore', { params: { id: r.id }, body: {} }).then(() => toast.success(t('task.restored', { key: r.key }))).catch((e) => toast.error(errorText(e))) } }, duration: 10000 }
          : undefined)
        onClose(true)
      }}>
      {(!workOn || !q.isError) && q.data?.suggestCancel && q.data.canCancel && cancel && (
        <Button variant="outline" onClick={() => onMove(moveFrom(original, cancel))}><RotateCcw className="size-4" aria-hidden />{t('task.cancelInstead')}</Button>
      )}
    </ConfirmDialog>
  )
}

export function TaskPanel({ id, onClose }: PanelProps) { return <TaskDetail id={id} onClose={onClose} /> }

/** Full-page task (§13.3.1 "open full page"). */
export function TaskPage() {
  const { id } = useParams()
  return <div className="mx-auto w-full max-w-3xl p-4 md:p-6 xl:p-8"><div className="overflow-hidden rounded-xl border bg-card"><TaskDetail key={id} id={id!} heading="h1" /></div></div>
}
