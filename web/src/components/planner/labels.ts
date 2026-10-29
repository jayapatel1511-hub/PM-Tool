import type { components } from '@/gen/api'
import { t, type TextKey } from '@/lib/i18n'

type Schemas = components['schemas']
export type PlannerEntry = Schemas['EntryRow']
export type PlannerCell = Schemas['CellRow']
export type ApprovedAllocation = Schemas['ApprovedRow']
/** `partial`: this person has planned work on projects the viewer cannot see (AC-PLN-12), so their totals are partial.
 *  `kept`: §45.7 (flag.planner_kept_rows), returned only because the viewer just edited it; it no longer matches the filters. */
export type PlannerPerson = Schemas['PlanningPersonRow']
export type UnfilledRole = Schemas['UnfilledRole']
export type PlannerGridData = Schemas['GridResult']

export const CONFIDENCE_KEYS: Record<string, TextKey> = { Confirmed: 'planner.confidence.confirmed', Expected: 'planner.confidence.expected', Possible: 'planner.confidence.possible' }
export const VISIBILITY_KEYS: Record<string, TextKey> = { Draft: 'planner.visibility.privateDraft', Published: 'planner.visibility.proposed', Confirmed: 'planner.visibility.confirmedAssignment', Self: 'planner.visibility.selfEntered' }
export const APPROVAL_KEYS: Record<string, TextKey> = { Confirmed: 'planner.approval.confirmed' }
// Source is a planning origin, not a confidence or visibility state. Keep the
// five canonical Tuesday accents distinct and let the other origins stay quiet.
export const SOURCE_TONES: Record<string, string> = { MajorProject: 'blue', OtherProject: 'mint', Proposal: 'lavender', BusinessDevelopment: 'neutral', Admin: 'amber', Supervision: 'neutral', Training: 'peach', InternalInitiative: 'neutral', FieldWork: 'neutral', Other: 'neutral' }
export const SOURCE_KEYS: Record<string, TextKey> = { MajorProject: 'planner.source.majorProject', OtherProject: 'planner.source.otherProject', Proposal: 'planner.source.proposal', BusinessDevelopment: 'planner.source.businessDevelopment', Admin: 'planner.source.admin', Supervision: 'planner.source.supervision', Training: 'planner.source.training', InternalInitiative: 'planner.source.internalInitiative', FieldWork: 'planner.source.fieldWork', Other: 'planner.source.other' }

export const confidenceLabel = (value: string) => t(CONFIDENCE_KEYS[value] ?? 'planner.unknown')
export const visibilityLabel = (value: string, self = false) => t(VISIBILITY_KEYS[self ? 'Self' : value] ?? 'planner.unknown')
export const approvalLabel = (value: string) => t(APPROVAL_KEYS[value] ?? 'planner.unknown')
export const SOURCE_OPTIONS = Object.keys(SOURCE_KEYS)
export const sourceLabel = (value: string) => t(SOURCE_KEYS[value] ?? 'planner.source.other')
export const sourceTone = (value: string) => SOURCE_TONES[value] ?? 'neutral'
const INDICATOR_KEYS: Record<string, TextKey> = { OverPlanned: 'planner.indicator.over', UnderPlanned: 'planner.indicator.under', StalePlan: 'planner.indicator.stale' }
export const indicatorLabel = (value: string) => t(INDICATOR_KEYS[value] ?? 'planner.unknown')
export const hoursLabel = (value: number) => `${value.toFixed(1).replace(/\.0$/, '')} h`
/** Capacity grouping (§40, FR-GRP-02): Reports to (the default), Office, Job title, Home discipline, or none. People with no
 *  value form a "Not set" group, listed last and never hidden. Grouping is done here, over the rows already loaded. */
export const GROUP_BY = ['supervisor', 'office', 'jobTitle', 'homeDiscipline', 'none'] as const
export const GROUP_LABEL: Record<(typeof GROUP_BY)[number], TextKey> = { supervisor: 'staff.group.supervisor', office: 'admin.office', jobTitle: 'admin.jobTitle', homeDiscipline: 'admin.homeDiscipline', none: 'tgroup.none' }
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
