import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { ErrorBanner, Loading, Page } from '@/components/hub/common'
import { useScope } from '@/components/hub/workspace'
import { get, qs } from '@/lib/api'

type Item = { id: string; key: string; title: string; status: string }
type Handoff = Item & { sendingOwnerId: string; receivingOwnerId: string; sendingDisciplineId: string; receivingDisciplineId: string }
type Group = { handoffId: string; handoffKey: string; taskIds: string[]; taskKeys: string[] }
type Startability = { id: string; targetType: 'Task' | 'Deliverable'; targetId: string; key: string; name: string;
  dueDate: string | null; state: string; blocked: string[]; unknown: string[] }
type LinkedAction = { id: string; key: string; text: string; status: string; dueDate: string | null;
  sourceType: 'Handoff' | 'ChangeNotice'; sourceId: string }
type UnavailableChangeTarget = { changeNoticeId: string; count: number }
type ProjectProjection = { id: string; projectNumber: string; name: string; disciplineId: string | null; data: {
  handoffs: Handoff[]; outgoing: Handoff[]; incoming: Handoff[]; changes: Item[]; reviews: Item[];
  linkedIssues: Item[]; uses: { id: string }[]; blockerGroups: Group[];
  startability: Startability[]; startabilityReadyTotal: number; startabilityFrom: string; startabilityTo: string;
  linkedActions: LinkedAction[]; unavailableChangeTargets: UnavailableChangeTarget[]
} }
type Choice = { id: string; name?: string; projectNumber?: string; displayName?: string }
type Projection = { evaluatedAt: string; projects: ProjectProjection[]; projectChoices: Choice[];
  disciplines: Choice[]; owners: Choice[] }

