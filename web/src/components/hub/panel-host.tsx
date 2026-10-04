import { ExternalLink, X } from 'lucide-react'
import type { ComponentType } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { t } from '@/lib/i18n'

export interface PanelProps { id: string; onClose: () => void }

/** Detail panels by item type, registered by the packets that own each item (tasks, deliverables…). */
export const PANELS: Record<string, { component: ComponentType<PanelProps>; fullPage?: (id: string) => string }> = {}

/** Opens an item's side panel over the current view; the URL carries it so panels are deep-linkable (§13.0). */
export function useItemPanel() {
  const [sp, setSp] = useSearchParams()
  return (type: string, id: string) => {
    if (!PANELS[type]) return
    const n = new URLSearchParams(sp)
    n.set('panel', `${type}:${id}`)
    setSp(n)
  }
}

export function PanelHost() {
  const [sp, setSp] = useSearchParams()
  const navigate = useNavigate()
  const raw = sp.get('panel')
  const [type, id] = raw ? [raw.slice(0, raw.indexOf(':')), raw.slice(raw.indexOf(':') + 1)] : [null, null]
  const entry = type ? PANELS[type] : undefined
  const close = () => { const n = new URLSearchParams(sp); n.delete('panel'); setSp(n) }
  if (!entry || !id) return null
  const Comp = entry.component
  return (
    <Sheet open onOpenChange={(o) => !o && close()}>
      <SheetContent side="right" className="w-full gap-0 overflow-y-auto p-0 sm:max-w-none xl:max-w-[560px] [&>button:last-child]:hidden">
        <SheetDescription className="sr-only">{t('panel.description')}</SheetDescription>
        {/* Header bar (design §6 detail panel): the item type, then "open full page" and close; it stays put while the item scrolls. */}
        <div className="sticky top-0 z-10 flex min-h-14 items-center justify-between gap-2 border-b bg-card py-2 pl-4 pr-2">
          <SheetTitle className="text-sm font-semibold">{t(`itemType.${type}`)}</SheetTitle>
          <div className="flex items-center gap-1">
            {entry.fullPage && <Button variant="ghost" size="sm" onClick={() => navigate(entry.fullPage!(id))}><ExternalLink className="size-4" aria-hidden />{t('panel.fullPage')}</Button>}
            <Button variant="ghost" size="icon" onClick={close} aria-label={t('common.close')}><X className="size-5" aria-hidden /></Button>
          </div>
        </div>
        <Comp id={id} onClose={close} />
      </SheetContent>
    </Sheet>
  )
}
