import { QRCodeSVG } from 'qrcode.react'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, describeError, type GameEvent } from '../lib/api'
import { registrationUrl } from '../lib/config'
import { formatEnd, formatLongDate, formatTime, googleCalendarUrl } from '../lib/format'
import { registerWithRetry } from '../lib/registerWithRetry'
import { useStore } from '../lib/useStore'

type LoadState = { kind: 'loading' } | { kind: 'not-found' } | { kind: 'error'; message: string } | { kind: 'ready'; event: GameEvent }

export const UNSUCCESSFUL_MESSAGE = 'Registration unsuccessful, try again.'

export function RegistrationPage() {
  const { eventId = '' } = useParams()
  const navigate = useNavigate()
  const store = useStore()
  const [load, setLoad] = useState<LoadState>({ kind: 'loading' })
  const [remaining, setRemaining] = useState(0)
  const [versionId, setVersionId] = useState(0)
  const [name, setName] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    Promise.all([api.getEvent(eventId), api.getCapacity(eventId)])
      .then(([event, capacity]) => {
        if (cancelled) return
        setRemaining(capacity.remainingCapacity)
        setVersionId(capacity.versionId)
        setLoad({ kind: 'ready', event })
      })
      .catch((e) => {
        if (cancelled) return
        if (e instanceof ApiError && e.status === 404) setLoad({ kind: 'not-found' })
        else setLoad({ kind: 'error', message: describeError(e) })
      })
    return () => {
      cancelled = true
    }
  }, [eventId])

  if (load.kind === 'loading') return <main className="page"><p className="muted">Loading…</p></main>
  if (load.kind === 'not-found')
    return (
      <main className="page">
        <h1>Event not found</h1>
        <Link to="/">See upcoming events</Link>
      </main>
    )
  if (load.kind === 'error')
    return (
      <main className="page">
        <p className="error" role="alert">{load.message}</p>
      </main>
    )

  const { event } = load
  const full = remaining <= 0
  const url = registrationUrl(event.eventId)
  const trimmed = name.trim()

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!trimmed || full) return
    setSubmitting(true)
    setMessage(null)
    const outcome = await registerWithRetry(api, event.eventId, trimmed, versionId)
    setSubmitting(false)
    switch (outcome.kind) {
      case 'registered':
        navigate('/')
        return
      case 'full':
        setVersionId(outcome.versionId)
        setRemaining(0)
        return
      case 'unsuccessful':
        setVersionId(outcome.versionId)
        if (outcome.remainingCapacity !== undefined) setRemaining(outcome.remainingCapacity)
        setMessage(UNSUCCESSFUL_MESSAGE)
        return
      case 'error':
        setMessage(outcome.error instanceof ApiError && outcome.error.status === 400 ? describeError(outcome.error) : UNSUCCESSFUL_MESSAGE)
    }
  }

  return (
    <main className="page registration">
      <p>
        <Link to="/">← All events</Link>
      </p>
      <h1>{event.name}</h1>
      <p className="game-type">{event.gameType}</p>
      <p className="when">
        {formatLongDate(event.date)} · {formatTime(event.time)} – {formatEnd(event.date, event.time, event.duration)}
      </p>
      {event.details && <p>{event.details}</p>}

      <form className="register-form" onSubmit={submit}>
        <label>
          Player name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={100}
            required
            autoComplete="name"
            disabled={full}
          />
        </label>
        <button type="submit" disabled={full || submitting || !trimmed}>
          {full ? 'Event Full' : submitting ? 'Registering…' : 'Submit'}
        </button>
        {message && <p className="error" role="alert">{message}</p>}
      </form>

      <section className="share">
        <QRCodeSVG value={url} size={160} title="QR code for this registration page" />
        <div>
          <p>Share this event:</p>
          <a href={url}>{url}</a>
          <p>
            <a
              href={googleCalendarUrl(event, store?.timeZone ?? 'America/Los_Angeles', [`Register: ${url}`])}
              target="_blank"
              rel="noreferrer"
            >
              Add to Google Calendar
            </a>
          </p>
        </div>
      </section>
    </main>
  )
}
