/* Boot registration source guard for FR-LIVE-05/08. Browser boot coverage remains in the preview suites. */
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..', 'src')
const registry = path.join(root, 'app', 'registrations.tsx')
const names = ['PANELS', 'PROJECT_ROUTES', 'ItemSlots', 'ShellSlots', 'ProjectSlots', 'ADMIN_EXTRA', 'CREATE_SOURCES', 'SETTINGS_SECTIONS']
const files = []
function walk(dir) {
  for (const name of fs.readdirSync(dir)) {
    const file = path.join(dir, name)
    if (fs.statSync(file).isDirectory()) walk(file)
    else if (/\.(tsx?|jsx?)$/.test(name)) files.push(file)
  }
}
walk(root)
const offenders = []
function hasMutation(source, name) {
  // Drop declarations before scanning so the canonical slot definitions are not mistaken for writes.
  const body = source.replace(new RegExp(`(?:export\\s+)?(?:const|let|var)\\s+${name}\\b`, 'g'), '')
  return new RegExp(`\\b${name}(?:(?:\\.[A-Za-z_$][\\w$]*|\\[[^\\]]+\\])\\s*=|\\s*=|\\.(?:push|unshift|splice)\\s*\\()`).test(body)
}
for (const file of files) {
  if (file === registry) continue
  const source = fs.readFileSync(file, 'utf8')
  for (const name of names) if (hasMutation(source, name)) offenders.push(path.relative(process.cwd(), file) + `: ${name}`)
}
if (offenders.length) throw new Error(`registry mutation outside registrations.tsx:\n${offenders.join('\n')}`)
const source = fs.readFileSync(registry, 'utf8')
const template = source.indexOf('CREATE_SOURCES.unshift(templateSource)')
const copy = source.indexOf('CREATE_SOURCES.push(copySource)')
if (template < 0 || copy < 0 || template > copy) throw new Error('template source must precede copy source')
for (const expected of ['PANELS.Task =', 'ItemSlots.Comments =', 'ShellSlots.Search =', 'ProjectSlots.Health =',
  'ADMIN_EXTRA.push(', 'CREATE_SOURCES.unshift(templateSource)', 'CREATE_SOURCES.push(copySource)', 'SETTINGS_SECTIONS.push(']) {
  if (!source.includes(expected)) throw new Error(`missing eager registration: ${expected}`)
}
// Seed nested and whole-object writes so the guard cannot regress to line-anchored matching.
for (const [name, sample] of [
  ['PANELS', 'function f(){ PANELS.Risk = {} }'],
  ['PANELS', 'function f(){ PANELS = {} }'],
  ['PROJECT_ROUTES', 'function f(){ PROJECT_ROUTES.push({}) }'],
  ['CREATE_SOURCES', 'function f(){ CREATE_SOURCES.unshift({})'],
  ['CREATE_SOURCES', 'function f(){ CREATE_SOURCES.splice(0, 1) }'],
]) {
  if (!hasMutation(sample, name)) throw new Error(`registration guard fixture failed: ${sample}`)
}
console.log('registrations: PASS')
