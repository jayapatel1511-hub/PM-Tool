import { disciplineColour } from '@/lib/discipline-colour'
import { useQuery } from '@tanstack/react-query'
import { Crown, Lock, Plus } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Notice, Page, Section, selectCls } from '@/components/hub/common'
import { Avatar, PeoplePicker, PersonName, usePeopleCapacity, weekOf } from '@/components/hub/people'
import { Chip } from '@/components/hub/pills'
import { DataTable } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { useProjectRefresh, useReference } from '@/hooks/data'
import { del, get, patch, post, qs } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { hoursLabel } from '@/components/planner/labels'
import { fmtDate, today } from '@/lib/format'
import { runtimeText, t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { AddFromTemplateDialog, usePublishedTemplates } from './FromTemplate'

interface TeamData {
  disciplines: { id: string; disciplineId: string; name: string; code: string; colour: string; leadUserId?: string; isActive: boolean; rowVersion: number }[]
  members: { id: string; userId: string; displayName: string; email: string; jobTitle?: string; isActive: boolean; roles: string[]; primaryDisciplineId?: string
    addedAt: string; leadOf: string[]; isPrimaryPm: boolean; canStaff: boolean; rowVersion: number }[]
  canManage: boolean; manageReason?: string
}

const ROLES = ['PM', 'TeamMember', 'Reviewer', 'Viewer']

/** Team & Disciplines (§12.2, §13.16): discipline leads, members with roles and primary discipline. */
export function TeamTab() {
  const p = useCurrentProject()
  const ref = useReference()
  const refresh = useProjectRefresh()
  const q = useQuery({ queryKey: ['p', p.id, 'team'], queryFn: () => get<TeamData>(`projects/${p.id}/team`) })
  const polish = useFlag('capacity_polish')
  const capacity = usePeopleCapacity(weekOf(today()), polish && !!q.data, q.data?.members.map(m => m.userId) ?? [])
  const away = (id: string) => { const hours = capacity(id)?.timeAway; return hours ? <Chip tone="idle">{t('planner.awayHours', { hours: hoursLabel(hours) })}</Chip> : null }
  const [removing, setRemoving] = useState<TeamData['members'][number] | null>(null)
  const [addDisc, setAddDisc] = useState('')
  const [fromTemplate, setFromTemplate] = useState(false)
  const templates = usePublishedTemplates()
  const [err, setErr] = useState<unknown>(null)
  const reload = () => { q.refetch(); refresh(p.id) }
  const run = async (f: () => Promise<unknown>) => { setErr(null); try { await f(); reload() } catch (e) { setErr(e) } }
  const header = { title: t('ptab.team') }
  if (q.isPending) return <Page {...header}><Loading /></Page>
  if (q.error) return <Page {...header}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const d = q.data
  const name = (userId?: string) => d.members.find((m) => m.userId === userId)?.displayName
  const unused = ref.data?.disciplines.filter((x) => x.isActive && !d.disciplines.some((y) => y.disciplineId === x.id && y.isActive)) ?? []

  return (
    <Page {...header}>
      {err != null && <ErrorBanner error={err} />}
      {!d.canManage && d.manageReason && <Notice icon={Lock} title={d.manageReason} />}
      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <Section title={t('team.disciplines')} count={d.disciplines.filter((x) => x.isActive).length} accent="mint">
          <ul className="divide-y">
            {d.disciplines.map((x) => (
              <li key={x.id} className="flex flex-wrap items-center gap-x-3 gap-y-2 px-5 py-3">
                <span className="flex w-44 min-w-0 items-center gap-2 text-sm font-semibold">
                  <span className="size-2.5 shrink-0 rounded-sm" style={{ background: disciplineColour(x.colour) }} aria-hidden /><span className="min-w-0 break-words">{x.name}</span>
                  {!x.isActive && <Chip tone="idle">{t('common.inactive')}</Chip>}
                </span>
                <div className="min-w-48 flex-1">
                  {d.canManage && x.isActive
                    ? <PeoplePicker value={x.leadUserId} valueName={name(x.leadUserId)} placeholder={t('team.noLead')} onChange={(id) => run(() => patch(`projects/${p.id}/disciplines/${x.id}`, { leadUserId: id }, x.rowVersion))} />
                    : <span className="text-sm">{t('team.lead')}: <PersonName name={name(x.leadUserId)} /></span>}
                </div>
                {!x.leadUserId && x.isActive && <Chip tone="warn">{t('team.leadMissing')}</Chip>}
                {d.canManage && (x.isActive
                  ? <Button size="sm" variant="ghost" className="text-bad hover:text-bad" aria-label={t('team.removeNamed', { name: x.name })} onClick={() => run(() => del(`projects/${p.id}/disciplines/${x.id}`, undefined, x.rowVersion))}>{t('common.remove')}</Button>
                  : <Button size="sm" variant="outline" onClick={() => run(() => patch(`projects/${p.id}/disciplines/${x.id}`, { isActive: true }, x.rowVersion))}>{t('admin.activate')}</Button>)}
              </li>
            ))}
          </ul>
          {d.canManage && (
            <form className="flex flex-wrap items-end gap-3 border-t px-5 py-4" onSubmit={(e) => { e.preventDefault(); if (addDisc) run(() => post(`projects/${p.id}/disciplines`, { disciplineId: addDisc })).then(() => setAddDisc('')) }}>
              <Field label={t('team.addDiscipline')} htmlFor="team-add-discipline" className="min-w-48 flex-1">
                <select id="team-add-discipline" className={selectCls} value={addDisc} onChange={(e) => setAddDisc(e.target.value)}>
                  <option value="">{t('common.selectPlaceholder')}</option>
                  {unused.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                </select>
              </Field>
              <Button type="submit" variant="outline" disabled={!addDisc}><Plus className="size-4" />{t('common.add')}</Button>
              {(templates.data?.length ?? 0) > 0 && <Button type="button" variant="outline" onClick={() => setFromTemplate(true)}>{t('tpl.addPack')}</Button>}
            </form>
          )}
        </Section>

        <Section title={t('team.members')} count={d.members.length} accent="mint">
          <AddMember projectId={p.id} disciplines={d.disciplines} canManage={d.canManage} onDone={reload} />
          <MemberTable projectId={p.id} d={d} away={away} run={run} onRemove={setRemoving} />
        </Section>
      </div>
      {removing && <RemoveMember projectId={p.id} member={removing} onClose={() => { setRemoving(null); reload() }} />}
      {fromTemplate && <AddFromTemplateDialog projectId={p.id} onClose={(added) => { setFromTemplate(false); if (added) reload() }} />}
    </Page>
  )
}

type Member = TeamData['members'][number]

/** The members list on the shared DataTable (§47.7, FR-REC-14): a table from `md` up and, below it, one card per member with the same controls. */
function MemberTable({ projectId, d, away, run, onRemove }: { projectId: string; d: TeamData; away: (id: string) => ReactNode; run: (f: () => Promise<unknown>) => Promise<void>; onRemove: (m: Member) => void }) {
  const edit = (m: Member, body: object) => run(() => patch(`projects/${projectId}/members/${m.id}`, body, m.rowVersion))
  const leadOf = (m: Member) => m.leadOf.length > 0 && ` · ${t('role.DisciplineLead')}: ${m.leadOf.map((id) => d.disciplines.find((x) => x.id === id)?.name).join(', ')}`
  const roleBox = (m: Member, r: string) => (
    <Checkbox aria-label={`${runtimeText(`role.${r}`)} — ${m.displayName}`} checked={m.roles.includes(r)} disabled={!d.canManage || (m.isPrimaryPm && r === 'PM')}
      onCheckedChange={(c) => {
        const roles = c ? [...m.roles, r] : m.roles.filter((x) => x !== r)
        if (roles.length) edit(m, { roles })
      }} />
  )
  const removeButton = (m: Member) => (d.canManage || m.canStaff) && !m.isPrimaryPm
    && <Button size="sm" variant="ghost" className="text-bad hover:text-bad" aria-label={t('team.removeNamed', { name: m.displayName })} onClick={() => onRemove(m)}>{t('common.remove')}</Button>
  const columns: Column<Member>[] = [
    { id: 'name', label: t('common.name'), fixed: true, className: 'min-w-52 align-top', cell: (m) => (
      <div className="flex items-start gap-2.5">
        <Avatar id={m.userId} name={m.displayName} />
        <div className="min-w-0">
          <div className="flex items-center gap-1 font-semibold">
            {m.isPrimaryPm && <Crown role="img" className="size-4 shrink-0 text-warn" aria-label={t('team.primaryPm')} />}<PersonName name={m.displayName} active={m.isActive} />{away(m.userId)}
          </div>
          <div className="text-xs/[18px] text-muted-foreground">{m.jobTitle}{leadOf(m)}</div>
        </div>
      </div>) },
    ...ROLES.map((r): Column<Member> => ({ id: r, label: runtimeText(`role.${r}`), fixed: true, className: 'text-center align-top', cell: (m) => roleBox(m, r) })),
    { id: 'primary', label: t('field.PrimaryDisciplineId'), fixed: true, className: 'align-top', cell: (m) => (
      <select className={cn(selectCls, 'h-(--control-row-h) w-auto min-w-44')} value={m.primaryDisciplineId ?? ''} disabled={!d.canManage} aria-label={t('field.PrimaryDisciplineId')}
        onChange={(e) => edit(m, { primaryDisciplineId: e.target.value || null })}>
        <option value="">{t('team.noPrimary')}</option>
        {d.disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
      </select>) },
    { id: 'added', label: t('team.added'), fixed: true, className: 'whitespace-nowrap align-top', cell: (m) => <span className="text-xs/[18px] text-muted-foreground tabular-nums">{fmtDate(m.addedAt)}</span> },
    { id: 'actions', label: t('common.actions'), srOnly: true, fixed: true, className: 'w-24 align-top text-right', cell: removeButton },
  ]
  const table = useTable<Member>('hub.teamMemberColumns', columns, d.members, (m) => [m.displayName])
  const cards = <ul className="divide-y md:hidden">{d.members.map((m) => <li key={m.id} className="space-y-3 p-4 text-sm">
    <div className="flex items-start gap-2.5"><Avatar id={m.userId} name={m.displayName} /><div className="min-w-0"><h4 className="flex items-center gap-1 break-words font-semibold">{m.isPrimaryPm && <Crown className="size-4 shrink-0 text-warn" aria-label={t('team.primaryPm')} />}<PersonName name={m.displayName} active={m.isActive} />{away(m.userId)}</h4><p className="break-words text-xs/[18px] text-muted-foreground">{m.jobTitle}{leadOf(m)}</p></div></div>
    <fieldset className="flex flex-wrap gap-x-4 gap-y-2"><legend className="sr-only">{t('team.role')}</legend>{ROLES.map((r) => <label key={r} className="flex min-h-10 items-center gap-2">{roleBox(m, r)}{runtimeText(`role.${r}`)}</label>)}</fieldset>
    <Field label={t('field.PrimaryDisciplineId')} htmlFor={`member-disc-${m.id}`}><select id={`member-disc-${m.id}`} className={selectCls} value={m.primaryDisciplineId ?? ''} disabled={!d.canManage} onChange={(e) => edit(m, { primaryDisciplineId: e.target.value || null })}><option value="">{t('team.noPrimary')}</option>{d.disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></Field>
    <p className="text-xs/[18px] text-muted-foreground">{t('team.added')}: {fmtDate(m.addedAt)}</p>
    {removeButton(m)}
  </li>)}</ul>
  return <DataTable table={table} rows={table.sorted} loading={false} empty={<Empty title={t('common.empty')} />} current={() => false} caption={t('team.members')} label={t('team.members')} cards={cards} />
}

function AddMember({ projectId, disciplines, canManage, onDone }: { projectId: string; disciplines: TeamData['disciplines']; canManage: boolean; onDone: () => void }) {
  const [userId, setUserId] = useState<string | null>(null)
  const [role, setRole] = useState('TeamMember')
  const [disc, setDisc] = useState('')
  const [err, setErr] = useState<unknown>(null)
  return (
    <div className="space-y-3 border-b px-5 py-4">
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('team.person')} htmlFor="team-add-person" className="w-full sm:w-64">
          <PeoplePicker id="team-add-person" value={userId} onChange={(id) => setUserId(id)} placeholder={t('team.addMember')} />
        </Field>
        {canManage && (
          <Field label={t('team.role')} htmlFor="team-add-role" className="w-full sm:w-44">
            <select id="team-add-role" className={selectCls} value={role} onChange={(e) => setRole(e.target.value)}>
              {['TeamMember', 'Reviewer', 'Viewer', 'PM'].map((r) => <option key={r} value={r}>{runtimeText(`role.${r}`)}</option>)}
            </select>
          </Field>
        )}
        <Field label={t('field.PrimaryDisciplineId')} htmlFor="team-add-primary" optional className="w-full sm:w-52">
          <select id="team-add-primary" className={selectCls} value={disc} onChange={(e) => setDisc(e.target.value)}>
            <option value="">{t('team.noPrimary')}</option>
            {disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Button disabled={!userId} onClick={async () => {
          setErr(null)
          try { await post(`projects/${projectId}/members`, { userId, roles: [role], primaryDisciplineId: disc || null }); setUserId(null); toast.success(t('team.added')); onDone() }
          catch (e) { setErr(e) }
        }}><Plus className="size-4" />{t('common.add')}</Button>
      </div>
      {err != null && <ErrorBanner error={err} />}
    </div>
  )
}

/** TM-04: removing someone with open items asks whether to reassign or leave them flagged. */
function RemoveMember({ projectId, member, onClose }: { projectId: string; member: TeamData['members'][number]; onClose: () => void }) {
  const items = useQuery({ queryKey: ['open-items', member.id], queryFn: () => get(`projects/${projectId}/members/${member.id}/open-items`) })
  const [choice, setChoice] = useState<'leave' | 'reassign'>('reassign')
  const [to, setTo] = useState<string | null>(null)
  const n = items.data?.count ?? 0
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} destructive reason="optional"
      title={t('team.removeTitle', { name: member.displayName })}
      body={items.isPending ? t('app.loading') : n === 0 ? t('team.removeNoItems') : t('team.removeItems', { n })}
      confirmLabel={t('common.remove')} busy={n > 0 && choice === 'reassign' && !to}
      onConfirm={(reason) => del(`projects/${projectId}/members/${member.id}${qs({ reassignTo: n > 0 && choice === 'reassign' ? to : undefined, reason })}`, undefined, member.rowVersion)}>
      {n > 0 && (
        <div className="space-y-3 text-sm">
          <ul className="max-h-40 space-y-1 overflow-y-auto rounded-md border bg-muted p-3">
            {[...items.data.tasks, ...items.data.deliverables].map((i: any) => <li key={i.id}><span className="key">{i.key}</span> {i.name} · {tv(i.status)}</li>)}
          </ul>
          <label className="flex min-h-6 items-center gap-2"><input type="radio" name="team-remove-choice" className="size-4 accent-(--primary)" checked={choice === 'reassign'} onChange={() => setChoice('reassign')} />{t('team.reassignTo')}</label>
          {choice === 'reassign' && <Field label={t('team.newOwner')} htmlFor="team-new-owner" className="ml-6"><PeoplePicker id="team-new-owner" value={to} onChange={setTo} exclude={[member.userId]} /></Field>}
          <label className="flex min-h-6 items-center gap-2"><input type="radio" name="team-remove-choice" className="size-4 accent-(--primary)" checked={choice === 'leave'} onChange={() => setChoice('leave')} />{t('team.leaveFlagged')}</label>
        </div>
      )}
    </ConfirmDialog>
  )
}

