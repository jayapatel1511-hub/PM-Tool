#!/usr/bin/env node
import { gzipSync } from 'node:zlib'
import { readFile, writeFile } from 'node:fs/promises'
import path from 'node:path'

const root = path.resolve(process.argv[2] ?? 'src/Hub.Api/wwwroot')
const output = path.resolve(process.argv[3] ?? path.join(root, 'chunk-report.json'))
const manifest = JSON.parse(await readFile(path.join(root, '.vite/manifest.json'), 'utf8'))
const cache = new Map()
async function size(file) {
  if (!cache.has(file)) {
    const bytes = await readFile(path.join(root, file))
    cache.set(file, { bytes: bytes.byteLength, gzipBytes: gzipSync(bytes, { level: 9 }).byteLength })
  }
  return cache.get(file)
}
const rows = []
for (const [entry, chunk] of Object.entries(manifest)) {
  const files = new Set()
  const visit = key => {
    if (files.has(key)) return
    files.add(key)
    for (const imported of manifest[key]?.imports ?? []) visit(imported)
  }
  visit(entry)
  const sizes = await Promise.all([...files].map(file => size(manifest[file]?.file ?? file)))
  rows.push({
    entry,
    file: chunk.file,
    imports: [...files].filter(file => file !== entry).map(file => manifest[file]?.file ?? file),
    bytes: sizes.reduce((sum, value) => sum + value.bytes, 0),
    gzipBytes: sizes.reduce((sum, value) => sum + value.gzipBytes, 0),
  })
}
rows.sort((a, b) => b.gzipBytes - a.gzipBytes || a.file.localeCompare(b.file))
await writeFile(output, JSON.stringify({ generatedAt: new Date().toISOString(), chunks: rows }, null, 2) + '\n')
console.log(`wrote ${rows.length} transitive entry sizes to ${output}`)
