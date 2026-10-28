import type { paths } from '@/gen/api'
import { toast } from 'sonner'
import { t } from '@/lib/i18n'
import { keepUnsent, leave, reloadAnswered } from '@/lib/drafts'

/** RFC 9457 problem returned by the API (§25.6). */
export class ApiError extends Error {
  status: number
  code: string
  body: Record<string, any>
  constructor(status: number, body: Record<string, any>, method = 'GET') {
    const write = !['GET', 'HEAD', 'OPTIONS'].includes(method.toUpperCase())
    const fallback = status === 502 || status === 503 ? t(write ? 'error.updatingWrite' : 'error.updatingRead')
      : status === 0 || status === 504 ? t(write ? 'error.unreachableWrite' : 'error.unreachableRead') : `HTTP ${status}`
    const detail = body?.detail ?? fallback
    super(status >= 500 && body?.traceId ? `${detail} (ref ${body.traceId})` : detail)
    this.status = status
    this.code = body?.code ?? 'error'
    this.body = body ?? {}
  }
  get fieldErrors(): Record<string, string[]> { return this.body.errors ?? {} }
}

// FR-OPS-19: network reads retain the existing two retries; application refusals do not.
export const shouldRetryRead = (n: number, e: unknown) => !(e instanceof ApiError && e.status > 0 && e.status < 500) && n < 2

type HeaderSource = () => Promise<Record<string, string>>
let authHeaders: HeaderSource = async () => ({})
let onUnauthorized: () => void = () => {}
// FR-SAV-05: whichever request meets the expired sign-in first, typed input is kept before the sign-in flow takes the page.
export function configureApi(h: HeaderSource, unauthorized: () => void) { authHeaders = h; onUnauthorized = () => { keepUnsent(); unauthorized() } }

/** Build embedded in the SPA by Vite; an unset value is the local development build. */
export const HUB_BUILD = (import.meta.env.VITE_HUB_BUILD as string | undefined) || 'dev'
const BUILD_NOTICE_KEY = 'hub.buildNotice'
let observedBuilds = new Set<string>()
try {
  const saved = JSON.parse(sessionStorage.getItem(BUILD_NOTICE_KEY) ?? '[]')
  if (Array.isArray(saved)) observedBuilds = new Set(saved.filter((x): x is string => typeof x === 'string'))
} catch { /* storage can be unavailable in a restricted browser context */ }

function observeBuild(res: Response) {
  const serverBuild = res.headers.get('X-Hub-Build')
  if (!serverBuild || serverBuild === 'dev' || HUB_BUILD === 'dev' || serverBuild === HUB_BUILD) return
  if (observedBuilds.has(serverBuild)) return
  observedBuilds.add(serverBuild)
  try {
    sessionStorage.setItem(BUILD_NOTICE_KEY, JSON.stringify([...observedBuilds]))
  } catch { /* storage can be unavailable in a restricted browser context */ }
  toast.info(t('app.updated'), { duration: Infinity, action: { label: t('common.reload'), onClick: (event) => { event.preventDefault(); leave(reloadAnswered) } } }) // FR-SAV-06: typed input is asked about or drafted first; Keep editing leaves the notice up
}

function requestHeaders(extra?: Record<string, string>) {
  return authHeaders().then(h => ({ 'X-Hub-Source': 'UI', ...h, ...extra }))
}

export interface ApiOptions {
  method?: string
  body?: unknown
  ifMatch?: number | null
  signal?: AbortSignal
  raw?: boolean
  quiet?: boolean
  /** Packet 034 creates retain one key across a retry. */
  idempotencyKey?: string
}

/** FR-SAV-02: an autosave field watches the write its save makes, even when the panel's save catches the refusal itself, and can send
 *  that write on another row version (Keep mine, a retry). Only a write that starts synchronously inside `send` is watched. */
export type WriteWatch = {
  error?: ApiError
  /** The row version the write goes on: the field's, else filled in with the caller's, so a refused write is retried on the version it went on. */
  ifMatch?: number
  /** The row version the server answered a saved write with. */
  saved?: number
  /** FR-SAV-05: the write was refused because sign-in expired; runs before the sign-in flow does. */
  signedOut?: () => void
}
let watching: WriteWatch | undefined
export function watchWrite<T>(watch: WriteWatch, send: () => T): T {
  const outer = watching
  watching = watch
  try { return send() } finally { watching = outer }
}

export async function api<T = any>(path: string, o: ApiOptions = {}): Promise<T> {
  const method = o.method ?? (o.body !== undefined ? 'POST' : 'GET')
  // Read before the first await, while the watching save is still on the stack.
  const watch = method === 'GET' ? undefined : watching
  if (watch && o.ifMatch != null) watch.ifMatch ??= o.ifMatch
  const fail = (e: ApiError) => { if (watch) watch.error = e; return e }
  const headers: Record<string, string> = await requestHeaders()
  if (o.body !== undefined) headers['Content-Type'] = 'application/json'
  if (o.idempotencyKey) headers['Idempotency-Key'] = o.idempotencyKey
  if (o.ifMatch != null) headers['If-Match'] = `"${watch?.ifMatch ?? o.ifMatch}"`
  let res: Response
  try { res = await fetch(path.startsWith('/') ? path : `/api/v1/${path}`, {
    method,
    headers,
    body: o.body !== undefined ? JSON.stringify(o.body) : undefined,
    signal: o.signal,
  }) } catch (e) { if (e instanceof DOMException && e.name === 'AbortError') throw e; throw fail(new ApiError(0, { code: 'network' }, method)) }
  observeBuild(res)
  if (res.status === 401) { watch?.signedOut?.(); onUnauthorized(); throw fail(new ApiError(401, { detail: 'Signed out' })) }
  if (!res.ok) {
    let body: any = {}
    try { body = await res.json() } catch { /* not JSON */ }
    throw fail(new ApiError(res.status, body, method))
  }
  if (o.raw) return res as unknown as T
  if (res.status === 204) return undefined as T
  const data = await res.json()
  if (watch && typeof data?.rowVersion === 'number') watch.saved = data.rowVersion
  if (!o.quiet && Array.isArray(data?.warnings)) data.warnings.forEach((w: string) => toast.warning(w))
  return data as T
}

