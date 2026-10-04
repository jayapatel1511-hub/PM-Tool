// Strings for the registers screens (Tuesday visual overhaul); folded into en.ts at integration.
const strings: Record<string, string> = {
  // Page introductions, empty states, filters and paging (§13.0 empty states and filters).
  'deliverable.subtitle': 'What each discipline must produce, when it is due and how far along it is',
  'register.noMatchHint': 'Clear the filters to see every item in this register.',
  'risk.emptyHint': 'Raise a risk when something may happen that would hurt the project. Score its probability and impact, then plan the response.',
  'issue.emptyHint': 'Raise an issue when something has gone wrong and needs resolving, with one owner and a target resolution date.',
  'issue.pager': 'Issue pages',
  'decision.requiredTo': 'Required to',
  'meeting.emptyHint': 'Record a meeting to capture the actions agreed in it, each with one owner and a due date.',
  // Detail-panel field groups (design §6 Detail panel: fields grouped by purpose).
  'register.group.ownership': 'Ownership',
  'register.group.assessment': 'Assessment',
  'register.group.response': 'Response',
  'register.group.schedule': 'Schedule',
  'register.group.reviewIssue': 'Review and issue',
  'register.group.related': 'Related work',
}
export default strings
