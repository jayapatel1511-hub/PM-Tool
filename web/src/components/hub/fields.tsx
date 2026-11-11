import { PersonPanelTrigger } from './person-panel-trigger'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Pencil } from 'lucide-react'
import { createContext, useCallback, useContext, useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { ActivityActor, ChangeList, actionLabel, groupActivity } from '@/components/hub/activity'
import { Empty, ErrorBanner, Loading, Spinner } from '@/components/hub/common'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { useFlag, useMe } from '@/lib/auth'
import { typedRequest, watchWrite, type ApiError, type WriteWatch } from '@/lib/api'
import { dropPending, keepPending, peekPending, pendingRoute, registerForm } from '@/lib/drafts'
import { ago, fmtDate, fmtTime } from '@/lib/format'
import { invalidateChange } from '@/lib/keys'
import { t , runtimeText } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useLocation, useSearchParams } from 'react-router'
import { useFormNavigationPreserver } from './form-navigation-context'

// Inline edit on click for detail panels; each field saves on its own (§13.0 interaction, §13.3.1 autosave).

const FieldLabelContext = createContext<string | undefined>(undefined)
/** The row's label when it is plain text: what names a field after sign-in expires (FR-SAV-05). */
const FieldNameContext = createContext<string | undefined>(undefined)

/** A labelled value in a detail panel: the label column sits beside the value from 640 px and above it on phones. `name` names the field
 *  when the label is not plain text. */
export function FieldRow({ label, name, children, className }: { label: ReactNode; name?: string; children: ReactNode; className?: string }) {
  const labelId = useId()
  return (
    <div className={cn('grid grid-cols-1 items-start gap-x-3 gap-y-1 py-1 text-sm sm:grid-cols-[8.5rem_1fr]', className)}>
      <div id={labelId} className="text-sm text-muted-foreground sm:pt-[calc((var(--control-row-h)_-_20px)/2)]">{label}</div>
      <div className="min-w-0">
        <FieldLabelContext.Provider value={labelId}><FieldNameContext.Provider value={name ?? (typeof label === 'string' ? label : undefined)}>{children}</FieldNameContext.Provider></FieldLabelContext.Provider>
      </div>
    </div>
  )
}

// `ifMatch`: the row version the field's write went on; `version`: the one a conflict names, for Keep mine.
type SaveState = { status: 'saving' | 'saved' | 'failed' | 'conflict'; value: unknown; message?: string; who?: string; at?: string; version?: number; ifMatch?: number }
const blank = (v: unknown) => (v === '' || v === undefined ? null : v)
const why = (e: unknown) => {
  const x = e as ApiError
  return x?.code === 'concurrency_conflict' && x.body?.changedBy ? t('error.conflict', { who: x.body.changedBy, when: ago(x.body.changedAt) }) : x?.message
}
// FR-SAV-02: a failed save goes again on its own after these waits, when it got no answer or found the server down or updating.
const RETRY_MS = [2000, 4000, 8000]
const since = (at?: string) => {
  const s = at ? Math.round((Date.now() - Date.parse(at)) / 1000) : NaN
  return s >= 0 && s < 60 ? t('rel.secondsAgo', { n: s }) : ago(at)
}

/** Per-field autosave (FR-SAV-01) and its feedback (design §8), shared by every inline field of the edit panels; the task panel's
 *  commit is the reference. `track` reports a save already sent: "Saving…" while pending, "Saved" only when the save reports success
 *  (a value other than null, false or undefined), "Not saved" when it was refused or failed. A refused save that lands later (a reason
 *  dialog completed it) stops saying "Not saved" once the field shows the value. With `delayMs` (`autosave_delay_ms`, present only
 *  while `flag.data_entry_safety` is on) `schedule` also saves typed text after that long without typing, and `commit` (blur, Enter)
 *  does not send that value a second time. Those saves go one at a time: one sent before the previous save is back would carry the
 *  row version from before it and be refused as a conflict, so it waits and goes out when that answer comes, on the row version the
 *  answer gives. Without `delayMs`, `commit` is the plain blur save it always was.
 *  FR-SAV-02 (unflagged, 051-D4) for a field that saves through `onSave` (`keep`; not a list row renamed in place): a save that fails
 *  never puts the server's copy back in the field. The field shows the value it sent (`kept`) with "Not saved", Retry (with the flag
 *  on) and "Discard my change"; a save that got no answer or found the server down or updating goes again after 2, 4 and 8 s while the
 *  field is on screen. Retries, and saves from a field still showing "Not saved", go on the row version the refused save went on, so a
 *  change someone made meanwhile is a conflict, not overwritten. A concurrency conflict brings the other value onto the screen beside this
 *  one and waits for "Keep mine" (sent on the row version the refusal names) or "Take theirs".
 *  With the flag on, leaving a field that says "Not saved" or shows a conflict asks first (FR-SAV-04).
 *  FR-SAV-05 (flag on, `field` names the field): a 401 from any request keeps the field's typed value in this tab, for the record the field
 *  was opened on, before the sign-in flow runs; after sign-in a captured row version is retried once. If expiry happened before the
 *  first save captured a version, the field keeps both values for explicit review and never automatically overwrites the current value. */
