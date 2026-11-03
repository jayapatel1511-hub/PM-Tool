import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import { useItemPanel } from '@/components/hub/panel-host'
import { itemHref } from '@/components/hub/search'
import { Key } from '@/components/hub/pills'
import { RecordContextActors, recordContextActionLabel } from '@/components/hub/record-context'
import type { components } from '@/gen/api'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { fmtDate, fmtTime } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { useWorkRecordOrigin } from './work-record-navigation'

type Input = components['schemas']['RecordContextInputView']
type Effect = components['schemas']['WorkInputEffect']
type SourceUse = components['schemas']['WorkInputSourceUse']
type Context = components['schemas']['RecordContextView']
type Readiness = components['schemas']['WorkReadinessInputs']
export type ReadinessInput = components['schemas']['WorkReadinessInput']

export function readinessCheckLabel(input: ReadinessInput) {
  return t(input.checkApplies === false ? 'readiness.notApplicable' : input.checkApplies == null || input.checkSatisfied == null
    ? 'readiness.unassessed' : input.checkSatisfied ? 'readiness.satisfied' : 'readiness.unsatisfied')
}

/** An input check describes readiness. It never relabels the task's Waiting/Blocked state. */
export function WorkReadinessFacts({ readiness, number, meId }: { readiness: Readiness; number: string; meId: string }) {
  if (!readiness.inputs.length && !readiness.hasAssessment && readiness.available) return null
  return <section aria-label={t('workInputs.readiness')} className="mx-4 mb-4 space-y-3 rounded-lg border px-4 py-3 text-sm">
    <h3 className="font-medium">{t('workInputs.readiness')}</h3>
    <p>{tv(readiness.state)}{!readiness.hasAssessment && <> · {t('workInputs.noReadinessAssessment')}</>}</p>
    {!readiness.available && <p className="text-muted-foreground">{t('workInputs.readinessUnavailable')}</p>}
    <ul className="divide-y">{readiness.inputs.map(input => <li key={`${input.code}:${input.source.kind}:${input.source.id}`} className="space-y-2 py-3 first:pt-0 last:pb-0">
      <p className="text-xs text-muted-foreground">{t(input.relationship === 'DirectTarget' ? 'workInputs.directTarget' : input.relationship === 'Constraint' ? 'workInputs.viaConstraint' : 'workInputs.readinessSource')}</p>
      <WorkInputFact input={input.source} number={number} meId={meId} focusKey={`readiness-input:${input.code}:${input.source.kind}:${input.source.id}:source`} />
      <p className="text-xs">{tv(input.status)} · {tv(input.code)}: {readinessCheckLabel(input)}</p>
      {!input.contextAvailable && <p className="text-xs text-muted-foreground">{t('workInputs.readinessUnavailable')}</p>}
      {input.linkedSource && <div className="space-y-2 border-l pl-3"><p className="text-xs text-muted-foreground">{t('workInputs.linkedSource')}</p>
        <WorkInputFact input={input.linkedSource} number={number} meId={meId} focusKey={`readiness-input:${input.code}:${input.source.kind}:${input.source.id}:linked:${input.linkedSource.kind}:${input.linkedSource.id}`} />
        {input.linkedStatus && <p className="text-xs text-muted-foreground">{tv(input.linkedStatus)}</p>}
        <p className="text-xs text-muted-foreground">{t('workInputs.constraintPersists')}</p>
      </div>}
      {!input.linkedAvailable && <p className="text-xs text-muted-foreground">{t('workInputs.linkedUnavailable')}</p>}
    </li>)}</ul>
  </section>
}

function InputRecord({ kind, id, number, children }: { kind: string; id: string; number: string; children: ReactNode }) {
  const open = useItemPanel(), location = useLocation(), origin = useWorkRecordOrigin()
  const cls = 'text-left text-primary underline underline-offset-2'
  return kind === 'Task' || kind === 'Decision'
    ? <button type="button" className={cls} onClick={event => open(kind, id, captureRecordContextOrigin(location, event.currentTarget))}>{children}</button>
    : <Link className={cls} to={itemHref(kind, number, id)} onClick={origin}>{children}</Link>
}

/** Presentation of canonical next-actor/action facts, with explicit stored downstream effects. */
export function WorkInputFact({ input, number, meId, otherTasks, focusKey }: { input: Input; number: string; meId: string; otherTasks?: Effect['otherOpenTasks']; focusKey?: string }) {
  const actorState = input.actorState ?? (input.nextActors.length === 0 && input.nextAction === 'not_assessed' ? 'Unknown' : input.nextActors[0]?.state ?? 'Unassigned')
  return <div className="space-y-2 text-sm text-foreground">
    <InputRecord kind={input.kind} id={input.id} number={number}><Key>{input.key}</Key> · {input.title}</InputRecord>
    <dl className="grid gap-x-4 gap-y-1 sm:grid-cols-3">
      {input.nextAction !== 'none' && <div><dt className="text-xs text-muted-foreground">{t('workInputs.resolver')}</dt><dd><RecordContextActors actors={input.nextActors} actorState={actorState} meId={meId} focusKey={`${focusKey ?? `work-input:${input.kind}:${input.id}`}:resolver`} /></dd></div>}
      <div><dt className="text-xs text-muted-foreground">{t('recordContext.neededBy')}</dt><dd>{input.neededBy ? fmtDate(input.neededBy) : t('recordContext.noDate')}</dd></div>
      <div><dt className="text-xs text-muted-foreground">{t('recordContext.nextAction')}</dt><dd>{recordContextActionLabel(input.nextAction)}</dd></div>
    </dl>
    {otherTasks && <details className="text-xs text-muted-foreground"><summary className="cursor-pointer">{t(otherTasks.length === 1 ? 'workInputs.alsoOne' : 'workInputs.alsoMany', { n: otherTasks.length })}</summary>
      {otherTasks.length > 0 && <ul aria-label={t('workInputs.otherTasks')} className="mt-2 space-y-1">{otherTasks.map(task => <li key={task.id}><InputRecord kind="Task" id={task.id} number={number}><Key>{task.key}</Key> · {task.name}</InputRecord></li>)}</ul>}
    </details>}
  </div>
}

