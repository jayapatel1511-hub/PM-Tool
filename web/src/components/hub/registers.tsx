import { Fragment, type ReactNode } from 'react'
import { Avatar } from '@/components/hub/people'
import { Missing } from '@/components/hub/common'
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

export function PanelHead({ meta, title, children }: { meta: ReactNode; title: ReactNode; children?: ReactNode }) {
  return <div className="space-y-3 p-4"><div className="flex flex-wrap items-center gap-2">{meta}</div>
    <h2 className="break-words text-2xl/8 font-semibold tracking-[-0.4px]">{title}</h2>{children}</div>
}

export function FieldGroup({ title, children, actions }: { title: ReactNode; children: ReactNode; actions?: ReactNode }) {
  return <section className="border-t p-4"><div className="mb-2 flex min-h-8 items-center justify-between gap-2"><h3 className="text-sm font-semibold">{title}</h3>{actions}</div>{children}</section>
}
