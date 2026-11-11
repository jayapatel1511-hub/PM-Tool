import { useEffect, useId, useRef, useState } from 'react'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { useUnsavedGuard } from '@/hooks/drafts'
import { t } from '@/lib/i18n'
import { fmtTime } from '@/lib/format'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { ErrorBanner, Spinner } from '@/components/hub/common'

type Read = components['schemas']['TaskContributionRead']
type Detail = components['schemas']['TaskDetailResponse']
type Intent = components['schemas']['TaskContributionSetBody']

const normalized = (note: string | null | undefined) => note?.trim() || null
const validRead = (value: Read) => value && typeof value.collaboratorId === 'string' && /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value.collaboratorId) && value.collaboratorId !== '00000000-0000-0000-0000-000000000000' && Number.isInteger(value.rowVersion) && value.rowVersion >= 0
  && (value.markedAt === null || typeof value.markedAt === 'string' && Number.isFinite(Date.parse(value.markedAt)))
  && (value.note === null || typeof value.note === 'string' && value.note.length <= 200) && typeof value.canMark === 'boolean' && typeof value.canWithdraw === 'boolean'
  && (value.refusalReason === null || typeof value.refusalReason === 'string')
  && (value.markedAt !== null || value.note === null)

/** A contribution belongs to the actual membership, never the parent task's version or an on-behalf actor. */
export function TaskContribution({ taskId, userId, value, memberPresent = true, currentAvailable, needsReason, readCurrent, onChanged }: {
  taskId: string; userId: string; value?: Read | null; memberPresent?: boolean; currentAvailable: boolean; needsReason: boolean
  readCurrent: () => Promise<Detail>; onChanged: () => Promise<unknown> | void
}) {
  const me = useMe(), prefix = useId(), mounted = useRef(true)
  const [note, setNote] = useState(''), [reason, setReason] = useState('')
  const [origin, setOrigin] = useState<Read | null>(null), [sent, setSent] = useState<Intent | null>(null)
  const [confirmed, setConfirmed] = useState<Read | null>(null), [checked, setChecked] = useState<Read | null | undefined>(undefined)
  const [error, setError] = useState<unknown>(null), [uncertain, setUncertain] = useState(false), [busy, setBusy] = useState(false)
  const own = userId === me.id, shown = confirmed ?? value
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  useEffect(() => { if (confirmed && value && (value.collaboratorId !== confirmed.collaboratorId || value.rowVersion >= confirmed.rowVersion)) setConfirmed(null) }, [value, confirmed])
  const discard = () => { setOrigin(null); setSent(null); setNote(''); setReason(''); setError(null); setUncertain(false); setChecked(undefined) }
  useUnsavedGuard(() => own && (busy || !!origin || !!sent || !!note || !!reason || uncertain), discard)
  const relationChanged = !!origin && (!value || origin.collaboratorId !== value.collaboratorId)
  const usable = own && memberPresent && currentAvailable && !!shown && validRead(shown) && !relationChanged && !busy && !sent
  const startDraft = () => { if (!origin && value && currentAvailable) setOrigin({ ...value }) }
  const submit = async (marked: boolean) => {
    if (!usable || !shown || (marked ? !shown.canMark : !shown.canWithdraw)) return
    const original = origin ?? { ...shown }
    if (marked && note.length > 200 || needsReason && !reason.trim()) return
    const intent: Intent = { collaboratorId: original.collaboratorId, rowVersion: original.rowVersion, marked, note: marked ? normalized(note) : null, reason: normalized(reason) }
    setOrigin(original); setSent(intent); setBusy(true); setError(null); setChecked(undefined)
    try {
      const result = await typedRequest('patch', '/api/v1/tasks/{id}/contribution', { params: { id: taskId }, body: intent, ifMatch: original.rowVersion })
      if (!validRead(result) || result.collaboratorId !== original.collaboratorId || result.rowVersion < original.rowVersion || !!result.markedAt !== intent.marked || normalized(result.note) !== normalized(intent.note)) throw new Error(t('taskContribution.uncertainty'))
      if (!mounted.current) return
      setConfirmed(result); setOrigin(null); setSent(null); setNote(''); setReason(''); setUncertain(false)
      // A failed background refresh cannot turn a confirmed PATCH into an uncertain command.
      await Promise.allSettled([Promise.resolve().then(onChanged)])
    } catch (caught) {
      if (!mounted.current) return
      setError(caught); setUncertain(!(caught instanceof ApiError) || caught.status === 0 || caught.status === 408 || caught.status >= 500)
    } finally { if (mounted.current) setBusy(false) }
  }
  const check = async () => {
    if (busy) return
    setBusy(true); setError(null); setChecked(undefined)
    try {
      const detail = await readCurrent(), next = detail.collaborators.find(person => person.id === me.id)?.contribution
      if (next && !validRead(next)) throw new Error(t('taskContribution.unavailable'))
      if (mounted.current) setChecked(next ?? null)
    } catch (caught) { if (mounted.current) setError(caught) }
    finally { if (mounted.current) setBusy(false) }
  }
  const matches = !!checked && !!sent && checked.collaboratorId === sent.collaboratorId && !!checked.markedAt === sent.marked && normalized(checked.note) === normalized(sent.note)
  if (!memberPresent && !origin && !sent && !note && !reason) return null
  return <div data-task-contribution-user={userId} className="min-w-0 space-y-2 text-sm">
    {currentAvailable && memberPresent && shown && validRead(shown) ? shown.markedAt && <div data-task-contribution-mark><p className="font-medium"><time dateTime={shown.markedAt}>{t(own ? 'taskContribution.markedAt' : 'taskContribution.markedBy', { when: fmtTime(shown.markedAt) })}</time></p>{shown.note && <p className="whitespace-pre-wrap break-words">{shown.note}</p>}</div>
      : <p className="text-muted-foreground">{t('taskContribution.unavailable')}</p>}
    {own && <>
      {(!shown?.markedAt || !!origin || !!sent) && <div className="space-y-1">
        <label htmlFor={prefix + '-note'}>{t('taskContribution.note')}</label>
        <Textarea id={prefix + '-note'} value={note} maxLength={200} rows={2} disabled={busy || !!sent || !currentAvailable || !value || !validRead(value) || !value.canMark || relationChanged} aria-describedby={prefix + '-hint'} onChange={event => { startDraft(); setNote(event.target.value) }} />
        <p id={prefix + '-hint'} className="text-xs text-muted-foreground">{t('taskContribution.noteHint')} <span className="tabular-nums">{note.length}/200</span></p>
      </div>}
      {needsReason && <div className="space-y-1"><label htmlFor={prefix + '-reason'}>{t('common.reason')}</label><Textarea id={prefix + '-reason'} rows={2} value={reason} disabled={busy || !!sent || relationChanged} onChange={event => { startDraft(); setReason(event.target.value) }} /></div>}
      {relationChanged && <p role="status">{t('taskContribution.membershipChanged')}</p>}
      {shown?.refusalReason && <p className="text-muted-foreground">{shown.refusalReason}</p>}
      {uncertain && <p role="status">{t('taskContribution.uncertainty')}</p>}
      {error != null && <ErrorBanner error={error} />}
      {checked !== undefined && <p role="status">{t(matches ? 'taskContribution.currentMatches' : 'taskContribution.currentDifferent')}</p>}
      <div className="flex flex-wrap gap-2">
        {!sent && !shown?.markedAt && <Button size="sm" variant="outline" disabled={!usable || !shown?.canMark || needsReason && !reason.trim()} onClick={() => submit(true)}>{busy && <Spinner />}{t('taskContribution.mark')}</Button>}
        {!sent && shown?.markedAt && <Button size="sm" variant="outline" disabled={!usable || !shown.canWithdraw || needsReason && !reason.trim()} onClick={() => submit(false)}>{t('taskContribution.withdraw')}</Button>}
        {(!!sent || relationChanged || !currentAvailable) && <Button size="sm" variant="outline" disabled={busy} onClick={check}>{busy && <Spinner />}{t('taskContribution.checkCurrent')}</Button>}
        {checked && <Button size="sm" variant="outline" disabled={busy} onClick={() => { setConfirmed(checked); discard() }}>{t('taskContribution.newIntent')}</Button>}
      </div>
    </>}
  </div>
}
