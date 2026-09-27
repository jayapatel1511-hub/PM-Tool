import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { Avatar } from '@/components/hub/people'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { plural, t } from '@/lib/i18n'

interface Member {
  userId: string; displayName: string; jobTitle?: string; email?: string
  projects: { projectId: string; projectNumber: string; projectName: string; roles: string[]; leads: string[] }[]
}

/** Team (§36.1, FR-VIS-01): everyone active on the selected projects with their roles there, linking to each project's Team
 *  screen. The same people make up Home's Team Members figure. */
export function WorkspaceTeamPage() {
  const scope = useScope()
  const [term, setTerm] = useState('')
  const q = useQuery({ queryKey: ['wteam', scope.api], enabled: scope.ready, queryFn: () => get<{ people: Member[]; projects: number }>(`team${qs({ projects: scope.api })}`) })
  const s = term.trim().toLowerCase()
  const people = (q.data?.people ?? []).filter((p) => !s || p.displayName.toLowerCase().includes(s) || (p.jobTitle ?? '').toLowerCase().includes(s))
  return (
    <Page title={t('nav.team')} subtitle={q.data ? plural(q.data.people.length, 'team.onePerson', 'team.nPeople', { n: q.data.people.length }) : scope.label}>
      <WorkspaceTabs />
      <Input type="search" className="h-8 w-64" placeholder={t('team.search')} aria-label={t('common.search')} value={term} onChange={(e) => setTerm(e.target.value)} />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={6} /> : people.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('team.none')}</Empty></div> : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40 text-xs text-muted-foreground">
              <tr><th scope="col" className="px-3 py-2 text-left font-medium">{t('team.person')}</th><th scope="col" className="px-3 py-2 text-left font-medium">{t('team.rolesByProject')}</th></tr>
            </thead>
            <tbody className="divide-y">
              {people.map((p) => (
                <tr key={p.userId} className="align-top">
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-2"><Avatar name={p.displayName} /><div><div className="font-medium">{p.displayName}</div>
                      {p.jobTitle && <div className="text-xs text-muted-foreground">{p.jobTitle}</div>}</div></div>
                  </td>
                  <td className="px-3 py-2">
                    <ul className="space-y-1">
                      {p.projects.map((m) => (
                        <li key={m.projectId} className="flex flex-wrap items-center gap-1.5">
                          <Link to={`/projects/${m.projectNumber}/team`} className="hover:underline" title={m.projectName}><span className="key font-mono text-xs">{m.projectNumber}</span> <span className="text-xs">{m.projectName}</span></Link>
                          {m.roles.map((r) => <span key={r} className="rounded bg-muted px-1.5 text-xs">{t(`role.${r}`)}</span>)}
                          {m.leads.map((l) => <span key={l} className="rounded bg-muted px-1.5 text-xs">{t('team.leadOf', { name: l })}</span>)}
                        </li>
                      ))}
                    </ul>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Page>
  )
}
