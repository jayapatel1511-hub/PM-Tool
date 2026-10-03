import { useQuery } from '@tanstack/react-query'
import { Check, ChevronsUpDown, X } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { get, qs } from '@/lib/api'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export interface Person { id: string; displayName: string; email?: string; jobTitle?: string; isActive?: boolean }

export function initials(name?: string | null) { return (name ?? '?').split(/\s+/).map((p) => p[0]).slice(0, 2).join('').toUpperCase() }

export function Avatar({ name, className, title }: { name?: string | null; className?: string; title?: string }) {
  return (
    <span title={title ?? name ?? ''} aria-hidden className={cn('inline-grid size-6 shrink-0 place-items-center rounded-full bg-accent text-[10px] font-semibold text-accent-foreground', className)}>
      {initials(name)}
    </span>
  )
}

export function PersonName({ name, active = true, className }: { name?: string | null; active?: boolean; className?: string }) {
  if (!name) return <span className={cn('text-muted-foreground', className)}>{t('common.dash')}</span>
  return <span className={cn(!active && 'text-muted-foreground', className)}>{active ? name : t('common.inactiveSuffix', { name })}</span>
}

/** People picker: searches active people by name or email (G-11: inactive people cannot be chosen). */
export function PeoplePicker({ value, valueName, onChange, placeholder, allowClear = true, exclude = [], id, disabled, candidates, compact, label }: {
  value?: string | null; valueName?: string | null; onChange: (id: string | null, person?: Person) => void; placeholder?: string
  allowClear?: boolean; exclude?: string[]; id?: string; disabled?: boolean; candidates?: Person[]; compact?: boolean; label?: string
}) {
  const [open, setOpen] = useState(false)
  const [q, setQ] = useState('')
  const search = useQuery({
    queryKey: ['people', q], enabled: open && !candidates,
    queryFn: () => get<Person[]>(`users${qs({ q, limit: 30 })}`), staleTime: 60_000,
  })
  const pool = (candidates ?? search.data ?? []).filter((p) => !exclude.includes(p.id) && (!candidates || !q || p.displayName.toLowerCase().includes(q.toLowerCase())))
  const selectedName = valueName ?? pool.find((p) => p.id === value)?.displayName
  return (
    <div className="flex items-center gap-1">
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button id={id} variant="outline" aria-label={label} disabled={disabled}
            className={cn('min-w-0 flex-1 justify-between font-normal', compact ? 'h-7 border-transparent bg-transparent px-1.5 text-[13px] shadow-none hover:border-border' : 'h-9 px-2')}>
            <span className={cn('truncate', !value && 'text-muted-foreground')}>{value ? selectedName ?? '…' : placeholder ?? t('people.choose')}</span>
            <ChevronsUpDown className="size-3.5 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-72 p-0" align="start">
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
