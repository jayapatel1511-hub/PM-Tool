import { useQuery } from '@tanstack/react-query'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import type { MouseEvent } from 'react'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { t, runtimeText } from '@/lib/i18n'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, selectCls } from '@/components/hub/common'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { recordContextActionLabel } from '@/components/hub/record-context'
import { DataTable } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { Button } from '@/components/ui/button'
import { reviewResponseOutcomeLabel } from './review-response-form'
import { CoordStatus, RegisterPager } from './CoordinationForms'

type Schemas = components['schemas']
type Finding = Schemas['ReviewRegisterRow']
type Person = Schemas['ReviewRegisterPerson']
type Originator = Schemas['ReviewRegisterOriginator']
const keys = { discipline: 'findingDisciplineId', severity: 'findingSeverity', originatorKind: 'findingOriginatorKind',
  originator: 'findingOriginatorId', resolver: 'findingResolverId', all: 'findingAllRounds', page: 'findingPage', size: 'findingPageSize' } as const
const positive = (raw: string | null, fallback: number, maximum = 2147483647) => {
  const value = Number(raw)
  return raw && /^[1-9]\d*$/.test(raw) && Number.isSafeInteger(value) && value <= maximum ? value : fallback
}
const knownId = (id?: string | null): id is string => !!id && id !== '00000000-0000-0000-0000-000000000000'
const stateName = (name: string | null, state: string) => !name?.trim() ? t('reviewRegister.unknown')
  : state === 'Inactive' ? t('common.inactiveSuffix', { name }) : state === 'Active' ? name : `${name} · ${t('reviewRegister.unknown')}`
function PersonValue({ person, focusKey }: { person?: Person | null; focusKey: string }) {
  return person && knownId(person.id) && person.name?.trim() && ['Active', 'Inactive'].includes(person.state)
    ? <PersonPanelTrigger userId={person.id} name={person.name} active={person.state === 'Active'} focusKey={focusKey} />
    : <span className="text-muted-foreground">{stateName(person?.name ?? null, person?.state ?? 'Unknown')}</span>
}
function OriginatorValue({ person, focusKey }: { person: Originator; focusKey: string }) {
  if (person.kind === 'User' && !person.externalPartyId && knownId(person.userId) && person.name?.trim() && ['Active', 'Inactive'].includes(person.state))
    return <PersonPanelTrigger userId={person.userId} name={person.name} active={person.state === 'Active'} focusKey={focusKey} />
  return <span className="text-muted-foreground">{stateName(person.name, person.state)}{person.kind === 'ExternalParty' && <> · {t('reviewRegister.external')}</>}</span>
}
function reasonText(reason: string | null) {
  switch (reason) {
    case 'historical_round': return t('reviewRegister.reasonHistorical')
    case 'package_terminal': return t('reviewRegister.reasonTerminal')
    case 'not_assessed': return t('reviewRegister.reasonNotAssessed')
    case 'not_your_action': return t('reviewRegister.reasonNotYours')
    case 'person_unavailable': return t('reviewRegister.reasonPerson')
    default: {
      const translated = reason ? runtimeText(reason) : null
      return translated && translated !== reason ? translated : t('reviewRegister.reasonOther')
    }
  }
}

