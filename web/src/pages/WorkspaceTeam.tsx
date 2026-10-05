import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { AccentDot, Empty, ErrorBanner, Field, FilterBar, Loading, Page, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { Avatar } from '@/components/hub/people'
import { Key } from '@/components/hub/pills'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { plural, t } from '@/lib/i18n'

interface Member {
  userId: string; displayName: string; jobTitle?: string; email?: string
  projects: { projectId: string; projectNumber: string; projectName: string; roles: string[]; leads: string[] }[]
}

const tag = 'rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium text-secondary-foreground'

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
      <FilterBar>
        <Field label={t('common.search')} htmlFor="team-search" className="w-full sm:w-72">
          <Input id="team-search" type="search" placeholder={t('team.search')} value={term} onChange={(e) => setTerm(e.target.value)} />
        </Field>
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={6} /></div> : people.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={s && <Button variant="outline" onClick={() => setTerm('')}>{t('common.clear')}</Button>}>{t('team.none')}</Empty></div>
      ) : (
        <TableRegion>
          <table className="w-full text-sm">
            <caption className="sr-only">{t('nav.team')}</caption>
            <thead className="bg-muted">
              <tr><th scope="col" className={thCls}>{t('team.person')}</th><th scope="col" className={thCls}>{t('team.rolesByProject')}</th></tr>
            </thead>
            <tbody>
              {people.map((p) => (
                <tr key={p.userId} className="border-t hover:bg-muted">
                  <td className={tdCls}>
                    <div className="flex min-w-48 items-center gap-2.5"><Avatar id={p.userId} name={p.displayName} />
                      <div className="min-w-0"><div className="break-words font-semibold">{p.displayName}</div>
                        {p.jobTitle && <div className="text-xs/[18px] text-muted-foreground">{p.jobTitle}</div>}</div></div>
                  </td>
                  <td className={tdCls}>
                    <ul className="space-y-1.5">
                      {p.projects.map((m) => (
                        <li key={m.projectId} className="flex flex-wrap items-center gap-x-2 gap-y-1">
                          <Link to={`/projects/${m.projectNumber}/team`} className="inline-flex min-w-0 items-center gap-2 hover:underline" title={m.projectName}>
                            <AccentDot id={m.projectId} /><Key>{m.projectNumber}</Key><span className="break-words">{m.projectName}</span>
                          </Link>
                          {m.roles.map((r) => <span key={r} className={tag}>{t(`role.${r}`)}</span>)}
                          {m.leads.map((l) => <span key={l} className={tag}>{t('team.leadOf', { name: l })}</span>)}
                        </li>
                      ))}
                    </ul>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableRegion>
      )}
    </Page>
  )
}
