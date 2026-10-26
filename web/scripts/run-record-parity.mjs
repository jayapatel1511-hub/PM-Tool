// Packet 043 S01 (FR-REC-09): builds a base commit and the checked-out commit in disposable directories, serves
// both against one owned synthetic Development API and database, and runs web/tests/record-parity.cjs.
// It refuses occupied ports and a dirty src/ or web/, so the candidate build is exactly HEAD.
//   TUESDAY_RECORD_BASE_REF (required)  the commit to compare against, e.g. HEAD^1 on a pull request
//   TUESDAY_RECORD_API_URL, TUESDAY_RECORD_BASE_PORT, TUESDAY_RECORD_CANDIDATE_PORT  local service addresses
//   TUESDAY_RECORD_SCREENS, TUESDAY_RECORD_FLAGS  passed to the harness
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { execFileSync, spawn } from 'node:child_process'
import { createServer } from 'node:net'
import { once } from 'node:events'

const root = path.resolve(import.meta.dirname, '../..')
const baseRef = process.env.TUESDAY_RECORD_BASE_REF
const api = process.env.TUESDAY_RECORD_API_URL ?? 'http://127.0.0.1:18502'
const basePort = Number(process.env.TUESDAY_RECORD_BASE_PORT ?? 19503)
const headPort = Number(process.env.TUESDAY_RECORD_CANDIDATE_PORT ?? 19502)
const output = path.join(root, 'ci-artifacts/record-parity')
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'record-parity-'))
const processes = []

function start(command, args, label, options) {
  const log = fs.openSync(path.join(output, `${label}.log`), 'w')
  const child = spawn(command, args, { ...options, env: { ...process.env, ...options.env }, detached: true, stdio: ['ignore', log, log] })
  processes.push(child)
  return child
}
async function exited(child, label) {
  const [code] = await once(child, 'exit')
  if (code !== 0) throw new Error(`${label} failed (${code}); see ${output}/${label}.log`)
}
async function ready(url, child, label) {
  const deadline = Date.now() + 120000
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`${label} exited (${child.exitCode}); see ${output}/${label}.log`)
    try { if ((await fetch(url, { signal: AbortSignal.timeout(2000) })).ok) return } catch { /* still starting */ }
    await new Promise(resolve => setTimeout(resolve, 250))
  }
  throw new Error(`${label} did not become ready: ${url}`)
}
async function requireFreePort(port) {
  const server = createServer()
  try { server.listen(port, '127.0.0.1'); await once(server, 'listening') }
  catch { throw new Error(`record parity port ${port} is already in use; refusing to attach to another service`) }
  finally { if (server.listening) await new Promise(resolve => server.close(resolve)) }
}
function stop() {
  for (const child of processes.splice(0)) { try { process.kill(-child.pid, 'SIGTERM') } catch (error) { if (error.code !== 'ESRCH') throw error } }
  fs.rmSync(temp, { recursive: true, force: true })
}

// A commit's tree with its own copy of the dependencies (the TypeScript build cache lives in node_modules).
async function build(ref, label) {
  const target = path.join(temp, label)
  fs.mkdirSync(target)
  execFileSync('tar', ['-x', '-C', target], { input: execFileSync('git', ['archive', ref], { cwd: root, maxBuffer: 512 * 1024 * 1024 }) })
  const config = fs.readFileSync(path.join(target, 'web/vite.config.ts'), 'utf8')
  if (!/preview:\s*\{\s*proxy/.test(config) || !config.includes('HUB_API_ORIGIN')) throw new Error(`${label} (${ref}) preview does not proxy HUB_API_ORIGIN; refusing an uncontrolled API`)
  fs.cpSync(fs.realpathSync(path.join(root, 'web/node_modules')), path.join(target, 'web/node_modules'), { recursive: true })
  await exited(start('npm', ['run', 'build'], `build-${label}`, { cwd: path.join(target, 'web') }), `build-${label}`)
  return target
}

async function main() {
  if (!baseRef) throw new Error('set TUESDAY_RECORD_BASE_REF to the commit to compare against')
  const apiUrl = new URL(api)
  if (!['127.0.0.1', 'localhost'].includes(apiUrl.hostname)) throw new Error('record parity API must be local')
  if (execFileSync('git', ['status', '--porcelain', '--', 'src', 'web'], { cwd: root, encoding: 'utf8' }).trim()) throw new Error('src/ and web/ must be clean: the API and the candidate build both come from HEAD')
  const refs = { base: execFileSync('git', ['rev-parse', `${baseRef}^{commit}`], { cwd: root, encoding: 'utf8' }).trim(), candidate: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim() }
  await requireFreePort(Number(apiUrl.port)); await requireFreePort(basePort); await requireFreePort(headPort)
  fs.mkdirSync(output, { recursive: true })
  console.log(`record parity: base ${refs.base}, candidate ${refs.candidate}; logs in ${output}`)
  const apiEnv = {
    ASPNETCORE_ENVIRONMENT: 'Development', Auth__Mode: 'Development',
    ConnectionStrings__Hub: `Host=localhost;Port=55432;Database=hub_record_${process.pid}_${Date.now()};Username=hub`,
    Seed__DevUsers: 'true', Seed__ReviewDemo: 'false', Jobs__Enabled: 'false',
  }
  const dotnet = ['run', '--project', 'src/Hub.Api', '-c', 'Release', '--no-launch-profile']
  await exited(start('dotnet', dotnet, 'initialize', { cwd: root, env: { ...apiEnv, Db__MigrateOnly: 'true', Db__InitializeAuditChains: 'true' } }), 'initialize')
  const backend = start('dotnet', [...dotnet, '--urls', api], 'api', { cwd: root, env: { ...apiEnv, Evaluation__Worker: 'true', RateLimit__PerMinute: '100000' } })
  const [baseDir, headDir] = [await build(refs.base, 'base'), await build(refs.candidate, 'candidate')]
  await ready(`${api}/health`, backend, 'api')
  const urls = {}
  for (const [label, dir, port] of [['base', baseDir, basePort], ['candidate', headDir, headPort]]) {
    const preview = start(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', String(port), '--strictPort'], `preview-${label}`, { cwd: path.join(dir, 'web'), env: { HUB_API_ORIGIN: api } })
    urls[label] = `http://127.0.0.1:${port}`
    await ready(urls[label], preview, `preview-${label}`)
  }
  const harness = spawn(process.execPath, ['web/tests/record-parity.cjs'], { cwd: root, stdio: 'inherit', env: {
    ...process.env, TUESDAY_SYNTHETIC_API: '1', TUESDAY_RECORD_API_URL: api, TUESDAY_RECORD_BASE_UI_URL: urls.base, TUESDAY_RECORD_CANDIDATE_UI_URL: urls.candidate,
    TUESDAY_RECORD_PARITY_OUT: process.env.TUESDAY_RECORD_PARITY_OUT ?? path.join(output, 'report.json'),
  } })
  const [code] = await once(harness, 'exit')
  if (code !== 0) throw new Error(`record parity failed (${code}); report and screenshots in ${output}`)
}

process.once('SIGINT', () => { stop(); process.exit(130) })
process.once('SIGTERM', () => { stop(); process.exit(143) })
try { await main() } catch (error) { console.error(`record parity runner: ${error.message}`); process.exitCode = 1 } finally { stop() }