function useAutosaveField(current?: unknown, onSave?: (v: string | null) => Promise<unknown>, { delayMs, keep = !!onSave, field }: { delayMs?: number; keep?: boolean; field?: string } = {}) {
  const qc = useQueryClient()
  const preservesNavigation = useFormNavigationPreserver()
  const me = useMe()
  const on = useFlag('data_entry_safety') && keep
  const name = on ? field : undefined
  const [route] = useState(pendingRoute) // the record this field belongs to, whatever is on screen when a refusal comes back
  // Read, not removed, while rendering: the one retry is spent in the effect below, which runs once per mount.
  const [resumed] = useState(() => name ? peekPending(me.id, name, me.settings.draftKeepDays ?? 7) : null)
  const resume = useRef(resumed)
  const [state, setState] = useState<SaveState | null>(resumed ? { status: 'saving', value: resumed.value, ifMatch: resumed.version } : null)
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined)
  const pause = useRef<ReturnType<typeof setTimeout>>(undefined)
  const again = useRef<ReturnType<typeof setTimeout>>(undefined)
  const sent = useRef<{ value: unknown } | undefined>(undefined)
  const held = useRef<{ value: unknown } | undefined>(undefined)
  const typed = useRef<{ value: unknown } | undefined>(undefined) // the newest value put in the field, for a 401 to keep
  const gone = useRef(false)
  const landed = state != null && blank(state.value) === blank(current)
  const shown = landed && (state.status === 'failed' || state.status === 'conflict') ? null : state
  const unsaved = shown?.status === 'failed' || shown?.status === 'conflict'
  const live = useRef({ current, onSave, name, user: me.id, shown, unsaved })
  useEffect(() => { live.current = { current, onSave, name, user: me.id, shown, unsaved } })
  const track = async (save: unknown, value?: unknown, watch?: WriteWatch, tries = 0) => {
    clearTimeout(timer.current)
    clearTimeout(again.current)
    setState({ status: 'saving', value, ifMatch: watch?.ifMatch })
    let error: unknown, thrown = false
    try {
      const r = await save
      if (r !== null && r !== false) {
        // undefined: the caller reports no outcome (it shows its own errors), so nothing is claimed.
        setState(r === undefined ? null : { status: 'saved', value })
        if (r !== undefined) timer.current = setTimeout(() => setState(null), 4000)
        // The answer frees the next save, even when the refetch brings the field's value back unchanged (the server wrote "1.50" as
        // "1.5"); a value typed while this one was out goes now, on the row version the answer gave.
        const next = held.current
        sent.current = held.current = undefined
        if (next) { sent.current = next; void attempt(next.value, 0, watch?.saved) }
        return
      }
      error = watch?.error
    } catch (e) { error = e; thrown = true }
    // A refused or failed save never comes back, so the next one goes straight out; a value typed while it was out is the one kept.
    const kept = held.current ? held.current.value : value
    sent.current = held.current = undefined
    const x = error as ApiError | undefined
    const ifMatch = watch?.ifMatch
    if (keep && x?.code === 'concurrency_conflict') {
      setState({ status: 'conflict', value: kept, who: x.body?.changedBy, at: x.body?.changedAt, version: x.body?.currentRowVersion, ifMatch })
      void invalidateChange(qc) // brings the other value into `current`, so the field can show both
      return
    }
    setState({ status: 'failed', value: kept, message: thrown ? why(error) : undefined, ifMatch })
    if (keep && !gone.current && tries < RETRY_MS.length && x != null && (x.status === 0 || x.status >= 500)) again.current = setTimeout(() => resend(kept, tries + 1, ifMatch), RETRY_MS[tries])
  }
  // Reads `live` (the newest render, so the newest row version) because it also runs from the timers and the effect below. A field
  // still saying "Not saved" shows a value typed against the version its save went on, so that is the one it saves on.
  const attempt = (value: unknown, tries = 0, version = live.current.shown?.status === 'failed' ? live.current.shown.ifMatch : undefined) => {
    const watch: WriteWatch = { ifMatch: version, signedOut: () => { const { name, user } = live.current; if (name) keepPending(user, route, name, value, watch.ifMatch) } }
    return track(watchWrite(watch, () => live.current.onSave!(value as string | null)), value, watch, tries)
  }
  const already = (value: unknown) => sent.current !== undefined && blank(sent.current.value) === blank(value)
  const send = (value: unknown, tries = 0, version?: number) => {
    if (sent.current) { held.current = already(value) ? undefined : { value }; return Promise.resolve() }
    if (blank(value) === blank(live.current.current)) return Promise.resolve()
    sent.current = { value }
    return attempt(value, tries, version)
  }
  // Retry, Keep mine, the timed retries and the retry after sign-in: nothing goes out once the field's value is the saved one.
  const resend = (value: unknown, tries = 0, version?: number) => {
    typed.current = { value }
    if (blank(value) === blank(live.current.current)) { setState(null); return Promise.resolve() }
    return delayMs == null ? attempt(value, tries, version) : send(value, tries, version)
  }
  useEffect(() => {
    const again = resume.current
    resume.current = null
    if (!again || !name) return
    dropPending(me.id, name)
    // A notification/read request can expire the session before the delayed autosave
    // ever starts. There is then no row version to pin the retry to. Keep the value
    // visible as an explicit conflict, but never turn it into an unversioned write.
    if (again.version == null) {
      typed.current = { value: again.value }
      void invalidateChange(qc)
      setState({ status: 'conflict', value: again.value })
      return
    }
    void resend(again.value, 0, again.version)
  }, []) // eslint-disable-line react-hooks/exhaustive-deps -- once per mount: the one retry
  const commit = (value: unknown) => {
    typed.current = { value }
    clearTimeout(pause.current)
    clearTimeout(again.current)
    if (state?.status === 'conflict' && state.version == null) { setState({ ...state, value }); return Promise.resolve() }
    return delayMs == null ? attempt(value) : send(value)
  }
  const schedule = (value: unknown) => {
    typed.current = { value }
    if (state?.status === 'conflict' && state.version == null) { setState({ ...state, value }); return }
    clearTimeout(again.current) // typing on replaces a timed retry; leaving the field (or Escape) settles it
    if (delayMs == null) return
    clearTimeout(pause.current)
    pause.current = setTimeout(() => send(value), delayMs)
  }
  const clear = () => { clearTimeout(timer.current); clearTimeout(pause.current); clearTimeout(again.current); held.current = typed.current = undefined; setState(null) }
  // The timed retries end with the field. With the flag on, the leave guard asks while it shows a value that is not saved, and a 401
  // from any request keeps what was typed in it.
  useEffect(() => {
    gone.current = false
    const off = on ? registerForm({
      unsaved: () => live.current.unsaved,
      discard: clear,
      preservesNavigation,
      keep: () => {
        const { name, user, current, shown } = live.current, v = typed.current
        if (name && v && blank(v.value) !== blank(current)) keepPending(user, route, name, v.value, shown?.ifMatch)
      },
    }) : undefined
    return () => { gone.current = true; clearTimeout(again.current); off?.() }
  }, [on]) // eslint-disable-line react-hooks/exhaustive-deps -- `live` and the refs carry the rest
  return {
    state: shown, current, keep, track, commit, schedule, clear,
    /** The value the field shows instead of the server's while its save is out, failed or in conflict, and once saved until the
     *  refetch brings it back. */
    kept: keep && shown && !(shown.status === 'saved' && landed) ? { value: shown.value } : undefined,
    /** "Discard my change" and "Take theirs": the field goes back to the server's value. */
    discard: () => { if (unsaved) clear(); return unsaved },
    retry: () => shown && resend(shown.value, 0, shown.ifMatch),
    keepMine: () => shown && resend(shown.value, 0, shown.version),
  }
}
type Autosave = ReturnType<typeof useAutosaveField>

