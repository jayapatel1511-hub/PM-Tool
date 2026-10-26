import type { Perm } from '@/lib/types'
import { createContext, useContext } from 'react'

export interface Me {
  id: string
  displayName: string
  email: string
  jobTitle?: string
  officeId?: string
  supervisorId?: string
  isTemplateEditor: boolean
  roles: { role: string; source: string }[]
  systemRoles: string[]
  capabilities: {
    createProject: boolean; createTask: boolean; portfolio: boolean; workload: boolean; staff: boolean; allStaff: boolean
    admin: boolean; templates: boolean; readOnly: boolean; directReports: number
  }
  settings: {
    dateFormat: string; orgTimeZone: string; today: string; idleTimeoutHours: number; restrictedProjectsEnabled: boolean
    allowSelfReview: boolean; taskDueSoonDays: number; milestoneApproachingDays: number; chainDepthLimit: number
    defaultWeeklyCapacityHours: number; coordinationLookaheadWeeks: number; workingDaysEnabled: boolean; routeCounterEnabled?: boolean; activityBurstMinutes?: number
    /** Present only while flag.data_entry_safety is on (§55.2). */
    autosaveDelayMs?: number; draftKeepDays?: number
  }
  preferences: { digestEnabled: boolean; digestTimeLocal?: string; denseRows: boolean; weeklySummaryEnabled: boolean }
  flags?: Record<string, boolean>
}

export interface AuthState { me: Me; signOut: () => void }
export const Ctx = createContext<AuthState | null>(null)
export const useMe = () => useContext(Ctx)!.me
export const useFlag = (name: string) => useMe().flags?.[name] === true
export const useAuth = () => useContext(Ctx)!

/** FR-FIG-09 (D5): how a control shows a server permission flag. Offered when the server allows it. Where the account or the
 *  project's status refuses it, the control stays visible, disabled, with the server's reason as its title, as it was offered
 *  before the flags; otherwise it is hidden. */
export function useOffer() {
  const readOnly = useMe().capabilities.readOnly
  return (perm: Perm, status: string) => ({
    shown: perm.ok || readOnly || ['Complete', 'Archived', 'Cancelled'].includes(status),
    props: { disabled: !perm.ok, title: perm.ok ? undefined : perm.reason ?? undefined },
  })
}
export type Offer = ReturnType<ReturnType<typeof useOffer>>
