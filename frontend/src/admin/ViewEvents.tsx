import type { EventClickArg } from '@fullcalendar/core'
import dayGridPlugin from '@fullcalendar/daygrid'
import FullCalendar from '@fullcalendar/react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, describeError, type GameEvent } from '../lib/api'
import { storeId } from '../lib/config'
import { endOf, formatDuration, formatEnd, formatLongDate, formatTime } from '../lib/format'

/** Calendar of upcoming events; clicking one opens its details. */
export function ViewEvents({ focusEvent }: { focusEvent?: GameEvent | null }) {
  const [events, setEvents] = useState<GameEvent[]>([])
  const [selected, setSelected] = useState<GameEvent | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.listEvents(storeId()).then(setEvents).catch((e) => setError(describeError(e)))
  }, [])

  const calendarEvents = events.map((e) => {
    const end = endOf(e.date, e.time, e.duration)
    return { id: e.eventId, title: e.name, start: `${e.date}T${e.time}`, end: `${end.date}T${end.time}` }
  })

  function onClick(arg: EventClickArg) {
    setSelected(events.find((e) => e.eventId === arg.event.id) ?? null)
  }

  return (
    <section>
      {error && <p className="error" role="alert">{error}</p>}
      <FullCalendar
        plugins={[dayGridPlugin]}
        initialView="dayGridMonth"
        initialDate={focusEvent?.date}
        // Event times are store-local wall-clock values; "UTC" makes FullCalendar show them unshifted.
        timeZone="UTC"
        events={calendarEvents}
        eventClick={onClick}
        eventTimeFormat={{ hour: 'numeric', minute: '2-digit', meridiem: 'short' }}
        height="auto"
      />

      {selected && (
        <div className="modal-backdrop" onClick={() => setSelected(null)}>
          <div className="modal" role="dialog" aria-modal="true" aria-labelledby="event-title" onClick={(e) => e.stopPropagation()}>
            <h2 id="event-title">{selected.name}</h2>
            <dl className="review">
              <dt>Game type</dt>
              <dd>{selected.gameType}</dd>
              <dt>When</dt>
              <dd>
                {formatLongDate(selected.date)}, {formatTime(selected.time)} – {formatEnd(selected.date, selected.time, selected.duration)} (
                {formatDuration(selected.duration)})
              </dd>
              <dt>Registered</dt>
              <dd>
                {selected.registeredCount} / {selected.capacity}
              </dd>
              <dt>Event minimum</dt>
              <dd>{selected.eventMinimum}</dd>
              <dt>Play formats</dt>
              <dd>{selected.playFormats || '—'}</dd>
              <dt>Details</dt>
              <dd>{selected.details || '—'}</dd>
            </dl>
            <div className="actions">
              <Link to={`/registration/${selected.eventId}`}>Registration page</Link>
              <button type="button" onClick={() => setSelected(null)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
