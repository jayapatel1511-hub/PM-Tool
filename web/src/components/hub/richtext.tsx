import { Fragment, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

// Descriptions and comments allow bold, italic, lists, links, inline code and @mentions (FR-TSK-01, §12.8); rendered as
// React nodes, never as HTML, and never images.
const INLINE = /(@\[[^\]\n]{1,120}\]\([0-9a-fA-F-]{36}\)|\*\*[^*]+\*\*|`[^`\n]+`|\*[^*\s][^*]*\*|_[^_\s][^_]*_|\[[^\]]+\]\(https?:\/\/[^\s)]+\)|https?:\/\/[^\s<]+)/g
const link = 'text-primary underline underline-offset-4'

function inline(text: string): ReactNode[] {
  return text.split(INLINE).map((part, i) => {
    const mention = /^@\[([^\]]+)\]\(([0-9a-fA-F-]{36})\)$/.exec(part)
    if (mention) return <span key={i} className="rounded bg-accent px-1 font-medium text-accent-foreground">@{mention[1]}</span>
    if (part.length > 4 && part.startsWith('**') && part.endsWith('**')) return <strong key={i}>{part.slice(2, -2)}</strong>
    if (part.length > 2 && part.startsWith('`') && part.endsWith('`')) return <code key={i} className="rounded bg-muted px-1 font-mono text-[0.9em]">{part.slice(1, -1)}</code>
    if (part.length > 2 && ((part.startsWith('*') && part.endsWith('*')) || (part.startsWith('_') && part.endsWith('_')))) return <em key={i}>{part.slice(1, -1)}</em>
    const md = /^\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)$/.exec(part)
    if (md) return <a key={i} href={md[2]} target="_blank" rel="noreferrer noopener" className={link}>{md[1]}</a>
    if (/^https?:\/\//.test(part)) return <a key={i} href={part} target="_blank" rel="noreferrer noopener" className={cn(link, 'break-all')}>{part}</a>
    return <Fragment key={i}>{part}</Fragment>
  })
}

export function RichText({ text, className }: { text?: string | null; className?: string }) {
  if (!text) return null
  const blocks: ReactNode[] = []
  let list = null as { ordered: boolean; items: string[] } | null
  const flush = () => {
    if (!list) return
    const L = list.ordered ? 'ol' : 'ul'
    blocks.push(<L key={blocks.length} className={list.ordered ? 'list-decimal pl-5' : 'list-disc pl-5'}>{list.items.map((x, i) => <li key={i}>{inline(x)}</li>)}</L>)
    list = null
  }
  for (const line of text.split('\n')) {
    const item = /^\s*(?:([-*])|\d+[.)])\s+(.*)$/.exec(line)
    if (item) {
      const ordered = !item[1]
      if (list && list.ordered !== ordered) flush()
      list ??= { ordered, items: [] }
      list.items.push(item[2])
      continue
    }
    flush()
    if (line.trim()) blocks.push(<p key={blocks.length}>{inline(line)}</p>)
  }
  flush()
  return <div className={cn('space-y-1.5 break-words', className)}>{blocks}</div>
}

/** Plain text of a comment for previews: mention tokens become "@Name". */
export const plainText = (text: string) => text.replace(/@\[([^\]]+)\]\([0-9a-fA-F-]{36}\)/g, '@$1')
