import type { MouseEvent } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { useFlag } from '@/lib/auth'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'

/** Existing work-record links keep their ordinary href and capture the source at click time. */
export function useWorkRecordOrigin() {
  const enabled = useFlag('screen_patterns')
  const location = useLocation()
  const navigate = useNavigate()
  return (event: MouseEvent<HTMLAnchorElement>) => {
    if (!enabled || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    if (event.currentTarget.target && event.currentTarget.target !== '_self') return
    const target = new URL(event.currentTarget.href)
    if (target.origin !== window.location.origin) return
    event.preventDefault()
    navigate(`${target.pathname}${target.search}${target.hash}`, {
      state: { recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) },
    })
  }
}
