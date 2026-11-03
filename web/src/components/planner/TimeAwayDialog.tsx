import { useRef, useState } from 'react'
import { useLocation } from 'react-router'
import { ErrorBanner, Field, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { DraftNotice } from '@/components/hub/drafts'
import { useSafeForm } from '@/hooks/drafts'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { t } from '@/lib/i18n'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { useFlag } from '@/lib/auth'
import { recordContextReturn } from '@/lib/record-context-navigation'
import type { FormNavigationLocation } from '@/lib/drafts'

export type LeaveRecord = components['schemas']['AvailabilityLeave']
const awayRouteUrl = (location: FormNavigationLocation) => location.pathname + location.search + location.hash
const awaySourceRoute = (location: FormNavigationLocation) => {
  const params = new URLSearchParams(location.search)
  for (const key of ['panel', 'personStaffScope', 'personProjectsPage', 'personProjectsPageSize']) params.delete(key)
  params.sort()
  return location.pathname + '?' + params.toString() + location.hash
}
const awayPersonTarget = (location: FormNavigationLocation) => {
  const target = new URLSearchParams(location.search).get('panel')
  return target && /^Person:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(target) ? target : null
}
export function TimeAwayDialog({ person, record, onClose, onChanged }: { person: { id: string; displayName: string } | null; record?: LeaveRecord; onClose: () => void; onChanged: () => void }) {
  const location = useLocation(), peopleOn = useFlag('people_screens')
  const mounted = useRef({ route: awaySourceRoute(location), personId: person?.id, recordId: record?.id ?? 'new' })
  const [from, setFrom] = useState(record?.fromDate ?? ''); const [through, setThrough] = useState(record?.throughDate ?? '')
  const [source, setSource] = useState(record?.sourceSystem ?? ''); const [reference, setReference] = useState(record?.externalId ?? '')
  const [fullDay, setFullDay] = useState(record?.hoursPerDay == null); const [hours, setHours] = useState(String(record?.hoursPerDay ?? 4)); const [status, setStatus] = useState(record?.status ?? 'Approved')
  const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null); const [stale, setStale] = useState(false)
  const receipt = useRef({ signature: '', key: '' })
  const safe = useSafeForm(record || !person ? null : `timeaway.new.${person.id}`, { from, through, source, reference, fullDay, hours, status },
    (d) => { setFrom(d.from); setThrough(d.through); setSource(d.source); setReference(d.reference); setFullDay(d.fullDay); setHours(d.hours); setStatus(d.status) },
    (fromLocation, toLocation) => {
      if (!peopleOn || !person || person.id !== mounted.current.personId || (record?.id ?? 'new') !== mounted.current.recordId
        || awaySourceRoute(fromLocation) !== mounted.current.route || awaySourceRoute(toLocation) !== mounted.current.route) return false
      const fromPerson = awayPersonTarget(fromLocation), toPerson = awayPersonTarget(toLocation)
      const fromReturn = recordContextReturn(fromLocation.state), toReturn = recordContextReturn(toLocation.state)
      if ((!fromPerson && new URLSearchParams(fromLocation.search).has('panel')) || (!toPerson && new URLSearchParams(toLocation.search).has('panel'))) return false
      if (toPerson && toReturn?.url === awayRouteUrl(fromLocation)) return true
      if (fromPerson && fromReturn?.url === awayRouteUrl(toLocation)) return true
      return !!fromPerson && fromPerson === toPerson && !!fromReturn && !!toReturn && fromReturn.url === toReturn.url
    }) // FR-SAV-03; the same mounted person/record only. Explicit close and other navigation remain guarded.
  const submit = async () => {
    if (!person || stale || busy) return
    const body = { personId: person.id, sourceSystem: source.trim(), externalId: reference.trim(), fromDate: from, throughDate: through, hoursPerDay: fullDay ? null : Number(hours), status, rowVersion: record?.rowVersion ?? 0 }
    const signature = JSON.stringify(body); if (receipt.current.signature !== signature) receipt.current = { signature, key: crypto.randomUUID() }
    setBusy(true); setError(null)
    try { await typedRequest('post', '/api/v1/planning/leave/manual', { body, idempotencyKey: receipt.current.key }); safe.clear(); onChanged(); onClose() }
    catch (e) { setError(e); if (e instanceof ApiError && e.status === 409) setStale(true) }
    finally { setBusy(false) }
  }
  const titleParts = t('availability.manualTitle', { person: '\uFFFC' }).split('\uFFFC')
  const title = person?.id && person.displayName.trim() && titleParts.length === 2
    ? <>{titleParts[0]}<PersonPanelTrigger userId={person.id} name={person.displayName} focusKey={`time-away:${person.id}:${record?.id ?? 'new'}:title`}>{person.displayName}</PersonPanelTrigger>{titleParts[1]}</>
    : t('availability.manualTitle', { person: person?.displayName ?? '' })
  return <Dialog open={!!person} onOpenChange={(open) => !open && !busy && safe.close(onClose)}><DialogContent className="max-w-lg"><DialogHeader><DialogTitle>{title}</DialogTitle><DialogDescription>{t('availability.manualHint')}</DialogDescription></DialogHeader>
    <DraftNotice form={safe} />
    {error != null && <ErrorBanner error={error} retry={stale ? () => { onChanged(); onClose() } : submit} retryLabel={stale ? t('error.reload') : undefined} />}
    <fieldset disabled={busy || stale} className="grid gap-3 sm:grid-cols-2">
      <Field label={t('availability.source')} htmlFor="away-source"><Input id="away-source" maxLength={120} value={source} disabled={!!record} onChange={(e) => setSource(e.target.value)} /></Field>
      <Field label={t('availability.reference')} htmlFor="away-reference"><Input id="away-reference" maxLength={120} value={reference} disabled={!!record} onChange={(e) => setReference(e.target.value)} /></Field>
      <Field label={t('common.from')} htmlFor="away-from"><Input id="away-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
      <Field label={t('common.to')} htmlFor="away-through"><Input id="away-through" type="date" min={from} value={through} onChange={(e) => setThrough(e.target.value)} /></Field>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={fullDay} onChange={(e) => setFullDay(e.target.checked)} />{t('availability.fullDay')}</label>
      {!fullDay && <Field label={t('availability.absentHours')} htmlFor="away-hours"><Input id="away-hours" type="number" min="0.5" max="24" step="0.5" value={hours} onChange={(e) => setHours(e.target.value)} /></Field>}
      {record && <Field label={t('common.status')} htmlFor="away-status"><select id="away-status" className="h-(--control-h) rounded border px-3 text-sm" value={status} onChange={(e) => setStatus(e.target.value)}><option value="Approved">{t('availability.approved')}</option><option value="Cancelled">{t('availability.cancelled')}</option></select></Field>}
    </fieldset><DialogFooter><Button variant="outline" onClick={() => safe.close(onClose)} disabled={busy}>{t('common.cancel')}</Button><Button onClick={submit} disabled={busy || stale || !source.trim() || !reference.trim() || !from || !through || through < from || !fullDay && (!(Number(hours) > 0) || Number(hours) > 24)}>{busy && <Spinner />}{t('common.save')}</Button></DialogFooter>
  </DialogContent></Dialog>
}
