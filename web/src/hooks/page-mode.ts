import { useEffect } from 'react'
import { useSearchParams } from 'react-router'
import { migratePageMode, pageMode } from '@/lib/page-mode'

export function usePageMode(key: 'tab' | 'layout', modes: readonly string[], fallback: string) {
  const [params, setParams] = useSearchParams()
  useEffect(() => {
    const next = migratePageMode(params, key, modes)
    if (next) setParams(next, { replace: true })
  }, [params, setParams, key, modes])
  return pageMode(params, key, modes, fallback)
}
