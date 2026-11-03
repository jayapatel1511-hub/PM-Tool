import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { useFlag, useMe } from '@/lib/auth'
import { invalidateWorkSeen } from '@/lib/keys'
import { Button } from '@/components/ui/button'
import { ErrorBanner } from './common'
import { useVisibleWorkDetailSeen, useWorkDocumentVisible, type WorkSeenDecoration, type WorkSeenReadState } from './work-seen'

/** Owns a committed detail surface; query/cache access by itself never acknowledges work. */
export function useWorkSeenDetailOwner({ itemType, itemId, sourceVersion, seen, read, visible = true }: {
  itemType: string; itemId: string; sourceVersion?: number; seen?: WorkSeenDecoration | null
  read: WorkSeenReadState & { refetch: () => Promise<unknown> }; visible?: boolean
}) {
  const enabled = useFlag('work_screens'), me = useMe(), qc = useQueryClient()
  const [params] = useSearchParams(), documentVisible = useWorkDocumentVisible()
  const surfaceRef = useRef<HTMLDivElement>(null)
  const [surfaceVisible, setSurfaceVisible] = useState(true)
  const observed = useRef<{ node: HTMLDivElement; cleanup: () => void } | null>(null)
  // Native modal/inert boundaries can hide a held parent without changing its route or query state.
  // Observe only the committed surface and its ancestors, never the whole document subtree.
  useLayoutEffect(() => {
    const node = enabled ? surfaceRef.current : null
    if (observed.current?.node === node) return
    observed.current?.cleanup(); observed.current = null
    if (!node) return
    const update = () => {
      const rect = node.getBoundingClientRect()
      setSurfaceVisible(node.isConnected && !node.closest('[hidden],[aria-hidden="true"],[inert],[data-state="closed"]') && rect.width > 0 && rect.height > 0)
    }
    const mutations = new MutationObserver(update)
    for (let element: HTMLElement | null = node; element; element = element.parentElement)
      mutations.observe(element, { attributes: true, attributeFilter: ['hidden', 'aria-hidden', 'inert', 'data-state', 'style', 'class'] })
    const resize = new ResizeObserver(update); resize.observe(node)
    observed.current = { node, cleanup: () => { mutations.disconnect(); resize.disconnect() } }
    update()
  })
  useEffect(() => () => { observed.current?.cleanup(); observed.current = null }, [])
  const target = params.get('panel')
  const exposed = visible && surfaceVisible && documentVisible && (!target || target.toLowerCase() === `${itemType}:${itemId}`.toLowerCase())
  const refetch = read.refetch
  useEffect(() => {
    if (enabled && exposed) void refetch()
  }, [enabled, exposed, itemType, itemId, me.id, refetch])
  const onCommitted = useCallback(() => invalidateWorkSeen(qc), [qc])
  const state = useVisibleWorkDetailSeen({ itemType, itemId,
    detail: sourceVersion === undefined ? null : { itemType, itemId, sourceVersion, seen },
    read, visible: exposed, surfaceRef, onCommitted })
  return { surfaceRef, state }
}

export function WorkSeenDetailNotice({ state }: { state: ReturnType<typeof useWorkSeenDetailOwner>['state'] }) {
  if (!state.error) return null
  return <div className="mx-4 my-2"><ErrorBanner error={state.error} /><Button variant="outline" size="sm" onClick={state.retry}>{state.retryLabel}</Button></div>
}