function csvCell(value: unknown) {
  let text = String(value ?? '')
  if (/^\s*[-=+@]/.test(text) && !/^[+-]?\d+(\.\d+)?$/.test(text)) text = `'${text}`
  return /[,"\n\r]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text
}

function downloadCsv(projection: Projection) {
  const lines = ['evaluatedAt,project,section,id,key,title,status']
  const add = (number: string, section: string, id: string, key: string, title: string, status: string) =>
    lines.push([projection.evaluatedAt, number, section, id, key, title, status].map(csvCell).join(','))
  for (const project of projection.projects) {
    const number = project.projectNumber, rows = project.data
    for (const row of rows.outgoing) add(number, 'Outgoing Handoff', row.id, row.key, row.title, row.status)
    for (const row of rows.incoming) add(number, 'Incoming Handoff', row.id, row.key, row.title, row.status)
    for (const row of rows.changes) add(number, 'Change', row.id, row.key, row.title, row.status)
    for (const row of rows.reviews) add(number, 'Review', row.id, row.key, row.title, row.status)
    for (const row of rows.linkedIssues) add(number, 'Issue', row.id, row.key, row.title, row.status)
    for (const row of rows.uses) add(number, 'InputUse', row.id, '', '', '')
    for (const group of rows.blockerGroups) add(number, 'BlockerGroup', group.handoffId, group.handoffKey, group.taskKeys.join('; '), '')
    for (const row of rows.startability) add(number, 'Startability', row.targetId, row.key,
      `${row.name}${row.blocked.length ? ` · blocked: ${row.blocked.join('; ')}` : ''}${row.unknown.length ? ` · unknown: ${row.unknown.join('; ')}` : ''}`, row.state)
    for (const row of rows.linkedActions) add(number, 'Linked Action', row.id, row.key,
      `${row.text} · ${row.sourceType}:${row.sourceId}`, row.status)
    for (const row of rows.unavailableChangeTargets) add(number, 'Unavailable Assessment Targets', row.changeNoticeId,
      '', String(row.count), '')
  }
  const blob = new Blob([`\ufeff${lines.join('\n')}\n`], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url; anchor.download = 'discipline-coordination.csv'; document.body.append(anchor); anchor.click(); anchor.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

/** Permitted workspace projection. One server snapshot supplies each project's rows and counts. */
export function WorkspaceCoordination() {
  const [sp, setSp] = useSearchParams()
  const scope = useScope()
  const projectId = sp.get('projectId') ?? '', disciplineId = sp.get('disciplineId') ?? ''
  const ownerId = sp.get('ownerId') ?? '', from = sp.get('from') ?? '', to = sp.get('to') ?? ''
  const set = (name: string, value: string) => setSp(previous => {
    const next = new URLSearchParams(previous)
    if (value) next.set(name, value); else next.delete(name)
    return next
  }, { replace: true })
  const scopeParams = { scopeKind: scope.kind, scopeProjectIds: scope.kind === 'set' ? scope.ids?.join(',') : undefined,
    scopeWorkspaceId: scope.ws?.id }
  const q = useQuery({ queryKey: ['workspace-coordination', scope.kind, scope.api, projectId, disciplineId, ownerId, from, to],
    enabled: scope.ready,
    queryFn: () => get<Projection>(`discipline-coordination${qs({ ...scopeParams, projectId, disciplineId, ownerId, from, to })}`) })
  if (q.isPending) return <Page title="Coordination"><Loading rows={5} /></Page>
  if (q.error) return <Page title="Coordination"><div className="flex flex-wrap gap-3 rounded border p-3">
    <label>From<input className="ml-2 rounded border p-1" type="date" value={from} onChange={e => set('from', e.target.value)} /></label>
    <label>To<input className="ml-2 rounded border p-1" type="date" value={to} onChange={e => set('to', e.target.value)} /></label>
    <button type="button" className="text-primary underline" onClick={() => setSp(new URLSearchParams(), { replace: true })}>Clear filters</button>
  </div><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const data = q.data
  const item = (project: ProjectProjection, path: string, prefix: string, row: Item) =>
    <li key={row.id}><Link className="text-primary underline" to={`/projects/${project.projectNumber}/${path}?panel=${prefix}:${row.id}`}>
      {project.projectNumber} · {row.key}</Link> · {row.title} · {row.status}</li>
  const actionsFor = (project: ProjectProjection, type: LinkedAction['sourceType'], id: string) =>
    project.data.linkedActions.filter(action => action.sourceType === type && action.sourceId === id)
      .map(action => <li key={action.id}>Existing action: <Link className="text-primary underline" to={`/projects/${project.projectNumber}/meetings?panel=Action:${action.id}`}>
        {action.key}</Link> · {action.text} · {action.status}{action.dueDate && ` · due ${action.dueDate}`}</li>)
  return <Page title="Coordination" subtitle="Current coordination across permitted workspace projects">
    <p className="no-print flex gap-4"><button type="button" className="text-primary underline" onClick={() => downloadCsv(data)}>Export these evaluated records as CSV</button>
      <button type="button" className="text-primary underline" onClick={() => window.print()}>Print this view</button></p>
    <div className="no-print flex flex-wrap gap-3 rounded border p-3">
      <label>Project<select className="ml-2 rounded border p-1" value={projectId} onChange={e => set('projectId', e.target.value)}>
        <option value="">All permitted projects</option>{data.projectChoices.map(p => <option key={p.id} value={p.id}>{p.projectNumber}</option>)}
      </select></label>
      <label>Discipline<select className="ml-2 rounded border p-1" value={disciplineId} onChange={e => set('disciplineId', e.target.value)}>
        <option value="">My project disciplines</option>{data.disciplines.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
      </select></label>
      <label>Owner<select className="ml-2 rounded border p-1" value={ownerId} onChange={e => set('ownerId', e.target.value)}>
        <option value="">All owners</option>{data.owners.map(o => <option key={o.id} value={o.id}>{o.displayName}</option>)}
      </select></label>
      <label>From<input className="ml-2 rounded border p-1" type="date" value={from} onChange={e => set('from', e.target.value)} /></label>
      <label>To<input className="ml-2 rounded border p-1" type="date" value={to} onChange={e => set('to', e.target.value)} /></label>
    </div>
    <p role="status" className="text-xs text-muted-foreground">Evaluated {new Date(data.evaluatedAt).toLocaleString()} · {data.projects.length} permitted projects. Date filters apply to handoffs and startability.</p>
    {data.projects.map(project => <section key={project.id} className="space-y-3 rounded border p-4" aria-labelledby={`coord-${project.id}`}>
      <h2 id={`coord-${project.id}`} className="font-semibold">{project.projectNumber} · {project.name}</h2>
      <div className="grid gap-3 md:grid-cols-2">
        <section><h3>What do we owe? ({project.data.outgoing.length})</h3>
          <ul>{project.data.outgoing.map(h => item(project, 'handoffs', 'Handoff', h))}</ul></section>
        <section><h3>What are we waiting for? ({project.data.incoming.length})</h3>
          <ul>{project.data.incoming.map(h => item(project, 'handoffs', 'Handoff', h))}</ul>
          {project.data.blockerGroups.length > 0 && <h4>Linked task blockers</h4>}<ul>{project.data.blockerGroups.map(g =>
          <li key={g.handoffId}><Link className="text-primary underline" to={`/projects/${project.projectNumber}/handoffs?panel=Handoff:${g.handoffId}`}>{g.handoffKey}</Link>
            {' · '}{g.taskIds.length} linked tasks: {g.taskIds.map((id, index) => <span key={id}>{index > 0 && ', '}
              <Link className="text-primary underline" to={`/projects/${project.projectNumber}/tasks?panel=Task:${id}`}>{g.taskKeys[index]}</Link></span>)}
            <ul>{actionsFor(project, 'Handoff', g.handoffId)}</ul></li>)}</ul></section>
        <section><h3>Which revision are we using? ({project.data.uses.length})</h3>
          <Link className="text-primary underline" to={`/projects/${project.projectNumber}/coordination`}>Open source revisions</Link></section>
        <section><h3>What changed? ({project.data.changes.length})</h3><ul>{project.data.changes.map(c => <li key={c.id}>
          <Link className="text-primary underline" to={`/projects/${project.projectNumber}/changes?panel=ChangeNotice:${c.id}`}>
            {project.projectNumber} · {c.key}</Link> · {c.title} · {c.status}
          <ul>{actionsFor(project, 'ChangeNotice', c.id)}</ul>
          {project.data.unavailableChangeTargets.filter(target => target.changeNoticeId === c.id).map(target =>
            <p key={target.changeNoticeId} role="status">{target.count} assessment target{target.count === 1 ? '' : 's'} unavailable</p>)}
        </li>)}</ul></section>
        <section><h3>What can we start? ({project.data.startabilityReadyTotal} Ready of {project.data.startability.length} assessed)</h3>
          <p className="text-xs text-muted-foreground">Assessed work due {project.data.startabilityFrom} through {project.data.startabilityTo}; live checks evaluated with this view.</p>
          <ul>{project.data.startability.map(row => <li key={row.id}>
            <Link className="text-primary underline" to={`/projects/${project.projectNumber}/${row.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${row.targetType}:${row.targetId}`}>
              {project.projectNumber} · {row.key}</Link> · {row.name} · {row.state}
            {row.blocked.length > 0 && <span> · Blocked: {row.blocked.join(', ')}</span>}
            {row.unknown.length > 0 && <span> · Unknown: {row.unknown.join(', ')}</span>}
          </li>)}</ul>
          {project.data.startability.length === 0 && <p>No assessed work is due in this window.</p>}
          <Link className="text-primary underline" to={`/projects/${project.projectNumber}/readiness`}>Open project readiness</Link></section>
      </div>
      {project.data.reviews.length > 0 && <p>{project.data.reviews.length} review packages · <Link className="text-primary underline" to={`/projects/${project.projectNumber}/reviews`}>Open reviews</Link></p>}
      {project.data.linkedIssues.length > 0 && <p>{project.data.linkedIssues.length} linked issues · <Link className="text-primary underline" to={`/projects/${project.projectNumber}/issues`}>Open issues</Link></p>}
      <p><Link className="text-primary underline" to={`/projects/${project.projectNumber}/coordination${qs({ meeting: '1', discipline: project.disciplineId, owner: ownerId, from, to })}`}>
        Open project meeting mode to assign or update an action</Link></p>
    </section>)}
    {data.projects.length === 0 && <p>No permitted projects match these filters.</p>}
  </Page>
}
