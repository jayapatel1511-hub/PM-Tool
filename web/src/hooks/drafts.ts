import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { useFlag, useMe } from '@/lib/auth-context'
import { clearDraft, draftStorageKey, leave, readDraft, registerForm, writeDraft } from '@/lib/drafts'

/** A form is compared as its content: an empty text or a cleared choice counts the same as one never touched. */
const content = (v: unknown) => JSON.stringify(v, (_, x) => (x === '' || x === null ? undefined : x)) ?? ''

/** FR-SAV-03: keeps what is typed in a create form or long text box as a draft in this browser, restores it when the same form opens
 *  again on this device and drops it on submit or Discard. `key` is `<form>.<context>` (`task.new.<projectId>`); null (an edit form) keeps
 *  no draft but still reports `unsaved()`, so leaving it asks (FR-SAV-04). The flag off does nothing. `value` is the whole form as
 *  plain data; a form with no change from its first render writes no draft. */
export function useDraft<T>(key: string | null | undefined, value: T, setValue: (v: T) => void) {
  const me = useMe()
  const on = useFlag('data_entry_safety')
  const keepDays = me.settings.draftKeepDays ?? 7
  const storeKey = on && key ? draftStorageKey(me.id, key) : null // a form's key does not change while it is open
  const json = content(value)
  const initial = useRef(value) // the empty form: what the first render held
  const first = useRef(json)
  const latest = useRef({ value, json, set: setValue })
  const written = useRef<string | null>(null) // the draft's content as stored, so a restored draft keeps its time until it changes
  const saved = useRef<string | null>(null) // the content a successful submit took
  const drafted = useRef(false)
  const restoring = useRef(false) // a restored value is on its way into the form: the empty form must not clear its draft first
  const [restoredAt, setRestoredAt] = useState<string | null>(() => {
    const found = storeKey ? readDraft(storeKey, keepDays) : null
    return found && content(found.value) !== json ? found.at : null
  })
  useEffect(() => { latest.current = { value, json, set: setValue } })

  useLayoutEffect(() => {
    if (!storeKey) return
    const found = readDraft(storeKey, keepDays)
    if (!found || content(found.value) === first.current) return
    latest.current.set(found.value as T)
    written.current = content(found.value)
    drafted.current = true
    restoring.current = true
  }, [storeKey, keepDays])

  useEffect(() => {
    if (!storeKey) return
    if (restoring.current && json === first.current) return // the restored value arrives with the next render
    restoring.current = false
    if (saved.current != null && json !== saved.current) saved.current = null // typing went on after a submit
    if (json === first.current) { clearDraft(storeKey); written.current = null; drafted.current = false; return }
    if (json === saved.current || json === written.current) return
    drafted.current = writeDraft(storeKey, latest.current.value)
    written.current = drafted.current ? json : null
  }, [storeKey, json])

  const forget = () => { if (storeKey) clearDraft(storeKey); written.current = null; drafted.current = false; setRestoredAt(null) }
  return {
    restoredAt,
    /** Discard on the notice: the form goes back to its empty state. */
    discard: () => { forget(); latest.current.set(initial.current) },
    /** After a successful submit: the draft is gone and what is on screen no longer counts as unsaved. */
    clear: () => { forget(); saved.current = latest.current.json },
    /** Typed input that is neither a stored draft nor unchanged from the first render (an edit form has no draft, so it always asks). */
    unsaved: () => on && latest.current.json !== first.current && latest.current.json !== saved.current && !drafted.current,
  }
}

/** FR-SAV-04: the form tells the leave guard when it holds input nothing has saved, so closing the page or tab, going to another
 *  page and Reload after a build update ask "Keep editing or discard?". */
export function useUnsavedGuard(unsaved: () => boolean, discard: () => void) {
  const latest = useRef({ unsaved, discard })
  useEffect(() => { latest.current = { unsaved, discard } })
  useEffect(() => registerForm({ unsaved: () => latest.current.unsaved(), discard: () => latest.current.discard() }), [])
}

/** The draft and the guard together, for a form that keeps both. `close(onClose)` is for every way a person closes it (the close
 *  control, Escape, a click outside, Cancel); a successful submit calls `clear()` and then its own close. */
export function useSafeForm<T>(key: string | null | undefined, value: T, setValue: (v: T) => void) {
  const draft = useDraft(key, value, setValue)
  useUnsavedGuard(draft.unsaved, draft.discard)
  return { ...draft, close: leave }
}
export type SafeForm = ReturnType<typeof useSafeForm>
