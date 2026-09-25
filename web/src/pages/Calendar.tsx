import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, ChevronLeft, ChevronRight, Flag, HardHat, ListTodo, Plus, Users } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Loading, Page, Spinner, selectCls } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, get, patch, post, qs } from '@/lib/api'
import { addDays, daysBetween, fmtDate, shortDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'

interface Deadline { kind: 'deadline'; sourceType: 'Task' | 'Deliverable' | 'Milestone'; id: string; key: string; title: string; date: string; projectId: string; projectNumber: string; status?: string; overdue: boolean }
interface CalEvent {
  kind: 'event'; id: string; type: string; title: string; start: string; end: string; timeZone: string; projectId?: string | null; projectNumber?: string | null; projectName?: string | null
  owner?: string; location?: string | null; description?: string | null; visibility: string; cancelled: boolean; rowVersion: number; canEdit: boolean
}
type Entry = Deadline | CalEvent
interface Range { timeZone: string; entries: Entry[] }

const TYPES = [
  { id: 'Deadline', label: 'calendar.type.Deadline', icon: Flag, tone: 'var(--bad)' },
  { id: 'Meeting', label: 'calendar.type.Meeting', icon: Users, tone: 'var(--work)' },
  { id: 'Site Work', label: 'calendar.type.Site Work', icon: HardHat, tone: 'var(--warn)' },
  { id: 'Internal Task', label: 'calendar.type.Internal Task', icon: ListTodo, tone: 'var(--ok)' },
] as const
const toneOf = (e: Entry) => TYPES.find((x) => x.id === (e.kind === 'deadline' ? 'Deadline' : e.type))?.tone ?? 'var(--idle)'
const HOUR = 40, FIRST = 6, LAST = 20
const monday = (d: string) => addDays(d, -((new Date(d + 'T00:00:00Z').getUTCDay() + 6) % 7))
const dayOf = (e: Entry) => (e.kind === 'deadline' ? e.date : e.start.slice(0, 10))
const time = (s: string) => s.slice(11, 16)
const weekday = (d: string) => t(`calendar.dow.${new Date(d + 'T00:00:00Z').getUTCDay()}`)

/** Team calendar (§36.5, FR-VIS-06): Week, Month and Agenda over selected projects, four type toggles, all-day project
 *  deadlines that open their source item, and events created, edited and cancelled here. */
export function CalendarPage() {
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); n.delete('panel'); setSp(n, { replace: true }) }
  const view = (sp.get('mode') as 'week' | 'month' | 'agenda' | null) ?? 'week'
  const anchor = sp.get('date') ?? today()
  const hidden = (sp.get('hide') ?? '').split(',').filter(Boolean)
  const scope = useScope()
  const [editing, setEditing] = useState<CalEvent | 'new' | null>(null)
  const from = view === 'week' ? monday(anchor) : view === 'month' ? monday(anchor.slice(0, 8) + '01') : anchor
  const to = view === 'week' ? addDays(from, 6) : view === 'month' ? addDays(from, 41) : addDays(anchor, 30)
  const types = TYPES.map((x) => x.id).filter((x) => !hidden.includes(x))
  const q = useQuery({ queryKey: ['calendar', from, to, scope.api, types.join(',')], enabled: types.length > 0 && scope.ready,
    queryFn: () => get<Range>(`calendar${qs({ from, to, projectIds: scope.api, types })}`) })
  const step = (n: number) => set('date', view === 'week' ? addDays(anchor, 7 * n) : view === 'month' ? shiftMonth(anchor, n) : addDays(anchor, 30 * n))
  const toggle = (id: string) => set('hide', (hidden.includes(id) ? hidden.filter((x) => x !== id) : [...hidden, id]).join(',') || null)
  const open = (e: Entry) => (e.kind === 'deadline' ? openPanel(e.sourceType, e.id) : setEditing(e))
  const entries = types.length === 0 ? [] : q.data?.entries ?? []
  const label = view === 'month' ? monthTitle(anchor) : `${fmtDate(from)} – ${fmtDate(to)}`

  return (
    <Page title={t('nav.calendar')} subtitle={t('calendar.subtitle', { zone: q.data?.timeZone ?? '' })}
      actions={<Button onClick={() => setEditing('new')}><Plus className="size-4" />{t('calendar.new')}</Button>}>
      <WorkspaceTabs />
      <div className="flex flex-wrap items-center gap-2">
        <div className="inline-flex overflow-hidden rounded-md border bg-card" role="group" aria-label={t('calendar.view')}>
          {(['week', 'month', 'agenda'] as const).map((m) => (
            <button key={m} type="button" aria-pressed={view === m} onClick={() => set('mode', m === 'week' ? null : m)} className={cn('h-8 px-3 text-sm', view === m ? 'bg-accent font-medium' : 'hover:bg-muted')}>{t(`calendar.mode.${m}`)}</button>
          ))}
        </div>
        <Button variant="outline" size="sm" onClick={() => set('date', null)}>{t('common.today')}</Button>
        <Button variant="ghost" size="icon" className="size-8" aria-label={t('calendar.previous')} onClick={() => step(-1)}><ChevronLeft className="size-4" /></Button>
        <Button variant="ghost" size="icon" className="size-8" aria-label={t('calendar.next')} onClick={() => step(1)}><ChevronRight className="size-4" /></Button>
        <span className="text-sm font-medium" aria-live="polite">{label}</span>
      </div>
      <div className="flex flex-wrap gap-1.5" role="group" aria-label={t('calendar.types')}>
        {TYPES.map((x) => (
          <button key={x.id} type="button" aria-pressed={!hidden.includes(x.id)} onClick={() => toggle(x.id)}
            className={cn('inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs', hidden.includes(x.id) ? 'bg-card text-muted-foreground line-through' : 'bg-accent')}>
            <x.icon className="size-3.5" style={{ color: x.tone }} aria-hidden />{t(x.label)}
          </button>
        ))}
      </div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {types.length > 0 && q.isPending ? <Loading rows={8} /> : view === 'week' ? <Week from={from} entries={entries} onOpen={open} />
        : view === 'month' ? <Month from={from} anchor={anchor} entries={entries} onOpen={open} onDay={(d) => setSp(new URLSearchParams({ ...Object.fromEntries(sp), date: d, mode: 'week' }))} />
        : <Agenda from={from} to={to} entries={entries} onOpen={open} />}
      {editing && <EventDialog event={editing === 'new' ? null : editing} anchor={anchor} onClose={() => setEditing(null)} />}
    </Page>
  )
}

