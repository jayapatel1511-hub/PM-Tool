import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, CalendarDays, ExternalLink, ListPlus, Plus, Users } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, Spinner, selectCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { FieldRow, HistoryList, InlineDate, InlineText, TabBar } from '@/components/hub/fields'
import { PANELS, useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { Chip, Key, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProject, useProjectRefresh } from '@/hooks/data'
import { ApiError, get, patch, post, qs } from '@/lib/api'
import { addDays, fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { CommentsSlot, ItemSlots } from './slots-items'
import { errorText } from './Tasks'

// Meetings & Actions (§12.11, §13.13, packet 015). Agendas, minutes and attendance are not built.

export interface ActionRow {
  id: string; projectId: string; projectNumber: string; key: string; meetingId: string; meetingTitle: string; meetingDate: string; text: string
  ownerType: 'User' | 'Discipline' | 'External Party'; ownerTypeLabel: string; ownerUserId?: string; ownerDisciplineId?: string; ownerExternalPartyId?: string
  ownerName?: string; leadId?: string; leadName?: string; noLead: boolean; partyIsClient: boolean; dueDate?: string; isOverdue: boolean; daysOverdue: number
  status: string; relatedTaskId?: string; taskKey?: string; relatedDecisionId?: string; decisionKey?: string; converted: boolean; rowVersion: number
}
interface MeetingRow { id: string; projectId: string; title: string; meetingDate: string; meetingType: string; notesLink?: string; calendarEventId?: string; calendarEventTitle?: string; open: number; total: number; rowVersion: number }
interface Perm { ok: boolean; reason?: string | null }
interface ActionDetail {
  action: ActionRow; meeting: { id: string; title: string; meetingDate: string; meetingType: string; notesLink?: string }
  task?: { id: string; key: string; name: string; status: string } | null; decision?: { id: string; key: string; subject: string; status: string } | null; taskDisciplineId?: string | null
  permissions: { edit: Perm; transitions: { to: string; ok: boolean; reason?: string | null }[]; convert: boolean; comment: boolean }
}

const TYPES = ['Coordination', 'Client', 'Design Review', 'Site', 'Other']
const STATUSES = ['Open', 'In Progress', 'Complete', 'Cancelled']
const OWNER_TYPES = ['User', 'Discipline', 'External Party'] as const
const FILTERS = ['q', 'status', 'ownerType', 'indicator'] as const

function useActionRefresh() {
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  return (projectId: string, id?: string) => {
    if (id) qc.invalidateQueries({ queryKey: ['action', id] })
    qc.invalidateQueries({ queryKey: ['coordination'] })
    qc.invalidateQueries({ queryKey: ['mywork'] })
    refresh(projectId)
  }
}

/** Who owns the action, with discipline actions showing their lead and external ones set apart (MTG-01, MTG-02). */
export function ActionOwner({ a }: { a: ActionRow }) {
  if (a.ownerType === 'External Party') return (
    <span className="inline-flex items-center gap-1" title={t('action.external')}><Building2 className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />{a.ownerName}
      <span className="sr-only">({t('action.external')})</span></span>
  )
  if (a.ownerType === 'Discipline') return (
    <span className="inline-flex flex-wrap items-center gap-1"><Users className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />{a.ownerName}
      {a.noLead ? <Chip tone="warn">{t('action.noLead')}</Chip> : <span className="text-muted-foreground">· {a.leadName}</span>}</span>
  )
  return <span>{a.ownerName}</span>
}

function Due({ a }: { a: ActionRow }) {
  return <span className={cn('whitespace-nowrap tabular-nums', a.isOverdue && 'font-medium text-bad')}>{fmtDate(a.dueDate)}{a.isOverdue && ` · ${t('ind.overdueD', { n: a.daysOverdue })}`}</span>
}

// ---------- Owner fields (FR-002) ----------

type Owner = { type: (typeof OWNER_TYPES)[number]; userId?: string | null; userName?: string | null; disciplineId?: string | null; partyId?: string | null }
export const ownerBody = (o: Owner) => ({
  ownerType: o.type, ownerUserId: o.type === 'User' ? o.userId ?? null : null, ownerDisciplineId: o.type === 'Discipline' ? o.disciplineId ?? null : null,
  ownerExternalPartyId: o.type === 'External Party' ? o.partyId ?? null : null,
})

function OwnerFields({ projectId, value, onChange, errors, prefix }: { projectId: string; value: Owner; onChange: (o: Owner) => void; errors?: string[]; prefix: string }) {
  const project = useProject(projectId)
  const parties = useQuery({ queryKey: ['p', projectId, 'parties'], queryFn: () => get<{ id: string; name: string; organisation?: string; isClient: boolean; isActive: boolean }[]>(`projects/${projectId}/external-parties`) })
  return (
    <fieldset className="space-y-1.5 sm:col-span-2">
      <legend className="text-sm font-medium">{t('common.owner')}</legend>
      <div className="flex flex-wrap gap-4 text-sm" role="radiogroup" aria-label={t('action.ownerType')}>
        {OWNER_TYPES.map((k) => <label key={k} className="flex items-center gap-1.5"><input type="radio" name={`${prefix}-owner-kind`} checked={value.type === k} onChange={() => onChange({ type: k })} />{t(`action.owner.${k}`)}</label>)}
      </div>
      {value.type === 'User' && <PeoplePicker value={value.userId} valueName={value.userName} onChange={(id, person) => onChange({ ...value, userId: id, userName: person?.displayName })} label={t('action.owner.User')} />}
      {value.type === 'Discipline' && (
        <select className={selectCls} value={value.disciplineId ?? ''} onChange={(e) => onChange({ ...value, disciplineId: e.target.value || null })} aria-label={t('action.owner.Discipline')}>
          <option value="">{t('action.chooseDiscipline')}</option>
          {(project.data?.disciplines ?? []).filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}{d.leadName ? ` · ${d.leadName}` : ` · ${t('action.noLead')}`}</option>)}
        </select>
      )}
      {value.type === 'External Party' && (
        <select className={selectCls} value={value.partyId ?? ''} onChange={(e) => onChange({ ...value, partyId: e.target.value || null })} aria-label={t('action.owner.External Party')}>
          <option value="">{t('decision.chooseParty')}</option>
          {(parties.data ?? []).filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}{x.organisation ? ` · ${x.organisation}` : ''}{x.isClient ? ` (${t('party.client')})` : ''}</option>)}
        </select>
      )}
      {value.type === 'External Party' && <p className="text-xs text-muted-foreground">{t('action.externalHint')}</p>}
      {value.type === 'Discipline' && <p className="text-xs text-muted-foreground">{t('action.disciplineHint')}</p>}
      {errors?.map((e) => <p key={e} className="text-xs text-bad" role="alert">{e}</p>)}
    </fieldset>
  )
}

