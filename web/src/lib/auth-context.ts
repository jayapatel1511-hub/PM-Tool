import type { components } from '@/gen/api'
import type { Perm } from '@/lib/types'
import { createContext, useContext } from 'react'

/** The signed-in person, derived from the contract so a server rename of a field fails the build. */
export type Me = components['schemas']['MeView']

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
/** A whole-project allocation shift is the project manager's (AllocationShift.cs demands Permissions.IsPM); selected rows are checked one by one. */
export const offersWholeProjectShift = (permissions: { isPm: boolean }) => permissions.isPm
/** Coordination choices narrowed to the disciplines the server says this person manages (CoordinationOptions.manageDisciplineIds). */
export const managedDisciplineChoices = <T extends { value: string }>(choices: T[], options: { manageDisciplineIds: string[] }) => choices.filter(choice => options.manageDisciplineIds.includes(choice.value))