function shiftMonth(d: string, n: number) {
  const [y, m] = d.split('-').map(Number)
  const date = new Date(Date.UTC(y, m - 1 + n, 1))
  return date.toISOString().slice(0, 10)
}
function monthTitle(d: string) { const [y, m] = d.split('-'); return `${t(`calendar.month.${Number(m)}`)} ${y}` }

function Chip({ e, onOpen, compact }: { e: Entry; onOpen: (e: Entry) => void; compact?: boolean }) {
  const deadline = e.kind === 'deadline'
  return (
    <button type="button" onClick={() => onOpen(e)} title={deadline ? `${e.key} ${e.title} · ${e.projectNumber}` : `${time(e.start)}–${time(e.end)} ${e.title}${e.location ? ` · ${e.location}` : ''}`}
      className={cn('flex w-full min-w-0 items-center gap-1 rounded border-l-4 bg-card px-1.5 py-0.5 text-left text-[11px] shadow-sm hover:bg-muted', deadline && e.overdue && 'text-bad')}
      style={{ borderLeftColor: toneOf(e) }}>
      {deadline ? <><Flag className="size-3 shrink-0" aria-hidden /><span className="sr-only">{t('calendar.type.Deadline')}:</span>{!compact && <span className="key shrink-0 text-muted-foreground">{e.key.split('-').pop()}</span>}</>
        : <span className="shrink-0 tabular-nums text-muted-foreground">{time(e.start)}</span>}
      <span className="truncate">{e.title}</span>
    </button>
  )
}

