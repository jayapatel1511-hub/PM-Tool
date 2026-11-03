import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Empty, ErrorBanner, Loading, Section } from './common'
import { Button } from '@/components/ui/button'
import { typedRequest } from '@/lib/api'
import type { Offer } from '@/lib/auth'
import { fmtDate, hours } from '@/lib/format'
import { t } from '@/lib/i18n'

/** Separate canonical role records, never person rows or an input to a capacity figure. */
export function TeamOpenRoles({ projectId, number, fill }: { projectId: string; number: string; fill: Offer }) {
  const q = useQuery({ queryKey: ['allocations', projectId, 'open-roles'], queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/open-roles', { params: { projectId } }) })
  const roles = q.data?.filter(role => role.status === 'Open') ?? []
  return <Section title={t('teamOrganisation.rolesTitle')} count={q.data ? roles.length : undefined}>
    <p className="px-5 py-3 text-xs text-muted-foreground">{t('teamOrganisation.rolesBasis')}</p>
    {q.isPending ? <Loading rows={2} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : roles.length === 0
      ? <Empty>{t('teamOrganisation.rolesEmpty')}</Empty> : <ul className="divide-y">{roles.map(role => <li key={role.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-3" data-team-open-role={role.id}>
        <div className="min-w-0 text-sm"><p className="break-words font-semibold">{role.roleLabel}</p><p className="break-words text-xs text-muted-foreground">{role.disciplineName ?? t('teamOrganisation.notSet')} · {fmtDate(role.fromDate)} – {fmtDate(role.throughDate)} · {hours(role.plannedHours)}</p></div>
        {fill.shown && (fill.props.disabled ? <Button size="sm" variant="outline" {...fill.props} aria-label={t('openRole.fillLabel', { role: role.roleLabel })}>{t('openRole.fill')}</Button>
          : <Button size="sm" variant="outline" asChild><Link aria-label={t('openRole.fillLabel', { role: role.roleLabel })} to={`/projects/${number}/allocations?fill=${role.id}`}>{t('openRole.fill')}</Link></Button>)}
      </li>)}</ul>}
  </Section>
}
