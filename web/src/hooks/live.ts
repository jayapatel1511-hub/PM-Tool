import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { useFlag } from '@/lib/auth-context'
import { ApiError, readEventStream } from '@/lib/api'
import { invalidateChange, refreshProject } from '@/lib/keys'
import { useProject } from '@/hooks/data'

type LiveEvent = { topic?: string; projectId?: string; itemType?: string; itemId?: string }

const RETRIES = [1_000, 2_000, 4_000, 8_000, 16_000, 30_000]
const PULSE = ['unread-pulse']

/** One authenticated stream per tab. Refreshes remain additive to the existing timers. */
export function useLiveEvents() {
  const enabled = useFlag('live_events')
  const qc = useQueryClient()
  const { pathname } = useLocation()
  const number = pathname.match(/^\/projects\/([^/]+)/)?.[1]
  const project = useProject(number)
  const openProjectId = project.data?.id
  const queryClient = useRef(qc)
  // Read through a ref so moving between projects does not close and reopen the stream.
  const openProject = useRef(openProjectId)

  useEffect(() => {
    queryClient.current = qc
  }, [qc])
  useEffect(() => {
    openProject.current = openProjectId
  }, [openProjectId])

  useEffect(() => {
    if (!enabled) return
    let stopped = false
    let running = false
    let controller: AbortController | null = null
    let reconnectTimer: ReturnType<typeof setTimeout> | undefined
    let coalesceTimer: ReturnType<typeof setTimeout> | undefined
    let pending: LiveEvent[] = []
    let retry = 0

    const flush = () => {
      coalesceTimer = undefined
      const events = pending
      pending = []
      if (!events.length) return
      const seen = new Set<string>()
      for (const event of events) {
        if (event.topic === 'project') {
          const key = `${event.itemType ?? ''}:${event.projectId ?? ''}:${event.itemId ?? ''}`
          if (seen.has(key)) continue
          seen.add(key)
          invalidateChange(queryClient.current, event.itemType, event.projectId, event.itemId)
        } else if (event.topic === 'evaluated') {
          const key = `evaluated:${event.projectId ?? ''}`
          if (seen.has(key)) continue
          seen.add(key)
          refreshProject(queryClient.current, event.projectId)
        }
      }
      void queryClient.current.invalidateQueries({ queryKey: PULSE })
    }

    const queue = (event: LiveEvent) => {
      pending.push(event)
      if (!coalesceTimer) coalesceTimer = setTimeout(flush, 300)
    }

    let releaseWait: (() => void) | undefined
    const wait = (ms: number) => new Promise<void>(resolve => {
      releaseWait = resolve
      reconnectTimer = setTimeout(() => { releaseWait = undefined; resolve() }, ms)
    })

    const connect = async () => {
      if (running || stopped || document.hidden) return
      running = true
      let reopening = false
      try {
        while (!stopped && !document.hidden) {
          controller = new AbortController()
          const openedAt = Date.now()
          let lastData = openedAt
          let first = true
          const watchdog = setInterval(() => {
            if (Date.now() - lastData >= 60_000) controller?.abort()
          }, 1_000)
          try {
            for await (const event of readEventStream('/api/v1/events', controller.signal)) {
              lastData = Date.now()
              // FR-LIVE-17: after every reopen, refresh the open project once to cover anything missed.
              if (first && reopening) refreshProject(queryClient.current, openProject.current)
              first = false
              if (event) queue(event as LiveEvent)
            }
            if (!stopped && !document.hidden) throw new Error('event stream closed')
          } catch (error) {
            if (stopped || document.hidden) break
            // A refusal (429 over the per-user cap, 401, 403) is not a dead stream: stop, and this tab keeps today's refresh (FR-LIVE-17).
            if (error instanceof ApiError && error.status >= 400 && error.status < 500) break
          } finally {
            clearInterval(watchdog)
            controller = null
          }
          if (!stopped && !document.hidden) {
            // A stream that stayed up past a minute ended normally (the server's lifetime limit); only failures back off.
            if (Date.now() - openedAt >= 60_000) retry = 0
            await wait(RETRIES[Math.min(retry, RETRIES.length - 1)])
            retry = Math.min(retry + 1, RETRIES.length - 1)
            reopening = true
          }
        }
      } finally {
        running = false
      }
    }

    const onVisibility = () => {
      if (document.hidden) {
        controller?.abort()
        if (reconnectTimer) clearTimeout(reconnectTimer)
        reconnectTimer = undefined
        releaseWait?.()
        releaseWait = undefined
        return
      }
      retry = 0
      void queryClient.current.invalidateQueries({ queryKey: PULSE })
      refreshProject(queryClient.current, openProject.current)
      void connect()
    }
    document.addEventListener('visibilitychange', onVisibility)
    void connect()
    return () => {
      stopped = true
      controller?.abort()
      if (reconnectTimer) clearTimeout(reconnectTimer)
      releaseWait?.()
      if (coalesceTimer) clearTimeout(coalesceTimer)
      document.removeEventListener('visibilitychange', onVisibility)
      flush()
    }
  }, [enabled])
}
