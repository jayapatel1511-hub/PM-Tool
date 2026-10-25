import { useQuery } from '@tanstack/react-query'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { Empty, ErrorBanner, Loading, Page, Section } from '@/components/hub/common'
import { Avatar } from '@/components/hub/people'
import { get, typedRequest } from '@/lib/api'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

type Reference = { disciplines?: { id: string; name: string }[]; offices?: { id: string; name: string }[] }

export function OrganisationPage() {
  const q = useQuery({ queryKey: ['organisation'], queryFn: () => typedRequest('get', '/api/v1/organisation', {}) })
  const ref = useQuery({ queryKey: ['reference'], queryFn: () => get<Reference>('reference') })
  const [open, setOpen] = useState<Set<string>>(new Set())
  if (q.isPending) return <Page title={t('organisation.title')}><Loading rows={8} /></Page>
  if (q.error) return <Page title={t('organisation.title')}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const d = q.data
  const children = (parentId?: string | null) => d.units.filter((u) => (u.parentId ?? null) === (parentId ?? null)).sort((a, b) => a.name.localeCompare(b.name))
  const branch = (parentId?: string | null, depth = 0): React.ReactNode => children(parentId).map((u) => {
    const people = d.people.filter((p) => p.unitId === u.id)
    const expanded = open.has(u.id)
    return <div key={u.id} className={cn('border-l pl-3', depth > 0 && 'ml-3')}>
      <button className="flex w-full items-center gap-2 rounded-md px-2 py-2 text-left hover:bg-muted" onClick={() => setOpen((s) => { const n = new Set(s); n.has(u.id) ? n.delete(u.id) : n.add(u.id); return n })} aria-expanded={expanded}>
        {expanded ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}<span className="font-semibold">{u.name}</span><span className="text-xs text-muted-foreground">{u.level ?? t('organisation.unit')} · {u.headCount} {t('organisation.peopleInSubtree')}</span>
      </button>
      {expanded && <div className="ml-5 border-t py-2 text-sm"><div className="mb-2 grid gap-1 text-muted-foreground sm:grid-cols-2"><span>{t('organisation.leader')}: {u.leader ?? '—'}</span><span>{t('organisation.discipline')}: {ref.data?.disciplines?.find((x) => x.id === u.disciplineId)?.name ?? '—'}</span><span>{t('organisation.office')}: {ref.data?.offices?.find((x) => x.id === u.officeId)?.name ?? '—'}</span><span>{t('organisation.peopleInUnit')}: {people.length}</span></div>{people.length > 0 ? <ul className="space-y-1">{people.map((p) => <li key={p.id} className="flex items-center gap-2"><Avatar id={p.id} name={p.displayName} />{p.displayName}</li>)}</ul> : <span className="text-muted-foreground">{t('organisation.noPeople')}</span>}</div>}
      {branch(u.id, depth + 1)}
    </div>
  })
  return <Page title={t('organisation.title')} subtitle={t('organisation.subtitle')}><Section title={t('organisation.structure')}><div className="rounded-lg border bg-card p-3">{branch()}</div><div className="mt-3 rounded-lg border bg-card p-3">{d.people.filter((p) => !p.unitId).length > 0 ? <><h3 className="font-semibold">{t('organisation.noUnit')}</h3><ul className="mt-2 space-y-1">{d.people.filter((p) => !p.unitId).map((p) => <li key={p.id}>{p.displayName}</li>)}</ul></> : <Empty>{t('organisation.noUnplaced')}</Empty>}</div></Section></Page>
}
