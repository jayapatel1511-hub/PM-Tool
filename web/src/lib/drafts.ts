// Local form drafts (FR-SAV-03) and the leave guard (FR-SAV-04, FR-SAV-06), packet 051. A draft lives in this browser only (051-D2):
// it is never sent to the server and is dropped after `draft_keep_days`. Storage can be missing or full (private window, blocked
// site data), so every access is guarded; a draft that could not be written does not count as a draft, and the leave guard asks.

import { recordContextReturn } from './record-context-navigation.ts'

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

// ---- A save the sign-in refusal cut off (FR-SAV-05) ----
// A comment or create form is already safe: its draft is in local storage. An edit-panel field has no draft, so a 401 keeps its typed value
// here, in this tab only (sign-in returns to the same tab), and the same field takes it up again once, when it is on screen again after
// sign-in. The record is the route's path and `panel`, the field its label; other users' entries are left.

const PENDING = 'hub.pendingFields'
type Pending = { user: string; route: string; field: string; value: unknown; version?: number; at: string }
// ponytail: two fields with the same label in one panel share an entry (the first to mount takes it); key by field id if a panel ever has that.
/** The record on screen. A field takes it when it mounts, so a refusal that comes back after the panel moved on is kept for its own record. */
export const pendingRoute = () => `${location.pathname}?${new URLSearchParams(location.search).get('panel') ?? ''}`
const stored = (): Pending[] => { try { const found = JSON.parse(sessionStorage.getItem(PENDING) ?? '[]'); return Array.isArray(found) ? found : [] } catch { return [] } }
const store = (rest: Pending[]) => { try { if (rest.length) sessionStorage.setItem(PENDING, JSON.stringify(rest)); else sessionStorage.removeItem(PENDING) } catch { /* storage unavailable */ } }
const isThis = (p: Pending, user: string, field: string, route = pendingRoute()) => p.user === user && p.field === field && p.route === route

/** Called before the sign-in flow runs: the field's typed value waits for the person to come back, with the row version it was typed
 *  against, so a record changed meanwhile is a conflict and not overwritten (FR-SAV-02). `route` is the field's record. */
export function keepPending(user: string, route: string, field: string, value: unknown, version?: number) {
  const rest = stored().filter((p) => !isThis(p, user, field, route))
  try { sessionStorage.setItem(PENDING, JSON.stringify([...rest, { user, route, field, value, version, at: new Date().toISOString() }])) } catch { /* storage unavailable: nothing to keep it in */ }
}

/** The value this field was saving when sign-in expired, if it is still within `keepDays`; reading does not remove it. */
export function peekPending(user: string, field: string, keepDays: number): { value: unknown; version?: number } | null {
  const found = stored().find((p) => isThis(p, user, field))
  return found && Date.now() - Date.parse(found.at) <= keepDays * DAY ? { value: found.value, version: found.version } : null
}

/** The field has taken its value up again (or it is stale): the one retry is spent. */
export function dropPending(user: string, field: string) {
  store(stored().filter((p) => !isThis(p, user, field)))
}

/** An explicit sign-out takes the person's drafts and waiting fields with it, so the next person on a shared browser finds none; an expired
 *  session keeps them (051-D2). */
export function forgetDrafts(user: string) {
  try {
    for (let i = localStorage.length - 1; i >= 0; i--) {
      const key = localStorage.key(i)
      if (key?.startsWith(draftStorageKey(user, ''))) localStorage.removeItem(key)
    }
  } catch { /* storage unavailable */ }
  store(stored().filter((p) => p.user !== user))
}

/** FR-SAV-06: with the flag on (the page frame sets this mark), the Reload of the build-update notice can be pressed while a dialog or
 *  panel is open: a click on a notice neither falls through to the overlay nor closes what is open under it. */
export const GUARD_TOASTS = 'data-guard-toasts'
export const onToast = (target: EventTarget | null) =>
  document.documentElement.hasAttribute(GUARD_TOASTS) && target instanceof Element && target.closest('[data-sonner-toaster]') != null

// ---- The leave guard: forms holding typed input that nothing has saved ----

export type FormNavigationLocation = { pathname: string; search: string; hash: string; state?: unknown }
export type FormNavigation = { currentLocation: FormNavigationLocation; nextLocation: FormNavigationLocation }
export type PreservesFormNavigation = (from: FormNavigationLocation, to: FormNavigationLocation) => boolean

