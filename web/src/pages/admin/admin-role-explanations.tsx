import { useId, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ErrorBanner, Field, Loading, selectCls } from '@/components/hub/common'
import { Pill, type Tone } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import type { components } from '@/gen/api'
import { useFlag, useMe } from '@/lib/auth'
import { typedRequest } from '@/lib/api'
import { t } from '@/lib/i18n'

type Read = components['schemas']['AdminRoleExamplesRead']
type Operation = components['schemas']['AdminPermissionOperation']
type Catalog = Pick<Read, 'systemRoleChoices' | 'membershipChoices' | 'scenarioChoices' | 'operationChoices'> & { rowLabels: Record<string, string> }

function OperationDecision({ kind }: { kind: string }) {
  const label = kind === 'Conditional' ? t('adminRoles.conditional') : kind === 'NotEligible' ? t('adminRoles.notEligible')
    : kind === 'PolicyPending' ? t('adminRoles.policyPending') : t('common.unavailable')
  const tone: Tone = kind === 'NotEligible' ? 'bad' : kind === 'PolicyPending' ? 'warn' : 'idle'
  return <Pill tone={tone}>{label}</Pill>
}

function OperationRequirements({ operation }: { operation: Operation }) {
  return <article data-role-operation={operation.key} className="min-w-0 space-y-3 border-t pt-4 first:border-t-0 first:pt-0">
    <div className="flex flex-wrap items-start justify-between gap-2"><h5 className="min-w-0 break-words font-semibold">{operation.label}</h5><OperationDecision kind={operation.decisionKind} /></div>
    {operation.decisionKind === 'Conditional' && <p className="text-sm text-muted-foreground">{t('adminRoles.conditionalHint')}</p>}
    {operation.decisionKind === 'PolicyPending' && <p className="text-sm text-warn">{t('adminRoles.policyPendingHint')}</p>}
    <section aria-label={t('adminRoles.operationFacts')} className="space-y-2"><h6 className="text-sm font-medium">{t('adminRoles.operationFacts')}</h6>
      {operation.facts.length ? <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">{operation.facts.map((fact, index) => <li key={`${index}-${fact}`}>{fact}</li>)}</ul> : <p className="text-sm text-muted-foreground">{t('common.unavailable')}</p>}
    </section>
    <section aria-label={t('adminRoles.requirements')} className="space-y-2"><h6 className="text-sm font-medium">{t('adminRoles.requirements')}</h6>
      {operation.requirements.length ? <ul className="space-y-2">{operation.requirements.map(requirement => <li key={requirement.key} data-role-requirement={requirement.key} className="rounded-md border px-3 py-2 text-sm">
        <span className="font-medium">{requirement.state === 'RequiresLiveCheck' ? t('adminRoles.requiresLiveCheck') : requirement.state === 'NotMetByExample' ? t('adminRoles.notMetByExample') : requirement.state === 'MetByExample' ? t('adminRoles.metByExample') : t('common.unavailable')}</span>
        <p className="mt-1 break-words text-muted-foreground">{requirement.text}</p>
      </li>)}</ul> : <p className="text-sm text-muted-foreground">{t('adminRoles.requirementsUnavailable')}</p>}
    </section>
    <details data-role-factors=""><summary className="cursor-pointer py-1 text-sm font-medium">{t('adminRoles.supportingChecks')}</summary><p className="mt-2 text-xs text-muted-foreground">{t('adminRoles.factorHint')}</p>
      <ul className="mt-3 space-y-3 text-sm">{operation.factors.map(factor => <li key={factor.actionKey} data-role-factor={factor.actionKey}>
        <p className="font-medium">{factor.actionLabel}</p><p>{t(factor.eligible ? 'adminRoles.factorEligible' : 'adminRoles.factorNotEligible')}</p>
        {factor.reasonText && <p className="mt-1 text-muted-foreground">{factor.reasonText}</p>}{factor.requiresReason && <p className="mt-1 text-muted-foreground">{t('adminRoles.requiresReason')}</p>}
      </li>)}</ul>
    </details>
  </article>
}

/** Examples describe selected system and project roles. Supporting checks never become whole-command authority. */
export function AdminRoleExamples() {
  const on = useFlag('people_screens'), me = useMe(), prefix = useId()
  const [role, setRole] = useState('baseline-Standard'), [scenario, setScenario] = useState('same-discipline')
  const [membership, setMembership] = useState('none'), [operation, setOperation] = useState('all')
  const catalog = useRef<Catalog | undefined>(undefined)
  const query = useQuery<Read>({ queryKey: ['admin-role-examples', me.id, on, role, scenario, membership, operation], enabled: on,
    queryFn: () => typedRequest('get', '/api/v1/admin/role-explanations', { query: { role, scenario, membership, operation } }) })
  if (!on) return null
  const payload = query.isSuccess && !query.error && !query.isFetching ? query.data : undefined
  const data = payload && Array.isArray(payload.rows) && Array.isArray(payload.roles)
    && Array.isArray(payload.systemRoleChoices) && Array.isArray(payload.membershipChoices) && Array.isArray(payload.scenarioChoices) && Array.isArray(payload.operationChoices)
    && payload.selectedMembershipKey === membership && payload.roles.some(item => item.key === role)
    && payload.scenarioChoices.some(choice => choice.key === scenario)
    && (operation === 'all' || (payload.rows.length === 1 && payload.rows[0].operations.length === 1 && payload.rows[0].operations[0].key === operation)) ? payload : undefined
  if (data) catalog.current = { systemRoleChoices: data.systemRoleChoices, membershipChoices: data.membershipChoices, scenarioChoices: data.scenarioChoices, operationChoices: data.operationChoices,
    rowLabels: { ...catalog.current?.rowLabels, ...Object.fromEntries(data.rows.map(row => [row.rowId, row.label])) } }
  const choices = catalog.current
  const operationGroups = [...new Set(choices?.operationChoices.filter(choice => choice.key !== 'all').map(choice => choice.rowId) ?? [])]
  return <section data-admin-role-examples aria-labelledby={`${prefix}-title`} className="min-w-0 space-y-4 rounded-lg border bg-card p-4">
    <div className="flex flex-wrap items-start justify-between gap-3"><div className="min-w-0 space-y-1"><h3 id={`${prefix}-title`} className="text-base font-semibold">{t('adminRoles.operationTitle')}</h3><p className="max-w-3xl text-sm text-muted-foreground">{t('adminRoles.operationHint')}</p></div><Button type="button" size="sm" variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>{t('adminRoles.refresh')}</Button></div>
    {choices && <div className="grid gap-3 sm:grid-cols-2">
      <Field label={t('adminRoles.systemRole')} htmlFor={`${prefix}-role`}><select id={`${prefix}-role`} className={selectCls} value={role} onChange={event => setRole(event.target.value)}>{choices.systemRoleChoices.map(choice => <option key={choice.key} value={choice.key}>{choice.label}</option>)}</select></Field>
      <Field label={t('adminRoles.projectMembership')} htmlFor={`${prefix}-membership`} hint={t('adminRoles.membershipHint')}><select id={`${prefix}-membership`} className={selectCls} value={membership} onChange={event => setMembership(event.target.value)}>{choices.membershipChoices.map(choice => <option key={choice.key} value={choice.key}>{choice.label}</option>)}</select></Field>
      <Field label={t('adminRoles.scenario')} htmlFor={`${prefix}-scenario`}><select id={`${prefix}-scenario`} className={selectCls} value={scenario} onChange={event => setScenario(event.target.value)}>{choices.scenarioChoices.map(choice => <option key={choice.key} value={choice.key}>{choice.label}</option>)}</select></Field>
      <Field label={t('adminRoles.operation')} htmlFor={`${prefix}-operation`}><select id={`${prefix}-operation`} className={selectCls} value={operation} onChange={event => setOperation(event.target.value)}><option value="all">{t('adminRoles.allOperations')}</option>{operationGroups.map(rowId => <optgroup key={rowId} label={choices.rowLabels[rowId] ?? t('adminRoles.unavailable')}>{choices.operationChoices.filter(choice => choice.rowId === rowId && choice.key !== 'all').map(choice => <option key={choice.key} value={choice.key}>{choice.label}</option>)}</optgroup>)}</select></Field>
    </div>}
    {query.error ? <ErrorBanner error={query.error} retry={() => void query.refetch()} /> : query.isPending || query.isFetching ? <Loading rows={3} /> : data ? <>
      <p className="text-sm text-muted-foreground tabular-nums" role="status">{data.rows.length === 1 ? t('adminRoles.singleRow') : t('adminRoles.rowCount', { n: data.rows.length })}</p>
      <ol className="min-w-0 space-y-3">{data.rows.map(row => <li key={row.rowId} data-role-row={row.rowId}><details open={operation !== 'all' || row.rowId === 'S01'} className="min-w-0 rounded-lg border">
        <summary className="cursor-pointer px-4 py-3 text-sm font-semibold"><span className="break-words">{row.label}</span><span className="ml-2 font-normal text-muted-foreground tabular-nums">{row.operations.length === 1 ? t('adminRoles.singleOperation') : t('adminRoles.operationCount', { n: row.operations.length })}</span></summary>
        <div className="min-w-0 space-y-4 border-t p-4">{row.operations.map(item => <OperationRequirements key={item.key} operation={item} />)}</div>
      </details></li>)}</ol>
    </> : <p className="text-sm text-muted-foreground">{t('adminRoles.unavailable')}</p>}
  </section>
}
