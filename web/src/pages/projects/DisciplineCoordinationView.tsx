import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import type { ReactNode } from 'react'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'

type Handoff = { id: string; key: string; title: string; status: string; neededBy: string; promisedBy?: string; targetKey?: string }
type Change = { id: string; key: string; title: string; status: string; pendingAssessments: number }
type Review = { id: string; key: string; title: string; status: string; outstandingDisciplines: number; blockingFindings: number }
type InputUse = { id: string; targetType: string; targetId: string; sourceRevisionId: string }
type Page<T> = { items: T[]; totalCount?: number }
type Data = { handoffs: Handoff[]; changes: Change[]; reviews: Review[]; uses: InputUse[]; usesTotal: number; partial: boolean; fetchedAt: string }

/** Packet 030's five-question coordination projection over the existing registers. */
export function DisciplineCoordinationView({ project, disciplineId }: { project: ProjectDetail; disciplineId?: string }) {
  const q = useQuery({
    queryKey: ['p', project.id, 'discipline-coordination', disciplineId],
    queryFn: async (): Promise<Data> => {
      const scope = disciplineId ? { disciplineId } : {}
      const [outgoing, incoming, changes, reviews, uses] = await Promise.all([
        get<Page<Handoff>>(`projects/${project.id}/handoffs${qs({ ...scope, direction: 'outgoing', pageSize: 100 })}`),
        get<Page<Handoff>>(`projects/${project.id}/handoffs${qs({ ...scope, direction: 'incoming', pageSize: 100 })}`),
        get<Page<Change>>(`projects/${project.id}/changes${qs({ pageSize: 100 })}`),
        get<Page<Review>>(`projects/${project.id}/reviews${qs({ pageSize: 100 })}`),
        get<Page<InputUse>>(`projects/${project.id}/input-uses${qs({ pageSize: 100 })}`),
      ])
      const totals = [outgoing, incoming, changes, reviews, uses].map((x) => x.totalCount ?? x.items.length)
      return { handoffs: [...outgoing.items, ...incoming.items], changes: changes.items, reviews: reviews.items, uses: uses.items, usesTotal: totals[4],
        partial: [outgoing, incoming, changes, reviews, uses].some((x) => (x.totalCount ?? x.items.length) > x.items.length), fetchedAt: new Date().toISOString() }
    },
  })
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  const d = q.data
  const outgoing = d.handoffs.filter((h) => h.status !== 'Cancelled' && h.status !== 'Incorporated' && (h.promisedBy || h.status === 'Draft'))
  const incoming = d.handoffs.filter((h) => ['Submitted', 'Clarification Requested', 'Returned', 'Accepted'].includes(h.status))
  const openChanges = d.changes.filter((c) => c.status === 'Open' || c.pendingAssessments > 0)
  const openReviews = d.reviews.filter((r) => !['Approved', 'Cancelled', 'Superseded'].includes(r.status))
  const link = (path: string, label: string) => <Link className="text-xs font-medium text-primary underline" to={`/projects/${project.projectNumber}/${path}`}>{label}</Link>
  const card = (id: string, title: string, count: number | string, content: ReactNode, href: string) => (
    <section aria-labelledby={`dcv-${id}`} className="rounded-md border bg-card p-3">
      <div className="flex items-center justify-between gap-2"><h2 id={`dcv-${id}`} className="font-medium">{title}</h2><span className="rounded-full bg-muted px-2 text-sm tabular-nums">{count}</span></div>
      <div className="mt-2 text-sm">{content}</div><div className="mt-2">{link(href, t('dcv.openRegister'))}</div>
    </section>
  )
  const items = (rows: { id: string; key: string; text: string; detail?: string }[], register: string) => rows.length ? <ul className="space-y-1">{rows.slice(0, 4).map((r) => <li key={r.id}><Link className="underline" to={`/projects/${project.projectNumber}/${register}?q=${encodeURIComponent(r.key)}`}>{r.key}</Link> <span>{r.text}</span>{r.detail && <span className="text-muted-foreground"> · {r.detail}</span>}</li>)}</ul> : <p className="text-muted-foreground">{t('dcv.none')}</p>
  return <section aria-labelledby="dcv-title" className="rounded-lg border border-primary/20 bg-primary/5 p-4">
    <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2"><div><h2 id="dcv-title" className="text-lg font-semibold">{t('dcv.title')}</h2><p className="text-sm text-muted-foreground">{t('dcv.subtitle')}</p><p role="status" className="mt-1 text-xs text-muted-foreground">{t('dcv.scopeNote')}</p></div><span className="text-xs text-muted-foreground">{t('dcv.clientRefresh', { when: new Date(d.fetchedAt).toLocaleTimeString() })}</span></div>
    {d.partial && <p role="status" className="mb-3 rounded border border-warn/30 bg-warn-bg px-3 py-2 text-xs text-warn">{t('dcv.partial')}</p>}
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      {card('owe', t('dcv.owe'), outgoing.length, items(outgoing.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: `${tv(h.status)} · ${fmtDate(h.promisedBy ?? h.neededBy)}` })), 'handoffs'), 'handoffs?direction=outgoing')}
      {card('waiting', t('dcv.waiting'), incoming.length, items(incoming.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: tv(h.status) })), 'handoffs'), 'handoffs?direction=incoming')}
      {card('using', `${t('dcv.using')} · ${t('dcv.projectWide')}`, d.usesTotal, d.uses.length ? <p>{t('dcv.usingHint', { n: d.usesTotal })}</p> : <p className="text-muted-foreground">{t('dcv.none')}</p>, 'changes')}
      {card('changed', `${t('dcv.changed')} · ${t('dcv.projectWide')}`, openChanges.length, items(openChanges.map((c) => ({ id: c.id, key: c.key, text: c.title, detail: `${tv(c.status)} · ${c.pendingAssessments} ${t('dcv.assessments')}` })), 'changes'), 'changes?status=Open')}
      {card('start', `${t('dcv.start')} · ${t('dcv.projectWide')}`, '—', <p role="status">{t('dcv.startUnavailable', { n: openReviews.length })}</p>, 'reviews')}
    </div>
  </section>
}
