/** Root switch for the dark-shipped visual system (§56.11).
 *
 * Keeping this tiny DOM operation outside Shell makes the lifecycle contract
 * explicit: the attribute is present only while the signed-in shell has the
 * server flag enabled, and cleanup can reliably restore the legacy surface.
 */
export const VISUAL_SYSTEM_ATTRIBUTE = 'data-visual'
export const VISUAL_SYSTEM_VALUE = 'v2'

export function setVisualSystem(enabled: boolean) {
  if (typeof document === 'undefined') return
  if (enabled) document.documentElement.setAttribute(VISUAL_SYSTEM_ATTRIBUTE, VISUAL_SYSTEM_VALUE)
  else document.documentElement.removeAttribute(VISUAL_SYSTEM_ATTRIBUTE)
}
