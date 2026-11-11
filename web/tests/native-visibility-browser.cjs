// Playwright normally forces every page visible (coreBundle: setFocusEmulationEnabled).
// An isolated default Chrome context plus documented noDefaults restores real tab visibility.
const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')
const { spawn } = require('node:child_process')
const { chromium } = require('playwright')

async function nativeVisibilityBrowser() {
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'pm-native-visibility-'))
  // The headless shell never hides a background tab, so a CHROME_EXECUTABLE_PATH naming it cannot serve this helper.
  const configured = process.env.CHROME_EXECUTABLE_PATH
  const executable = configured && !/headless[_-]shell/.test(configured) ? configured : '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
  const child = spawn(executable, [
    '--headless=new', '--window-size=1440,1100', '--no-first-run', '--no-default-browser-check', '--disable-background-networking',
    '--disable-component-update', '--disable-sync', '--no-pings', '--remote-debugging-address=127.0.0.1',
    '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank',
  ], { stdio: 'ignore' })
  let spawnError
  child.once('error', error => { spawnError = error })
  const cleanup = async () => {
    if (child.exitCode === null && child.signalCode === null && !spawnError) {
      const exited = new Promise(resolve => child.once('exit', resolve))
      child.kill('SIGTERM')
      let timer
      try {
        await Promise.race([exited, new Promise(resolve => { timer = setTimeout(() => { child.kill('SIGKILL'); resolve() }, 3000) })])
        if (child.exitCode === null && child.signalCode === null) await exited
      } finally { clearTimeout(timer) }
    }
    fs.rmSync(profile, { recursive: true, force: true }) // Only this helper's freshly created synthetic profile.
  }
  try {
    const portFile = path.join(profile, 'DevToolsActivePort')
    // Chrome may create the file before writing it: wait for the complete "port\n" line.
    const published = () => { try { return /^\d+\n/.test(fs.readFileSync(portFile, 'utf8')) } catch { return false } }
    for (let attempt = 0; attempt < 200 && !published() && !spawnError && child.exitCode === null; attempt++)
      await new Promise(resolve => setTimeout(resolve, 50))
    if (spawnError) throw spawnError
    if (!published()) throw new Error('Isolated Chrome did not publish its loopback endpoint')
    const port = Number(fs.readFileSync(portFile, 'utf8').split('\n')[0])
    if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Invalid isolated Chrome port')
    const browser = await chromium.connectOverCDP('http://127.0.0.1:' + port, { noDefaults: true })
    const disconnect = browser.close.bind(browser)
    let closed = false
    browser.close = async () => {
      if (closed) return
      closed = true
      try { await disconnect() } finally { await cleanup() }
    }
    return browser
  } catch (error) { await cleanup(); throw error }
}

module.exports = { nativeVisibilityBrowser }