/** Week: all-day deadlines on top, timed events on an hour grid; overlapping events sit side by side (§36.5). */
function Week({ from, entries, onOpen }: { from: string; entries: Entry[]; onOpen: (e: Entry) => void }) {
  const days = Array.from({ length: 7 }, (_, i) => addDays(from, i))
  const now = today()
  return ( // focusable so the keyboard can scroll a week with no events in it (WCAG 2.1.1)
    <div className="overflow-x-auto rounded-lg border bg-card focus-visible:ring-2 focus-visible:ring-ring" tabIndex={0} role="region" aria-label={t('calendar.mode.week')}>
      <div className="grid min-w-[46rem]" style={{ gridTemplateColumns: '3.5rem repeat(7, minmax(0, 1fr))' }}>
        <div className="border-b" />
        {days.map((d) => <div key={d} className={cn('border-b border-l px-2 py-1 text-xs', d === now && 'bg-accent font-semibold')}>{weekday(d)} {shortDate(d)}</div>)}
        <div className="border-b px-1 py-1 text-[10px] text-muted-foreground">{t('calendar.allDay')}</div>
        {days.map((d) => (
          <div key={d} className="space-y-0.5 border-b border-l p-1" role="group" aria-label={t('calendar.allDayOn', { date: fmtDate(d) })}>
            {entries.filter((e) => e.kind === 'deadline' && e.date === d).map((e) => <Chip key={e.id} e={e} onOpen={onOpen} />)}
          </div>
        ))}
        <div className="relative" style={{ height: (LAST - FIRST) * HOUR }}>
          {Array.from({ length: LAST - FIRST }, (_, i) => <span key={i} className="absolute right-1 text-[10px] text-muted-foreground" style={{ top: i * HOUR - 6 }}>{String(FIRST + i).padStart(2, '0')}:00</span>)}
        </div>
        {days.map((d) => <DayColumn key={d} day={d} events={entries.filter((e): e is CalEvent => e.kind === 'event' && e.start.slice(0, 10) <= d && e.end.slice(0, 10) >= d)} onOpen={onOpen} />)}
      </div>
    </div>
  )
}

function DayColumn({ day, events, onOpen }: { day: string; events: CalEvent[]; onOpen: (e: Entry) => void }) {
  const mins = (s: string) => (s.slice(0, 10) < day ? 0 : s.slice(0, 10) > day ? 24 * 60 : Number(s.slice(11, 13)) * 60 + Number(s.slice(14, 16)))
  const sorted = [...events].sort((a, b) => a.start.localeCompare(b.start))
  const lanes: number[] = []
  const placed = sorted.map((e) => {
    const top = Math.max(mins(e.start) - FIRST * 60, 0), bottom = Math.min(mins(e.end) - FIRST * 60, (LAST - FIRST) * 60)
    let lane = lanes.findIndex((end) => end <= top)
    if (lane < 0) { lane = lanes.length; lanes.push(0) }
    lanes[lane] = Math.max(bottom, top + 20)
    return { e, top, height: Math.max(bottom - top, 20), lane }
  })
  const n = Math.max(lanes.length, 1)
  return (
    <div className="relative border-l" style={{ height: (LAST - FIRST) * HOUR, backgroundImage: `repeating-linear-gradient(to bottom, var(--border) 0 1px, transparent 1px ${HOUR}px)` }}>
      {placed.map(({ e, top, height, lane }) => (
        <button key={e.id} type="button" onClick={() => onOpen(e)} className="absolute overflow-hidden rounded border-l-4 bg-card px-1 text-left text-[11px] leading-tight shadow hover:z-10 hover:bg-muted"
          style={{ top: (top / 60) * HOUR, height: (height / 60) * HOUR, left: `${(lane / n) * 100}%`, width: `${100 / n}%`, borderLeftColor: toneOf(e) }}
          title={`${time(e.start)}–${time(e.end)} ${e.title}${e.location ? ` · ${e.location}` : ''}`}>
          <span className="block tabular-nums text-muted-foreground">{time(e.start)}–{time(e.end)}</span>
          <span className="block truncate font-medium">{e.title}</span>
          {e.projectNumber && <span className="block truncate text-muted-foreground">{e.projectNumber}</span>}
        </button>
      ))}
    </div>
  )
}

