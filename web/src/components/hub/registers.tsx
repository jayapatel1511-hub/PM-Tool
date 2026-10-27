import { Fragment, type ReactElement, type ReactNode } from 'react'
import { Avatar } from '@/components/hub/people'
import { Loading, Missing, TableRegion } from '@/components/hub/common'
import type { Column } from '@/components/hub/table'
import { fmtDate } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Shared register presentation: the selected row has both tint and a leading cue. */
export const SELECTED_ROW = 'bg-accent shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent'
export const TITLE_LINK = 'break-words text-left font-medium text-primary underline-offset-4 hover:underline'

export function Person({ id, name }: { id?: string | null; name?: string | null }) {
  if (!name) return <Missing />
  return <span className="inline-flex items-center gap-2"><Avatar id={id} name={name} />{name}</span>
}

export function DateText({ date }: { date?: string | null }) {
  return date ? <span className="whitespace-nowrap tabular-nums">{fmtDate(date)}</span> : <Missing />
}

export function GroupRow({ span, label, count }: { span: number; label: ReactNode; count: number }) {
  return <tr className="border-t"><th colSpan={span} scope="rowgroup" className="bg-muted px-(--cell-px) py-2 text-left font-semibold">
    {label}<span className="ml-2 rounded-md bg-card px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{count}</span>
  </th></tr>
}

export function RegisterCards<T extends { id: string }>({ groups, columns, current }: { groups: { label: string; rows: T[] }[]; columns: Column<T>[]; current: (r: T) => boolean }) {
  return <div className="space-y-4 md:hidden">
    {groups.map((g, i) => <div key={`${i}-${g.label}`} className="space-y-2">
      {g.label && <h3 className="text-sm font-semibold">{g.label}<span className="ml-2 rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{g.rows.length}</span></h3>}
      <ul className="space-y-2">{g.rows.map((r) => <li key={r.id} className={cn('rounded-lg border bg-card p-4', current(r) && SELECTED_ROW)}>
        <dl className="grid grid-cols-[minmax(5.5rem,auto)_minmax(0,1fr)] items-start gap-x-3 gap-y-1.5 text-sm">
          {columns.map((c) => <Fragment key={c.id}><dt className="text-muted-foreground">{c.label}</dt><dd className="min-w-0 break-words">{c.cell(r)}</dd></Fragment>)}
        </dl>
      </li>)}</ul>
    </div>)}
  </div>
}

/** The shared register list (§47.7, FR-REC-14): a table from `md` up, cards below it, optional group rows, one selected row.
 *  A list with its own phone cards (or none), group row or click-through row passes `cards`, `groupRow` or `onRowClick`;
 *  `oneBody` keeps the groups in a single `<tbody>`. */
export function DataTable<T extends { id: string }>({ table, rows, loading, empty, current, groupBy, caption, label, cards, wide, groupRow, onRowClick, oneBody }: {
  table: { visible: Column<T>[]; header: (c: Column<T>) => ReactElement; cell: (c: Column<T>, r: T) => ReactNode }; rows: T[]; loading: boolean; empty: ReactNode
  current: (r: T) => boolean; groupBy?: (r: T) => string
  /** The table's screen-reader caption; omitted for a table that never had one, so its accessible name does not change. */ caption?: string
  /** Names the scroll region (role region) when the screen's table had one. */ label?: string
  /** Below `md`: omitted draws one generic card per row, `false` draws none, a node replaces them for a screen whose rows carry their own phone controls. */
  cards?: ReactNode
  /** Keeps the table at every width with no cards, for a screen that never had phone cards. */ wide?: boolean
  groupRow?: (label: string, count: number, span: number) => ReactElement; onRowClick?: (r: T) => void; oneBody?: boolean
}) {
  if (loading) return <div className="rounded-lg border bg-card"><Loading rows={6} /></div>
  if (!rows.length) return <div className="rounded-lg border bg-card">{empty}</div>
  const groups: { label: string; items: T[] }[] = []
  for (const row of rows) {
    const label = groupBy?.(row) ?? ''
    if (groups.at(-1)?.label !== label) groups.push({ label, items: [] })
    groups.at(-1)!.items.push(row)
  }
  const body = groups.map((group, index) => <Fragment key={`${index}-${group.label}`}>
    {groupBy && (groupRow?.(group.label, group.items.length, table.visible.length) ?? <GroupRow span={table.visible.length} label={group.label} count={group.items.length} />)}
    {group.items.map((r) => <tr key={r.id} className={cn('border-t hover:bg-muted', onRowClick && 'cursor-pointer', current(r) && SELECTED_ROW)} onClick={onRowClick && (() => onRowClick(r))}>{table.visible.map((c) => table.cell(c, r))}</tr>)}
  </Fragment>)
  return (<>
    {!wide && (cards === undefined ? <RegisterCards groups={groups.map((g) => ({ label: g.label, rows: g.items }))} columns={table.visible} current={current} /> : cards)}
    <TableRegion className={wide ? undefined : 'hidden md:block'} {...(label && { role: 'region', 'aria-label': label })}>
      <table className="w-full text-sm">
        {caption && <caption className="sr-only">{caption}</caption>}
        <thead className="bg-muted text-left text-muted-foreground"><tr>{table.visible.map((c) => table.header(c))}</tr></thead>
        {oneBody ? <tbody>{body}</tbody> : body.map((group, index) => <tbody key={index}>{group}</tbody>)}
      </table>
    </TableRegion>
  </>)
}

export function PanelHead({ meta, title, children }: { meta: ReactNode; title: ReactNode; children?: ReactNode }) {
  return <div className="space-y-3 p-4"><div className="flex flex-wrap items-center gap-2">{meta}</div>
    <h2 className="break-words text-2xl/8 font-semibold tracking-[-0.4px]">{title}</h2>{children}</div>
}

export function FieldGroup({ title, children, actions }: { title: ReactNode; children: ReactNode; actions?: ReactNode }) {
  return <section className="border-t p-4"><div className="mb-2 flex min-h-8 items-center justify-between gap-2"><h3 className="text-sm font-semibold">{title}</h3>{actions}</div>{children}</section>
}
