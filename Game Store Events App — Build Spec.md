# Game Store Events App — Build Spec

Oct 21, 2026 · @Peter

## 1. Overview

A web app where a game store lists upcoming events, players register for a seat, and the store admin creates events from reusable templates.

- **Players** browse upcoming events, open a registration page (directly or via QR code), and register by name.
- **Store admin** creates events through a 4-step wizard, views all events on a calendar, and manages event templates.

Scope is a single store, "Basic Game Store". Section 10 lists the decisions made while turning the original instructions into this spec.

## 2. Tech stack and constraints

| Area | Requirement |
| --- | --- |
| Backend | .NET 10 Web API |
| API style | RESTful, JSON, camelCase property names |
| Backend layout | One API with store-manager and event-manager controllers |
| Data store | In-memory database for application state; data resets on restart |
| Design | Dependency injection throughout; business logic in services behind interfaces so it can be unit tested without the web host or database |
| Frontend | React with Vite |
| QR codes | qrcode.react |
| Calendar | FullCalendar |
| Packaging | Frontend and backend run in Docker, started with Docker Compose |
| Config | Public base URL for QR codes and links set by the APP\_BASE\_URL environment variable |

## 3. Domain model

Five tables across two domains. IDs are UUIDv7 except templateId.

### Store domain

**Store**

| Field | Type | Notes |
| --- | --- | --- |
| storeId | UUIDv7 | Fixed, known value for the seeded store |
| storeName | varchar(100) |  |
| timeZone | IANA zone name | Seeded as America/Los\_Angeles; event date/time are store-local |

### Event domain

**Event**

| Field | Type | Notes |
| --- | --- | --- |
| eventId | UUIDv7 |  |
| storeId | UUIDv7 | FK to Store |
| gameType | varchar(100) |  |
| name | varchar(250) |  |
| date | DATE | Store-local date |
| time | TIME | Start time, store-local |
| duration | Duration (minutes or ISO 8601, not time of day) |  |
| capacity | SMALLINT | Total seats |
| eventMinimum | SMALLINT | Players needed for the event to be held; informational only |
| playFormats | varchar(250) |  |
| details | varchar(250) |  |

**Registration**

| Field | Type | Notes |
| --- | --- | --- |
| registrationId | UUIDv7 |  |
| eventId | UUIDv7 | FK to Event |
| name | varchar(100) | Player name |

**Capacity** (one row per event, used for optimistic concurrency)

| Field | Type | Notes |
| --- | --- | --- |
| eventId | UUIDv7 | PK, FK to Event |
| versionId | SMALLINT | Increments on every successful ADD or REMOVE |
| remainingCapacity | SMALLINT | Seats left |

**Template**

| Field | Type | Notes |
| --- | --- | --- |
| templateId | SMALLINT | New template = highest existing templateId + 1 |
| storeId | UUIDv7 | FK to Store |
| gameType | varchar(100) |  |
| name | varchar(100) |  |
| defaultCapacity | SMALLINT |  |
| defaultEventMinimum | SMALLINT |  |
| defaultDuration | Duration |  |
| defaultPlayFormats | varchar(250) |  |
| defaultDetails | varchar(250) |  |

### Seed data

One store: **Basic Game Store**.

Four templates for that store. Template 0 is a copy of template 1's values, named "Default"; template 1 is named after its gameType.

| templateId | gameType | name | Capacity | Minimum | Duration | Play formats | Details |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | Magic: The Gathering | Default | 30 | 3 | 3 h | 1v1 constructed | 3 rounds best of 3 |
| 1 | Magic: The Gathering | Magic: The Gathering | 30 | 3 | 3 h | 1v1 constructed | 3 rounds best of 3 |
| 2 | Magic: The Gathering Commander | Magic: The Gathering Commander | 30 | 6 | 4 h | pods | swiss pairing |
| 3 | Galactic Empires CCG | Galactic Empires | 30 | 7 | 4 h | 7v7 | winner takes all swiss final if more than one pod |

## 4. Business rules

Registration uses optimistic concurrency on the Capacity row: a write succeeds only if the caller's versionId matches the current one.

### Registration (ADD)

1. The client reads remainingCapacity and versionId for the event.
2. If remainingCapacity is 0, registration is disabled (button shows "Event Full").
3. The client sends ADD with its versionId.
4. The server registers the player only if versionId matches **and** remainingCapacity > 0. On success it creates the Registration, decrements remainingCapacity, increments versionId, and returns 200.
5. If remainingCapacity is 0, the server returns 409 with reason EVENT\_FULL. Otherwise, if versionId does not match, it returns 409 with reason VERSION\_CONFLICT. Both include the current versionId and remainingCapacity.
6. On VERSION\_CONFLICT, the client retries with the returned versionId, up to 3 attempts in total.
7. On EVENT\_FULL, the client stops and shows "Event Full". If 3 attempts fail, it shows "Registration unsuccessful, try again."

