// Preload for web/tests/*.cjs (node -r): also writes every console.table, with the log line before it as its heading,
// and the FAIL lines to the JSON file named by TABLE_JSON. The test itself is unchanged.
const fs = require('node:fs')
// NODE_OPTIONS also reaches npm itself; only the test process writes the file.
if (/[\\/]tests[\\/][^\\/]+\.cjs$/.test(process.argv[1] ?? '')) {
const out = { tables: [], errors: [] }
let heading = ''
const { log, table, error } = console
console.log = (...a) => { const s = a.join(' ').trim(); if (s) heading = s; log(...a) }
console.table = (rows, ...a) => { out.tables.push({ heading, rows }); table(rows, ...a) }
console.error = (...a) => { out.errors.push(a.map(String).join(' ')); error(...a) }
process.on('exit', (code) => fs.writeFileSync(process.env.TABLE_JSON, JSON.stringify({ exitCode: process.exitCode ?? code, ...out }, null, 2)))
}
