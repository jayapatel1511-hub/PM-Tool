import { useCallback, useEffect, useReducer, useRef, useState, type RefObject } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { CheckCheck, MessageSquare } from 'lucide-react'
import { api } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { ago, fmtTime } from '@/lib/format'
import { runtimeText, t } from '@/lib/i18n'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { ErrorBanner } from './common'
import { PANELS } from './panel-host'
import { PersonPanelTrigger } from './person-panel-trigger'
import { itemHref } from './search'

/** Structural wire contracts until the coordinator regenerates the additive API contracts. */
export type WorkSeenDecoration = { state: string; observedTokens: { token: string; role: string; sourceVersion: number }[] }
export type WorkActivityFact = { eventId: string | null; occurredAt: string | null; actorId: string | null; actorName: string | null; actorType: string | null; commentable: boolean; commentCount: number | null }
export type WorkSeenFields = { seen?: WorkSeenDecoration | null; workActivity?: WorkActivityFact | null }
export type WorkSeenCommandResult = { requestId: string; committedAt: string; revision: number; firstVisitAt: string | null; previousVisitAt: string | null; lastVisitAt: string | null; lastMarkAllAt: string | null }
export type WorkSeenReadState = { isSuccess: boolean; isFetchedAfterMount: boolean; isFetching: boolean; dataUpdatedAt: number; error: unknown; isPlaceholderData?: boolean }
export type WorkSeenCommitted = (result: WorkSeenCommandResult) => void | Promise<unknown>
type Status = 'idle' | 'pending' | 'success' | 'error'
type OpenedPayload = { itemType: string; itemId: string; observedTokens: string[] }
type CommandPath = '/api/v1/me/work/visits' | '/api/v1/me/work/seen-all' | '/api/v1/me/work/opened'
type Attempt = { signature: string; requestId: string; status: Status; result?: WorkSeenCommandResult; error?: unknown; controller?: AbortController; promise?: Promise<void> }
const label = (key: string, values?: Record<string, string | number>) => runtimeText('workSeen.' + key, values)
const activityLabel = (key: string, values?: Record<string, string | number>) => runtimeText('workActivity.' + key, values)
const guid = (value: unknown): value is string => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) && value !== '00000000-0000-0000-0000-000000000000'
const utc = (value: unknown): value is string => typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]00:00)$/i.test(value) && Number.isFinite(Date.parse(value))

/** A successful empty mock response must never enroll, acknowledge, or unlock Mark all. */
function commandResult(value: unknown, requestId: string, path: CommandPath): WorkSeenCommandResult {
  if (!value || typeof value !== 'object') throw new Error(label('invalidResponse'))
  const row = value as Record<string, unknown>
  const times = ['firstVisitAt', 'previousVisitAt', 'lastVisitAt', 'lastMarkAllAt'] as const
  if (!guid(row.requestId) || row.requestId.toLowerCase() !== requestId || !utc(row.committedAt)
    || !Number.isSafeInteger(row.revision) || Number(row.revision) < 1
    || times.some(key => !(key in row) || (row[key] !== null && !utc(row[key])))
    || times.some(key => typeof row[key] === 'string' && Date.parse(row[key]) > Date.parse(row.committedAt as string))
    || (path.endsWith('/visits') && (!utc(row.firstVisitAt) || !utc(row.lastVisitAt)))
    || (path.endsWith('/seen-all') && (!utc(row.firstVisitAt) || !utc(row.lastMarkAllAt)))) throw new Error(label('invalidResponse'))
  return row as WorkSeenCommandResult
}

/** One request ID belongs to one exact normalized payload. Changed tokens replace the attempt. */
function usePersonalCommand(identity: string | null, onCommitted?: WorkSeenCommitted) {
  const current = useRef<{ identity: string | null; active: boolean; attempt?: Attempt }>({ identity, active: false })
  if (current.current.identity !== identity) current.current = { identity, active: false }
  const callback = useRef(onCommitted); callback.current = onCommitted
  const [, render] = useReducer(n => n + 1, 0)
  useEffect(() => {
    const state = current.current; state.active = true
    return () => { state.active = false; state.attempt?.controller?.abort() }
  }, [identity])
  const execute = useCallback((path: CommandPath, payload: OpenedPayload | Record<string, never> = {}) => {
    const state = current.current
    if (!identity || state.identity !== identity || !state.active) return Promise.resolve()
    const signature = JSON.stringify({ path, ...payload })
    let attempt = state.attempt
    if (!attempt || attempt.signature !== signature || attempt.status === 'success') {
      attempt?.controller?.abort()
      attempt = { signature, requestId: crypto.randomUUID(), status: 'idle' }; state.attempt = attempt
    }
    if (attempt.status === 'pending') return attempt.promise ?? Promise.resolve()
    const sent = attempt, controller = new AbortController(); sent.controller = controller; sent.status = 'pending'; sent.error = undefined
    render()
    sent.promise = (async () => {
      try {
        const result = commandResult(await api<unknown>(path, { method: 'POST', body: { requestId: sent.requestId, ...payload }, signal: controller.signal }), sent.requestId, path)
        if (!state.active || current.current !== state || state.attempt !== sent) return
        sent.result = result; sent.status = 'success'; render()
        // Re-read decorated canonical rows; never clear or rewrite cached badges optimistically.
        await callback.current?.(result)
      } catch (error) {
        if (!state.active || current.current !== state || state.attempt !== sent) return
        sent.error = error; sent.status = 'error'; render()
      }
    })()
    return sent.promise
  }, [identity])
  return { execute, attempt: current.current.attempt }
}

