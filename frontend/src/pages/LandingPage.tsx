import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, describeError, type GameEvent } from '../lib/api'
import { storeId } from '../lib/config'
import { formatDay, formatMonth, formatTime, formatWeekday } from '../lib/format'
import { useStore } from '../lib/useStore'

export function LandingPage() {
  const store = useStore()
  const [events, setEvents] = useState<GameEvent[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  // Refetched every time the page mounts, so counts are current after returning from registration.
  useEffect(() => {
    let cancelled = false
    api
      .listEvents(storeId())
      .then((list) => !cancelled && setEvents(list))
      .catch((e) => !cancelled && setError(describeError(e)))
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <main className="page">
      <header className="site-header">
        <Link to="/admin" className="store-name">
          {store?.storeName ?? 'Basic Game Store'}
        </Link>
        <p className="subtitle">Upcoming events</p>
      </header>

      {error && <p className="error" role="alert">{error}</p>}
      {!error && events === null && <p className="muted">Loading events…</p>}
      {events?.length === 0 && <p className="muted">No upcoming events. Check back soon.</p>}

      <ul className="event-list">
        {events?.map((e) => (
          <li key={e.eventId} className="event-card">
            <div className="date-badge" aria-label={`${formatWeekday(e.date)} ${formatMonth(e.date)} ${formatDay(e.date)}`}>
              <span className="month">{formatMonth(e.date)}</span>
              <span className="day">{formatDay(e.date)}</span>
              <span className="weekday">{formatWeekday(e.date)}</span>
            </div>
            <div className="event-info">
              <h2>{e.name}</h2>
              <p className="game-type">{e.gameType}</p>
              <p className="meta">
                <span>Starts {formatTime(e.time)}</span>
                <span>Minimum {e.eventMinimum} players</span>
                <span className="seats">
                  {e.registeredCount} / {e.capacity} registered
                </span>
              </p>
            </div>
            <Link className="button" to={`/registration/${e.eventId}`}>
              Register
            </Link>
          </li>
        ))}
      </ul>
    </main>
  )
}
