#!/usr/bin/env node
// AC-LIVE-18's "before" SPA: today's web/ source without packet 042's live client, built into <outDir>. Two in-memory
// edits, nothing on disk changes: Shell.tsx loses `useLiveEvents()`, and useUnread gets back the plain pulse it had at
// 46dc7685^ (no project id, no project stamp). Any other drift fails the build. Usage: node web/scripts/build-before-live.mjs <outDir>
import { realpathSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { build } from 'vite'

if (!process.argv[2]) throw new Error('usage: build-before-live.mjs <outDir>')
const web = realpathSync(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..'))
const outDir = path.resolve(process.argv[2])

const BEFORE_UNREAD = `export function useUnread() {
  const qc = useQueryClient()
  const pulse = useQuery({ queryKey: ['unread-pulse'], queryFn: () => typedRequest('get', '/api/v1/me/notifications/pulse'), refetchInterval: 4_000 })
  const stamp = pulse.data?.stamp
  const seen = useRef(stamp)
  useEffect(() => {
    if (!stamp || seen.current === stamp) return
    if (seen.current) { qc.invalidateQueries({ queryKey: ['notifications'] }); qc.invalidateQueries({ queryKey: ['following'] }) }
    seen.current = stamp
  }, [stamp, qc])
  return useQuery({ queryKey: ['unread', stamp], queryFn: () => typedRequest('get', '/api/v1/me/notifications/unread-count'),
    enabled: pulse.isFetched, placeholderData: keepPreviousData })
}
`
function replaceOnce(code, from, to, file) {
  const hits = typeof from === 'string' ? code.split(from).length - 1 : (code.match(new RegExp(from, 'g')) ?? []).length
  if (hits !== 1) throw new Error(`${file}: expected one ${from}, found ${hits}`)
  return code.replace(from, to)
}
const edits = {
  'src/app/Shell.tsx': (code, file) => replaceOnce(replaceOnce(code, "import { useLiveEvents } from '@/hooks/live'\n", '', file), '  useLiveEvents()\n', '', file),
  'src/components/hub/notifications.tsx': (code, file) => replaceOnce(code, /export function useUnread\(\) \{\n[\s\S]*?\n\}\n/, BEFORE_UNREAD, file),
}
const edited = new Set()
await build({
  root: web, configFile: path.join(web, 'vite.config.ts'), logLevel: 'warn', build: { outDir, emptyOutDir: true },
  plugins: [{
    name: 'before-live-client', enforce: 'pre',
    transform(code, id) {
      const file = path.relative(web, id.split('?')[0])
      if (!edits[file]) return null
      edited.add(file)
      return { code: edits[file](code, file), map: null }
    },
  }],
})
const missed = Object.keys(edits).filter(file => !edited.has(file))
if (missed.length) throw new Error(`not edited: ${missed.join(', ')}`)
console.log(`before build (no live client) in ${outDir}`)
