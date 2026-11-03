import { AccentDot } from '@/components/hub/common'
import { useFlag } from '@/lib/auth-context'
import { cn } from '@/lib/utils'
import type { ReactNode } from 'react'
import { ItemMark as ShapeMark } from './item-mark'

export const IDENTITY_COLOURS = [
  { value: '#ec6a3e', label: 'Coral', token: '--project-coral' },
  { value: '#20a36d', label: 'Mint', token: '--project-mint' },
  { value: '#d9467b', label: 'Rose', token: '--project-rose' },
  { value: '#1597cc', label: 'Cyan', token: '--project-cyan' },
  { value: '#84a32a', label: 'Lime', token: '--project-lime' },
] as const
export const resolveIdentityColour = (enabled: boolean, colour?: string | null) => enabled && IDENTITY_COLOURS.some((x) => x.value === colour) ? colour : null
export const identityColourToken = (colour?: string | null) => {
  const identity = IDENTITY_COLOURS.find(x => x.value === colour)
  return identity ? `var(${identity.token})` : undefined
}

/** Stored project identity mark. A missing v2 value stays neutral; only the dark flag uses the legacy hash. */
export function IdentityDot({ id, colour, className }: { id?: string | null; colour?: string | null; className?: string }) {
  const enabled = useFlag('visual_system')
  const stored = resolveIdentityColour(enabled, colour)
  if (!enabled) return <AccentDot id={id} className={className} />
  return <span aria-hidden data-identity-colour={stored ?? undefined} data-identity-state={stored ? 'stored' : 'missing'} className={cn('inline-block size-2.5 shrink-0 rounded-full border-2', className)} style={{ borderColor: identityColourToken(stored) ?? 'var(--line-3)' }} />
}

/** v2 project item mark: shape carries type, fill carries project identity. */
export function ItemMark({ kind, colour, className, fallback }: { kind?: 'Task' | 'Deliverable' | 'Milestone'; colour?: string | null; className?: string; fallback?: ReactNode }) {
  const enabled = useFlag('visual_system')
  const stored = resolveIdentityColour(enabled, colour)
  if (!enabled || !kind) return fallback ?? null
  return <ShapeMark kind={kind} colour={identityColourToken(stored)} className={className} />
}
