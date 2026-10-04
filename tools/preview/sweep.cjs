// Integration sweep for the Tuesday overhaul: every route, as several roles, at several widths, against the running dev
// server (5173) and the preview API (5080, synthetic data). Reports page errors, axe violations, horizontal overflow,
// text under 12 px, raw i18n keys and remnants of the old navy/blue palette. Usage:
//   CHROME_EXECUTABLE_PATH=... node tools/preview/sweep.cjs [widths=1440,1024,768,375] [shots=0|1]   (needs the preview running)
const { chromium } = require('../../web/node_modules/playwright');
const fs = require('fs'), path = require('path'), os = require('os');
const out = process.env.SWEEP_OUT ?? path.join(os.tmpdir(), 'tuesday-sweep'); // results and screenshots stay outside the repo
fs.mkdirSync(out, { recursive: true });
const base = 'http://localhost:5173';
const widths = (process.argv[2] ?? '1440,1024,768,375').split(',').map(Number);
const shots = process.argv[3] === '1';
const axePath = require.resolve('../../web/node_modules/axe-core/axe.min.js');
// All development users share the API's IP rate-limit bucket. Pace real requests across contexts;
// this keeps the audit under the existing 600/minute limit without changing security or hiding errors.
let apiQueue = Promise.resolve(), nextApiAt = 0;
const paceApi = () => {
  const turn = apiQueue.then(async () => {
    const delay = Math.max(0, nextApiAt - Date.now());
    if (delay) await new Promise(resolve => setTimeout(resolve, delay));
    nextApiAt = Date.now() + 150;
  });
  apiQueue = turn.catch(() => {});
  return turn;
};
const P = 'SYN-101';
const project = ['dashboard', 'coordination', 'readiness', 'handoffs', 'reviews', 'changes', 'submissions', 'allocations', 'design-basis', 'tasks', 'board',
  'milestones', 'timeline', 'deliverables', 'decisions', 'risks', 'issues', 'meetings', 'files', 'calendar', 'team', 'activity', 'settings'].map((t) => `/projects/${P}/${t}`);
