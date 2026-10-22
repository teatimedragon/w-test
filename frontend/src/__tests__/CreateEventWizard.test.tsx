import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CreateEventWizard } from '../admin/CreateEventWizard'
import { api, ApiError } from '../lib/api'
import { AdminPage } from '../pages/AdminPage'
import { event, store, templates } from './fixtures'

// FullCalendar needs real layout; the calendar view is replaced with a stub that names its events.
vi.mock('../admin/ViewEvents', () => ({
  ViewEvents: ({ focusEvent }: { focusEvent?: { name: string } | null }) => <p>Calendar showing {focusEvent?.name}</p>,
}))

beforeEach(() => {
  vi.restoreAllMocks()
  vi.spyOn(api, 'listTemplates').mockResolvedValue(templates)
  vi.spyOn(api, 'getStore').mockResolvedValue(store)
})

const currentStep = () => screen.getByRole('heading', { level: 2 }).textContent

async function goThroughWizard(user: ReturnType<typeof userEvent.setup>) {
  // Step 1: select template.
  expect(currentStep()).toBe('Select template')
  await user.click(await screen.findByRole('radio', { name: /Magic: The Gathering Commander/ }))
  await user.click(screen.getByRole('button', { name: 'Next' }))

  // Step 2: details prefilled from the template.
  expect(currentStep()).toBe('Event details')
  expect(screen.getByLabelText('Event name')).toHaveValue('Magic: The Gathering Commander')
  expect(screen.getByLabelText('Game type')).toHaveValue('Magic: The Gathering Commander')
  expect(screen.getByLabelText('Play formats')).toHaveValue('pods')
  expect(screen.getByLabelText('Details')).toHaveValue('swiss pairing')
  await user.clear(screen.getByLabelText('Event name'))
  await user.type(screen.getByLabelText('Event name'), 'Commander Night')
  await user.click(screen.getByRole('button', { name: 'Next' }))

  // Step 3: date, time, duration (end time computed), capacity and minimum prefilled.
  expect(currentStep()).toBe('Date, time and seats')
  expect(screen.getByLabelText('Duration (minutes)')).toHaveValue(240)
  expect(screen.getByLabelText('Capacity')).toHaveValue(30)
  expect(screen.getByLabelText('Event minimum')).toHaveValue(6)
  await user.type(screen.getByLabelText('Date'), '2026-11-06')
  await user.type(screen.getByLabelText('Start time'), '21:30')
  expect(screen.getByTestId('end-time')).toHaveTextContent('Ends at 1:30 AM (Sat, Nov 7)')
  await user.click(screen.getByRole('button', { name: 'Next' }))

  // Step 4: review.
  expect(currentStep()).toBe('Review and create')
  const review = document.querySelector<HTMLElement>('dl.review')!
  expect(within(review).getByText('Commander Night')).toBeInTheDocument()
  expect(within(review).getByText(/Friday, November 6, 2026, 9:30 PM – 1:30 AM \(Sat, Nov 7\) \(4 h\)/)).toBeInTheDocument()
}

describe('CreateEventWizard', () => {
  it('walks the four steps and creates the event from the template values', async () => {
    const user = userEvent.setup()
    const onCreated = vi.fn()
    const createEvent = vi.spyOn(api, 'createEvent').mockResolvedValue({ ...event, name: 'Commander Night' })
    render(<CreateEventWizard onCreated={onCreated} />)

    await goThroughWizard(user)
    await user.click(screen.getByRole('button', { name: 'Create event' }))

    expect(createEvent).toHaveBeenCalledWith(store.storeId, {
      gameType: 'Magic: The Gathering Commander',
      name: 'Commander Night',
      playFormats: 'pods',
      details: 'swiss pairing',
      date: '2026-11-06',
      time: '21:30',
      duration: 240,
      capacity: 30,
      eventMinimum: 6,
    })
    expect(onCreated).toHaveBeenCalledWith(expect.objectContaining({ name: 'Commander Night' }))
  })

  it('selects the first template by default and blocks a step with invalid values', async () => {
    const user = userEvent.setup()
    render(<CreateEventWizard onCreated={vi.fn()} />)

    expect(await screen.findByRole('radio', { name: /^Default/ })).toBeChecked()
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.clear(screen.getByLabelText('Event name'))
    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Event name is required.')
    expect(currentStep()).toBe('Event details')

    await user.type(screen.getByLabelText('Event name'), 'FNM')
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.clear(screen.getByLabelText('Event minimum'))
    await user.type(screen.getByLabelText('Event minimum'), '31')
    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Date is required.')
    expect(screen.getByRole('alert')).toHaveTextContent('Event minimum must be between 1 and the capacity.')
    expect(currentStep()).toBe('Date, time and seats')
  })

  it('Back returns to the previous step and keeps the entered values', async () => {
    const user = userEvent.setup()
    render(<CreateEventWizard onCreated={vi.fn()} />)

    await user.click(await screen.findByRole('button', { name: 'Next' }))
    await user.type(screen.getByLabelText('Details'), ' and prizes')
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.click(screen.getByRole('button', { name: 'Back' }))

    expect(screen.getByLabelText('Details')).toHaveValue('3 rounds best of 3 and prizes')
  })

  it('shows the server error when create fails', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'createEvent').mockRejectedValue(
      new ApiError({ status: 400, title: 'Invalid', errors: { date: ['The event date and time must not be in the past.'] } }),
    )
    render(<CreateEventWizard onCreated={vi.fn()} />)

    await goThroughWizard(user)
    await user.click(screen.getByRole('button', { name: 'Create event' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The event date and time must not be in the past.')
  })
})

describe('AdminPage', () => {
  it('defaults to Create Event and switches to View Events after creating', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'createEvent').mockResolvedValue({ ...event, name: 'Commander Night' })
    render(
      <MemoryRouter>
        <AdminPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('tab', { name: 'Create Event' })).toHaveAttribute('aria-selected', 'true')
    await goThroughWizard(user)
    await user.click(screen.getByRole('button', { name: 'Create event' }))

    expect(await screen.findByText('Calendar showing Commander Night')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'View Events' })).toHaveAttribute('aria-selected', 'true')
  })
})
