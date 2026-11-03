import type { components } from '@/gen/api'
import { itemHref } from '@/components/hub/search'
import { fmtDate } from '@/lib/format'
import { runtimeText, t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useFlag } from '@/lib/auth'
import { ArrowRight, CircleAlert, UserRound } from 'lucide-react'
import { useLocation, useNavigate } from 'react-router'
import { captureRecordContextOrigin, type RecordContextOrigin } from '@/lib/record-context-navigation'
import { Fragment } from 'react'
import { PersonPanelTrigger } from './person-panel-trigger'

type Context = components['schemas']['RecordContextView']
type Person = components['schemas']['RecordContextPerson']
type Input = components['schemas']['RecordContextInputView']

const actionKeys: Record<string, string> = { run_current_checks: 'runChecks', acknowledge_withdrawal: 'acknowledgeWithdrawal', adopt_current_revision: 'adoptRevision', verify_change_update: 'verifyChange', follow_linked_task: 'followLinkedTask', review_prerequisites: 'reviewPrerequisites', start: 'start', complete: 'complete', review: 'review', revise: 'revise', resolve_input: 'resolveInput', resume: 'resume', none: 'noNextAction', not_assessed: 'notAssessed', transition: 'transition', decide: 'decide', submit: 'submit', accept: 'accept', respond_and_resubmit: 'respond', incorporate: 'incorporate', remove: 'remove', verify_removal: 'verifyRemoval', assess_change: 'assessChange', complete_or_verify_follow_up: 'followUp', respond_to_comment: 'respondToComment', verify_closure: 'verifyClosure', start_checking: 'startChecking', resolve_check: 'resolveCheck', issue: 'issue', review_resolve_complete: 'reviewResolveComplete' }
const actionLabel = (value: string) => runtimeText(`recordContext.action.${actionKeys[value] ?? 'notAssessed'}`)
const stateLabel = (person: Person | undefined, meId: string) => {
  if (!person) return runtimeText('recordContext.unassigned')
  if (person.id === meId && person.state === 'Active' && person.kind === 'User') return runtimeText('recordContext.you')
  if (person.kind === 'ExternalParty') return `${person.name ?? runtimeText('recordContext.unknown')} (${person.state === 'Inactive' ? `${runtimeText('recordContext.inactive')} · ` : ''}${runtimeText('recordContext.noAccount')})`
  if (person.state === 'Inactive') return `${person.name ?? runtimeText('recordContext.unknown')} (${runtimeText('recordContext.inactive')})`
  if (person.state === 'External') return `${person.name ?? runtimeText('recordContext.external')} (${runtimeText('recordContext.noAccount')})`
  if (person.state === 'Unassigned') return runtimeText('recordContext.unassigned')
  return person.name ?? runtimeText('recordContext.unknown')
}

function ContextPerson({ person, meId, focusKey }: { person?: Person; meId: string; focusKey?: string }) {
  const label = stateLabel(person, meId)
  const location = useLocation()
  const panel = new URLSearchParams(location.search).get('panel')
  // Panel item identities survive a keyed detail remount; local React IDs do not.
  const sourceKey = panel ?? location.pathname
  return person?.kind === 'User' && person.id && person.name && (person.state === 'Active' || person.state === 'Inactive')
    ? <PersonPanelTrigger userId={person.id} name={person.name} active={person.state === 'Active'} focusKey={focusKey ? `${sourceKey}:${focusKey}` : undefined}>{label}</PersonPanelTrigger>
    : <>{label}</>
}

function ActorList({ actors, meId, actorState, focusKey }: { actors: Person[]; meId: string; actorState: string; focusKey?: string }) {
  if (actors.length === 0) {
    if (actorState === 'Unknown') return <span>{runtimeText('recordContext.unknown')}</span>
    if (actorState === 'NotAssessed') return <span>{runtimeText('recordContext.notAssessed')}</span>
    return <span>{runtimeText('recordContext.unassigned')}</span>
  }
  if (actors.length > 1) return <span>{runtimeText('recordContext.multiple')} ({actors.map((person, index) => <Fragment key={`${person.kind}:${person.id ?? index}`}>{index > 0 && ', '}<ContextPerson person={person} meId={meId} focusKey={focusKey ? `${focusKey}:${index}` : undefined} /></Fragment>)}){actorState === 'Partial' && <span> · {t('recordContext.otherActorsUnknown')}</span>}</span>
  return <span><ContextPerson person={actors[0]} meId={meId} focusKey={focusKey ? `${focusKey}:0` : undefined} />{actorState === 'Partial' && <span> · {t('recordContext.otherActorsUnknown')}</span>}</span>
}

export const recordContextActionLabel = actionLabel
export const RecordContextActors = ActorList

function InputLink({ input, projectNumber, openPanel }: { input: Input; projectNumber: string; openPanel: (type: string, id: string, origin?: RecordContextOrigin) => void }) {
  const navigate = useNavigate()
  const location = useLocation()
  const label = `${input.kind === 'ChangeAssessment' ? `${t('recordContext.assessment')} · ` : input.kind === 'SubmissionCheck' ? `${t('recordContext.submissionCheck')} · ` : ''}${input.key} · ${input.title}`
  if (input.kind === 'Task' || input.kind === 'Decision') return <button type="button" className="text-left text-primary underline underline-offset-2" onClick={e => openPanel(input.kind, input.id, captureRecordContextOrigin(location, e.currentTarget))}>{label}</button>
  const target = new URL(itemHref(input.kind, projectNumber, input.id), window.location.origin)
  if (target.pathname === location.pathname) {
    const params = new URLSearchParams(location.search)
    for (const [key, value] of target.searchParams) params.set(key, value)
    target.search = params.toString()
  }
  const href = `${target.pathname}${target.search}${target.hash}`
  return <a className="text-primary underline underline-offset-2" href={href} onClick={(e) => {
    if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
    e.preventDefault()
    navigate(href, { state: { recordContextReturn: captureRecordContextOrigin(location, e.currentTarget) } })
  }}>{label}</a>
}

