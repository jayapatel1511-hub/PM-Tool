// Run from any directory; mutate the compiler host only, never maintained source.
import assert from 'node:assert/strict'
import ts from '../../../web/node_modules/typescript/lib/typescript.js'
import { resolve } from 'node:path'
const root = resolve(import.meta.dirname, '../../../web')
const configPath = resolve(root, 'tsconfig.app.json')
const config = ts.parseJsonConfigFileContent(ts.parseConfigFileTextToJson(configPath, ts.sys.readFile(configPath)).config, ts.sys, root)
const recordsPath = resolve(root, 'src/components/hub/records.tsx')
const source = ts.sys.readFile(recordsPath)
const missing = source.replace(/^  RoleStatus:.*\n/m, '')
assert.notEqual(missing, source, 'negative control must remove an actual renderer')
function compile(override) {
  const host = ts.createCompilerHost(config.options), read = host.readFile.bind(host)
  host.getCurrentDirectory = () => root
  host.readFile = file => override && resolve(file) === recordsPath ? missing : read(file)
  return ts.getPreEmitDiagnostics(ts.createProgram(config.fileNames, config.options, host))
}
const positive = compile(false), negative = compile(true)
assert.equal(positive.length, 0, 'the unchanged actual application must compile')
assert.ok(negative.some(d => d.file?.fileName === recordsPath && ts.flattenDiagnosticMessageText(d.messageText, ' ').includes("Property 'RoleStatus' is missing")), 'removing the real map member must fail the application build')
console.log(JSON.stringify({ result: 'PASS', positiveDiagnostics: positive.length, missingRenderer: 'RoleStatus', negativeDiagnosticCodes: negative.map(d => d.code), maintainedFilesWritten: 0 }, null, 2))
