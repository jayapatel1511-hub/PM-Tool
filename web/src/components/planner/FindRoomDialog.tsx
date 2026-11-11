import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { useQuery } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useReference } from '@/hooks/data'
import { typedRequest } from '@/lib/api'
import { hours, shortDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { Empty, ErrorBanner, Field, Loading, selectCls, tdCls, thCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { BareTable } from '@/components/ui/table'

/** Read-only search over the full permitted planner scope, not just the currently filtered grid rows. */
export function FindRoomDialog({ from, horizonWeeks, maxHours, disciplineId, onClose }: {
  from: string; horizonWeeks: number; maxHours: number; disciplineId?: string; onClose: () => void
}) {
  const reference = useReference()
  const [start, setStart] = useState(from)
  const [hoursPerWeek, setHoursPerWeek] = useState('8')
  const [weeks, setWeeks] = useState(String(Math.min(4, horizonWeeks)))
  const [discipline, setDiscipline] = useState(disciplineId ?? '')
  const [search, setSearch] = useState<{ From: string; HoursPerWeek: number; Weeks: number; DisciplineId?: string } | null>(null)
  // The existing planner invalidation prefix refreshes this read after plans, allocations or availability change.
  const result = useQuery({ queryKey: ['planning-grid', 'find-room', search], enabled: search !== null,
    queryFn: () => typedRequest('get', '/api/v1/planning/find-room', { query: search! }) })
  const submit = (event: FormEvent) => {
    event.preventDefault()
    setSearch({ From: start, HoursPerWeek: Number(hoursPerWeek), Weeks: Number(weeks), DisciplineId: discipline || undefined })
  }
  return <Dialog open onOpenChange={(open) => { if (!open) onClose() }}><DialogContent className="max-w-3xl">
    <DialogHeader><DialogTitle>{t('capacity.findRoom')}</DialogTitle><DialogDescription>{t('capacity.findRoomBasis')}</DialogDescription></DialogHeader>
    <div className="space-y-4">
      <form onSubmit={submit} className="flex flex-wrap items-end gap-3">
        <Field label={t('capacity.hoursPerWeek')} htmlFor="room-hours" className="w-36"><Input id="room-hours" type="number" required min="0.1" step="0.1" max={maxHours} value={hoursPerWeek} onChange={(event) => setHoursPerWeek(event.target.value)} /></Field>
        <Field label={t('capacity.numberOfWeeks')} htmlFor="room-weeks" className="w-32"><Input id="room-weeks" type="number" required min="1" max={horizonWeeks} step="1" value={weeks} onChange={(event) => setWeeks(event.target.value)} /></Field>
        <Field label={t('planner.startWeek')} htmlFor="room-from" className="w-44"><Input id="room-from" type="date" required step="7" min="1970-01-05" value={start} onChange={(event) => setStart(event.target.value)} /></Field>
        <Field label={t('capacity.homeDiscipline')} htmlFor="room-discipline" className="w-48"><select id="room-discipline" className={selectCls} value={discipline} onChange={(event) => setDiscipline(event.target.value)}><option value="">{t('planner.anyDiscipline')}</option>{reference.data?.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select></Field>
        <Button type="submit" disabled={result.isFetching}>{t('capacity.findRoom')}</Button>
      </form>
      {result.error && <ErrorBanner error={result.error} retry={() => result.refetch()} />}
      {result.isFetching ? <Loading rows={3} /> : result.data && <div aria-live="polite" className="space-y-4">
        <p className="text-sm text-muted-foreground">{t('capacity.roomMatches', { n: result.data.matches.length, h: hours(result.data.hoursPerWeek), weeks: result.data.weeks.length })}</p>
        {result.data.matches.length === 0 ? <Empty title={t('capacity.noRoomTitle')}>{t('capacity.noRoomHint')}</Empty> : <div className="overflow-auto rounded-lg border"><BareTable className="w-full text-sm"><caption className="sr-only">{t('capacity.roomResults')}</caption><thead><tr><th scope="col" className={thCls}>{t('planner.person')}</th>{result.data.weeks.map((week) => <th key={week} scope="col" className={thCls}>{t('planner.weekOf')} {shortDate(week)}</th>)}</tr></thead><tbody>{result.data.matches.map((person) => <tr key={person.id}><th scope="row" className={tdCls}><PersonPanelTrigger userId={person.id} name={person.displayName} focusKey={`find-room:${from}:${person.id}:match`} /></th>{person.weeks.map((week) => <td key={week.week} className={`${tdCls} tabular-nums`}>{t('capacity.freeHours', { h: hours(week.freeHours) })}</td>)}</tr>)}</tbody></BareTable></div>}
        {result.data.unavailable.length > 0 && <section aria-labelledby="room-unavailable"><h3 id="room-unavailable" className="text-sm font-medium">{t('capacity.roomUnavailable')}</h3><p className="text-sm text-muted-foreground">{t('capacity.roomUnavailableHint')}</p><ul className="mt-2 divide-y rounded-lg border text-sm">{result.data.unavailable.map((person) => <li key={person.id} className="flex flex-wrap justify-between gap-2 px-3 py-2"><PersonPanelTrigger userId={person.id} name={person.displayName} focusKey={`find-room:${from}:${person.id}:unavailable`} /><span className="text-muted-foreground">{t(person.reason === 'PartialView' ? 'capacity.partialView' : 'capacity.provisionalAvailability')}</span></li>)}</ul></section>}
      </div>}
    </div>
  </DialogContent></Dialog>
}
