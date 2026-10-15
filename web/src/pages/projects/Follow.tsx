import { Bell, BellOff, ChevronDown } from 'lucide-react'
import { toast } from 'sonner'
import { selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { useProjectRefresh } from '@/hooks/data'
import { del, put } from '@/lib/api'
import { t } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import { ProjectSlots } from './slots'

const LEVELS = ['AllActivity', 'MyItemsOnly', 'Muted']

async function change(projectId: string, level: string | null) {
  if (level) await put(`projects/${projectId}/follow`, { level })
  else await del(`projects/${projectId}/follow`)
}

/** Follow level for one project (§12.18 ASG-02): only the user sets their own; any change is a manual choice. Lists that
 *  show several projects pass `projectName`, so each select is named for its project ("Follow level for …"). */
export function FollowLevelSelect({ projectId, level, onChanged, projectName }: { projectId: string; level?: string | null; onChanged: () => void; projectName?: string }) {
  return (
    <select className={cn(selectCls, 'h-(--control-row-h)')} value={level ?? ''} aria-label={projectName ? t('follow.levelFor', { project: projectName }) : t('follow.level')}
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
        <Button variant="outline" size="sm" title={title}>
          {f?.level === 'Muted' ? <BellOff className="size-4" /> : <Bell className="size-4" />}
          {f ? t('follow.following', { level: t(`follow.${f.level}`) }) : t('follow.follow')}<ChevronDown className="size-4 text-muted-foreground" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-72">
        {/* Each choice saves on select, including the current level (it turns a team follow into the user's own choice). */}
        <DropdownMenuRadioGroup value={f?.level ?? ''}>
          {LEVELS.map((l) => (
            <DropdownMenuRadioItem key={l} value={l} onSelect={() => set(l)} className="flex-col items-start gap-0 py-2">
              <span>{t(`follow.${l}`)}</span>
              <span className="text-xs/[18px] text-muted-foreground">{t(`follow.${l}.hint`)}</span>
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        {f && <><DropdownMenuSeparator /><DropdownMenuItem onSelect={() => set(null)}>{t('follow.unfollow')}</DropdownMenuItem></>}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

ProjectSlots.Follow = FollowControl
