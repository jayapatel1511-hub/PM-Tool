import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCheck, Settings } from 'lucide-react'
import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ChangeList, type ActivityRow } from '@/components/hub/activity'
import { AccentDot, ActiveFilters, ChipToggle, Empty, ErrorBanner, Field, Loading, Page, selectCls } from '@/components/hub/common'
import { TabBar } from '@/components/hub/fields'
import { NotificationItem, useMarkRead, useOpenNotification, useUnread, type NotificationRow } from '@/components/hub/notifications'
import { Avatar } from '@/components/hub/people'
import { Key } from '@/components/hub/pills'
import { itemHref } from '@/components/hub/search'
import { Button } from '@/components/ui/button'
import { get, post, qs } from '@/lib/api'
import { fmtDate, fmtTime, localIso, today, addDays } from '@/lib/format'
import { en } from '@/i18n/en'
import { t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'

// Every event with a label (the same `event.*` table Preferences uses), so new events appear in the filter automatically.
const EVENTS = Object.keys(en).filter((k) => k.startsWith('event.')).map((k) => k.slice(6))
  .sort((a, b) => t(`event.${a}`).localeCompare(t(`event.${b}`)))

/** "Today", "Yesterday" or the date, for grouping by day (§13.17). */
function dayLabel(ts: string) {
  const d = localIso(new Date(ts))
  return d === today() ? t('rel.todayCap') : d === addDays(today(), -1) ? t('rel.yesterdayCap') : fmtDate(d)
}

function byDay<T>(rows: T[], ts: (r: T) => string) {
  const m = new Map<string, T[]>()
  for (const r of rows) { const k = dayLabel(ts(r)); m.set(k, [...(m.get(k) ?? []), r]) }
  return [...m.entries()]
}

/** Notification Centre (§13.17): personal notifications and the Following feed. */
export function NotificationsPage() {
  const [sp, setSp] = useSearchParams()
  const tab = sp.get('tab') === 'following' ? 'following' : 'notifications'
  const unread = useUnread()
  return (
    <Page title={t('nav.notifications')} actions={<Button asChild variant="outline"><Link to="/preferences"><Settings className="size-4" />{t('top.preferences')}</Link></Button>}>
      <div className="overflow-hidden rounded-lg border bg-card">
        <TabBar tabs={[{ id: 'notifications', label: t('notif.personal'), count: unread.data?.notifications }, { id: 'following', label: t('notif.following'), count: unread.data?.following }]}
          value={tab} onChange={(v) => setSp(v === 'following' ? { tab: v } : {}, { replace: true })} />
        {tab === 'notifications' ? <Personal /> : <Following />}
      </div>
    </Page>
  )
}

function Personal() {
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = { unread: sp.get('unread') === 'true', type: sp.get('type') ?? undefined, projectId: sp.get('projectId') ?? undefined }
  const q = useInfiniteQuery({
    queryKey: ['notifications', 'list', filters],
    queryFn: ({ pageParam }) => get<PageOf<NotificationRow>>(`me/notifications${qs({ ...filters, page: pageParam, pageSize: 50 })}`),
    initialPageParam: 1, getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
  })
  const rows = useMemo(() => q.data?.pages.flatMap((p) => p.items) ?? [], [q.data])
  const projects = useMemo(() => [...new Map(rows.filter((r) => r.projectId).map((r) => [r.projectId!, r.projectNumber!])).entries()], [rows])
  const markRead = useMarkRead()
  const open = useOpenNotification()
  // Active filters as removable tokens with Clear (§13.0 Filters).
  const tokens = ([
    ['unread', t('notif.unreadOnly'), filters.unread && t('common.yes')],
    ['type', t('common.type'), filters.type && t(`event.${filters.type}`)],
    ['projectId', t('common.project'), projects.find(([id]) => id === filters.projectId)?.[1]],
  ] as const).filter(([k]) => sp.get(k))
  const clear = () => { const n = new URLSearchParams(sp); for (const [k] of tokens) n.delete(k); setSp(n, { replace: true }) }
  return (
    <div>
      <div className="flex flex-col gap-3 border-b px-4 py-4">
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.type')} htmlFor="notif-type" className="w-full sm:w-64">
            <select id="notif-type" className={selectCls} value={filters.type ?? ''} onChange={(e) => set('type', e.target.value || undefined)}>
              <option value="">{t('notif.anyType')}</option>{EVENTS.map((e) => <option key={e} value={e}>{t(`event.${e}`)}</option>)}
            </select>
          </Field>
          <Field label={t('common.project')} htmlFor="notif-project" className="w-full sm:w-44">
            <select id="notif-project" className={selectCls} value={filters.projectId ?? ''} onChange={(e) => set('projectId', e.target.value || undefined)}>
              <option value="">{t('notif.anyProject')}</option>{projects.map(([id, num]) => <option key={id} value={id}>{num}</option>)}
            </select>
          </Field>
          <div className="flex min-h-(--control-h) items-center">
            <ChipToggle on={filters.unread} onClick={() => set('unread', filters.unread ? undefined : 'true')}>{t('notif.unreadOnly')}</ChipToggle>
          </div>
          <div className="flex-1" />
          <Button variant="outline" onClick={() => markRead({ all: true })}><CheckCheck className="size-4" />{t('notif.markAll')}</Button>
        </div>
        <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value || t('common.dash') }))} onRemove={(k) => set(k)} onClear={clear} />
      </div>
      {q.error && <div className="border-b p-4"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>}
      {q.isPending ? <Loading rows={5} /> : rows.length === 0
        ? <Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{filters.unread ? t('notif.noneUnread') : t('notif.none')}</Empty> : (
        <div className="divide-y">
          {byDay(rows, (r) => r.updatedAt).map(([day, list]) => (
            <section key={day} aria-label={day}>
              <h2 className="border-b bg-muted px-4 py-2 text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">{day}</h2>
              <div className="divide-y">{list.map((n) => <NotificationItem key={n.id} n={n} onOpen={open} />)}</div>
            </section>
          ))}
          {q.hasNextPage && <div className="p-4 text-center"><Button variant="outline" size="sm" onClick={() => q.fetchNextPage()}>{t('notif.more')}</Button></div>}
        </div>
      )}
    </div>
  )
}

