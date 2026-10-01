import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Activity, AlertTriangle, AtSign, Bell, CalendarClock, CheckCircle2, Eye, Flag, Link2Off, MessageSquare, Scale, Unlock, UserPlus, Users,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Empty, Loading } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { get, post } from '@/lib/api'
import { ago } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { Page } from '@/lib/types'
import { cn } from '@/lib/utils'
import { ShellSlots } from '@/app/slots'

// Notification centre entry point (§13.17, §17.6): unread count polled every 60 seconds (FR-015).

export interface NotificationRow {
  id: string; eventType: string; projectId?: string; projectNumber?: string; itemType?: string; itemId?: string; itemKey?: string
  title: string; body?: string; linkPath?: string; actorName?: string; count: number; createdAt: string; updatedAt: string; readAt?: string | null
}

const ICON: Record<string, LucideIcon> = {
  TaskAssigned: UserPlus, ReviewerSet: Eye, ReviewRequested: Eye, ReviewOutcome: CheckCircle2, TaskBlocked: Link2Off, TaskUnblocked: Unlock,
  BlockingOverdue: AlertTriangle, DueDateChanged: CalendarClock, CommentOnItem: MessageSquare, Mention: AtSign, DecisionAssigned: Scale,
  DecisionOverdue: Scale, DecisionRecorded: Scale, MilestoneStatus: Flag, MilestoneDateChanged: Flag, AddedToProject: Users, BecameDisciplineLead: Users,
  AttentionCritical: AlertTriangle, HealthOverride: Activity, MemberAutoAdded: Users, StaffAssignment: Users, SupervisorStaffing: Users,
  IssueVerifierAssigned: Eye, IssueVerificationOutcome: CheckCircle2,
  ConstraintAction: Link2Off, ConstraintOutcome: CheckCircle2, CommitmentProposed: Flag, CommitmentChanged: Flag,
}
export const eventIcon = (code: string) => ICON[code] ?? Bell

/** FR-003 (§17.6): a cheap pulse every 4 seconds while the tab is in view; the counts, the bell's list and the Following
 *  feed are read again only when it changes. */
export function useUnread() {
  const qc = useQueryClient()
  const pulse = useQuery({ queryKey: ['unread-pulse'], queryFn: () => get<{ stamp: string }>('me/notifications/pulse'), refetchInterval: 4_000 })
  const stamp = pulse.data?.stamp
  const seen = useRef(stamp)
  useEffect(() => {
    if (!stamp || seen.current === stamp) return
    if (seen.current) { qc.invalidateQueries({ queryKey: ['notifications'] }); qc.invalidateQueries({ queryKey: ['following'] }) }
    seen.current = stamp
  }, [stamp, qc])
  return useQuery({ queryKey: ['unread', stamp], queryFn: () => get<{ notifications: number; following: number }>('me/notifications/unread-count'),
    enabled: pulse.isFetched, placeholderData: keepPreviousData })
}

export function useMarkRead() {
  const qc = useQueryClient()
  return async (body: { ids?: string[]; all?: boolean }) => {
    await post('me/notifications/read', body)
    qc.invalidateQueries({ queryKey: ['unread'] })
    qc.invalidateQueries({ queryKey: ['notifications'] })
  }
}

/** One notification: icon by type, sentence, project and time; opening it marks it read. */
export function NotificationItem({ n, onOpen }: { n: NotificationRow; onOpen: (n: NotificationRow) => void }) {
  const Icon = eventIcon(n.eventType)
  return (
    <button type="button" onClick={() => onOpen(n)} className={cn('flex w-full items-start gap-3 px-3 py-2.5 text-left text-sm hover:bg-muted', !n.readAt && 'bg-accent/40')}>
      <Icon className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
      <span className="min-w-0 flex-1">
        <span className="block">{n.title}</span>
        {n.body && <span className="block truncate text-xs text-muted-foreground">{n.body}</span>}
        <span className="block text-xs text-muted-foreground">{[n.projectNumber, ago(n.updatedAt)].filter(Boolean).join(' · ')}</span>
      </span>
      {!n.readAt && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" aria-label={t('notif.unread')} />}
    </button>
  )
}

export function useOpenNotification() {
  const navigate = useNavigate()
  const markRead = useMarkRead()
  return (n: NotificationRow) => {
    if (!n.readAt) markRead({ ids: [n.id] })
    if (n.linkPath) navigate(n.linkPath)
  }
}

function BellButton() {
  const unread = useUnread()
  const [open, setOpen] = useState(false)
  const count = unread.data?.notifications ?? 0
  const recent = useQuery({ queryKey: ['notifications', 'recent'], queryFn: () => get<Page<NotificationRow>>('me/notifications?pageSize=8'), enabled: open })
  const markRead = useMarkRead()
  const openItem = useOpenNotification()
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="icon" className="relative size-9" aria-label={count ? t('notif.bellCount', { n: count }) : t('nav.notifications')}>
          <Bell className="size-4" />
          {count > 0 && <span className="absolute right-1 top-1 grid min-w-4 place-items-center rounded-full bg-bad px-1 text-[10px] font-semibold leading-4 text-white">{count > 99 ? '99+' : count}</span>}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-96 p-0">
        <div className="flex items-center justify-between border-b px-3 py-2">
          <span className="text-sm font-semibold">{t('nav.notifications')}</span>
          {count > 0 && <Button size="sm" variant="ghost" onClick={() => markRead({ all: true })}>{t('notif.markAll')}</Button>}
        </div>
        <div className="max-h-96 overflow-y-auto">
          {recent.isPending ? <Loading rows={3} /> : (recent.data?.items ?? []).length === 0 ? <Empty>{t('notif.none')}</Empty>
            : recent.data!.items.map((n) => <NotificationItem key={n.id} n={n} onOpen={(x) => { setOpen(false); openItem(x) }} />)}
        </div>
        <div className="flex justify-between border-t px-3 py-2 text-sm">
          <Link to="/notifications" className="text-primary hover:underline" onClick={() => setOpen(false)}>{t('notif.openCentre')}</Link>
          {(unread.data?.following ?? 0) > 0 && <Link to="/notifications?tab=following" className="text-muted-foreground hover:underline" onClick={() => setOpen(false)}>{t('notif.followingCount', { n: unread.data!.following })}</Link>}
        </div>
      </PopoverContent>
    </Popover>
  )
}

ShellSlots.Bell = BellButton
