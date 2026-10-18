// Packet 046 browser runner. Behaviour is verified by the mocked Chromium suite.
const assert = require('node:assert/strict')
const { spawnSync } = require('node:child_process')
const path = require('node:path')

const browser = spawnSync(process.execPath, [path.join(__dirname, 'interaction-browser.cjs')], { stdio: 'inherit', env: process.env })
assert.equal(browser.status, 0, 'packet 046 mocked Chromium interaction suite')
