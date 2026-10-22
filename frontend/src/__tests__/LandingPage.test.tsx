import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../lib/api'
import { LandingPage } from '../pages/LandingPage'
import { event, store } from './fixtures'

beforeEach(() => vi.restoreAllMocks())

describe('LandingPage', () => {
  it('lists upcoming events with date, start time, minimum, seats and a Register link', async () => {
    vi.spyOn(api, 'getStore').mockResolvedValue(store)
    const listEvents = vi.spyOn(api, 'listEvents').mockResolvedValue([event])

    render(
      <MemoryRouter>
        <LandingPage />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { name: 'Friday Night Magic' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Basic Game Store' })).toHaveAttribute('href', '/admin')
    expect(screen.getByLabelText('Thu Oct 29')).toBeInTheDocument()
    expect(screen.getByText('Starts 6:00 PM')).toBeInTheDocument()
    expect(screen.getByText('Minimum 3 players')).toBeInTheDocument()
    expect(screen.getByText('12 / 30 registered')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Register' })).toHaveAttribute('href', `/registration/${event.eventId}`)
    expect(listEvents).toHaveBeenCalledWith(store.storeId)
  })
})
