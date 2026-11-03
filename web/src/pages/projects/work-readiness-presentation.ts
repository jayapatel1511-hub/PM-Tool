import type { components } from '@/gen/api'
import type { TaskRow } from './Tasks'

type Readiness = components['schemas']['WorkReadinessTask']
type Input = components['schemas']['WorkReadinessInput']
export type ReadinessSourceGroup = {
  key: string
  source: components['schemas']['RecordContextInputView']
  status: string
  members: { task: TaskRow; readiness: Readiness['readiness']; input: Input }[]
}

/** One current Handoff source across the actual shown work; each relationship retains its own exact input. */
export function readinessSourceGroups(rows: TaskRow[], readiness: Readiness[]) {
  const tasks = new Map(rows.map(task => [task.id, task]))
  const groups = new Map<string, ReadinessSourceGroup>()
  for (const item of readiness) {
    const task = tasks.get(item.taskId)
    if (!task || task.status === 'Complete' || task.status === 'Cancelled') continue
    for (const input of item.readiness.inputs) {
      const direct = input.relationship === 'DirectTarget' && input.source.kind === 'Handoff'
      const source = direct ? input.source : input.relationship === 'Constraint' && input.linkedSource?.kind === 'Handoff' ? input.linkedSource : null
      if (!source) continue
      const key = `${source.kind}:${source.id}`
      const group = groups.get(key) ?? { key, source, status: direct ? input.status : input.linkedStatus ?? '', members: [] }
      if (!group.members.some(member => member.task.id === task.id && member.input.source.kind === input.source.kind && member.input.source.id === input.source.id))
        group.members.push({ task, readiness: item.readiness, input })
      groups.set(key, group)
    }
  }
  return [...groups.values()].map(group => ({ ...group, members: [...group.members].sort((a, b) =>
    a.task.key.localeCompare(b.task.key) || a.task.id.localeCompare(b.task.id) || a.input.source.key.localeCompare(b.input.source.key)) }))
    .sort((a, b) => a.source.key.localeCompare(b.source.key) || a.key.localeCompare(b.key))
}
