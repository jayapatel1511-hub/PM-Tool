import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { MemberResponsibilityDialog, type ResponsibilityDraft } from '@/components/hub/member-responsibility'
import { filterTeamRows, groupTeamRows, teamGroup, teamViewParams } from '@/components/hub/team-organisation'
import { TeamOrganisationControls, TeamOrganisationGroup, teamOrganisationColumns } from '@/components/hub/team-organisation-controls'
import { TeamOpenRoles } from '@/components/hub/team-open-roles'
import { ViewMenu } from '@/components/hub/views'
import { disciplineColour } from '@/lib/discipline-colour'
import { useQuery } from '@tanstack/react-query'
import { Crown, Lock, Plus } from 'lucide-react'
import { Fragment, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Notice, Page, Section, selectCls } from '@/components/hub/common'
import { Avatar, PeoplePicker, PersonName, usePeopleCapacity, weekOf } from '@/components/hub/people'
import { Chip } from '@/components/hub/pills'
import { DataTable } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { useProjectRefresh, useReference } from '@/hooks/data'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { useFlag, useOffer } from '@/lib/auth'
import { hoursLabel } from '@/components/planner/labels'
import { fmtDate, today } from '@/lib/format'
import { runtimeText, t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { AddFromTemplateDialog, usePublishedTemplates } from './FromTemplate'
import { CertificationFilter, PersonFactsChips, useCertificationTags } from '@/components/hub/person-facts'

type TeamData = components['schemas']['ProjectTeamRead']

const ROLES = ['PM', 'TeamMember', 'Reviewer', 'Viewer']

/** Team & Disciplines (§12.2, §13.16): discipline leads, members with roles and primary discipline. */
export function TeamTab() {
  const p = useCurrentProject()
  const peopleOn = useFlag('people_screens'), unitsOn = useFlag('org_structure')
  const fill = useOffer()(p.permissions.proposeAllocation, p.status)
  const ref = useReference()
  const refresh = useProjectRefresh()
  const [sp, setSp] = useSearchParams(), factsEnabled = useFlag('capacity_screens'), tags = useCertificationTags()
  const certificationTagId = factsEnabled ? sp.get('certificationTagId') || undefined : undefined
  const q = useQuery({ queryKey: ['p', p.id, 'team', ...(peopleOn ? ['people_screens', unitsOn] : []), ...(certificationTagId ? ['certification', certificationTagId] : [])], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/team', { params: { id: p.id }, query: certificationTagId ? { certificationTagId } : {} }) })
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
  const name = (userId?: string | null) => d.members.find((m) => m.userId === userId)?.displayName ?? d.disciplines.find(discipline => discipline.leadUserId === userId)?.leadName
  const unused = ref.data?.disciplines.filter((x) => x.isActive && !d.disciplines.some((y) => y.disciplineId === x.id && y.isActive)) ?? []

  return (
    <Page {...header}>
      {err != null && <ErrorBanner error={err} />}
      {!d.canManage && d.manageReason && <Notice icon={Lock} title={d.manageReason} />}
      {peopleOn && <TeamOpenRoles projectId={p.id} number={p.projectNumber} fill={fill} />}
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
                    ? <PeoplePicker value={x.leadUserId} valueName={name(x.leadUserId)} placeholder={t('team.noLead')} onChange={(id) => run(() => typedRequest('patch', '/api/v1/projects/{id}/disciplines/{pdId}', { params: { id: p.id, pdId: x.id }, body: { leadUserId: id }, ifMatch: x.rowVersion }))} />
                    : <span className="text-sm">{t('team.lead')}: {x.leadUserId && name(x.leadUserId) ? <PersonPanelTrigger userId={x.leadUserId} name={name(x.leadUserId)!} active={d.members.find(member => member.userId === x.leadUserId)?.isActive ?? true} focusKey={`project:${p.id}:discipline:${x.id}:lead`} /> : <PersonName name={name(x.leadUserId)} />}</span>}
                  {peopleOn && d.canManage && x.isActive && x.leadUserId && name(x.leadUserId) && <span className="mt-1 block text-xs"><PersonPanelTrigger userId={x.leadUserId} name={name(x.leadUserId)!} focusKey={`project:${p.id}:discipline:${x.id}:selected-lead-details`}>{t('common.details')}</PersonPanelTrigger></span>}
                </div>
                {!x.leadUserId && x.isActive && <Chip tone="warn">{t('team.leadMissing')}</Chip>}
                {d.canManage && (x.isActive
                  ? <Button size="sm" variant="ghost" className="text-bad hover:text-bad" aria-label={t('team.removeNamed', { name: x.name })} onClick={() => run(() => typedRequest('delete', '/api/v1/projects/{id}/disciplines/{pdId}', { params: { id: p.id, pdId: x.id }, ifMatch: x.rowVersion }))}>{t('common.remove')}</Button>
                  : <Button size="sm" variant="outline" onClick={() => run(() => typedRequest('patch', '/api/v1/projects/{id}/disciplines/{pdId}', { params: { id: p.id, pdId: x.id }, body: { isActive: true }, ifMatch: x.rowVersion }))}>{t('admin.activate')}</Button>)}
              </li>
            ))}
          </ul>
          {d.canManage && (
            <form className="flex flex-wrap items-end gap-3 border-t px-5 py-4" onSubmit={(e) => { e.preventDefault(); if (addDisc) run(() => typedRequest('post', '/api/v1/projects/{id}/disciplines', { params: { id: p.id }, body: { disciplineId: addDisc, leadUserId: null } })).then(() => setAddDisc('')) }}>
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

        <Section title={t('team.members')} count={filterTeamRows(d.members, sp, peopleOn, unitsOn).length} accent="mint">
          {factsEnabled && <div className="space-y-2 border-b px-5 py-3"><CertificationFilter id="project-team-certification" value={certificationTagId ?? ''} onChange={value => { const next = new URLSearchParams(sp); if (value) next.set('certificationTagId', value); else next.delete('certificationTagId'); setSp(next, { replace: true }) }} />{certificationTagId && <p className="text-sm">{t('personFacts.filter')}: {tags.data?.find(tag => tag.id === certificationTagId)?.name ?? t('common.unavailable')} <Button variant="ghost" size="sm" onClick={() => { const next = new URLSearchParams(sp); next.delete('certificationTagId'); setSp(next, { replace: true }) }}>{t('common.clear')}</Button></p>}</div>}
          <AddMember projectId={p.id} disciplines={d.disciplines} canManage={d.canManage} onDone={reload} />
          <MemberTable projectId={p.id} projectStatus={p.status} d={d} away={away} run={run} onRemove={setRemoving} />
        </Section>
      </div>
      {removing && <RemoveMember projectId={p.id} member={removing} onClose={() => { setRemoving(null); reload() }} />}
      {fromTemplate && <AddFromTemplateDialog projectId={p.id} onClose={(added) => { setFromTemplate(false); if (added) reload() }} />}
    </Page>
  )
}

type Member = TeamData['members'][number]

/** The members list on the shared DataTable (§47.7, FR-REC-14): a table from `md` up and, below it, one card per member with the same controls. */
function MemberTable({ projectId, projectStatus, d, away, run, onRemove }: { projectId: string; projectStatus: string; d: TeamData; away: (id: string) => ReactNode; run: (f: () => Promise<unknown>) => Promise<void>; onRemove: (m: Member) => void }) {
  const peopleOn = useFlag('people_screens'), unitsOn = useFlag('org_structure'), [sp] = useSearchParams()
  const shown = filterTeamRows(d.members, sp, peopleOn, unitsOn)
  const [responsibility, setResponsibility] = useState<ResponsibilityDraft | null>(null)
  const responsibilityNote = (member: Member) => peopleOn && <div className="mt-2 space-y-1 text-xs" data-member-responsibility={member.id}>
    <p className="break-words"><span className="text-muted-foreground">{t('responsibility.label')}: </span>{member.responsibility?.note || t('teamOrganisation.notSet')}</p>
    {member.responsibility?.canEdit && <Button size="sm" variant="ghost" className="h-auto min-h-8 whitespace-normal px-0 text-left text-xs text-primary" aria-label={t('responsibility.editNamed', { name: member.displayName })}
      onClick={() => setResponsibility({ memberId: member.id, name: member.displayName, note: member.responsibility?.note ?? '', rowVersion: member.rowVersion })}>{t('common.edit')}</Button>}
  </div>
  const edit = (m: Member, body: object) => run(() => typedRequest('patch', '/api/v1/projects/{id}/members/{memberId}', { params: { id: projectId, memberId: m.id }, body, ifMatch: m.rowVersion }))
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
            {m.isPrimaryPm && <Crown role="img" className="size-4 shrink-0 text-warn" aria-label={t('team.primaryPm')} />}<PersonPanelTrigger userId={m.userId} name={m.displayName} active={m.isActive} focusKey={`project:${projectId}:member:${m.id}:table:person`} />{away(m.userId)}
          </div>
          <div className="text-xs/[18px] text-muted-foreground">{m.jobTitle}{leadOf(m)}</div>
          <PersonFactsChips facts={m.personFacts} />{responsibilityNote(m)}
        </div>
      </div>) },
    ...(peopleOn ? teamOrganisationColumns<Member>(unitsOn, m => `project:${projectId}:member:${m.id}:table`) : []),
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
  const table = useTable<Member>(peopleOn ? 'hub.teamMemberColumns.people' : 'hub.teamMemberColumns', columns, shown, (m) => [m.displayName])
  const grouped = groupTeamRows(table.sorted, d.members, teamGroup(sp, peopleOn, unitsOn))
  const cards = <ul className="divide-y md:hidden">{grouped.rows.map((m, index) => <Fragment key={m.id}>{grouped.groupBy && (index === 0 || grouped.groupBy(grouped.rows[index - 1]) !== grouped.groupBy(m)) && <li className="break-words bg-muted px-4 py-2 text-sm font-semibold" data-team-group><TeamOrganisationGroup label={grouped.groupLabel(grouped.groupBy(m))} supervisor={grouped.supervisor(grouped.groupBy(m))} focusKey={`project:${projectId}:team:group:${grouped.groupBy(m)}:card`} /></li>}<li key={m.id} className="space-y-3 p-4 text-sm">
    <div className="flex items-start gap-2.5"><Avatar id={m.userId} name={m.displayName} /><div className="min-w-0"><h4 className="flex items-center gap-1 break-words font-semibold">{m.isPrimaryPm && <Crown className="size-4 shrink-0 text-warn" aria-label={t('team.primaryPm')} />}<PersonPanelTrigger userId={m.userId} name={m.displayName} active={m.isActive} focusKey={`project:${projectId}:member:${m.id}:card:person`} />{away(m.userId)}</h4><p className="break-words text-xs/[18px] text-muted-foreground">{m.jobTitle}{leadOf(m)}</p>{peopleOn && <dl className="mt-2 space-y-1">{teamOrganisationColumns<Member>(unitsOn, m => `project:${projectId}:member:${m.id}:card`).filter(column => table.visible.some(visible => visible.id === column.id)).map(column => <div key={column.id} className="flex flex-wrap gap-x-2"><dt className="text-xs text-muted-foreground">{column.label}</dt><dd className="break-words text-xs">{column.cell(m)}</dd></div>)}</dl>}<PersonFactsChips facts={m.personFacts} />{responsibilityNote(m)}</div></div>
    <fieldset className="flex flex-wrap gap-x-4 gap-y-2"><legend className="sr-only">{t('team.role')}</legend>{ROLES.map((r) => <label key={r} className="flex min-h-10 items-center gap-2">{roleBox(m, r)}{runtimeText(`role.${r}`)}</label>)}</fieldset>
    <Field label={t('field.PrimaryDisciplineId')} htmlFor={`member-disc-${m.id}`}><select id={`member-disc-${m.id}`} className={selectCls} value={m.primaryDisciplineId ?? ''} disabled={!d.canManage} onChange={(e) => edit(m, { primaryDisciplineId: e.target.value || null })}><option value="">{t('team.noPrimary')}</option>{d.disciplines.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></Field>
    <p className="text-xs/[18px] text-muted-foreground">{t('team.added')}: {fmtDate(m.addedAt)}</p>
    {removeButton(m)}
  </li></Fragment>)}</ul>
  return <>{peopleOn && <div className="space-y-2 px-5 py-3"><div className="flex flex-wrap items-end justify-between gap-3"><TeamOrganisationControls rows={d.members} unitsOn={unitsOn} prefix="project-team" />{table.menu}<ViewMenu listType="project-team" projectId={projectId} captureParams={() => teamViewParams(sp, unitsOn, table.colsParam)} /></div><p className="text-xs text-muted-foreground" role="status">{t('teamOrganisation.visible', { n: shown.length })}</p></div>}<DataTable table={table} rows={grouped.rows} groupBy={grouped.groupBy} groupLabel={(key, surface) => <TeamOrganisationGroup label={grouped.groupLabel(key)} supervisor={grouped.supervisor(key)} focusKey={`project:${projectId}:team:group:${key}:${surface}`} />} loading={false} empty={<Empty title={t('common.empty')} />} current={() => false} caption={t('team.members')} label={t('team.members')} cards={cards} />{peopleOn && responsibility && <MemberResponsibilityDialog key={responsibility.memberId} projectId={projectId} draft={responsibility} latest={d.members.find(member => member.id === responsibility.memberId)} needsReason={projectStatus === 'Complete'} onClose={() => setResponsibility(null)} />}</>
}