const asText = (v: unknown) => (v == null || v === '' ? t('common.dash') : String(v))
function NoteAction({ onClick, children }: { onClick: () => unknown; children: ReactNode }) {
  return <button type="button" className="min-h-6 font-medium text-primary underline underline-offset-4 hover:text-foreground" onClick={() => void onClick()}>{children}</button>
}

/** The live save line under a field; empty (and silent) when there is nothing to report. `show` writes a value the way the field
 *  shows it, for the two values of a conflict. */
function SaveNote({ save, show = asText }: { save: Autosave; show?: (v: unknown) => string }) {
  const { state } = save
  const retry = useFlag('data_entry_safety') // FR-SAV-08: the Retry control is a visible addition; the timed retries are not flagged
  return (
    <span aria-live="polite" className={cn('block text-xs/[18px] font-normal', state && 'px-2 pt-0.5', state?.status === 'failed' || state?.status === 'conflict' ? 'text-bad' : 'text-muted-foreground')}>
      {state?.status === 'saving' && <span className="inline-flex items-center gap-1.5"><Spinner className="size-3.5" />{t('common.saving')}</span>}
      {state?.status === 'saved' && <span className="inline-flex items-center gap-1.5 text-ok"><Check className="size-3.5" aria-hidden />{t('common.saved')}</span>}
      {state?.status === 'failed' && <><span aria-hidden>■ </span>{state.message ? t('inline.notSavedBecause', { reason: state.message }) : t('inline.notSaved')}
        {save.keep && <>{retry && <> · <NoteAction onClick={save.retry}>{t('inline.retry')}</NoteAction></>} · <NoteAction onClick={save.discard}>{t('inline.discard')}</NoteAction></>}</>}
      {state?.status === 'conflict' && <><span aria-hidden>■ </span>{t('inline.notSaved')}
        <span className="block break-words text-foreground">{t('inline.theirs', { who: state.who ?? t('inline.someone'), when: since(state.at), value: show(save.current) })}</span>
        <span className="block break-words text-foreground">{t('inline.yours', { value: show(state.value) })}</span>
        <NoteAction onClick={save.keepMine}>{t('inline.keepMine')}</NoteAction> · <NoteAction onClick={save.discard}>{t('inline.takeTheirs')}</NoteAction></>}
    </span>
  )
}