const taskPresentationKeys = ['taskSection', 'taskHistoryActor', 'taskHistoryPage', 'groupChanges', 'recordTab', 'recordTabItem']
const personPresentationKeys = ['personStaffScope', 'personProjectsPage', 'personProjectsPageSize']
const taskSections = ['details', 'dependencies', 'review', 'time', 'comments', 'links', 'history']
const locationUrl = (location: FormNavigationLocation) => `${location.pathname}${location.search}${location.hash}`
const parsedLocation = (url: string): FormNavigationLocation => {
  const parsed = new URL(url, 'http://hub.local')
  return { pathname: parsed.pathname, search: parsed.search, hash: parsed.hash }
}
const taskOwner = (location: FormNavigationLocation, taskId: string, ownerId: string) => {
  const params = new URLSearchParams(location.search), panels = params.getAll('panel')
  if (panels.length) return panels.length === 1 && [taskId, ownerId].some(id => panels[0] === `Task:${id}`)
  try { return [taskId, ownerId].some(id => decodeURIComponent(location.pathname) === `/tasks/${id}`) } catch { return false }
}
const personOwner = (location: FormNavigationLocation) => {
  const panels = new URLSearchParams(location.search).getAll('panel')
  return panels.length === 1 && /^Person:[A-Za-z0-9-]+$/.test(panels[0])
}
const taskPresentation = (location: FormNavigationLocation, taskId: string, ownerId: string) => {
  const params = new URLSearchParams(location.search)
  if (taskPresentationKeys.some(key => params.getAll(key).length > 1)) return false
  const section = params.get('taskSection'), actor = params.get('taskHistoryActor'), page = params.get('taskHistoryPage')
  const item = params.get('recordTabItem'), tab = params.get('recordTab'), group = params.get('groupChanges')
  return (!section || taskSections.includes(section))
    && (!actor || actor.startsWith(`${taskId}:`) && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(actor.slice(taskId.length + 1)))
    && (!page || page.startsWith(`${taskId}:`) && /^[1-9][0-9]*$/.test(page.slice(taskId.length + 1)))
    && (!item || [taskId, ownerId].some(id => item === `Task:${id}`))
    && (!tab || ['comments', 'history'].includes(tab)) && (!group || ['true', 'false'].includes(group))
}
const presentationBase = (location: FormNavigationLocation, person = false) => {
  const params = new URLSearchParams(location.search)
  for (const key of [...taskPresentationKeys, ...(person ? ['panel', ...personPresentationKeys] : [])]) params.delete(key)
  return JSON.stringify([location.pathname, location.hash, [...params].sort(([ak, av], [bk, bv]) => ak.localeCompare(bk) || av.localeCompare(bv))])
}

/** The exact Task source under a bounded chain of Person overlays; modal sources remain guarded. */
export function taskDetailReturnOrigin(state: unknown, taskId: string, ownerId = taskId) {
  let current = state
  for (let depth = 0; depth < 8; depth++) {
    const returned = recordContextReturn(current)
    if (!returned) return null
    const source = parsedLocation(returned.url)
    const slot = returned.state?.recordContextRestore?.surface?.slot
    if (slot === 'dialog-body' || slot === 'dialog-content') return null
    if (taskOwner(source, taskId, ownerId)) return returned
    if (!personOwner(source)) return null
    current = returned.state
  }
  return null
}

/** Only same-Task presentation or its recorded Person round trip keeps mounted memory input. */
export function preservesTaskDetailNavigation(taskId: string, ownerId = taskId): PreservesFormNavigation {
  return (from, to) => {
    const fromTask = taskOwner(from, taskId, ownerId), toTask = taskOwner(to, taskId, ownerId)
    if (fromTask && toTask) return taskPresentation(from, taskId, ownerId) && taskPresentation(to, taskId, ownerId)
      && presentationBase(from) === presentationBase(to)
    if (from.pathname !== to.pathname || from.hash !== to.hash) return false
    if (fromTask && personOwner(to)) {
      const source = taskDetailReturnOrigin(to.state, taskId, ownerId)
      return !!source && source.url === locationUrl(from) && taskPresentation(from, taskId, ownerId)
        && presentationBase(from, true) === presentationBase(to, true)
    }
    if (personOwner(from) && toTask) {
      const source = taskDetailReturnOrigin(from.state, taskId, ownerId), parent = recordContextReturn(from.state)
      return !!source && source.url === locationUrl(to) && parent?.url === locationUrl(to) && taskPresentation(to, taskId, ownerId)
        && presentationBase(from, true) === presentationBase(to, true)
    }
    if (personOwner(from) && personOwner(to)) {
      const parent = recordContextReturn(to.state), fromSource = taskDetailReturnOrigin(from.state, taskId, ownerId), toSource = taskDetailReturnOrigin(to.state, taskId, ownerId)
      const returning = recordContextReturn(from.state)
      return !!fromSource && !!toSource && fromSource.url === toSource.url
        && (new URLSearchParams(from.search).get('panel') === new URLSearchParams(to.search).get('panel')
          || parent?.url === locationUrl(from) || returning?.url === locationUrl(to))
        && presentationBase(from, true) === presentationBase(to, true)
    }
    return false
  }
}

type Form = { unsaved: () => boolean; discard: () => void; /** FR-SAV-05: put typed input where it outlives the sign-in flow. */ keep?: () => void;
  /** A mounted memory-only form may explicitly identify an overlay round trip that retains all its input. */ preservesNavigation?: PreservesFormNavigation }
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
export const blocksUnsavedNavigation = ({ currentLocation, nextLocation }: FormNavigation) => {
  for (const form of forms) if (form.unsaved() && !form.preservesNavigation?.(currentLocation, nextLocation)) return true
  return false
}

/** FR-SAV-06: Reload once the person has answered (or nothing needed asking); the browser's own prompt would ask a second time, before the
 *  discarded forms have rendered empty. */
export function reloadAnswered() { window.removeEventListener('beforeunload', warnOnUnload); listening = false; window.location.reload() }

/** FR-SAV-05: any request refused for an expired sign-in (often the notification pulse, not the save) keeps every form's typed input
 *  before the sign-in flow takes the page. */
export const keepUnsent = () => { for (const f of forms) f.keep?.() }

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
export function leave(proceed: () => void, navigation?: FormNavigation) {
  if (navigation ? blocksUnsavedNavigation(navigation) : hasUnsaved()) leaveQuestion.show({ proceed }); else proceed()
}
