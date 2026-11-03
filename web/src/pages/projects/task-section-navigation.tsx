import { useEffect, useRef, useState, type ReactNode, type RefObject } from 'react'
import { useLocation, useSearchParams } from 'react-router'
import { runtimeText } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export const TASK_SECTION_IDS = ['details', 'dependencies', 'review', 'time', 'comments', 'links', 'history'] as const
export type TaskSectionId = typeof TASK_SECTION_IDS[number]
export const taskSectionId = (taskId: string, section: TaskSectionId) => `task-${taskId}-${section}`

export function TaskSection({ taskId, name, enabled, children }: { taskId: string; name: TaskSectionId; enabled: boolean; children: ReactNode }) {
  if (!enabled) return <>{children}</>
  return <section id={taskSectionId(taskId, name)} data-task-section={name} aria-label={runtimeText(`taskSections.${name}`)} tabIndex={-1} className="min-w-0 outline-offset-[-2px]">{children}</section>
}

/** The actual sheet/main owns scrolling. Observation changes only the marker, never focus or route state. */
export function TaskSectionNavigation({ taskId, surface, owner, paused, inspector, unavailable = [] }: {
  taskId: string; surface: RefObject<HTMLDivElement | null>; owner: boolean; paused: boolean; inspector: boolean; unavailable?: TaskSectionId[]
}) {
  const [params] = useSearchParams(), location = useLocation()
  const [active, setActive] = useState<TaskSectionId>('details')
  const nav = useRef<HTMLElement>(null), scrollRoot = useRef<HTMLElement | null>(null), consumed = useRef('')
  const live = useRef({ owner, paused, unavailable }); live.current = { owner, paused, unavailable }
  const requested = params.get('taskSection')
  const exposed = () => {
    const node = surface.current
    if (!node || !live.current.owner || live.current.paused || document.visibilityState === 'hidden' || node.closest('[hidden],[inert],[aria-hidden="true"]')) return false
    const ownDialog = node.closest('[role="dialog"]')
    return ![...document.querySelectorAll<HTMLElement>('[role="dialog"], [data-radix-popper-content-wrapper]')].some(overlay => overlay !== ownDialog && !overlay.contains(node) && overlay.getClientRects().length > 0 && !overlay.closest('[hidden],[aria-hidden="true"],[data-state="closed"]'))
  }
  const offset = () => (surface.current?.closest('[role="dialog"]') ? 56 : 0) + (nav.current?.getBoundingClientRect().height ?? 0) + 8
  const go = (name: TaskSectionId) => {
    if (!exposed() || live.current.unavailable.includes(name)) return
    const target = surface.current?.querySelector<HTMLElement>(`[id="${taskSectionId(taskId, name)}"]`), root = scrollRoot.current
    if (!target || !root) return
    const top = root === document.scrollingElement ? 0 : root.getBoundingClientRect().top
    root.scrollTo({ top: root.scrollTop + target.getBoundingClientRect().top - top - offset(), behavior: 'instant' })
    target.focus({ preventScroll: true }); setActive(name)
  }
  useEffect(() => {
    const node = surface.current
    if (!node) return
    let root: HTMLElement | null = node.parentElement
    while (root && !/(auto|scroll)/.test(getComputedStyle(root).overflowY)) root = root.parentElement
    root ??= document.scrollingElement as HTMLElement | null
    if (!root) return
    const scroller = root
    scrollRoot.current = scroller
    let frame = 0
    const measure = () => {
      frame = 0
      if (!exposed()) return
      const top = (scroller === document.scrollingElement ? 0 : scroller.getBoundingClientRect().top) + offset()
      const sections = TASK_SECTION_IDS.map(name => ({ name, node: node.querySelector<HTMLElement>(`[id="${taskSectionId(taskId, name)}"]`) })).filter(entry => entry.node && !live.current.unavailable.includes(entry.name))
      const above = sections.filter(entry => entry.node!.getBoundingClientRect().top <= top + 8)
      const atEnd = scroller.scrollHeight > scroller.clientHeight && scroller.scrollTop + scroller.clientHeight >= scroller.scrollHeight - 2
      const next = atEnd ? sections.at(-1)?.name : above.at(-1)?.name ?? sections[0]?.name
      if (next) setActive(next)
    }
    const schedule = () => { if (!frame) frame = requestAnimationFrame(measure) }
    const observer = new IntersectionObserver(schedule, { root: scroller === document.scrollingElement ? null : scroller, threshold: [0, 1] })
    for (const name of TASK_SECTION_IDS) { const section = node.querySelector(`[id="${taskSectionId(taskId, name)}"]`); if (section) observer.observe(section) }
    const resize = new ResizeObserver(schedule); resize.observe(scroller); if (nav.current) resize.observe(nav.current)
    const mutation = new MutationObserver(schedule)
    for (let ancestor: HTMLElement | null = node; ancestor; ancestor = ancestor.parentElement) mutation.observe(ancestor, { attributes: true, attributeFilter: ['hidden', 'inert', 'aria-hidden', 'data-state', 'class', 'style'] })
    mutation.observe(document.body, { childList: true })
    scroller.addEventListener('scroll', schedule, { passive: true }); document.addEventListener('visibilitychange', schedule); schedule()
    return () => { observer.disconnect(); resize.disconnect(); mutation.disconnect(); scroller.removeEventListener('scroll', schedule); document.removeEventListener('visibilitychange', schedule); cancelAnimationFrame(frame); scrollRoot.current = null }
  }, [taskId, surface])
  useEffect(() => {
    if (!owner || paused || !requested || !TASK_SECTION_IDS.includes(requested as TaskSectionId) || consumed.current === requested) return
    if (!exposed()) return
    consumed.current = requested; go(requested as TaskSectionId)
  }, [taskId, requested, owner, paused, location.pathname])
  return <nav ref={nav} aria-label={runtimeText('taskSections.navigation')} data-task-section-navigation={taskId} className={cn('sticky z-10 flex min-w-0 flex-wrap gap-1 border-y bg-card px-3 py-2', inspector ? 'top-14' : 'top-0')}>
    {TASK_SECTION_IDS.map(name => <button key={name} type="button" aria-controls={taskSectionId(taskId, name)} aria-current={active === name ? 'location' : undefined} disabled={unavailable.includes(name)} onClick={() => go(name)} className={cn('min-h-9 rounded-md px-2 text-sm disabled:cursor-not-allowed disabled:opacity-60', active === name ? 'bg-muted font-semibold text-foreground' : 'text-muted-foreground hover:bg-muted')}>{runtimeText(`taskSections.${name}`)}</button>)}
  </nav>
}
