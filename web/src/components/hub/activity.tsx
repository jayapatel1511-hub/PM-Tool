import { Button } from '@/components/ui/button'
import { Empty, tdCls, thCls } from '@/components/hub/common'
import { fmtDate, fmtTime } from '@/lib/format'
import { t, tv } from '@/lib/i18n'

export interface ActivityRow {
  id: string; occurredAt: string; actorId?: string; actorName?: string; actorType: string; projectId?: string
  itemType: string; itemId?: string; itemKey?: string; itemName?: string; action: string; categories: string[]
  changes: { field: string; old: unknown; new: unknown; oldLabel?: string; newLabel?: string }[]
  summary: string; reason?: string; source: string; correlationId?: string; snapshot?: Record<string, unknown>; snapshotLabels?: Record<string, string | null>
}

const isDate = (v?: string) => !!v && /^\d{4}-\d{2}-\d{2}$/.test(v)
const isStamp = (v?: string) => !!v && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(v)
const isGuid = (v: unknown) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)
const fieldLabel = (name: string) => { const key = `field.${name}`; const label = t(key); return label === key ? name.replace(/Id$/, '').replace(/([a-z])([A-Z])/g, '$1 $2') : label }
const rawLabel = (value: unknown) => value == null ? null : typeof value === 'string' ? value : JSON.stringify(value)
const show = (v?: string | null) => {
  if (v == null || v === '') return t('common.dash')
  if (isGuid(v)) return <details className="inline-block align-top"><summary className="cursor-pointer underline-offset-4 hover:underline">{t('activity.referenceUnavailable')}</summary><code className="block break-all text-xs">{v}</code></details>
  if (isStamp(v)) return <time dateTime={v} title={v}>{fmtTime(v)}</time>
  return isDate(v) ? fmtDate(v) : tv(v)
}

/** "Due date: 2026-09-10 → 2026-09-17" (§20.3 human-readable changes). */
export function ChangeList({ row }: { row: ActivityRow }) {
  if (row.action === 'Deleted' && row.snapshot) {
    return <details><summary className="cursor-pointer text-muted-foreground">{t('activity.snapshot')}</summary><ul className="mt-1 space-y-0.5">{Object.entries(row.snapshot).filter(([, v]) => v != null && v !== '')
      .map(([k, v]) => <li key={k}><span className="text-muted-foreground">{fieldLabel(k)}:</span>{' '}{show(row.snapshotLabels?.[k] ?? (typeof v === 'string' ? v : JSON.stringify(v)))}</li>)}</ul></details>
  }
  if (!row.changes.length) return null
  return (
    <ul className="space-y-0.5">
      {row.changes.map((c) => (
        <li key={c.field}>
          <span className="text-muted-foreground">{fieldLabel(c.field)}:</span>{' '}
          {row.action === 'Created' ? show(c.newLabel ?? rawLabel(c.new)) : <>{show(c.oldLabel ?? rawLabel(c.old))} → {show(c.newLabel ?? rawLabel(c.new))}</>}
        </li>
      ))}
    </ul>
  )
}

export function ActivityTable({ rows, total, page, pageSize, onPage, onOpen }: {
  rows: ActivityRow[]; total: number; page: number; pageSize: number; onPage: (p: number) => void; onOpen?: (r: ActivityRow) => void
}) {
  if (!rows.length) return <div className="rounded-lg border bg-card"><Empty>{t('activity.empty')}</Empty></div>
  return (
    <div className="rounded-lg border bg-card">
      {/* Text-only rows: the scroll region itself takes focus so the keyboard can scroll it (WCAG 2.1.1). */}
      <div className="scroll-region overflow-x-auto" tabIndex={0} role="region" aria-label={t('activity.tableLabel')}>
        <table className="w-full text-sm">
          <thead className="bg-muted">
            <tr>{['common.when', 'common.actor', 'common.action', 'common.item', 'common.change', 'common.reason', 'common.source'].map((h) => <th key={h} scope="col" className={thCls}>{t(h)}</th>)}</tr>
          </thead>
          <tbody>
            {rows.map((r) => {
              const itemName = r.itemType === 'User' && r.changes.some(c => c.field === 'Role' && c.new === r.itemName) ? tv(r.itemName ?? '') : r.itemName
              return (
              <tr key={r.id} className="border-t align-top">
                <td className={`${tdCls} whitespace-nowrap tabular-nums`} title={r.occurredAt}>{fmtTime(r.occurredAt)}</td>
                <td className={tdCls}>{r.actorName ?? t('common.system')}</td>
                <td className={tdCls}>{t(`action.${r.action}`)}</td>
                <td className={tdCls}>
                  {/* Some records (allocation links, availability) have no key or name: fall back to the item type so the button is named. */}
                  {onOpen && r.itemId ? <button className="text-left hover:underline" onClick={() => onOpen(r)}>{r.itemKey || itemName ? <><span className="key">{r.itemKey}</span> {itemName}</> : t(`itemType.${r.itemType}`)}</button>
                    : <><span className="key">{r.itemKey}</span> {itemName}</>}
                  <div className="text-xs text-muted-foreground">{t(`itemType.${r.itemType}`)}</div>
                </td>
                <td className={`${tdCls} min-w-64`}><ChangeList row={r} /></td>
                <td className={tdCls}>{r.reason}</td>
                <td className={`${tdCls} text-xs text-muted-foreground`}>{r.source}</td>
              </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      {total > pageSize && (
        <div className="flex items-center justify-end gap-2 border-t px-4 py-2.5 text-sm">
          <span className="text-muted-foreground">{t('paging.range', { from: (page - 1) * pageSize + 1, to: Math.min(page * pageSize, total), total })}</span>
          <Button size="sm" variant="outline" disabled={page === 1} onClick={() => onPage(page - 1)}>{t('common.previous')}</Button>
          <Button size="sm" variant="outline" disabled={page * pageSize >= total} onClick={() => onPage(page + 1)}>{t('common.next')}</Button>
        </div>
      )}
    </div>
  )
}
