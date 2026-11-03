import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import type { PreservesFormNavigation } from '@/lib/drafts'

const FormNavigationContext = createContext<PreservesFormNavigation | undefined>(undefined)
const FormAvailabilityContext = createContext<boolean | undefined>(undefined)

/** Only mounted record forms explicitly enclosed by their owning surface inherit this proof. */
export function FormNavigationScope({ preservesNavigation, available, children }: { preservesNavigation?: PreservesFormNavigation; available?: boolean; children: ReactNode }) {
  const parentPreserver = useContext(FormNavigationContext), parentAvailability = useContext(FormAvailabilityContext)
  const currentAvailability = parentAvailability === false || available === false ? false : available ?? parentAvailability
  return <FormNavigationContext.Provider value={preservesNavigation ?? parentPreserver}>
    <FormAvailabilityContext.Provider value={currentAvailability}>{children}</FormAvailabilityContext.Provider>
  </FormNavigationContext.Provider>
}

/** Portals share React context, so an unavailable record also closes its owned menu/dialog surfaces.
 * Component form state stays mounted; outside an opted-in scope the primitive receives its original props. */
export function useAvailablePortalOpen(props: { open?: boolean; defaultOpen?: boolean; onOpenChange?: (open: boolean) => void }, restoreOnAvailability = true) {
  const available = useContext(FormAvailabilityContext), [requestedOpen, setRequestedOpen] = useState(props.defaultOpen ?? false)
  const dismissed = useRef(false)
  useEffect(() => {
    if (available !== false) { dismissed.current = false; return }
    if (restoreOnAvailability || dismissed.current) return
    dismissed.current = true
    setRequestedOpen(false)
    if (props.open ?? requestedOpen) props.onOpenChange?.(false)
  }, [available, restoreOnAvailability, props.open, props.onOpenChange, requestedOpen])
  if (available === undefined) return {}
  return {
    open: available && (props.open ?? requestedOpen),
    onOpenChange: (next: boolean) => {
      if (!available) return
      setRequestedOpen(next); props.onOpenChange?.(next)
    },
  }
}

export function useFormNavigationPreserver(): PreservesFormNavigation {
  const inherited = useContext(FormNavigationContext), latest = useRef(inherited)
  latest.current = inherited
  return useCallback((from, to) => latest.current?.(from, to) ?? false, [])
}
