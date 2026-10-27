import { Download } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { ApiError, download, qs } from '@/lib/api'
import { t } from '@/lib/i18n'

/** A further list the menu exports, after the page's own entries (§47.5 FR-REC-12). */
export interface ExportEntry { path: string; params: Record<string, unknown>; name: string; label: string }

/** "Export what is on screen" (§13.3, FR-007): the list's own filters, as CSV or Excel. */
export function ExportMenu({ path, params, name, label, extra = [] }: { path: string; params: Record<string, unknown>; name: string; label?: string; extra?: ExportEntry[] }) {
  const run = (format: 'csv' | 'xlsx', entry: Omit<ExportEntry, 'label'> = { path, params, name }) => download(`/api/v1/${entry.path}${qs({ ...entry.params, format })}`, `${entry.name}.${format}`)
    .catch((e) => toast.error(e instanceof ApiError ? e.message : t('app.error')))
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild><Button variant="outline"><Download className="size-4" />{label ?? t('export.label')}</Button></DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={() => run('xlsx')}>{t('export.xlsx')}</DropdownMenuItem>
        <DropdownMenuItem onSelect={() => run('csv')}>{t('export.csv')}</DropdownMenuItem>
        {extra.flatMap((entry) => (['xlsx', 'csv'] as const).map((format) => <DropdownMenuItem key={`${entry.path}:${format}`} onSelect={() => run(format, entry)}>{entry.label} · {t(`export.${format}`)}</DropdownMenuItem>))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
