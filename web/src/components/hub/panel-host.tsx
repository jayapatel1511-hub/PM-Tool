import { ExternalLink, X } from 'lucide-react'
import { lazy, Suspense, useRef, type ComponentType, type LazyExoticComponent } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { useMe } from '@/lib/auth-context'
import { leave, taskDetailReturnOrigin, type FormNavigationLocation } from '@/lib/drafts'
import { t , runtimeText } from '@/lib/i18n'
import { lazyImport } from '@/lib/lazy'
import { Loading } from '@/components/hub/common'
import { recordContextReturn, type RecordContextOrigin } from '@/lib/record-context-navigation'

export interface PanelProps { id: string; onClose: () => void }

/** Detail panels by item type, registered by the packets that own each item (tasks, deliverables…). An entry that names a
 *  flag is absent while that flag is off (§47.5 FR-REC-12), so its `?panel=` links open nothing. */
export const PANELS: Record<string, { component: ComponentType<PanelProps> | LazyExoticComponent<ComponentType<PanelProps>>; fullPage?: (id: string) => string; flag?: string }> = {}
type PanelEntry = (typeof PANELS)[string]

function usePanels() {
  const flags = useMe().flags
  return (type: string | null) => { const entry = type ? PANELS[type] : undefined; return entry && (!entry.flag || flags?.[entry.flag] === true) ? entry : undefined }
}

export function lazyPanel(load: () => Promise<{ default: ComponentType<PanelProps> }>) {
  return lazy(() => lazyImport(load))
}

/** Opens an item's side panel over the current view; the URL carries it so panels are deep-linkable (§13.0). */
export function useItemPanel() {
  const [sp, setSp] = useSearchParams(), panel = usePanels()
  const flags = useMe().flags
  const workActivityOn = flags?.work_screens === true || flags?.people_screens === true
  return (type: string, id: string, origin?: RecordContextOrigin, context?: { personStaffScope?: string }) => {
    if (!panel(type)) return
    const n = new URLSearchParams(sp)
    if (type === 'Person') {
      if (context?.personStaffScope) n.set('personStaffScope', context.personStaffScope)
      else n.delete('personStaffScope')
      if (sp.get('panel') !== `Person:${id}`) { n.delete('personProjectsPage'); n.delete('personProjectsPageSize') }
    }
    if (workActivityOn) { for (const key of ['taskSection', 'taskHistoryActor', 'taskHistoryPage', 'recordTab', 'recordTabItem']) n.delete(key) }
    n.set('panel', `${type}:${id}`)
    setSp(n, origin ? { state: { recordContextReturn: origin } } : undefined)
  }
}

export function PanelHost() {
  const [sp, setSp] = useSearchParams()
  const flags = useMe().flags
  const workActivityOn = flags?.work_screens === true || flags?.people_screens === true
  const navigate = useNavigate(), location = useLocation()
  const raw = sp.get('panel')
  const [type, id] = raw ? [raw.slice(0, raw.indexOf(':')), raw.slice(raw.indexOf(':') + 1)] : [null, null]
  const entry = usePanels()(type)
  const heldTask = useRef<{ id: string; entry: PanelEntry; location: FormNavigationLocation } | null>(null)
  const activeTask = flags?.work_screens === true && type === 'Task' && entry && id
  if (activeTask && entry && id) heldTask.current = { id, entry, location }
  else if (flags?.work_screens === true && type === 'Person' && heldTask.current) {
    const held = heldTask.current, source = taskDetailReturnOrigin(location.state, held.id)
    const url = `${held.location.pathname}${held.location.search}${held.location.hash}`
    if (source?.url !== url || location.pathname !== held.location.pathname) heldTask.current = null
  } else heldTask.current = null
  const task = heldTask.current
  const close = () => {
    const returned = recordContextReturn(location.state)
    if (returned) { navigate(returned.url, { replace: true, state: returned.state }); return }
    const n = new URLSearchParams(sp); n.delete('panel')
    if (type === 'Person') { n.delete('personStaffScope'); n.delete('personProjectsPage'); n.delete('personProjectsPageSize') }
    if (workActivityOn) { for (const key of ['taskSection', 'taskHistoryActor', 'taskHistoryPage', 'recordTab', 'recordTabItem']) n.delete(key) }
    setSp(n)
  }
  const closePanel = () => {
    const returned = type === 'Person' ? recordContextReturn(location.state) : null
    if (!returned) { leave(close); return }
    const destination = new URL(returned.url, 'http://hub.local')
    leave(close, { currentLocation: location, nextLocation: { pathname: destination.pathname,
      search: destination.search, hash: destination.hash, state: returned.state } })
  } // FR-SAV-04: only forms that prove this exact return preserves their input may bypass the question.
  if ((!entry && !task) || !id) return null
  const Comp = entry?.component, TaskComp = task?.entry.component
  return (
    <Sheet open onOpenChange={(o) => !o && closePanel()}>
      <SheetContent side="right" className="w-full gap-0 overflow-y-auto p-0 sm:max-w-none xl:max-w-[560px] [&>button:last-child]:hidden">
        <SheetDescription className="sr-only">{t('panel.description')}</SheetDescription>
        {/* Header bar (design §6 detail panel): the item type, then "open full page" and close; it stays put while the item scrolls. */}
        <div className="sticky top-0 z-10 flex min-h-14 items-center justify-between gap-2 border-b bg-card py-2 pl-4 pr-2">
          <SheetTitle className="text-sm font-semibold">{runtimeText(`itemType.${type}`)}</SheetTitle>
          <div className="flex items-center gap-1">
            {entry?.fullPage && <Button variant="ghost" size="sm" onClick={() => navigate(entry.fullPage!(id))}><ExternalLink className="size-4" aria-hidden />{t('panel.fullPage')}</Button>}
            <Button variant="ghost" size="icon" onClick={closePanel} aria-label={t('common.close')}><X className="size-5" aria-hidden /></Button>
          </div>
        </div>
        {/* Keyed by item: a field's unsaved text, "Not saved" and timed retry belong to the record they were typed for (FR-SAV-02). */}
        {task && TaskComp && <div key={`retained-task:${task.id}`} hidden={!activeTask} inert={!activeTask ? true : undefined}
          aria-hidden={!activeTask ? true : undefined} data-retained-task={task.id}>
          <Suspense fallback={<Loading rows={8} />}><TaskComp key={`Task:${task.id}`} id={task.id} onClose={close} /></Suspense>
        </div>}
        {(!task || !activeTask) && Comp && <Suspense fallback={<Loading rows={8} />}><Comp key={`${type}:${id}`} id={id} onClose={close} /></Suspense>}
        {!Comp && task && <p className="p-4 text-sm text-muted-foreground">{t('common.unavailable')}</p>}
      </SheetContent>
    </Sheet>
  )
}