const common = ['/home', '/my-work', '/my-work?tab=today', '/my-work?tab=inbox', '/boards', '/tasks', '/timeline', '/calendar', '/calendar?mode=agenda', '/files', '/team', '/time', '/projects', '/notifications', '/search?q=synthetic', '/preferences', '/workload', '/planner', '/staff', '/portfolio', '/reports', '/reports/tasks-due-this-week', '/templates', '/admin', '/admin/users', '/admin/settings', '/admin/activity', '/admin/operations', '/admin/holidays', '/admin/reference/disciplines', ...['clients', 'offices', 'deliverableTypes', 'phases', 'projectTypes'].map(k => `/admin/reference/${k}`), '/missing-preview-route', ...project];
const routes = Object.fromEntries(['priya', 'sam', 'lena', 'jordan', 'alex', 'rita'].map(who => [who, [...common]]));
// Include the actual seeded detail routes; no guessed identifiers or skipped route families.
async function details() {
  const read = async route => {
    const response = await fetch(`http://localhost:5080/api/v1/${route}`, { headers: { 'X-Dev-User': 'jordan@hub.test' } });
    if (!response.ok) throw new Error(`Fixture lookup ${route}: HTTP ${response.status}`);
    return response.json();
  };
  const seededProject = await read(`projects/${P}`);
  const tasks = await read(`projects/${seededProject.id}/tasks?pageSize=200`);
  const templates = await read('templates');
  const users = await read('users?limit=500');
  const reports = await read('reports');
  for (const list of Object.values(routes)) for (const report of reports) { const route = `/reports/${report.code}${report.params.some(p => p.type === 'project') ? `?projectId=${seededProject.id}` : ''}`; if (!list.includes(route)) list.push(route); }
  const task = tasks.items[0], template = templates.templates.find(x => x.status === 'Published'), draftTemplate = templates.templates.find(x => x.name?.startsWith('Synthetic preview') && x.status === 'Draft');
  if (draftTemplate) routes.jordan.push(`/templates/${draftTemplate.id}`);
  const person = users.find(x => x.email === 'alex@hub.test');
  if (!task || !template || !person) throw new Error('Required synthetic detail fixtures are missing');
  for (const list of Object.values(routes)) list.push(`/tasks/${task.id}`, `/templates/${template.id}`, `/people/${person.id}/reassign-work`);
}
// An explicit cold recheck preserves the original first pass and replaces only the measured
// route/role/width combinations. Nothing is suppressed: remaining failures still exit nonzero.
const failed = row => row.errors?.length || row.axe?.length || row.overflowX > 0 || row.menuOverflow?.length || row.small?.length || row.keys?.length || row.old?.length || row.evalError;
const prior = process.env.SWEEP_RECHECK_FROM ? JSON.parse(fs.readFileSync(process.env.SWEEP_RECHECK_FROM, 'utf8')) : null;
// Optional exact path selection also remeasures previously passing routes after a local repair.
const affectedPaths = new Set((process.env.SWEEP_RECHECK_ROUTES ?? '').split(',').filter(Boolean));
const identity = row => JSON.stringify([row.who, row.width, row.route]);
const OLD = ['rgb(27, 34, 48)', 'rgb(29, 85, 208)', 'rgb(37, 99, 235)', 'rgb(26, 72, 173)'];
(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) });
  if (!prior) await details();
  const results = [];
  const recheckRoutes = {};
  if (prior) for (const row of prior.filter(row => failed(row) || affectedPaths.has(row.route.split('?')[0]))) {
    recheckRoutes[row.who] ??= [];
    if (!recheckRoutes[row.who].includes(row.route)) recheckRoutes[row.who].push(row.route);
  }
  const work = prior ? Object.entries(recheckRoutes) : [...Object.entries(routes), ['public', ['/', '/login']]];
  let cursor = 0;
  await Promise.all(Array.from({ length: 3 }, async () => {
    while (cursor < work.length) {
    const [who, list] = work[cursor++];
    for (const width of widths) {
      const ctx = await browser.newContext({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
      await ctx.route('**/api/v1/**', async route => { await paceApi(); await route.continue(); });
      if (who !== 'public') await ctx.addInitScript((u) => { sessionStorage.setItem('hub.devUser', `${u}@hub.test`); sessionStorage.setItem('hub.signedIn', '1') }, who);
      const page = await ctx.newPage();
      const errors = [];
      page.on('pageerror', (e) => errors.push(e.message));
      page.on('console', (m) => { if (m.type() === 'error' && !/status of 403/.test(m.text())) errors.push(m.text().slice(0, 200)) }); // a 403 is an expected access state
      for (const r of list) {
        errors.length = 0;
        await page.goto(base + r, { waitUntil: 'domcontentloaded', timeout: 30000 }); await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
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
            if (/^[a-z][a-zA-Z]*(\.[a-zA-Z0-9_]+)+:?$/.test(text) && !/@|\.(test|com|pdf|dwg)$/.test(text)) keys.push(text)
          }
          for (const el of document.querySelectorAll('body *')) { if (!vis(el)) continue;
            const s = getComputedStyle(el); if (OLD.includes(s.backgroundColor) || OLD.includes(s.color)) old.push(`${el.tagName.toLowerCase()}.${String(el.className).slice(0, 40)} ${OLD.includes(s.backgroundColor) ? 'bg ' + s.backgroundColor : 'fg ' + s.color}`) }
          const axeRes = await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] }, resultTypes: ['violations'] })
          return { menuOverflow: Array.from(document.querySelectorAll('nav,[role="tablist"]')).filter(el => vis(el) && el.scrollWidth > el.clientWidth + 1).map(el => ({ label: el.getAttribute('aria-label') ?? el.getAttribute('role') ?? 'navigation', overflow: el.scrollWidth - el.clientWidth })), overflowX: Math.max(document.scrollingElement.scrollWidth - innerWidth, ...Array.from(document.querySelectorAll('main')).map(el => el.scrollWidth - el.clientWidth)), small: [...new Set(small)].slice(0, 6), keys: [...new Set(keys)].slice(0, 6), old: [...new Set(old)].slice(0, 6),
            axe: axeRes.violations.map((v) => `${v.id}(${v.impact})x${v.nodes.length}: ${v.nodes[0].target.join(' ')}`), title: document.querySelector('main h1, main h2')?.textContent?.slice(0, 50) }
        }, OLD).catch((e) => ({ evalError: String(e).slice(0, 200) }))
        const row = { who, width, route: r, errors: [...new Set(errors)].slice(0, 4), ...res }
        results.push(row)
        fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(results, null, 2))
        if (shots) await page.screenshot({ path: path.join(out, `${who}-${width}-${r.replace(/[/?=&]+/g, '_')}.png`), fullPage: false })
        const flags = [row.errors?.length && 'errors', row.axe?.length && 'axe', row.overflowX > 0 && 'overflow', row.menuOverflow?.length && 'menu-scroll', row.small?.length && 'small', row.keys?.length && 'keys', row.old?.length && 'old', row.evalError && 'eval'].filter(Boolean)
        console.log(`${flags.length ? '✗' : '✓'} ${who} ${width} ${r} ${flags.join(',')}`)
      }
      await ctx.close()
    }
  }
  }));
  let finalResults = results;
  if (prior) {
    fs.writeFileSync(path.join(out, 'rechecks.json'), JSON.stringify(results, null, 2));
    fs.writeFileSync(path.join(out, 'first-pass.json'), JSON.stringify(prior, null, 2));
    const replacements = new Map(results.map(row => [identity(row), row]));
    finalResults = prior.map(row => replacements.get(identity(row)) ?? row);
    console.log(`Cold rechecks: ${results.length}; first-pass flags: ${prior.filter(failed).length}. Original evidence retained in first-pass.json.`);
  }
  fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(finalResults, null, 2));
  await browser.close();
  const failures = finalResults.filter(failed);
  console.log(`${finalResults.length} combinations; ${failures.length} failed; results at ${out}`);
  if (failures.length) process.exitCode = 1
})().catch((e) => { console.error(e); process.exitCode = 1 })
