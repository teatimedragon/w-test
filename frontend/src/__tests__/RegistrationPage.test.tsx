import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../lib/api'
import { RegistrationPage } from '../pages/RegistrationPage'
import { conflict, event, notFound } from './fixtures'

function renderPage(remainingCapacity = 18, versionId = 13) {
  vi.spyOn(api, 'getEvent').mockResolvedValue(event)
  vi.spyOn(api, 'getCapacity').mockResolvedValue({ remainingCapacity, versionId })
  render(
    <MemoryRouter initialEntries={[`/registration/${event.eventId}`]}>
      <Routes>
        <Route path="/" element={<p>Landing page</p>} />
        <Route path="/registration/:eventId" element={<RegistrationPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

async function submitName(name = 'Alice') {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('Player name'), name)
  await user.click(screen.getByRole('button', { name: 'Submit' }))
}

beforeEach(() => {
  vi.restoreAllMocks()
  window.__APP_CONFIG__ = { appBaseUrl: 'https://events.example.com/' }
})

describe('RegistrationPage', () => {
  it('shows the event, a QR link built from APP_BASE_URL, and an .ics calendar download', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: 'Friday Night Magic' })).toBeInTheDocument()
    expect(screen.getByText(/Thursday, October 29, 2026 · 6:00 PM – 9:00 PM/)).toBeInTheDocument()
    const url = `https://events.example.com/registration/${event.eventId}`
    expect(screen.getByRole('link', { name: url })).toHaveAttribute('href', url)
    expect(screen.getByTitle('QR code for this registration page')).toBeInTheDocument()

    const calendar = screen.getByRole('link', { name: 'Add to Google Calendar (.ics)' })
    expect(calendar).toHaveAttribute('href', `/event-manager/events/${event.eventId}/calendar.ics`)
    expect(calendar).toHaveAttribute('download')
  })

  it('disables Submit and shows "Event Full" when remainingCapacity is 0', async () => {
    renderPage(0, 31)

    const button = await screen.findByRole('button', { name: 'Event Full' })
    expect(button).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Submit' })).not.toBeInTheDocument()
  })

  it('registers with the loaded versionId and returns to the landing page', async () => {
    renderPage()
    const register = vi.spyOn(api, 'register').mockResolvedValue({ name: 'Alice', registrationId: 'r1', versionId: 14, remainingCapacity: 17 })

    await submitName('  Alice ')

    expect(await screen.findByText('Landing page')).toBeInTheDocument()
    expect(register).toHaveBeenCalledWith(event.eventId, 13, 'Alice')
  })

  it('retries on VERSION_CONFLICT using the returned versionId', async () => {
    renderPage()
    const register = vi
      .spyOn(api, 'register')
      .mockRejectedValueOnce(conflict('VERSION_CONFLICT', 15, 16))
      .mockRejectedValueOnce(conflict('VERSION_CONFLICT', 16, 15))
      .mockResolvedValueOnce({ name: 'Alice', registrationId: 'r1', versionId: 17, remainingCapacity: 14 })

    await submitName()

    expect(await screen.findByText('Landing page')).toBeInTheDocument()
    expect(register.mock.calls.map((c) => c[1])).toEqual([13, 15, 16])
  })

  it('shows "Registration unsuccessful, try again." after 3 failed attempts', async () => {
    renderPage()
    const register = vi.spyOn(api, 'register').mockRejectedValue(conflict('VERSION_CONFLICT', 20, 10))

    await submitName()

    expect(await screen.findByRole('alert')).toHaveTextContent('Registration unsuccessful, try again.')
    expect(register).toHaveBeenCalledTimes(3)
    expect(screen.queryByText('Landing page')).not.toBeInTheDocument()
  })

  it('stops and shows "Event Full" when the server answers EVENT_FULL', async () => {
    renderPage(1, 13)
    const register = vi.spyOn(api, 'register').mockRejectedValue(conflict('EVENT_FULL', 14, 0))

    await submitName()

    expect(await screen.findByRole('button', { name: 'Event Full' })).toBeDisabled()
    expect(register).toHaveBeenCalledTimes(1)
  })

  it('shows "Event not found" for an unknown or past event', async () => {
      vi.spyOn(api, 'getEvent').mockRejectedValue(notFound())
    vi.spyOn(api, 'getCapacity').mockRejectedValue(notFound())
    render(
      <MemoryRouter initialEntries={['/registration/nope']}>
        <Routes>
          <Route path="/registration/:eventId" element={<RegistrationPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { name: 'Event not found' })).toBeInTheDocument()
  })

  it('keeps Submit disabled until a name is entered', async () => {
    renderPage()

    await waitFor(() => expect(screen.getByRole('button', { name: 'Submit' })).toBeDisabled())
  })
})
