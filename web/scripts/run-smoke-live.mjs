// Required web-job real-API check. Every invocation creates its own synthetic database.
import { spawn } from 'node:child_process'
import { createWriteStream, mkdirSync } from 'node:fs'
import path from 'node:path'
import { once } from 'node:events'
import { createServer } from 'node:net'

const root = path.resolve(import.meta.dirname, '../..')
const apiPort = Number(process.env.TUESDAY_SMOKE_API_PORT ?? 18502)
const uiPort = Number(process.env.TUESDAY_SMOKE_UI_PORT ?? 19502)
const api = `http://127.0.0.1:${apiPort}`, ui = `http://127.0.0.1:${uiPort}`
const database = `hub_smoke_${process.pid}_${Date.now()}`
const output = path.join(root, 'ci-artifacts/smoke-live')
mkdirSync(output, { recursive: true })
const processes = []
function start(command, args, label, env) {
  const log = createWriteStream(path.join(output, `${label}.log`))
  const child = spawn(command, args, { cwd: root, env: { ...process.env, ...env }, detached: true, stdio: ['ignore', 'pipe', 'pipe'] })
  child.stdout.pipe(log, { end: false }); child.stderr.pipe(log, { end: false }); child.once('close', () => log.end()); processes.push(child)
  return child
}
async function ready(url, child) {
  const deadline = Date.now() + 120000
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`verification service exited (${child.exitCode}); see ${output}`)
    try { if ((await fetch(url, { signal: AbortSignal.timeout(2000) })).ok) return } catch {}
    await new Promise(resolve => setTimeout(resolve, 200))
  }
  throw new Error(`verification service did not become ready: ${url}`)
}
function stop() { for (const child of processes) { try { process.kill(-child.pid, 'SIGTERM') } catch (error) { if (error.code !== 'ESRCH') throw error } } }
async function requireFreePort(port) {
  const server = createServer()
  try { server.listen(port, '127.0.0.1'); await once(server, 'listening') }
  catch { throw new Error(`verification port ${port} is already in use; refusing to attach to another service`) }
  finally { if (server.listening) await new Promise(resolve => server.close(resolve)) }
}
process.once('SIGINT', stop); process.once('SIGTERM', stop)
try {
  await requireFreePort(apiPort); await requireFreePort(uiPort)
  const initializer = start('dotnet', ['run', '--project', 'src/Hub.Api', '-c', 'Release', '--no-launch-profile'], 'initialize', {
    ASPNETCORE_ENVIRONMENT: 'Development', Auth__Mode: 'Development',
    ConnectionStrings__Hub: `Host=localhost;Port=55432;Database=${database};Username=hub`,
    Seed__DevUsers: 'true', Seed__ReviewDemo: 'false', Jobs__Enabled: 'false',
    Db__MigrateOnly: 'true', Db__InitializeAuditChains: 'true',
  })
  const [initialized] = await once(initializer, 'exit')
  if (initialized !== 0) throw new Error(`synthetic migration initializer failed (${initialized}); see ${output}/initialize.log`)
  const backend = start('dotnet', ['run', '--project', 'src/Hub.Api', '-c', 'Release', '--no-launch-profile', '--urls', api], 'api', {
    ASPNETCORE_ENVIRONMENT: 'Development', Auth__Mode: 'Development',
    ConnectionStrings__Hub: `Host=localhost;Port=55432;Database=${database};Username=hub`,
    Seed__DevUsers: 'true', Seed__ReviewDemo: 'false', Jobs__Enabled: 'false', Evaluation__Worker: 'true', RateLimit__PerMinute: '100000',
  })
  await ready(`${api}/health`, backend)
  const health = await (await fetch(`${api}/health`)).json()
  if (process.env.HUB_BUILD && health.build !== process.env.HUB_BUILD) throw new Error(`unexpected health build: ${health.build}`)
  console.log(JSON.stringify({ health, database, api, ui }))
  const preview = start(process.execPath, ['web/node_modules/vite/bin/vite.js', 'preview', 'web', '--config', 'web/vite.config.ts', '--host', '127.0.0.1', '--port', String(uiPort), '--strictPort'], 'preview', { HUB_API_ORIGIN: api })
  await ready(ui, preview)
  for (const [file, label] of [['smoke-live.cjs', 'browser'], ['live-real.cjs', 'live-browser'], ['saved-views-real.cjs', 'saved-views-browser'], ['interaction-real.cjs', 'interaction-browser']]) {
    const test = start(process.execPath, [`web/tests/${file}`], label, {
      TUESDAY_UI_BASE_URL: ui, TUESDAY_API_BASE_URL: api, TUESDAY_LIVE_UI_URL: ui, TUESDAY_LIVE_API_URL: api, TUESDAY_SYNTHETIC_API: '1',
    })
    const [code] = await once(test, 'exit')
    if (code !== 0) throw new Error(`real-API ${label} failed (${code}); see ${output}/${label}.log`)
  }
  console.log(`PASS: owned synthetic real-API smoke, two-browser live flow, saved-view reopen and packet 046 interaction check; evidence ${output}`)
} catch (error) { console.error(error.message); process.exitCode = 1 } finally { stop() }
