import { cn } from '@/lib/utils'
import type { CSSProperties } from 'react'
import './semantic-colours.css'

export type MarkKind = 'Task' | 'Deliverable' | 'Milestone'

/** Compact record mark: shape communicates record kind; colour is optional project identity only. */
export function ItemMark({ kind, colour, label, className }: { kind: MarkKind; colour?: string | null; label?: string; className?: string }) {
  return <span role={label ? 'img' : undefined} aria-hidden={label ? undefined : true} aria-label={label} title={label} data-item-kind={kind} data-item-mark className={cn('item-mark', className)} style={colour ? { '--item-mark-colour': colour } as CSSProperties : undefined} />
}
