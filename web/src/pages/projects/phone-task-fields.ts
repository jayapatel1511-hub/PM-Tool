import type { TaskRow } from './Tasks'

/** Compact only absent optional fields; unknown estimates, missing due dates and owners still matter. */
export function showPhoneTaskField(id: string, r: TaskRow) {
  if (id === 'deliverable') return !!r.deliverableId
  if (id === 'reviewer') return !!r.reviewerName || r.requiresReview
  if (id === 'milestone') return !!r.milestoneKey
  if (id === 'start') return !!r.startDate
  return true
}
