import { Search } from 'lucide-react'
import type { ComponentType, RefObject } from 'react'
import { t } from '@/lib/i18n'

// Top-bar parts that later packets fill in: search results (009), workspace scope (022),
// quick-create (002–008, 023) and the notification bell (006).
function SearchBox({ inputRef }: { inputRef: RefObject<HTMLInputElement | null> }) {
  return (
    <label className="relative flex w-full max-w-md items-center">
      <Search aria-hidden className="pointer-events-none absolute left-2.5 size-4 text-muted-foreground" />
      <span className="sr-only">{t('common.search')}</span>
      <input ref={inputRef} type="search" placeholder={t('top.search')} title={t('top.searchShortcut')}
        className="h-8 w-full rounded-md border bg-muted pl-8 pr-2 text-sm placeholder:text-muted-foreground focus:bg-card" />
    </label>
  )
}

export const ShellSlots: {
  Search: ComponentType<{ inputRef: RefObject<HTMLInputElement | null> }>; Workspace: ComponentType; QuickCreate: ComponentType; Bell: ComponentType
} = {
  Search: SearchBox,
  Workspace: () => null,
  QuickCreate: () => null,
  Bell: () => null,
}
