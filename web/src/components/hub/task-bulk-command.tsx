import { useQueries, useQueryClient } from '@tanstack/react-query'
import { useEffect, useId, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { leaveQuestion } from '@/lib/drafts'
import { useUnsavedGuard } from '@/hooks/drafts'
import { invalidateChange } from '@/lib/keys'
import { runtimeText, t, tv } from '@/lib/i18n'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ErrorBanner, Field, Spinner, selectCls } from './common'
import { PeoplePicker } from './people'

type Schemas = components['schemas']
export const TASK_BULK_OPERATIONS = ['assign', 'setDueDate', 'shiftDueDates', 'setPriority', 'setDeliverable', 'hold', 'cancel'] as const
export type TaskBulkOperation = typeof TASK_BULK_OPERATIONS[number]
export type TaskBulkSelectionRow = Pick<Schemas['TaskQueriesRow'], 'id' | 'projectId' | 'rowVersion' | 'key' | 'projectNumber' | 'projectName'>
type Intent = Schemas['TaskBulkIntent']
type Command = Schemas['TaskBulkCommandBody']
type Preview = Schemas['TaskBulkPreview']
type Result = Schemas['TaskBulkCommandResult']

/** Versions are snapshots of an explicit selection; background refetches never promote them. */
export function useTaskBulkSelection(scope: string, enabled: boolean) {
  const [state, setState] = useState<{ scope: string; rows: Map<string, TaskBulkSelectionRow> }>({ scope, rows: new Map() })
  const selected = useMemo(() => enabled && state.scope === scope ? state.rows : new Map<string, TaskBulkSelectionRow>(), [enabled, state, scope])
  useEffect(() => {
    if (state.scope !== scope || (!enabled && state.rows.size)) {
      if (enabled && state.rows.size) toast.info(t('bulkCommand.reset'))
      setState({ scope, rows: new Map() })
    }
  }, [scope, enabled, state.scope, state.rows.size])
  const clear = () => setState({ scope, rows: new Map() })
  const toggle = (row: TaskBulkSelectionRow) => {
    if (!selected.has(row.id) && selected.size >= 200) { toast.info(t('bulkCommand.limit')); return }
    const rows = new Map(selected); if (rows.has(row.id)) rows.delete(row.id); else rows.set(row.id, { ...row })
    setState({ scope, rows })
  }
  const page = (rows: TaskBulkSelectionRow[], on: boolean) => {
    const next = new Map(selected)
    for (const row of rows) { if (!on) next.delete(row.id); else if (!next.has(row.id) && next.size < 200) next.set(row.id, { ...row }) }
    if (on && rows.some(row => !next.has(row.id))) toast.info(t('bulkCommand.limit'))
    setState({ scope, rows: next })
  }
  return { selected, clear, toggle, page }
}

export function TaskBulkToolbar({ selected, pageIds, onAction, onClear, omitShift = false }: { selected: ReadonlyMap<string, TaskBulkSelectionRow>; pageIds: string[]; onAction: (op: TaskBulkOperation) => void; onClear: () => void; omitShift?: boolean }) {
  if (!selected.size) return null
  return <div role="region" aria-label={t('bulk.actions')} className="sticky top-0 z-20 flex flex-wrap items-center gap-2 rounded-lg border bg-card p-3" data-task-bulk-toolbar>
    <p className="basis-full text-sm tabular-nums" aria-live="polite">{t('bulkCommand.selection', { selected: selected.size, outside: [...selected.keys()].filter(id => !pageIds.includes(id)).length })}</p>
    {TASK_BULK_OPERATIONS.filter(op => !omitShift || op !== 'shiftDueDates').map(op => <Button key={op} variant="outline" onClick={() => onAction(op)}>{runtimeText(`tbulk.${op}`)}</Button>)}
    <Button variant="ghost" onClick={onClear}>{t('common.clear')}</Button>
  </div>
}

