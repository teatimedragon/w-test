import { useEffect, useState } from 'react'
import { api, describeError, type GameEvent, type NewEvent, type Template } from '../lib/api'
import { storeId } from '../lib/config'
import { formatDuration, formatEnd, formatLongDate, formatTime } from '../lib/format'

const STEPS = ['Select template', 'Event details', 'Date, time and seats', 'Review and create'] as const

const emptyEvent: NewEvent = {
  gameType: '',
  name: '',
  playFormats: '',
  details: '',
  date: '',
  time: '',
  duration: 0,
  capacity: 0,
  eventMinimum: 0,
}

function fromTemplate(t: Template, keep: NewEvent): NewEvent {
  return {
    ...keep,
    gameType: t.gameType,
    name: t.name,
    playFormats: t.defaultPlayFormats,
    details: t.defaultDetails,
    duration: t.defaultDuration,
    capacity: t.defaultCapacity,
    eventMinimum: t.defaultEventMinimum,
  }
}

function stepProblems(step: number, e: NewEvent): string[] {
  const problems: string[] = []
  if (step === 1) {
    if (!e.name.trim()) problems.push('Event name is required.')
    if (!e.gameType.trim()) problems.push('Game type is required.')
  }
  if (step === 2) {
    if (!e.date) problems.push('Date is required.')
    if (!e.time) problems.push('Start time is required.')
    if (!(e.duration > 0)) problems.push('Duration must be greater than 0.')
    if (!(e.capacity > 0)) problems.push('Capacity must be greater than 0.')
    if (!(e.eventMinimum > 0) || e.eventMinimum > e.capacity) problems.push('Event minimum must be between 1 and the capacity.')
  }
  return problems
}

/** Four-step wizard: template → details → date/time/seats → review. */
export function CreateEventWizard({ onCreated }: { onCreated: (event: GameEvent) => void }) {
  const [templates, setTemplates] = useState<Template[] | null>(null)
  const [templateId, setTemplateId] = useState<number | undefined>(undefined)
  const [step, setStep] = useState(0)
  const [event, setEvent] = useState<NewEvent>(emptyEvent)
  const [problems, setProblems] = useState<string[]>([])
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    api
      .listTemplates(storeId())
      .then((list) => {
        setTemplates(list)
        setTemplateId(list[0]?.templateId)
      })
      .catch((e) => setProblems([describeError(e)]))
  }, [])

  const set = <K extends keyof NewEvent>(key: K, v: NewEvent[K]) => setEvent((prev) => ({ ...prev, [key]: v }))
  const num = (v: string) => (v === '' ? 0 : Number(v))

  function next() {
    if (step === 0) {
      const template = templates?.find((t) => t.templateId === templateId)
      if (!template) return setProblems(['Select a template.'])
      setEvent((prev) => fromTemplate(template, prev))
    }
    const found = stepProblems(step, event)
    setProblems(found)
    if (found.length === 0) setStep(step + 1)
  }

  function back() {
    setProblems([])
    setStep(step - 1)
  }

  async function create() {
    setSaving(true)
    setProblems([])
    try {
      onCreated(await api.createEvent(storeId(), { ...event, name: event.name.trim(), gameType: event.gameType.trim() }))
    } catch (e) {
      setProblems([describeError(e)])
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="wizard">
      <ol className="steps">
        {STEPS.map((label, i) => (
          <li key={label} className={i === step ? 'current' : i < step ? 'done' : ''} aria-current={i === step ? 'step' : undefined}>
            {i + 1}. {label}
          </li>
        ))}
      </ol>

      <h2>{STEPS[step]}</h2>

      {step === 0 && (
        <fieldset className="template-picker">
          <legend>Template</legend>
          {templates === null && <p className="muted">Loading templates…</p>}
          {templates?.map((t) => (
            <label key={t.templateId} className="radio">
              <input type="radio" name="template" checked={templateId === t.templateId} onChange={() => setTemplateId(t.templateId)} />
              <span>
                <strong>{t.name}</strong> <span className="muted">{t.gameType} · {t.defaultCapacity} seats · {formatDuration(t.defaultDuration)}</span>
              </span>
            </label>
          ))}
        </fieldset>
      )}

      {step === 1 && (
        <div className="fields">
          <label>
            Event name
            <input value={event.name} maxLength={250} onChange={(e) => set('name', e.target.value)} />
          </label>
          <label>
            Game type
            <input value={event.gameType} maxLength={100} onChange={(e) => set('gameType', e.target.value)} />
          </label>
          <label>
            Play formats
            <input value={event.playFormats} maxLength={250} onChange={(e) => set('playFormats', e.target.value)} />
          </label>
          <label>
            Details
            <textarea value={event.details} maxLength={250} onChange={(e) => set('details', e.target.value)} />
          </label>
        </div>
      )}

      {step === 2 && (
        <div className="fields">
          <label>
            Date
            <input type="date" value={event.date} onChange={(e) => set('date', e.target.value)} />
          </label>
          <label>
            Start time
            <input type="time" value={event.time} onChange={(e) => set('time', e.target.value)} />
          </label>
          <label>
            Duration (minutes)
            <input type="number" min={1} step={15} value={event.duration} onChange={(e) => set('duration', num(e.target.value))} />
          </label>
          <p className="computed" data-testid="end-time">
            {event.date && event.time && event.duration > 0
              ? `Ends at ${formatEnd(event.date, event.time, event.duration)}`
              : 'Pick a date, start time and duration to see the end time.'}
          </p>
          <label>
            Capacity
            <input type="number" min={1} value={event.capacity} onChange={(e) => set('capacity', num(e.target.value))} />
          </label>
          <label>
            Event minimum
            <input type="number" min={1} value={event.eventMinimum} onChange={(e) => set('eventMinimum', num(e.target.value))} />
          </label>
        </div>
      )}

      {step === 3 && (
        <dl className="review">
          <dt>Name</dt>
          <dd>{event.name}</dd>
          <dt>Game type</dt>
          <dd>{event.gameType}</dd>
          <dt>When</dt>
          <dd>
            {formatLongDate(event.date)}, {formatTime(event.time)} – {formatEnd(event.date, event.time, event.duration)} ({formatDuration(event.duration)})
          </dd>
          <dt>Seats</dt>
          <dd>
            {event.capacity} (minimum {event.eventMinimum})
          </dd>
          <dt>Play formats</dt>
          <dd>{event.playFormats || '—'}</dd>
          <dt>Details</dt>
          <dd>{event.details || '—'}</dd>
        </dl>
      )}

      {problems.length > 0 && (
        <ul className="error" role="alert">
          {problems.map((p) => (
            <li key={p}>{p}</li>
          ))}
        </ul>
      )}

      <div className="actions">
        {step > 0 && (
          <button type="button" className="secondary" onClick={back} disabled={saving}>
            Back
          </button>
        )}
        {step < 3 && (
          <button type="button" onClick={next} disabled={templates === null}>
            Next
          </button>
        )}
        {step === 3 && (
          <button type="button" onClick={create} disabled={saving}>
            {saving ? 'Creating…' : 'Create event'}
          </button>
        )}
      </div>
    </section>
  )
}