export function WorkWaitingInputs({ context, effects, manualType, manualReason, number, meId, children }: { context: Context; effects: Effect[]; manualType?: string | null; manualReason?: string | null; number: string; meId: string; children?: ReactNode }) {
  const byInput = new Map(effects.map(effect => [`${effect.kind}:${effect.id}`, effect.otherOpenTasks]))
  const unavailable = Math.max(0, (context.unavailableInputCount ?? 0) - (manualType ? 1 : 0))
  return <section aria-label={t('workInputs.title')} className="mx-4 mb-4 space-y-3 rounded-lg border px-4 py-3 text-sm">
    <h3 className="font-medium">{t('workInputs.title')}</h3>
    <ul className="divide-y">{context.inputs.map(input => <li key={`${input.kind}:${input.id}`} className="py-3 first:pt-0 last:pb-0"><WorkInputFact input={input} number={number} meId={meId} otherTasks={byInput.get(`${input.kind}:${input.id}`)} focusKey={`waiting-input:${input.kind}:${input.id}`} /></li>)}
      {manualType && <li className="space-y-1 py-3 last:pb-0"><p>{t('task.manualBlocker', { type: tv(manualType), reason: manualReason ?? '' })}</p>
        <p className="text-xs text-muted-foreground">{t('workInputs.resolver')}: {t('recordContext.unknown')} · {t('recordContext.neededBy')}: {t('recordContext.unknown')} · {t('recordContext.nextAction')}: {recordContextActionLabel('not_assessed')}</p>
      </li>}
    </ul>
    {unavailable > 0 && <p className="text-xs text-muted-foreground">{t('recordContext.unavailableInputs', { n: unavailable })}</p>}
    {context.inputs.length === 0 && !manualType && unavailable === 0 && <p className="text-xs text-muted-foreground">{t('workInputs.unavailable')}</p>}
    {children}
  </section>
}

export function WorkSourceUses({ sources, number }: { sources: SourceUse[]; number: string }) {
  const origin = useWorkRecordOrigin()
  return <section aria-label={t('workInputs.sourceUses')} className="mx-4 mb-4 space-y-3 rounded-lg border px-4 py-3 text-sm">
    <h3 className="font-medium">{t('workInputs.sourceUses')}</h3>
    <ul className="divide-y">{sources.map(use => <li key={use.id} className="space-y-2 py-3 first:pt-0 last:pb-0">
      <p className="font-medium"><Key>{use.revisionUsed?.key ?? use.currentRegisteredRevision?.key ?? t('common.unavailable')}</Key> · {use.revisionUsed?.title ?? use.currentRegisteredRevision?.title ?? t('workInputs.unavailable')}</p>
      <p>{use.intendedUse}</p>
      <dl className="grid gap-x-4 gap-y-1 sm:grid-cols-2">
        <div><dt className="text-xs text-muted-foreground">{t('workInputs.used')}</dt><dd className="font-mono">{use.revisionUsed?.revision ?? t('recordContext.unknown')}</dd></div>
        <div><dt className="text-xs text-muted-foreground">{t('workInputs.current')}</dt><dd className="font-mono">{use.currentRegisteredRevision?.revision ?? t('recordContext.unknown')}</dd></div>
      </dl>
      <p className="font-mono" aria-label={`${t('workInputs.used')} → ${t('workInputs.current')}`}>{use.revisionUsed?.revision ?? t('recordContext.unknown')} → {use.currentRegisteredRevision?.revision ?? t('recordContext.unknown')}</p>
      <div><p className="text-xs text-muted-foreground">{t('workInputs.assessment')}</p>
        {!use.assessmentAvailable ? <p>{t('workInputs.assessmentUnavailable')}</p> : use.assessments.length === 0 ? <p>{t('workInputs.noAssessment')}</p>
          : <ul className="space-y-1">{use.assessments.map(assessment => <li key={assessment.id}><Link className="text-primary underline underline-offset-2" onClick={origin} to={itemHref('ChangeAssessment', number, assessment.id)}><Key>{assessment.key}</Key> · {tv(assessment.status)}</Link> · {tv(assessment.noticeStatus)}</li>)}</ul>}
      </div>
      <dl className="space-y-1 text-xs text-muted-foreground">{[use.revisionUsed, use.currentRegisteredRevision].map((revision, index) => <div key={index}>
        <dt>{t(index ? 'workInputs.current' : 'workInputs.used')}: {revision?.revision ?? t('recordContext.unknown')}</dt>
        <dd>{t('workInputs.registered')}: {revision ? fmtTime(revision.registeredAt) : t('recordContext.unknown')} · {t('workInputs.checked')}: {revision?.sourceCheckedAt ? fmtTime(revision.sourceCheckedAt) : t('recordContext.unknown')}</dd>
      </div>)}</dl>
    </li>)}</ul>
  </section>
}
