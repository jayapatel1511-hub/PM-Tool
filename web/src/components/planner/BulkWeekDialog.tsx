import { useQuery } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { ErrorBanner, Loading, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ApiError, api, get, qs } from '@/lib/api'
import { t } from '@/lib/i18n'
import { confidenceLabel, hoursLabel } from './labels'

type Candidate = { id: string; rowVersion: number; personName: string; label: string; hoursPerWeek: number; confidence: string; warnings: string[] }
type Preview = { snapshot: string; entries: Candidate[] }
export function BulkWeekDialog({ operation, week, onClose, onChanged }: { operation: 'Copy' | 'Publish'; week: string; onClose: () => void; onChanged: () => void }) {
  const q = useQuery({ queryKey: ['week-preview', operation, week], queryFn: () => get<Preview>(`planning/week-preview${qs({ operation, week })}`) })
  const [selected, setSelected] = useState<Set<string>>(new Set()); const [busy, setBusy] = useState(false); const [stale, setStale] = useState(false); const [error, setError] = useState<unknown>(null)
  const receipt = useRef({ signature: '', key: '' })
  const reload = async () => { const next = await q.refetch(); if (!next.error) { setStale(false); setError(null); setSelected(new Set()); receipt.current = { signature: '', key: '' } } }
  const apply = async () => {
    if (busy || stale || !q.data) return
    const body = { operation, week, snapshot: q.data.snapshot, entries: q.data.entries.filter((e) => selected.has(e.id)).map((e) => ({ id: e.id, rowVersion: e.rowVersion })) }
    const signature = JSON.stringify(body); if (receipt.current.signature !== signature) receipt.current = { signature, key: crypto.randomUUID() }
    setBusy(true); setError(null)
    try { await api('planning/week-apply', { body, idempotencyKey: receipt.current.key }); onChanged(); onClose() }
    catch (e) { setError(e); if (e instanceof ApiError && e.status === 409) setStale(true) }
    finally { setBusy(false) }
  }
  const eligible = q.data?.entries.filter((e) => !e.warnings.includes('ProjectNotActive')) ?? []
  return <Dialog open onOpenChange={(open) => !open && !busy && onClose()}><DialogContent className="max-w-3xl"><DialogHeader><DialogTitle>{t(operation === 'Copy' ? 'planner.copyWeek' : 'planner.publishWeek')} · {week}</DialogTitle><DialogDescription>{t(operation === 'Copy' ? 'planner.copyWeekHint' : 'planner.publishWeekHint')}</DialogDescription></DialogHeader>
    {(error ?? q.error) != null && <ErrorBanner error={error ?? q.error} retry={stale || q.error ? reload : apply} retryLabel={stale ? t('error.reload') : undefined} />}
    {q.isPending ? <Loading rows={3} /> : q.data && <><Button size="sm" variant="outline" disabled={busy || stale} onClick={() => setSelected(new Set(selected.size ? [] : eligible.map((e) => e.id)))}>{t(selected.size ? 'planner.clearSelection' : 'planner.selectAll')}</Button><div className="max-h-96 overflow-auto rounded border"><table className="w-full text-left text-sm"><thead><tr>{['planner.selection', 'planner.person', 'planner.label', 'planner.hours', 'planner.confidence', 'planner.warnings'].map((key) => <th className="p-2" scope="col" key={key}>{t(key)}</th>)}</tr></thead><tbody>{q.data.entries.map((e) => <tr key={e.id} className="border-t"><td className="p-2"><input type="checkbox" aria-label={t('planner.selectEntry', { label: e.label, person: e.personName })} checked={selected.has(e.id)} disabled={busy || stale || e.warnings.includes('ProjectNotActive')} onChange={(event) => setSelected((current) => { const next = new Set(current); if (event.target.checked) next.add(e.id); else next.delete(e.id); return next })} /></td><td className="p-2">{e.personName}</td><td className="p-2">{e.label}</td><td className="p-2">{hoursLabel(e.hoursPerWeek)}</td><td className="p-2">{confidenceLabel(e.confidence)}</td><td className="p-2">{e.warnings.map((w) => t(`planner.bulkWarning.${w}`)).join(' · ')}</td></tr>)}</tbody></table>{q.data.entries.length === 0 && <p className="p-4 text-sm">{t('planner.noBulkEntries')}</p>}</div></>}
    <DialogFooter><Button variant="outline" onClick={onClose} disabled={busy}>{t('common.cancel')}</Button><Button onClick={apply} disabled={busy || stale || q.isError || q.isFetching || selected.size === 0}>{busy && <Spinner />}{t(operation === 'Copy' ? 'planner.copySelected' : 'planner.publishSelected')}</Button></DialogFooter>
  </DialogContent></Dialog>
}