export function RecordContext({ context, projectNumber, meId, openPanel, inheritedNoticeDate = false }: { context?: Context | null; projectNumber: string; meId: string; openPanel: (type: string, id: string, origin?: RecordContextOrigin) => void; inheritedNoticeDate?: boolean }) {
  const contextOn = useFlag('screen_patterns')
  if (!contextOn || !context) return null
  const actors = context.terminal ? [] : context.nextActors
  return <section aria-label={runtimeText('recordContext.title')} className="mt-3 rounded-lg border bg-muted/30 px-3 py-2 text-sm">
    <h3 className="sr-only">{runtimeText('recordContext.title')}</h3>
    {context.available === false && <p className="mb-2 text-xs text-muted-foreground">{t('recordContext.unavailable')}</p>}
    <dl className="grid gap-x-4 gap-y-1 sm:grid-cols-3">
      <div><dt className="text-xs text-muted-foreground">{runtimeText('recordContext.owner')}</dt><dd className="flex items-center gap-1 font-medium"><UserRound className="size-3.5" aria-hidden /><ContextPerson person={context.owner} meId={meId} focusKey="record-context:owner" /></dd></div>
      {!context.terminal && <div><dt className="text-xs text-muted-foreground">{runtimeText('recordContext.nextActor')}</dt><dd><ActorList actors={actors} meId={meId} actorState={context.actorState} focusKey="record-context:next" /></dd></div>}
      <div><dt className="text-xs text-muted-foreground">{runtimeText('recordContext.nextAction')}</dt><dd className="flex items-center gap-1 font-medium"><ArrowRight className="size-3.5" aria-hidden />{context.terminal ? runtimeText('recordContext.noNextAction') : actionLabel(context.nextAction)}</dd></div>
    </dl>
    <div className="mt-1 text-xs text-muted-foreground"><span className="font-medium">{runtimeText(inheritedNoticeDate ? 'recordContext.noticeNeededBy' : 'recordContext.neededBy')}</span>: {context.available === false ? runtimeText('recordContext.unknown') : context.neededBy ? fmtDate(context.neededBy) : runtimeText('recordContext.noDate')}</div>
    {!context.terminal && (context.requiresReviewGate || context.nextTransitions.length > 0) && <p className="mt-2 border-t pt-2 text-xs text-muted-foreground">
      {context.requiresReviewGate && <span>{t('recordContext.reviewRequired')}</span>}
      {context.nextTransitions.length > 0 && <span className={context.requiresReviewGate ? 'ml-2' : undefined}>{t('recordContext.workflowTransitions', { values: context.nextTransitions.map(tv).join(', ') })}</span>}
    </p>}
    {context.reviewPackageGate && <div className="mt-2 border-t pt-2 text-xs">
      <p className="font-medium">{t('recordContext.requiredPackage')}: {context.reviewPackageGate.key ?? t('common.unavailable')}{context.reviewPackageGate.title && ` · ${context.reviewPackageGate.title}`}</p>
      <p>{context.reviewPackageGate.satisfied == null ? t('recordContext.packageUnknown') : context.reviewPackageGate.satisfied ? t('recordContext.packagePass') : t('recordContext.packageFail')}{context.reviewPackageGate.reason && ` ${context.reviewPackageGate.reason}`}</p>
    </div>}
    {(context.unavailableInputCount ?? 0) > 0 && <p className="mt-2 border-t pt-2 text-xs text-muted-foreground">{t('recordContext.unavailableInputs', { n: context.unavailableInputCount ?? 0 })}</p>}
    {context.inputs.length > 0 && <div className="mt-2 border-t pt-2"><p className="text-xs font-medium text-muted-foreground">{runtimeText('recordContext.inputs')}</p><ul className="mt-1 space-y-1">{context.inputs.map(input => <li key={`${input.kind}:${input.id}`} className={cn('space-y-0.5', input.primary && 'font-medium')}><div className="flex flex-wrap items-center gap-1"><CircleAlert className="size-3.5 text-muted-foreground" aria-hidden /><InputLink input={input} projectNumber={projectNumber} openPanel={openPanel} /><span className="text-xs text-muted-foreground">· {actionLabel(input.nextAction)}</span></div><p className="pl-4 text-xs text-muted-foreground">{input.nextAction !== 'none' && <><span>{t('recordContext.nextActor')}: </span><ActorList actors={input.nextActors} meId={meId} actorState={input.actorState ?? (input.nextActors.length === 0 && input.nextAction === 'not_assessed' ? 'Unknown' : input.nextActors[0]?.state ?? 'Unassigned')} focusKey={`record-context:input:${input.kind}:${input.id}:next`} /> · </>}{t(input.kind === 'ChangeAssessment' ? 'recordContext.noticeNeededBy' : 'recordContext.neededBy')}: {input.neededBy ? fmtDate(input.neededBy) : t('recordContext.noDate')}</p></li>)}</ul></div>}
  </section>
}
