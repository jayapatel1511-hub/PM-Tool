import type { components } from '@/gen/api'

type Detail = components['schemas']['ActionDetail']
type LinkRow = components['schemas']['ItemLinkRow']
const compare = (a: string, b: string) => a < b ? -1 : a > b ? 1 : 0

/** Stored ItemLinks take precedence over the same direct reference, retaining their
 * relationship and link ID. Conversion is already shown once in the follows-task line. */
export function projectActionRelations(detail: Detail): LinkRow[] {
  const rows = [...detail.links]
  if (detail.task && !detail.action.converted && !rows.some(l => l.targetType === 'Task' && l.targetId === detail.task!.id))
    rows.push({ id: `direct-task:${detail.task.id}`, targetType: 'Task', targetId: detail.task.id, relation: 'related', key: detail.task.key,
      name: detail.task.name, status: detail.task.status, date: null, person: null })
  if (detail.decision && !rows.some(l => l.targetType === 'Decision' && l.targetId === detail.decision!.id))
    rows.push({ id: `direct-decision:${detail.decision.id}`, targetType: 'Decision', targetId: detail.decision.id, relation: 'related', key: detail.decision.key,
      name: detail.decision.subject, status: detail.decision.status, date: null, person: null })
  const seen = new Set<string>()
  return rows.sort((a, b) => compare(a.relation, b.relation) || compare(a.key, b.key) || compare(a.targetId, b.targetId)
    || compare(a.targetType, b.targetType) || compare(a.id, b.id)).filter(l => {
    if (detail.action.converted && l.targetType === 'Task' && l.targetId === detail.task?.id) return false
    const key = `${l.targetType}:${l.targetId}`
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
}
