import { useRef, useState } from 'react'
import { ErrorBanner, Field, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { DraftNotice } from '@/components/hub/drafts'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useSafeForm } from '@/hooks/drafts'
import { typedRequest } from '@/lib/api'
import { runtimeText, t  } from '@/lib/i18n'

type Preview = { rows: { row: number; projectNumber: string; name: string; classification: string; error?: string | null }[]; creates: number; exists: number; errors: number; canApply: boolean }
export function ProjectImportDialog({ onClose, onChanged }: { onClose: () => void; onChanged: () => void }) {
  const [csv, setCsv] = useState('')
  const [preview, setPreview] = useState<Preview | null>(null)
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const receipt = useRef(crypto.randomUUID())
  const change = (value: string) => { setCsv(value); setPreview(null); setDone(false); setError(null); receipt.current = crypto.randomUUID() }
  const safe = useSafeForm('project.import', csv, change) // FR-SAV-03: a pasted CSV survives a closed dialog
  const run = async (apply: boolean) => { setBusy(true); setError(null); try { const result = apply
    ? await typedRequest('post', '/api/v1/projects/import', { body: { csv }, idempotencyKey: receipt.current })
    : await typedRequest('post', '/api/v1/projects/import-preview', { body: { csv } }); setPreview(result); if (apply) { safe.clear(); setDone(true); onChanged() } } catch (e) { setError(e) } finally { setBusy(false) } }
  return <Dialog open onOpenChange={open => !open && !busy && safe.close(onClose)}><DialogContent className="max-h-[90dvh] max-w-3xl overflow-y-auto"><DialogHeader><DialogTitle>{t('projectImport.title')}</DialogTitle><DialogDescription>{t('projectImport.hint')}</DialogDescription></DialogHeader>
    <DraftNotice form={safe} />
    {error != null && <ErrorBanner error={error} />}
    <Field label={t('projectImport.file')} htmlFor="project-import-file"><Input id="project-import-file" type="file" accept=".csv,text/csv" disabled={busy} onChange={e => { const file = e.target.files?.[0]; if (file) void file.text().then(change).catch(setError) }} /></Field>
    <Field label={t('projectImport.csv')} htmlFor="project-import-csv"><Textarea id="project-import-csv" value={csv} rows={6} disabled={busy} onChange={e => change(e.target.value)} /></Field>
    {preview && <><p role="status">{t(done ? 'projectImport.complete' : 'projectImport.summary', { creates: preview.creates, exists: preview.exists, errors: preview.errors })}</p><TableRegion role="region" aria-label={t('projectImport.preview')}><table className="w-full text-sm"><thead><tr>{(['row', 'number', 'name', 'classification', 'error'] as const).map(key => <th key={key} scope="col" className={thCls}>{t(`projectImport.${key}`)}</th>)}</tr></thead><tbody>{preview.rows.map(row => <tr key={row.row}><td className={tdCls}>{row.row}</td><td className={tdCls}>{row.projectNumber}</td><td className={tdCls}>{row.name}</td><td className={tdCls}>{runtimeText(`projectImport.${row.classification}`)}</td><td className={tdCls}>{row.error ?? '—'}</td></tr>)}</tbody></table></TableRegion></>}
    <DialogFooter><Button variant="outline" onClick={() => safe.close(onClose)} disabled={busy}>{t('common.close')}</Button><Button variant="outline" disabled={busy || !csv || done} onClick={() => void run(false)}>{t('projectImport.preview')}</Button><Button disabled={busy || !preview?.canApply || done || preview.creates === 0} onClick={() => void run(true)}>{t('projectImport.commit')}</Button></DialogFooter>
  </DialogContent></Dialog>
}