/** Read the authenticated fetch-based event stream; callers own cancellation and reconnect policy. */
export async function* readEventStream(path = '/api/v1/events', signal?: AbortSignal): AsyncGenerator<Record<string, unknown> | null> {
  const res = await fetch(path.startsWith('/') ? path : `/api/v1/${path}`, { headers: await requestHeaders({ Accept: 'text/event-stream' }), signal })
  observeBuild(res)
  if (res.status === 401) { onUnauthorized(); throw new ApiError(401, { detail: 'Signed out' }) }
  if (!res.ok) {
    let body: any = {}
    try { body = await res.json() } catch { /* not JSON */ }
    throw new ApiError(res.status, body)
  }
  if (!res.body) throw new Error('Event stream has no body')
  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let pending = ''
  try {
    while (true) {
      const next = await reader.read()
      pending += decoder.decode(next.value ?? new Uint8Array(), { stream: !next.done })
      const frames = pending.split('\n\n')
      pending = frames.pop() ?? ''
      for (const frame of frames) {
        const data = frame.split('\n').filter(line => line.startsWith('data:')).map(line => line.slice(5).trimStart()).join('\n')
        if (data) yield JSON.parse(data) as Record<string, unknown>
        else if (frame.split('\n').some(line => line.startsWith(':'))) yield null
      }
      if (next.done) break
    }
  } finally {
    reader.releaseLock()
  }
}

export const get = <T = any>(path: string, signal?: AbortSignal) => api<T>(path, { signal })
export const post = <T = any>(path: string, body: unknown = {}, ifMatch?: number | null) => api<T>(path, { method: 'POST', body, ifMatch })
export const patch = <T = any>(path: string, body: unknown, ifMatch?: number | null) => api<T>(path, { method: 'PATCH', body, ifMatch })
export const put = <T = any>(path: string, body: unknown) => api<T>(path, { method: 'PUT', body })
export const del = <T = any>(path: string, body?: unknown, ifMatch?: number | null) => api<T>(path, { method: 'DELETE', body, ifMatch })

/** Builds a query string, skipping empty values; arrays become comma-separated (§25.5). */
export function qs(p: Record<string, unknown>): string {
  const u = new URLSearchParams()
  for (const [k, v] of Object.entries(p)) {
    if (v === undefined || v === null || v === '' || (Array.isArray(v) && v.length === 0) || v === false) continue
    u.set(k, Array.isArray(v) ? v.join(',') : String(v))
  }
  const s = u.toString()
  return s ? `?${s}` : ''
}

/** Downloads a file from the API with the caller's credentials. */
export async function download(path: string, fallbackName: string) {
  const res = await api<Response>(path, { raw: true })
  const blob = await res.blob()
  const name = /filename="?([^";]+)"?/.exec(res.headers.get('content-disposition') ?? '')?.[1] ?? fallbackName
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = name
  a.click()
  URL.revokeObjectURL(a.href)
}


type HttpMethod = 'get' | 'post' | 'patch' | 'put' | 'delete'
type PathsFor<M extends HttpMethod> = { [P in keyof paths]: M extends keyof paths[P] ? paths[P][M] extends undefined ? never : P : never }[keyof paths]
type Operation<M extends HttpMethod, P extends PathsFor<M>> = M extends keyof paths[P] ? paths[P][M] : never
type ParametersOf<O, K extends string> = O extends { parameters: infer P } ? K extends keyof P ? Exclude<P[K], undefined> : never : never
type JsonBody<O> = O extends { requestBody: { content: { 'application/json': infer B } } } ? B : unknown
type JsonReply<R> = R extends { content: { 'application/json': infer B } } ? B : void
type Reply<O> = O extends { responses: infer R } ? JsonReply<R[Extract<keyof R, 200 | 201 | 202 | 204>]> : never
type TypedOptions<O> = Omit<ApiOptions, 'method' | 'body'> & {
  query?: ParametersOf<O, 'query'>
  body?: JsonBody<O>
} & ([ParametersOf<O, 'path'>] extends [never] ? { params?: never } : { params: ParametersOf<O, 'path'> })

/** Generated contract, existing transport: authentication, source, version and retry headers stay in api(). */
export function typedRequest<M extends HttpMethod, P extends PathsFor<M>>(method: M, path: P, options: TypedOptions<Operation<M, P>>): Promise<Reply<Operation<M, P>>> {
  const { params, query, ...rest } = options
  let url = String(path)
  for (const [key, value] of Object.entries(params ?? {})) url = url.replace(`{${key}}`, encodeURIComponent(String(value)))
  return api(url + qs(query ?? {}), { ...rest, method: method.toUpperCase() })
}
