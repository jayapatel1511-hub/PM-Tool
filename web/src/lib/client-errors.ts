import { ApiError } from '@/lib/api'

export type ClientErrorKind = 'render' | 'error' | 'unhandled rejection'
export type ClientError = {
  Reference: string
  Route: string
  Build?: string
  Message: string
  Kind: ClientErrorKind
  Name: string
  StackFrame?: string
}

type Sender = (body: ClientError) => Promise<void>
let sender: Sender | null = null
let routePattern = () => '/'
let installed = false
const sent = new Set<string>()
let count = 0

export function configureClientErrorReporter(options: { send: Sender; route?: () => string }) {
  sender = options.send
  if (options.route) routePattern = options.route
}

export function setClientErrorRoute(route: () => string) { routePattern = route }

export function disableClientErrorReporter() { sender = null }

function scrub(value: unknown, max = 200) {
  let text = typeof value === 'string' ? value : ''
  // Redact URLs, quoted strings, IDs and control characters before sending.
  text = text.replace(/"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'/gs, '…')
    .replace(/https?:\/\/[^\s]+/gi, '…')
    .replace(/\b[0-9a-f]{8}-[0-9a-f-]{27,}\b/gi, '…')
    .replace(/\b\d{4,}\b/g, '…')
    .split('').filter(character => character.charCodeAt(0) >= 32 && character.charCodeAt(0) !== 127).join('')
    .replace(/\s+/g, ' ').trim()
  return text.slice(0, max)
}

function frame(stack: unknown) {
  const line = typeof stack === 'string' ? stack.split('\n').find(v => /\(?[^():\s]+:\d+:\d+\)?/.test(v)) : ''
  const match = line?.match(/(?:\(|\s)([^()\s]+):(\d+):(\d+)\)?$/)
  const file = match?.[1].split(/[?#]/)[0].split('/').pop()
  return match && file ? `${scrub(file, 100)}:${match[2]}:${match[3]}` : undefined
}

export function clientErrorReference(route = routePattern()) {
  const build = import.meta.env.VITE_HUB_BUILD
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'
  const random = typeof crypto !== 'undefined' && crypto.getRandomValues
    ? [...crypto.getRandomValues(new Uint8Array(6))].map(n => alphabet[n % alphabet.length]).join('')
    : 'XXXXXX'
  return [scrub(route, 200), scrub(build, 64), random].filter(Boolean).join(' · ')
}

export function reportClientError(input: { reference?: string; kind: ClientErrorKind; error?: unknown; message?: string }) {
  if (!sender || count >= 5) return
  const error = input.error instanceof Error ? input.error : undefined
  if ((input.error instanceof ApiError && input.error.status < 500) || error?.name === 'AbortError') return
  const message = scrub(input.message ?? error?.message ?? (typeof input.error === 'string' ? input.error : 'Unknown error'))
  const body: ClientError = {
    Reference: input.reference ?? clientErrorReference(), Route: scrub(routePattern(), 200),
    Build: scrub(import.meta.env.VITE_HUB_BUILD, 64), Message: message,
    Kind: input.kind, Name: scrub(error?.name ?? 'Error', 80), StackFrame: frame(error?.stack),
  }
  const key = body.Message
  if (sent.has(key)) return
  sent.add(key); count += 1
  void sender(body).catch(() => {})
}

export function installClientErrorListeners() {
  if (installed || typeof window === 'undefined') return
  installed = true
  window.addEventListener('error', event => reportClientError({ kind: 'error', error: event.error, message: event.message }))
  window.addEventListener('unhandledrejection', event => reportClientError({ kind: 'unhandled rejection', error: event.reason }))
}
