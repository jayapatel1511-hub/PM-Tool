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
      <SheetContent side="right" className="w-full gap-0 overflow-y-auto p-0 sm:max-w-[600px] [&>button:last-child]:hidden">
        <SheetTitle className="sr-only">{t(`itemType.${type}`)}</SheetTitle>
        <SheetDescription className="sr-only">{t('panel.description')}</SheetDescription>
        <div className="sticky top-0 z-10 flex justify-end gap-1 border-b bg-card/95 px-2 py-1 backdrop-blur">
          {entry.fullPage && <Button variant="ghost" size="sm" onClick={() => navigate(entry.fullPage!(id))}><ExternalLink className="size-4" />{t('panel.fullPage')}</Button>}
          <Button variant="ghost" size="icon" className="size-8" onClick={close} aria-label={t('common.close')}><X className="size-4" /></Button>
        </div>
        <Comp id={id} onClose={close} />
      </SheetContent>
    </Sheet>
  )
}
