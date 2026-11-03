// S15 owns only these URL additions. The existing workspace and Calendar position remain authoritative.
export const calendarTypes = ['Deadline', 'Meeting', 'Site Work', 'Internal Task'] as const
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
export const calendarSourceKey = (type: string, id: string) => `${type}:${id}`
export const calendarTriggerId = (type: string, id: string, action = 'source') => `calendar-${action}-${type}-${id}`
export function calendarScreenState(params: URLSearchParams, dynamicToday: string) {
  const value = params.get('mode')
  const mode = value === 'month' || value === 'agenda' ? value : 'week'
  const hidden = calendarTypes.filter(type => (params.get('hide') ?? '').split(',').includes(type))
  const person = params.get('personId'), discipline = params.get('disciplineId')
  return { mode, anchor: params.get('date') ?? dynamicToday, hidden, types: calendarTypes.filter(type => !hidden.includes(type)),
    personId: person && guid.test(person) ? person : undefined, disciplineId: discipline && guid.test(discipline) ? discipline : undefined,
    showAway: params.get('showAway') === 'true' }
}
export function clearCalendarFilters(params: URLSearchParams) {
  const next = new URLSearchParams(params)
  for (const key of ['hide', 'showAway', 'personId', 'disciplineId', 'view']) next.delete(key)
  return next
}
export function calendarViewParams(params: URLSearchParams, scope: Record<string, string>) {
  const result: Record<string, string> = { ...scope }
  const state = calendarScreenState(params, '')
  if (params.has('mode')) result.mode = state.mode
  if (params.get('date')) result.date = params.get('date')!
  if (state.hidden.length) result.hide = state.hidden.join(',')
  if (state.showAway) result.showAway = 'true'
  if (state.personId) result.personId = state.personId
  if (state.disciplineId) result.disciplineId = state.disciplineId
  return result
}
export function calendarViewProject(params: URLSearchParams) {
  const selected = params.get('projects')
  return !params.has('ws') && selected && guid.test(selected) ? selected : undefined
}
