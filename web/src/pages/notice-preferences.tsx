import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useId, useRef, useState } from 'react'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { runtimeText } from '@/lib/i18n'
import { invalidateNotifications } from '@/lib/keys'
import { useUnsavedGuard } from '@/hooks/drafts'
import { ErrorBanner, Field, Loading, Section, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'

type Read = components['schemas']['NoticePreferencesRead']
type Intent = components['schemas']['NoticePreferencesSetBody']
type Draft = Omit<Read, 'revision'>
const fields = ['noticeDigestFrequency', 'holdImmediateEmailWhenAway', 'timeReminderEnabled', 'timeReminderWeekday', 'timeReminderTimeLocal'] as const
const weekdays = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'] as const
const validTime = (value: string) => /^(?:[01]\d|2[0-3]):[0-5]\d$/.test(value)
const validDay = (value: number | null) => value !== null && Number.isInteger(value) && value >= 1 && value <= 7
const validRead = (value: Read) => value && Number.isInteger(value.revision) && value.revision >= 0 && value.revision <= 2147483647
  && (value.noticeDigestFrequency === 'Daily' || value.noticeDigestFrequency === 'Weekly')
  && typeof value.holdImmediateEmailWhenAway === 'boolean' && typeof value.timeReminderEnabled === 'boolean'
  && (value.timeReminderWeekday === null || validDay(value.timeReminderWeekday))
  && (value.timeReminderTimeLocal === null || typeof value.timeReminderTimeLocal === 'string' && validTime(value.timeReminderTimeLocal))
  && (!value.timeReminderEnabled || validDay(value.timeReminderWeekday) && value.timeReminderTimeLocal !== null)
const choices = (value: Read): Draft => ({ noticeDigestFrequency: value.noticeDigestFrequency, holdImmediateEmailWhenAway: value.holdImmediateEmailWhenAway,
  timeReminderEnabled: value.timeReminderEnabled, timeReminderWeekday: value.timeReminderWeekday, timeReminderTimeLocal: value.timeReminderTimeLocal })
const sameChoices = (a: Draft, b: Draft) => fields.every(field => a[field] === b[field])
const expectedRead = (original: Read, intent: Intent): Read => ({ revision: original.revision + 1,
  noticeDigestFrequency: intent.noticeDigestFrequency ?? original.noticeDigestFrequency,
  holdImmediateEmailWhenAway: intent.holdImmediateEmailWhenAway ?? original.holdImmediateEmailWhenAway,
  timeReminderEnabled: intent.timeReminderEnabled ?? original.timeReminderEnabled,
  timeReminderWeekday: intent.timeReminderWeekday ?? original.timeReminderWeekday,
  timeReminderTimeLocal: intent.timeReminderTimeLocal ?? original.timeReminderTimeLocal })

/** Self preferences only. Root mounts this after the complete notice delivery policy is integrated. */
export function NoticePreferences() {
  const me = useMe(), enabled = useFlag('people_screens'), qc = useQueryClient()
  const queryKey = ['notice-preferences', me.id, enabled] as const
  const q = useQuery({ queryKey, enabled, retry: false, queryFn: async ({ signal }) => {
    const value = await typedRequest('get', '/api/v1/me/notice-preferences', { signal })
    const previous = qc.getQueryData<Read>(queryKey)
    if (!validRead(value) || previous && value.revision < previous.revision) throw new Error(runtimeText('noticePrefs.invalidRead'))
    return value
  } })
  if (!enabled) return null
  if (!q.data) return <Section title={runtimeText('noticePrefs.title')} id="notice-preferences">
    <div className="space-y-3 px-5 py-4">{q.isPending ? <Loading rows={3} /> : <><ErrorBanner error={q.error} /><Button type="button" size="sm" variant="outline" disabled={q.isFetching} onClick={() => { void q.refetch() }}>{q.isFetching && <Spinner />}{runtimeText(q.isFetching ? 'noticePrefs.checking' : 'noticePrefs.check')}</Button></>}</div>
  </Section>
  return <NoticePreferencesEditor key={me.id} value={q.data} available={!q.isError} readError={q.error} fetching={q.isFetching} zone={me.settings.orgTimeZone} afterSaved={() => invalidateNotifications(qc)}
    readCurrent={async () => (await q.refetch({ throwOnError: true, cancelRefetch: false })).data!} acceptReceipt={receipt => {
      const state = qc.getQueryState<Read>(queryKey)
      // Merge only into an idle, successful current read. A pending or failed GET keeps full authority.
      if (state?.status !== 'success' || state.fetchStatus !== 'idle' || !state.data || state.data.revision > receipt.revision
        || state.data.revision === receipt.revision && !sameChoices(choices(state.data), choices(receipt))) return false
      if (state.data.revision < receipt.revision) qc.setQueryData(queryKey, receipt)
      return true
    }} />
}

function NoticePreferencesEditor({ value, available, readError, fetching, zone, readCurrent, acceptReceipt, afterSaved }: {
  value: Read; available: boolean; readError: unknown; fetching: boolean; zone: string | null
  readCurrent: () => Promise<Read>; acceptReceipt: (value: Read) => boolean; afterSaved: () => Promise<unknown>
}) {
  const prefix = useId(), mounted = useRef(true), working = useRef(false)
  const [confirmed, setConfirmed] = useState<Read | null>(null)
  const [origin, setOrigin] = useState<Read | null>(null), [draft, setDraft] = useState<Draft | null>(null)
  const [sent, setSent] = useState<Intent | null>(null), [checked, setChecked] = useState<Read | null>(null)
  const [error, setError] = useState<unknown>(null), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [saved, setSaved] = useState(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const current = confirmed && confirmed.revision > value.revision ? confirmed : value
  const shown = draft ?? choices(current), baseline = origin ?? current
  const checkedCurrent = checked?.revision === value.revision ? checked : null
  const dirty = !!draft && !sameChoices(draft, choices(baseline))
  const discard = () => { setOrigin(null); setDraft(null); setSent(null); setChecked(null); setError(null); setUncertain(false); setSaved(false) }
  useUnsavedGuard(() => busy || dirty || !!sent || uncertain, discard)
  const change = (patch: Partial<Draft>) => {
    if (!available || working.current || sent) return
    const next = { ...shown, ...patch }
    setOrigin({ ...baseline }); setDraft(next); setChecked(null); setError(null); setSaved(false)
  }
  const reminderMissing = shown.timeReminderEnabled && (!validDay(shown.timeReminderWeekday) || !shown.timeReminderTimeLocal || !validTime(shown.timeReminderTimeLocal))
  // Null is an unchanged fragment, not a command to erase a saved time.
  const clearedSavedTime = baseline.timeReminderTimeLocal !== null && !shown.timeReminderTimeLocal
  const invalid = reminderMissing || clearedSavedTime
  const submit = async () => {
    if (!available || working.current || sent || !dirty || invalid) return
    const original = { ...baseline }, intent: Intent = { revision: original.revision }
    if (shown.noticeDigestFrequency !== original.noticeDigestFrequency) intent.noticeDigestFrequency = shown.noticeDigestFrequency
    if (shown.holdImmediateEmailWhenAway !== original.holdImmediateEmailWhenAway) intent.holdImmediateEmailWhenAway = shown.holdImmediateEmailWhenAway
    if (shown.timeReminderEnabled !== original.timeReminderEnabled) intent.timeReminderEnabled = shown.timeReminderEnabled
    if (shown.timeReminderWeekday !== null && shown.timeReminderWeekday !== original.timeReminderWeekday) intent.timeReminderWeekday = shown.timeReminderWeekday
    if (shown.timeReminderTimeLocal !== null && shown.timeReminderTimeLocal !== original.timeReminderTimeLocal) intent.timeReminderTimeLocal = shown.timeReminderTimeLocal
    const expected = expectedRead(original, intent)
    working.current = true; setOrigin(original); setSent(intent); setBusy(true); setError(null); setChecked(null); setSaved(false)
    try {
      const result = await typedRequest('patch', '/api/v1/me/notice-preferences', { body: intent })
      if (!validRead(result) || result.revision !== expected.revision || !sameChoices(choices(result), choices(expected))) throw new Error(runtimeText('noticePrefs.uncertain'))
      if (!mounted.current) return
      setConfirmed(result); setUncertain(false)
      const accepted = acceptReceipt(result)
      // Notification reads refresh independently; their failure cannot change this confirmed command's outcome.
      void Promise.resolve().then(afterSaved).catch(() => undefined)
      if (!accepted) return
      setOrigin(null); setDraft(null); setSent(null); setSaved(true)
    } catch (caught) {
      if (!mounted.current) return
      setError(caught); setUncertain(!(caught instanceof ApiError) || caught.status === 0 || caught.status === 408 || caught.status >= 500)
    } finally { working.current = false; if (mounted.current) setBusy(false) }
  }
  const check = async () => {
    if (working.current) return
    working.current = true; setBusy(true); setError(null); setChecked(null); setSaved(false)
    try {
      const current = await readCurrent()
      if (!validRead(current)) throw new Error(runtimeText('noticePrefs.invalidRead'))
      if (mounted.current) setChecked({ ...current })
    } catch (caught) { if (mounted.current) setError(caught) }
    finally { working.current = false; if (mounted.current) setBusy(false) }
  }
  const useCurrent = () => {
    if (!checkedCurrent || !available || working.current) return
    setOrigin({ ...checkedCurrent }); setDraft(choices(checkedCurrent)); setSent(null); setChecked(null); setError(null); setUncertain(false); setSaved(false)
  }
  const locked = busy || !!sent || !available
  return <Section title={runtimeText('noticePrefs.title')} id="notice-preferences">
    {!available && <div className="space-y-3 px-5 py-4" data-notice-preferences-error>
      <ErrorBanner error={readError} /><p className="text-sm text-muted-foreground">{runtimeText('noticePrefs.unavailable')}</p>
      <Button type="button" size="sm" variant="outline" disabled={busy || fetching} onClick={check}>{(busy || fetching) && <Spinner />}{runtimeText(busy || fetching ? 'noticePrefs.checking' : 'noticePrefs.check')}</Button>
    </div>}
    <form aria-label={runtimeText('noticePrefs.title')} data-notice-preferences-form hidden={!available} inert={!available ? true : undefined} aria-hidden={!available ? true : undefined}
      onSubmit={event => { event.preventDefault(); void submit() }} className="divide-y">
      <div className="space-y-4 px-5 py-4">
        <p className="text-sm text-muted-foreground">{runtimeText('noticePrefs.hint')}</p>
        <Field label={runtimeText('noticePrefs.frequency')} htmlFor={prefix + '-frequency'} hint={runtimeText(shown.noticeDigestFrequency === 'Weekly' ? 'noticePrefs.weeklyHint' : 'noticePrefs.frequencyHint')}>
          <select id={prefix + '-frequency'} className="h-(--control-row-h) w-full rounded-md border bg-card px-2 text-sm sm:w-44" value={shown.noticeDigestFrequency} disabled={locked}
            onChange={event => change({ noticeDigestFrequency: event.target.value })}>
            <option value="Daily">{runtimeText('noticePrefs.daily')}</option><option value="Weekly">{runtimeText('noticePrefs.weekly')}</option>
          </select>
        </Field>
        <div className="space-y-1">
          <label className="flex min-h-(--control-row-h) items-center gap-3 text-sm font-medium" htmlFor={prefix + '-hold'}><Switch id={prefix + '-hold'} checked={shown.holdImmediateEmailWhenAway} disabled={locked} onCheckedChange={on => change({ holdImmediateEmailWhenAway: on })} />{runtimeText('noticePrefs.immediateHold')}</label>
          <p className="text-xs/[18px] text-muted-foreground">{runtimeText('noticePrefs.immediateHint')}</p>
        </div>
      </div>
      <div className="space-y-3 px-5 py-4">
        <label className="flex min-h-(--control-row-h) items-center gap-3 text-sm font-medium" htmlFor={prefix + '-reminder'}><Switch id={prefix + '-reminder'} checked={shown.timeReminderEnabled} disabled={locked} onCheckedChange={on => change({ timeReminderEnabled: on })} />{runtimeText('noticePrefs.reminder')}</label>
        <p className="text-xs/[18px] text-muted-foreground">{runtimeText('noticePrefs.reminderHint')}</p>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={runtimeText('noticePrefs.day')} htmlFor={prefix + '-day'}>
            <select id={prefix + '-day'} className="h-(--control-row-h) w-full rounded-md border bg-card px-2 text-sm" value={shown.timeReminderWeekday ?? ''} disabled={locked}
              onChange={event => change({ timeReminderWeekday: Number(event.target.value) })}>
              <option value="" disabled>{runtimeText('noticePrefs.chooseDay')}</option>{weekdays.map((day, index) => <option key={day} value={index + 1}>{runtimeText(`noticePrefs.${day}`)}</option>)}
            </select>
          </Field>
          <Field label={runtimeText('noticePrefs.time')} htmlFor={prefix + '-time'}><Input id={prefix + '-time'} type="time" step={60} value={shown.timeReminderTimeLocal ?? ''} disabled={locked}
            onChange={event => change({ timeReminderTimeLocal: event.target.value || null })} /></Field>
        </div>
        <p className="text-xs/[18px] text-muted-foreground">{zone ? runtimeText('noticePrefs.zone', { zone }) : <>{runtimeText('field.TimeZone')}: {runtimeText('common.unavailable')}.</>} {runtimeText('noticePrefs.keepChoices')}</p>
        {invalid && <p role="alert" className="text-sm text-bad">{runtimeText(clearedSavedTime ? 'noticePrefs.keepSavedTime' : 'noticePrefs.chooseBoth')}</p>}
      </div>
      <div className="space-y-3 px-5 py-4">
        {saved && <p role="status" className="text-sm">{runtimeText('noticePrefs.saved')}</p>}
        {uncertain && <p role="status" className="text-sm">{runtimeText('noticePrefs.uncertain')}</p>}
        {error != null && <ErrorBanner error={error} />}
        {checkedCurrent && <div className="space-y-2 text-sm" data-notice-preferences-current>
          <p role="status">{runtimeText(sameChoices(choices(checkedCurrent), shown) ? 'noticePrefs.currentMatches' : 'noticePrefs.currentDifferent')}</p>
          <p className="text-xs/[18px] text-muted-foreground">{runtimeText('noticePrefs.useCurrentHint')}</p>
          <Button type="button" size="sm" variant="outline" disabled={busy} onClick={useCurrent}>{runtimeText('noticePrefs.useCurrent')}</Button>
        </div>}
        <div className="flex flex-wrap gap-2">
          {!sent && <><Button type="submit" size="sm" disabled={locked || !dirty || invalid}>{busy && <Spinner />}{runtimeText(busy ? 'noticePrefs.saving' : 'noticePrefs.save')}</Button>
            {dirty && <Button type="button" size="sm" variant="outline" disabled={busy} onClick={discard}>{runtimeText('noticePrefs.discard')}</Button>}</>}
          {sent && <Button type="button" size="sm" variant="outline" disabled={busy} onClick={check}>{busy && <Spinner />}{runtimeText(busy ? 'noticePrefs.checking' : 'noticePrefs.check')}</Button>}
        </div>
      </div>
    </form>
  </Section>
}
