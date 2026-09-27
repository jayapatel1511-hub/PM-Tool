import type { ComponentType } from 'react'

// Item-panel parts delivered by later packets: comments (006), document links (006).
export interface ItemPartProps { type: string; id: string; projectId: string; inheritedFrom?: string }
// "Raise from here" on task and deliverable panels (014): a risk or issue pre-linked to the item.
export interface RaiseProps { projectId: string; targetType: 'Task' | 'Deliverable'; targetId: string; targetKey: string; targetName: string; disciplineId?: string | null }
export const ItemSlots: { Comments: ComponentType<ItemPartProps> | null; Links: ComponentType<ItemPartProps> | null; Raise: ComponentType<RaiseProps> | null } = { Comments: null, Links: null, Raise: null }

export function CommentsSlot(props: ItemPartProps) {
  const C = ItemSlots.Comments
  return C ? <C {...props} /> : null
}

export function LinksSlot(props: ItemPartProps) {
  const C = ItemSlots.Links
  return C ? <C {...props} /> : null
}

export function RaiseSlot(props: RaiseProps) {
  const C = ItemSlots.Raise
  return C ? <C {...props} /> : null
}
