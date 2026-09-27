// Response shapes of the Hub API used by the SPA.

export interface RefItem { id: string; name: string; isActive: boolean; code?: string; colour?: string; sortOrder?: number; shortName?: string; timeZone?: string; defaultDisciplineId?: string }
export interface Reference { disciplines: RefItem[]; clients: RefItem[]; offices: RefItem[]; deliverableTypes: RefItem[]; phases: RefItem[]; projectTypes: RefItem[] }

export interface Perm { ok: boolean; reason?: string | null }
export interface MilestoneRef { id: string; key: string; name: string; date?: string | null; status?: string | null; milestoneType?: string }

export interface ProjectRow {
  id: string; projectNumber: string; name: string; status: string; priority: string; visibility: string
  startDate?: string; targetCompletionDate?: string; client: { id: string; name: string }; pm: { id: string; displayName: string }
  office?: string; phase?: string; computedHealth: string; reportedHealth: string; healthReasons?: HealthReason[] | null
  healthOverrideNote?: string; healthOverrideAt?: string; progressPct?: number | null; overdueTasks: number; blockedTasks: number
  overdueDecisions: number; highIssues: number; attentionCritical: number; attentionWarning: number
  nextMilestone?: MilestoneRef | null; nextSubmission?: MilestoneRef | null; myRoles: string[]; starred: boolean; lastActivityAt?: string
}

export interface HealthReason { text: string; rule?: string; colour?: string | null; threshold?: number | string | null; values?: Record<string, unknown> | null }

export interface ProjectDiscipline { id: string; disciplineId: string; name: string; code: string; colour: string; leadUserId?: string; leadName?: string; isActive: boolean }

export interface ProjectDetail {
  id: string; projectNumber: string; name: string; clientId: string; clientReference?: string; projectManagerId: string; pmName: string
  officeId: string; projectTypeId?: string; description?: string; location?: string; status: string; phaseId?: string
  startDate?: string; targetCompletionDate?: string; priority: string; visibility: string; internalNotes?: string; coordinationDay?: string
  allowViewerComments: boolean; lastCoordinationReviewedAt?: string; lastCoordinationReviewedBy?: string; statusChangedAt?: string
  activatedAt?: string; completedAt?: string; archivedAt?: string; rowVersion: number; client: string; office: string; phase?: string
  createdFromTemplateId?: string; templateVersion?: number; templateName?: string | null
  links: { id: string; title: string; url: string; linkType: string }[]
  disciplines: ProjectDiscipline[]
  health: { computed: string; reported: string; overrideActive: boolean; healthOverride?: string; healthOverrideNote?: string; overrideBy?: string
    healthOverrideAt?: string; healthOverrideExpiresAt?: string; reasons?: HealthReason[] | null; inputs?: Record<string, number> | null; evaluatedAt?: string }
  progressPct?: number | null; nextMilestone?: MilestoneRef | null; nextSubmission?: MilestoneRef | null
  myRoles: string[]; follow?: { level: string; source: string } | null; starred: boolean
  setupChecklist?: { hasMilestone: boolean; everyDisciplineHasLead: boolean; everySubmissionHasDeliverable: boolean } | null
  suggestArchive: boolean; editWindowDaysLeft?: number | null
  permissions: {
    edit: Perm; manageTeam: Perm; manageMilestones: Perm; healthOverride: Perm; comment: Perm; raiseRegister: Perm; runCoordination: Perm
    createEvent: Perm; enterTime: Perm; createTaskIn: string[]; createDeliverableIn: string[]; leadOf: string[]; isPm: boolean
    transitions: string[]; changeNumber: boolean; setVisibility: boolean; needsReason: boolean
  }
}

export interface Page<T> { items: T[]; page: number; pageSize: number; totalCount: number }
