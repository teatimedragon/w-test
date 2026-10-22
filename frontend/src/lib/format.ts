// Event dates and times are store-local wall-clock values ("YYYY-MM-DD", "HH:mm"). They are
// formatted by treating them as UTC so the viewer's own time zone never shifts them.

function wallClock(date: string, time = '00:00'): Date {
  const [y, m, d] = date.split('-').map(Number)
  const [hh, mm] = time.split(':').map(Number)
  return new Date(Date.UTC(y, m - 1, d, hh, mm))
}

const fmt = (options: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', ...options })

/** "Thu" */
export const formatWeekday = (date: string) => fmt({ weekday: 'short' }).format(wallClock(date))
/** "Oct" */
export const formatMonth = (date: string) => fmt({ month: 'short' }).format(wallClock(date))
/** "29" */
export const formatDay = (date: string) => String(wallClock(date).getUTCDate())
/** "Thursday, October 29, 2026" */
export const formatLongDate = (date: string) =>
  fmt({ weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' }).format(wallClock(date))
/** "6:00 PM" */
export const formatTime = (time: string) => fmt({ hour: 'numeric', minute: '2-digit' }).format(wallClock('2000-01-01', time))

/** "3 h", "1 h 30 min", "45 min" */
export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return [h ? `${h} h` : '', m ? `${m} min` : ''].filter(Boolean).join(' ') || '0 min'
}

/** Store-local end date and time of an event that starts at date/time and lasts `minutes`. */
export function endOf(date: string, time: string, minutes: number): { date: string; time: string } {
  const end = new Date(wallClock(date, time).getTime() + minutes * 60_000)
  const iso = end.toISOString()
  return { date: iso.slice(0, 10), time: iso.slice(11, 16) }
}

/** "9:00 PM", or "1:00 AM (Fri, Oct 30)" when the event ends on a later day. */
export function formatEnd(date: string, time: string, minutes: number): string {
  const end = endOf(date, time, minutes)
  const t = formatTime(end.time)
  return end.date === date ? t : `${t} (${formatWeekday(end.date)}, ${formatMonth(end.date)} ${formatDay(end.date)})`
}

const compact = (date: string, time: string) => `${date.replaceAll('-', '')}T${time.replace(':', '')}00`

/** Google Calendar "add event" link. Times are passed as store-local with ctz set to the store's zone. */
export function googleCalendarUrl(
  event: { name: string; date: string; time: string; duration: number; details: string },
  timeZone: string,
  extraDetails: string[] = [],
): string {
  const end = endOf(event.date, event.time, event.duration)
  const params = new URLSearchParams({
    action: 'TEMPLATE',
    text: event.name,
    dates: `${compact(event.date, event.time)}/${compact(end.date, end.time)}`,
    ctz: timeZone,
    details: [event.details, ...extraDetails].filter(Boolean).join('\n'),
  })
  return `https://calendar.google.com/calendar/render?${params}`
}
