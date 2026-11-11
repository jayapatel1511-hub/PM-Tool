import { useQuery } from '@tanstack/react-query'
import { useLocation, useNavigate, useSearchParams } from 'react-router'
import type { PanelProps } from './panel-host'
import { Empty, ErrorBanner, Field, Loading, selectCls } from './common'
import { Avatar, CapacityFigure, PersonName, usePeopleCapacity } from './people'
import { CertificationChip } from './person-facts'
import { teamFacets, teamFacetLabel } from './team-organisation'
import { TeamOrganisationValue } from './team-organisation-controls'
import { Button } from '@/components/ui/button'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { fmtDate } from '@/lib/format'
import { t, textFor, tv } from '@/lib/i18n'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'

const PROJECT_ROLES = { PM: 'role.PM', DisciplineLead: 'role.DisciplineLead', TeamMember: 'role.TeamMember', Reviewer: 'role.Reviewer', Viewer: 'role.Viewer' } as const
const pageNumber = (raw: string | null, fallback: number) => raw && /^\d+$/.test(raw) && Number.isSafeInteger(Number(raw)) && Number(raw) > 0 ? Number(raw) : fallback

/** Read-only facts. The separate canonical planner read may omit an out-of-scope person entirely. */
export function PersonPanel({ id }: PanelProps) {
  const me = useMe(), enabled = useFlag('people_screens'), capacityOn = useFlag('capacity_screens'), unitsOn = useFlag('org_structure')
  const [sp, setSp] = useSearchParams(), location = useLocation(), navigate = useNavigate()
  const staffScope = sp.get('personStaffScope') ?? undefined, page = pageNumber(sp.get('personProjectsPage'), 1), size = Math.min(100, pageNumber(sp.get('personProjectsPageSize'), 25))
  const q = useQuery({ queryKey: ['person-panel', me.id, id, staffScope ?? null, page, size, capacityOn, unitsOn], enabled,
    queryFn: () => typedRequest('get', '/api/v1/people/{userId}/panel', { params: { userId: id }, query: { staffScope, projectsPage: page, projectsPageSize: size } }) })
  const data = !q.error ? q.data : undefined
  const capacity = usePeopleCapacity(data?.week, enabled && !!data, data ? [id] : [])
  if (!enabled) return null
  if (q.error) return <div className="p-5"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  if (q.isPending || !data) return <Loading rows={6} />
  const projects = data.projects, facts = capacityOn ? data.personFacts : null
  const changePage = (nextPage: number, nextSize = size) => {
    const next = new URLSearchParams(sp); next.set('personProjectsPage', String(nextPage)); next.set('personProjectsPageSize', String(nextSize)); setSp(next, { replace: true, state: location.state })
  }
  return <article className="min-w-0 space-y-6 p-5" data-person-panel={id}>
    <header className="flex items-start gap-3"><Avatar id={id} name={data.displayName} className="size-10 text-sm" /><div className="min-w-0">
      <h2 className="break-words text-xl font-semibold"><PersonName name={data.displayName} active={data.isActive} /></h2>
      {data.jobTitle && <p className="mt-1 break-words text-sm text-muted-foreground">{data.jobTitle}</p>}
    </div></header>
    <section aria-labelledby="person-panel-organisation"><h3 id="person-panel-organisation" className="text-sm font-semibold">{t('personPanel.organisation')}</h3>
      <dl className="mt-2 grid gap-2 text-sm">{teamFacets(unitsOn).map(facet => <div key={facet} className="grid min-w-0 gap-0.5 sm:grid-cols-[9rem_1fr] sm:gap-3"><dt className="text-muted-foreground">{teamFacetLabel(facet)}</dt><dd className="break-words"><TeamOrganisationValue row={data} facet={facet} focusKey={`person:${id}:organisation:${facet}`} /></dd></div>)}</dl>
    </section>
    {me.capabilities.workload && capacity(id) && <section aria-label={t('personPanel.currentWeek', { date: fmtDate(data.week) })} className="space-y-1 border-y py-3">
      <h3 className="text-sm font-semibold">{t('personPanel.currentWeek', { date: fmtDate(data.week) })}</h3><CapacityFigure cell={capacity(id)} />
    </section>}
    {facts && <section aria-labelledby="person-panel-recorded"><h3 id="person-panel-recorded" className="text-sm font-semibold">{t('personFacts.adminSection')}</h3>
      <dl className="mt-2 grid gap-2 text-sm">{[
        [t('personFacts.typeLabel'), facts.notices.personType || t('personFacts.unrecorded')],
        [t('personFacts.start'), facts.notices.startDate ? fmtDate(facts.notices.startDate) : t('personFacts.unrecorded')],
        [t('personFacts.end'), facts.notices.endDate ? fmtDate(facts.notices.endDate) : t('personFacts.unrecorded')],
      ].map(([label, value]) => <div key={label} className="grid gap-0.5 sm:grid-cols-[9rem_1fr] sm:gap-3"><dt className="text-muted-foreground">{label}</dt><dd>{value}</dd></div>)}</dl>
      <h4 className="mt-4 text-sm font-medium">{t('personFacts.certifications')}</h4>
      {facts.certifications.length ? <ul className="mt-2 flex flex-wrap gap-2">{facts.certifications.map(record => <li key={record.id}><CertificationChip record={record} /></li>)}</ul> : <p className="mt-1 text-sm text-muted-foreground">{t('personFacts.noCertifications')}</p>}
    </section>}
    <section aria-labelledby="person-panel-projects"><h3 id="person-panel-projects" className="text-sm font-semibold">{t('personPanel.projects')}</h3><p className="mt-1 text-xs text-muted-foreground">{t('personPanel.projectsBasis')}</p>
      {projects.items.length ? <ul className="mt-3 divide-y">{projects.items.map(membership => <li key={membership.memberId} className="space-y-1 py-3 text-sm" data-person-project={membership.projectId}>
        <a className="break-words font-semibold text-primary underline-offset-2 hover:underline" href={`/projects/${membership.projectNumber}/team`} onClick={event => {
          if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
          event.preventDefault(); navigate(`/projects/${membership.projectNumber}/team`, { state: { recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
        }}>{membership.projectNumber} · {membership.projectName}</a>
        <p className="break-words text-xs text-muted-foreground">{tv(membership.projectStatus)} · {membership.roles.map(role => textFor(PROJECT_ROLES, role)).join(' · ')}{membership.primaryDiscipline && ` · ${membership.primaryDiscipline.name || t('common.unavailable')}`}</p>
        {membership.leadOf.map(discipline => <p key={discipline.id} className="break-words text-xs">{t('team.leadOf', { name: discipline.name || t('common.unavailable') })}</p>)}
        <p className="break-words"><span className="text-muted-foreground">{t('responsibility.label')}: </span>{membership.responsibilityNote || t('teamOrganisation.notSet')}</p>
      </li>)}</ul> : <Empty>{t(projects.total ? 'personPanel.emptyPage' : 'personPanel.noProjects')}</Empty>}
      <div className="mt-3 flex flex-wrap items-end justify-between gap-3 border-t pt-3">
        <Field label={t('personPanel.pageSize')} htmlFor="person-project-page-size"><select id="person-project-page-size" className={selectCls} value={size} onChange={event => changePage(1, Number(event.target.value))}>
          {[25, 50, 100].map(value => <option key={value} value={value}>{value}</option>)}{![25, 50, 100].includes(size) && <option value={size}>{size}</option>}
        </select></Field>
        <div className="space-y-2"><p role="status" className="text-xs text-muted-foreground">{t('personPanel.page', { page: projects.page, total: projects.total })}</p><div className="flex gap-2">
          <Button size="sm" variant="outline" disabled={projects.page <= 1 || q.isFetching} onClick={() => changePage(projects.page - 1)}>{t('common.previous')}</Button>
          <Button size="sm" variant="outline" disabled={projects.page * projects.pageSize >= projects.total || q.isFetching} onClick={() => changePage(projects.page + 1)}>{t('common.next')}</Button>
        </div></div>
      </div>
    </section>
  </article>
}