/** The same save feedback for a control that is not one of the inline fields (a checkbox, a slider): call `track` with the
 *  save's promise and the value being saved. */
export function SaveStatus({ current, children }: { current?: unknown; children: (track: (save: unknown, value?: unknown) => Promise<void>) => ReactNode }) {
  const save = useAutosaveField(current)
  return <>{children(save.track)}<SaveNote save={save} /></>
}

function Display({ children, onEdit, disabled, title }: { children: ReactNode; onEdit: () => void; disabled?: boolean; title?: string }) {
  if (disabled) return <div className="flex min-h-(--control-row-h) items-center px-2 py-1" title={title}><div className="min-w-0 flex-1 break-words">{children}</div></div>
  return (
    <button type="button" onClick={onEdit} title={title}
      className="group flex min-h-(--control-row-h) w-full items-center justify-between gap-2 rounded-md border border-transparent px-2 py-1 text-left hover:border-input hover:bg-muted focus-visible:bg-muted">
      <span className="min-w-0 flex-1 break-words">{children}</span>
      <Pencil className="size-3.5 shrink-0 text-muted-foreground opacity-0 group-hover:opacity-100 group-focus-visible:opacity-100" aria-hidden />
    </button>
  )
}

export function InlineText({ value, onSave, multiline, disabled, placeholder, title, render, initiallyEditing = false, onCancel, needsReason }: {
  value?: string | null; onSave: (v: string | null) => Promise<unknown>; multiline?: boolean; disabled?: boolean; placeholder?: string; title?: string; render?: (v: string) => ReactNode; initiallyEditing?: boolean; onCancel?: () => void
  /** The server asks a reason for this change (a Complete project): no save after a pause, so the reason is asked once, on blur (FR-SAV-01). */
  needsReason?: boolean
}) {
  const labelId = useContext(FieldLabelContext)
  const field = useContext(FieldNameContext) ?? title
  const delay = useMe().settings.autosaveDelayMs ?? undefined
  const [editing, setEditing] = useState(initiallyEditing)
  const ref = useRef<HTMLInputElement & HTMLTextAreaElement>(null)
  // A list row renamed in place (initiallyEditing) is not an edit panel field: it keeps saving on blur and Enter only.
  const save = useAutosaveField(value, onSave, { delayMs: initiallyEditing || needsReason ? undefined : delay, keep: !initiallyEditing, field })
  const [v, setV] = useState(() => save.kept ? String(save.kept.value ?? '') : value ?? '') // a save the sign-in refusal cut off comes back as typed
  const hold = !!save.kept || (save.state != null && save.state.status !== 'saved')
  useEffect(() => { if (!editing && !hold) setV(value ?? '') }, [value, editing, hold])
  useEffect(() => { if (editing) ref.current?.focus() }, [editing])
  const commit = async () => { setEditing(false); if ((v || null) === (value || null)) { save.discard(); onCancel?.(); return } await save.commit(v.trim() || null) }
  const note = <SaveNote save={save} />
  // What was typed stays on screen while its save is out, failed or in conflict (FR-SAV-02), including text typed after that save went.
  const shown = save.kept ? v.trim() || null : value
  if (!editing) return <><Display onEdit={() => setEditing(true)} disabled={disabled} title={title}>{shown ? (render ? render(shown) : shown) : <span className="text-muted-foreground">{placeholder ?? t('common.dash')}</span>}</Display>{note}</>
  const props = { ref, value: v, 'aria-labelledby': labelId, 'aria-label': labelId ? undefined : title, onChange: (e: any) => { setV(e.target.value); save.schedule(e.target.value.trim() || null) }, onBlur: commit,
    onKeyDown: (e: React.KeyboardEvent) => { if (e.key === 'Escape') { save.clear(); setV(value ?? ''); setEditing(false); onCancel?.() } if (e.key === 'Enter' && (!multiline || e.ctrlKey || e.metaKey)) { e.preventDefault(); commit() } } }
  return <>{!labelId && title && <span className="mb-1 block text-xs/[18px] font-normal text-muted-foreground">{title}</span>}{multiline ? <Textarea rows={4} {...props} /> : <Input className="min-h-(--control-row-h)" {...props} />}{note}</>
}

