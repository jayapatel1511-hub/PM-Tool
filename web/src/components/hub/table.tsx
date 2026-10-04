import { ArrowDown, ArrowUp, Columns3 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export interface Column<T> { id: string; label: string; cell: (r: T) => ReactNode; sort?: (r: T) => unknown; className?: string; fixed?: boolean; optional?: boolean }

const cmp = (a: unknown, b: unknown) => (a == null && b == null ? 0 : a == null ? 1 : b == null ? -1 : a < b ? -1 : a > b ? 1 : 0)

/** Register tables (§13.0, FR-006): click a header to sort (the URL keeps it), due date and key break ties, and each viewer
 *  chooses the columns they see (kept in this browser only). */
export function useTable<T>(storageKey: string, columns: Column<T>[], rows: T[], tiebreak: (r: T) => unknown[]) {
  const [sp, setSp] = useSearchParams()
  const [hidden, setHidden] = useState<string[]>(() => { try { return JSON.parse(localStorage.getItem(storageKey) ?? 'null') ?? columns.filter((c) => c.optional).map((c) => c.id) } catch { return [] } })
  const [field, dir] = (sp.get('sort') ?? '').split(':')
  const col = columns.find((c) => c.id === field && c.sort)
  const sign = dir === 'desc' ? -1 : 1
  const sorted = !col ? rows : [...rows].sort((a, b) => cmp(col.sort!(a), col.sort!(b)) * sign || tiebreak(a).reduce<number>((r, v, i) => r || cmp(v, tiebreak(b)[i]), 0))
  const urlCols = sp.get('cols')?.split(',') // a saved view or shared link carries its columns
  const visible = columns.filter((c) => c.fixed || (urlCols ? urlCols.includes(c.id) : !hidden.includes(c.id)))
  const colsParam = visible.filter((c) => !c.fixed).map((c) => c.id).join(',')
  const setSort = (id: string) => {
    const n = new URLSearchParams(sp)
    const next = field !== id ? id : dir === 'desc' ? null : `${id}:desc`
    if (next) n.set('sort', next); else n.delete('sort')
    n.delete('page')
    setSp(n, { replace: true })
  }
  const header = (c: Column<T>) => (
    <th key={c.id} scope="col" className="whitespace-nowrap px-(--cell-px) py-2.5 font-medium" aria-sort={field === c.id ? (dir === 'desc' ? 'descending' : 'ascending') : c.sort ? 'none' : undefined}>
      {c.sort ? (
        <button type="button" className="inline-flex items-center gap-1 hover:text-foreground" onClick={() => setSort(c.id)}>
          {c.label}{field === c.id && (dir === 'desc' ? <ArrowDown className="size-3" aria-hidden /> : <ArrowUp className="size-3" aria-hidden />)}
        </button>
      ) : c.label}
    </th>
  )
  const toggle = (id: string, on: boolean) => {
    const base = urlCols ? columns.filter((c) => !c.fixed && !urlCols.includes(c.id)).map((c) => c.id) : hidden
    const next = on ? base.filter((x) => x !== id) : [...base, id]
    setHidden(next)
    try { localStorage.setItem(storageKey, JSON.stringify(next)) } catch { /* per-viewer convenience only */ }
    if (urlCols) { const n = new URLSearchParams(sp); n.delete('cols'); setSp(n, { replace: true }) }
  }
  const menu = (
    <DropdownMenu>
      <DropdownMenuTrigger asChild><Button variant="outline" size="sm"><Columns3 className="size-4" />{t('task.columns')}</Button></DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {columns.filter((c) => !c.fixed).map((c) => (
          <DropdownMenuCheckboxItem key={c.id} checked={!hidden.includes(c.id)} onSelect={(e) => e.preventDefault()} onCheckedChange={(on) => toggle(c.id, !!on)}>{c.label}</DropdownMenuCheckboxItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
  const cell = (c: Column<T>, r: T) => <td key={c.id} className={cn('h-(--row-min) px-(--cell-px) py-(--cell-py)', c.className)}>{c.cell(r)}</td>
  return { sorted, visible, header, cell, menu, colsParam }
}
