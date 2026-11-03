import { useQuery } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { useLocation } from 'react-router'
import { ErrorBanner, Field, Loading, Missing, selectCls } from '@/components/hub/common'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { components } from '@/gen/api'
import { useUnsavedGuard } from '@/hooks/drafts'
import { ApiError, typedRequest } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { leave, type FormNavigationLocation } from '@/lib/drafts'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { recordContextReturn } from '@/lib/record-context-navigation'

type Preview = components['schemas']['DrawingImportPreview']
type PreviewBody = components['schemas']['DrawingImportPreviewBody']
type CommitBody = components['schemas']['DrawingImportCommitBody']
type Created = components['schemas']['DrawingImportCreated']
const routeUrl = (location: FormNavigationLocation) => location.pathname + location.search + location.hash
const sourceRoute = (location: FormNavigationLocation) => {
  const query = new URLSearchParams(location.search)
  for (const key of ['panel', 'personStaffScope', 'personProjectsPage', 'personProjectsPageSize']) query.delete(key)
  query.sort()
  return location.pathname + '?' + query.toString() + location.hash
}
const personAt = (location: FormNavigationLocation) => new URLSearchParams(location.search).get('panel')?.startsWith('Person:') === true
const readFile = (file: File) => new Promise<string>((resolve, reject) => {
  const reader = new FileReader()
  reader.onerror = () => reject(new Error(t('drawingImport.fileRead')))
  reader.onload = () => { const result = reader.result; if (typeof result === 'string' && result.includes(',')) resolve(result.slice(result.indexOf(',') + 1)); else reject(new Error(t('drawingImport.fileRead'))) }
  reader.readAsDataURL(file)
})