/** Preview is read-only; Apply and uncertain retry retain one exact body/request ID/version set. */
export function TaskBulkCommandDialog({ operation, rows, scopeKey, onClose, onChanged }: { operation: TaskBulkOperation; rows: TaskBulkSelectionRow[]; scopeKey: string; onClose: (completed: boolean) => void; onChanged: () => void | Promise<unknown> }) {
  const me = useMe(), initialActor = useRef(me.id).current, initialScope = useRef(scopeKey).current
  const current = useRef({ actor: me.id, scope: scopeKey }); current.current = { actor: me.id, scope: scopeKey }
  const mounted = useRef(true), locked = useRef(false), uncertainty = useRef(false)
  const sameActor = me.id === initialActor, sameScope = scopeKey === initialScope
  const question = useSyncExternalStore(leaveQuestion.subscribe, leaveQuestion.get)
  const [navigationBlocks, setNavigationBlocks] = useState(0)
  // Keep the one global router blocker. An in-flight command cannot be discarded by its leave question.
  useUnsavedGuard(() => locked.current, () => {})
  useLayoutEffect(() => {
    if (question && locked.current) { leaveQuestion.answer(false); setNavigationBlocks(count => count + 1) }
  }, [question])
  useLayoutEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const prefix = useId(), qc = useQueryClient(), original = useRef(rows.map(row => ({ ...row }))).current
  const focusOrigin = useRef<{ opener: HTMLElement | null; scroll: { node: HTMLElement; top: number; left: number }[] } | null>(null)
  const captureFocus = () => {
    const nodes = [document.getElementById('content'), ...document.querySelectorAll<HTMLElement>('[data-task-layout="desktop"]')].filter((node): node is HTMLElement => !!node)
    focusOrigin.current = { opener: document.activeElement instanceof HTMLElement ? document.activeElement : null, scroll: [...new Set(nodes)].map(node => ({ node, top: node.scrollTop, left: node.scrollLeft })) }
  }
  const projects = useMemo(() => [...new Map(original.map(row => [row.projectId, { id: row.projectId, number: row.projectNumber }])).values()], [original])
  const choices = useQueries({ queries: projects.map(project => ({ queryKey: ['p', project.id, 'deliverables', {}], enabled: operation === 'setDeliverable',
    queryFn: () => typedRequest('get', '/api/v1/projects/{id}/deliverables', { params: { id: project.id } }) })) })
  const [value, setValue] = useState(operation === 'setPriority' ? 'Medium' : '')
  const [assignee, setAssignee] = useState<string | null>(null), [targets, setTargets] = useState<Record<string, string>>({}), [reason, setReason] = useState('')
  const [preview, setPreview] = useState<Preview | null>(null), [result, setResult] = useState<Result | null>(null), [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false)
  const command = useRef<Command | null>(null), sending = useRef(false)
  useLayoutEffect(() => {
    if (!sameActor) { locked.current = false; uncertainty.current = false; command.current = null; onClose(false) }
    else if (!sameScope && !locked.current) { command.current = null; onClose(false) }
  }, [sameActor, sameScope, onClose])
  const needsReason = operation === 'shiftDueDates' || operation === 'hold' || operation === 'cancel'
  const intent = (): Intent => {
    const params = operation === 'assign' ? { kind: 'assign' as const, assigneeId: assignee }
      : operation === 'setDueDate' ? { kind: 'setDueDate' as const, dueDate: value || null }
      : operation === 'shiftDueDates' ? { kind: 'shiftDueDates' as const, days: Number(value) }
      : operation === 'setPriority' ? { kind: 'setPriority' as const, priority: value }
      : operation === 'setDeliverable' ? { kind: 'setDeliverable' as const, targets: projects.map(project => ({ projectId: project.id, deliverableId: targets[project.id] === '__clear__' ? null : targets[project.id] })) }
      : { kind: operation }
    return { schemaVersion: 1, items: original.map(row => ({ taskId: row.id, projectId: row.projectId, rowVersion: row.rowVersion })), operation, params, reason: reason.trim() || null }
  }
  const resetPreview = () => { setPreview(null); setError(null) }
  const review = async () => {
    if (sending.current || !sameActor || !sameScope || locked.current) return; sending.current = true; setBusy(true); setError(null)
    try {
      const next = intent(); const read = await typedRequest('post', '/api/v1/tasks/bulk/preview', { body: next })
      if (!mounted.current || current.current.actor !== initialActor || current.current.scope !== initialScope) return
      command.current = { requestId: crypto.randomUUID(), intent: next }; setPreview(read)
    } catch (caught) { if (mounted.current && current.current.actor === initialActor && current.current.scope === initialScope) setError(caught) }
    finally { sending.current = false; if (mounted.current) setBusy(false) }
  }
  const apply = async () => {
    if (sending.current || !command.current || !sameActor || !sameScope && !uncertainty.current) return
    sending.current = true; locked.current = true; setBusy(true); setError(null)
    try {
      const answer = await typedRequest('post', '/api/v1/tasks/bulk/commands', { body: command.current })
      if (!mounted.current || current.current.actor !== initialActor) return
      locked.current = false; uncertainty.current = false
      setResult(answer); setUncertain(false)
      // Close cannot race the refresh that may remove its selected row. Receipt success stays definite if a read fails.
      await Promise.allSettled([invalidateChange(qc, 'Task'), Promise.resolve().then(onChanged)])
    } catch (caught) {
      if (!mounted.current || current.current.actor !== initialActor) return
      const unknown = !(caught instanceof ApiError) || caught.status === 0 || caught.status === 408 || caught.status >= 500 || caught.code === 'idempotency_in_progress'
      uncertainty.current ||= unknown; locked.current = uncertainty.current
      setUncertain(uncertainty.current); setError(caught)
    } finally { sending.current = false; if (mounted.current) setBusy(false) }
  }
  const close = () => { if (!busy && !uncertain) onClose(!!result) }
  const restoreFocus = (event: Event) => {
    event.preventDefault()
    const visible = (element: HTMLElement) => element.isConnected && element.getClientRects().length > 0
    let target: HTMLElement | undefined
    if (current.current.actor === initialActor && current.current.scope === initialScope) {
      if (!result && focusOrigin.current?.opener && visible(focusOrigin.current.opener)) target = focusOrigin.current.opener
      else for (const row of original) {
        target = [...document.querySelectorAll<HTMLElement>(`[data-task-bulk-return="${row.id}"],[data-task-focus="key"][data-task-id="${row.id}"]`)].find(visible)
        if (target) break
      }
    }
    target ??= [...document.querySelectorAll<HTMLElement>('[data-task-bulk-list]')].find(visible)
    target?.focus({ preventScroll: true })
    for (const { node, top, left } of focusOrigin.current?.scroll ?? []) if (node.isConnected) { node.scrollTop = top; node.scrollLeft = left }
  }
  const renderedRows = result?.rows ?? preview?.rows ?? []
  return <Dialog open onOpenChange={open => !open && close()}><DialogContent onOpenAutoFocus={captureFocus} onCloseAutoFocus={restoreFocus} className="max-h-[90vh] overflow-y-auto sm:max-w-2xl" data-task-bulk-dialog data-bulk-navigation-blocked={navigationBlocks || undefined}>
    <DialogHeader><DialogTitle>{t(result ? 'bulkCommand.result' : 'bulkCommand.title')}</DialogTitle><DialogDescription>{runtimeText(`tbulk.${operation}`)} · {t('bulk.selected', { n: original.length })}</DialogDescription></DialogHeader>
    {!result && <form className="space-y-3" onSubmit={event => { event.preventDefault(); if (!uncertain) void review() }}>
      <fieldset disabled={busy || uncertain || !sameActor || !sameScope} className="space-y-3" onChange={resetPreview}>
        {operation === 'assign' && <Field label={t('field.AssigneeId')} htmlFor={prefix + '-assignee'}><PeoplePicker id={prefix + '-assignee'} value={assignee} onChange={id => { setAssignee(id); resetPreview() }} /></Field>}
        {operation === 'setDueDate' && <Field label={t('col.due')} htmlFor={prefix + '-date'}><Input id={prefix + '-date'} type="date" value={value} onChange={event => setValue(event.target.value)} /></Field>}
        {operation === 'shiftDueDates' && <Field label={t('bulk.days')} htmlFor={prefix + '-days'}><Input id={prefix + '-days'} type="number" required step={1} value={value} onChange={event => setValue(event.target.value)} /></Field>}
        {operation === 'setPriority' && <Field label={t('common.priority')} htmlFor={prefix + '-priority'}><select id={prefix + '-priority'} className={selectCls} value={value} onChange={event => setValue(event.target.value)}>{['Low', 'Medium', 'High', 'Critical'].map(priority => <option key={priority} value={priority}>{tv(priority)}</option>)}</select></Field>}
        {operation === 'setDeliverable' && projects.map((project, index) => <Field key={project.id} label={t('bulkCommand.projectDeliverable', { project: project.number })} htmlFor={prefix + project.id}>
          {choices[index].error && <ErrorBanner error={choices[index].error} retry={() => choices[index].refetch()} />}
          <select id={prefix + project.id} className={selectCls} required disabled={!choices[index].isSuccess} value={targets[project.id] ?? ''} onChange={event => setTargets(old => ({ ...old, [project.id]: event.target.value }))}>
            <option value="">{t('task.deliverable')}</option><option value="__clear__">{t('common.none')}</option>{(choices[index].data ?? []).map(d => <option key={d.id} value={d.id}>{d.key} {d.name}</option>)}
          </select>
        </Field>)}
        <Field label={t('common.reason')} htmlFor={prefix + '-reason'} optional={!needsReason} hint={t('common.reasonHint')}><Textarea id={prefix + '-reason'} required={needsReason} minLength={reason ? 5 : undefined} value={reason} onChange={event => setReason(event.target.value)} /></Field>
      </fieldset>
      {!uncertain && <Button type="submit" variant="outline" disabled={busy || !sameActor || !sameScope || operation === 'setDeliverable' && (choices.some(q => !q.isSuccess) || projects.some(p => !targets[p.id]))}>{busy && <Spinner />}{t('bulkCommand.preview')}</Button>}
    </form>}
    {error != null && <ErrorBanner error={error} />}
    {(uncertain || navigationBlocks > 0) && !result && <p role="status">{t('bulkCommand.uncertain')}</p>}
    {preview && !result && <p role="status" className="text-sm tabular-nums">{t('bulkCommand.previewCounts', preview.counts)} <span className="block text-muted-foreground">{t('bulkCommand.previewHint')}</span></p>}
    {result && <p role="status" className="text-sm tabular-nums">{t('bulkCommand.resultCounts', result.counts)}</p>}
    {renderedRows.length > 0 && <ul className="max-h-72 space-y-2 overflow-y-auto text-sm" aria-label={t(result ? 'bulkCommand.result' : 'bulkCommand.preview')}>{renderedRows.map(row => <li key={row.index} data-task-bulk-outcome={row.outcome} className="break-words rounded border p-2">
      {row.outcome === 'unavailable' ? t('bulkCommand.unavailable', { index: row.index + 1 }) : <><strong>{row.key}</strong> · {runtimeText('bulkCommand.' + row.outcome)}{row.reason && <p>{row.reason}</p>}</>}
    </li>)}</ul>}
    <DialogFooter><Button variant="outline" disabled={busy || uncertain} onClick={close}>{t(result ? 'common.close' : 'common.cancel')}</Button>
      {!result && (uncertain || preview) && <Button disabled={busy || !sameActor || !sameScope && !uncertain || !uncertain && !preview?.counts.eligible} onClick={apply}>{busy && <Spinner />}{t(uncertain ? 'bulkCommand.retry' : 'bulkCommand.confirm')}</Button>}
    </DialogFooter>
  </DialogContent></Dialog>
}
