import { useQuery } from '@tanstack/react-query'
import { Crown, Plus } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, ErrorBanner, Field, Loading, Section, selectCls } from '@/components/hub/common'
import { PeoplePicker, PersonName } from '@/components/hub/people'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { useProjectRefresh, useReference } from '@/hooks/data'
import { del, get, patch, post, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { useCurrentProject } from './ProjectLayout'
import { AddFromTemplateDialog, usePublishedTemplates } from './FromTemplate'

interface TeamData {
  disciplines: { id: string; disciplineId: string; name: string; code: string; colour: string; leadUserId?: string; isActive: boolean }[]
  members: { id: string; userId: string; displayName: string; email: string; jobTitle?: string; isActive: boolean; roles: string[]; primaryDisciplineId?: string
    addedAt: string; leadOf: string[]; isPrimaryPm: boolean; canStaff: boolean }[]
  canManage: boolean; manageReason?: string
}

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
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const d = q.data
  const name = (userId?: string) => d.members.find((m) => m.userId === userId)?.displayName
  const unused = ref.data?.disciplines.filter((x) => x.isActive && !d.disciplines.some((y) => y.disciplineId === x.id && y.isActive)) ?? []

  return (
    <div className="grid gap-4 p-4 xl:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
      {err != null && <div className="xl:col-span-2"><ErrorBanner error={err} /></div>}
      {!d.canManage && d.manageReason && <p className="text-sm text-muted-foreground xl:col-span-2">{d.manageReason}</p>}
      <Section title={t('team.disciplines')} count={d.disciplines.filter((x) => x.isActive).length}
        actions={d.canManage && (
          <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); if (addDisc) run(() => post(`projects/${p.id}/disciplines`, { disciplineId: addDisc })).then(() => setAddDisc('')) }}>
            <select className="h-8 rounded-md border px-2 text-sm" value={addDisc} onChange={(e) => setAddDisc(e.target.value)} aria-label={t('team.addDiscipline')}>
              <option value="">{t('team.addDiscipline')}</option>
              {unused.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
            <Button size="sm" type="submit" disabled={!addDisc}><Plus className="size-4" />{t('common.add')}</Button>
            {(templates.data?.length ?? 0) > 0 && <Button size="sm" type="button" variant="outline" onClick={() => setFromTemplate(true)}>{t('tpl.addPack')}</Button>}
          </form>
        )}>
        <ul className="divide-y">
          {d.disciplines.map((x) => (
            <li key={x.id} className="flex flex-wrap items-center gap-3 px-4 py-2">
              <span className="flex w-40 items-center gap-2 text-sm font-medium"><span className="size-2.5 rounded-sm" style={{ background: x.colour }} aria-hidden />{x.name}
                {!x.isActive && <Badge variant="outline">{t('common.inactive')}</Badge>}</span>
              <div className="min-w-48 flex-1">
                {d.canManage && x.isActive
                  ? <PeoplePicker value={x.leadUserId} valueName={name(x.leadUserId)} placeholder={t('team.noLead')} onChange={(id) => run(() => patch(`projects/${p.id}/disciplines/${x.id}`, { leadUserId: id }))} />
                  : <span className="text-sm">{t('team.lead')}: <PersonName name={name(x.leadUserId)} /></span>}
              </div>
              {!x.leadUserId && x.isActive && <span className="text-xs text-warn">▲ {t('team.leadMissing')}</span>}
              {d.canManage && (x.isActive
                ? <Button size="sm" variant="ghost" onClick={() => run(() => del(`projects/${p.id}/disciplines/${x.id}`))}>{t('common.remove')}</Button>
                : <Button size="sm" variant="ghost" onClick={() => run(() => patch(`projects/${p.id}/disciplines/${x.id}`, { isActive: true }))}>{t('admin.activate')}</Button>)}
            </li>
          ))}
        </ul>
      </Section>

      <Section title={t('team.members')} count={d.members.length}
        actions={<AddMember projectId={p.id} disciplines={d.disciplines} canManage={d.canManage} onDone={reload} />}>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr><th className="px-3 py-2 font-medium">{t('common.name')}</th>{['PM', 'TeamMember', 'Reviewer', 'Viewer'].map((r) => <th key={r} className="px-2 py-2 font-medium">{t(`role.${r}`)}</th>)}
                <th className="px-3 py-2 font-medium">{t('field.PrimaryDisciplineId')}</th><th className="px-3 py-2 font-medium">{t('team.added')}</th><th><span className="sr-only">{t('common.actions')}</span></th></tr>
            </thead>
            <tbody>
              {d.members.map((m) => (
                <tr key={m.id} className="border-t">
                  <td className="px-3 py-1.5">
                    <div className="flex items-center gap-1 font-medium">{m.isPrimaryPm && <Crown className="size-3.5 text-warn" aria-label={t('team.primaryPm')} />}<PersonName name={m.displayName} active={m.isActive} /></div>
                    <div className="text-xs text-muted-foreground">{m.jobTitle}{m.leadOf.length > 0 && ` · ${t('role.DisciplineLead')}: ${m.leadOf.map((id) => d.disciplines.find((x) => x.id === id)?.name).join(', ')}`}</div>
                  </td>
                  {['PM', 'TeamMember', 'Reviewer', 'Viewer'].map((r) => (
                    <td key={r} className="px-2 py-1.5">
                      <Checkbox aria-label={`${t(`role.${r}`)} — ${m.displayName}`} checked={m.roles.includes(r)} disabled={!d.canManage || (m.isPrimaryPm && r === 'PM')}
                        onCheckedChange={(c) => {
                          const roles = c ? [...m.roles, r] : m.roles.filter((x) => x !== r)
                          if (roles.length) run(() => patch(`projects/${p.id}/members/${m.id}`, { roles }))
                        }} />
                    </td>
                  ))}
                  <td className="px-3 py-1.5">
                    <select className="h-8 rounded-md border bg-card px-1 text-sm" value={m.primaryDisciplineId ?? ''} disabled={!d.canManage} aria-label={t('field.PrimaryDisciplineId')}
                      onChange={(e) => run(() => patch(`projects/${p.id}/members/${m.id}`, { primaryDisciplineId: e.target.value || null }))}>
                      <option value="">{t('team.noPrimary')}</option>
                      {d.disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                    </select>
                  </td>
                  <td className="whitespace-nowrap px-3 py-1.5 text-xs text-muted-foreground">{fmtDate(m.addedAt)}</td>
                  <td className="px-3 py-1.5 text-right">
                    {(d.canManage || m.canStaff) && !m.isPrimaryPm && <Button size="sm" variant="ghost" onClick={() => setRemoving(m)}>{t('common.remove')}</Button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>
      {removing && <RemoveMember projectId={p.id} member={removing} onClose={() => { setRemoving(null); reload() }} />}
      {fromTemplate && <AddFromTemplateDialog projectId={p.id} onClose={(added) => { setFromTemplate(false); if (added) reload() }} />}
    </div>
  )
}

function AddMember({ projectId, disciplines, canManage, onDone }: { projectId: string; disciplines: TeamData['disciplines']; canManage: boolean; onDone: () => void }) {
  const [userId, setUserId] = useState<string | null>(null)
  const [role, setRole] = useState('TeamMember')
  const [disc, setDisc] = useState('')
  const [err, setErr] = useState<unknown>(null)
  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="w-56"><PeoplePicker value={userId} onChange={(id) => setUserId(id)} placeholder={t('team.addMember')} /></div>
      {canManage && (
        <select className="h-8 rounded-md border px-2 text-sm" value={role} onChange={(e) => setRole(e.target.value)} aria-label={t('team.role')}>
          {['TeamMember', 'Reviewer', 'Viewer', 'PM'].map((r) => <option key={r} value={r}>{t(`role.${r}`)}</option>)}
        </select>
      )}
      <select className="h-8 rounded-md border px-2 text-sm" value={disc} onChange={(e) => setDisc(e.target.value)} aria-label={t('field.PrimaryDisciplineId')}>
        <option value="">{t('team.noPrimary')}</option>
        {disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
      </select>
      <Button size="sm" disabled={!userId} onClick={async () => {
        setErr(null)
        try { await post(`projects/${projectId}/members`, { userId, roles: [role], primaryDisciplineId: disc || null }); setUserId(null); toast.success(t('team.added')); onDone() }
        catch (e) { setErr(e) }
      }}><Plus className="size-4" />{t('common.add')}</Button>
      {err != null && <div className="w-full"><ErrorBanner error={err} /></div>}
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
        <div className="space-y-2 text-sm">
          <ul className="max-h-40 overflow-y-auto rounded border p-2 text-xs">
            {[...items.data.tasks, ...items.data.deliverables].map((i: any) => <li key={i.id}><span className="key">{i.key}</span> {i.name} · {i.status}</li>)}
          </ul>
          <label className="flex items-center gap-2"><input type="radio" checked={choice === 'reassign'} onChange={() => setChoice('reassign')} />{t('team.reassignTo')}</label>
          {choice === 'reassign' && <Field label={t('team.newOwner')}><PeoplePicker value={to} onChange={setTo} exclude={[member.userId]} /></Field>}
          <label className="flex items-center gap-2"><input type="radio" checked={choice === 'leave'} onChange={() => setChoice('leave')} />{t('team.leaveFlagged')}</label>
        </div>
      )}
    </ConfirmDialog>
  )
}

export { selectCls }
