import { lazy, type ReactNode } from 'react'
import { t } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import type { CreateContext, DisciplinePick } from '@/pages/projects/CreateProject'
import { lazyImport } from '@/lib/lazy'

export const ADMIN_EXTRA: { to: string; label: string }[] = []
export const operationsRegistration = { to: '/admin/operations', label: t('ops.title') }
export const holidaysRegistration = { to: '/admin/holidays', label: t('holiday.title') }

export interface CreateSource {
  id: string
  label: string
  render: (v: any, set: (v: any) => void, ctx: CreateContext) => ReactNode
  body: (v: any) => Record<string, unknown>
  summary?: (v: any, ctx: CreateContext) => ReactNode
}
export const CREATE_SOURCES: CreateSource[] = []
export const SETTINGS_SECTIONS: { id: string; render: (p: ProjectDetail) => ReactNode }[] = []

const CopySource = lazy(() => lazyImport(() => import('@/pages/projects/CopyStructure').then(m => ({ default: m.CopyFrom }))))
export const copySource: CreateSource = {
  id: 'copy', label: 'copy.label',
  render: (v, set) => <CopySource value={v as string | null} onChange={set} />,
  body: (v) => (v ? { copyFromProjectId: v } : {}),
}

const TemplateSource = lazy(() => lazyImport(() => import('@/pages/projects/FromTemplate').then(m => ({ default: m.TemplateSource }))))
const TemplateSummary = lazy(() => lazyImport(() => import('@/pages/projects/FromTemplate').then(m => ({ default: m.TemplateSummary }))))
const TemplateSettings = lazy(() => lazyImport(() => import('@/pages/projects/FromTemplate').then(m => ({ default: m.TemplateSettings }))))
export const templateSource: CreateSource = {
  id: 'template', label: 'tpl.from',
  render: (v, set, ctx) => <TemplateSource value={v} onChange={set} ctx={ctx} />,
  summary: (v, ctx) => <TemplateSummary value={v} ctx={ctx} />,
  body: (v) => (v ? { templateId: v.templateId, template: { milestoneDates: v.dates } } : {}),
}
export const templateSettings = {
  id: 'template-origin',
  render: (p: ProjectDetail) => <TemplateSettings project={p} />,
}

export type { CreateContext, DisciplinePick }
