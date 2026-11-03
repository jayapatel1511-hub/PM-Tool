import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Notice, Page, Section, selectCls } from '@/components/hub/common'
import { AddLink, LinkAnchor } from '@/components/hub/links'
import { LinkReferenceEditor, LinkReferenceSummary } from '@/components/hub/document-link-reference'
import { IdentityDot } from '@/components/hub/identity-colour'
import { useItemPanel } from '@/components/hub/panel-host'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import type { components } from '@/gen/api'
import { Key } from '@/components/hub/pills'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { fmtDate } from '@/lib/format'
import { plural, t, runtimeText, tv } from '@/lib/i18n'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { invalidateChange } from '@/lib/keys'
import { accentOf } from '@/lib/utils'

type ReferenceLink = Pick<components['schemas']['LinkRow'], 'id' | 'title' | 'rowVersion' | 'referenceContext'>

/** Files (§36.8, FR-VIS-09): the selected projects' document links grouped by project and linked item, with search, a type
 *  filter and Copy path for network folders. A link library only: nothing is uploaded or stored. */
export function FilesPage() {
  const qc = useQueryClient()
  const visualSystem = useFlag('visual_system')
  const peopleOn = useFlag('people_screens'), me = useMe()
  const scope = useScope()
  const location = useLocation()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const [adding, setAdding] = useState<string | null>(null)
  const [addingMissing, setAddingMissing] = useState<{ viewer: string; scope: string; id: string } | null>(null)
  const [editing, setEditing] = useState<{ viewer: string; scope: string; link: ReferenceLink } | null>(null)
  const f = { q: sp.get('q') ?? undefined, type: sp.get('type') ?? undefined }
  const filters = peopleOn ? { disciplineId: sp.get('disciplineId')?.trim() || undefined,
    addedById: sp.get('addedById')?.trim() || undefined, addedThisWeek: sp.get('addedThisWeek')?.trim() || undefined } : {}
  // The box shows the applied search until someone types, so a removed token or Clear updates it too.
  const [draft, setDraft] = useState<string | null>(null)
  const term = draft ?? f.q ?? ''
  const q = useQuery({ queryKey: ['files', scope.api, f, me.id, peopleOn, ...(peopleOn ? [filters] : [])], enabled: scope.ready,
    queryFn: () => typedRequest('get', '/api/v1/files', { query: { projects: scope.api, ...f, ...filters } }) })
  const receivedScreen = peopleOn && q.isSuccess ? q.data.screen : undefined
  const contexts = new Map(receivedScreen?.links.map(context => [context.linkId, context]))
  const shownLinks = q.data?.projects.flatMap(project => project.items.flatMap(item => item.links)) ?? []
  // One server envelope supplies rows and permissions. A missing/misaligned context cannot annotate a different link.
  const coherent = receivedScreen && contexts.size === receivedScreen.links.length && contexts.size === shownLinks.length
    && shownLinks.every(link => { const context = contexts.get(link.id); return context && Number.isInteger(context.rowVersion) && context.rowVersion > 0 })
  const liveScreen = coherent ? receivedScreen : undefined
  const data = peopleOn ? (q.isSuccess && liveScreen ? q.data : undefined) : q.data
  const heldChoices = useRef<{ viewer: string; scope: string; screen: components['schemas']['FileScreenRead'] } | null>(null)
  useEffect(() => {
    if (!peopleOn || q.error || (q.isSuccess && !liveScreen)) { heldChoices.current = null; return }
    if (liveScreen) heldChoices.current = { viewer: me.id, scope: scope.api, screen: liveScreen }
  }, [peopleOn, me.id, scope.api, liveScreen, q.error, q.isSuccess])
  // Keep only scope-owned choices mounted while the next filter read is pending; never borrow its rows or rights.
  const choices = liveScreen ?? (peopleOn && q.isPending && heldChoices.current?.viewer === me.id && heldChoices.current.scope === scope.api ? heldChoices.current.screen : undefined)
  const discipline = choices?.disciplines.find(choice => choice.id.toLowerCase() === filters.disciplineId?.toLowerCase())
  const author = choices?.authors.find(choice => choice.id.toLowerCase() === filters.addedById?.toLowerCase())
  const disciplineLabel = discipline ? discipline.isActive ? discipline.name : t('common.inactiveSuffix', { name: discipline.name }) : t('files.selectedDisciplineValue', { id: filters.disciplineId })
  const authorLabel = author ? author.isActive ? author.displayName : t('common.inactiveSuffix', { name: author.displayName }) : t('files.selectedAuthorValue', { id: filters.addedById })
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); if (k === 'q') setDraft(null); setSp(n, { replace: true }) }
  const tokens = [
    { key: 'q', label: t('common.search'), value: f.q && `“${f.q}”` },
    { key: 'type', label: t('common.type'), value: f.type && runtimeText(`linkType.${f.type}`) },
    { key: 'disciplineId', label: t('common.discipline'), value: filters.disciplineId && disciplineLabel },
    { key: 'addedById', label: t('files.addedByFilter'), value: filters.addedById && authorLabel },
    { key: 'addedThisWeek', label: t('files.addedThisWeek'), value: filters.addedThisWeek && filters.addedThisWeek !== 'false' ? filters.addedThisWeek === 'true' ? t('common.yes') : filters.addedThisWeek : undefined },
  ].flatMap(token => token.value ? [{ ...token, value: token.value }] : [])
  const clear = () => { const n = new URLSearchParams(sp); for (const key of ['q', 'type', ...(peopleOn ? ['disciplineId', 'addedById', 'addedThisWeek'] : [])]) n.delete(key); setDraft(null); setSp(n, { replace: true }) }
  const error = q.error ?? (peopleOn && q.isSuccess && !liveScreen ? new Error(t('files.contextUnavailable')) : null)
  const editor = peopleOn && data && editing?.viewer === me.id && editing.scope === scope.api ? editing : null
  const missingForm = peopleOn && addingMissing?.viewer === me.id && addingMissing.scope === scope.api ? addingMissing : null
  const closeEditor = () => { const id = editing?.link.id; setEditing(null); if (id) requestAnimationFrame(() => document.getElementById(`file-reference-${id}`)?.focus()) }
  return (
    <Page tabs={<WorkspaceTabs />} title={t('nav.files')} subtitle={data ? plural(data.total, 'files.one', 'files.many', { n: data.total }) : scope.label}>
      <FilterBar activeCount={tokens.length}
        search={<Field label={t('common.search')} htmlFor="files-search">
          <form onSubmit={(e) => { e.preventDefault(); set('q', term.trim() || null); setDraft(null) }}>
            <Input id="files-search" type="search" placeholder={t('files.search')} value={term}
              onChange={(e) => { if (e.target.value) setDraft(e.target.value); else { setDraft(null); set('q', null) } }} />
          </form>
        </Field>}
        active={<div className="min-w-0 max-w-full [&_strong]:break-all [&_[role=group]>span]:min-w-0 [&_[role=group]>span]:max-w-full"><ActiveFilters tokens={tokens} onRemove={(k) => set(k, null)} onClear={clear} /></div>}>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.type')} htmlFor="files-type" className="w-full sm:w-48">
            <select id="files-type" className={selectCls} value={f.type ?? ''} onChange={(e) => set('type', e.target.value)}>
              <option value="">{t('files.anyType')}</option>{q.data?.types.map((x) => <option key={x} value={x}>{runtimeText(`linkType.${x}`)}</option>)}
            </select>
          </Field>
          {peopleOn && <>
            <Field label={t('common.discipline')} htmlFor="files-discipline" className="min-w-0 w-full sm:w-48">
              <select id="files-discipline" className={`${selectCls} max-w-full`} value={discipline?.id ?? filters.disciplineId ?? ''} onChange={event => set('disciplineId', event.target.value)} aria-describedby={choices?.disciplinesTruncated ? 'files-discipline-limit' : undefined}>
                <option value="">{t('files.anyDiscipline')}</option>
                {filters.disciplineId && !discipline && <option value={filters.disciplineId}>{disciplineLabel}</option>}
                {choices?.disciplines.map(choice => <option key={choice.id} value={choice.id}>{choice.isActive ? choice.name : t('common.inactiveSuffix', { name: choice.name })}</option>)}
              </select>
              {choices?.disciplinesTruncated && <p id="files-discipline-limit" className="text-xs text-muted-foreground">{t('files.disciplineChoicesTruncated', { n: choices.disciplines.length })}</p>}
            </Field>
            <Field label={t('files.addedByFilter')} htmlFor="files-author" className="min-w-0 w-full sm:w-48">
              <select id="files-author" className={`${selectCls} max-w-full`} value={author?.id ?? filters.addedById ?? ''} onChange={event => set('addedById', event.target.value)} aria-describedby={choices?.authorsTruncated ? 'files-author-limit' : undefined}>
                <option value="">{t('files.anyAuthor')}</option>
                {filters.addedById && !author && <option value={filters.addedById}>{authorLabel}</option>}
                {choices?.authors.map(choice => <option key={choice.id} value={choice.id}>{choice.isActive ? choice.displayName : t('common.inactiveSuffix', { name: choice.displayName })}</option>)}
              </select>
              {choices?.authorsTruncated && <p id="files-author-limit" className="text-xs text-muted-foreground">{t('files.authorChoicesTruncated', { n: choices.authors.length })}</p>}
            </Field>
            <label className="inline-flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox id="files-week" checked={filters.addedThisWeek === 'true'} onCheckedChange={checked => set('addedThisWeek', checked ? 'true' : null)} />{t('files.addedThisWeek')}</label>
          </>}
          <p className="text-sm text-muted-foreground sm:pb-2.5">{t('files.note')}</p>
        </div>
        {peopleOn && <p className="text-xs text-muted-foreground">{t('files.linkFiltersHint')}</p>}
        {choices && <p className="text-xs text-muted-foreground">{t('files.weekHint', { from: fmtDate(choices.weekFrom), through: fmtDate(choices.weekThrough), zone: choices.orgTimeZone })}</p>}
      </FilterBar>
      {error && <ErrorBanner error={error} retry={() => q.refetch()} />}
      {data?.truncated && <Notice title={t('ws.truncatedTitle')}>{t('files.truncated')}</Notice>}
      {!data ? (q.isPending && <div className="rounded-lg border bg-card"><Loading rows={6} /></div>) : data.projects.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('files.none')}</Empty></div>
      ) : data.projects.map((p) => (
        <Section key={p.projectId} id={`f-${p.projectId}`} accent={accentOf(p.projectId)} count={p.count}
          title={<Link className="hover:underline" to={`/projects/${p.projectNumber}`}>{visualSystem && <IdentityDot id={p.projectId} colour={p.identityColour} className="mr-2 align-middle" />}<span className="key mr-2 text-(--acc-fg)">{p.projectNumber}</span>{p.name}</Link>}
          actions={p.canAdd && adding !== p.projectId && <Button size="sm" variant="outline" onClick={() => setAdding(p.projectId)}><Plus className="size-4" />{t('links.add')}</Button>}>
          {adding === p.projectId && <div className="border-b p-4"><AddLink type="Project" id={p.projectId} onDone={() => setAdding(null)} /></div>}
          {p.items.length === 0 ? <Empty>{t('files.noneHere')}</Empty> : (
            <ul className="divide-y">
              {p.items.map((i) => (
                <li key={`${i.itemType}-${i.itemId}`} className="px-5 py-3">
                  <p className="mb-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs/[18px] text-muted-foreground">
                    <span className="font-medium">{runtimeText(`itemType.${i.itemType}`)}</span>
                    {i.itemType === 'Project' ? <span>{i.name}</span>
                      : <button id={`files-item-${i.itemType}-${i.itemId}`} type="button" className="text-left text-sm text-foreground hover:underline" onClick={event => openPanel(i.itemType, i.itemId, peopleOn ? captureRecordContextOrigin(location, event.currentTarget) : undefined)}><Key>{i.key}</Key> {i.name}</button>}
                  </p>
                  <ul className="space-y-1">
                    {i.links.map((l) => {
                      const author = l.addedByUser
                      const context = liveScreen ? contexts.get(l.id) : undefined
                      const parts = t('files.added', { name: '\uFFFC', date: fmtDate(l.addedAt.slice(0, 10)) }).split('\uFFFC')
                      const knownAuthor = author?.id && author.id !== '00000000-0000-0000-0000-000000000000' && author.displayName.trim() && author.displayName === l.addedBy && parts.length === 2
                      return (
                        <li key={l.id} data-file-link={l.id} className="flex min-h-8 min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                          <LinkAnchor l={l} />
                          <span className="text-xs/[18px] text-muted-foreground">{runtimeText(`linkType.${l.linkType}`)}</span>
                          <span className="ml-auto text-xs/[18px] text-muted-foreground tabular-nums">{knownAuthor ? <>{parts[0]}<PersonPanelTrigger userId={author.id} name={author.displayName} active={author.isActive} focusKey={`files:${p.projectId}:${i.itemType}:${i.itemId}:link:${l.id}:added-by`}>{l.addedBy}</PersonPanelTrigger>{parts[1]}</> : t('files.added', { name: l.addedBy ?? '', date: fmtDate(l.addedAt.slice(0, 10)) })}</span>
                          {context && <div className="w-full min-w-0 space-y-1 break-words">
                            <LinkReferenceSummary context={context.referenceContext} focusKey={`files:${p.projectId}:${i.itemType}:${i.itemId}:link:${l.id}:reference`} />
                            {context.canChange && <Button id={`file-reference-${l.id}`} size="sm" variant="ghost" onClick={() => setEditing({ viewer: me.id, scope: scope.api, link: { id: l.id, title: l.title, rowVersion: context.rowVersion, referenceContext: context.referenceContext } })}>{t('files.editReference')}</Button>}
                          </div>}
                        </li>
                      )})}
                  </ul>
                </li>
              ))}
            </ul>
          )}
        </Section>
      ))}
      {editor && <LinkReferenceEditor key={`${editor.viewer}:${editor.scope}:${editor.link.id}`} link={editor.link} onClose={closeEditor} onDone={() => { void invalidateChange(qc, 'DocumentLink'); closeEditor() }} />}
      {peopleOn && data && liveScreen && <Section id="files-missing" title={t('files.missingTitle')} count={liveScreen.missingDeliverables.total}>
        <p className="border-b px-5 py-3 text-sm text-muted-foreground">{t('files.missingHint')}</p>
        {liveScreen.missingDeliverables.truncated && <div className="px-5 py-3"><Notice title={t('ws.truncatedTitle')}>{t('files.missingTruncated', { shown: liveScreen.missingDeliverables.items.length, total: liveScreen.missingDeliverables.total })}</Notice></div>}
        {liveScreen.missingDeliverables.items.length === 0 ? <Empty>{t('files.missingNone')}</Empty> : <ul className="divide-y">
          {liveScreen.missingDeliverables.items.map(item => <li key={item.id} data-missing-deliverable={item.id} className="min-w-0 px-5 py-3">
            <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <Link className="key text-muted-foreground hover:underline" to={`/projects/${item.projectNumber}`}>{item.projectNumber}</Link>
              <button id={`files-missing-${item.id}`} type="button" className="min-w-0 text-left hover:underline" onClick={event => openPanel('Deliverable', item.id, captureRecordContextOrigin(location, event.currentTarget))}><Key>{item.key}</Key> {item.name}</button>
              <span className="text-xs text-muted-foreground">{item.disciplineName} · {tv(item.status)}</span>
              <span className="text-xs text-muted-foreground">{item.currentRevision ? t('files.currentRevision', { revision: item.currentRevision }) : t('files.currentRevisionUnknown')}</span>
              {item.canAddLink && missingForm?.id !== item.id && <Button size="sm" variant="outline" className="sm:ml-auto" onClick={() => setAddingMissing({ viewer: me.id, scope: scope.api, id: item.id })}><Plus className="size-4" />{t('links.add')}</Button>}
            </div>
            {item.canAddLink && missingForm?.id === item.id && <div className="mt-3"><AddLink type="Deliverable" id={item.id} onDone={() => setAddingMissing(null)} /></div>}
          </li>)}
        </ul>}
      </Section>}
    </Page>
  )
}