function visibleSurface(ref: RefObject<HTMLElement | null>) {
  const node = ref.current
  if (document.visibilityState !== 'visible' || !node?.isConnected || node.closest('[hidden],[aria-hidden="true"],[inert],[data-state="closed"]')) return false
  const rect = node.getBoundingClientRect(), style = getComputedStyle(node)
  return rect.width > 0 && rect.height > 0 && rect.bottom > 0 && rect.right > 0 && rect.top < innerHeight && rect.left < innerWidth && style.display !== 'none' && style.visibility !== 'hidden'
}
function fresh(read: WorkSeenReadState, startedAt: number) {
  return read.isSuccess && read.isFetchedAfterMount && !read.isFetching && !read.error && !read.isPlaceholderData
    && Number.isFinite(read.dataUpdatedAt) && read.dataUpdatedAt > 0 && read.dataUpdatedAt >= startedAt
}
export function useWorkDocumentVisible() {
  const [visible, setVisible] = useState(() => typeof document !== 'undefined' && document.visibilityState === 'visible')
  useEffect(() => { const update = () => setVisible(document.visibilityState === 'visible'); document.addEventListener('visibilitychange', update); return () => document.removeEventListener('visibilitychange', update) }, [])
  return visible
}

/** Mount at the MyWork route owner, before its loading branches; tabs/filters do not create visits. */
export function useOwnWorkSeenVisit({ enabled, own, visible = true, subjectId, read, surfaceRef, onCommitted }: {
  enabled: boolean; own: boolean; visible?: boolean; subjectId?: string; read: WorkSeenReadState; surfaceRef: RefObject<HTMLElement | null>; onCommitted: WorkSeenCommitted
}) {
  const me = useMe(), workOn = useFlag('work_screens'), documentVisible = useWorkDocumentVisible()
  const admitted = enabled && workOn && own
  const activeSurface = admitted && visible && documentVisible
  const session = useRef<{ key: string; id: string; startedAt: number; visible: boolean } | null>(null)
  const key = admitted ? me.id : null
  if (!key) session.current = null
  else if (session.current?.key !== key) session.current = { key, id: crypto.randomUUID(), startedAt: Date.now(), visible: activeSurface }
  else { if (activeSurface && !session.current.visible) session.current.startedAt = Math.max(Date.now(), read.dataUpdatedAt + 1); session.current.visible = activeSurface }
  const identity = session.current?.id ?? null
  const visit = usePersonalCommand(identity, onCommitted), mark = usePersonalCommand(identity, onCommitted)
  const readyRead = activeSurface && subjectId === me.id && !!session.current && fresh(read, session.current.startedAt)
  useEffect(() => {
    if (!readyRead || !documentVisible || visit.attempt) return
    const frame = requestAnimationFrame(() => { if (visibleSurface(surfaceRef)) void visit.execute('/api/v1/me/work/visits') })
    return () => cancelAnimationFrame(frame)
  }, [identity, readyRead, documentVisible, read.dataUpdatedAt, visit.attempt, visit.execute, surfaceRef])
  const retryVisit = () => { if (readyRead && visibleSurface(surfaceRef) && visit.attempt?.status === 'error') void visit.execute('/api/v1/me/work/visits') }
  const ready = admitted && visit.attempt?.status === 'success'
  const markAll = () => { if (ready && visible && visibleSurface(surfaceRef)) void mark.execute('/api/v1/me/work/seen-all') }
  return { enabled: admitted, ready, visitStatus: visit.attempt?.status ?? 'idle' as Status, visitError: visit.attempt?.error,
    result: visit.attempt?.result, retryVisit, markAll, markStatus: mark.attempt?.status ?? 'idle' as Status, markError: mark.attempt?.error, markResult: mark.attempt?.result }
}