export function InlineDate({ value, onSave, disabled, title, hint }: { value?: string | null; onSave: (v: string | null) => Promise<unknown>; disabled?: boolean; title?: string; hint?: ReactNode | ((draft: string | null) => ReactNode) }) {
  const labelId = useContext(FieldLabelContext)
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(value ?? null)
  const save = useAutosaveField(value, onSave, { field: useContext(FieldNameContext) ?? title })
  const shown = save.kept ? save.kept.value as string | null : value
  const note = <SaveNote save={save} show={(v) => fmtDate(v as string | null)} />
  if (!editing) return <><Display onEdit={() => { setDraft(shown ?? null); setEditing(true) }} disabled={disabled} title={title}><span className="tabular-nums">{fmtDate(shown)}</span></Display>{note}</>
  return <><Input type="date" aria-labelledby={labelId} title={title} className="min-h-(--control-row-h) w-44" autoFocus onChange={e=>setDraft(e.target.value || null)} defaultValue={shown ?? ''}
    onBlur={async (e) => { setEditing(false); if ((e.target.value || null) !== (value ?? null)) await save.commit(e.target.value || null); else save.discard() }}
    onKeyDown={(e) => { if (e.key === 'Escape') { save.clear(); setEditing(false) } if (e.key === 'Enter') (e.target as HTMLInputElement).blur() }} />{hint && <span className="mt-1 block text-xs text-muted-foreground">{typeof hint === 'function' ? hint(draft) : hint}</span>}{note}</>
}