/** Read-only unresolved finding index. Commands and authority stay in each exact finding detail. */
export function ReviewOpenFindings({ projectId, projectNumber }: { projectId: string; projectNumber: string }) {
  const me = useMe(), registerOn = useFlag('register_screens'), patternsOn = useFlag('screen_patterns'), peopleOn = useFlag('people_screens')
  const [sp, setSp] = useSearchParams(), location = useLocation(), navigate = useNavigate()
  const disciplineId = sp.get(keys.discipline) ?? '', severity = sp.get(keys.severity) ?? '', originatorId = sp.get(keys.originator) ?? ''
  const originatorKind = sp.get(keys.originatorKind) ?? (originatorId ? 'User' : ''), resolverId = sp.get(keys.resolver) ?? ''
  const allRounds = sp.get(keys.all) === 'true', page = positive(sp.get(keys.page), 1), pageSize = positive(sp.get(keys.size), 50, 200)
  const query = { DisciplineId: disciplineId || undefined, Severity: severity || undefined, OriginatorKind: originatorKind || undefined,
    OriginatorId: originatorId || undefined, ResolverId: resolverId || undefined, AllRounds: allRounds, Page: page, PageSize: pageSize }
  const q = useQuery({ queryKey: ['review-open-findings', projectId, me.id, registerOn, patternsOn, peopleOn, query], enabled: registerOn,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/review-findings', { params: { projectId }, query }) })
  const data = q.error ? undefined : q.data, rows = data?.items ?? [], options = data?.options
  const set = (key: string, value: string) => {
    const next = new URLSearchParams(sp)
    if (value) next.set(key, value); else next.delete(key)
    if (key !== keys.page) next.delete(keys.page)
    setSp(next, { replace: true, state: location.state })
  }
  const chooseOriginator = (value: string) => {
    const next = new URLSearchParams(sp), colon = value.indexOf(':')
    next.delete(keys.originator); next.delete(keys.originatorKind); next.delete(keys.page)
    if (colon > 0) { next.set(keys.originatorKind, value.slice(0, colon)); if (value.slice(colon + 1)) next.set(keys.originator, value.slice(colon + 1)) }
    setSp(next, { replace: true, state: location.state })
  }
  const clear = () => {
    const next = new URLSearchParams(sp)
    for (const key of [keys.discipline, keys.severity, keys.originator, keys.originatorKind, keys.resolver, keys.page]) next.delete(key)
    setSp(next, { replace: true, state: location.state })
  }
  const href = (row: Finding) => {
    const next = new URLSearchParams(sp); next.set('panel', `ReviewFinding:${row.id}`); next.set('round', row.roundId)
    return `/projects/${encodeURIComponent(projectNumber)}/reviews?${next}`
  }
  const inspect = (event: MouseEvent<HTMLAnchorElement>) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    event.preventDefault()
    const state: unknown = location.state
    const prior = state && typeof state === 'object' && !Array.isArray(state) ? state : {}
    navigate(event.currentTarget.getAttribute('href')!, { state: { ...prior, recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
  }
  const rowLink = (row: Finding, surface: 'table' | 'card') => <Link id={`review-finding-open-${projectId}-${row.roundId}-${row.id}-${surface}`}
    data-review-finding-open={row.id} to={href(row)} onClick={inspect} className="break-words font-medium text-primary underline-offset-4 hover:underline"
    aria-label={`${t('reviewRegister.inspect')}: ${row.packageKey} · ${t('reviewRegister.round', { n: row.roundNumber })} · ${row.text}`}>{row.text}</Link>
  const packageInfo = (row: Finding) => <div className="space-y-1"><p className="key font-medium">{row.packageKey}</p><p className="break-words">{row.packageTitle}</p>
    <p className="text-xs text-muted-foreground">{t('reviewRegister.round', { n: row.roundNumber })} · {row.packageStatus}</p>
    {!row.isCurrentRound && <p className="text-xs font-medium text-warn">{t('reviewRegister.historical')}</p>}
    {!row.actionability.isPackageLive && <p className="text-xs font-medium text-muted-foreground">{t('reviewRegister.terminal')}</p>}</div>
  const findingMeta = (row: Finding) => <div className="space-y-1"><div className="flex flex-wrap gap-2"><CoordStatus status={row.severity} /><CoordStatus status={row.status} /></div>
    {(row.response != null || row.responseOutcome != null) && <p className="break-words text-xs text-muted-foreground">{t('reviewResponse.outcome')}: {reviewResponseOutcomeLabel(row.responseOutcome)}</p>}
    {row.pendingWithdrawalAcknowledgement && <p className="text-xs font-medium text-warn">{t('reviewRegister.pendingAck')}</p>}
    {row.carriedFromId && <p className="text-xs text-muted-foreground">{t('reviewRegister.carried')}</p>}
    <p className="break-words text-xs text-muted-foreground">{t('reviewRegister.source', { key: row.source.key, revision: row.source.revision })}{!row.source.available && <> · {t('reviewRegister.sourceUnavailable')}</>}</p></div>
  const roleKey = (row: Finding, role: string, surface: string) => `review-register:${projectId}:${row.roundId}:${row.id}:${role}:${surface}`
  const nextAction = (row: Finding, surface: string) => <div className="space-y-1"><p>{recordContextActionLabel(row.actionability.nextAction)}</p>
    <PersonValue person={row.actionability.nextActor} focusKey={roleKey(row, 'next-actor', surface)} />
    {row.actionability.canAct ? <p className="text-xs font-medium">{t('reviewRegister.yourNext')}</p> : <p className="text-xs text-muted-foreground">{reasonText(row.actionability.reason)}</p>}</div>
  const columns: Column<Finding>[] = [
    { id: 'findingPackage', label: t('reviewRegister.package'), fixed: true, className: 'min-w-40 align-top', cell: packageInfo },
    { id: 'findingText', label: t('reviewRegister.finding'), fixed: true, className: 'min-w-64 max-w-xl align-top', cell: row => <div className="space-y-2">{rowLink(row, 'table')}{findingMeta(row)}</div> },
    { id: 'findingDiscipline', label: t('reviewRegister.discipline'), fixed: true, className: 'align-top', cell: row => stateName(row.disciplineName, row.disciplineState) },
    { id: 'findingOriginator', label: t('reviewRegister.originator'), fixed: true, className: 'align-top', cell: row => <OriginatorValue person={row.originator} focusKey={roleKey(row, 'originator', 'table')} /> },
    { id: 'findingResolver', label: t('reviewRegister.resolver'), fixed: true, className: 'align-top', cell: row => <PersonValue person={row.resolutionOwner} focusKey={roleKey(row, 'resolver', 'table')} /> },
    { id: 'findingNext', label: t('reviewRegister.next'), fixed: true, className: 'min-w-44 align-top', cell: row => nextAction(row, 'table') },
  ]
  // Fixed columns/server order: parent package sort/cols/page parameters never alter this independent list.
  const table = useTable(`hub.review-register.${projectId}.${me.id}`, columns, rows, row => [row.id], null)
  if (!registerOn) return null
  const originValue = originatorKind ? `${originatorKind}:${originatorId}` : ''
  const originOptions = options?.originators.flatMap(person => person.kind === 'User' && knownId(person.userId)
    ? [{ value: `User:${person.userId}`, label: stateName(person.name, person.state) }]
    : person.kind === 'ExternalParty' && knownId(person.externalPartyId) ? [{ value: `ExternalParty:${person.externalPartyId}`, label: `${stateName(person.name, person.state)} · ${t('reviewRegister.external')}` }] : []) ?? []
  const disciplineOptions = options?.disciplines.map(d => ({ value: d.id, label: stateName(d.name, d.state) })) ?? []
  const resolverOptions = options?.resolutionOwners.filter(person => knownId(person.id)).map(person => ({ value: person.id!, label: stateName(person.name, person.state) })) ?? []
  const severityOptions = options?.severities.map(value => ({ value, label: value })) ?? []
  const optionLabel = (choices: { value: string; label: string }[], value: string) => choices.find(choice => choice.value === value)?.label ?? t('reviewRegister.selectedUnavailable')
  const tokens = [
    ...(disciplineId ? [{ key: keys.discipline, label: t('reviewRegister.discipline'), value: optionLabel(disciplineOptions, disciplineId) }] : []),
    ...(severity ? [{ key: keys.severity, label: t('reviewRegister.severity'), value: severity }] : []),
    ...(originValue ? [{ key: keys.originator, label: t('reviewRegister.originator'), value: optionLabel(originOptions, originValue) }] : []),
    ...(resolverId ? [{ key: keys.resolver, label: t('reviewRegister.resolver'), value: optionLabel(resolverOptions, resolverId) }] : []),
  ]
  const field = (id: string, label: string, value: string, choices: { value: string; label: string }[], change: (value: string) => void) => <Field label={label} htmlFor={id}>
    <select id={id} className={selectCls} value={value} disabled={q.isPending} onChange={event => change(event.target.value)}><option value="">{t('reviewRegister.any')}</option>
      {value && !choices.some(choice => choice.value === value) && <option value={value}>{t('reviewRegister.selectedUnavailable')}</option>}
      {choices.map(choice => <option key={choice.value} value={choice.value}>{choice.label}</option>)}
    </select></Field>
  return <section data-review-open-findings={projectId} aria-labelledby={`review-findings-title-${projectId}`} className="space-y-4 border-t pt-6">
    <div><h2 id={`review-findings-title-${projectId}`} className="text-lg font-semibold">{t('reviewRegister.title')}</h2><p className="text-sm text-muted-foreground">{t('reviewRegister.hint')}</p></div>
    <FilterBar activeCount={tokens.length} lead={<label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={allRounds} onChange={event => set(keys.all, event.target.checked ? 'true' : '')} />{t('reviewRegister.allRounds')}<span className="sr-only">{t('reviewRegister.allHint')}</span></label>}
      active={<ActiveFilters tokens={tokens} onRemove={key => key === keys.originator ? chooseOriginator('') : set(key, '')} onClear={clear} />}>
      {field('finding-discipline', t('reviewRegister.discipline'), disciplineId, disciplineOptions, value => set(keys.discipline, value))}
      {field('finding-severity', t('reviewRegister.severity'), severity, severityOptions, value => set(keys.severity, value))}
      {field('finding-originator', t('reviewRegister.originator'), originValue, originOptions, chooseOriginator)}
      {field('finding-resolver', t('reviewRegister.resolver'), resolverId, resolverOptions, value => set(keys.resolver, value))}
    </FilterBar>
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : data && <>
      <div className="flex flex-wrap items-end justify-between gap-3"><p role="status" className="text-sm tabular-nums">{t('reviewRegister.count', { shown: data.items.length, total: data.totalCount })} · {t(data.allRounds ? 'reviewRegister.allRounds' : 'reviewRegister.currentRounds')}</p>
        <Field label={t('reviewRegister.pageSize')} htmlFor="finding-page-size"><select id="finding-page-size" className={selectCls} value={pageSize} onChange={event => set(keys.size, event.target.value)}>
          {[25, 50, 100, 200].includes(pageSize) ? null : <option value={pageSize}>{pageSize}</option>}{[25, 50, 100, 200].map(size => <option key={size} value={size}>{size}</option>)}
        </select></Field></div>
      {q.isFetching && <p role="status" className="text-xs text-muted-foreground">{t('reviewRegister.refreshing')}</p>}
      <DataTable table={table} rows={rows} loading={false} caption={t('reviewRegister.title')} current={row => sp.get('panel') === `ReviewFinding:${row.id}`}
        empty={<Empty>{t('reviewRegister.empty')}</Empty>} cards={<ul className="space-y-3 md:hidden" aria-label={t('reviewRegister.title')}>{rows.map(row => <li key={row.id} data-review-finding-card={row.id} className="space-y-3 rounded-lg border bg-card p-4 text-sm">
          {packageInfo(row)}{rowLink(row, 'card')}{findingMeta(row)}
          <dl className="grid min-w-0 grid-cols-[auto_1fr] gap-x-3 gap-y-2"><dt>{t('reviewRegister.discipline')}</dt><dd>{stateName(row.disciplineName, row.disciplineState)}</dd>
            <dt>{t('reviewRegister.originator')}</dt><dd><OriginatorValue person={row.originator} focusKey={roleKey(row, 'originator', 'card')} /></dd>
            <dt>{t('reviewRegister.resolver')}</dt><dd><PersonValue person={row.resolutionOwner} focusKey={roleKey(row, 'resolver', 'card')} /></dd>
            <dt>{t('reviewRegister.next')}</dt><dd>{nextAction(row, 'card')}</dd></dl>
        </li>)}</ul>} />
      <RegisterPager label={t('reviewRegister.pager')} page={data.page} total={data.totalCount} pageSize={data.pageSize} onPage={next => set(keys.page, String(next))} />
      {!rows.length && page > 1 && <Button variant="outline" onClick={() => set(keys.page, '')}>{t('reviewRegister.firstPage')}</Button>}
    </>}
  </section>
}
