import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { AccentDot, Empty, ErrorBanner, Field, FilterBar, Page } from '@/components/hub/common'
import { Avatar } from '@/components/hub/people'
import { Key } from '@/components/hub/pills'
import { DataTable } from '@/components/hub/registers'
import { useTable } from '@/components/hub/table'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { plural, t , runtimeText } from '@/lib/i18n'

interface Member {
  userId: string; displayName: string; jobTitle?: string; email?: string
  projects: { projectId: string; projectNumber: string; projectName: string; roles: string[]; leads: string[] }[]
}

type Row = Member & { id: string }

const tag = 'rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium text-secondary-foreground'

/** Team (§36.1, FR-VIS-01): everyone active on the selected projects with their roles there, linking to each project's Team
 *  screen. The same people make up Home's Team Members figure. */
export function WorkspaceTeamPage() {
  const scope = useScope()
  const [term, setTerm] = useState('')
  const q = useQuery({ queryKey: ['wteam', scope.api], enabled: scope.ready, queryFn: () => get<{ people: Member[]; projects: number }>(`team${qs({ projects: scope.api })}`) })
  const s = term.trim().toLowerCase()
  const people: Row[] = (q.data?.people ?? []).map((p) => ({ ...p, id: p.userId })).filter((p) => !s || p.displayName.toLowerCase().includes(s) || (p.jobTitle ?? '').toLowerCase().includes(s))
  const table = useTable<Row>('hub.teamColumns', [
    { id: 'person', label: t('team.person'), fixed: true, className: 'align-top', cell: (p) => (
      <div className="flex min-w-48 items-center gap-2.5"><Avatar id={p.userId} name={p.displayName} />
        <div className="min-w-0"><div className="break-words font-semibold">{p.displayName}</div>
          {p.jobTitle && <div className="text-xs/[18px] text-muted-foreground">{p.jobTitle}</div>}</div></div>
    ) },
    { id: 'roles', label: t('team.rolesByProject'), fixed: true, className: 'min-w-80 align-top', cell: (p) => (
      <ul className="space-y-1.5">
        {p.projects.map((m) => (
          <li key={m.projectId} className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <Link to={`/projects/${m.projectNumber}/team`} className="inline-flex min-w-0 items-center gap-2 hover:underline" title={m.projectName}>
              <AccentDot id={m.projectId} /><span className="shrink-0 whitespace-nowrap"><Key>{m.projectNumber}</Key></span><span className="break-words">{m.projectName}</span>
            </Link>
            {m.roles.map((r) => <span key={r} className={tag}>{runtimeText(`role.${r}`)}</span>)}
            {m.leads.map((l) => <span key={l} className={tag}>{t('team.leadOf', { name: l })}</span>)}
          </li>
        ))}
      </ul>
    ) },
  ], people, (r) => [r.displayName])
  return (
    <Page title={t('nav.team')} subtitle={q.data ? plural(q.data.people.length, 'team.onePerson', 'team.nPeople', { n: q.data.people.length }) : scope.label}>
      <WorkspaceTabs />
      <FilterBar search={<Field label={t('common.search')} htmlFor="team-search">
        <Input id="team-search" type="search" placeholder={t('team.search')} value={term} onChange={(e) => setTerm(e.target.value)} />
      </Field>} />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <DataTable table={table} rows={table.sorted} loading={q.isPending} wide caption={t('nav.team')} current={() => false}
        empty={<Empty action={s && <Button variant="outline" onClick={() => setTerm('')}>{t('common.clear')}</Button>}>{t('team.none')}</Empty>} />
    </Page>
  )
}
