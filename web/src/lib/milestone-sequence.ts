/** §12.3's milestone_type order; Other has no place in it. */
export const MILESTONE_TYPES = ['Kickoff', 'Field Work', 'Design Submission', 'Client Workshop', 'Permit Submission', 'Tender', 'Construction', 'IFC', 'Record Drawings', 'Closeout', 'Other'] as const

type Milestone = { id: string; name: string; milestoneType: string; date?: string | null; isCancelled: boolean }
export type SequenceCue = { kind: 'noDate' } | { kind: 'before'; name: string }

/** FR-SCR-66, information only: an undated milestone, or one dated before a milestone whose type comes earlier (the earliest such). */
export function sequenceCue(m: Milestone, all: readonly Milestone[]): SequenceCue | null {
  if (m.isCancelled) return null
  if (!m.date) return { kind: 'noDate' }
  const order = (x: Milestone) => x.milestoneType === 'Other' ? -1 : MILESTONE_TYPES.indexOf(x.milestoneType as typeof MILESTONE_TYPES[number])
  if (order(m) < 0) return null
  const earlier = all.filter(x => x.id !== m.id && !x.isCancelled && x.date && order(x) >= 0 && order(x) < order(m) && x.date > m.date!)
    .sort((a, b) => a.date!.localeCompare(b.date!))[0]
  return earlier ? { kind: 'before', name: earlier.name } : null
}
