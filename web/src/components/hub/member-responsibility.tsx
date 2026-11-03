import { useQueryClient } from '@tanstack/react-query'
import { useId, useRef, useState } from 'react'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { ErrorBanner, Field } from './common'
import { DraftNotice } from './drafts'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { useSafeForm } from '@/hooks/drafts'
import { ApiError, typedRequest } from '@/lib/api'
import { t } from '@/lib/i18n'

type Member = components['schemas']['ProjectTeamMember']
export type ResponsibilityDraft = { memberId: string; name: string; note: string; rowVersion: number }

/** A single editor outside the desktop/phone renderers; refresh never replaces its captured member version. */
export function MemberResponsibilityDialog({ projectId, draft, latest, needsReason, onClose }: {
  projectId: string; draft: ResponsibilityDraft; latest?: Member; needsReason: boolean; onClose: () => void
}) {
  const qc = useQueryClient(), inputId = useId(), inFlight = useRef(false)
  const [value, setValue] = useState({ note: draft.note, reason: '', rowVersion: draft.rowVersion })
  const form = useSafeForm(`member-responsibility.${projectId}.${draft.memberId}`, value, setValue)
  const [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(null), [acknowledged, setAcknowledged] = useState(false)
  const conflict = error instanceof ApiError && error.status === 409
  const refresh = () => Promise.all([
    qc.invalidateQueries({ queryKey: ['p', projectId, 'team'] }), qc.invalidateQueries({ queryKey: ['wteam'] }), qc.invalidateQueries({ queryKey: ['person-panel'] }),
  ])
  const close = () => { if (!inFlight.current) form.close(onClose) }
  const canEdit = latest?.responsibility?.canEdit === true
  const changed = (latest?.rowVersion ?? draft.rowVersion) !== value.rowVersion
  async function save() {
    if (inFlight.current || acknowledged || !canEdit || value.note.trim().length > 80 || needsReason && value.reason.trim().length < 5) return
    inFlight.current = true; setBusy(true); setError(null)
    let saved = false
    try {
      const response = await typedRequest('patch', '/api/v1/projects/{projectId}/members/{memberId}/responsibility', { params: { projectId, memberId: draft.memberId },
        body: { responsibilityNote: value.note.trim() || null, reason: value.reason.trim() || null, rowVersion: value.rowVersion }, ifMatch: value.rowVersion })
      if (response.id !== draft.memberId || !Number.isInteger(response.rowVersion) || response.rowVersion < value.rowVersion || response.responsibilityNote !== (value.note.trim() || null)) throw new Error(t('personFacts.responseUnknown'))
      saved = true; setAcknowledged(true); form.clear(); toast.success(t('responsibility.saved'))
      await refresh(); onClose()
    } catch (failure) { setError(failure); if (!saved && failure instanceof ApiError && failure.status === 409) { try { await refresh() } catch { /* Preserve the refusal and original version until a successful explicit refresh. */ } } }
    finally { inFlight.current = false; setBusy(false) }
  }
  return <Dialog open onOpenChange={open => !open && close()}><DialogContent><DialogHeader>
    <DialogTitle>{t('responsibility.editNamed', { name: draft.name })}</DialogTitle><DialogDescription>{t('responsibility.hint')}</DialogDescription>
  </DialogHeader>
    <DraftNotice form={form} />
    {error != null && <ErrorBanner error={error} />}
    {acknowledged ? <p role="status" className="text-sm">{t('responsibility.savedReload')}</p> : <>
      <Field label={t('responsibility.label')} htmlFor={inputId}><Input id={inputId} value={value.note} maxLength={80} disabled={busy || !canEdit} onChange={event => setValue(prior => ({ ...prior, note: event.target.value }))} /><span className="text-xs text-muted-foreground">{value.note.length}/80</span></Field>
      {needsReason && <Field label={t('common.reason')} htmlFor={`${inputId}-reason`}><Input id={`${inputId}-reason`} value={value.reason} disabled={busy || !canEdit} onChange={event => setValue(prior => ({ ...prior, reason: event.target.value }))} /></Field>}
      {!canEdit && <p role="status" className="text-sm text-muted-foreground">{latest?.responsibility?.editReason || t('responsibility.unavailable')}</p>}
      {(conflict || changed) && <div className="space-y-2 rounded-md border p-3 text-sm" role="status"><p>{t('responsibility.conflict')}</p>
        <p><span className="font-medium">{t('responsibility.latest')}: </span>{latest?.responsibility?.note || t('teamOrganisation.notSet')}</p>
        <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" disabled={busy} onClick={() => refresh()}>{t('responsibility.refresh')}</Button>
          {latest && changed && <><Button size="sm" variant="outline" disabled={busy || !canEdit} onClick={() => { setValue(prior => ({ ...prior, rowVersion: latest.rowVersion })); setError(null) }}>{t('responsibility.keepMine')}</Button>
            <Button size="sm" variant="outline" disabled={busy} onClick={() => { setValue({ note: latest.responsibility?.note ?? '', reason: '', rowVersion: latest.rowVersion }); setError(null) }}>{t('responsibility.takeLatest')}</Button></>}
        </div></div>}
    </>}
    <DialogFooter><Button variant="outline" disabled={busy} onClick={close}>{t(acknowledged ? 'common.close' : 'common.cancel')}</Button>
      {acknowledged ? <Button disabled={busy} onClick={async () => { setBusy(true); try { await refresh(); onClose() } catch (failure) { setError(failure) } finally { setBusy(false) } }}>{t('common.reload')}</Button>
        : <Button disabled={busy || !canEdit || value.note.trim().length > 80 || needsReason && value.reason.trim().length < 5} onClick={save}>{t('common.save')}</Button>}
    </DialogFooter>
  </DialogContent></Dialog>
}
