// Local form drafts (FR-SAV-03) and the leave guard (FR-SAV-04, FR-SAV-06), packet 051. A draft lives in this browser only (051-D2):
// it is never sent to the server and is dropped after `draft_keep_days`. Storage can be missing or full (private window, blocked
// site data), so every access is guarded; a draft that could not be written does not count as a draft, and the leave guard asks.

const PREFIX = 'draft.'
const DAY = 86_400_000

type Stored = { at: string; value: unknown }

/** `draft.<user>.<form>.<context>`, e.g. `draft.<user>.task.new.<projectId>` (FR-SAV-03). */
export const draftStorageKey = (userId: string, key: string) => `${PREFIX}${userId}.${key}`

/** The stored draft, or null when there is none, it is unreadable or it is older than `keepDays` (those are removed). */
export function readDraft(storeKey: string, keepDays: number): Stored | null {
  try {
    const raw = localStorage.getItem(storeKey)
    if (raw == null) return null
    try {
      const found = JSON.parse(raw) as Stored
      if (typeof found?.at === 'string' && 'value' in found && Date.now() - Date.parse(found.at) <= keepDays * DAY) return found
    } catch { /* unreadable: dropped below */ }
    localStorage.removeItem(storeKey)
  } catch { /* storage unavailable */ }
  return null
}

/** Writes the draft; false when the browser would not keep it. `at` keeps the time of a restored draft that has not changed. */
export function writeDraft(storeKey: string, value: unknown, at = new Date().toISOString()): boolean {
  try { localStorage.setItem(storeKey, JSON.stringify({ at, value })); return true } catch { return false }
}

export function clearDraft(storeKey: string) {
  try { localStorage.removeItem(storeKey) } catch { /* nothing to remove */ }
}

/** Drops every draft of this browser older than `keepDays`, whoever wrote it. */
export function pruneDrafts(keepDays: number) {
  try {
    for (let i = localStorage.length - 1; i >= 0; i--) {
      const key = localStorage.key(i)
      if (key?.startsWith(PREFIX)) readDraft(key, keepDays)
    }
  } catch { /* storage unavailable */ }
}

/** FR-SAV-06: with the flag on (the page frame sets this mark), the Reload of the build-update notice can be pressed while a dialog or
 *  panel is open: a click on a notice neither falls through to the overlay nor closes what is open under it. */
export const GUARD_TOASTS = 'data-guard-toasts'
export const onToast = (target: EventTarget | null) =>
  document.documentElement.hasAttribute(GUARD_TOASTS) && target instanceof Element && target.closest('[data-sonner-toaster]') != null

// ---- The leave guard: forms holding typed input that nothing has saved ----

type Form = { unsaved: () => boolean; discard: () => void }
const forms = new Set<Form>()
let listening = false
const warnOnUnload = (e: BeforeUnloadEvent) => { if (hasUnsaved()) { e.preventDefault(); e.returnValue = '' } }

/** A mounted form says whether it holds input that is neither saved nor drafted. Returns the way to unregister. */
export function registerForm(form: Form) {
  forms.add(form)
  // The browser's own prompt is used for closing the tab and reloading (FR-SAV-04).
  if (!listening) { listening = true; window.addEventListener('beforeunload', warnOnUnload) }
  return () => { forms.delete(form) }
}

export const hasUnsaved = () => { for (const f of forms) if (f.unsaved()) return true; return false }

/** Leaving the page abandons what these forms hold; a form that stays on screen (the New dialogs live in the page frame) starts empty. */
export const discardUnsaved = () => { for (const f of [...forms]) if (f.unsaved()) f.discard() }

type Ask = { proceed: () => void; cancel?: () => void }
let asking: Ask | null = null
const listeners = new Set<() => void>()
const emit = () => listeners.forEach((l) => l())

/** The "Keep editing or discard?" question for the host to show. */
export const leaveQuestion = {
  subscribe: (l: () => void) => { listeners.add(l); return () => { listeners.delete(l) } },
  get: () => asking,
  /** Ask now, whatever the forms say (the router has already stopped a navigation). */
  show: (ask: Ask) => { asking = ask; emit() },
  answer: (discard: boolean) => { const a = asking; asking = null; emit(); if (discard) a?.proceed(); else a?.cancel?.() },
}

/** Leaving through `proceed` (closing a dialog, the Reload action): at once when nothing typed would be lost, otherwise after the
 *  question. A form with a draft, or with no change, never asks (FR-SAV-04). */
export function leave(proceed: () => void) {
  if (hasUnsaved()) leaveQuestion.show({ proceed }); else proceed()
}