function Month({ from, anchor, entries, onOpen, onDay }: { from: string; anchor: string; entries: Entry[]; onOpen: (e: Entry) => void; onDay: (d: string) => void }) {
  const days = Array.from({ length: 42 }, (_, i) => addDays(from, i))
  const month = anchor.slice(0, 7)
  const now = today()
  return (
    <div className="overflow-x-auto rounded-lg border bg-card">
      <div className="grid min-w-[46rem] grid-cols-7">
        {days.slice(0, 7).map((d) => <div key={d} className="border-b px-2 py-1 text-xs text-muted-foreground">{weekday(d)}</div>)}
        {days.map((d) => {
          const list = entries.filter((e) => dayOf(e) === d || (e.kind === 'event' && e.start.slice(0, 10) < d && e.end.slice(0, 10) >= d))
          return (
            <div key={d} className={cn('min-h-24 space-y-0.5 border-b border-l p-1', d.slice(0, 7) !== month && 'bg-muted/30 text-muted-foreground')}>
              <button type="button" className={cn('rounded px-1 text-xs hover:bg-muted', d === now && 'bg-primary text-primary-foreground')} onClick={() => onDay(d)}
                aria-label={t('calendar.openWeek', { date: fmtDate(d) })}>{Number(d.slice(8))}</button>
              {list.slice(0, 3).map((e) => <Chip key={`${e.kind}-${e.id}`} e={e} onOpen={onOpen} compact />)}
              {list.length > 3 && <button type="button" className="px-1 text-[11px] text-muted-foreground hover:underline" onClick={() => onDay(d)}>{t('calendar.more', { n: list.length - 3 })}</button>}
            </div>
          )
        })}
      </div>
    </div>
  )
}

