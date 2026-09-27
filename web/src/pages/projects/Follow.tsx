import { Bell, BellOff, Check, ChevronDown } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { useProjectRefresh } from '@/hooks/data'
import { del, put } from '@/lib/api'
import { t } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { ProjectSlots } from './slots'

const LEVELS = ['AllActivity', 'MyItemsOnly', 'Muted']

async function change(projectId: string, level: string | null) {
  if (level) await put(`projects/${projectId}/follow`, { level })
  else await del(`projects/${projectId}/follow`)
}

/** Follow level for one project (§12.18 ASG-02): only the user sets their own; any change is a manual choice. */
export function FollowLevelSelect({ projectId, level, onChanged }: { projectId: string; level?: string | null; onChanged: () => void }) {
  return (
    <select className="h-8 w-full rounded-md border bg-card px-2 text-sm" value={level ?? ''} aria-label={t('follow.level')}
      onChange={async (e) => { await change(projectId, e.target.value || null); onChanged() }}>
      {LEVELS.map((l) => <option key={l} value={l}>{t(`follow.${l}`)}</option>)}
      <option value="">{t('follow.none')}</option>
    </select>
  )
}

function FollowControl({ p }: { p: ProjectDetail }) {
  const refresh = useProjectRefresh()
  const f = p.follow
  const set = async (level: string | null) => { await change(p.id, level); refresh(p.id); toast.success(level ? t('follow.saved', { level: t(`follow.${level}`) }) : t('follow.unfollowed')) }
  const title = f?.source === 'Assignment' ? t('follow.becauseTeam') : undefined
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" title={title} className="h-7 gap-1 text-xs">
          {f?.level === 'Muted' ? <BellOff className="size-3.5" /> : <Bell className="size-3.5" />}
          {f ? t('follow.following', { level: t(`follow.${f.level}`) }) : t('follow.follow')}<ChevronDown className="size-3" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        {LEVELS.map((l) => (
          <DropdownMenuItem key={l} onSelect={() => set(l)} className="flex-col items-start gap-0">
            <span className="flex items-center gap-1.5">{f?.level === l ? <Check className="size-3.5" /> : <span className="w-3.5" />}{t(`follow.${l}`)}</span>
            <span className="pl-5 text-xs text-muted-foreground">{t(`follow.${l}.hint`)}</span>
          </DropdownMenuItem>
        ))}
        {f && <><DropdownMenuSeparator /><DropdownMenuItem onSelect={() => set(null)}>{t('follow.unfollow')}</DropdownMenuItem></>}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

ProjectSlots.Follow = FollowControl
