import { Fragment, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import type { components } from '@/gen/api'
import { useItemPanel } from '@/components/hub/panel-host'
import { itemHref } from '@/components/hub/search'
import { RecordContextActors, recordContextActionLabel } from '@/components/hub/record-context'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { fmtDate, fmtTime } from '@/lib/format'
import { runtimeText, t, tv } from '@/lib/i18n'
import { Button } from '@/components/ui/button'
import { CoordStatus, PersonLabel, type CoordOptions } from './CoordinationForms'
import { useWorkRecordOrigin } from './work-record-navigation'

type Schemas = components['schemas']
type Presentation = Schemas['ReadinessDeliveryPresentation']
type Source = Schemas['RecordContextInputView']
type Input = Schemas['WorkReadinessInput']
type Check = Schemas['ReadinessCheckRead']
const label = (key: string, values?: Record<string, string | number>) => runtimeText(`readinessDelivery.${key}`, values)
const link = 'break-words text-left text-primary underline underline-offset-4'

function SourceAction({ source, number, children }: { source: Source; number: string; children: ReactNode }) {
  const open = useItemPanel(), location = useLocation(), origin = useWorkRecordOrigin()
  return source.kind === 'Task' || source.kind === 'Decision'
    ? <button type="button" className={link} onClick={event => open(source.kind, source.id, captureRecordContextOrigin(location, event.currentTarget))}>{children}</button>
    : <Link className={link} to={itemHref(source.kind, number, source.id)} onClick={origin}>{children}</Link>
}

/** Failure is the aggregate check's fact. A source keeps its own state and action, never an invented per-source failure. */
export function ReadinessNextSteps({ presentation: p, number, discipline, meId, inspect, focusKey = 'readiness:detail' }: {
  presentation?: Presentation | null; number: string; discipline: string; meId: string; inspect: (code: string) => void; focusKey?: string
}) {
  if (!p?.available || !Array.isArray(p.checks) || !Array.isArray(p.inputs))
    return <p className="text-sm text-warn">{label('checksUnavailable')}</p>
  const unresolved = p.checks.filter(c => c.blocking || c.unknown).map(c => c.code)
  if (p.blocked?.includes('Constraint') && !unresolved.includes('Constraint')) unresolved.push('Constraint')
  type Group = { actors: Source['nextActors']; actorState: string; applicability: boolean; entries: { code: string; inputs: Input[] }[] }
  const groups = new Map<string, Group>()
  for (const code of unresolved) {
    const check = p.checks.find(c => c.code === code), inputs = p.inputs.filter(input => input.code === code)
    const applicability = check?.applies == null && code !== 'Constraint' && inputs.length === 0
    const byActor = new Map<string, { actors: Source['nextActors']; actorState: string; inputs: Input[] }>()
    for (const input of inputs) {
      const actors = input.source.nextActors ?? [], actorState = input.source.actorState ?? 'Unknown'
      const key = `${actorState}:${actors.map(a => `${a.kind}:${a.id ?? a.state}`).sort().join(',')}`
      const group = byActor.get(key) ?? { actors, actorState, inputs: [] }; group.inputs.push(input); byActor.set(key, group)
    }
    if (byActor.size === 0) byActor.set(applicability ? 'applicability' : 'unknown', { actors: [], actorState: 'Unknown', inputs: [] })
    for (const [key, actor] of byActor) {
      const group = groups.get(key) ?? { ...actor, applicability, entries: [] }
      group.entries.push({ code, inputs: actor.inputs }); groups.set(key, group)
    }
  }
  return <section data-readiness-next-steps className="space-y-3" aria-label={label('nextSteps')}>
    {groups.size === 0 ? <p className="text-sm text-muted-foreground">{label('noPending')}</p> : [...groups].map(([key, group]) => <section key={key} className="space-y-2 rounded-md border p-3" data-readiness-actor-group={key}>
      <h4 className="flex flex-wrap gap-x-2 text-sm font-semibold">{group.applicability ? label('eligibleRole') : <RecordContextActors actors={group.actors} actorState={group.actorState} meId={meId} focusKey={`${focusKey}:group:${key}:next`} />}<span className="font-normal text-muted-foreground">· {discipline}</span></h4>
      {group.applicability && <ApplicabilityActors presentation={p} meId={meId} focusKey={`${focusKey}:applicability`} />}
      <ul className="space-y-3">{group.entries.map(({ code, inputs }) => {
        const check = p.checks.find(c => c.code === code)
        return <li key={code} data-readiness-next-check={code} className="space-y-1 text-sm">
          <p className="font-medium">{tv(code)} · {label('aggregateCheck')}: {check?.unknown ? t('readiness.unassessed') : t('readiness.unsatisfied')}</p>
          {check?.reason && <p className="whitespace-pre-wrap">{check.reason}</p>}
          {inputs.length === 0 && <p className="text-xs text-muted-foreground">{label('sourceLess')}</p>}
          {inputs.map(input => <div key={`${input.source.kind}:${input.source.id}`} className="space-y-1" data-readiness-source={`${input.source.kind}:${input.source.id}`}>
            {input.contextAvailable ? <SourceAction source={input.source} number={number}>{input.source.nextAction === 'none' || input.source.nextAction === 'not_assessed' ? label('openSource') : recordContextActionLabel(input.source.nextAction)} · <span className="key">{input.source.key}</span> · {input.source.title}</SourceAction>
              : <p className="text-warn">{label('sourceUnavailable')}</p>}
            <p className="text-xs text-muted-foreground">{tv(input.status)}{input.relationship === 'DirectTarget' && ` · ${label('directTarget')}`}{input.source.neededBy && ` · ${fmtDate(input.source.neededBy)}`}</p>
            {input.source.kind === 'WorkConstraint' && <ConstraintOwners presentation={p} id={input.source.id} meId={meId} focusKey={`${focusKey}:check:${code}:constraint:${input.source.id}`} />}
            {input.relationship === 'Constraint' && <div className="border-l-2 pl-3 text-xs text-muted-foreground"><p>{label('linkedReadiness')}</p>
              {input.linkedSource && input.linkedAvailable ? <><SourceAction source={input.linkedSource} number={number}><span className="key">{input.linkedSource.key}</span> · {input.linkedSource.title}</SourceAction><p>{input.linkedStatus && tv(input.linkedStatus)}</p></> : !input.linkedAvailable ? <p>{label('sourceUnavailable')}</p> : null}
            </div>}
          </div>)}
          {check && <Button size="sm" variant="outline" onClick={() => inspect(code)}>{group.applicability ? label('inspectApplicability') : label('inspectCheck')} · {tv(code)}</Button>}
        </li>
      })}</ul>
    </section>)}
  </section>
}

function ConstraintOwners({ presentation, id, meId, focusKey }: { presentation: Presentation; id: string; meId: string; focusKey: string }) {
  const owners = presentation.constraintOwners?.find(c => c.id === id)
  if (!owners) return <p className="text-xs text-muted-foreground">{label('constraintOwnersUnavailable')}</p>
  const person = (actor: Schemas['RecordContextPerson'], role: 'removal' | 'affected') => <><RecordContextActors actors={[actor]} actorState={actor.state} meId={meId} focusKey={`${focusKey}:${role}-owner`} />{actor.state === 'Removed' ? ` · ${label('removedActor')}` : actor.state === 'Unavailable' ? ` · ${label('unavailableActor')}` : ''}</>
  return <dl className="grid gap-1 text-xs text-muted-foreground sm:grid-cols-2" data-readiness-constraint-owners={id}>
    <div><dt>{t('readiness.removalOwner')}</dt><dd>{person(owners.removalOwner, 'removal')}</dd></div>
    <div><dt>{t('readiness.affectedOwner')}</dt><dd>{person(owners.affectedOwner, 'affected')}</dd></div>
  </dl>
}

function ApplicabilityActors({ presentation: p, meId, focusKey }: { presentation: Presentation; meId: string; focusKey: string }) {
  const group = p.applicability
  if (!group || !Array.isArray(group.candidates)) return <p className="text-sm text-warn">{label('unknownActors')}</p>
  const actors = group.candidates.filter(c => c.allowed).map(c => c.person)
  return <div className="space-y-1 text-xs" data-readiness-applicability={group.state}>
    <p>{actors.length > 0 ? <RecordContextActors actors={actors} actorState={group.state === 'Multiple' ? 'Multiple' : 'Active'} meId={meId} focusKey={`${focusKey}:${group.role}:candidates`} /> : label(group.state === 'Refused' ? 'refusedActors' : 'unknownActors')}</p>
    {group.candidates.filter(c => !c.allowed).map(c => <p key={c.person.id ?? c.person.state} className="text-muted-foreground"><RecordContextActors actors={[c.person]} actorState={c.person.state} meId={meId} focusKey={`${focusKey}:candidate:${c.person.kind}:${c.person.id ?? c.person.state}:refused`} /> · {tv(c.person.state)} · {c.refusal ?? label('refusedActors')}</p>)}
  </div>
}

export function orderedReadinessChecks(checks: Check[], p?: Presentation | null) {
  const facts = new Map((p?.checks ?? []).map(c => [c.code, c]))
  const order = (code: string) => facts.get(code)?.blocking ? 0 : facts.get(code)?.unknown ? 1 : facts.get(code)?.applies === false ? 3 : 2
  return [...checks].sort((a, b) => order(a.code) - order(b.code) || a.code.localeCompare(b.code))
}

export function ReadinessWorkList({ work, options, number, meId, inspect }: {
  work?: Schemas['ReadinessWorkPage'] | null; options?: CoordOptions; number: string; meId: string; inspect: (target: string, code?: string) => void
}) {
  if (!work || !Array.isArray(work.items)) return <p className="p-5 text-sm text-warn">{label('listUnavailable')}</p>
  if (work.items.length === 0) return <p className="p-5 text-sm text-muted-foreground">{label('noWork')}</p>
  return <ul className="divide-y">{work.items.map((row, index) => {
    const discipline = options?.disciplines.find(d => d.id === row.disciplineId)?.name ?? t('recordContext.unknown')
    const target = `${row.targetType}:${row.targetId}`
    return <Fragment key={target}>
      {(index === 0 || work.items[index - 1].state !== row.state) && <li className="bg-muted/40 px-5 py-2"><h3 className="text-sm font-semibold">{tv(row.state)}</h3></li>}
      <li data-readiness-work={target} data-readiness-state={row.state} className="space-y-3 px-5 py-4">
        <div className="flex flex-wrap items-center justify-between gap-2"><h4 className="min-w-0 break-words text-sm font-semibold"><span className="key font-normal">{row.key}</span> · {row.name}</h4><CoordStatus status={row.state} /></div>
        <p className="flex flex-wrap gap-x-3 text-xs text-muted-foreground"><span>{tv(row.targetType)} · {discipline}</span><span>{row.dueDate ? fmtDate(row.dueDate) : t('readiness.noDueDate')}</span><span>{t('readiness.performer')}: {options && row.ownerId ? <PersonLabel options={options} id={row.ownerId} focusKey={`readiness-work:${row.targetType}:${row.targetId}:performer`} /> : t('recordContext.unknown')}</span></p>
        {!row.hasAssessment && <p className="text-sm text-warn">{t('readiness.noAssessment')}</p>}
        <ReadinessNextSteps presentation={row.presentation} number={number} discipline={discipline} meId={meId} inspect={code => inspect(target, code)} focusKey={`readiness-work:${target}`} />
        <Button size="sm" variant="outline" onClick={() => inspect(target)}>{t('readiness.inspect')} · <span className="key">{row.key}</span></Button>
      </li>
    </Fragment>
  })}</ul>
}

export const readinessDeliveryLabel = label
export const readinessReadTime = (value?: string) => value ? fmtTime(value) : t('recordContext.unknown')
