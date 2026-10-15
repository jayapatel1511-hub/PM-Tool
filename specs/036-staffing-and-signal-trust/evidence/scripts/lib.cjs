// Shared helpers for the packet 036 acceptance browser checks (read-only unless a check says otherwise).
const { chromium } = require('playwright')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://localhost:5185'
const EVID = '/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1/specs/036-staffing-and-signal-trust/evidence'
const SCRATCH = '/private/tmp/claude-501/-Users-jaypatel-PM-Tool/f879a8c1-966b-47f2-8c07-d393d08b7d6e/scratchpad/acc/b'
const NAMES = { jordan: 'Jordan Lee', lena: 'Lena Brooks', sam: 'Sam Patel', priya: 'Priya Nair', marc: 'Marc Dubois', alex: 'Alex Chen', jill: 'Jill Martin', diane: 'Diane Roy', omar: 'Omar Haddad', rita: 'Rita Gomez' }
let browser
async function open(who, { width = 1440, height = 1000 } = {}) {
  browser ??= await chromium.launch({ headless: true })
  const ctx = await browser.newContext({ viewport: { width, height }, reducedMotion: 'reduce' })
  const page = await ctx.newPage()
  page.on('pageerror', (e) => console.log(`[pageerror ${who}]`, e.message))
  await page.goto(`${base}/login`)
  await page.getByRole('button', { name: new RegExp(NAMES[who]) }).click()
  await page.waitForURL((u) => !u.pathname.startsWith('/login'), { timeout: 20000 })
  return page
}
const api = async (who, endpoint, init = {}) => {
  const r = await fetch(`${base}/api/v1/${endpoint}`, { ...init, headers: { 'X-Dev-User': `${who}@hub.test`, 'Content-Type': 'application/json', ...(init.headers ?? {}) } })
  const t = await r.text(); let j = null; try { j = JSON.parse(t) } catch {}
  return { status: r.status, body: j ?? t }
}
const shot = async (page, name, opts = {}) => { await page.screenshot({ path: `${opts.scratch ? SCRATCH : EVID}/${name}.png`, fullPage: opts.full ?? false, ...(opts.clip ? { clip: opts.clip } : {}) }); return `${name}.png` }
const settle = (page, ms = 800) => page.waitForLoadState('networkidle').catch(() => {}).then(() => page.waitForTimeout(ms))
const close = async () => { if (browser) await browser.close() }
module.exports = { base, EVID, SCRATCH, open, api, shot, settle, close, NAMES }
