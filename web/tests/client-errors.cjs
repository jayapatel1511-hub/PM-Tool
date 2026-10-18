// AC-OPS-08, AC-OPS-11: execute the reporter with synthetic events and an in-memory sender; no network.
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path'), ts = require('typescript');
class ApiError extends Error { constructor(status, message) { super(message); this.status = status } }
function page(send) {
  const listeners = {}, module = { exports: {} };
  const source = fs.readFileSync(path.join(__dirname, '../src/lib/client-errors.ts'), 'utf8').replaceAll('import.meta.env.VITE_HUB_BUILD', '"abcdef1234567890"');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  new Function('require', 'module', 'exports', 'window', js)(() => ({ ApiError }), module, module.exports, { addEventListener: (name, listener) => { listeners[name] = listener } });
  const reporter = module.exports;
  reporter.setClientErrorRoute(() => 'projects/:number/tasks');
  if (send) reporter.configureClientErrorReporter({ send });
  reporter.installClientErrorListeners(); reporter.installClientErrorListeners();
  return { reporter, listeners };
}
(async () => {
  for (const message of ['Bad "Private\nName" value', 'Bad "https://synthetic.invalid" for "Private Name"']) {
    const privateReports = [], testPage = page(async body => { privateReports.push(body) });
    testPage.listeners.error({ error: new Error(message), message });
    assert.equal(privateReports.length, 1);
    assert.ok(!JSON.stringify(privateReports[0]).includes('Private'), 'quoted user content must be removed before URLs and controls');
  }
  for (const suffix of ['?next=/PrivateName', '#next=/PrivateName']) {
    const frameReports = [], framePage = page(async body => { frameReports.push(body) });
    const error = new Error('synthetic frame');
    error.stack = 'Error: synthetic\n at render (https://synthetic.invalid/assets/app.js'+suffix+':10:20)';
    framePage.reporter.reportClientError({kind: 'error', error});
    assert.equal(frameReports[0].StackFrame, 'app.js:10:20');
    assert.ok(!JSON.stringify(frameReports[0]).includes('PrivateName'));
  }
  const reports = [], { reporter, listeners } = page(async body => { reports.push(body) });
  const error = new Error('Expected "private synthetic name" from https://example.test/private?id=12345');
  error.stack = 'Error: synthetic\n at render (https://example.test/assets/app.js?token=synthetic:10:20)';
  const reference = reporter.clientErrorReference();
  assert.match(reference, /^projects\/:number\/tasks · abcdef1234567890 · [A-Z2-9]{6}$/);
  reporter.reportClientError({ reference, kind: 'render', error });
  reporter.reportClientError({ reference, kind: 'render', error });
  assert.equal(reports.length, 1);
  assert.equal(reports[0].Reference, reference);
  assert.equal(reports[0].Route, 'projects/:number/tasks');
  assert.equal(reports[0].Name, 'Error');
  assert.equal(reports[0].StackFrame, 'app.js:10:20');
  assert.ok(!JSON.stringify(reports[0]).includes('private synthetic name'));
  assert.ok(!JSON.stringify(reports[0]).includes('https://'));
  listeners.error({ error: new Error('second synthetic error'), message: 'second synthetic error' });
  listeners.unhandledrejection({ reason: new Error('third synthetic rejection') });
  assert.equal(reports.length, 3);
  listeners.unhandledrejection({ reason: 'third synthetic rejection' });
  assert.equal(reports.length, 3, 'same message is deduplicated across kinds');
  for (const ignored of [new ApiError(404, 'missing'), new ApiError(0, 'offline'), new DOMException('cancelled', 'AbortError')])
    listeners.unhandledrejection({ reason: ignored });
  assert.equal(reports.length, 3);
  for (let i = 0; i < 7; i++) listeners.error({ error: new Error('distinct synthetic ' + i), message: 'distinct synthetic ' + i });
  assert.equal(reports.length, 5);
  reporter.disableClientErrorReporter();
  listeners.error({ message: 'signed out' }); assert.equal(reports.length, 5);
  const unauthenticated = page(); unauthenticated.listeners.error({ message: 'anonymous synthetic error' });
  let attempts = 0;
  const failing = page(async () => { attempts++; throw new Error('synthetic report failure') });
  failing.listeners.unhandledrejection({ reason: new Error('report once') });
  failing.listeners.unhandledrejection({ reason: new Error('report once') });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(attempts, 1);
  console.log('PASS client reporter: same reference, redaction, reduced frames, events, 4xx/abort skips, dedupe, cap, auth and silent failure');
})().catch(e => { console.error(e); process.exitCode = 1 });
