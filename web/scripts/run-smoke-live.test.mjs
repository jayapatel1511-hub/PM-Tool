import assert from 'node:assert/strict'
import { createServer } from 'node:net'
import { spawn } from 'node:child_process'
import { once } from 'node:events'
const server = createServer().listen(0, '127.0.0.1')
await once(server, 'listening')
try {
  const child = spawn(process.execPath, [new URL('./run-smoke-live.mjs', import.meta.url).pathname], {
    env: { ...process.env, TUESDAY_SMOKE_API_PORT: String(server.address().port) }, stdio: ['ignore', 'pipe', 'pipe'],
  })
  let output = ''
  child.stdout.on('data', data => { output += data }); child.stderr.on('data', data => { output += data })
  const [code] = await once(child, 'exit')
  assert.equal(code, 1)
  assert.match(output, /already in use; refusing to attach/)
  console.log('PASS: occupied verification port fails before starting or mutating a service')
} finally { await new Promise(resolve => server.close(resolve)) }
