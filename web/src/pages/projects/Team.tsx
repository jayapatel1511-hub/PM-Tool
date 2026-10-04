import { useQuery } from '@tanstack/react-query'
import { Crown, Lock, Plus } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, ErrorBanner, Field, Loading, Notice, Page, Section, selectCls, tdCls, thCls } from '@/components/hub/common'
import { Avatar, PeoplePicker, PersonName } from '@/components/hub/people'
import { Chip } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { useProjectRefresh, useReference } from '@/hooks/data'
import { del, get, patch, post, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { AddFromTemplateDialog, usePublishedTemplates } from './FromTemplate'

interface TeamData {
  disciplines: { id: string; disciplineId: string; name: string; code: string; colour: string; leadUserId?: string; isActive: boolean }[]
  members: { id: string; userId: string; displayName: string; email: string; jobTitle?: string; isActive: boolean; roles: string[]; primaryDisciplineId?: string
    addedAt: string; leadOf: string[]; isPrimaryPm: boolean; canStaff: boolean }[]
  canManage: boolean; manageReason?: string
}

const ROLES = ['PM', 'TeamMember', 'Reviewer', 'Viewer']

/** Team & Disciplines (§12.2, §13.16): discipline leads, members with roles and primary discipline. */
export function TeamTab() {
  const p = useCurrentProject()
  const ref = useReference()
  const refresh = useProjectRefresh()
  const q = useQuery({ queryKey: ['p', p.id, 'team'], queryFn: () => get<TeamData>(`projects/${p.id}/team`) })
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
                  <span className="size-2.5 shrink-0 rounded-sm" style={{ background: x.colour }} aria-hidden /><span className="min-w-0 break-words">{x.name}</span>
                  {!x.isActive && <Chip tone="idle">{t('common.inactive')}</Chip>}
                </span>
                <div className="min-w-48 flex-1">
                  {d.canManage && x.isActive
                    ? <PeoplePicker value={x.leadUserId} valueName={name(x.leadUserId)} placeholder={t('team.noLead')} onChange={(id) => run(() => patch(`projects/${p.id}/disciplines/${x.id}`, { leadUserId: id }))} />
                    : <span className="text-sm">{t('team.lead')}: <PersonName name={name(x.leadUserId)} /></span>}
                </div>
                {!x.leadUserId && x.isActive && <Chip tone="warn">{t('team.leadMissing')}</Chip>}
                {d.canManage && (x.isActive
                  ? <Button size="sm" variant="ghost" className="text-bad hover:text-bad" aria-label={t('team.removeNamed', { name: x.name })} onClick={() => run(() => del(`projects/${p.id}/disciplines/${x.id}`))}>{t('common.remove')}</Button>
                  : <Button size="sm" variant="outline" onClick={() => run(() => patch(`projects/${p.id}/disciplines/${x.id}`, { isActive: true }))}>{t('admin.activate')}</Button>)}
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
          <div className="scroll-region overflow-x-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">{t('team.members')}</caption>
              <thead className="bg-muted">
                <tr>
                  <th scope="col" className={thCls}>{t('common.name')}</th>
                  {ROLES.map((r) => <th key={r} scope="col" className={cn(thCls, 'text-center')}>{t(`role.${r}`)}</th>)}
                  <th scope="col" className={thCls}>{t('field.PrimaryDisciplineId')}</th>
                  <th scope="col" className={thCls}>{t('team.added')}</th>
                  <th scope="col" className="w-24"><span className="sr-only">{t('common.actions')}</span></th>
                </tr>
              </thead>
              <tbody>
                {d.members.map((m) => (
                  <tr key={m.id} className="border-t hover:bg-muted">
                    <td className={cn(tdCls, 'min-w-52')}>
                      <div className="flex items-start gap-2.5">
                        <Avatar id={m.userId} name={m.displayName} />
                        <div className="min-w-0">
                          <div className="flex items-center gap-1 font-semibold">
                            {m.isPrimaryPm && <Crown role="img" className="size-4 shrink-0 text-warn" aria-label={t('team.primaryPm')} />}<PersonName name={m.displayName} active={m.isActive} />
                          </div>
                          <div className="text-xs/[18px] text-muted-foreground">{m.jobTitle}{m.leadOf.length > 0 && ` · ${t('role.DisciplineLead')}: ${m.leadOf.map((id) => d.disciplines.find((x) => x.id === id)?.name).join(', ')}`}</div>
                        </div>
                      </div>
                    </td>
                    {ROLES.map((r) => (
                      <td key={r} className={cn(tdCls, 'text-center')}>
                        <Checkbox aria-label={`${t(`role.${r}`)} — ${m.displayName}`} checked={m.roles.includes(r)} disabled={!d.canManage || (m.isPrimaryPm && r === 'PM')}
                          onCheckedChange={(c) => {
                            const roles = c ? [...m.roles, r] : m.roles.filter((x) => x !== r)
                            if (roles.length) run(() => patch(`projects/${p.id}/members/${m.id}`, { roles }))
                          }} />
                      </td>
                    ))}
                    <td className={tdCls}>
                      <select className={cn(selectCls, 'h-(--control-row-h) w-auto min-w-44')} value={m.primaryDisciplineId ?? ''} disabled={!d.canManage} aria-label={t('field.PrimaryDisciplineId')}
                        onChange={(e) => run(() => patch(`projects/${p.id}/members/${m.id}`, { primaryDisciplineId: e.target.value || null }))}>
                        <option value="">{t('team.noPrimary')}</option>
                        {d.disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                      </select>
                    </td>
                    <td className={cn(tdCls, 'whitespace-nowrap text-xs/[18px] text-muted-foreground tabular-nums')}>{fmtDate(m.addedAt)}</td>
                    <td className={cn(tdCls, 'text-right')}>
                      {(d.canManage || m.canStaff) && !m.isPrimaryPm && <Button size="sm" variant="ghost" className="text-bad hover:text-bad" aria-label={t('team.removeNamed', { name: m.displayName })} onClick={() => setRemoving(m)}>{t('common.remove')}</Button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Section>
      </div>
      {removing && <RemoveMember projectId={p.id} member={removing} onClose={() => { setRemoving(null); reload() }} />}
      {fromTemplate && <AddFromTemplateDialog projectId={p.id} onClose={(added) => { setFromTemplate(false); if (added) reload() }} />}
    </Page>
  )
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
              {['TeamMember', 'Reviewer', 'Viewer', 'PM'].map((r) => <option key={r} value={r}>{t(`role.${r}`)}</option>)}
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
      onConfirm={(reason) => del(`projects/${projectId}/members/${member.id}${qs({ reassignTo: n > 0 && choice === 'reassign' ? to : undefined, reason })}`)}>
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

export { selectCls }
