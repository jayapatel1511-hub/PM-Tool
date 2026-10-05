import { useQuery } from '@tanstack/react-query'
import { Check, Pencil } from 'lucide-react'
import { createContext, useContext, useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { ChangeList, type ActivityRow } from '@/components/hub/activity'
import { Empty, ErrorBanner, Loading, Spinner } from '@/components/hub/common'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { get, type ApiError } from '@/lib/api'
import { ago, fmtDate, fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

// Inline edit on click for detail panels; each field saves on its own (§13.0 interaction, §13.3.1 autosave).

const FieldLabelContext = createContext<string | undefined>(undefined)

/** A labelled value in a detail panel: the label column sits beside the value from 640 px and above it on phones. */
export function FieldRow({ label, children, className }: { label: ReactNode; children: ReactNode; className?: string }) {
  const labelId = useId()
  return (
    <div className={cn('grid grid-cols-1 items-start gap-x-3 gap-y-1 py-1 text-sm sm:grid-cols-[8.5rem_1fr]', className)}>
      <div id={labelId} className="text-sm text-muted-foreground sm:pt-[calc((var(--control-row-h)_-_20px)/2)]">{label}</div>
      <div className="min-w-0">
        <FieldLabelContext.Provider value={labelId}>{children}</FieldLabelContext.Provider>
      </div>
    </div>
  )
}

type SaveState = { status: 'saving' | 'saved' | 'failed'; value: unknown; message?: string }
const blank = (v: unknown) => (v === '' || v === undefined ? null : v)
const why = (e: unknown) => {
  const x = e as ApiError
  return x?.code === 'concurrency_conflict' && x.body?.changedBy ? t('error.conflict', { who: x.body.changedBy, when: ago(x.body.changedAt) }) : x?.message
}

/** Save feedback for one field (design §8): "Saving…" while pending, "Saved" only when the save reports success (a value
 *  other than null, false or undefined), "Not saved" when it was refused or failed. A refused save that lands later (a
 *  reason dialog completed it) stops saying "Not saved" once the field shows the value. */
function useSaveStatus(current?: unknown) {
  const [state, setState] = useState<SaveState | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined)
  const track = async (save: unknown, value?: unknown) => {
    clearTimeout(timer.current)
    setState({ status: 'saving', value })
    try {
      const r = await save
      // undefined: the caller reports no outcome (it shows its own errors), so nothing is claimed.
      setState(r === undefined ? null : { status: r === null || r === false ? 'failed' : 'saved', value })
      if (r != null && r !== false) timer.current = setTimeout(() => setState(null), 4000)
    } catch (e) { setState({ status: 'failed', value, message: why(e) }) }
  }
  const landed = state?.status === 'failed' && blank(state.value) === blank(current)
  return { state: landed ? null : state, track, clear: () => { clearTimeout(timer.current); setState(null) } }
}

/** The live save line under a field; empty (and silent) when there is nothing to report. */
function SaveNote({ state }: { state: SaveState | null }) {
  return (
    <span aria-live="polite" className={cn('block text-xs/[18px] font-normal', state && 'px-2 pt-0.5', state?.status === 'failed' ? 'text-bad' : 'text-muted-foreground')}>
      {state?.status === 'saving' && <span className="inline-flex items-center gap-1.5"><Spinner className="size-3.5" />{t('common.saving')}</span>}
      {state?.status === 'saved' && <span className="inline-flex items-center gap-1.5 text-ok"><Check className="size-3.5" aria-hidden />{t('common.saved')}</span>}
      {state?.status === 'failed' && <><span aria-hidden>■ </span>{state.message ? t('inline.notSavedBecause', { reason: state.message }) : t('inline.notSaved')}</>}
    </span>
  )
}

/** The same save feedback for a control that is not one of the inline fields (a checkbox, a slider): call `track` with the
 *  save's promise and the value being saved. */
export function SaveStatus({ current, children }: { current?: unknown; children: (track: (save: unknown, value?: unknown) => Promise<void>) => ReactNode }) {
  const save = useSaveStatus(current)
  return <>{children(save.track)}<SaveNote state={save.state} /></>
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

export function InlineText({ value, onSave, multiline, disabled, placeholder, title, render }: {
  value?: string | null; onSave: (v: string | null) => Promise<unknown>; multiline?: boolean; disabled?: boolean; placeholder?: string; title?: string; render?: (v: string) => ReactNode
}) {
  const labelId = useContext(FieldLabelContext)
  const [editing, setEditing] = useState(false)
  const [v, setV] = useState(value ?? '')
  const ref = useRef<HTMLInputElement & HTMLTextAreaElement>(null)
  const save = useSaveStatus(value)
  useEffect(() => { if (!editing && save.state?.status !== 'saving' && save.state?.status !== 'failed') setV(value ?? '') }, [value, editing, save.state?.status])
  useEffect(() => { if (editing) ref.current?.focus() }, [editing])
  const commit = async () => { setEditing(false); if ((v || null) !== (value || null)) await save.track(onSave(v.trim() || null), v.trim() || null) }
  const note = <SaveNote state={save.state} />
  if (!editing) return <><Display onEdit={() => setEditing(true)} disabled={disabled} title={title}>{value ? (render ? render(value) : value) : <span className="text-muted-foreground">{placeholder ?? t('common.dash')}</span>}</Display>{note}</>
  const props = { ref, value: v, 'aria-labelledby': labelId, 'aria-label': labelId ? undefined : title, onChange: (e: any) => setV(e.target.value), onBlur: commit,
    onKeyDown: (e: React.KeyboardEvent) => { if (e.key === 'Escape') { save.clear(); setV(value ?? ''); setEditing(false) } if (e.key === 'Enter' && (!multiline || e.ctrlKey || e.metaKey)) { e.preventDefault(); commit() } } }
  return <>{!labelId && title && <span className="mb-1 block text-xs/[18px] font-normal text-muted-foreground">{title}</span>}{multiline ? <Textarea rows={4} {...props} /> : <Input className="min-h-(--control-row-h)" {...props} />}{note}</>
}

export function InlineDate({ value, onSave, disabled, title }: { value?: string | null; onSave: (v: string | null) => Promise<unknown>; disabled?: boolean; title?: string }) {
  const labelId = useContext(FieldLabelContext)
  const [editing, setEditing] = useState(false)
  const save = useSaveStatus(value)
  const note = <SaveNote state={save.state} />
  if (!editing) return <><Display onEdit={() => setEditing(true)} disabled={disabled} title={title}><span className="tabular-nums">{fmtDate(value)}</span></Display>{note}</>
  return <><Input type="date" aria-labelledby={labelId} title={title} className="min-h-(--control-row-h) w-44" autoFocus defaultValue={save.state?.status === 'failed' ? String(save.state.value ?? '') : value ?? ''}
    onBlur={async (e) => { setEditing(false); if ((e.target.value || null) !== (value ?? null)) await save.track(onSave(e.target.value || null), e.target.value || null) }}
    onKeyDown={(e) => { if (e.key === 'Escape') { save.clear(); setEditing(false) } if (e.key === 'Enter') (e.target as HTMLInputElement).blur() }} />{note}</>
}

export function InlineSelect({ value, options, onSave, disabled, allowEmpty, title }: {
  value?: string | null; options: { value: string; label: string }[]; onSave: (v: string | null) => Promise<unknown>; disabled?: boolean; allowEmpty?: boolean; title?: string
}) {
  const labelId = useContext(FieldLabelContext)
  const save = useSaveStatus(value)
  const label = options.find((o) => o.value === value)?.label
  if (disabled) return <Display onEdit={() => {}} disabled title={title}>{label ?? t('common.dash')}</Display>
  return (
    <>
      <select className="h-(--control-row-h) w-full rounded-md border border-transparent bg-transparent px-1.5 text-sm hover:border-input hover:bg-muted focus-visible:border-input" value={save.state?.status === 'failed' || save.state?.status === 'saving' ? String(save.state.value ?? '') : value ?? ''} title={title}
        onChange={(e) => save.track(onSave(e.target.value || null), e.target.value || null)} aria-label={title} aria-labelledby={title ? undefined : labelId}>
        {allowEmpty && <option value="">{t('common.none')}</option>}
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
      <SaveNote state={save.state} />
    </>
  )
}

export function InlinePerson({ value, name, onSave, disabled, title, allowClear = true }: { value?: string | null; name?: string | null; onSave: (id: string | null) => Promise<unknown>; disabled?: boolean; title?: string; allowClear?: boolean }) {
  const save = useSaveStatus(value)
  const [candidate, setCandidate] = useState<{ id: string; displayName: string } | undefined>()
  const retaining = save.state?.status === 'saving' || save.state?.status === 'failed'
  const displayId = retaining ? save.state?.value as string | null : value
  const displayName = retaining && displayId !== value ? candidate?.displayName : name
  const avatar = displayId && <Avatar id={displayId} name={displayName} />
  if (disabled) return <Display onEdit={() => {}} disabled title={title}><span className="flex items-center gap-2">{avatar}{displayName ?? t('common.dash')}</span></Display>
  return (
    <>
      <div className="flex items-center gap-2">
        {avatar}
        <div className="min-w-0 flex-1"><PeoplePicker value={displayId} valueName={displayName} onChange={(id, person) => { setCandidate(person); save.track(onSave(id), id) }} allowClear={allowClear} placeholder={t('common.none')} /></div>
      </div>
      <SaveNote state={save.state} />
    </>
  )
}

/** Item History tab: field-level changes, newest first (FR-AUD-02, §12.8 "history is facts"). */
export function HistoryList({ type, id }: { type: string; id: string }) {
  const q = useQuery({ queryKey: ['history', type, id], queryFn: () => get<{ items: ActivityRow[] }>(`items/${type}/${id}/activity?pageSize=100`) })
  if (q.isPending) return <Loading rows={3} />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  if (!q.data?.items.length) return <Empty>{t('activity.empty')}</Empty>
  return (
    <ol className="divide-y px-4">
      {q.data.items.map((r) => (
        <li key={r.id} className="flex gap-3 py-3 text-sm">
          <Avatar id={r.actorId} name={r.actorName ?? t('common.system')} className="mt-0.5" />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-baseline gap-x-2">
              <span className="font-semibold">{r.actorName ?? t('common.system')}</span>
              <span className="text-muted-foreground">{t(`action.${r.action}`)}{r.itemType !== type ? ` · ${t(`itemType.${r.itemType}`)} ${r.itemKey ?? ''}` : ''}</span>
              <time className="text-xs/[18px] text-muted-foreground tabular-nums" dateTime={r.occurredAt} title={r.occurredAt}>{fmtTime(r.occurredAt)}</time>
            </div>
            <div className="mt-1"><ChangeList row={r} /></div>
            {r.reason && <p className="mt-1 text-xs/[18px] text-muted-foreground">{t('common.reason')}: {r.reason}</p>}
          </div>
        </li>
      ))}
    </ol>
  )
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
