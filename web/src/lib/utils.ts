import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export const ACCENTS = ['blue', 'mint', 'lavender', 'peach', 'amber', 'rose'] as const
export type Accent = (typeof ACCENTS)[number]

/** A stable identity accent derived from an id (FNV-1a), so a person or project keeps one colour across views, sorts and
 *  status changes. Render it with `data-accent` and the `--acc-*` variables from index.css. */
export function accentOf(key?: string | null): Accent {
  let h = 0x811c9dc5
  for (const c of key ?? '') h = Math.imul(h ^ c.charCodeAt(0), 0x01000193)
  return ACCENTS[(h >>> 0) % ACCENTS.length]
}
