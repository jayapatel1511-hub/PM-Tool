import type { ExportEntry } from '@/components/hub/export'
import { useFlag } from '@/lib/auth'
import { t } from '@/lib/i18n'

const DATA_FILTERS = ['disciplineId', 'status', 'milestoneId', 'ownerId', 'typeId', 'dueFrom', 'dueTo', 'indicator', 'requiresReview', 'q',
  'revisionWarning', 'purposeOfIssueId', 'returnCodeId', 'noDueDate', 'sort']

/** Existing ExportMenu extra entry; current data scope is retained while view/panel/layout stay out of API predicates. */
export function useDeliverableClientExport(projectId: string, projectNumber: string, dataFilters: Record<string, unknown>): ExportEntry[] {
  const enabled = useFlag('register_screens')
  if (!enabled) return []
  return [{ path: `projects/${projectId}/deliverables/client-export`, params: Object.fromEntries(Object.entries(dataFilters).filter(([key]) => DATA_FILTERS.includes(key))),
    name: `${projectNumber}-client-deliverables`, label: t('deliverableClient.export') }]
}