export function InlineSelect({ value, options, onSave, disabled, allowEmpty, title }: {
  value?: string | null; options: { value: string; label: string }[]; onSave: (v: string | null) => Promise<unknown>; disabled?: boolean; allowEmpty?: boolean; title?: string
}) {
  const labelId = useContext(FieldLabelContext)
  const save = useAutosaveField(value, onSave, { field: useContext(FieldNameContext) ?? title })
  const label = options.find((o) => o.value === value)?.label
  if (disabled) return <Display onEdit={() => {}} disabled title={title}>{label ?? t('common.dash')}</Display>
  return (
    <>
      <select className="h-(--control-row-h) w-full rounded-md border border-transparent bg-transparent px-1.5 text-sm hover:border-input hover:bg-muted focus-visible:border-input" value={String((save.kept ? save.kept.value : value) ?? '')} title={title}
        onChange={(e) => { const v = e.target.value || null; if (v !== (value ?? null) || !save.discard()) save.commit(v) }} aria-label={title} aria-labelledby={title ? undefined : labelId}>
        {allowEmpty && <option value="">{t('common.none')}</option>}
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
      <SaveNote save={save} show={(v) => options.find((o) => o.value === v)?.label ?? t('common.none')} />
    </>
  )
}

export function InlinePerson({ value, name, onSave, disabled, title, allowClear = true, exclude, focusKey }: { value?: string | null; name?: string | null; onSave: (id: string | null) => Promise<unknown>; disabled?: boolean; title?: string; allowClear?: boolean; exclude?: string[]; focusKey?: string }) {
  const peopleOn = useFlag('people_screens')
  const save = useAutosaveField(value, onSave, { field: useContext(FieldNameContext) ?? title })
  const [candidate, setCandidate] = useState<{ id: string; displayName: string } | undefined>()
  const displayId = save.kept ? save.kept.value as string | null : value
  const displayName = save.kept && displayId !== value ? candidate?.displayName : name
  const avatar = displayId && <Avatar id={displayId} name={displayName} />
  if (disabled) return <Display onEdit={() => {}} disabled title={title}><span className="flex items-center gap-2">{avatar}{displayId && displayName ? <PersonPanelTrigger userId={displayId} name={displayName} focusKey={focusKey} /> : displayName ?? t('common.dash')}</span></Display>
  return (
    <>
      <div className="flex items-center gap-2">
        {avatar}
        <div className="min-w-0 flex-1"><PeoplePicker value={displayId} valueName={displayName} onChange={(id, person) => { setCandidate(person); save.commit(id) }} allowClear={allowClear} exclude={exclude} placeholder={t('common.none')} /></div>
        {peopleOn && displayId && displayName && <PersonPanelTrigger userId={displayId} name={displayName} focusKey={focusKey}>{t('common.details')}</PersonPanelTrigger>}
      </div>
      <SaveNote save={save} show={(id) => (id === value ? name : id === candidate?.id ? candidate?.displayName : null) ?? t('common.dash')} />
    </>
  )
}

