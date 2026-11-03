import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import type { components } from '@/gen/api'
import { RecordContext, RecordContextActors, recordContextActionLabel } from '@/components/hub/record-context'
import { useItemPanel } from '@/components/hub/panel-host'
import { itemHref } from '@/components/hub/search'
import { Pill } from '@/components/hub/pills'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { fmtDate, fmtTime } from '@/lib/format'
import { runtimeText, tv } from '@/lib/i18n'
import { CoordStatus, PersonLabel, type CoordOptions } from './CoordinationForms'
import { useWorkRecordOrigin } from './work-record-navigation'

type Schemas = components['schemas']
type Check = Schemas['SubmissionCheckRow']
type Summary = Schemas['DeliverySummary']
type Detail = Schemas['SubmissionDetail']
type Source = Schemas['RecordContextInputView']
const label = (key: string, values?: Record<string, string | number>) => runtimeText(`delivery.${key}`, values)
const rank = (state?: string | null) => state === 'Fail' ? 0 : state === 'Pending' ? 1 : state === 'Pass' ? 3 : state === 'Not Applicable' ? 4 : 2
const unknown = () => label('unavailable')

export function DeliveryBlockerSummary({ summary }: { summary?: Summary | null }) {
  if (!summary || typeof summary.available !== 'boolean') return <span className="text-xs text-muted-foreground">{label('checklistUnavailable')}</span>
  return <span className="inline-flex flex-wrap gap-x-3 gap-y-1 text-sm tabular-nums" title={label('countBasis')}>
    <span className={summary.blockingCheckCount ? 'text-bad' : 'text-muted-foreground'}>{summary.available ? typeof summary.blockingCheckCount === 'number' ? label('blockers', { n: summary.blockingCheckCount }) : label('checklistUnavailable') : label('notEvaluated')}</span>
    {summary.packageProblemCount > 0 && <span className="text-warn">{label('packageProblems', { n: summary.packageProblemCount })}</span>}
    {summary.unknownCheckCount > 0 && <span className="text-warn">{label('unknownChecks', { n: summary.unknownCheckCount })}</span>}
  </span>
}

function ActionLink({ source, number, children }: { source: Source; number: string; children: ReactNode }) {
  const open = useItemPanel(), location = useLocation(), origin = useWorkRecordOrigin()
  const cls = 'break-words text-left text-primary underline underline-offset-4'
  return source.kind === 'Task' || source.kind === 'Decision'
    ? <button type="button" className={cls} onClick={event => open(source.kind, source.id, captureRecordContextOrigin(location, event.currentTarget))}>{children}</button>
    : <Link className={cls} to={itemHref(source.kind, number, source.id)} onClick={origin}>{children}</Link>
}

