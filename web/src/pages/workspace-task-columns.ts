/** Workspace layout IDs are separate from project-task export field names. */
export const WORKSPACE_TASK_COLUMN_IDS = ['key', 'name', 'project', 'deliverable', 'status', 'assignee', 'reviewer', 'dueDate', 'priority', 'estimate', 'progress', 'indicators', 'lastActivity', 'submissionDate', 'comments'] as const
export type WorkspaceTaskColumnId = (typeof WORKSPACE_TASK_COLUMN_IDS)[number]
export const REQUIRED_WORKSPACE_TASK_COLUMNS: readonly WorkspaceTaskColumnId[] = ['key', 'name', 'project']
export const LEGACY_WORKSPACE_TASK_COLUMNS: readonly WorkspaceTaskColumnId[] = ['key', 'name', 'project', 'status', 'assignee', 'dueDate', 'priority', 'progress', 'indicators']
export const DEFAULT_WORKSPACE_TASK_COLUMNS: readonly WorkspaceTaskColumnId[] = [...LEGACY_WORKSPACE_TASK_COLUMNS, 'lastActivity', 'comments']

/** Canonical order, one occurrence, and identity even when a shared URL contains only unknown IDs. */
export function normalizeWorkspaceTaskColumns(value: readonly unknown[]): WorkspaceTaskColumnId[] {
  const selected = new Set<unknown>([...REQUIRED_WORKSPACE_TASK_COLUMNS, ...value])
  return WORKSPACE_TASK_COLUMN_IDS.filter(id => selected.has(id))
}

export const workspaceTaskColumnsKey = (userId: string) => `hub.workspaceTaskColumns.${userId}`

export function readWorkspaceTaskColumns(userId: string): WorkspaceTaskColumnId[] {
  try {
    const raw = localStorage.getItem(workspaceTaskColumnsKey(userId))
    if (raw != null) {
      const parsed: unknown = JSON.parse(raw)
      if (Array.isArray(parsed)) return normalizeWorkspaceTaskColumns(parsed)
    }
  } catch { /* Browser defaults are optional; an explicit URL always wins. */ }
  return normalizeWorkspaceTaskColumns(DEFAULT_WORKSPACE_TASK_COLUMNS)
}

export function saveWorkspaceTaskColumns(userId: string, columns: readonly WorkspaceTaskColumnId[]) {
  try { localStorage.setItem(workspaceTaskColumnsKey(userId), JSON.stringify(normalizeWorkspaceTaskColumns(columns))) } catch { /* Keep the URL choice when storage is unavailable. */ }
}

/** null means no override; an explicitly empty cols= means the mandatory identity columns only. */
export function resolveWorkspaceTaskColumns(raw: string | null, defaults: readonly WorkspaceTaskColumnId[]) {
  return normalizeWorkspaceTaskColumns(raw === null ? defaults : raw.split(','))
}

export interface TaskSubmissionDateRow {
  submissionDate?: { state?: string | null; targetDate?: string | null } | null
}
