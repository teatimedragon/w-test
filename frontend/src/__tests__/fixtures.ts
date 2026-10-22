import { ApiError, type GameEvent, type Store, type Template } from '../lib/api'

export const store: Store = {
  storeId: '0199f0a0-0000-7000-8000-000000000001',
  storeName: 'Basic Game Store',
  timeZone: 'America/Los_Angeles',
}

export const event: GameEvent = {
  eventId: '01a16227-2b7c-786b-95d1-8835a3bfaf2b',
  gameType: 'Magic: The Gathering',
  date: '2026-10-29',
  time: '18:00',
  name: 'Friday Night Magic',
  capacity: 30,
  eventMinimum: 3,
  duration: 180,
  playFormats: '1v1 constructed',
  details: '3 rounds best of 3',
  registeredCount: 12,
}

export const templates: Template[] = [
  { templateId: 0, gameType: 'Magic: The Gathering', name: 'Default', defaultCapacity: 30, defaultEventMinimum: 3, defaultDuration: 180, defaultPlayFormats: '1v1 constructed', defaultDetails: '3 rounds best of 3' },
  { templateId: 1, gameType: 'Magic: The Gathering', name: 'Magic: The Gathering', defaultCapacity: 30, defaultEventMinimum: 3, defaultDuration: 180, defaultPlayFormats: '1v1 constructed', defaultDetails: '3 rounds best of 3' },
  { templateId: 2, gameType: 'Magic: The Gathering Commander', name: 'Magic: The Gathering Commander', defaultCapacity: 30, defaultEventMinimum: 6, defaultDuration: 240, defaultPlayFormats: 'pods', defaultDetails: 'swiss pairing' },
  { templateId: 3, gameType: 'Galactic Empires CCG', name: 'Galactic Empires', defaultCapacity: 30, defaultEventMinimum: 7, defaultDuration: 240, defaultPlayFormats: '7v7', defaultDetails: 'winner takes all swiss final if more than one pod' },
]

export const conflict = (reason: 'VERSION_CONFLICT' | 'EVENT_FULL', versionId: number, remainingCapacity: number) =>
  new ApiError({ status: 409, title: 'Conflict', reason, versionId, remainingCapacity })

export const notFound = () => new ApiError({ status: 404, title: 'Not found' })