/** Source bytes stay in this mounted form only; preview is read-only and commit retains one exact retry intent. */
export function DeliverableImportDialog({ projectId, onClose, onChanged }: { projectId: string; onClose: () => void; onChanged: () => void }) {
  const me = useMe(), location = useLocation()
  const origin = useRef(sourceRoute(location))
  const fileRead = useRef(0)
  const [file, setFile] = useState<{ name: string; format: string; contentBase64: string } | null>(null)
  const [discipline, setDiscipline] = useState(''), [type, setType] = useState('')
  const [worksheet, setWorksheet] = useState(''), [source, setSource] = useState(''), [reason, setReason] = useState('')
  const [preview, setPreview] = useState<Preview | null>(null)
  const [error, setError] = useState<unknown>(null), [busy, setBusy] = useState(false)
  const [attempt, setAttempt] = useState<{ key: string; body: CommitBody } | null>(null)
  const [uncertain, setUncertain] = useState(false), [stale, setStale] = useState(false)
  const [created, setCreated] = useState<Created | null>(null)
  const [correctionRequired, setCorrectionRequired] = useState<boolean | null>(null)
  const options = useQuery({ queryKey: ['drawing-import-options', me.id, projectId],
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/deliverables/import-options', { params: { projectId } }) })
  const changed = () => { setPreview(null); setAttempt(null); setError(null); setStale(false); setCreated(null) }
  const discard = () => { fileRead.current++; setFile(null); setPreview(null); setAttempt(null); setUncertain(false); setCreated(null) }
  // Only this form opts out for a proven Person overlay that leaves its exact source mounted.
  // Other dirty forms, another route/filter, explicit close and browser unload remain guarded.
  useUnsavedGuard(() => !!file && !created, discard, (from, to) => {
    if (sourceRoute(from) !== origin.current || sourceRoute(to) !== origin.current) return false
    if (personAt(to) && recordContextReturn(to.state)?.url === routeUrl(from)) return true
    if (personAt(from) && recordContextReturn(from.state)?.url === routeUrl(to)) return true
    return personAt(from) && personAt(to) && new URLSearchParams(from.search).get('panel') === new URLSearchParams(to.search).get('panel')
      && recordContextReturn(from.state)?.url === recordContextReturn(to.state)?.url
  })
  const close = () => { if (!busy) leave(() => { discard(); onClose() }) }
  const selectFile = async (selected?: File) => {
    const generation = ++fileRead.current
    changed(); setFile(null); setWorksheet('')
    if (!selected) return
    const format = selected.name.toLowerCase().endsWith('.xlsx') ? 'xlsx' : selected.name.toLowerCase().endsWith('.csv') ? 'csv' : null
    if (!format) { setError(new Error(t('drawingImport.fileType'))); return }
    if (selected.size > (format === 'csv' ? 256 * 1024 : 2 * 1024 * 1024)) { setError(new Error(t('drawingImport.fileLimit'))); return }
    setBusy(true)
    try { const contentBase64 = await readFile(selected); if (generation === fileRead.current) setFile({ name: selected.name, format, contentBase64 }) }
    catch (failure) { if (generation === fileRead.current) setError(failure) }
    finally { if (generation === fileRead.current) setBusy(false) }
  }
  const body = (): PreviewBody => ({ format: file!.format, contentBase64: file!.contentBase64, worksheet: worksheet || null,
    sourceReference: source.trim() || null, defaults: { projectDisciplineId: discipline || null, deliverableTypeId: type || null }, reason: reason.trim() || null })
  const inspect = async () => {
    if (!file || busy || uncertain) return
    setBusy(true); setError(null); setStale(false); setAttempt(null)
    try {
      const result = await typedRequest('post', '/api/v1/projects/{projectId}/deliverables/import-preview', { params: { projectId }, body: body() })
      setPreview(result); setCorrectionRequired(result.correctionReasonRequired)
    }
    catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  const commit = async () => {
    if (busy || created || !preview?.canCommit || stale) return
    const current = attempt ?? { key: crypto.randomUUID(), body: { ...body(), previewFingerprint: preview.previewFingerprint, projectRowVersion: preview.projectRowVersion } }
    setAttempt(current); setBusy(true); setError(null)
    try {
      const result = await typedRequest('post', '/api/v1/projects/{projectId}/deliverables/import', { params: { projectId }, body: current.body, idempotencyKey: current.key })
      setCreated(result); setUncertain(false); onChanged()
    } catch (failure) {
      setError(failure)
      // A later replay denial cannot prove that an earlier unanswered attempt rolled
      // back. Keep that original intent even when authority or the flag has changed.
      const pending = uncertain || !(failure instanceof ApiError) || failure.status === 0 || failure.status >= 500 || failure.code === 'idempotency_in_progress'
      setUncertain(pending)
      if (!pending) { setAttempt(null); setStale(true) }
    } finally { setBusy(false) }
  }
  const template = () => {
    const url = URL.createObjectURL(new Blob(['title,client drawing number,discipline,type,owner email,reviewer email,milestone,start date,due date,revision,requires review,priority,description\r\n'], { type: 'text/csv;charset=utf-8' }))
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'drawing-list-template.csv'; anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 1000)
  }
  const locked = busy || uncertain || !!created
  const sheets = preview?.sheets ?? []
  return <Dialog open onOpenChange={open => !open && close()}><DialogContent className="max-h-[92dvh] sm:max-w-3xl" aria-describedby="drawing-import-hint">
    <DialogHeader><DialogTitle>{t('drawingImport.title')}</DialogTitle><DialogDescription id="drawing-import-hint">{t('drawingImport.hint')}</DialogDescription></DialogHeader>
    <div className="min-w-0 space-y-4" data-drawing-import>
      {options.isPending ? <Loading rows={3} /> : options.error ? <ErrorBanner error={options.error} retry={() => options.refetch()} /> : <>
        {!options.data?.canImport && <p role="status">{t('drawingImport.noPermission')}</p>}
        <div className="flex flex-wrap items-end justify-between gap-3"><Field label={t('drawingImport.file')} htmlFor="drawing-import-file" className="min-w-0 flex-1">
          <Input id="drawing-import-file" type="file" accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" disabled={locked || !options.data?.canImport} onChange={event => void selectFile(event.target.files?.[0])} />
        </Field><Button variant="outline" disabled={busy} onClick={template}>{t('drawingImport.template')}</Button></div>
        <p className="text-xs text-muted-foreground">{t('drawingImport.limits')}</p>
        <div className="grid min-w-0 gap-3 sm:grid-cols-2">
          <Field label={t('drawingImport.defaultDiscipline')} htmlFor="drawing-import-discipline"><select id="drawing-import-discipline" className={selectCls} value={discipline} disabled={locked} onChange={event => { changed(); setDiscipline(event.target.value) }}>
            <option value="">{t('drawingImport.fromFile')}</option>{options.data?.disciplines.filter(row => row.canCreate).map(row => <option key={row.id} value={row.id}>{row.name}</option>)}
          </select></Field>
          <Field label={t('drawingImport.defaultType')} htmlFor="drawing-import-type"><select id="drawing-import-type" className={selectCls} value={type} disabled={locked} onChange={event => { changed(); setType(event.target.value) }}>
            <option value="">{t('drawingImport.fromFile')}</option>{options.data?.types.map(row => <option key={row.id} value={row.id}>{row.name}</option>)}
          </select></Field>
        </div>
        {sheets.length > 1 && <Field label={t('drawingImport.sheet')} htmlFor="drawing-import-sheet"><select id="drawing-import-sheet" className={selectCls} value={worksheet} disabled={locked} onChange={event => { changed(); setWorksheet(event.target.value) }}>
          <option value="">{t('drawingImport.chooseSheet')}</option>{sheets.map(sheet => <option key={sheet.name} value={sheet.name}>{sheet.name}{sheet.state !== 'visible' ? ` (${sheet.state})` : ''}</option>)}
        </select></Field>}
        <details><summary className="cursor-pointer text-sm font-medium">{t('drawingImport.sourceOptional')}</summary><Field label={t('drawingImport.source')} htmlFor="drawing-import-source" className="mt-2"><Input id="drawing-import-source" value={source} maxLength={200} disabled={locked} onChange={event => { changed(); setSource(event.target.value) }} /></Field></details>
        {(correctionRequired ?? options.data?.correctionReasonRequired) && <Field label={t('drawingImport.reason')} htmlFor="drawing-import-reason"><Textarea id="drawing-import-reason" value={reason} maxLength={1000} disabled={locked} onChange={event => { changed(); setReason(event.target.value) }} /></Field>}
      </>}
      {error != null && <ErrorBanner error={error} />}
      {uncertain && <p role="status" className="rounded-md border p-3 text-sm">{t('drawingImport.uncertain')}</p>}
      {stale && <p role="status" className="text-sm">{t('drawingImport.inspectAgain')}</p>}
      {created ? <section aria-label={t('drawingImport.result')} className="space-y-2"><p role="status" className="font-medium">{t('drawingImport.created', { n: created.total })}</p>
        <ul className="max-h-60 overflow-y-auto text-sm">{created.rows.map(row => <li key={row.id} className="border-t py-2"><span className="font-mono">{row.key}</span> · {t('drawingImport.row', { n: row.sourceRow })}{row.warnings.map((warning, index) => <p key={index} className="text-xs text-muted-foreground">{warning}</p>)}</li>)}</ul>
      </section> : preview && <section aria-label={t('drawingImport.preview')} className="min-w-0 space-y-3">
        <p role="status" className="text-sm font-medium">{t('drawingImport.summary', { creates: preview.creates, errors: preview.errors })}</p>
        <p className="text-xs text-muted-foreground">{t('drawingImport.notReserved')}</p>
        {preview.diagnostics.length > 0 && <ul className="space-y-1 text-sm">{preview.diagnostics.map((diagnostic, index) => <li key={index} className="break-words">{diagnostic.sheet && `${diagnostic.sheet} · `}{diagnostic.sourceRow != null && `${t('drawingImport.row', { n: diagnostic.sourceRow })} · `}{diagnostic.message}</li>)}</ul>}
        <ol className="max-h-[40dvh] min-w-0 space-y-2 overflow-y-auto" aria-label={t('drawingImport.rows')}>
          {preview.rows.map(row => <li key={row.sourceRow} data-drawing-import-row={row.sourceRow}><details id={`drawing-import-row-${projectId}-${row.sourceRow}`} data-person-source-disclosure open={row.errors.length > 0} className="rounded-md border p-3">
            <summary className="cursor-pointer break-words text-sm font-medium">{t('drawingImport.row', { n: row.sourceRow })} · {row.values?.name || t('drawingImport.unresolved')} · {t(row.errors.length ? 'drawingImport.invalid' : 'drawingImport.create')}{row.hidden && ` · ${t('drawingImport.hidden')}`}</summary>
            {[...row.errors, ...row.warnings].map((message, index) => <p key={index} className="mt-2 break-words text-sm">{message.field && `${message.field}: `}{message.message}</p>)}
            {row.sourceRowKey && <dl className="mt-2 text-sm"><dt className="text-xs text-muted-foreground">{t('drawingImport.sourceRowKey')}</dt><dd className="break-words">{row.sourceRowKey}</dd></dl>}
            {row.values && <dl className="mt-3 grid min-w-0 gap-2 text-sm sm:grid-cols-2">
              <div><dt className="text-xs text-muted-foreground">{t('drawingImport.disciplineType')}</dt><dd className="break-words">{row.values.projectDisciplineName} · {row.values.deliverableTypeName}</dd></div>
              <div><dt className="text-xs text-muted-foreground">{t('drawingImport.owner')}</dt><dd><PersonPanelTrigger userId={row.values.ownerId} name={row.values.ownerName} focusKey={`drawing-import:${projectId}:${row.sourceRow}:owner`} />{row.values.ownerFromLead && <span className="ml-1 text-xs text-muted-foreground">{t('drawingImport.leadDefault')}</span>}</dd></div>
              <div><dt className="text-xs text-muted-foreground">{t('drawingImport.reviewer')}</dt><dd>{row.values.reviewerId && row.values.reviewerName ? <PersonPanelTrigger userId={row.values.reviewerId} name={row.values.reviewerName} focusKey={`drawing-import:${projectId}:${row.sourceRow}:reviewer`} /> : <Missing />}</dd></div>
              <div><dt className="text-xs text-muted-foreground">{t('drawingImport.numberRevision')}</dt><dd className="break-words">{row.values.clientDrawingNumber || '—'} · {row.values.revision || '—'}</dd></div>
              <div><dt className="text-xs text-muted-foreground">{t('drawingImport.dates')}</dt><dd>{fmtDate(row.values.startDate)} → {fmtDate(row.values.dueDate)}{row.values.dueFromMilestone && <span className="ml-1 text-xs text-muted-foreground">{t('drawingImport.milestoneDefault')}</span>}</dd></div>
              <div><dt className="text-xs text-muted-foreground">{t('drawingImport.milestone')}</dt><dd className="break-words">{row.values.milestoneKey || <Missing />}</dd></div>
              <div><dt className="text-xs text-muted-foreground">{t('drawingImport.priorityReview')}</dt><dd>{row.values.priority} · {t(row.values.requiresReview ? 'drawingImport.reviewRequired' : 'drawingImport.noReview')}</dd></div>
              {row.values.description && <div className="sm:col-span-2"><dt className="text-xs text-muted-foreground">{t('drawingImport.description')}</dt><dd className="whitespace-pre-wrap break-words">{row.values.description}</dd></div>}
            </dl>}
          </details></li>)}
        </ol>
      </section>}
    </div>
    <DialogFooter className="flex-wrap"><Button variant="outline" disabled={busy} onClick={close}>{t('common.close')}</Button>
      {!created && <><Button variant="outline" disabled={!file || busy || uncertain || !options.data?.canImport} onClick={() => void inspect()}>{t('drawingImport.preview')}</Button>
        <Button disabled={busy || stale || !preview?.canCommit || preview.creates === 0} onClick={() => void commit()}>{t(uncertain ? 'drawingImport.retry' : 'drawingImport.commit', { n: preview?.creates ?? 0 })}</Button></>}
    </DialogFooter>
  </DialogContent></Dialog>
}
