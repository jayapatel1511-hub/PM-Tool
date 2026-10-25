// Focused source contract checks for the dark Task Pickup UI.
const assert = require('node:assert/strict')
const fs = require('node:fs')

const path = (file) => fs.readFileSync(`${__dirname}/../${file}`, 'utf8')
const panel = path('src/pages/projects/TaskPanel.tsx')
const pickup = path('src/pages/Pickup.tsx')
const tasks = path('src/pages/projects/Tasks.tsx')
const myWork = path('src/pages/MyWork.tsx')
const routes = path('src/app/routes.tsx')

assert.match(panel, /useFlag\('task_pickup'\)/)
assert.match(panel, /pickup-offer/)
assert.match(panel, /pickup\.keepWatching/)
// The page is gated at its route (NotFound while the flag is off), not inside the page.
assert.match(routes, /routed\(PickupPage, 'task_pickup'\)/)
assert.match(pickup, /projectId/)
assert.match(pickup, /disciplineId/)
assert.match(pickup, /neededBy/)
assert.match(tasks, /pickupOfferOpen/)
assert.match(myWork, /\/pickup/)
console.log('task-pickup source checks: 7 passed')
