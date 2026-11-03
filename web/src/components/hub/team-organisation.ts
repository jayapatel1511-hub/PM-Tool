import { t } from '@/lib/i18n'

export type TeamFacet = 'homeDiscipline' | 'office' | 'supervisor' | 'unit'
type Reference = { id: string; name?: string | null }
export type TeamOrganisationRow = { organisationFacts?: { homeDiscipline?: Reference | null; office?: Reference | null; supervisor?: Reference | null; unit?: Reference | null } | null }
const facets: TeamFacet[] = ['homeDiscipline', 'office', 'supervisor', 'unit']
export const teamFacets = (unitsOn: boolean) => facets.filter(facet => unitsOn || facet !== 'unit')
export const teamFacetLabel = (facet: TeamFacet) => t(`teamOrganisation.${facet}`)
export function teamFacetValue(row: TeamOrganisationRow, facet: TeamFacet) {
  if (!row.organisationFacts) return 'unavailable'
  return row.organisationFacts[facet]?.id ?? 'not-set'
}
export function teamFacetText(row: TeamOrganisationRow, facet: TeamFacet) {
  if (!row.organisationFacts) return t('common.unavailable')
  const reference = row.organisationFacts[facet]
  return reference ? reference.name || `${t('common.unavailable')} · ${reference.id}` : t('teamOrganisation.notSet')
}
export function teamFacetChoices(rows: TeamOrganisationRow[], facet: TeamFacet) {
  const choices = new Map<string, string>()
  for (const row of rows) choices.set(teamFacetValue(row, facet), teamFacetText(row, facet))
  const names = new Map<string, number>()
  for (const label of choices.values()) names.set(label, (names.get(label) ?? 0) + 1)
  return [...choices].map(([id, name]) => ({ id, name: (names.get(name) ?? 0) > 1 ? `${name} · ${id}` : name }))
    .sort((a, b) => Number(a.id === 'not-set' || a.id === 'unavailable') - Number(b.id === 'not-set' || b.id === 'unavailable') || a.name.localeCompare(b.name) || a.id.localeCompare(b.id))
}
export function teamGroup(sp: URLSearchParams, enabled: boolean, unitsOn: boolean): TeamFacet | undefined {
  return enabled ? teamFacets(unitsOn).find(facet => facet === sp.get('group')) : undefined
}
/** Exact IDs over the unchanged server cohort; missing facts are distinct from known Not set. */
export function filterTeamRows<T extends TeamOrganisationRow>(rows: T[], sp: URLSearchParams, enabled: boolean, unitsOn: boolean): T[] {
  if (!enabled) return rows
  return rows.filter(row => teamFacets(unitsOn).every(facet => {
    const value = sp.get(`${facet}Id`)
    return !value || !/^(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|not-set|unavailable)$/i.test(value) || teamFacetValue(row, facet) === value
  }))
}
/** Keep identity separate from presentation, including duplicate supervisor names. */
export function groupTeamRows<T extends TeamOrganisationRow>(rows: T[], cohort: T[], group?: TeamFacet) {
  if (!group) return { rows, groupBy: undefined, groupLabel: (key: string) => key, supervisor: (_key: string): Reference | undefined => undefined }
  const choices = teamFacetChoices(cohort, group), labels = new Map(choices.map(choice => [choice.id, choice.name]))
  const grouped = new Map<string, T[]>()
  for (const row of rows) { const key = teamFacetValue(row, group), bucket = grouped.get(key); if (bucket) bucket.push(row); else grouped.set(key, [row]) }
  return { rows: choices.flatMap(choice => grouped.get(choice.id) ?? []), groupBy: (row: T) => teamFacetValue(row, group),
    groupLabel: (key: string) => labels.get(key) ?? t('common.unavailable'),
    supervisor: (key: string) => group === 'supervisor' ? cohort.find(row => row.organisationFacts?.supervisor?.id === key && !!row.organisationFacts.supervisor.name)?.organisationFacts?.supervisor ?? undefined : undefined }
}
export function teamViewParams(sp: URLSearchParams, unitsOn: boolean, cols: string, scope: Record<string, string> = {}) {
  const params = Object.fromEntries(sp)
  delete params.panel; delete params.view
  if (!unitsOn) delete params.unitId
  return { ...params, ...scope, group: teamGroup(sp, true, unitsOn) ?? 'none', cols }
}
