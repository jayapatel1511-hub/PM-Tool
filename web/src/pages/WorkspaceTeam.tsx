import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { filterTeamRows, groupTeamRows, teamGroup, teamViewParams } from '@/components/hub/team-organisation'
import { TeamOrganisationControls, TeamOrganisationGroup, teamOrganisationColumns } from '@/components/hub/team-organisation-controls'
import { ViewMenu } from '@/components/hub/views'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Page } from '@/components/hub/common'
import { IdentityDot } from '@/components/hub/identity-colour'
import { Avatar } from '@/components/hub/people'
import { Key } from '@/components/hub/pills'
import { DataTable, RegisterCards } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { HOME_PRESETS, useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { plural, t, runtimeText, textFor, tv } from '@/lib/i18n'
import { CertificationFilter, PersonFactsChips, useCertificationTags } from '@/components/hub/person-facts'

type Member = components['schemas']['TeamPersonRow']

type Row = Member & { id: string }

const tag = 'rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium text-secondary-foreground'

/** Team (§36.1, FR-VIS-01): everyone active on the selected projects with their roles there, linking to each project's Team
 *  screen. The same people make up Home's Team Members figure. */
export function WorkspaceTeamPage() {
  const scope = useScope()
  const [sp, setSp] = useSearchParams()
  const overview = useFlag('home_overview')
  const peopleOn = useFlag('people_screens'), unitsOn = useFlag('org_structure')
  const factsEnabled = useFlag('capacity_screens'), tags = useCertificationTags()
  const certificationTagId = factsEnabled ? sp.get('certificationTagId') || undefined : undefined
  const homePreset = overview ? sp.get('homePreset') || undefined : undefined
  const homeProjectStatus = overview ? sp.get('homeProjectStatus') || undefined : undefined
  const clearHomeFilters = () => { const next = new URLSearchParams(sp); next.delete('homePreset'); next.delete('homeProjectStatus'); if (factsEnabled) next.delete('certificationTagId'); setSp(next, { replace: true }) }
  const [localTerm, setLocalTerm] = useState('')
  const term = peopleOn ? sp.get('q') ?? '' : localTerm
  const setTerm = (value: string) => { if (!peopleOn) { setLocalTerm(value); return }; const next = new URLSearchParams(sp); if (value) next.set('q', value); else next.delete('q'); setSp(next, { replace: true }) }
  const q = useQuery({ queryKey: ['wteam', scope.api, ...(peopleOn ? ['people_screens', unitsOn] : []), ...(homePreset ? [homePreset] : []), ...(homeProjectStatus ? [homeProjectStatus] : []), ...(certificationTagId ? ['certification', certificationTagId] : [])], enabled: scope.ready, queryFn: () => typedRequest('get', '/api/v1/team', { query: { projects: scope.api, ...(homePreset ? { homePreset } : {}), ...(homeProjectStatus ? { homeProjectStatus } : {}), ...(certificationTagId ? { certificationTagId } : {}) } }) })
  const s = term.trim().toLowerCase()
  const cohort: Row[] = (q.data?.people ?? []).map((p) => ({ ...p, id: p.userId }))
  const people = filterTeamRows(cohort.filter(p => !s || p.displayName.toLowerCase().includes(s) || (p.jobTitle ?? '').toLowerCase().includes(s)), sp, peopleOn, unitsOn)
  const columns = (surface: 'table' | 'card'): Column<Row>[] => [
    { id: 'person', label: t('team.person'), fixed: true, className: 'align-top', cell: (p) => (
      <div className="flex min-w-48 items-center gap-2.5"><Avatar id={p.userId} name={p.displayName} />
        <div className="min-w-0"><div className="break-words font-semibold"><PersonPanelTrigger userId={p.userId} name={p.displayName} focusKey={`workspace-team:${p.userId}:${surface}:person`} /></div>
          {p.jobTitle && <div className="text-xs/[18px] text-muted-foreground">{p.jobTitle}</div>}<PersonFactsChips facts={p.personFacts} /></div></div>
    ) },
    ...(peopleOn ? teamOrganisationColumns<Row>(unitsOn, p => `workspace-team:${p.userId}:${surface}`) : []),
    { id: 'roles', label: t('team.rolesByProject'), fixed: true, className: 'min-w-80 align-top', cell: (p) => (
      <ul className="space-y-1.5">
        {p.projects.map((m) => (
          <li key={m.projectId} className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <Link to={`/projects/${m.projectNumber}/team`} className="inline-flex min-w-0 items-center gap-2 hover:underline" title={m.projectName}>
              <IdentityDot id={m.projectId} colour={m.identityColour} /><span className="shrink-0 whitespace-nowrap"><Key>{m.projectNumber}</Key></span><span className="break-words">{m.projectName}</span>
            </Link>
            {m.roles.map((r) => <span key={r} className={tag}>{runtimeText(`role.${r}`)}</span>)}
            {m.leads.map((l) => <span key={l} className={tag}>{t('team.leadOf', { name: l })}</span>)}
            {peopleOn && m.responsibilityNote && <span className="w-full break-words text-xs"><span className="text-muted-foreground">{t('responsibility.label')}: </span>{m.responsibilityNote}</span>}
          </li>
        ))}
      </ul>
    ) },
  ]
  const table = useTable<Row>(peopleOn ? 'hub.teamColumns.people' : 'hub.teamColumns', columns('table'), people, (r) => [r.displayName])
  const grouped = groupTeamRows(table.sorted, cohort, teamGroup(sp, peopleOn, unitsOn))
  const cardGroups: { label: string; rows: Row[] }[] = []
  for (const row of grouped.rows) { const label = grouped.groupBy?.(row) ?? ''; if (cardGroups.at(-1)?.label !== label) cardGroups.push({ label, rows: [] }); cardGroups.at(-1)!.rows.push(row) }
  const groupLabel = (key: string, surface: 'table' | 'card') => <TeamOrganisationGroup label={grouped.groupLabel(key)} supervisor={grouped.supervisor(key)} focusKey={`workspace-team:group:${key}:${surface}`} />
  const cardColumns = columns('card').filter(column => table.visible.some(visible => visible.id === column.id))
  return (
    <Page tabs={<WorkspaceTabs />} title={t('nav.team')} subtitle={q.data ? plural(peopleOn ? people.length : q.data.people.length, 'team.onePerson', 'team.nPeople', { n: peopleOn ? people.length : q.data.people.length }) : scope.label} actions={peopleOn && <ViewMenu listType="workspace-team" captureParams={() => teamViewParams(sp, unitsOn, table.colsParam, scope.params)} />}>
      <FilterBar search={<Field label={t('common.search')} htmlFor="team-search">
        <Input id="team-search" type="search" placeholder={t('team.search')} value={term} onChange={(e) => setTerm(e.target.value)} />
      </Field>}>{factsEnabled && !peopleOn && <CertificationFilter id="team-certification" value={certificationTagId ?? ''} onChange={value => { const next = new URLSearchParams(sp); if (value) next.set('certificationTagId', value); else next.delete('certificationTagId'); setSp(next, { replace: true }) }} />}</FilterBar>
      {peopleOn && <div className="flex flex-wrap items-end justify-between gap-3"><TeamOrganisationControls rows={cohort} unitsOn={unitsOn} prefix="workspace-team">
        {factsEnabled && <CertificationFilter id="team-certification" value={certificationTagId ?? ''} onChange={value => { const next = new URLSearchParams(sp); if (value) next.set('certificationTagId', value); else next.delete('certificationTagId'); setSp(next, { replace: true }) }} />}
      </TeamOrganisationControls>{table.menu}</div>}
      {(homePreset || homeProjectStatus || certificationTagId) && <ActiveFilters tokens={[
        ...(homePreset ? [{ key: 'homePreset', label: t('homeScope.label'), value: textFor(HOME_PRESETS, homePreset) }] : []),
        ...(homeProjectStatus ? [{ key: 'homeProjectStatus', label: t('homeScope.projectStatus'), value: homeProjectStatus.split(',').map(tv).join(', ') }] : []),
        ...(certificationTagId ? [{ key: 'certificationTagId', label: t('personFacts.filter'), value: tags.data?.find(tag => tag.id === certificationTagId)?.name ?? t('common.unavailable') }] : []),
      ]} onRemove={key => { const next = new URLSearchParams(sp); next.delete(key); setSp(next, { replace: true }) }} onClear={clearHomeFilters} />}
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <DataTable table={table} rows={grouped.rows} groupBy={grouped.groupBy} groupLabel={groupLabel} cards={peopleOn ? <RegisterCards groups={cardGroups} columns={cardColumns} current={() => false} groupLabel={key => groupLabel(key, 'card')} /> : undefined} loading={q.isPending} wide={!peopleOn} caption={t('nav.team')} current={() => false}
        empty={<Empty action={s && <Button variant="outline" onClick={() => setTerm('')}>{t('common.clear')}</Button>}>{t('team.none')}</Empty>} />
    </Page>
  )
}