export function WorkSeenControls({ state }: { state: ReturnType<typeof useOwnWorkSeenVisit> | null }) {
  if (!state?.enabled) return null
  return <div className="flex min-w-0 flex-wrap items-center gap-2" data-work-seen-controls>
    <Button size="sm" variant="outline" data-work-seen-all disabled={!state.ready || state.markStatus === 'pending'} onClick={state.markAll}>
      <CheckCheck className="size-4" aria-hidden />{label(state.markStatus === 'pending' ? 'marking' : 'markAll')}
    </Button>
    {state.result && <span className="text-xs text-muted-foreground">{state.result.previousVisitAt
      ? <time dateTime={state.result.previousVisitAt} title={fmtTime(state.result.previousVisitAt)}>{label('previousVisit', { at: ago(state.result.previousVisitAt) })}</time> : label('firstVisit')}</span>}
    {state.markStatus === 'success' && state.markResult?.lastMarkAllAt && <time className="text-xs text-muted-foreground" dateTime={state.markResult.lastMarkAllAt}>{label('markedAt', { at: fmtTime(state.markResult.lastMarkAllAt) })}</time>}
    {state.visitStatus === 'error' && <ErrorBanner error={state.visitError} retry={state.retryVisit} retryLabel={label('retryVisit')} />}
    {state.markStatus === 'error' && <ErrorBanner error={state.markError} retry={state.markAll} retryLabel={label('retryMarkAll')} />}
  </div>
}

export function WorkNewBadge({ seen, className }: { seen?: WorkSeenDecoration | null; className?: string }) {
  const enabled = useFlag('work_screens')
  return enabled && seen?.state === 'New' ? <span data-work-new className={cn('rounded-sm border bg-secondary px-1.5 py-0.5 text-xs font-semibold text-secondary-foreground', className)}>{label('new')}</span> : null
}

/** Supply the actual answered tuple/version, not route IDs or a register's cached row. */
export type WorkSeenDetail = { itemType: string; itemId: string; sourceVersion: number; seen?: WorkSeenDecoration | null }
function openedPayload(itemType: string, itemId: string, detail?: WorkSeenDetail | null): OpenedPayload | null {
  if (!detail || !['Task', 'Deliverable', 'Decision', 'Action', 'Risk', 'Issue', 'Handoff', 'ChangeNotice', 'Project'].includes(itemType)
    || detail.itemType !== itemType || !guid(detail.itemId) || !guid(itemId) || detail.itemId.toLowerCase() !== itemId.toLowerCase() || !Number.isSafeInteger(detail.sourceVersion) || detail.sourceVersion < 0
    || !detail.seen || !['New', 'Unenrolled'].includes(detail.seen.state) || !Array.isArray(detail.seen.observedTokens)
    || !detail.seen.observedTokens.length || detail.seen.observedTokens.length > 32) return null
  if (detail.seen.observedTokens.some(row => !row || !guid(row.token) || typeof row.role !== 'string' || !row.role.trim() || !Number.isSafeInteger(row.sourceVersion) || row.sourceVersion < 0 || row.sourceVersion > detail.sourceVersion)) return null
  return { itemType, itemId: itemId.toLowerCase(), observedTokens: [...new Set(detail.seen.observedTokens.map(row => row.token.toLowerCase()))].sort() }
}

/** Call unconditionally at the detail owner before loading branches. Never call in a query function/prefetch. */
export function useVisibleWorkDetailSeen({ itemType, itemId, detail, read, visible, surfaceRef, onCommitted }: {
  itemType: string; itemId: string; detail?: WorkSeenDetail | null; read: WorkSeenReadState; visible: boolean; surfaceRef: RefObject<HTMLElement | null>; onCommitted: WorkSeenCommitted
}) {
  const me = useMe(), enabled = useFlag('work_screens'), documentVisible = useWorkDocumentVisible()
  const key = enabled ? me.id + ':' + itemType + ':' + itemId.toLowerCase() : null
  const activeSurface = enabled && visible && documentVisible
  const session = useRef<{ key: string; startedAt: number; visible: boolean } | null>(null)
  if (!key) session.current = null
  else if (session.current?.key !== key) session.current = { key, startedAt: Date.now(), visible: activeSurface }
  else { if (activeSurface && !session.current.visible) session.current.startedAt = Math.max(Date.now(), read.dataUpdatedAt + 1); session.current.visible = activeSurface }
  const command = usePersonalCommand(key, onCommitted)
  const payload = openedPayload(itemType, itemId, detail), signature = payload ? JSON.stringify(payload) : null
  const expected = signature ? JSON.stringify({ path: '/api/v1/me/work/opened', ...JSON.parse(signature) }) : null
  const currentAttempt = command.attempt?.signature === expected ? command.attempt : undefined
  const ready = !!key && activeSurface && !!session.current && fresh(read, session.current.startedAt)
  useEffect(() => {
    if (!ready || !documentVisible || !signature) return
    if (command.attempt?.signature === expected) return
    const frame = requestAnimationFrame(() => { if (visibleSurface(surfaceRef)) void command.execute('/api/v1/me/work/opened', JSON.parse(signature) as OpenedPayload) })
    return () => cancelAnimationFrame(frame)
  }, [key, ready, documentVisible, signature, expected, read.dataUpdatedAt, command.attempt, command.execute, surfaceRef])
  const retry = () => { if (ready && signature && visibleSurface(surfaceRef) && currentAttempt?.status === 'error') void command.execute('/api/v1/me/work/opened', JSON.parse(signature) as OpenedPayload) }
  return { status: currentAttempt?.status ?? 'idle' as Status, error: currentAttempt?.error, retry, retryLabel: label('retryOpened') }
}

