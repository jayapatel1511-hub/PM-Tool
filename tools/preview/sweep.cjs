// Integration sweep for the Tuesday overhaul: every route, as several roles, at several widths, against the running dev
// server (5173) and the preview API (5080, synthetic data). Reports page errors, axe violations, horizontal overflow,
// text under 12 px, raw i18n keys and remnants of the old navy/blue palette. Usage:
//   CHROME_EXECUTABLE_PATH=... node tools/preview/sweep.cjs [widths=1440,1024,768,375] [shots=0|1]   (needs the preview running)
const { chromium } = require('/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1/web/node_modules/playwright');
const fs = require('fs'), path = require('path'), os = require('os');
const out = process.env.SWEEP_OUT ?? path.join(os.tmpdir(), 'tuesday-sweep'); // results and screenshots stay outside the repo
fs.mkdirSync(out, { recursive: true });
const base = 'http://localhost:5173';
const widths = (process.argv[2] ?? '1440,1024,768,375').split(',').map(Number);
const shots = process.argv[3] === '1';
const axePath = '/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1/web/node_modules/axe-core/axe.min.js';
const P = 'SYN-101';
const project = ['dashboard', 'coordination', 'readiness', 'handoffs', 'reviews', 'changes', 'submissions', 'allocations', 'design-basis', 'tasks', 'board',
  'milestones', 'timeline', 'deliverables', 'decisions', 'risks', 'issues', 'meetings', 'files', 'calendar', 'team', 'activity', 'settings'].map((t) => `/projects/${P}/${t}`);
const routes = {
  priya: ['/home', '/my-work', '/my-work?tab=today', '/my-work?tab=inbox', '/boards', '/tasks', '/timeline', '/calendar', '/calendar?mode=agenda', '/files', '/team', '/time',
    '/projects', '/notifications', '/search?q=synthetic', '/preferences', '/workload', ...project],
  sam: ['/workload', '/staff', '/my-work'],
  jordan: ['/portfolio', '/reports', '/reports/tasks-due-this-week', '/templates', '/admin', '/admin/users', '/admin/settings', '/admin/activity', '/admin/operations', '/admin/holidays', '/admin/reference/disciplines'],
  alex: ['/my-work', '/workload', '/home'],
  rita: ['/my-work', '/projects', `/projects/${P}/dashboard`],
};
const OLD = ['rgb(27, 34, 48)', 'rgb(29, 85, 208)', 'rgb(37, 99, 235)', 'rgb(26, 72, 173)'];
(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  const results = [];
  for (const [who, list] of Object.entries(routes)) {
    for (const width of widths) {
      const ctx = await browser.newContext({ viewport: { width, height: 900 } });
      await ctx.addInitScript((u) => { sessionStorage.setItem('hub.devUser', `${u}@hub.test`); sessionStorage.setItem('hub.signedIn', '1') }, who);
      const page = await ctx.newPage();
      const errors = [];
      page.on('pageerror', (e) => errors.push(e.message));
      page.on('console', (m) => { if (m.type() === 'error' && !/status of 403/.test(m.text())) errors.push(m.text().slice(0, 200)) }); // a 403 is an expected access state
      for (const r of list) {
        errors.length = 0;
        await page.goto(base + r, { waitUntil: 'networkidle' }).catch((e) => errors.push(String(e)));
        await page.waitForTimeout(600);
        await page.addScriptTag({ path: axePath });
        const res = await page.evaluate(async (OLD) => {
          const vis = (el) => { const s = getComputedStyle(el); const b = el.getBoundingClientRect(); return s.visibility !== 'hidden' && s.display !== 'none' && b.width > 0 && b.height > 0 && !el.closest('.sr-only') }
          const small = [], keys = [], old = []
          const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT)
          for (let n = walker.nextNode(); n; n = walker.nextNode()) {
            const text = n.textContent.trim(); const el = n.parentElement
            if (!text || !el || !vis(el)) continue
            if (parseFloat(getComputedStyle(el).fontSize) < 12 && !/^[▲■●○✓◐▾▴·/]$/.test(text)) small.push(`${text.slice(0, 30)} (${getComputedStyle(el).fontSize})`)
            if (/^[a-z][a-zA-Z]*(\.[a-zA-Z0-9]+)+$/.test(text) && !/@|\.(test|com|pdf|dwg)$/.test(text)) keys.push(text)
          }
          for (const el of document.querySelectorAll('body *')) { if (!vis(el) || /background/.test(el.getAttribute('style') ?? '')) continue; // inline colours are data (e.g. discipline colours)
            const s = getComputedStyle(el); if (OLD.includes(s.backgroundColor) || OLD.includes(s.color)) old.push(`${el.tagName.toLowerCase()}.${String(el.className).slice(0, 40)} ${OLD.includes(s.backgroundColor) ? 'bg ' + s.backgroundColor : 'fg ' + s.color}`) }
          const axeRes = await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] }, resultTypes: ['violations'] })
          return { overflowX: document.scrollingElement.scrollWidth - innerWidth, small: [...new Set(small)].slice(0, 6), keys: [...new Set(keys)].slice(0, 6), old: [...new Set(old)].slice(0, 6),
            axe: axeRes.violations.map((v) => `${v.id}(${v.impact})x${v.nodes.length}: ${v.nodes[0].target.join(' ')}`), title: document.querySelector('main h1, main h2')?.textContent?.slice(0, 50) }
        }, OLD).catch((e) => ({ evalError: String(e).slice(0, 200) }))
        const row = { who, width, route: r, errors: [...new Set(errors)].slice(0, 4), ...res }
        results.push(row)
        if (shots) await page.screenshot({ path: path.join(out, `${who}-${width}-${r.replace(/[/?=&]+/g, '_')}.png`), fullPage: false })
        const flags = [row.errors?.length && 'errors', row.axe?.length && 'axe', row.overflowX > 0 && 'overflow', row.small?.length && 'small', row.keys?.length && 'keys', row.old?.length && 'old', row.evalError && 'eval'].filter(Boolean)
        console.log(`${flags.length ? '✗' : '✓'} ${who} ${width} ${r} ${flags.join(',')}`)
      }
      await ctx.close()
    }
  }
  fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(results, null, 2))
  await browser.close()
})().catch((e) => { console.error(e); process.exitCode = 1 })
