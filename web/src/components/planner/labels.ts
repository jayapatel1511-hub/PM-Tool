import { t, tv } from '@/lib/i18n'

export type PlannerEntry = {
  id: string; personId: string; personName?: string; ownerId?: string; ownerName?: string; ownerKind?: string
  label: string; sourceCategory: string; projectId?: string | null; projectNumber?: string | null; projectName?: string | null
  projectDisciplineId?: string | null; disciplineName?: string | null; hoursPerWeek: number; startWeek: string; endWeek: string
  confidence: string; visibility: string; notes?: string | null; lastValidatedAt?: string; stale?: boolean; warnings?: string[]
  canEdit?: boolean; canChangeVisibility?: boolean; correctionOnly?: boolean; rowVersion: number; matchesFilter?: boolean
}

export type PlannerCell = {
  week: string; capacity: number; timeAway: number; additionalAvailability?: number; approved: number; confirmed: number
  expected: number; possible: number; remaining: number; taskEstimates: number; unestimatedTasks: number
  overPlanned: boolean; underPlanned: boolean; staleEntries: number; ownDraftHours?: number
}

export type ApprovedAllocation = { allocationId: string; projectId?: string; projectNumber?: string; projectName?: string; canOpen: boolean; weeks: { week: string; hours: number }[] }
export type PlannerPerson = {
  id: string; displayName: string; supervisorId?: string; supervisorName?: string; weeklyCapacity: number
  officeId?: string | null; jobTitle?: string | null; homeDisciplineId?: string | null
  canCreateSelfEntry: boolean; canCreateManagerEntry: boolean; canRecordTimeAway: boolean; indicators: string[]
  cells: PlannerCell[]; entries: PlannerEntry[]; approvedAllocations: ApprovedAllocation[]
}
export type PlannerGridData = { weeks: string[]; currentWeek: string; evaluatedAt?: string; partialView: boolean; draftAccess: boolean; settings: { horizonWeeks: number; overPct: number; underPct: number; underWeeks: number; staleDays: number; maxHoursPerWeek: number }; people: PlannerPerson[] }

export const CONFIDENCE_KEYS: Record<string, string> = { Confirmed: 'planner.confidence.confirmed', Expected: 'planner.confidence.expected', Possible: 'planner.confidence.possible' }
export const VISIBILITY_KEYS: Record<string, string> = { Draft: 'planner.visibility.privateDraft', Published: 'planner.visibility.proposed', Confirmed: 'planner.visibility.confirmedAssignment', Self: 'planner.visibility.selfEntered' }
export const APPROVAL_KEYS: Record<string, string> = { Confirmed: 'planner.approval.confirmed' }
// Source is a planning origin, not a confidence or visibility state. Keep the
// five canonical Tuesday accents distinct and let the other origins stay quiet.
export const SOURCE_TONES: Record<string, string> = { MajorProject: 'blue', OtherProject: 'mint', Proposal: 'lavender', BusinessDevelopment: 'neutral', Admin: 'amber', Supervision: 'neutral', Training: 'peach', InternalInitiative: 'neutral', FieldWork: 'neutral', Other: 'neutral' }
export const SOURCE_KEYS: Record<string, string> = { MajorProject: 'planner.source.majorProject', OtherProject: 'planner.source.otherProject', Proposal: 'planner.source.proposal', BusinessDevelopment: 'planner.source.businessDevelopment', Admin: 'planner.source.admin', Supervision: 'planner.source.supervision', Training: 'planner.source.training', InternalInitiative: 'planner.source.internalInitiative', FieldWork: 'planner.source.fieldWork', Other: 'planner.source.other' }

export const confidenceLabel = (value: string) => t(CONFIDENCE_KEYS[value] ?? 'planner.unknown')
export const visibilityLabel = (value: string, self = false) => t(VISIBILITY_KEYS[self ? 'Self' : value] ?? 'planner.unknown')
export const approvalLabel = (value: string) => t(APPROVAL_KEYS[value] ?? 'planner.unknown')
export const SOURCE_OPTIONS = Object.keys(SOURCE_KEYS)
export const sourceLabel = (value: string) => t(SOURCE_KEYS[value] ?? 'planner.source.other')
export const sourceTone = (value: string) => SOURCE_TONES[value] ?? 'neutral'
export const indicatorLabel = (value: string) => t(({ OverPlanned: 'planner.indicator.over', UnderPlanned: 'planner.indicator.under', StalePlan: 'planner.indicator.stale' } as Record<string, string>)[value] ?? 'planner.unknown')
export const hoursLabel = (value: number) => `${value.toFixed(1).replace(/\.0$/, '')} h`
export const storedStatusLabel = (value: string) => tv(value)

/** Capacity grouping (§40, FR-GRP-02): Reports to (the default), Office, Job title, Home discipline, or none. People with no
 *  value form a "Not set" group, listed last and never hidden. Grouping is done here, over the rows already loaded. */
export const GROUP_BY = ['supervisor', 'office', 'jobTitle', 'homeDiscipline', 'none'] as const
export const GROUP_LABEL: Record<string, string> = { supervisor: 'staff.group.supervisor', office: 'admin.office', jobTitle: 'admin.jobTitle', homeDiscipline: 'admin.homeDiscipline', none: 'tgroup.none' }
type Groupable = { supervisorName?: string | null; officeId?: string | null; jobTitle?: string | null; homeDisciplineId?: string | null }
export function groupPeople<T extends Groupable>(rows: T[], by: string, ref?: { offices?: { id: string; name: string }[]; disciplines?: { id: string; name: string }[] }): [string, T[]][] {
  const notSet = t('staff.group.notSet')
  const label = (p: T): string => {
    switch (by) {
      case 'none': return ''
      case 'office': return ref?.offices?.find((o) => o.id === p.officeId)?.name ?? notSet
      case 'jobTitle': return p.jobTitle?.trim() || notSet
      case 'homeDiscipline': return ref?.disciplines?.find((d) => d.id === p.homeDisciplineId)?.name ?? notSet
      default: return p.supervisorName ? t('workload.reportsTo', { name: p.supervisorName }) : t('workload.noSupervisor')
    }
  }
  const groups = new Map<string, T[]>()
  for (const p of rows) { const g = label(p); groups.set(g, [...(groups.get(g) ?? []), p]) }
  const notSetLast = (a: string) => (a === notSet ? 1 : 0)
  return [...groups.entries()].sort(([a], [b]) => notSetLast(a) - notSetLast(b))
}
