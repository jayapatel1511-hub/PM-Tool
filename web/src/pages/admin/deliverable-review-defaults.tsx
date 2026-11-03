import { useRef, useState, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ErrorBanner, Loading, Spinner } from '@/components/hub/common'
import { useMe } from '@/lib/auth'
import { ApiError, typedRequest } from '@/lib/api'
import { invalidateChange } from '@/lib/keys'
import { useSafeForm } from '@/hooks/drafts'
import { t } from '@/lib/i18n'

type Schemas = components['schemas']
type Discipline = Pick<Schemas['DisciplineRow'], 'id' | 'name' | 'isActive'>
type DefaultsProps = { typeId: string; name: string; typeActive: boolean; disciplines: Discipline[]; catalogueError: unknown;
  cataloguePending: boolean; onRetryCatalogue: () => void; onClose: () => void }
export function DeliverableReviewDefaults(props: DefaultsProps) {
  const me = useMe()
  return <DefaultsRead key={`${props.typeId}:${me.id}`} {...props} />
}
function DefaultsRead({ typeId, name, typeActive, disciplines, catalogueError, cataloguePending, onRetryCatalogue, onClose }: DefaultsProps) {
  const me = useMe()
  const read = useQuery({ queryKey: ['admin', 'deliverable-review-defaults', typeId, me.id, true], queryFn: () => typedRequest('get', '/api/v1/admin/deliverableTypes/{id}/review-defaults', { params: { id: typeId } }) })
  const initial = useRef<Schemas['ReviewDefaultSettings'] | null>(null)
  if (!initial.current && !read.isError && read.data) initial.current = read.data
  if (!initial.current) return <Dialog open onOpenChange={open => { if (!open) onClose() }}><DialogContent><DialogHeader><DialogTitle>{t('reviewDefaults.title')}</DialogTitle><DialogDescription>{name}</DialogDescription></DialogHeader>{read.isError ? <ErrorBanner error={read.error} retry={() => read.refetch()} /> : <Loading />}{catalogueError != null && <ErrorBanner error={catalogueError} retry={onRetryCatalogue} />}</DialogContent></Dialog>
  // The first successful read belongs to this editing session. Later reads/errors must never rebase or unmount it.
  return <DefaultsEditor initial={initial.current} name={name} typeActive={typeActive} disciplines={disciplines} onClose={onClose}
    readError={read.isError ? read.error : null} onRetryRead={() => { void read.refetch() }} catalogueError={catalogueError} cataloguePending={cataloguePending} onRetryCatalogue={onRetryCatalogue} />
}
function DefaultsEditor({ initial, name, typeActive, disciplines, onClose, readError, onRetryRead, catalogueError, cataloguePending, onRetryCatalogue }: Omit<DefaultsProps, 'typeId'> & {
  initial: Schemas['ReviewDefaultSettings']; readError: unknown; onRetryRead: () => void
}) {
  const qc = useQueryClient(), original = useRef(initial), sending = useRef(false)
  const [ids, setIds] = useState(initial.disciplineIds), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<unknown>(null)
  const attempt = useRef<Schemas['ReviewDefaultSettingsWrite'] | null>(null), safe = useSafeForm(null, ids, setIds)
  const readUnavailable = readError != null || catalogueError != null || cataloguePending
  const equal = (a: string[], b: string[]) => [...a].sort().join(',') === [...b].sort().join(',')
  const finish = (reconciled: boolean) => { safe.clear(); void invalidateChange(qc, 'ReferenceData'); toast.success(t(reconciled ? 'reviewDefaults.reconciled' : 'common.saved')); onClose() }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (sending.current || (!uncertain && readUnavailable)) return
    sending.current = true; setBusy(true); setError(null)
    try {
      if (uncertain) {
        const current = await typedRequest('get', '/api/v1/admin/deliverableTypes/{id}/review-defaults', { params: { id: original.current.deliverableTypeId } })
        if (current.deliverableTypeId !== original.current.deliverableTypeId || !Number.isInteger(current.rowVersion)
          || current.rowVersion < original.current.rowVersion || !equal(current.disciplineIds, attempt.current?.disciplineIds ?? [])) throw new Error(t('reviewDefaults.notMatched'))
        await finish(true)
      } else {
        attempt.current = { disciplineIds: [...ids].sort(), rowVersion: original.current.rowVersion }
        const result: unknown = await typedRequest('put', '/api/v1/admin/deliverableTypes/{id}/review-defaults', { params: { id: original.current.deliverableTypeId }, ifMatch: original.current.rowVersion, body: attempt.current })
        const expectedVersion = original.current.rowVersion + (equal(ids, original.current.disciplineIds) ? 0 : 1)
        if (!result || typeof result !== 'object' || Object.keys(result).length !== 3 || !('deliverableTypeId' in result) || result.deliverableTypeId !== original.current.deliverableTypeId
          || !('rowVersion' in result) || result.rowVersion !== expectedVersion
          || !('disciplineIds' in result) || !Array.isArray(result.disciplineIds) || !result.disciplineIds.every(id => typeof id === 'string') || !equal(result.disciplineIds, ids)) throw new Error(t('app.error'))
        await finish(false)
      }
    } catch (caught) { if (attempt.current && (!(caught instanceof ApiError) || caught.status === 0 || caught.status === 408 || caught.status >= 500)) setUncertain(true); setError(caught) }
    finally { sending.current = false; setBusy(false) }
  }
  const close = () => { if (!sending.current) safe.close(onClose) }
  return <Dialog open onOpenChange={open => { if (!open) close() }}><DialogContent data-review-default-settings={initial.deliverableTypeId}>
    <DialogHeader><DialogTitle>{t('reviewDefaults.title')}</DialogTitle><DialogDescription>{name}</DialogDescription></DialogHeader>
    <form className="space-y-3" onSubmit={submit}><p className="text-sm text-muted-foreground">{t('reviewDefaults.hint')}</p>
      <fieldset disabled={busy || uncertain || readUnavailable} className="space-y-2">{disciplines.map(discipline => <label className="flex items-start gap-2 text-sm" key={discipline.id}>
        <input type="checkbox" checked={ids.includes(discipline.id)} disabled={(!typeActive || !discipline.isActive) && !original.current.disciplineIds.includes(discipline.id)}
          onChange={event => setIds(old => event.target.checked ? [...old, discipline.id] : old.filter(id => id !== discipline.id))} />
        {discipline.name}{!discipline.isActive && ` · ${t('reviewDefaults.retired')}`}
      </label>)}</fieldset>
      {ids.length > 50 && <p role="alert">{t('reviewDefaults.tooMany')}</p>}{error != null && <ErrorBanner error={error} />}
      {readError != null && <ErrorBanner error={readError} retry={onRetryRead} />}
      {catalogueError != null && <ErrorBanner error={catalogueError} retry={onRetryCatalogue} />}
      {cataloguePending && <Loading />}
      {uncertain && <p role="status" className="text-sm text-warn">{t('reviewDefaults.uncertain')}</p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={close}>{t('common.cancel')}</Button>
        <Button type="submit" disabled={busy || (!uncertain && (readUnavailable || ids.length > 50))}>{busy && <Spinner />}{busy ? t('common.saving') : uncertain ? t('reviewDefaults.check') : t('common.save')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}
