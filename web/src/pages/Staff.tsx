import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronRight, UserPlus } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, selectCls } from '@/components/hub/common'
import { PeoplePicker } from '@/components/hub/people'
import { HealthPill, Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { useProject, useReference } from '@/hooks/data'
import { del, get, post, qs } from '@/lib/api'
import { ago, fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { Page as PageOf, ProjectRow } from '@/lib/types'
import { cn } from '@/lib/utils'

interface StaffRow {
  id: string; displayName: string; jobTitle?: string; officeId?: string; isActive: boolean; projects: number
  roles: { pm: number; dl: number; team: number; reviewer: number; viewer: number }
  open: number; overdue: number; blocked: number; blocking: number; reviews: number; reviewsStalled: number; deliverablesDue: number; lastActivityAt?: string
}
interface StaffData { scope: string; canSeeAll: boolean; tiles: { staff: number; assignments: number; withOverdue: number; withBlocked: number; reviewsStalled: number }; people: StaffRow[] }
interface Assignment {
  memberId: string; projectId: string; projectNumber: string; name: string; status: string; reportedHealth: string; roles: string[]; leads: string[]
  primaryDiscipline?: string; addedAt: string; open: number; overdue: number; nextDue?: { id: string; key: string; name: string; dueDate: string } | null; canRemove: boolean
}

const COLS: [keyof StaffRow | 'name', string][] = [['name', 'staff.name'], ['projects', 'staff.projects'], ['open', 'dash.open'], ['overdue', 'ind.overdue'], ['blocked', 'ind.blocked'],
  ['blocking', 'staff.blocking'], ['reviews', 'staff.reviews'], ['deliverablesDue', 'dash.due14'], ['lastActivityAt', 'task.lastActivity']]

/** My Staff (§13.19): a manager's direct reports, their assignments and work counts, and staffing them on projects. */
export function StaffPage() {
  const [sp, setSp] = useSearchParams()
  const ref = useReference()
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const f = { scope: sp.get('scope') ?? undefined, showInactive: sp.get('inactive') === '1' || undefined, officeId: sp.get('office') ?? undefined,
    disciplineId: sp.get('discipline') ?? undefined, indicator: sp.get('indicator') ?? undefined }
  const q = useQuery({ queryKey: ['staff', f], queryFn: () => get<StaffData>(`staff${qs(f)}`) })
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [sort, setSort] = useState<{ key: string; desc: boolean } | null>(null)
  if (q.isPending) return <Loading rows={8} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} /></div>
  const d = q.data
  const rows = sort ? [...d.people].sort((a, b) => {
    const va = sort.key === 'name' ? a.displayName : (a as any)[sort.key] ?? '', vb = sort.key === 'name' ? b.displayName : (b as any)[sort.key] ?? ''
    const r = typeof va === 'number' ? va - (vb as number) : String(va).localeCompare(String(vb))
    return sort.desc ? -r : r
  }) : d.people
  const office = (id?: string) => ref.data?.offices.find((o) => o.id === id)?.name
  const tile = (label: string, n: number, indicator?: string) => (
    <button type="button" onClick={() => set('indicator', indicator)} className={cn('rounded-lg border bg-card px-4 py-3 text-left hover:bg-muted', f.indicator === indicator && indicator && 'ring-2 ring-primary')}>
      <div className="text-xs text-muted-foreground">{label}</div><div className="text-xl font-semibold tabular-nums">{n}</div>
    </button>
  )
  const roleChips = (r: StaffRow['roles']) => (['pm', 'dl', 'team', 'reviewer', 'viewer'] as const).filter((k) => r[k] > 0).map((k) => `${t(`staff.role.${k}`)} ${r[k]}`).join(' · ')
  const count = (p: StaffRow, n: number, section: string, bad = false) => <Link to={`/my-work?userId=${p.id}#mw-${section}`} className={cn('tabular-nums hover:underline', bad && n > 0 && 'font-medium text-bad')}>{n}</Link>
  return (
    <Page title={t('nav.staff')} subtitle={d.scope === 'all' ? t('staff.allSubtitle') : t('staff.directSubtitle')}>
      <div className="flex flex-wrap items-center gap-2 text-sm">
        {d.canSeeAll && (
          <div className="inline-flex overflow-hidden rounded-md border bg-card" role="group" aria-label={t('staff.scope')}>
            {['direct', 'all'].map((s) => <button key={s} className={cn('px-3 py-1', (f.scope ?? 'direct') === s ? 'bg-accent font-medium' : 'hover:bg-muted')} aria-pressed={(f.scope ?? 'direct') === s}
              onClick={() => set('scope', s === 'direct' ? undefined : s)}>{t(`staff.scope.${s}`)}</button>)}
          </div>
        )}
        <select className="h-8 rounded-md border bg-card px-2" value={f.officeId ?? ''} onChange={(e) => set('office', e.target.value || undefined)} aria-label={t('field.OfficeId')}>
          <option value="">{t('staff.anyOffice')}</option>{ref.data?.offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2" value={f.disciplineId ?? ''} onChange={(e) => set('discipline', e.target.value || undefined)} aria-label={t('common.discipline')}>
          <option value="">{t('projects.anyDiscipline')}</option>{ref.data?.disciplines.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
        <label className="flex items-center gap-1.5"><input type="checkbox" checked={!!f.showInactive} onChange={(e) => set('inactive', e.target.checked ? '1' : undefined)} />{t('staff.showInactive')}</label>
      </div>
      <div className="grid gap-3 sm:grid-cols-3 xl:grid-cols-5">
        {tile(t('staff.tile.staff'), d.tiles.staff)}
        {tile(t('staff.tile.assignments'), d.tiles.assignments)}
        {tile(t('staff.tile.overdue'), d.tiles.withOverdue, 'overdue')}
        {tile(t('staff.tile.blocked'), d.tiles.withBlocked, 'blocked')}
        {tile(t('staff.tile.reviews'), d.tiles.reviewsStalled, 'reviews')}
      </div>
      {rows.length > 0 && (
        <ul className="space-y-2 md:hidden" aria-label={t('nav.staff')}>
          {rows.map((p) => (
            <li key={p.id} className="rounded-lg border bg-card p-3 text-sm">
              <div className="font-medium">{p.isActive ? p.displayName : t('common.inactiveSuffix', { name: p.displayName })}</div>
              <div className="text-xs text-muted-foreground">{[p.jobTitle, office(p.officeId), roleChips(p.roles)].filter(Boolean).join(' · ')}</div>
              <dl className="mt-2 grid grid-cols-4 gap-2 text-center text-xs">
                {([['dash.open', p.open, false], ['ind.overdue', p.overdue, true], ['ind.blocked', p.blocked, true], ['staff.reviews', p.reviews, false]] as const).map(([label, n, bad]) => (
                  <div key={label}><dt className="text-muted-foreground">{t(label)}</dt><dd className={cn('text-base font-semibold tabular-nums', bad && n > 0 && 'text-bad')}>{n}</dd></div>
                ))}
              </dl>
            </li>
          ))}
        </ul>
      )}
      {rows.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('staff.none')}</Empty></div> : (
        <div className="hidden overflow-x-auto rounded-lg border bg-card md:block">
          <table className="w-full text-sm">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr><th className="w-8"><span className="sr-only">{t('common.details')}</span></th>{COLS.map(([k, label]) => (
                <th key={k} className="whitespace-nowrap px-3 py-2 font-medium" aria-sort={sort?.key === k ? (sort.desc ? 'descending' : 'ascending') : 'none'}>
                  <button className="hover:text-foreground" onClick={() => setSort(sort?.key === k ? { key: k, desc: !sort.desc } : { key: k, desc: k !== 'name' })}>{t(label)}</button>
                </th>))}<th className="px-3 py-2 font-medium">{t('staff.roles')}</th></tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <Fragment key={p.id}>
                  <tr className={cn('border-t', !p.isActive && 'text-muted-foreground')}>
                    <td className="px-2"><button className="rounded p-1 hover:bg-muted" aria-expanded={open.has(p.id)} aria-label={t('staff.showAssignments', { name: p.displayName })}
                      onClick={() => setOpen((s) => { const n = new Set(s); if (n.has(p.id)) n.delete(p.id); else n.add(p.id); return n })}>
                      {open.has(p.id) ? <ChevronDown className="size-3.5" /> : <ChevronRight className="size-3.5" />}</button></td>
                    <td className="px-3 py-2">
                      <Link to={`/my-work?userId=${p.id}`} className="font-medium hover:underline">{p.isActive ? p.displayName : t('common.inactiveSuffix', { name: p.displayName })}</Link>
                      <div className="text-xs text-muted-foreground">{[p.jobTitle, office(p.officeId)].filter(Boolean).join(' · ')}</div>
                    </td>
                    <td className="px-3 py-2 tabular-nums">{p.projects}</td>
                    <td className="px-3 py-2">{count(p, p.open, 'tasks')}</td>
                    <td className="px-3 py-2">{count(p, p.overdue, 'tasks', true)}</td>
                    <td className="px-3 py-2">{count(p, p.blocked, 'waiting', true)}</td>
                    <td className="px-3 py-2">{count(p, p.blocking, 'blocking', true)}</td>
                    <td className="px-3 py-2">{count(p, p.reviews, 'reviews')}{p.reviewsStalled > 0 && <span className="ml-1 text-xs text-warn">({t('staff.stalled', { n: p.reviewsStalled })})</span>}</td>
                    <td className="px-3 py-2">{count(p, p.deliverablesDue, 'deliverables')}</td>
                    <td className="whitespace-nowrap px-3 py-2 text-xs text-muted-foreground">{p.lastActivityAt ? ago(p.lastActivityAt) : t('common.dash')}</td>
                    <td className="whitespace-nowrap px-3 py-2 text-xs">{roleChips(p.roles) || t('common.dash')}</td>
                  </tr>
                  {open.has(p.id) && <tr className="border-t bg-muted/20"><td /><td colSpan={COLS.length + 1} className="px-3 py-2"><Assignments person={p} scope={f.scope} onChanged={() => q.refetch()} /></td></tr>}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Page>
  )
}

function Assignments({ person, scope, onChanged }: { person: StaffRow; scope?: string; onChanged: () => void }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['staff', person.id, 'assignments', scope], queryFn: () => get<Assignment[]>(`staff/${person.id}/assignments${qs({ scope })}`) })
  const [assigning, setAssigning] = useState(false)
  const [removing, setRemoving] = useState<Assignment | null>(null)
  const reload = () => { qc.invalidateQueries({ queryKey: ['staff'] }); onChanged() }
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <ErrorBanner error={q.error} />
  return (
    <div className="space-y-2">
      {q.data.length === 0 ? <p className="text-sm text-muted-foreground">{t('staff.noAssignments')}</p> : (
        <ul className="divide-y rounded border bg-card">
          {q.data.map((a) => (
            <li key={a.memberId} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm">
              <Link to={`/projects/${a.projectNumber}`} className="min-w-0 flex-1 truncate hover:underline"><Key>{a.projectNumber}</Key> {a.name}</Link>
              <HealthPill health={a.reportedHealth} />
              <span className="text-xs">{a.roles.map((r) => t(`role.${r}`)).join(', ')}{a.leads.length > 0 && ` (${a.leads.join(', ')})`}</span>
              <span className="text-xs text-muted-foreground">{a.primaryDiscipline ?? t('common.dash')} · {t('staff.added', { date: fmtDate(a.addedAt) })}</span>
              <span className="text-xs tabular-nums">{t('staff.openOverdue', { open: a.open, overdue: a.overdue })}</span>
              <span className="text-xs text-muted-foreground">{a.nextDue ? `${a.nextDue.key} ${fmtDate(a.nextDue.dueDate)}` : ''}</span>
              <span className="text-xs text-muted-foreground">{tv(a.status)}</span>
              {a.canRemove && <Button size="sm" variant="ghost" className="h-7 text-bad" onClick={() => setRemoving(a)}>{t('staff.remove')}</Button>}
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={() => setAssigning(true)}><UserPlus className="size-3.5" />{t('staff.assign')}</Button>
        <Button size="sm" variant="ghost" asChild><Link to={`/people/${person.id}/reassign-work`}>{t('reassign.action')}</Link></Button>
      </div>
      {assigning && <AssignDialog person={person} onClose={(ok) => { setAssigning(false); if (ok) { q.refetch(); reload() } }} />}
      {removing && <RemoveDialog person={person} a={removing} onClose={(ok) => { setRemoving(null); if (ok) { q.refetch(); reload() } }} />}
    </div>
  )
}

/** ASG-10: a supervisor adds a direct report to a Setup, Active or On Hold project they can view, as Team Member with a primary discipline. */
function AssignDialog({ person, onClose }: { person: StaffRow; onClose: (ok: boolean) => void }) {
  const projects = useQuery({ queryKey: ['projects', 'staffable'], queryFn: () => get<PageOf<ProjectRow> | ProjectRow[]>(`projects${qs({ status: 'Setup,Active,On Hold', pageSize: 200 })}`) })
  const [number, setNumber] = useState('')
  const [discipline, setDiscipline] = useState('')
  const detail = useProject(number || undefined)
  const list: ProjectRow[] = Array.isArray(projects.data) ? projects.data : projects.data?.items ?? []
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('staff.assignTitle', { name: person.displayName })} body={t('staff.assignBody')}
      confirmLabel={t('staff.assign')} busy={!number || !discipline}
      onConfirm={async () => {
        await post(`projects/${detail.data!.id}/members`, { userId: person.id, roles: ['TeamMember'], primaryDisciplineId: discipline })
        toast.success(t('staff.assigned', { name: person.displayName, number })); onClose(true)
      }}>
      <Field label={t('nav.projects')} htmlFor="as-project">
        <select id="as-project" className={selectCls} value={number} onChange={(e) => { setNumber(e.target.value); setDiscipline('') }}>
          <option value="">{t('common.selectPlaceholder')}</option>{list.map((p) => <option key={p.id} value={p.projectNumber}>{p.projectNumber} {p.name} ({tv(p.status)})</option>)}
        </select>
      </Field>
      <Field label={t('field.PrimaryDisciplineId')} htmlFor="as-disc">
        <select id="as-disc" className={selectCls} value={discipline} disabled={!detail.data} onChange={(e) => setDiscipline(e.target.value)}>
          <option value="">{t('common.selectPlaceholder')}</option>{detail.data?.disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
      </Field>
    </ConfirmDialog>
  )
}

/** TM-04, E-29: open work is reassigned within the project or left flagged, with a reason; the PM is told. */
function RemoveDialog({ person, a, onClose }: { person: StaffRow; a: Assignment; onClose: (ok: boolean) => void }) {
  const items = useQuery({ queryKey: ['open-items', a.memberId], queryFn: () => get<{ count: number; tasks: { id: string; key: string; name: string }[] }>(`projects/${a.projectId}/members/${a.memberId}/open-items`) })
  const [to, setTo] = useState<string | null>(null)
  return (
    <ConfirmDialog open destructive reason onOpenChange={(o) => !o && onClose(false)} title={t('staff.removeTitle', { name: person.displayName, number: a.projectNumber })} confirmLabel={t('staff.remove')}
      body={items.data ? (items.data.count > 0 ? t('staff.openItems', { n: items.data.count }) : t('staff.noOpenItems')) : undefined}
      onConfirm={async (reason) => { await del(`projects/${a.projectId}/members/${a.memberId}${qs({ reassignTo: to, reason })}`); toast.success(t('staff.removed')); onClose(true) }}>
      {items.data && items.data.count > 0 && (
        <Field label={t('staff.reassignTo')} hint={t('staff.reassignHint')}><PeoplePicker value={to} onChange={setTo} exclude={[person.id]} /></Field>
      )}
    </ConfirmDialog>
  )
}