function Agenda({ from, to, entries, onOpen }: { from: string; to: string; entries: Entry[]; onOpen: (e: Entry) => void }) {
  const days = Array.from({ length: daysBetween(from, to) + 1 }, (_, i) => addDays(from, i)).filter((d) => entries.some((e) => dayOf(e) === d))
  if (days.length === 0) return <div className="rounded-lg border bg-card"><Empty>{t('calendar.nothing')}</Empty></div>
  return (
    <div className="divide-y rounded-lg border bg-card">
      {days.map((d) => {
        const list = entries.filter((e) => dayOf(e) === d).sort((a, b) => (a.kind === 'deadline' ? '' : a.start).localeCompare(b.kind === 'deadline' ? '' : b.start))
        return (
          <section key={d} aria-label={fmtDate(d)} className="grid gap-2 px-4 py-2 sm:grid-cols-[9rem_1fr]">
            <h2 className={cn('text-sm font-semibold', d === today() && 'text-primary')}>{weekday(d)} {fmtDate(d)}</h2>
            <ul className="space-y-1">
              {list.map((e) => (
                <li key={`${e.kind}-${e.id}`}>
                  <button type="button" className="flex w-full flex-wrap items-center gap-x-2 rounded px-1 py-0.5 text-left text-sm hover:bg-muted" onClick={() => onOpen(e)}>
                    <span className="inline-block h-3 w-1 rounded" style={{ background: toneOf(e) }} aria-hidden />
                    <span className="w-24 shrink-0 text-xs tabular-nums text-muted-foreground">{e.kind === 'deadline' ? t('calendar.allDay') : `${time(e.start)}–${time(e.end)}`}</span>
                    {e.kind === 'deadline' && <Key>{e.key}</Key>}
                    <span className={cn('font-medium', e.kind === 'deadline' && e.overdue && 'text-bad')}>{e.title}</span>
                    <span className="text-xs text-muted-foreground">{e.kind === 'deadline' ? `${t(`itemType.${e.sourceType}`)} · ${e.projectNumber}` : [tv(e.type), e.projectNumber, e.location].filter(Boolean).join(' · ')}</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}

/** New Event and event detail (§36.5): type, title, project (required for Meeting and Site Work), start, end in the
 *  organisation's time zone, location, description, visibility; the owner or a project PM edits and cancels. */
export function EventDialog({ event, anchor, onClose }: { event: CalEvent | null; anchor: string; onClose: () => void }) {
  const qc = useQueryClient()
  const readOnly = !!event && !event.canEdit
  const [f, setF] = useState<Record<string, string>>(event ? {
    type: event.type, title: event.title, projectId: event.projectId ?? '', start: event.start, end: event.end, location: event.location ?? '', description: event.description ?? '', visibility: event.visibility,
  } : { type: 'Meeting', title: '', projectId: '', start: `${anchor}T09:00`, end: `${anchor}T10:00`, location: '', description: '', visibility: 'Project' })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const projects = useQuery({ queryKey: ['projects-pick', ''], queryFn: () => get<PageOf<{ id: string; projectNumber: string; name: string }>>('projects?pageSize=200') })
  const fe = err?.fieldErrors ?? {}
  const done = () => { qc.invalidateQueries({ queryKey: ['calendar'] }); onClose() }
  const save = async () => {
    setBusy(true); setErr(null)
    const body = { ...f, projectId: f.projectId || null, location: f.location || null, description: f.description || null, visibility: f.projectId ? f.visibility : 'Private' }
    try {
      if (event) await patch(`calendar/events/${event.id}`, body, event.rowVersion); else await post('calendar/events', body)
      toast.success(t('common.saved')); done()
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const cancel = async () => {
    if (!event) return
    try { await post(`calendar/events/${event.id}/cancel`, { rowVersion: event.rowVersion }); toast.success(t('calendar.cancelled')); done() } catch (e) { setErr(e as ApiError) }
  }
  const input = (k: string, props: Record<string, unknown> = {}) => ({ id: `ev-${k}`, value: f[k] ?? '', disabled: readOnly, onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value }), ...props })
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2"><CalendarClock className="size-4" aria-hidden />{event ? event.title : t('calendar.new')}</DialogTitle>
          <DialogDescription>{event && readOnly ? t('calendar.readOnly', { owner: event.owner ?? '' }) : t('calendar.zoneNote', { zone: event?.timeZone ?? t('calendar.orgZone') })}</DialogDescription>
        </DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); save() }}>
          <Field label={t('common.type')} htmlFor="ev-type"><select className={selectCls} {...input('type')}>{TYPES.slice(1).map((x) => <option key={x.id} value={x.id}>{t(x.label)}</option>)}</select></Field>
          <Field label={t('calendar.project')} htmlFor="ev-projectId" error={fe.projectId} hint={f.type === 'Internal Task' ? t('calendar.projectOptional') : undefined}>
            <select className={selectCls} {...input('projectId')}>
              <option value="">{t('calendar.noProject')}</option>{projects.data?.items.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
            </select>
          </Field>
          <Field label={t('calendar.title')} htmlFor="ev-title" error={fe.title} className="sm:col-span-2"><Input required maxLength={200} {...input('title')} /></Field>
          <Field label={t('calendar.start')} htmlFor="ev-start" error={fe.start}><Input type="datetime-local" required {...input('start')} /></Field>
          <Field label={t('calendar.end')} htmlFor="ev-end" error={fe.end}><Input type="datetime-local" required {...input('end')} /></Field>
          <Field label={t('calendar.location')} htmlFor="ev-location" className="sm:col-span-2"><Input maxLength={300} {...input('location')} /></Field>
          <Field label={t('common.description')} htmlFor="ev-description" className="sm:col-span-2"><Textarea rows={3} {...input('description')} /></Field>
          <Field label={t('calendar.visibility')} htmlFor="ev-visibility" hint={f.projectId ? undefined : t('calendar.privateOnly')}>
            <select className={selectCls} {...input('visibility', { disabled: readOnly || !f.projectId })}>
              <option value="Project">{t('calendar.vis.Project')}</option><option value="Private">{t('calendar.vis.Private')}</option>
            </select>
          </Field>
          {event?.owner && <p className="self-end pb-2 text-xs text-muted-foreground sm:col-span-1">{t('calendar.owner', { name: event.owner })}</p>}
          {err && !Object.keys(fe).length && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="gap-2 sm:col-span-2">
            {event && !readOnly && <Button type="button" variant="outline" className="text-bad sm:mr-auto" onClick={cancel}>{t('calendar.cancelEvent')}</Button>}
            <Button type="button" variant="outline" onClick={onClose}>{t('common.close')}</Button>
            {!readOnly && <Button type="submit" disabled={busy}>{busy && <Spinner />}{t('common.save')}</Button>}
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
