import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ManageTemplates } from '../admin/ManageTemplates'
import { api } from '../lib/api'
import { store, templates } from './fixtures'

beforeEach(() => {
  vi.restoreAllMocks()
  vi.spyOn(api, 'listTemplates').mockResolvedValue(templates)
})

describe('ManageTemplates', () => {
  it('preselects template 0 (Default)', async () => {
    render(<ManageTemplates />)

    expect(await screen.findByRole('radio', { name: /^Default/ })).toBeChecked()
    expect(screen.getByRole('button', { name: 'Update Template' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create New Template' })).toBeInTheDocument()
  })

  it('Update Template edits the selected template and saves it under its templateId', async () => {
    const user = userEvent.setup()
    const save = vi.spyOn(api, 'saveTemplate').mockImplementation(async (_, t) => t)
    render(<ManageTemplates />)

    await user.click(await screen.findByRole('radio', { name: /Galactic Empires/ }))
    await user.click(screen.getByRole('button', { name: 'Update Template' }))
    expect(screen.getByLabelText('Template name')).toHaveValue('Galactic Empires')
    await user.clear(screen.getByLabelText('Default capacity'))
    await user.type(screen.getByLabelText('Default capacity'), '28')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(save).toHaveBeenCalledWith(store.storeId, { ...templates[3], defaultCapacity: 28 })
    expect(await screen.findByRole('status')).toHaveTextContent('Saved "Galactic Empires".')
  })

  it('Create New Template copies the selected values, then asks for a name before saving', async () => {
    const user = userEvent.setup()
    const save = vi.spyOn(api, 'saveTemplate').mockImplementation(async (_, t) => ({ ...t, templateId: 4 }))
    render(<ManageTemplates />)

    await user.click(await screen.findByRole('radio', { name: /Magic: The Gathering Commander/ }))
    await user.click(screen.getByRole('button', { name: 'Create New Template' }))

    // Copied values, editable, with Continue (no name yet).
    expect(screen.queryByLabelText('Template name')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Default play formats')).toHaveValue('pods')
    await user.clear(screen.getByLabelText('Default event minimum'))
    await user.type(screen.getByLabelText('Default event minimum'), '4')
    await user.click(screen.getByRole('button', { name: 'Continue' }))

    // Name step with Save.
    await user.type(screen.getByLabelText('Template name'), 'Commander League')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(save).toHaveBeenCalledWith(store.storeId, {
      ...templates[2],
      templateId: undefined,
      name: 'Commander League',
      defaultEventMinimum: 4,
    })
    expect(await screen.findByRole('status')).toHaveTextContent('Created template "Commander League".')
  })

  it('requires a name for a new template', async () => {
    const user = userEvent.setup()
    const save = vi.spyOn(api, 'saveTemplate')
    render(<ManageTemplates />)

    await user.click(await screen.findByRole('button', { name: 'Create New Template' }))
    await user.click(screen.getByRole('button', { name: 'Continue' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Template name is required.')
    expect(save).not.toHaveBeenCalled()
  })
})
