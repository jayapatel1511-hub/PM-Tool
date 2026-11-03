import { useId, type ReactNode } from 'react'
import { useLocation } from 'react-router'
import { PersonName } from './people'
import { useItemPanel } from './panel-host'
import { useFlag } from '@/lib/auth'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

// A stable DOM identifier, never an authority token. Hash the complete source/key so encoded labels stay bounded.
const focusHash = (value: string) => {
  let hash = 2166136261
  for (let i = 0; i < value.length; i++) hash = Math.imul(hash ^ value.charCodeAt(i), 16777619)
  return (hash >>> 0).toString(36)
}

/** Passive, known-ID names only. Pickers and existing destination links keep their own action. */
export function PersonPanelTrigger({ userId, name, active = true, staffScope, className, children, focusKey }: {
  userId: string; name: string; active?: boolean; staffScope?: string | null; className?: string; children?: ReactNode; focusKey?: string
}) {
  const enabled = useFlag('people_screens'), open = useItemPanel(), location = useLocation(), triggerId = useId()
  // Explicit record/role keys survive a source panel or nested list component being remounted.
  const sourcePanel = new URLSearchParams(location.search).get('panel') ?? ''
  const focusId = focusKey ? `person-trigger-${focusHash(`${location.pathname}|${sourcePanel}|${userId}|${focusKey}`)}-${encodeURIComponent(focusKey).slice(0, 64)}-${userId}` : triggerId
  const label = children ?? <PersonName name={name} active={active} />
  if (!enabled) return <>{label}</>
  return <button id={focusId} type="button" data-person-panel-trigger={userId} aria-haspopup="dialog" aria-label={t('personPanel.open', { name })}
    className={cn('min-w-0 rounded-sm text-left text-primary underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring', className)}
    onClick={event => { event.stopPropagation(); open('Person', userId, captureRecordContextOrigin(location, event.currentTarget), { personStaffScope: staffScope ?? undefined }) }}>{label}</button>
}
