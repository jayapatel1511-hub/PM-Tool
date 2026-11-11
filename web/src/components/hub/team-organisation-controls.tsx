import { useSearchParams } from 'react-router'
import type { ReactNode } from 'react'
import { Rows3 } from 'lucide-react'
import { ChoiceMenu, Field, FilterBar, selectCls } from '@/components/hub/common'
import type { Column } from '@/components/hub/table'
import { teamFacetChoices, teamFacetLabel, teamFacets, teamFacetText, teamGroup, type TeamFacet, type TeamOrganisationRow } from './team-organisation'
import { PersonPanelTrigger } from './person-panel-trigger'
import { t } from '@/lib/i18n'

export function TeamOrganisationValue({ row, facet, focusKey }: { row: TeamOrganisationRow; facet: TeamFacet; focusKey: string }) {
  const supervisor = facet === 'supervisor' ? row.organisationFacts?.supervisor : null
  return supervisor?.id && supervisor.name ? <PersonPanelTrigger userId={supervisor.id} name={supervisor.name} focusKey={focusKey} /> : <>{teamFacetText(row, facet)}</>
}
export function TeamOrganisationGroup({ label, supervisor, focusKey }: { label: string; supervisor?: { id: string; name?: string | null }; focusKey: string }) {
  return supervisor?.id && supervisor.name ? <PersonPanelTrigger userId={supervisor.id} name={supervisor.name} focusKey={focusKey}>{label}</PersonPanelTrigger> : <>{label}</>
}
export function teamOrganisationColumns<T extends TeamOrganisationRow>(unitsOn: boolean, focusKey: (row: T) => string): Column<T>[] {
  return teamFacets(unitsOn).map(facet => ({ id: facet, label: teamFacetLabel(facet), className: 'align-top',
    sort: row => teamFacetText(row, facet), cell: row => <span className="break-words"><TeamOrganisationValue row={row} facet={facet} focusKey={`${focusKey(row)}:${facet}`} /></span> }))
}
export function TeamOrganisationControls({ rows, unitsOn, prefix, children }: { rows: TeamOrganisationRow[]; unitsOn: boolean; prefix: string; children?: ReactNode }) {
  const [sp, setSp] = useSearchParams()
  const change = (key: string, value: string) => setSp(current => { const next = new URLSearchParams(current); if (value) next.set(key, value); else next.delete(key); return next }, { replace: true })
  // §41 R1: grouping is a compact menu button beside Filters, not a visible field.
  return <FilterBar activeCount={teamFacets(unitsOn).filter(facet => !!sp.get(`${facet}Id`)).length} lead={
    <ChoiceMenu label={t('teamOrganisation.group')} icon={Rows3} value={teamGroup(sp, true, unitsOn) ?? 'none'} onChange={value => change('group', value === 'none' ? '' : value)}
      options={[{ value: 'none', label: t('teamOrganisation.none') }, ...teamFacets(unitsOn).map(facet => ({ value: facet as string, label: teamFacetLabel(facet) }))]} />}>
    {teamFacets(unitsOn).map(facet => {
      const choices = teamFacetChoices(rows, facet), value = sp.get(`${facet}Id`) ?? ''
      return <Field key={facet} label={teamFacetLabel(facet)} htmlFor={`${prefix}-${facet}`} className="w-full sm:w-64"><select id={`${prefix}-${facet}`} className={selectCls}
        value={choices.some(choice => choice.id === value) ? value : ''} onChange={event => change(`${facet}Id`, event.target.value)}>
        <option value="">{t('teamOrganisation.all')}</option>{choices.map(choice => <option key={choice.id} value={choice.id}>{choice.name}</option>)}
      </select></Field>
    })}{children}
  </FilterBar>
}
