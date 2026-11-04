import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { ApiError, typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { t } from '@/lib/i18n'
import { ConfirmDialog, ErrorBanner, Field, Loading, selectCls } from './common'

/** FR-SCR-65 (053-D13): a milestone or deliverable date move records an organisation reason category beside its free-text reason. */
export function useDateReasons() {
  const on = useFlag('register_screens'), me = useMe()
  const q = useQuery({ queryKey: ['reference', 'date-reasons', me.id, on], enabled: on,
    queryFn: async () => (await typedRequest('get', '/api/v1/reference', {})).dateReasons ?? [] })
  const choices = (q.data ?? []).filter(choice => choice.isActive)
  // The server requires one while the organisation keeps an active category; an empty list never blocks a move.
  return { choices, all: q.data ?? [], required: on && choices.length > 0 }
}

export function DateReasonSelect({ id, value, onChange }: { id: string; value: string; onChange: (value: string) => void }) {
  const { choices, required } = useDateReasons()
  if (!required) return null
  return <Field label={t('dateReason.label')} htmlFor={id}>
    <select id={id} className={selectCls} required value={value} onChange={event => onChange(event.target.value)} data-date-reason>
      <option value="">{t('common.selectPlaceholder')}</option>
      {choices.map(choice => <option key={choice.id} value={choice.id}>{choice.name}</option>)}
    </select>
  </Field>
}

export const needsDateReason = (error: unknown) => error instanceof ApiError && !!error.fieldErrors?.dateReasonCategoryId

/** Inline edits (a panel date, a Timeline drag) learn from the server's refusal that a reason is needed, then repeat the same save with it. */
export function useDateReasonPrompt() {
  const [resolve, setResolve] = useState<((id: string | null) => void) | null>(null)
  const [value, setValue] = useState('')
  const ask = () => new Promise<string | null>(done => { setValue(''); setResolve(() => done) })
  const finish = (id: string | null) => { resolve?.(id); setResolve(null) }
  const dialog = resolve && <ConfirmDialog open title={t('dateReason.title')} body={t('dateReason.body')} busy={!value} confirmLabel={t('common.save')}
    onOpenChange={open => { if (!open) finish(null) }} onConfirm={() => finish(value)}>
    <DateReasonSelect id="date-reason-prompt" value={value} onChange={setValue} />
  </ConfirmDialog>
  return { ask, dialog }
}

export const isDateReasonTag = (category?: string | null) => !!category?.startsWith('dateReason:')

/** "Why dates moved" on the Milestone View: each count opens the project activity list filtered to its moves. */
export function DateReasonCounts({ projectId, base }: { projectId: string; base: string }) {
  const me = useMe()
  const q = useQuery({ queryKey: ['date-reasons', projectId, me.id], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/milestones/date-reasons', { params: { id: projectId } }) })
  if (q.isError) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  if (!q.data) return <Loading rows={1} />
  return <section className="space-y-2 rounded-md border p-3" aria-label={t('dateReason.summary')} data-date-reasons>
    <h2 className="text-sm font-semibold">{t('dateReason.summary')}</h2><p className="text-xs text-muted-foreground">{t('dateReason.scope')}</p>
    {q.data.moves === 0 ? <p className="text-sm text-muted-foreground">{t('dateReason.empty')}</p> : <ul className="flex flex-wrap gap-2 text-sm">
      {q.data.reasons.map(reason => <li key={reason.categoryId}><Link className="inline-flex min-h-9 items-center gap-2 rounded-md border px-3 tabular-nums hover:bg-secondary"
        to={`${base}/activity?${new URLSearchParams({ category: reason.activityCategory })}`} data-date-reason-count={reason.name}>{reason.name}<strong>{reason.count}</strong></Link></li>)}
      {q.data.notRecorded > 0 && <li className="inline-flex min-h-9 items-center px-1 text-muted-foreground tabular-nums">{t('dateReason.notRecorded', { count: q.data.notRecorded })}</li>}
    </ul>}
  </section>
}