/** Item History tab: field-level changes, newest first (FR-AUD-02, §12.8 "history is facts"). */
export function HistoryList({ type, id, taskActorFilter = false, projectId }: { type: string; id: string; taskActorFilter?: boolean; projectId?: string }) {
  const [sp, setSp] = useSearchParams()
  const location = useLocation(), me = useMe()
  const small = useFlag('small_ux'), peopleOn = useFlag('people_screens'), workOn = useFlag('work_screens')
  const filtering = taskActorFilter && type === 'Task' && workOn
  const scoped = (key: string) => { const raw = sp.get(key); return raw?.startsWith(id + ':') ? raw.slice(id.length + 1) : undefined }
  const actorId = filtering ? scoped('taskHistoryActor') || undefined : undefined
  const requestedPage = Number(scoped('taskHistoryPage') ?? 1)
  const page = filtering && Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1
  const burst = (me.settings as { activityBurstMinutes?: number }).activityBurstMinutes ?? 10
  const grouped = small && sp.get('groupChanges') === 'true'
  const actors = useQuery({ queryKey: ['task-history-actors', id, projectId, me.id, workOn, peopleOn], enabled: filtering,
    queryFn: () => typedRequest('get', '/api/v1/tasks/{id}/activity/actors', { params: { id } }) })
  const q = useQuery({ queryKey: filtering ? ['history', type, id, projectId, me.id, workOn, peopleOn, actorId, page] : ['history', type, id],
    queryFn: () => typedRequest('get', '/api/v1/items/{type}/{id}/activity', { params: { type, id }, query: { pageSize: 100, ...(filtering ? { actorId, page } : {}) } }) })
  const change = (actor: string | undefined, nextPage: number) => {
    const next = new URLSearchParams(sp)
    if (actor) next.set('taskHistoryActor', id + ':' + actor); else next.delete('taskHistoryActor')
    next.set('taskHistoryPage', id + ':' + nextPage)
    setSp(next, { replace: true, state: location.state, preventScrollReset: true })
  }
  if (!filtering && q.isPending) return <Loading rows={3} />
  if (!filtering && q.error) return <div className="p-4"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  if (!filtering && !q.data?.items.length) return <Empty>{t('activity.empty')}</Empty>
  const failed = q.isError || filtering && actors.isError
  const currentUnavailable = failed || q.isPending || filtering && actors.isPending
  const rows = currentUnavailable ? [] : q.data?.items ?? []
  const choices = currentUnavailable ? [] : actors.data ?? []
  const groups = grouped ? groupActivity(rows, burst) : rows.map((r) => ({ rows: [r] }))
  const pages = Math.max(1, Math.ceil((q.data?.totalCount ?? 0) / (q.data?.pageSize ?? 100)))
  return (
    <>
      {filtering && <div className="space-y-2 px-4 py-3" data-task-history-controls={id}>
        <label className="flex flex-wrap items-center gap-2 text-sm">{t('taskHistory.person')}
          <select className="min-h-9 max-w-full rounded-md border bg-card px-2" value={currentUnavailable ? '' : actorId ?? ''} disabled={currentUnavailable} onChange={event => change(event.target.value || undefined, 1)}>
            <option value="">{currentUnavailable ? t('common.unavailable') : t('taskHistory.all')}</option>
            {!currentUnavailable && actorId && !choices.some(actor => actor.id === actorId) && <option value={actorId}>{t('common.unavailable')}</option>}
            {choices.map(actor => <option key={actor.id} value={actor.id}>{actor.isActive ? actor.displayName : t('common.inactiveSuffix', { name: actor.displayName })}</option>)}
          </select>
        </label>
        {!currentUnavailable && q.data && <div className="flex flex-wrap items-center gap-2 text-sm" aria-live="polite">
          <Button size="sm" variant="outline" disabled={q.isFetching || page <= 1} onClick={() => change(actorId, page - 1)}>{t('common.previous')}</Button>
          <span>{t('taskHistory.page', { page: q.data.page, pages, total: q.data.totalCount })}</span>
          <Button size="sm" variant="outline" disabled={q.isFetching || page >= pages} onClick={() => change(actorId, page + 1)}>{t('common.next')}</Button>
        </div>}
      </div>}
      {filtering && (q.isPending || actors.isPending) && <Loading rows={3} />}
      {filtering && failed && <div className="space-y-3 p-4"><ErrorBanner error={q.error ?? actors.error} /><Button type="button" size="sm" variant="outline" disabled={q.isFetching || actors.isFetching} onClick={() => { void q.refetch(); void actors.refetch() }}>{t('taskContribution.checkCurrent')}</Button></div>}
      {filtering && !failed && !q.isPending && !actors.isPending && !rows.length && <Empty>{t('activity.empty')}</Empty>}
      {small && <label className="flex items-center gap-2 px-4 py-3 text-sm"><input type="checkbox" checked={grouped} onChange={(e) => { const n = new URLSearchParams(sp); if (e.target.checked) n.set('groupChanges', 'true'); else n.delete('groupChanges'); setSp(n, filtering ? { replace: true, state: location.state, preventScrollReset: true } : { replace: true }) }} />{t('activity.groupChanges')}</label>}
      <ol className="divide-y px-4">
      {groups.map((g) => {
        const r = g.rows[0]
        return (
        <li key={r.id} className="flex gap-3 py-3 text-sm">
          <Avatar id={r.actorId} name={r.actorName ?? t('common.system')} className="mt-0.5" />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-baseline gap-x-2">
              <span className="font-semibold">{peopleOn ? <ActivityActor row={r} focusKey={`history:${type}:${id}:${r.id}:actor`} /> : r.actorName ?? t('common.system')}</span>
              <span className="text-muted-foreground">{actionLabel(r.action)}{r.itemType !== type ? ` · ${runtimeText(`itemType.${r.itemType}`)} ${r.itemKey ?? ''}` : ''}</span>
              <time className="text-xs/[18px] text-muted-foreground tabular-nums" dateTime={r.occurredAt} title={r.occurredAt}>{fmtTime(r.occurredAt)}</time>
            </div>
            {g.rows.length > 1 ? <details id={`history-group-${type}-${id}-${r.id}`} data-person-source-disclosure={peopleOn ? '' : undefined} className="mt-1"><summary className="cursor-pointer text-muted-foreground">{peopleOn ? t('activity.groupSummary', { name: '', n: g.rows.length }).trim() : t('activity.groupSummary', { name: r.actorName ?? t('common.system'), n: g.rows.length })} · {fmtTime(g.rows[g.rows.length-1].occurredAt)} – {fmtTime(r.occurredAt)}</summary><div className="mt-1 space-y-2">{g.rows.map((x) => <div key={x.id}><time dateTime={x.occurredAt}>{fmtTime(x.occurredAt)}</time> · {runtimeText(`action.${x.action}`)}<ChangeList row={x} />{x.reason && <p>{t('common.reason')}: {x.reason}</p>}</div>)}</div></details> : <div className="mt-1"><ChangeList row={r} /></div>}
            {r.reason && <p className="mt-1 text-xs/[18px] text-muted-foreground">{t('common.reason')}: {r.reason}</p>}
          </div>
        </li>
        )
      })}
      </ol>
    </>
  )
}