function AddMember({ projectId, disciplines, canManage, onDone }: { projectId: string; disciplines: TeamData['disciplines']; canManage: boolean; onDone: () => void }) {
  const peopleOn = useFlag('people_screens')
  const [userId, setUserId] = useState<string | null>(null)
  const [role, setRole] = useState('TeamMember')
  const [disc, setDisc] = useState('')
  const [err, setErr] = useState<unknown>(null)
  return (
    <div className="space-y-3 border-b px-5 py-4">
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('team.person')} htmlFor="team-add-person" className="w-full sm:w-64">
          <PeoplePicker id="team-add-person" capacityWeek={peopleOn ? weekOf(today()) : undefined} value={userId} onChange={(id) => setUserId(id)} placeholder={t('team.addMember')} />
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
          if (!userId) return
          setErr(null)
          try { await typedRequest('post', '/api/v1/projects/{id}/members', { params: { id: projectId }, body: { userId, roles: [role], primaryDisciplineId: disc || null } }); setUserId(null); toast.success(t('team.added')); onDone() }
          catch (e) { setErr(e) }
        }}><Plus className="size-4" />{t('common.add')}</Button>
      </div>
      {err != null && <ErrorBanner error={err} />}
    </div>
  )
}

/** TM-04: removing someone with open items asks whether to reassign or leave them flagged. */
function RemoveMember({ projectId, member, onClose }: { projectId: string; member: TeamData['members'][number]; onClose: () => void }) {
  const items = useQuery({ queryKey: ['open-items', member.id], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/members/{memberId}/open-items', { params: { id: projectId, memberId: member.id } }) })
  const [choice, setChoice] = useState<'leave' | 'reassign'>('reassign')
  const [to, setTo] = useState<string | null>(null)
  const n = items.data?.count ?? 0
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} destructive reason="optional"
      title={t('team.removeTitle', { name: member.displayName })}
      body={items.isPending ? t('app.loading') : n === 0 ? t('team.removeNoItems') : t('team.removeItems', { n })}
      confirmLabel={t('common.remove')} busy={n > 0 && choice === 'reassign' && !to}
      onConfirm={(reason) => typedRequest('delete', '/api/v1/projects/{id}/members/{memberId}', { params: { id: projectId, memberId: member.id }, query: { reassignTo: n > 0 && choice === 'reassign' ? to ?? undefined : undefined, reason }, ifMatch: member.rowVersion })}>
      {items.data && n > 0 && (
        <div className="space-y-3 text-sm">
          <ul className="max-h-40 space-y-1 overflow-y-auto rounded-md border bg-muted p-3">
            {[...items.data.tasks, ...items.data.deliverables].map((i) => <li key={i.id}><span className="key">{i.key}</span> {i.name} · {tv(i.status)}</li>)}
          </ul>
          <label className="flex min-h-6 items-center gap-2"><input type="radio" name="team-remove-choice" className="size-4 accent-(--primary)" checked={choice === 'reassign'} onChange={() => setChoice('reassign')} />{t('team.reassignTo')}</label>
          {choice === 'reassign' && <Field label={t('team.newOwner')} htmlFor="team-new-owner" className="ml-6"><PeoplePicker id="team-new-owner" value={to} onChange={setTo} exclude={[member.userId]} /></Field>}
          <label className="flex min-h-6 items-center gap-2"><input type="radio" name="team-remove-choice" className="size-4 accent-(--primary)" checked={choice === 'leave'} onChange={() => setChoice('leave')} />{t('team.leaveFlagged')}</label>
        </div>
      )}
    </ConfirmDialog>
  )
}

