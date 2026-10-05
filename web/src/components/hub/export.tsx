import { Download } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { ApiError, download, qs } from '@/lib/api'
import { t } from '@/lib/i18n'

/** "Export what is on screen" (§13.3, FR-007): the list's own filters, as CSV or Excel. */
export function ExportMenu({ path, params, name, label }: { path: string; params: Record<string, unknown>; name: string; label?: string }) {
  const run = (format: 'csv' | 'xlsx') => download(`/api/v1/${path}${qs({ ...params, format })}`, `${name}.${format}`)
    .catch((e) => toast.error(e instanceof ApiError ? e.message : t('app.error')))
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild><Button variant="outline"><Download className="size-4" />{label ?? t('export.label')}</Button></DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={() => run('xlsx')}>{t('export.xlsx')}</DropdownMenuItem>
        <DropdownMenuItem onSelect={() => run('csv')}>{t('export.csv')}</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
