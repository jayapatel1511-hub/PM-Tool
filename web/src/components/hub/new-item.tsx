import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { ShellSlots } from '@/app/slots'
import { Field, selectCls } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { useProject } from '@/hooks/data'
import { get } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { today } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { EventDialog } from '@/pages/Calendar'
import { CreateProjectDialog } from '@/pages/projects/CreateProject'
import { CreateTask, useProjectLists } from '@/pages/projects/Tasks'

/** Create a task in a chosen project (§13.10, §36.1): only live projects the person is on, and only where they may create. */
export function NewTaskDialog({ onClose, projectId }: { onClose: (created?: string) => void; projectId?: string }) {
  const list = useQuery({ queryKey: ['projects-mine-live'], queryFn: () => get<PageOf<{ id: string; projectNumber: string; name: string }>>('projects?mine=true&pageSize=200') })
  const live = list.data?.items ?? []
  const [picked, setPicked] = useState<string | undefined>(projectId)
  const number = live.find((p) => p.id === (picked ?? live[0]?.id))?.projectNumber
  const [chosen, setChosen] = useState(false)
  const project = useProject(number)
  const lists = useProjectLists(project.data?.id)
  const openPanel = useItemPanel()
  const allowed = (project.data?.permissions.createTaskIn.length ?? 0) > 0
  if (chosen && project.data && allowed)
    return <CreateTask p={project.data} deliverables={lists.deliverables} milestones={lists.milestones} onClose={(id) => { onClose(id); if (id) openPanel('Task', id) }} />
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-sm">
        <DialogHeader><DialogTitle>{t('task.new')}</DialogTitle></DialogHeader>
        {list.isFetched && live.length === 0 ? <p className="text-sm text-muted-foreground">{t('mywork.noProjects')}</p> : (
          <Field label={t('mywork.inProject')} htmlFor="nt-project" hint={project.data && !allowed ? t('mywork.cannotCreate') : undefined}>
            <select id="nt-project" className={selectCls} value={picked ?? live[0]?.id ?? ''} onChange={(e) => setPicked(e.target.value)}>
              {live.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
            </select>
          </Field>
        )}
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onClose()}>{t('common.cancel')}</Button>
          <Button disabled={!allowed} onClick={() => setChosen(true)}>{t('mywork.continue')}</Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}

/** New Item (FR-VIS-01): only the types this person may create. */
function NewItemMenu() {
  const me = useMe()
  const [open, setOpen] = useState<'project' | 'task' | 'event' | null>(null)
  const close = () => setOpen(null)
  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button size="sm" className="gap-1" aria-label={t('new.label')}><Plus className="size-4" /><span className="hidden sm:inline">{t('new.label')}</span></Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {me.capabilities.createProject && <DropdownMenuItem onSelect={() => setOpen('project')}>{t('new.project')}</DropdownMenuItem>}
          {me.capabilities.createTask && <DropdownMenuItem onSelect={() => setOpen('task')}>{t('new.task')}</DropdownMenuItem>}
          <DropdownMenuItem onSelect={() => setOpen('event')}>{t('new.event')}</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      {open === 'project' && <CreateProjectDialog onClose={close} />}
      {open === 'task' && <NewTaskDialog onClose={close} />}
      {open === 'event' && <EventDialog event={null} anchor={today()} onClose={close} />}
    </>
  )
}

ShellSlots.QuickCreate = NewItemMenu
