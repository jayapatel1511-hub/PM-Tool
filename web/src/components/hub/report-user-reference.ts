const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)

/** Only server-declared User references for this precise report column, never a name or arbitrary ID lookup. */
export function reportUserReference(row: unknown, column: string) {
  if (!object(row) || !object(row.userReferences)) return undefined
  const reference = row.userReferences[column]
  if (!object(reference) || reference.kind !== 'User' || typeof reference.id !== 'string' || !reference.id ||
    typeof reference.displayName !== 'string' || !reference.displayName.trim() || typeof reference.sourceKey !== 'string' || !reference.sourceKey) return undefined
  return { id: reference.id, name: reference.displayName, sourceKey: reference.sourceKey,
    active: typeof reference.isActive === 'boolean' ? reference.isActive : undefined }
}
