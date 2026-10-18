import { useEffect, useRef } from 'react'
import { matchRoutes, useLocation } from 'react-router'
import { routes } from '@/app/routes'
import { post } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'

let navigationStarted = performance.now()
export function trackRouteTiming(router: { subscribe: (fn: (state: { location: { key: string }; navigation: { location?: { key: string } } }) => void) => () => void }) {
  let key: string | undefined
  router.subscribe(state => { const next = state.navigation.location?.key ?? state.location.key; if (key !== next) { key = next; navigationStarted = performance.now() } })
}

/** Route patterns only; failed aggregate sends are ignored and never retried. */
export function useRouteCounter(_pathname: string) {
  const enabled = useFlag('route_counter')
  const active = useMe().settings.routeCounterEnabled === true
  const location = useLocation()
  const sent = useRef<string | null>(null)
  useEffect(() => {
    if (!enabled || !active || sent.current === location.key) return
    const matched = matchRoutes(routes, location)
    const route = matched?.map(m => m.route.path).filter(p => p && p !== '*').join('/')
    if (!route) return
    sent.current = location.key
    const elapsedMs = Math.min(60000, Math.max(0, Math.round(performance.now() - navigationStarted)))
    void post('route-counts', { route, elapsedMs }).catch(() => undefined)
  }, [enabled, active, location])
}
