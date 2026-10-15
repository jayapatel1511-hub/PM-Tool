/** Alternating label rows stop before the next label on their own row. Full names stay on the milestone buttons. */
export function milestoneLabelLayout(anchors: number[], width: number) {
  return anchors.map((at, i) => {
    const left = Math.max(0, at + 14)
    const end = i + 2 < anchors.length ? Math.max(0, anchors[i + 2] + 14) - 8 : width
    return { left, top: i % 2 === 0 ? 31 : 53, maxWidth: Math.max(0, Math.min(240, width - left, end - left)) }
  })
}