interface FeedItem { entry: ActivityRow & { projectId: string }; count: number; projectNumber: string; projectName: string; notified: boolean; unread: boolean }
interface Feed { projects: { projectId: string; projectNumber: string; name: string; status: string; unread: number }[]; items: FeedItem[]; totalCount: number }

const PROJECT_ITEM = 'flex min-h-(--control-row-h) w-full items-center gap-2 rounded-md px-2.5 py-1 text-left hover:bg-muted'
const PROJECT_ON = 'bg-accent font-semibold text-accent-foreground shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent'

/** Following feed (ASG-05): changes by others on projects followed at All activity, grouped by project then day. */
function Following() {
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const projectId = sp.get('projectId') ?? undefined
  const important = sp.get('important') === 'true'
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const q = useQuery({ queryKey: ['following', projectId, important], queryFn: () => get<Feed>(`me/following${qs({ projectId, importantOnly: important, pageSize: 200 })}`) })
  const markRead = async (id: string) => { await post(`me/following/${id}/read`); qc.invalidateQueries({ queryKey: ['following'] }); qc.invalidateQueries({ queryKey: ['unread'] }) }
  if (q.isPending) return <Loading rows={5} />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const { projects, items } = q.data
  const groups = new Map<string, FeedItem[]>()
  for (const i of items) groups.set(i.projectNumber, [...(groups.get(i.projectNumber) ?? []), i])
  return (
    <div className="grid md:grid-cols-[16rem_minmax(0,1fr)]">
      <aside className="space-y-4 border-b p-4 md:border-b-0 md:border-r" aria-label={t('notif.followedProjects')}>
        {projects.length === 0 ? <p className="text-sm text-muted-foreground">{t('notif.followNone')}</p> : (
          <ul className="space-y-1 text-sm">
            <li><button type="button" aria-pressed={!projectId} className={cn(PROJECT_ITEM, !projectId && PROJECT_ON)} onClick={() => set('projectId')}>{t('notif.allProjects')}</button></li>
            {projects.map((p) => (
              <li key={p.projectId} className="flex items-center gap-1">
                <button type="button" aria-pressed={projectId === p.projectId} className={cn(PROJECT_ITEM, 'min-w-0 flex-1', projectId === p.projectId && PROJECT_ON)} onClick={() => set('projectId', p.projectId)}>
                  <AccentDot id={p.projectId} />
                  <span className="min-w-0 flex-1 truncate"><Key>{p.projectNumber}</Key> {p.name}</span>
                  {p.unread > 0 && <span className="rounded-md bg-primary px-1.5 text-xs/[18px] font-semibold text-primary-foreground tabular-nums">{p.unread}<span className="sr-only"> {t('notif.unread')}</span></span>}
                </button>
                {p.unread > 0 && <Button variant="outline" size="icon-sm" aria-label={t('notif.markProjectRead', { number: p.projectNumber })} title={t('notif.markProjectRead', { number: p.projectNumber })}
                  onClick={() => markRead(p.projectId)}><CheckCheck className="size-4" /></Button>}
              </li>
            ))}
          </ul>
        )}
        <ChipToggle on={important} onClick={() => set('important', important ? undefined : 'true')}>{t('activity.important')}</ChipToggle>
      </aside>
      <div className="min-w-0">
        {items.length === 0 ? <Empty action={important && <Button variant="outline" onClick={() => set('important')}>{t('filters.clear')}</Button>}>{t('notif.feedEmpty')}</Empty> : (
          <div className="divide-y">
            {[...groups.entries()].map(([num, list]) => (
              <section key={num} aria-label={num}>
                <h2 className="flex items-center gap-2 border-b bg-muted px-4 py-2.5 text-sm font-semibold">
                  <AccentDot id={list[0].entry.projectId} /><Key>{num}</Key><span className="min-w-0 break-words">{list[0].projectName}</span>
                </h2>
                {byDay(list, (i) => i.entry.occurredAt).map(([day, rows]) => (
                  <div key={day} className="px-4 py-3">
                    <h3 className="mb-2 text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">{day}</h3>
                    <ol className="space-y-3">
                      {rows.map((i) => {
                        const e = i.entry
                        // Traceable back to the record (design §10), unless the record was deleted.
                        const item = <>{e.itemKey && <Key>{e.itemKey}</Key>} {e.itemName}</>
                        return (
                          <li key={e.id} className={cn('flex gap-3 border-l-[3px] pl-3 text-sm', i.unread ? 'border-primary' : 'border-transparent')}>
                            <Avatar id={e.actorId} name={e.actorName ?? t('common.system')} />
                            <div className="min-w-0 flex-1">
                              <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                                <span className="font-semibold">{e.actorName ?? t('common.system')}</span>
                                <span className="text-muted-foreground">{t(`action.${e.action}`)}</span>
                                {e.itemId && e.action !== 'Deleted'
                                  ? <Link className="break-words text-primary underline underline-offset-4 hover:text-foreground" to={itemHref(e.itemType, num, e.itemId)}>{item}</Link>
                                  : <span className="break-words">{item}</span>}
                                {i.count > 1 && <span className="rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium">{t('notif.changes', { n: i.count })}</span>}
                                {i.notified && <span className="text-xs/[18px] text-muted-foreground">{t('notif.notified')}</span>}
                                {i.unread && <span className="sr-only">{t('notif.unread')}</span>}
                                <time className="ml-auto text-xs/[18px] text-muted-foreground tabular-nums" dateTime={e.occurredAt}>{fmtTime(e.occurredAt)}</time>
                              </div>
                              {i.count === 1 && <div className="mt-1"><ChangeList row={e} /></div>}
                              {e.reason && <div className="mt-0.5 text-xs/[18px] text-muted-foreground">{t('common.reason')}: {e.reason}</div>}
                            </div>
                          </li>
                        )
                      })}
                    </ol>
                  </div>
                ))}
              </section>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