### Drop (REMOVE)

- Removes one registration, identified by registrationId.
- Increments remainingCapacity and versionId; same versionId check as ADD.
- Available in the API only; players have no drop option in the UI.

### Validation

- Player name: required, trimmed, 1–100 characters.
- Event: capacity > 0; 0 < eventMinimum ≤ capacity; date/time not in the past; duration > 0; text fields within column lengths.
- Template: same numeric rules as events; name required.

### Other rules

- "Upcoming" = events whose start date/time is now or later in the store time zone. The API returns upcoming events only.
- Event lists are sorted by date, then start time.
- Past events are never displayed anywhere (landing page, admin calendar); a registration link to a past event shows "Event not found".
- eventMinimum is informational: if it isn't met the event won't be held, but the app takes no action and shows no warning.

## 5. API specification

All bodies are JSON with camelCase fields. Validation failures return 400 with field errors; unknown IDs return 404.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | /store-manager/stores/{storeId} | Store details |
| GET | /event-manager/stores/{storeId}/events | Store's upcoming events |
| POST | /event-manager/stores/{storeId}/events | Create event |
| GET | /event-manager/events/{eventId} | One event's details |
| GET | /event-manager/events/{eventId}/capacity | Remaining capacity + versionId |
| POST | /event-manager/register | Register (ADD) or drop (REMOVE) |
| GET | /event-manager/stores/{storeId}/templates | Store's templates |
| PUT | /event-manager/stores/{storeId}/templates | Create or update template |

### GET /store-manager/stores/{storeId}

200: `{ storeId, storeName, timeZone }`

### GET /event-manager/stores/{storeId}/events

200: array of `{ eventId, gameType, date, time, name, capacity, eventMinimum, duration, playFormats, details, registeredCount }`

Upcoming events only, sorted by date then start time. The service looks up each event's registeredCount and includes it in the response; the UI computes remaining seats as capacity − registeredCount.

### POST /event-manager/stores/{storeId}/events

Request: `{ gameType, date, time, name, capacity, eventMinimum, duration, playFormats, details }`

201: the created event, same shape as the list item. Also creates its Capacity row (remainingCapacity = capacity, versionId = 1).

### GET /event-manager/events/{eventId}

200: same shape as the list item. 404 if the event doesn't exist or has already started.

### GET /event-manager/events/{eventId}/capacity

200: `{ remainingCapacity, versionId }`

### POST /event-manager/register

Request: `{ eventId, action: "ADD" | "REMOVE", versionId, name (ADD), registrationId (REMOVE) }`

- 200 (ADD): `{ name, registrationId, versionId, remainingCapacity }`
- 200 (REMOVE): `{ registrationId, versionId, remainingCapacity }`
- 409: Problem Details body (RFC 9457, ASP.NET Core ProblemDetails) with extension fields `{ reason: "VERSION_CONFLICT" | "EVENT_FULL", versionId, remainingCapacity }`. VERSION\_CONFLICT = stale versionId, client may retry; EVENT\_FULL = no seats left, client stops.

### GET /event-manager/stores/{storeId}/templates

200: array of `{ templateId, gameType, name, defaultCapacity, defaultEventMinimum, defaultDuration, defaultPlayFormats, defaultDetails }`

### PUT /event-manager/stores/{storeId}/templates

Request and 200 response: one template, same shape as above. An existing templateId updates it; a new template gets templateId = highest existing templateId + 1.

## 6. UI: pages and routes

Three pages: a public landing page, a public registration page, and an admin page with three views.

| Route | Page | Audience |
| --- | --- | --- |
| / | Upcoming events for the default store | Players |
| /registration/{eventId} | Registration (directly navigable) | Players |
| /admin | Store events admin (no login) | Store admin |

### Landing page (/)

- Store name at the top; it links to /admin.
- One card or row per upcoming event showing:
  - Name and game type
  - Month, day, day of week, and start time
  - Minimum players needed
  - Registered players out of total capacity, e.g. "12 / 30"
  - Register button linking to /registration/{eventId}
- Data is refetched whenever the page loads, so counts are current after returning from registration.

### Registration page (/registration/{eventId})

- On load, fetch event details and current capacity. If the event doesn't exist or is in the past, show "Event not found".
- Show event name, date, and time.
- Player name input and Submit button.
- If remainingCapacity is 0: Submit is disabled and reads "Event Full".
- QR code encoding the page URL (APP\_BASE\_URL + /registration/{eventId}).
- The same URL shown as a clickable link.
- "Add to Google Calendar" link prefilled with event name, start, end, and details.
- On Submit: run the ADD flow in section 4. On success, return to /. On final failure, show "Registration unsuccessful, try again."