/** Scoped source-tab state survives a record -> Person -> record remount. Never consume another record's tab. */
export function useRecordTab(type: string, id: string, initial: string, allowedTabs: readonly string[]): [string, (value: string) => void] {
  const peopleOn = useFlag('people_screens'), workOn = useFlag('work_screens')
  const enabled = peopleOn || workOn, item = `${type}:${id}`
  const [sp, setSp] = useSearchParams(), location = useLocation()
  const [local, setLocal] = useState({ item, tab: initial })
  const raw = sp.get('recordTab')
  const selected = enabled && sp.get('recordTabItem') === item && raw && allowedTabs.includes(raw) ? raw : undefined
  const current = useRef({ enabled, item, sp, setSp, state: location.state, allowedTabs })
  current.current = { enabled, item, sp, setSp, state: location.state, allowedTabs }
  // A stable setter is needed by Task's existing explicit-comments effect; no replace-navigation loop.
  const choose = useCallback((value: string) => {
    const context = current.current
    if (!context.allowedTabs.includes(value)) return
    setLocal(previous => previous.item === context.item && previous.tab === value ? previous : { item: context.item, tab: value })
    if (!context.enabled || context.sp.get('recordTab') === value && context.sp.get('recordTabItem') === context.item) return
    context.setSp(params => { const next = new URLSearchParams(params); next.set('recordTab', value); next.set('recordTabItem', context.item); return next }, { replace: true, state: context.state })
  }, [])
  const fallback = enabled && (local.item !== item || !allowedTabs.includes(local.tab)) ? initial : local.tab
  return [selected ?? fallback, choose]
}

/** `label` names the tab list for assistive technology when the tabs need a group name; without it nothing changes. */
export function TabBar<T extends string>({ tabs, value, onChange, label }: { tabs: { id: T; label: string; count?: number }[]; value: T; onChange: (v: T) => void; label?: string }) {
  return (
    <div role="tablist" aria-label={label} className="flex min-w-0 flex-wrap gap-1 border-b px-4">
      {tabs.map((tb) => (
        <button key={tb.id} role="tab" aria-selected={value === tb.id} onClick={() => onChange(tb.id)}
          className={cn('min-h-11 whitespace-nowrap px-3 text-sm text-muted-foreground hover:bg-muted hover:text-foreground', value === tb.id && 'font-semibold text-foreground shadow-[inset_0_-2px_0_var(--primary)]')}>
          {tb.label}{tb.count != null && tb.count > 0 && <span className="ml-1.5 rounded-md bg-secondary px-1.5 text-xs tabular-nums">{tb.count}</span>}
        </button>
      ))}
    </div>
  )
}
