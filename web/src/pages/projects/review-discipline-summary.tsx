import { useQuery } from '@tanstack/react-query'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { useFlag, useMe } from '@/lib/auth'
import { typedRequest } from '@/lib/api'
import { t } from '@/lib/i18n'

export function ReviewDisciplineSummary({ projectId }: { projectId: string }) {
  const me = useMe(), on = useFlag('register_screens')
  const query = useQuery({ queryKey: ['review-discipline-summary', projectId, me.id, on], enabled: on,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/reviews/discipline-summary', { params: { projectId } }) })
  if (!on) return null
  if (query.isError) return <ErrorBanner error={query.error} retry={() => query.refetch()} />
  if (!query.data) return <Loading />
  const metric = (value: number | null | undefined) => value == null ? <span aria-label={t('reviewDiscipline.noSamples')}>—</span> : value.toLocaleString(undefined, { maximumFractionDigits: 1 })
  return <section className="min-w-0 space-y-3 rounded-lg border p-4" aria-label={t('reviewDiscipline.title')}>
    <h2 className="text-base font-semibold">{t('reviewDiscipline.title')}</h2>
    <p className="text-sm text-muted-foreground">{t('reviewDiscipline.scope')}</p>
    <p className="text-xs text-muted-foreground">{t('reviewDiscipline.timingHint')}</p>
    {!query.data.disciplines.length ? <p>{t('reviewDiscipline.empty')}</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm">
      <thead><tr>{[t('common.discipline'), t('reviewDiscipline.roundMedian'), t('reviewDiscipline.roundMax'), t('reviewDiscipline.packages'), t('reviewDiscipline.nonApplicable'), t('reviewDiscipline.timing'), t('reviewDiscipline.timed')].map(label => <th key={label} className="p-2 font-medium">{label}</th>)}</tr></thead>
      <tbody>{query.data.disciplines.map(row => <tr key={row.projectDisciplineId} className="border-t">
        <th scope="row" className="p-2 font-medium">{row.name || '—'}{row.state === 'Inactive' && ` · ${t('reviewDiscipline.retired')}`}
          {(row.unavailablePackageCount > 0 || row.unavailableTimingAssignmentRoundCount > 0) && <span className="block text-xs text-warn">{t('reviewDiscipline.partial')}</span>}</th>
        <td className="p-2 tabular-nums">{metric(row.medianRoundsToApproval)}</td><td className="p-2 tabular-nums">{metric(row.maximumRoundsToApproval)}</td>
        <td className="p-2 tabular-nums">{row.packageSampleCount} / {row.eligiblePackageCount}</td><td className="p-2 tabular-nums">{row.nonApplicablePackageCount}</td>
        <td className="p-2 tabular-nums">{metric(row.medianDaysPerRound)}</td><td className="p-2 tabular-nums">{row.timedAssignmentRoundCount} / {row.startedAssignmentRoundCount}</td>
      </tr>)}</tbody>
    </table></div>}
  </section>
}