### Admin page (/admin)

A toggle switches between three views; **Create Event** is the default.

**Create Event** (4-step wizard)

1. Select template.
2. Edit event details (name, game type, play formats, details), prefilled from the template.
3. Set date, start time, and duration (the computed end time is shown), plus capacity and event minimum (prefilled).
4. Review and create.

After creating, switch to the View Events calendar showing the new event alongside all others.

**View Events**

- Calendar of upcoming events, each shown by name.
- Clicking an event name opens a popup with its full details.

**Manage Templates**

- List of templates with template 0 (Default) preselected.
- Two buttons at the bottom: Update Template and Create New Template.
- Update Template: next step shows the selected template's fields, editable, with a Save button.
- Create New Template: next step shows editable values copied from the selected template with a Continue button; the following step asks for the new template's name, with a Save button.

## 7. Testing

Every business rule in section 4 and every endpoint in section 5 has at least one test that names the rule it proves.

- **Unit tests** (.NET): service-layer logic with dependencies mocked, covering:
  - Register succeeds when versionId matches and seats remain
  - Stale versionId returns VERSION\_CONFLICT with current versionId and remainingCapacity
  - Full event returns EVENT\_FULL
  - REMOVE frees a seat and bumps the version
  - Validation rules for players, events, and templates
  - Event creation from template defaults; Capacity row initialized
  - Template create (highest templateId + 1) and update
  - Upcoming filter excludes past events, using the store time zone
  - Seed data matches section 3
- **Integration tests** (.NET, in-process API host): each endpoint's success and error responses, plus a concurrent-registration test showing seats are never oversold.
- **Frontend tests**: component tests for the retry logic, "Event Full" state, and wizard steps.

## 8. Deliverables

- Source for backend, frontend, and tests.
- Dockerfiles and a Docker Compose file that start the whole app with one command.
- **README.md**: prerequisites, how to run with Docker, URLs to open, how to run the tests, and config settings (such as APP\_BASE\_URL).
- **applog.md**: the logic and decisions made while building, starting from the decisions in section 10.

## 9. Assumptions and out of scope

- Single store; no store management UI.
- No login; the admin page is open to anyone.
- Data lives in memory and resets when the container restarts.
- No email or notifications; the Google Calendar item is a link the player clicks.
- No editing or deleting events after creation.
- Players can't drop a registration in the UI; REMOVE exists in the API only.

## 10. Decisions

Decisions made while cleaning up the original instructions, all settled on 2026-10-21. The sections above already reflect them.

| Area | Decision |
| --- | --- |
| Seed templates | Four templates. Template 0, "Default", copies template 1's values; template 1 is named "Magic: The Gathering" after its gameType |
| Template details | Templates carry defaultDetails in the table and API |
| Naming | gameType everywhere (the API no longer uses game); camelCase JSON, including the Store response |
| Registration retries | The UI retries on VERSION\_CONFLICT, up to 3 attempts in total |
| 409 responses | Problem Details body with reason VERSION\_CONFLICT (stale version, retry) or EVENT\_FULL (no seats, stop), plus versionId and remainingCapacity |
| Capacity naming | The Capacity table field is remainingCapacity; Event.capacity is total seats |
| Registered counts | The events list returns registeredCount per event, looked up by the service; the UI computes open seats as capacity − registeredCount |
| Event details | New endpoint GET /event-manager/events/{eventId} |
| Drop registration | REMOVE takes registrationId and returns the new versionId and remainingCapacity; no UI for players to drop |
| Event duration | The wizard asks for duration and shows the computed end time; duration is stored as a length, not a time of day |
| Event minimum | Informational only: if not met, the event won't be held; the app takes no action |
| Upcoming events | The API returns upcoming events only, sorted by date and start time. Past events are never displayed; a registration link to a past event shows "Event not found" |
| Default store | Seeded with a fixed storeId; / loads it |
| Time zone | Store.timeZone (IANA), seeded America/Los\_Angeles, returned by the Store API; event date and time are store-local; the service converts for the upcoming filter and calendar link |
| QR code URL | Public base URL set by the APP\_BASE\_URL environment variable |
| Google Calendar | "Add to Google Calendar" link on the registration page |
| Validation | Rules in section 4 |
| Template IDs | New templateId = highest existing templateId + 1; create and update both use PUT on the templates collection |
| Manage Templates | Template 0 (Default) is preselected; Create New copies the selected template; copied values are editable before Continue |
| Create event response | 201 with the created event |
| Admin access | No login for now |
| Stack | .NET 10, React with Vite, qrcode.react, FullCalendar |
| Backend layout | One API with store-manager and event-manager controllers |
| Frontend tests | Component tests for the retry logic, Event Full state, and wizard steps |
