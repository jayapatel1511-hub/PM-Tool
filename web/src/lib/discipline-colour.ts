// Legacy reference-data swatches use the Tuesday display palette; stored values stay intact.
export function disciplineColour(value?: string | null) {
  const legacy: Record<string, string> = {
    '#1b2230': 'var(--foreground)', '#1d55d0': 'var(--done)', '#2563eb': 'var(--done)', '#1a48ad': 'var(--done)',
    'rgb(27, 34, 48)': 'var(--foreground)', 'rgb(29, 85, 208)': 'var(--done)',
    'rgb(37, 99, 235)': 'var(--done)', 'rgb(26, 72, 173)': 'var(--done)',
  }
  return legacy[value?.toLowerCase() ?? ''] ?? value ?? 'var(--muted-foreground)'
}
