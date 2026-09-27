import { useQuery } from '@tanstack/react-query'
import { Pencil } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { ChangeList, type ActivityRow } from '@/components/hub/activity'
import { Empty, Loading } from '@/components/hub/common'
import { PeoplePicker } from '@/components/hub/people'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { get } from '@/lib/api'
import { fmtDate, fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

// Inline edit on click for detail panels; each field saves on its own (§13.0 interaction, §13.3.1 autosave).

export function FieldRow({ label, children, className }: { label: ReactNode; children: ReactNode; className?: string }) {
  return (
    <div className={cn('grid grid-cols-[8.5rem_1fr] items-start gap-2 py-1 text-sm', className)}>
      <div className="pt-1.5 text-xs text-muted-foreground">{label}</div>
      <div className="min-w-0">{children}</div>
    </div>
  )
}

function Display({ children, onEdit, disabled, title }: { children: ReactNode; onEdit: () => void; disabled?: boolean; title?: string }) {
  if (disabled) return <div className="min-h-8 px-2 py-1.5" title={title}>{children}</div>
  return (
    <button type="button" onClick={onEdit} title={title}
      className="group flex min-h-8 w-full items-center justify-between gap-2 rounded-md px-2 py-1.5 text-left hover:bg-muted focus-visible:bg-muted">
      <span className="min-w-0 flex-1 truncate">{children}</span>
      <Pencil className="size-3 shrink-0 opacity-0 group-hover:opacity-60 group-focus-visible:opacity-60" aria-hidden />
    </button>
  )
}

export function InlineText({ value, onSave, multiline, disabled, placeholder, title, render }: {
  value?: string | null; onSave: (v: string | null) => Promise<unknown>; multiline?: boolean; disabled?: boolean; placeholder?: string; title?: string; render?: (v: string) => ReactNode
}) {
  const [editing, setEditing] = useState(false)
  const [v, setV] = useState(value ?? '')
  const ref = useRef<HTMLInputElement & HTMLTextAreaElement>(null)
  useEffect(() => { setV(value ?? '') }, [value])
  useEffect(() => { if (editing) ref.current?.focus() }, [editing])
  const commit = async () => { setEditing(false); if ((v || null) !== (value || null)) await onSave(v.trim() || null) }
  if (!editing) return <Display onEdit={() => setEditing(true)} disabled={disabled} title={title}>{value ? (render ? render(value) : value) : <span className="text-muted-foreground">{placeholder ?? t('common.dash')}</span>}</Display>
  const props = { ref, value: v, onChange: (e: any) => setV(e.target.value), onBlur: commit,
    onKeyDown: (e: React.KeyboardEvent) => { if (e.key === 'Escape') { setV(value ?? ''); setEditing(false) } if (e.key === 'Enter' && (!multiline || e.ctrlKey || e.metaKey)) { e.preventDefault(); commit() } } }
  return multiline ? <Textarea rows={4} {...props} /> : <Input className="h-8" {...props} />
}

export function InlineDate({ value, onSave, disabled, title }: { value?: string | null; onSave: (v: string | null) => Promise<unknown>; disabled?: boolean; title?: string }) {
  const [editing, setEditing] = useState(false)
  if (!editing) return <Display onEdit={() => setEditing(true)} disabled={disabled} title={title}>{fmtDate(value)}</Display>
  return <Input type="date" className="h-8 w-44" autoFocus defaultValue={value ?? ''} onBlur={async (e) => { setEditing(false); if ((e.target.value || null) !== (value ?? null)) await onSave(e.target.value || null) }}
    onKeyDown={(e) => { if (e.key === 'Escape') setEditing(false); if (e.key === 'Enter') (e.target as HTMLInputElement).blur() }} />
}

export function InlineSelect({ value, options, onSave, disabled, allowEmpty, title }: {
  value?: string | null; options: { value: string; label: string }[]; onSave: (v: string | null) => Promise<unknown>; disabled?: boolean; allowEmpty?: boolean; title?: string
}) {
  const label = options.find((o) => o.value === value)?.label
  if (disabled) return <Display onEdit={() => {}} disabled title={title}>{label ?? t('common.dash')}</Display>
  return (
    <select className="h-8 w-full rounded-md border border-transparent bg-transparent px-1.5 text-sm hover:border-border focus:border-input" value={value ?? ''} title={title}
      onChange={(e) => onSave(e.target.value || null)} aria-label={title}>
      {allowEmpty && <option value="">{t('common.none')}</option>}
      {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
  )
}

export function InlinePerson({ value, name, onSave, disabled, title, allowClear = true }: { value?: string | null; name?: string | null; onSave: (id: string | null) => Promise<unknown>; disabled?: boolean; title?: string; allowClear?: boolean }) {
  if (disabled) return <Display onEdit={() => {}} disabled title={title}>{name ?? t('common.dash')}</Display>
  return <PeoplePicker value={value} valueName={name} onChange={(id) => onSave(id)} allowClear={allowClear} placeholder={t('common.none')} />
}

/** Item History tab: field-level changes, newest first (FR-AUD-02, §12.8 "history is facts"). */
export function HistoryList({ type, id }: { type: string; id: string }) {
  const q = useQuery({ queryKey: ['history', type, id], queryFn: () => get<{ items: ActivityRow[] }>(`items/${type}/${id}/activity?pageSize=100`) })
  if (q.isPending) return <Loading rows={3} />
  if (!q.data?.items.length) return <Empty>{t('activity.empty')}</Empty>
  return (
    <ol className="space-y-3 p-4">
      {q.data.items.map((r) => (
        <li key={r.id} className="text-sm">
          <div className="flex flex-wrap items-baseline gap-x-2">
            <span className="font-medium">{r.actorName ?? t('common.system')}</span>
            <span className="text-muted-foreground">{t(`action.${r.action}`)}{r.itemType !== type ? ` · ${t(`itemType.${r.itemType}`)} ${r.itemKey ?? ''}` : ''}</span>
            <span className="text-xs text-muted-foreground" title={r.occurredAt}>{fmtTime(r.occurredAt)}</span>
          </div>
          <div className="mt-0.5 text-[13px]"><ChangeList row={r} /></div>
          {r.reason && <div className="mt-0.5 text-xs text-muted-foreground">{t('common.reason')}: {r.reason}</div>}
        </li>
      ))}
    </ol>
  )
}

export function TabBar<T extends string>({ tabs, value, onChange }: { tabs: { id: T; label: string; count?: number }[]; value: T; onChange: (v: T) => void }) {
  return (
    <div role="tablist" className="flex gap-1 overflow-x-auto border-b px-4">
      {tabs.map((tb) => (
        <button key={tb.id} role="tab" aria-selected={value === tb.id} onClick={() => onChange(tb.id)}
          className={cn('whitespace-nowrap border-b-2 border-transparent px-3 py-2 text-sm text-muted-foreground hover:text-foreground', value === tb.id && 'border-primary font-medium text-foreground')}>
          {tb.label}{tb.count != null && tb.count > 0 && <span className="ml-1.5 rounded-full bg-muted px-1.5 text-xs">{tb.count}</span>}
        </button>
      ))}
    </div>
  )
}