function focusId(value: string) { let hash = 2166136261; for (const c of value) hash = Math.imul(hash ^ c.charCodeAt(0), 16777619); return 'work-comment-' + (hash >>> 0).toString(36) }

/** Exact page-batched event/comment facts only. This component performs no row history/comment fetch. */
export function WorkActivityComments({ itemType, itemId, itemKey, projectNumber, fact, focusKey }: {
  itemType: string; itemId: string; itemKey: string; projectNumber?: string | null; fact?: WorkActivityFact | null; focusKey: string
}) {
  const enabled = useFlag('work_screens'), me = useMe(), location = useLocation(), navigate = useNavigate()
  if (!enabled) return null
  const at = fact?.occurredAt, timeKnown = typeof at === 'string' && Number.isFinite(Date.parse(at))
  const typedUser = fact?.actorType === 'User' || fact?.actorType === 'Admin'
  const actorId = fact?.actorId
  const name = typedUser && fact?.actorName?.trim() ? fact.actorName : fact?.actorType === 'System' ? t('common.system') : activityLabel('actorUnavailable')
  const count = fact?.commentCount, countKnown = typeof count === 'number' && Number.isInteger(count) && count >= 0
  const entry = PANELS[itemType], panel = !!entry && (!entry.flag || me.flags?.[entry.flag] === true)
  const hasDestination = panel || !!projectNumber?.trim()
  const comments = fact?.commentable === false ? null : !countKnown ? <span>{activityLabel('commentsUnavailable')}</span>
    : count === 0 ? <span className="inline-flex items-center gap-1" aria-label={activityLabel('zeroComments')}><MessageSquare className="size-3.5" aria-hidden /><span className="tabular-nums">0</span></span>
    : !hasDestination ? <span title={activityLabel('commentsUnavailable')} className="tabular-nums">{count}</span>
    : <button id={focusId(location.pathname + '|' + focusKey + '|' + itemType + ':' + itemId)} type="button" data-work-comments={itemType + ':' + itemId}
      className="inline-flex min-h-8 shrink-0 items-center gap-1 rounded-sm px-1 text-xs text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      aria-label={activityLabel('openComments', { key: itemKey, n: count })} onClick={event => {
        event.stopPropagation()
        const destination = panel ? new URL(location.pathname + location.search + location.hash, window.location.origin) : new URL(itemHref(itemType, projectNumber!, itemId), window.location.origin)
        if (panel) destination.searchParams.set('panel', itemType + ':' + itemId)
        if (itemType === 'Task') { destination.searchParams.set('taskSection', 'comments'); destination.searchParams.delete('recordTabItem'); destination.searchParams.delete('recordTab') }
        else { destination.searchParams.delete('taskSection'); destination.searchParams.set('recordTabItem', itemType + ':' + itemId); destination.searchParams.set('recordTab', 'comments') }
        const prior = location.state && typeof location.state === 'object' && !Array.isArray(location.state) ? location.state : {}
        navigate(destination.pathname + destination.search + destination.hash, { state: { ...prior, recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
      }}><MessageSquare className="size-3.5" aria-hidden /><span className="tabular-nums">{count}</span></button>
  return <span className="inline-flex min-w-0 flex-wrap items-center gap-x-2 text-xs text-muted-foreground" data-work-activity={itemType + ':' + itemId}>
    <span role="group" aria-label={t('task.lastActivity')} title={timeKnown ? fmtTime(at) : activityLabel('timeUnavailable')} className="inline-flex min-w-0 flex-wrap gap-x-1">
      {timeKnown ? <time dateTime={at}>{ago(at)}</time> : <span>{activityLabel('timeUnavailable')}</span>}<span aria-hidden>·</span>
      {typedUser && guid(actorId) && fact?.actorName?.trim() ? <PersonPanelTrigger userId={actorId} name={name} focusKey={focusKey + ':activity:actor'} /> : <span className="break-words">{name}</span>}
    </span>{comments}
  </span>
}