/** Sorting/grouping is presentation only. Every effective state and blocking membership comes from the canonical read. */
export function SubmissionDelivery({ detail: d, number, meId, checkId, options, milestoneName, renderActions }: {
  detail: Detail; number: string; meId: string; checkId?: string; options: CoordOptions; milestoneName?: string; renderActions: (check: Check) => ReactNode
}) {
  const presentation = d.delivery, origin = useWorkRecordOrigin(), openPanel = useItemPanel()
  const checks = Array.isArray(d.checks) ? d.checks.filter(check => !checkId || check.id === checkId) : []
  const facts = new Map((presentation?.checks ?? []).map(check => [check.checkId, check]))
  const groups = new Map<string, { state: string | null; owner: Schemas['RecordContextPerson']; discipline: string; rows: Check[] }>()
  for (const check of checks) {
    const fact = facts.get(check.id), state = fact?.state ?? check.effectiveStatus ?? null
    const owner = fact?.checkingOwner ?? check.context?.owner ?? { id: null, name: null, state: 'Unknown', kind: 'User' }
    const discipline = check.projectDisciplineId ? fact?.disciplineName ?? unknown() : label('general')
    const key = rank(state) < 3 ? `${rank(state)}:${owner.id ?? owner.state}:${check.projectDisciplineId ?? ''}` : String(rank(state))
    const group = groups.get(key) ?? { state, owner, discipline, rows: [] }; group.rows.push(check); groups.set(key, group)
  }
  const ordered = [...groups.entries()].sort(([a, x], [b, y]) => rank(x.state) - rank(y.state) || (x.owner.name ?? '').localeCompare(y.owner.name ?? '') || x.discipline.localeCompare(y.discipline) || a.localeCompare(b))
  const revisions = (r?: Schemas['DeliveryRevision'] | null) => r ? r.url ? <a className="font-mono text-primary underline underline-offset-4" href={r.url} target="_blank" rel="noopener noreferrer">{r.revision}</a> : <span className="font-mono">{r.revision}</span> : unknown()
  return <div className="space-y-6" data-delivery-presentation>
    <header className="space-y-2 rounded-lg border p-4">
      <DeliveryBlockerSummary summary={presentation?.summary} />
      <dl className="grid gap-x-6 gap-y-2 sm:grid-cols-2"><div><dt className="text-xs text-muted-foreground">{runtimeText('coord.coordinator')}</dt><dd><PersonLabel options={options} id={d.package.coordinatorId} focusKey={`delivery:${d.package.id}:coordinator`} /></dd></div>
        <div><dt className="text-xs text-muted-foreground">{runtimeText('submissions.targetDate')}</dt><dd className="tabular-nums">{fmtDate(d.package.targetDate)}</dd></div></dl>
    </header>
    <section aria-label={runtimeText('submissions.checks')} className="space-y-3" data-delivery-checklist>
      <h3 className="text-base/6 font-semibold">{runtimeText('submissions.checks')}</h3>
      {(!presentation?.checksComplete || !Array.isArray(d.checks) || !Array.isArray(presentation?.checks)) && <p role="status" className="text-sm text-warn">{label('checklistUnavailable')}</p>}
      {presentation?.packageProblems?.length ? <aside className="space-y-1 rounded-md border p-3"><h4 className="font-medium">{label('packageProblems', { n: presentation.packageProblems.length })}</h4>{presentation.packageProblems.map((problem, index) => <p key={`${problem.code}:${index}`}>{problem.message}</p>)}</aside> : null}
      {ordered.map(([key, group]) => <section key={key} className="space-y-2" data-delivery-state={group.state ?? 'Unknown'}>
        <h4 className="flex flex-wrap items-center gap-2 text-sm font-medium"><CoordStatus status={group.state ?? 'Not Assessed'} />{rank(group.state) < 3 && <><span>{label('checkingOwner')}:</span><RecordContextActors actors={[group.owner]} actorState={group.owner.state} meId={meId} focusKey={`submission:${d.package.id}:checking-owner:${key}`} /><span className="text-muted-foreground">· {group.discipline}</span></>}</h4>
        {group.rows.sort((a, b) => a.kind.localeCompare(b.kind) || a.id.localeCompare(b.id)).map(check => {
          const fact = facts.get(check.id), sources = check.context?.inputs ?? []
          return <article key={check.id} id={`delivery-check-${check.id}`} data-delivery-check={check.id} className="space-y-3 rounded-lg border p-4">
            <div className="flex flex-wrap items-center gap-2"><h5 className="font-medium">{check.kind === 'Applicability' ? check.evidenceRule : tv(check.kind)}</h5>{!check.required && <span className="text-xs text-muted-foreground">{label('optional')}</span>}{renderActions(check)}</div>
            {!checkId && check.context && <RecordContext context={check.context} projectNumber={number} meId={meId} openPanel={openPanel} />}
            {fact && Array.isArray(fact.reasons) ? fact.reasons.map((reason, index) => <div key={`${reason.code}:${index}`} className="space-y-1"><p>{reason.message}</p>{reason.sourceExpected && !reason.source && <p className="text-xs text-warn">{label('sourceUnavailable')}</p>}</div>) : <><p className="text-xs text-muted-foreground">{label('checkDetailsUnavailable')}</p>{check.reason && <p>{check.reason}</p>}</>}
            {sources.length > 0 ? <ul className="space-y-2">{sources.map(source => <li key={`${source.kind}:${source.id}`}>
              <ActionLink source={source} number={number}>{source.nextAction === 'not_assessed' || source.nextAction === 'none' ? label('openSource') : recordContextActionLabel(source.nextAction)} · <span className="key">{source.key}</span> · {source.title}</ActionLink>
              <p className="text-xs text-muted-foreground"><RecordContextActors actors={source.nextActors} actorState={source.actorState ?? 'Unknown'} meId={meId} focusKey={`submission:${d.package.id}:check:${check.id}:source:${source.kind}:${source.id}:next`} /></p>
            </li>)}</ul> : check.context && !check.context.terminal ? <Link className="text-primary underline underline-offset-4" onClick={origin} to={itemHref('SubmissionCheck', number, check.id)}>{check.context.nextAction === 'not_assessed' ? label('openCheck') : recordContextActionLabel(check.context.nextAction)}</Link> : null}
            {check.evidenceUrl && <a className="text-primary underline underline-offset-4" href={check.evidenceUrl} target="_blank" rel="noopener noreferrer">{runtimeText('coord.evidence')}</a>}
          </article>
        })}
      </section>)}
    </section>
    <section aria-label={label('statusCounts')} className="space-y-2"><h3 className="text-base/6 font-semibold">{label('statusCounts')}</h3>
      {presentation?.manifestComplete && Array.isArray(presentation.manifest) && Array.isArray(presentation.statusCounts) ? <><p className="text-xs text-muted-foreground">{label('currentAt', { time: presentation.evaluatedAt ? fmtTime(presentation.evaluatedAt) : unknown() })}</p>
        <ul className="flex flex-wrap gap-x-6 gap-y-2">{presentation.statusCounts.map(count => <li key={count.status}>{tv(count.status)}: <span className="tabular-nums">{count.count}</span></li>)}</ul>
        {presentation.unavailableDeliverables > 0 && <p className="text-warn">{label('unavailableMembers', { n: presentation.unavailableDeliverables })}</p>}</> : <p className="text-muted-foreground">{label('statusUnavailable')}</p>}
    </section>
    <details className="rounded-lg border" data-delivery-manifest><summary className="cursor-pointer px-4 py-3 font-medium">{runtimeText('submissions.manifest')}</summary><div className="space-y-3 border-t p-4">
      {(!presentation?.manifestComplete || !Array.isArray(presentation?.manifest)) && <p className="text-warn">{label('manifestUnavailable')}</p>}
      {Array.isArray(presentation?.manifest) && presentation.manifest.map(member => <article key={member.id} className="space-y-2 rounded-md border p-3">
        <h4 className="font-medium">{member.available ? <Link className="text-primary underline underline-offset-4" onClick={origin} to={itemHref('Deliverable', number, member.deliverableId)}><span className="key">{member.key}</span> · {member.title}</Link> : label('memberUnavailable')}</h4>
        <dl className="grid gap-2 sm:grid-cols-2"><div><dt className="text-xs text-muted-foreground">{label('recordedRevision')}</dt><dd>{revisions(member.recordedRevision)}</dd></div><div><dt className="text-xs text-muted-foreground">{label('currentRevision')}</dt><dd>{revisions(member.currentRevision)}</dd></div></dl>
        {member.revisionMismatch === true && <Pill tone="warn">{label('revisionMismatch')}</Pill>}
        {member.revisionMismatch == null && <p className="text-xs text-muted-foreground">{label('comparisonUnavailable')}</p>}
      </article>)}
    </div></details>
    <details className="rounded-lg border"><summary className="cursor-pointer px-4 py-3 font-medium">{label('packageDetails')}</summary><dl className="grid gap-x-4 gap-y-2 border-t p-4 sm:grid-cols-[auto_1fr]"><dt>{runtimeText('submissions.milestone')}</dt><dd>{milestoneName ?? unknown()}</dd><dt>{runtimeText('submissions.recipient')}</dt><dd>{d.package.recipientReference}</dd><dt>{label('purpose')}</dt><dd className="whitespace-pre-wrap">{d.package.purpose}</dd></dl></details>
    {presentation?.issueAvailability === 'Available' && d.issue ? <section className="space-y-2 rounded-lg border p-4" aria-label={label('issueEvidence')}><h3 className="font-medium">{runtimeText('submissions.issued')}</h3><p>{fmtTime(d.issue.authorisedAt)} · <PersonLabel options={options} id={d.issue.authorisedBy} focusKey={`delivery:${d.package.id}:issue:authorised-by`} /> · {d.issue.destination}</p><p className="text-xs text-muted-foreground">{label('issueFrozen')}</p><a className="text-primary underline underline-offset-4" href={d.issue.transmittalUrl} target="_blank" rel="noopener noreferrer">{runtimeText('submissions.transmittal')}</a></section>
      : <p className="text-sm text-muted-foreground">{presentation?.issueAvailability === 'NotIssued' ? label('notIssued') : label('issueUnavailable')}</p>}
  </div>
}
