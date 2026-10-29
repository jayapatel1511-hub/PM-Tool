// Response shapes of the Hub API used by the SPA. The project list row and header come from the generated contract; only the
// evaluator's stored documents, which the contract leaves open, are narrowed here.
import type { components } from '@/gen/api'

type Schemas = components['schemas']

export type ProjectStatusNoteData = Schemas['NoteView']
export interface Perm { ok: boolean; reason?: string | null }

export interface HealthReason { text: string; rule?: string; colour?: string | null; threshold?: number | string | null; values?: Record<string, unknown> | null }

export type ProjectRow = Omit<Schemas['ProjectRow'], 'healthReasons'> & { healthReasons: HealthReason[] | null }
export type ProjectDiscipline = Schemas['ProjectDisciplineRow']
export type ProjectDetail = Omit<Schemas['ProjectDetail'], 'health'> & {
  health: Omit<Schemas['ProjectHealth'], 'reasons' | 'inputs'> & { reasons: HealthReason[] | null; inputs: Record<string, number> | null }
}

export interface Page<T> { items: T[]; page: number; pageSize: number; totalCount: number }
