import { useQuery } from '@tanstack/react-query'
import { Check, ChevronsUpDown, X } from 'lucide-react'
import { createContext, useContext, useEffect, useRef, useState, type ComponentProps } from 'react'
import type { PlannerCell, PlannerGridData } from '@/components/planner/labels'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { get, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { addDays, hours } from '@/lib/format'
import { t } from '@/lib/i18n'
import { accentOf, cn } from '@/lib/utils'

export interface Person { id: string; displayName: string; email?: string; jobTitle?: string; isActive?: boolean }

export function initials(name?: string | null) { return (name ?? '?').split(/\s+/).map((p) => p[0]).slice(0, 2).join('').toUpperCase() }

/** Initials in the person's stable accent (keyed by id where known), 28 px in tables. The accent is a ring and the initials,
 *  not a fill, so a list of people stays inside the colour budget (§41 R8). */
export function Avatar({ name, id, className, title }: { name?: string | null; id?: string | null; className?: string; title?: string }) {
  return (
    <span title={title ?? name ?? ''} aria-hidden data-accent={accentOf(id ?? name)}
      className={cn('inline-grid size-7 shrink-0 place-items-center rounded-full border-[1.5px] border-(--acc-stripe) bg-card text-xs font-semibold text-(--acc-fg)', className)}>
      {initials(name)}
    </span>
  )
}

export function PersonName({ name, active = true, className }: { name?: string | null; active?: boolean; className?: string }) {
  if (!name) return <span className={cn('text-muted-foreground', className)}>{t('common.dash')}</span>
  return <span className={cn(!active && 'text-muted-foreground', className)}>{active ? name : t('common.inactiveSuffix', { name })}</span>
}

/** The Monday of a date's week (weeks start on Monday, as the planner's do). */
export const weekOf = (d: string) => addDays(d, -((new Date(`${d}T00:00:00Z`).getUTCDay() + 6) % 7))

/** The week whose capacity a picker shows, for pickers rendered by shared fields that cannot pass the prop (FR-PICK-02). */
export const CapacityWeek = createContext<string | undefined>(undefined)

type PickerCell = PlannerCell & { partial?: boolean }

/** Each candidate's remaining and time-away hours for one week (FR-PICK-01/02, D8). Only viewers who can open Workload ask,
 *  in one planning-grid request for the candidates not yet asked about (chunks of 50, the server's limit, for longer
 *  lists); the server drops anyone outside the viewer's scope, and those people simply show no figure. */
function usePickerCapacity(week: string | undefined, open: boolean, ids: string[]) {
  const allowed = useMe().capabilities.workload
  const [cells, setCells] = useState<Record<string, PickerCell>>({})
  const asked = useRef(new Set<string>())
  const idsKey = week && open && allowed ? ids.join(',') : ''
  useEffect(() => {
    if (!week || !idsKey) return
    const wanted = idsKey.split(',').filter((id) => !asked.current.has(`${week}:${id}`))
    for (const id of wanted) asked.current.add(`${week}:${id}`)
    for (let i = 0; i < wanted.length; i += 50)
      get<PlannerGridData>(`planning/grid${qs({ personIds: wanted.slice(i, i + 50).join(','), from: week, weeks: 1 })}`)
        .then((g) => setCells((c) => ({ ...c, ...Object.fromEntries(g.people.filter((p) => p.cells[0]).map((p) => [`${week}:${p.id}`, { ...p.cells[0], partial: p.partial }])) })))
        .catch(() => { /* no figure: the plain picker still works */ })
  }, [week, idsKey])
  return (id: string) => (week ? cells[`${week}:${id}`] : undefined)
}

/** "12 h free · 16 h away", short and right-aligned beside a candidate; nothing without data. When the person has planned
 *  work on projects the viewer cannot see, the free hours would be too high, so the figure says "Partial view" instead,
 *  as Workload and the Planner do (FR-CAP-06, AC-PLN-12). */
function CapacityFigure({ cell }: { cell?: PickerCell }) {
  if (!cell) return null
  const free = cell.partial ? t('workload.partialChip') : cell.remaining < 0 ? t('people.overBy', { h: hours(-cell.remaining) }) : t('people.free', { h: hours(cell.remaining) })
  return (
    <span data-capacity title={cell.partial ? t('workload.partial') : undefined} className={cn('shrink-0 text-xs tabular-nums', cell.remaining < 0 && !cell.partial ? 'font-medium text-warn' : 'text-muted-foreground')}>
      {free}{cell.timeAway > 0 && ` · ${t('people.away', { h: hours(cell.timeAway) })}`}
    </span>
  )
}

/** People picker: searches active people by name or email (G-11: inactive people cannot be chosen). With `capacityWeek`
 *  (or a CapacityWeek provider) each candidate shows the free hours for that week to viewers who may see them. */
export function PeoplePicker({ value, valueName, onChange, placeholder, allowClear = true, exclude = [], id, disabled, candidates, compact, label, capacityWeek, ...aria }: {
  value?: string | null; valueName?: string | null; onChange: (id: string | null, person?: Person) => void; placeholder?: string
  allowClear?: boolean; exclude?: string[]; id?: string; disabled?: boolean; candidates?: Person[]; compact?: boolean; label?: string; capacityWeek?: string
} & Pick<ComponentProps<typeof Button>, 'aria-describedby' | 'aria-invalid'>) {
  const [open, setOpen] = useState(false)
  const [q, setQ] = useState('')
  const providedWeek = useContext(CapacityWeek)
  const week = capacityWeek ?? providedWeek
  const search = useQuery({
    queryKey: ['people', q], enabled: open && !candidates,
    queryFn: () => get<Person[]>(`users${qs({ q, limit: 30 })}`), staleTime: 60_000,
  })
  const pool = (candidates ?? search.data ?? []).filter((p) => !exclude.includes(p.id) && (!candidates || !q || p.displayName.toLowerCase().includes(q.toLowerCase())))
  const capacity = usePickerCapacity(week, open, pool.map((p) => p.id))
  const selectedName = valueName ?? pool.find((p) => p.id === value)?.displayName
  return (
    <div className="flex items-center gap-1">
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button {...aria} id={id} variant="outline" aria-label={compact && label ? `${label}: ${value ? selectedName ?? t('common.unavailable') : placeholder ?? t('people.choose')}` : label} disabled={disabled}
            className={cn('min-w-0 flex-1 justify-between font-normal', compact ? 'min-h-(--control-row-h) border-transparent bg-transparent px-2 hover:border-input' : 'px-3')}>
            <span className={cn('truncate', !value && 'text-muted-foreground')}>{value ? selectedName ?? '…' : placeholder ?? t('people.choose')}</span>
            <ChevronsUpDown className="size-3.5 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className={cn('p-0', week ? 'w-96' : 'w-72')} align="start">
          <Command shouldFilter={false}>
            <CommandInput placeholder={t('people.search')} value={q} onValueChange={setQ} />
            <CommandList>
              <CommandEmpty>{t('people.none')}</CommandEmpty>
              <CommandGroup>
                {pool.map((p) => (
                  <CommandItem key={p.id} value={p.id} onSelect={() => { onChange(p.id, p); setOpen(false) }}>
                    <Check className={cn('size-3.5', value === p.id ? 'opacity-100' : 'opacity-0')} />
                    <span className="truncate">{p.displayName}</span>
                    {p.jobTitle && <span className="ml-auto truncate text-xs text-muted-foreground">{p.jobTitle}</span>}
                    {week && <span className={cn('flex shrink-0 justify-end', !p.jobTitle && 'ml-auto')}><CapacityFigure cell={capacity(p.id)} /></span>}
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {allowClear && value && !disabled && !compact && (
        <Button variant="ghost" size="icon" className="size-8" onClick={() => onChange(null)} aria-label={t('common.clear')}><X className="size-3.5" /></Button>
      )}
    </div>
  )
}
