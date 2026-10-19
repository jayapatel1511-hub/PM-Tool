import { useQuery } from '@tanstack/react-query'
import { selectCls } from '@/components/hub/common'
import { Label } from '@/components/ui/label'
import { get } from '@/lib/api'
import { t } from '@/lib/i18n'
import type { Page, ProjectRow } from '@/lib/types'

/** §27.1: start from another project's structure (disciplines, undated milestones, deliverables, unassigned tasks, dependencies). */
export function CopyFrom({ value, onChange }: { value?: string | null; onChange: (v: string | null) => void }) {
  const q = useQuery({ queryKey: ['projects', 'copy-sources'], queryFn: () => get<Page<ProjectRow>>('projects?includeArchived=true&pageSize=200&sort=number:desc') })
  return (
    <div className="space-y-1.5">
      <Label htmlFor="copy-from">{t('copy.label')}<span className="font-normal text-muted-foreground"> ({t('common.optional')})</span></Label>
      <select id="copy-from" className={selectCls} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)} aria-describedby="copy-from-hint">
        <option value="">{t('copy.none')}</option>
        {q.data?.items.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
      </select>
      <p id="copy-from-hint" className="text-xs/[18px] text-muted-foreground">{t('copy.hint')}</p>
    </div>
  )
}

export const copySource = { id: 'copy', label: 'copy.label', render: (v: any, set: (value: any) => void) => <CopyFrom value={v as string | null} onChange={set} />, body: (v: any) => (v ? { copyFromProjectId: v } : {}) }
