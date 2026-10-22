export interface Store {
  storeId: string
  storeName: string
  timeZone: string
}

/** date is store-local "YYYY-MM-DD", time is store-local "HH:mm", duration is minutes. */
export interface GameEvent {
  eventId: string
  gameType: string
  date: string
  time: string
  name: string
  capacity: number
  eventMinimum: number
  duration: number
  playFormats: string
  details: string
  registeredCount: number
}

export type NewEvent = Omit<GameEvent, 'eventId' | 'registeredCount'>

export interface Capacity {
  remainingCapacity: number
  versionId: number
}

export interface Template {
  templateId?: number
  gameType: string
  name: string
  defaultCapacity: number
  defaultEventMinimum: number
  defaultDuration: number
  defaultPlayFormats: string
  defaultDetails: string
}

export interface AddResult {
  name: string
  registrationId: string
  versionId: number
  remainingCapacity: number
}

export type ConflictReason = 'VERSION_CONFLICT' | 'EVENT_FULL'

/** RFC 9457 Problem Details, with the extension fields this API adds. */
export interface Problem {
  status: number
  title?: string
  detail?: string
  errors?: Record<string, string[]>
  reason?: ConflictReason
  versionId?: number
  remainingCapacity?: number
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: Problem

  constructor(problem: Problem) {
    super(problem.title ?? `Request failed with status ${problem.status}`)
    this.status = problem.status
    this.problem = problem
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { Accept: 'application/json', ...(init?.body ? { 'Content-Type': 'application/json' } : {}) },
  })
  const text = await response.text()
  const body = text ? JSON.parse(text) : undefined
  if (!response.ok) throw new ApiError({ status: response.status, ...(body ?? {}) })
  return body as T
}

export const api = {
  getStore: (storeId: string) => request<Store>(`/store-manager/stores/${storeId}`),
  listEvents: (storeId: string) => request<GameEvent[]>(`/event-manager/stores/${storeId}/events`),
  createEvent: (storeId: string, event: NewEvent) =>
    request<GameEvent>(`/event-manager/stores/${storeId}/events`, { method: 'POST', body: JSON.stringify(event) }),
  getEvent: (eventId: string) => request<GameEvent>(`/event-manager/events/${eventId}`),
  getCapacity: (eventId: string) => request<Capacity>(`/event-manager/events/${eventId}/capacity`),
  register: (eventId: string, versionId: number, name: string) =>
    request<AddResult>('/event-manager/register', {
      method: 'POST',
      body: JSON.stringify({ eventId, action: 'ADD', versionId, name }),
    }),
  listTemplates: (storeId: string) => request<Template[]>(`/event-manager/stores/${storeId}/templates`),
  saveTemplate: (storeId: string, template: Template) =>
    request<Template>(`/event-manager/stores/${storeId}/templates`, { method: 'PUT', body: JSON.stringify(template) }),
}

export type Api = typeof api

/** First field error message, or the title, for showing a failed request to the user. */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const firstFieldError = error.problem.errors && Object.values(error.problem.errors)[0]?.[0]
    return firstFieldError ?? error.problem.detail ?? error.message
  }
  return 'Something went wrong. Please try again.'
}
