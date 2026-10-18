import { toast } from 'sonner'
import { t } from '@/lib/i18n'

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
export function configureApi(h: HeaderSource, unauthorized: () => void) { authHeaders = h; onUnauthorized = unauthorized }

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

export async function api<T = any>(path: string, o: ApiOptions = {}): Promise<T> {
  const headers: Record<string, string> = { 'X-Hub-Source': 'UI', ...(await authHeaders()) }
  if (o.body !== undefined) headers['Content-Type'] = 'application/json'
  if (o.idempotencyKey) headers['Idempotency-Key'] = o.idempotencyKey
  if (o.ifMatch != null) headers['If-Match'] = `"${o.ifMatch}"`
  const method = o.method ?? (o.body !== undefined ? 'POST' : 'GET')
  let res: Response
  try { res = await fetch(path.startsWith('/') ? path : `/api/v1/${path}`, {
    method,
    headers,
    body: o.body !== undefined ? JSON.stringify(o.body) : undefined,
    signal: o.signal,
  }) } catch (e) { if (e instanceof DOMException && e.name === 'AbortError') throw e; throw new ApiError(0, { code: 'network' }, method) }
  if (res.status === 401) { onUnauthorized(); throw new ApiError(401, { detail: 'Signed out' }) }
  if (!res.ok) {
    let body: any = {}
    try { body = await res.json() } catch { /* not JSON */ }
    throw new ApiError(res.status, body, method)
  }
  if (o.raw) return res as unknown as T
  if (res.status === 204) return undefined as T
  const data = await res.json()
  if (!o.quiet && Array.isArray(data?.warnings)) data.warnings.forEach((w: string) => toast.warning(w))
  return data as T
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
