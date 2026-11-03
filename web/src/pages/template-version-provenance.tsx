import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { Empty, ErrorBanner, Loading, Notice, Section } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { t, runtimeText, tv } from '@/lib/i18n'

type Template = components['schemas']['TemplateRead']
type Diff = components['schemas']['TemplateDraftDiffRead']
const unavailable = () => new Error(t('tpl.dataUnavailable'))

export function TemplateVersionDetails({ template, dirty }: { template: Pick<Template, 'id' | 'rowVersion' | 'status' | 'screen'>; dirty: boolean }) {
  const me = useMe(); const people = useFlag('people_screens'); const risks = useFlag('register_screens')
  const roles = useFlag('template_roles'); const working = useFlag('template_working_days')
  const [projectPage, setProjectPage] = useState(1); const [changePage, setChangePage] = useState(1)
  const size = template.screen?.draftDiff?.pageSize ?? 50
  const projects = useQuery({ queryKey: ['template', template.id, 'projects', me.id, people, projectPage], enabled: people,
    queryFn: () => typedRequest('get', '/api/v1/templates/{id}/projects', { params: { id: template.id }, query: { page: String(projectPage), pageSize: '20' } }) })
  const changes = useQuery({ queryKey: ['template', template.id, 'changes', me.id, people, risks, roles, working, template.rowVersion, changePage, size],
    enabled: people && changePage > 1 && !dirty && template.status === 'Draft',
    queryFn: async () => {
      const reply = await typedRequest('get', '/api/v1/templates/{id}', { params: { id: template.id }, query: { changePage: String(changePage), changePageSize: String(size) } })
      const diff = reply.screen?.draftDiff
      if (!diff || diff.draftRowVersion !== template.rowVersion) throw unavailable()
      return diff
    } })
  if (!people) return null
  if (!template.screen) return <ErrorBanner error={unavailable()} />
  return <>
    <Section title={t('tpl.versionProjects')}>
      <div className="space-y-3 p-5">
        {projects.error ? <ErrorBanner error={projects.error} retry={() => projects.refetch()} /> : projects.isPending ? <Loading rows={2} /> : <>
          <p className="text-sm text-muted-foreground">{t('tpl.versionProjectsCount', { count: projects.data.totalCount })}</p>
          {projects.data.items.length === 0 ? <Empty>{t('tpl.versionProjectsNone')}</Empty> : <ul className="divide-y">{projects.data.items.map(project => <li key={project.projectId} className="flex flex-wrap justify-between gap-2 py-2 text-sm">
            <Link className="font-medium hover:underline" to={`/projects/${project.projectId}`}>{project.projectNumber} · {project.name}</Link><span className="text-muted-foreground">{tv(project.status)}</span>
          </li>)}</ul>}
          <div className="flex gap-2"><Button size="sm" variant="outline" disabled={projectPage <= 1} onClick={() => setProjectPage(projectPage - 1)}>{t('common.previous')}</Button>
            <Button size="sm" variant="outline" disabled={projectPage * projects.data.pageSize >= projects.data.totalCount} onClick={() => setProjectPage(projectPage + 1)}>{t('common.next')}</Button></div>
        </>}
      </div>
    </Section>
    {template.status === 'Draft' && <Section title={t('tpl.draftChanges')}><div className="space-y-3 p-5">
      {dirty ? <Notice title={t('tpl.diffSaved')} /> : changes.error ? <ErrorBanner error={changes.error} retry={() => changes.refetch()} />
        : changePage > 1 && changes.isPending ? <Loading rows={3} />
          : <DiffRead diff={changePage > 1 ? changes.data : template.screen.draftDiff ?? undefined} />}
      {!dirty && template.screen.draftDiff?.state === 'Available' && <div className="flex gap-2">
        <Button size="sm" variant="outline" disabled={changePage <= 1} onClick={() => setChangePage(changePage - 1)}>{t('common.previous')}</Button>
        <Button size="sm" variant="outline" disabled={changePage * size >= (template.screen.draftDiff.totalCount ?? 0)} onClick={() => setChangePage(changePage + 1)}>{t('common.next')}</Button>
      </div>}
    </div></Section>}
  </>
}

function DiffRead({ diff }: { diff: Diff | undefined }) {
  if (!diff) return <ErrorBanner error={unavailable()} />
  if (diff.state !== 'Available' || !diff.counts || !diff.source) return <p className="text-sm text-muted-foreground">{t(diff.state === 'NotRecorded' ? 'tpl.diffNotRecorded' : 'tpl.diffUnavailable')}</p>
  return <div className="space-y-3" data-template-diff>
    <p className="text-sm">{t('tpl.diffSource', { name: diff.source.name, version: diff.source.version ?? '' })}</p>
    {diff.incompleteSections.length > 0 && <Notice title={t('tpl.diffIncomplete')} />}
    <p className="text-sm tabular-nums">{t('tpl.diffCounts', { added: diff.counts.added, removed: diff.counts.removed, changed: diff.counts.changed })}</p>
    {diff.totalCount === 0 && <p className="text-sm text-muted-foreground">{t('tpl.diffNone')}</p>}
    <ul className="space-y-3">{diff.changes.map(change => <li key={`${change.kind}:${change.ref}`} className="rounded-md border p-3 text-sm">
      <p className="font-medium">{runtimeText(`tpl.diff${change.change}`)} · {runtimeText(`tpl.kind.${change.kind}`)} · {change.afterName ?? change.beforeName}</p>
      {change.fields.length > 0 && <dl className="mt-2 grid gap-1 text-xs/[18px] text-muted-foreground">{change.fields.map(field => <div key={field.field} className="grid grid-cols-[minmax(100px,1fr)_2fr_2fr] gap-3">
        <dt>{runtimeText(`tpl.field.${field.field}`)}</dt><dd className="break-words">{t('tpl.diffBefore')}: {field.before === 'True' ? t('common.yes') : field.before === 'False' ? t('common.no') : field.before ?? '—'}</dd><dd className="break-words">{t('tpl.diffAfter')}: {field.after === 'True' ? t('common.yes') : field.after === 'False' ? t('common.no') : field.after ?? '—'}</dd>
      </div>)}</dl>}
    </li>)}</ul>
  </div>
}

export function ProjectTemplateProvenance({ projectId }: { projectId: string }) {
  const me = useMe(); const enabled = useFlag('people_screens')
  const read = useQuery({ queryKey: ['template-provenance', projectId, me.id, enabled], enabled,
    queryFn: () => typedRequest('get', '/api/v1/projects/{id}/template-provenance', { params: { id: projectId } }) })
  if (!enabled) return null
  if (read.error) return <Section title={t('tpl.origin')}><ErrorBanner error={read.error} retry={() => read.refetch()} /></Section>
  if (read.isPending) return <Section title={t('tpl.origin')}><Loading rows={2} /></Section>
  const value = read.data
  return <Section title={t('tpl.origin')}><div className="space-y-2 break-words p-5 text-sm" data-template-provenance>
    {value.state !== 'Recorded' || !value.source ? <p>{t('tpl.provenanceUnknown')}</p> : <>
      <p>{t('tpl.provenanceSource', { name: value.source.name, version: value.source.version })} {value.newer ? t('tpl.provenanceNewer', { version: value.newer.version, status: tv(value.newer.status) }) : t('tpl.provenanceNoNewer')}</p>
      {value.currentPublished && <p className="text-muted-foreground">{t('tpl.provenancePublished', { version: value.currentPublished.version })}</p>}
    </>}
    <p className="text-xs/[18px] text-muted-foreground">{t('tpl.provenanceInfo')}</p>
  </div></Section>
}
