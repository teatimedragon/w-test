import { describe, expect, it, vi } from 'vitest'
import { registerWithRetry } from '../lib/registerWithRetry'
import { conflict } from './fixtures'

const added = { name: 'Alice', registrationId: 'r1', versionId: 9, remainingCapacity: 4 }

describe('registerWithRetry (section 4 ADD flow)', () => {
  it('retries VERSION_CONFLICT with the versionId the server returned', async () => {
    const register = vi.fn().mockRejectedValueOnce(conflict('VERSION_CONFLICT', 8, 5)).mockResolvedValueOnce(added)

    const outcome = await registerWithRetry({ register }, 'e1', 'Alice', 7)

    expect(outcome).toEqual({ kind: 'registered', result: added })
    expect(register.mock.calls).toEqual([
      ['e1', 7, 'Alice'],
      ['e1', 8, 'Alice'],
    ])
  })

  it('gives up after 3 attempts in total', async () => {
    const register = vi
      .fn()
      .mockRejectedValueOnce(conflict('VERSION_CONFLICT', 2, 5))
      .mockRejectedValueOnce(conflict('VERSION_CONFLICT', 3, 4))
      .mockRejectedValueOnce(conflict('VERSION_CONFLICT', 4, 3))

    const outcome = await registerWithRetry({ register }, 'e1', 'Alice', 1)

    expect(outcome).toEqual({ kind: 'unsuccessful', versionId: 4, remainingCapacity: 3 })
    expect(register).toHaveBeenCalledTimes(3)
  })

  it('stops immediately on EVENT_FULL', async () => {
    const register = vi.fn().mockRejectedValueOnce(conflict('VERSION_CONFLICT', 2, 1)).mockRejectedValueOnce(conflict('EVENT_FULL', 3, 0))

    const outcome = await registerWithRetry({ register }, 'e1', 'Alice', 1)

    expect(outcome).toEqual({ kind: 'full', versionId: 3 })
    expect(register).toHaveBeenCalledTimes(2)
  })

  it('does not retry other errors', async () => {
    const error = new Error('network down')
    const register = vi.fn().mockRejectedValue(error)

    expect(await registerWithRetry({ register }, 'e1', 'Alice', 1)).toEqual({ kind: 'error', error })
    expect(register).toHaveBeenCalledTimes(1)
  })
})