const CURRENT = '__current'

/** New meeting action: against a chosen meeting, or today's coordination meeting in meeting mode (MTG-04). */
export function ActionForm({ projectId, meetingId, related, links, defaultOwner, onClose }: {
  projectId: string; meetingId?: string; related?: { taskId?: string; decisionId?: string; label: string }; links?: { targetType: string; targetId: string }[]
  defaultOwner?: Owner; onClose: (created?: { id: string; key: string; text: string }) => void
}) {
  const done = useActionRefresh()
  const meetings = useQuery({ queryKey: ['p', projectId, 'meetings'], queryFn: () => get<MeetingRow[]>(`projects/${projectId}/meetings`), enabled: !meetingId })
  const [meeting, setMeeting] = useState(meetingId ?? CURRENT)
  const [text, setText] = useState('')
  const [owner, setOwner] = useState<Owner>(defaultOwner ?? { type: 'User' })
  const [due, setDue] = useState(addDays(today(), 7))
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      const mid = meeting === CURRENT ? (await post<MeetingRow>(`projects/${projectId}/meetings/current`)).id : meeting
      const r = await post(`meetings/${mid}/actions`, { text, ...ownerBody(owner), dueDate: due || null, relatedTaskId: related?.taskId ?? null, relatedDecisionId: related?.decisionId ?? null, links })
      toast.success(t('action.added', { key: r.key })); done(projectId); onClose({ id: r.id, key: r.key, text })
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const fe = err?.fieldErrors ?? {}
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
        <DialogHeader><DialogTitle>{t('action.new')}</DialogTitle><DialogDescription>{related ? t('action.relatedTo', { what: related.label }) : t('action.newHint')}</DialogDescription></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          {!meetingId && (
            <Field label={t('action.meeting')} htmlFor="a-meeting" error={fe.meetingId} className="sm:col-span-2">
              <select id="a-meeting" className={selectCls} value={meeting} onChange={(e) => setMeeting(e.target.value)}>
                <option value={CURRENT}>{t('action.todaysCoordination')}</option>
                {(meetings.data ?? []).map((m) => <option key={m.id} value={m.id}>{m.title} · {fmtDate(m.meetingDate)}</option>)}
              </select>
            </Field>
          )}
          <Field label={t('action.text')} htmlFor="a-text" error={fe.text} className="sm:col-span-2"><Textarea id="a-text" required autoFocus rows={2} value={text} onChange={(e) => setText(e.target.value)} /></Field>
          <OwnerFields projectId={projectId} value={owner} onChange={setOwner} prefix="a" errors={[...(fe.ownerType ?? []), ...(fe.ownerUserId ?? []), ...(fe.ownerDisciplineId ?? []), ...(fe.ownerExternalPartyId ?? [])]} />
          <Field label={t('common.due')} htmlFor="a-due" error={fe.dueDate}><Input id="a-due" type="date" value={due} onChange={(e) => setDue(e.target.value)} /></Field>
          {err && !Object.keys(fe).length && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2">
            <Button type="button" variant="outline" onClick={() => onClose()}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy || !text.trim()}>{busy && <Spinner />}{t('action.add')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ---------- Meetings (FR-001) ----------

function MeetingForm({ projectId, meeting, onClose }: { projectId: string; meeting?: MeetingRow; onClose: () => void }) {
  const done = useActionRefresh()
  const [f, setF] = useState<Record<string, any>>(meeting ? { ...meeting } : { meetingDate: today(), meetingType: 'Client' })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const from = f.meetingDate ? addDays(f.meetingDate, -3) : today()
  const events = useQuery({ queryKey: ['calendar', projectId, 'meeting-link', from], enabled: !!f.meetingDate,
    queryFn: () => get<{ entries: { kind: string; id: string; title: string; start: string }[] }>(`calendar${qs({ from, to: addDays(from, 6), projectIds: projectId, types: 'Meeting' })}`) })
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      const body = { title: f.title, meetingDate: f.meetingDate, meetingType: f.meetingType, notesLink: f.notesLink || null, calendarEventId: f.calendarEventId || null }
      if (meeting) await patch(`meetings/${meeting.id}`, body, meeting.rowVersion); else await post(`projects/${projectId}/meetings`, body)
      toast.success(t('common.saved')); done(projectId); onClose()
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const fe = err?.fieldErrors ?? {}
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader><DialogTitle>{meeting ? t('meeting.edit') : t('meeting.new')}</DialogTitle><DialogDescription>{t('meeting.hint')}</DialogDescription></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('register.title')} htmlFor="m-title" error={fe.title} className="sm:col-span-2"><Input id="m-title" required autoFocus placeholder={t('meeting.titlePlaceholder')} value={f.title ?? ''} onChange={(e) => setF({ ...f, title: e.target.value })} /></Field>
          <Field label={t('meeting.date')} htmlFor="m-date" error={fe.meetingDate}><Input id="m-date" type="date" required value={f.meetingDate ?? ''} onChange={(e) => setF({ ...f, meetingDate: e.target.value })} /></Field>
          <Field label={t('common.type')} htmlFor="m-type" error={fe.meetingType}>
            <select id="m-type" className={selectCls} value={f.meetingType} onChange={(e) => setF({ ...f, meetingType: e.target.value })}>{TYPES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select>
          </Field>
          <Field label={t('meeting.notesLink')} htmlFor="m-link" error={fe.notesLink} hint={t('meeting.notesHint')} className="sm:col-span-2"><Input id="m-link" type="url" placeholder="https://…" value={f.notesLink ?? ''} onChange={(e) => setF({ ...f, notesLink: e.target.value })} /></Field>
          <Field label={t('meeting.calendarEvent')} htmlFor="m-event" error={fe.calendarEventId} className="sm:col-span-2">
            <select id="m-event" className={selectCls} value={f.calendarEventId ?? ''} onChange={(e) => setF({ ...f, calendarEventId: e.target.value || null })}>
              <option value="">{t('meeting.noEvent')}</option>
              {(events.data?.entries ?? []).filter((x) => x.kind === 'event').map((x) => <option key={x.id} value={x.id}>{x.title} · {fmtDate(x.start.slice(0, 10))}</option>)}
            </select>
          </Field>
          {err && !Object.keys(fe).length && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2">
            <Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy}>{busy && <Spinner />}{t('common.save')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** The action register (FR-007): meetings newest first, each with its actions by due date. */
export function MeetingsTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const [meetingForm, setMeetingForm] = useState<MeetingRow | 'new' | null>(null)
  const [adding, setAdding] = useState<string | null>(null)
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)]))
  const active = FILTERS.some((k) => sp.has(k))
  const meetings = useQuery({ queryKey: ['p', p.id, 'meetings'], queryFn: () => get<MeetingRow[]>(`projects/${p.id}/meetings`) })
  const actions = useQuery({ queryKey: ['p', p.id, 'actions', filters], queryFn: () => get<ActionRow[]>(`projects/${p.id}/actions${qs(filters)}`) })
  const canMeet = p.permissions.runCoordination.ok
  const canRaise = p.permissions.raiseRegister.ok
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  const byMeeting = new Map<string, ActionRow[]>()
  for (const a of actions.data ?? []) byMeeting.set(a.meetingId, [...(byMeeting.get(a.meetingId) ?? []), a])
  const shown = (meetings.data ?? []).filter((m) => !active || byMeeting.has(m.id))
  return (
    <Page title={t('ptab.meetings')} subtitle={t('meeting.subtitle')}
      actions={<>
        <ExportMenu path={`projects/${p.id}/actions/export`} params={filters} name={`${p.projectNumber}-actions`} />
        {canMeet && <Button onClick={() => setMeetingForm('new')}><Plus className="size-4" />{t('meeting.new')}</Button>}
      </>}>
      <div className="flex flex-wrap items-center gap-2">
        <Input key={filters.q ? 'q' : 'empty'} className="h-8 w-52" type="search" placeholder={t('common.search')} defaultValue={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} aria-label={t('common.search')} />
        <select className={sel} value={filters.status ?? ''} onChange={(e) => set('status', e.target.value)} aria-label={t('common.status')}>
          <option value="">{t('register.anyStatus')}</option><option value="Open,In Progress">{t('register.openStatuses')}</option>
          {STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
        </select>
        <select className={sel} value={filters.ownerType ?? ''} onChange={(e) => set('ownerType', e.target.value)} aria-label={t('action.ownerType')}>
          <option value="">{t('action.anyOwnerType')}</option>{OWNER_TYPES.map((x) => <option key={x} value={x}>{t(`action.owner.${x}`)}</option>)}
        </select>
        <button type="button" aria-pressed={filters.indicator === 'overdue'} onClick={() => set('indicator', filters.indicator === 'overdue' ? null : 'overdue')}
          className={cn('rounded-full border px-2.5 py-0.5 text-xs', filters.indicator === 'overdue' ? 'border-primary bg-primary text-primary-foreground' : 'bg-card hover:bg-muted')}>{t('ind.overdue')}</button>
        {active && <button type="button" className="px-2 text-xs text-muted-foreground hover:text-foreground" onClick={() => setSp(new URLSearchParams(), { replace: true })}>{t('common.clear')}</button>}
      </div>
      {(meetings.error || actions.error) && <ErrorBanner error={meetings.error ?? actions.error} retry={() => { meetings.refetch(); actions.refetch() }} />}
      {meetings.isPending || actions.isPending ? <Loading rows={6} /> : shown.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={canMeet && !active && <Button onClick={() => setMeetingForm('new')}>{t('meeting.new')}</Button>}>{active ? t('register.noMatch') : t('meeting.empty')}</Empty></div>
      ) : shown.map((m) => {
        const rows = byMeeting.get(m.id) ?? []
        return (
          <section key={m.id} className="rounded-lg border bg-card" aria-labelledby={`mt-${m.id}`}>
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b px-4 py-2.5">
              <h2 id={`mt-${m.id}`} className="font-semibold">{m.title}</h2>
              <span className="text-sm text-muted-foreground">{fmtDate(m.meetingDate)} · {tv(m.meetingType)}</span>
              {m.notesLink && <a className="inline-flex items-center gap-1 text-sm text-primary underline" href={m.notesLink} target="_blank" rel="noreferrer noopener"><ExternalLink className="size-3.5" aria-hidden />{t('meeting.minutes')}</a>}
              {m.calendarEventTitle && <span className="inline-flex items-center gap-1 text-sm text-muted-foreground"><CalendarDays className="size-3.5" aria-hidden />{m.calendarEventTitle}</span>}
              <span className="ml-auto flex gap-1.5">
                {canMeet && <Button size="sm" variant="ghost" onClick={() => setMeetingForm(m)}>{t('common.edit')}</Button>}
                {canRaise && <Button size="sm" variant="outline" onClick={() => setAdding(m.id)}><ListPlus className="size-4" />{t('action.add')}</Button>}
              </span>
            </div>
            {rows.length === 0 ? <p className="px-4 py-3 text-sm text-muted-foreground">{t('meeting.noActions')}</p> : (
              <ul className="divide-y text-[13px]">
                {rows.map((a) => (
                  <li key={a.id} className={cn('flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2', a.isOverdue && 'bg-bad-bg/30')}>
                    <Key>{a.key}</Key>
                    <button className="min-w-[12rem] flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Action', a.id)}>{a.text}</button>
                    <ActionOwner a={a} /><Due a={a} /><StatusPill status={a.status} />
                    {a.taskKey && <button className="hover:underline" title={t('action.becameTask')} onClick={() => openPanel('Task', a.relatedTaskId!)}><Key>{a.taskKey}</Key></button>}
                  </li>
                ))}
              </ul>
            )}
          </section>
        )
      })}
      {meetingForm && <MeetingForm projectId={p.id} meeting={meetingForm === 'new' ? undefined : meetingForm} onClose={() => setMeetingForm(null)} />}
      {adding && <ActionForm projectId={p.id} meetingId={adding} onClose={() => setAdding(null)} />}
    </Page>
  )
}

