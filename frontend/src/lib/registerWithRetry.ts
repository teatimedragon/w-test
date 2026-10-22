import { ApiError, type AddResult, type Api } from './api'

export const MAX_ATTEMPTS = 3

export type RegisterOutcome =
  | { kind: 'registered'; result: AddResult }
  | { kind: 'full'; versionId: number }
  | { kind: 'unsuccessful'; versionId: number; remainingCapacity?: number }
  | { kind: 'error'; error: unknown }

/**
 * The ADD flow from the spec: send ADD with the caller's versionId; on VERSION_CONFLICT retry with the
 * versionId the server returned, up to 3 attempts in total; on EVENT_FULL stop.
 */
export async function registerWithRetry(
  client: Pick<Api, 'register'>,
  eventId: string,
  name: string,
  versionId: number,
): Promise<RegisterOutcome> {
  let currentVersion = versionId
  let remainingCapacity: number | undefined
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    try {
      return { kind: 'registered', result: await client.register(eventId, currentVersion, name) }
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 409) return { kind: 'error', error }
      const { reason, versionId: latest, remainingCapacity: remaining } = error.problem
      if (reason === 'EVENT_FULL') return { kind: 'full', versionId: latest ?? currentVersion }
      currentVersion = latest ?? currentVersion
      remainingCapacity = remaining
    }
  }
  return { kind: 'unsuccessful', versionId: currentVersion, remainingCapacity }
}
