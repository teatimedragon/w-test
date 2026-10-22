import type { Template } from '../lib/api'

/** Client-side check mirroring the server rules, so mistakes show before saving. */
export function templateProblems(t: Template, requireName = true): string[] {
  const problems: string[] = []
  if (requireName && !t.name.trim()) problems.push('Template name is required.')
  if (!t.gameType.trim()) problems.push('Game type is required.')
  if (!(t.defaultCapacity > 0)) problems.push('Capacity must be greater than 0.')
  if (!(t.defaultEventMinimum > 0) || t.defaultEventMinimum > t.defaultCapacity)
    problems.push('Event minimum must be between 1 and the capacity.')
  if (!(t.defaultDuration > 0)) problems.push('Duration must be greater than 0.')
  return problems
}