// ---------- Panel, convert (MTG-03) ----------

function ConvertDialog({ a, disciplineId, onClose }: { a: ActionRow; disciplineId?: string | null; onClose: () => void }) {
  const done = useActionRefresh()
  const openPanel = useItemPanel()
  const project = useProject(a.projectId)
  const [f, setF] = useState<Record<string, any>>({ assigneeId: a.ownerUserId ?? a.leadId ?? null, assigneeName: a.ownerType === 'User' ? a.ownerName : a.leadName, dueDate: a.dueDate ?? '', projectDisciplineId: disciplineId ?? '' })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const save = async () => {
    setErr(null); setBusy(true)
    try {
      const r = await post(`actions/${a.id}/convert`, { assigneeId: f.assigneeId || null, dueDate: f.dueDate || null, projectDisciplineId: f.projectDisciplineId || null }, a.rowVersion)
      toast.success(t('action.converted', { key: a.key, task: r.taskKey })); done(a.projectId, a.id); onClose(); openPanel('Task', r.taskId)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t('action.convertTitle', { key: a.key })}</DialogTitle><DialogDescription>{t('action.convertHint')}</DialogDescription></DialogHeader>
        <div className="grid gap-3">
          <p className="rounded bg-muted/60 p-2 text-sm">{a.text}</p>
          <Field label={t('common.discipline')} htmlFor="c-disc" error={err?.fieldErrors.projectDisciplineId}>
            <select id="c-disc" className={selectCls} value={f.projectDisciplineId} onChange={(e) => setF({ ...f, projectDisciplineId: e.target.value })} required>
              <option value="">{t('action.chooseDiscipline')}</option>
              {(project.data?.disciplines ?? []).filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label={t('field.AssigneeId')} htmlFor="c-who" error={err?.fieldErrors.assigneeId}>
            <PeoplePicker id="c-who" value={f.assigneeId} valueName={f.assigneeName} onChange={(id, person) => setF({ ...f, assigneeId: id, assigneeName: person?.displayName })} />
          </Field>
          <Field label={t('common.due')} htmlFor="c-due" error={err?.fieldErrors.dueDate}><Input id="c-due" type="date" value={f.dueDate} onChange={(e) => setF({ ...f, dueDate: e.target.value })} /></Field>
          {err && !Object.keys(err.fieldErrors).length && <ErrorBanner error={err} />}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={busy || !f.projectDisciplineId} onClick={save}>{busy && <Spinner />}{t('action.convert')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

const STEP: Record<string, string> = { Open: 'register.toOpen', 'In Progress': 'action.start', Complete: 'action.complete', Cancelled: 'action.cancel' }

function ActionPanel({ id }: PanelProps) {
  const q = useQuery({ queryKey: ['action', id], queryFn: () => get<ActionDetail>(`actions/${id}`) })
  const done = useActionRefresh()
  const openPanel = useItemPanel()
  const [tab, setTab] = useState<'comments' | 'history'>(ItemSlots.Comments ? 'comments' : 'history')
  const [step, setStep] = useState<string | null>(null)
  const [owner, setOwner] = useState<Owner | null>(null)
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const { action: a, permissions: perm, meeting: m } = q.data
  const can = perm.edit.ok
  const refresh = () => done(a.projectId, a.id)
  const save = (body: object) => patch(`actions/${a.id}`, body, a.rowVersion).then(() => { refresh(); return true }).catch((e) => { toast.error(errorText(e)); return false })
  const go = (to: string, reason?: string) => post(`actions/${a.id}/transition`, { toStatus: to, reason }, a.rowVersion).then(refresh)
  const onStep = async (to: string) => {
    if (to === 'Cancelled') { setStep(to); return }
    try { await go(to) } catch (e) { toast.error(errorText(e)) }
  }
  return (
    <div>
      <div className="space-y-2 border-b p-4">
        <div className="flex flex-wrap items-center gap-2"><ListPlus className="size-4 text-muted-foreground" aria-hidden /><Key>{a.key}</Key><StatusPill status={a.status} />
          {a.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: a.daysOverdue })}</Chip>}{a.noLead && <Chip tone="warn">{t('action.noLead')}</Chip>}</div>
        <h2 className="text-lg font-semibold">{a.text}</h2>
        <p className="text-sm text-muted-foreground">{t('action.fromMeeting', { title: m.title, date: fmtDate(m.meetingDate) })}
          {m.notesLink && <> · <a className="text-primary underline" href={m.notesLink} target="_blank" rel="noreferrer noopener">{t('meeting.minutes')}</a></>}</p>
        {q.data.task && a.converted && <p className="text-sm">{t('action.followsTask')} <button className="hover:underline" onClick={() => openPanel('Task', q.data.task!.id)}><Key>{q.data.task.key}</Key> {q.data.task.name}</button> <StatusPill status={q.data.task.status} /></p>}
        <div className="flex flex-wrap gap-1.5 pt-1">
          {perm.transitions.map((s) => <Button key={s.to} size="sm" variant={s.to === 'Complete' ? 'default' : 'outline'} disabled={!s.ok} title={s.reason ?? undefined}
            onClick={() => onStep(s.to)}>{t(s.to === 'In Progress' && a.status === 'Complete' ? 'action.reopen' : STEP[s.to])}</Button>)}
          {perm.convert && <Button size="sm" variant="outline" onClick={() => setStep('convert')}>{t('action.convert')}</Button>}
        </div>
      </div>
      <div className="px-4 py-2">
        <FieldRow label={t('action.text')}><InlineText value={a.text} multiline disabled={!can} title={perm.edit.reason ?? undefined} onSave={(v) => save({ text: v })} /></FieldRow>
        <FieldRow label={t('common.owner')}>
          {owner ? (
            <div className="grid gap-2 px-2 py-1">
              <OwnerFields projectId={a.projectId} value={owner} onChange={setOwner} prefix="p" />
              <div className="flex gap-2"><Button size="sm" onClick={() => save(ownerBody(owner)).then((ok) => ok && setOwner(null))}>{t('common.save')}</Button><Button size="sm" variant="ghost" onClick={() => setOwner(null)}>{t('common.cancel')}</Button></div>
            </div>
          ) : (
            <div className="flex flex-wrap items-center gap-2 px-2 py-1.5"><ActionOwner a={a} />
              {can && <button type="button" className="text-xs text-muted-foreground underline hover:text-foreground"
                onClick={() => setOwner({ type: a.ownerType, userId: a.ownerUserId, userName: a.ownerType === 'User' ? a.ownerName : null, disciplineId: a.ownerDisciplineId, partyId: a.ownerExternalPartyId })}>{t('action.changeOwner')}</button>}</div>
          )}
        </FieldRow>
        <FieldRow label={t('common.due')}><InlineDate value={a.dueDate} disabled={!can} onSave={(v) => save({ dueDate: v })} /></FieldRow>
        {q.data.task && !a.converted && <FieldRow label={t('action.relatedTask')}><button className="px-2 py-1.5 text-left hover:underline" onClick={() => openPanel('Task', q.data.task!.id)}><Key>{q.data.task.key}</Key> {q.data.task.name}</button></FieldRow>}
        {q.data.decision && <FieldRow label={t('action.relatedDecision')}><button className="px-2 py-1.5 text-left hover:underline" onClick={() => openPanel('Decision', q.data.decision!.id)}><Key>{q.data.decision.key}</Key> {q.data.decision.subject}</button></FieldRow>}
      </div>
      <TabBar tabs={[...(ItemSlots.Comments ? [{ id: 'comments' as const, label: t('common.comments') }] : []), { id: 'history' as const, label: t('common.history') }]} value={tab} onChange={setTab} />
      {tab === 'comments' && <CommentsSlot type="Action" id={a.id} projectId={a.projectId} />}
      {tab === 'history' && <HistoryList type="Action" id={a.id} />}
      {step === 'convert' && <ConvertDialog a={a} disciplineId={q.data.taskDisciplineId} onClose={() => setStep(null)} />}
      {step === 'Cancelled' && <ConfirmDialog open onOpenChange={(o) => !o && setStep(null)} destructive reason title={t('action.cancelTitle', { key: a.key })} body={t('action.cancelHint')}
        confirmLabel={t('action.cancel')} onConfirm={(reason) => go('Cancelled', reason)} />}
    </div>
  )
}

PANELS.Action = { component: ActionPanel }
