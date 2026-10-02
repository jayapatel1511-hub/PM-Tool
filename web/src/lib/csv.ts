/** One client-built CSV cell, by the server's `Export.Csv` rules (src/Hub.Api/Infrastructure/Export.cs): text a spreadsheet
 *  would run as a formula (leading = + - @ tab or carriage return) gets a leading apostrophe while numbers stay numbers. It
 *  also quotes a field holding any separator a spreadsheet may split on (comma, semicolon, tab), a quote or a line break.
 *  A formula behind leading spaces is guarded too, so this is never weaker than the server rule. */
export function csvCell(value: unknown): string {
  let text = String(value ?? '')
  if (/^(\s*[=+\-@]|[\t\r])/.test(text) && !/^\s*[+-]?(\d+\.?\d*|\.\d+)(e[+-]?\d+)?\s*$/i.test(text)) text = `'${text}`
  return /[,;"\t\n\r]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text
}
