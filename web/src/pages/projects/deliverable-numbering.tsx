import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Field, Loading, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useSafeForm } from '@/hooks/drafts'
import { useProjectRefresh } from '@/hooks/data'
import { ApiError, typedRequest } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { t } from '@/lib/i18n'
import type { components } from '@/gen/api'

type Numbering = components['schemas']['NumberingRead']

export function DeliverableNumberingSettings({ projectId }: { projectId: string }) {
  const enabled = useFlag('register_screens')
  const [manual, setManual] = useState<{ projectId: string; value: Numbering; session: number } | null>(null)
  const q = useQuery({ queryKey: ['p', projectId, 'deliverable-numbering'], enabled,
    queryFn: () => typedRequest('get', '/api/v1/projects/{id}/deliverable-numbering', { params: { id: projectId } }) })
  if (!enabled) return null
  return <section className="space-y-3 rounded-lg border bg-card p-4" aria-labelledby="deliverable-numbering-title">
    <h2 id="deliverable-numbering-title" className="font-semibold">{t('deliverableRegister.numbering')}</h2>
    {!q.data ? q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : <Loading rows={2} /> : <>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <NumberingForm key={`${projectId}:${manual?.session ?? 0}`} projectId={projectId}
        initial={{ ...(manual?.projectId === projectId ? manual.value : q.data), canEdit: q.data.canEdit }}
        reset={value => setManual(current => ({ projectId, value, session: (current?.session ?? 0) + 1 }))}
        reload={async () => (await q.refetch()).data} /></>}
  </section>
}

function NumberingForm({ projectId, initial, reload, reset }: { projectId: string; initial: Numbering; reload: () => Promise<Numbering | undefined>; reset: (value: Numbering) => void }) {
  // Refetches may change permissions, but never replace the baseline behind typed input.
  const [f, setF] = useState(() => ({ pattern: initial.pattern ?? '', reason: '', rowVersion: initial.rowVersion }))
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<ApiError | null>(null)
  const safe = useSafeForm(null, f, setF)
  const refresh = useProjectRefresh()
  const conflict = err?.code === 'concurrency_conflict'
  const submit = async () => {
    if (busy || conflict || !initial.canEdit) return
    setBusy(true); setErr(null)
    try {
      const saved = await typedRequest('put', '/api/v1/projects/{id}/deliverable-numbering', { params: { id: projectId },
        body: { pattern: f.pattern || null, reason: f.reason || null, rowVersion: f.rowVersion } })
      safe.clear(); reset({ ...initial, pattern: saved.pattern, rowVersion: saved.rowVersion }); refresh(projectId)
      toast.success(t('deliverableRegister.patternSaved'))
    } catch (error) { setErr(error as ApiError) } finally { setBusy(false) }
  }
  return <form className="space-y-3" onSubmit={e => { e.preventDefault(); void submit() }}>
    <Field label={t('deliverableRegister.pattern')} htmlFor="deliverable-number-pattern" hint={t('deliverableRegister.patternHint')} error={err?.fieldErrors.pattern}>
      <Input id="deliverable-number-pattern" maxLength={120} value={f.pattern} disabled={!initial.canEdit || busy} onChange={e => setF({ ...f, pattern: e.target.value })} />
    </Field>
    {initial.canEdit ? <>
      <Field label={t('common.reason')} optional htmlFor="deliverable-number-reason" error={err?.fieldErrors.reason}>
        <Textarea id="deliverable-number-reason" maxLength={1000} value={f.reason} disabled={busy} onChange={e => setF({ ...f, reason: e.target.value })} />
      </Field>
      {err && <ErrorBanner error={err} />}
      {conflict && <p role="alert" className="text-sm text-warn">{t('deliverableRegister.patternConflict')}</p>}
      <div className="flex flex-wrap gap-2">
        <Button type="submit" disabled={busy || conflict}>{busy && <Spinner />}{t('common.save')}</Button>
        <Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(() => { void reload().then(current => {
          if (current) { safe.clear(); reset(current) }
        }) })}>{t('common.reload')}</Button>
      </div>
    </> : <p className="text-sm text-muted-foreground">{t('deliverableRegister.patternReadOnly')}</p>}
  </form>
}
