import type { MouseEvent } from 'react'
import { useLocation } from 'react-router'
import { useItemPanel } from '@/components/hub/panel-host'
import { useFlag } from '@/lib/auth'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'

/** Keep the inspected source panel and its scroll when following a stored relation. */
export function useProjectRelatedPanel() {
  const openPanel = useItemPanel()
  const location = useLocation()
  const enabled = useFlag('screen_patterns')
  return (kind: string, id: string, event: MouseEvent<HTMLElement>) =>
    openPanel(kind, id, enabled ? captureRecordContextOrigin(location, event.currentTarget) : undefined)
}
