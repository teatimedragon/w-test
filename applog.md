# App log

The logic and decisions behind the Game Store Events app. It starts with the decisions settled in the spec (section 10), then records what was decided while building, including how ambiguous parts of the spec were read.

## 1. Decisions from the spec (settled 2026-10-21)

| Area | Decision |
| --- | --- |
| Seed templates | Four templates. Template 0, "Default", copies template 1's values; template 1 is named "Magic: The Gathering" after its gameType |
| Template details | Templates carry defaultDetails in the table and API |
| Naming | gameType everywhere (the API no longer uses game); camelCase JSON, including the Store response |
| Registration retries | The UI retries on VERSION_CONFLICT, up to 3 attempts in total |
| 409 responses | Problem Details body with reason VERSION_CONFLICT (stale version, retry) or EVENT_FULL (no seats, stop), plus versionId and remainingCapacity |
| Capacity naming | The Capacity table field is remainingCapacity; Event.capacity is total seats |
| Registered counts | The events list returns registeredCount per event, looked up by the service; the UI computes open seats as capacity − registeredCount |
| Event details | New endpoint GET /event-manager/events/{eventId} |
| Drop registration | REMOVE takes registrationId and returns the new versionId and remainingCapacity; no UI for players to drop |
| Event duration | The wizard asks for duration and shows the computed end time; duration is stored as a length, not a time of day |
| Event minimum | Informational only: if not met, the event won't be held; the app takes no action |
| Upcoming events | The API returns upcoming events only, sorted by date and start time. Past events are never displayed; a registration link to a past event shows "Event not found" |
| Default store | Seeded with a fixed storeId; / loads it |
| Time zone | Store.timeZone (IANA), seeded America/Los_Angeles, returned by the Store API; event date and time are store-local; the service converts for the upcoming filter and calendar link |
| QR code URL | Public base URL set by the APP_BASE_URL environment variable |
| Google Calendar | "Add to Google Calendar" link on the registration page |
| Validation | Rules in section 4 |
| Template IDs | New templateId = highest existing templateId + 1; create and update both use PUT on the templates collection |
| Manage Templates | Template 0 (Default) is preselected; Create New copies the selected template; copied values are editable before Continue |
| Create event response | 201 with the created event |
| Admin access | No login for now |
| Stack | .NET 10, React with Vite, qrcode.react, FullCalendar |
| Backend layout | One API with store-manager and event-manager controllers |
| Frontend tests | Component tests for the retry logic, Event Full state, and wizard steps |

## 2. Architecture

- **Three backend projects.** `GameStore.Core` holds the domain, DTOs, validators, services and the repository interfaces, and has no web or EF dependency. `GameStore.Infrastructure` implements the repositories on EF Core's in-memory provider and seeds the data. `GameStore.Api` is the host: two controllers (`store-manager`, `event-manager`) that only translate HTTP to service calls, and an exception handler that turns service exceptions into Problem Details.
- **Dependency injection throughout.** Services are registered behind `IStoreService`, `IEventService`, `IRegistrationService` and `ITemplateService`; repositories behind `I*Repository`; the clock is the framework's `TimeProvider`. Unit tests construct services with NSubstitute mocks and a `FakeTimeProvider`, with no web host or database.
- **"In-memory database" = EF Core InMemory provider.** It gives the five tables real keys and relationships while keeping state in process, so data resets on restart. Each repository call opens its own short-lived `DbContext` (via `IDbContextFactory`) so a read never returns a stale tracked entity.
- **Business logic stays in services.** The version check, EVENT_FULL check, upcoming filter, max+1 template id and validation all live in Core. The repository only does the atomic write (below).
- **Frontend.** React Router pages for `/`, `/registration/:eventId` and `/admin`. A small `api` module wraps `fetch` and turns non-2xx responses into an `ApiError` carrying the Problem Details body. The ADD retry flow is a standalone function (`registerWithRetry`) so it can be tested directly and through the page.
- **Docker.** The API runs on `mcr.microsoft.com/dotnet/aspnet:10.0`. The frontend is built with Node and served by nginx, which also proxies `/store-manager` and `/event-manager` to the API container. Same origin means no CORS configuration.

## 3. Registration and optimistic concurrency

The spec's flow, as implemented:

1. The registration page loads the event and `GET .../capacity` (`remainingCapacity`, `versionId`).
2. If `remainingCapacity` is 0, Submit is disabled and reads "Event Full".
3. Submit sends `ADD` with the loaded `versionId`.
4. The service loads the Capacity row. **EVENT_FULL is checked first**, then the version. The spec says "If remainingCapacity is 0 … EVENT_FULL. Otherwise, if versionId does not match … VERSION_CONFLICT", so a stale caller on a full event gets EVENT_FULL and stops rather than retrying into a full event.
5. On a match, the service asks the repository to apply a `CapacityChange` (expected version, new version, new remaining, registration to add or remove). The repository applies it **only if the stored versionId still equals the expected one** (compare-and-swap) and saves the Capacity update and the Registration insert/delete together.
6. If the compare-and-swap fails (another request won between our read and write), the service re-reads the row and returns 409 with the fresh state: EVENT_FULL if that took the last seat, otherwise VERSION_CONFLICT.
7. The client retries VERSION_CONFLICT with the returned versionId, **3 attempts in total** (the first try plus two retries). EVENT_FULL stops immediately and turns the button into "Event Full". After 3 failed attempts it shows "Registration unsuccessful, try again." On success it navigates to `/`.

**Why a lock as well as a version check.** The EF in-memory provider has no transactions, so the read-compare-write in step 5 runs under a single process-wide `SemaphoreSlim` (`CapacityWriteLock`). The versionId check is still the business rule that decides who wins; the lock only makes the check-and-write atomic. `versionId` is also marked as an EF concurrency token. The integration test `Concurrent_registrations_never_oversell_seats` sends 60 parallel players at a 10-seat event and checks that registrations ≤ seats and that `registeredCount`, `remainingCapacity` and `versionId` all agree afterwards. A real database would replace the lock with a conditional `UPDATE … WHERE versionId = @expected`.

**REMOVE** needs the `registrationId` and the current `versionId`. It returns 404 if the registration doesn't exist or belongs to a different event, VERSION_CONFLICT on a stale version, and otherwise deletes the registration, increments `remainingCapacity` and bumps `versionId`.

## 4. Decisions made while building

| Area | Decision | Why |
| --- | --- | --- |
| Duration format | Whole **minutes** (integer) in the API (`duration`, `defaultDuration`); `TimeSpan` in the domain | The spec allows minutes or ISO 8601. Minutes are simplest for the wizard's number input and for computing end times. |
| Date/time format | `date` is `YYYY-MM-DD`; `time` is `HH:mm` (the API also accepts `HH:mm:ss`) | Matches HTML date/time inputs; a custom converter keeps responses free of seconds. |
| versionId overflow | `versionId` is SMALLINT; after 32767 it wraps to 1 | Repeated ADD/REMOVE could otherwise overflow. Wrapping keeps the type from the spec; a collision would need 32767 writes between a client's read and write. |
| New event's versionId | Starts at 1 with `remainingCapacity = capacity` | As specified. |
| Past events everywhere | GET event, GET capacity and POST register all return 404 once the event has started | The spec says past events are never displayed and a link to one shows "Event not found". Registering for a started event is treated the same way. |
| "Upcoming" boundary | An event starting exactly now is upcoming (`start >= now`) | "Now or later" in the spec. |
| Daylight-saving gap | A store-local start time that doesn't exist (e.g. 02:30 on the spring-forward night) is moved forward by the DST delta (to 03:30) | Avoids an exception; documented and unit tested. Ambiguous fall-back times use the standard-time offset (.NET default). |
| Required event fields | `name` and `gameType` are required; `playFormats` and `details` are optional (empty string) | The spec lists column lengths only. An event without a name or game type can't be displayed meaningfully. |
| Required template fields | `name` and `gameType` required; numeric rules as events | The spec says "name required"; gameType is required for the same reason as on events. |
| Text trimming | Names, game types, play formats and details are trimmed before validation and storage | The spec says player names are trimmed; applied to all text for consistency. Length limits apply to the trimmed value. |
| SMALLINT bounds | `capacity` / `defaultCapacity` must be ≤ 32767 | Column type is SMALLINT. |
| Template PUT with unknown id | A `templateId` that doesn't exist creates a new template with **highest + 1**, ignoring the requested id | The spec: "An existing templateId updates it; a new template gets templateId = highest existing templateId + 1." |
| Template id scope | "Highest existing templateId" is across all templates (it's the table's key), not per store | templateId is the primary key; with one store the result is the same. |
| Template create race | If two creates compute the same id, the loser recomputes and retries (up to 5 times) | Keeps ids unique without a lock. |
| Template of another store | Updating a templateId that belongs to a different store returns 404 | Store-scoped route. |
| 404 for malformed ids | Route constraints (`{id:guid}`) make a non-GUID id a 404 | Same as an unknown id. |
| Validation response | 400 `ValidationProblemDetails` with `errors` keyed by camelCase field name | "Validation failures return 400 with field errors." |
| 409 response | `ProblemDetails` with `status`, `title`, `detail`, and extensions `reason`, `versionId`, `remainingCapacity`; content type `application/problem+json` | RFC 9457, as specified. |
| Seeded store id | `0199f0a0-0000-7000-8000-000000000001` (a valid UUIDv7: version 7, RFC variant) | "Fixed, known value." |
| New ids | `Guid.CreateVersion7()` for events and registrations | UUIDv7 as specified. |
| Calendar file (changed 2026-10-22) | The registration page's "Add to Google Calendar (.ics)" link downloads an iCalendar file from `GET /event-manager/events/{eventId}/calendar.ics` instead of opening a Google Calendar URL. The file has SUMMARY = event name, LOCATION = store name, and DTSTART/DTEND in UTC, converted by the service from the store-local date/time with the store's time zone (end = start + duration). DESCRIPTION carries game type, play formats and details; UID is stable per event so re-importing updates rather than duplicates | Peter asked for the link to save a .ics file with the event name, start and end times and the store as location. Building it on the server keeps the time-zone conversion in the service (as section 10 says) and lets Google Calendar, Apple Calendar and Outlook all import it. UTC times avoid needing a VTIMEZONE block. Started events return 404, like the other event endpoints |
| Calendar time zone | FullCalendar is given `timeZone="UTC"` and the store-local times, so events display at their store-local wall-clock times whatever the admin's browser zone | Avoids a time-zone plugin; event times are always shown as store-local. |
| End time display | The wizard and popups show the computed end time, with the date when it falls on the next day | "The computed end time is shown." |
| APP_BASE_URL at runtime | The nginx container writes `config.js` (`window.__APP_CONFIG__`) from `APP_BASE_URL` at start-up | Vite env vars are baked in at build time; this lets the same image serve any base URL. Without it the app falls back to the page origin. |
| Default store in UI | The frontend uses the seeded store id (overridable with `STORE_ID`) | "/ loads it." |
| Store name link | The store name on `/` links to `/admin`; on `/admin` it links back to `/` | Spec for `/`; the reverse link is a convenience. |
| Landing page refresh | Events are fetched on every mount of `/` | Counts are current after returning from registration. |
| Wizard template list | Step 1 preselects the first template (template 0, Default) | Mirrors Manage Templates. |
| Wizard step checks | Each step checks its own fields client-side before Next; the server remains the authority and its field errors are shown on Create | Faster feedback without duplicating the business rules as the source of truth. |
| After create | The wizard resets and the admin page switches to View Events, opening the calendar on the new event's month with a "Created …" notice | "Switch to the View Events calendar showing the new event alongside all others." |
| Create New Template | Copies all values from the selected template except the name, which is asked for in the next step | The spec's two steps: copied values with Continue, then the name with Save. |
| No event editing or player drop UI | Not built | Out of scope (section 9). |
| Health check | `GET /health` on the API | Convenience for containers; not part of the spec's API. |

## 5. Test map

Every business rule in section 4, every endpoint in section 5, and the calendar-file endpoint added later has at least one test named after it.

### Business rules (section 4)

| Rule | Tests |
| --- | --- |
| ADD succeeds when versionId matches and seats remain | `RegistrationServiceTests.Add_succeeds_when_versionId_matches_and_seats_remain`; integration `Register_ADD_returns_200_with_registration_and_new_capacity` |
| Stale versionId → VERSION_CONFLICT with current versionId and remainingCapacity | `Add_with_stale_versionId_returns_VERSION_CONFLICT_with_current_versionId_and_remainingCapacity`; `Add_that_loses_a_race_at_write_time_returns_VERSION_CONFLICT_with_fresh_state`; integration `Register_with_stale_versionId_returns_409_problem_details_VERSION_CONFLICT` |
| Full event → EVENT_FULL | `Add_to_full_event_returns_EVENT_FULL`; `Add_to_full_event_returns_EVENT_FULL_even_when_versionId_is_also_stale`; `Add_that_loses_a_race_for_the_last_seat_returns_EVENT_FULL`; integration `Register_for_full_event_returns_409_problem_details_EVENT_FULL` |
| REMOVE frees a seat and bumps the version (same version check) | `Remove_frees_a_seat_and_bumps_the_version`; `Remove_with_stale_versionId_returns_VERSION_CONFLICT`; integration `Register_REMOVE_returns_200_and_frees_the_seat` |
| Seats never oversold | integration `Concurrent_registrations_never_oversell_seats`, `Concurrent_registrations_with_the_same_versionId_let_exactly_one_through` |
| Client retries VERSION_CONFLICT, 3 attempts total | frontend `registerWithRetry` tests; `RegistrationPage` "retries on VERSION_CONFLICT using the returned versionId" |
| Client stops on EVENT_FULL; "Event Full" | `registerWithRetry` "stops immediately on EVENT_FULL"; `RegistrationPage` "stops and shows Event Full…" and "disables Submit and shows Event Full when remainingCapacity is 0" |
| "Registration unsuccessful, try again." after 3 failures | `RegistrationPage` "shows Registration unsuccessful, try again. after 3 failed attempts" |
| Player name validation | `ValidationTests.Player_name_*`; `Add_rejects_missing_player_name`; integration `Register_with_invalid_name_returns_400_with_field_errors` |
| Event validation | `ValidationTests.Event_*`; `EventServiceTests.Event_creation_rejects_a_start_in_the_past_in_the_store_time_zone`; integration `Create_event_with_invalid_fields_returns_400_with_field_errors` |
| Template validation | `ValidationTests.Template_*`; `Template_upsert_rejects_missing_name`; integration `Put_template_with_invalid_fields_returns_400_with_field_errors` |
| Upcoming = start ≥ now in store time zone | `Upcoming_filter_excludes_past_events_using_the_store_time_zone`; `Upcoming_includes_an_event_starting_exactly_now`; integration `List_events_hides_events_once_they_start` |
| Sorted by date, then start time | `Event_list_is_sorted_by_date_then_start_time`; integration `List_events_returns_upcoming_events_sorted_with_registeredCount` |
| Past events never displayed; link shows "Event not found" | `Get_event_that_already_started_is_not_found`; `Register_for_event_that_already_started_is_not_found`; integration `Get_event_that_has_started_returns_404`; `RegistrationPage` "shows Event not found…" |
| eventMinimum informational | No behaviour to test beyond validation; the landing page shows it (`LandingPage` test) |
| Event creation from template defaults; Capacity row initialized | `Event_creation_from_template_defaults_initializes_the_Capacity_row`; integration `Create_event_returns_201_with_the_created_event_and_initializes_capacity`; wizard tests |
| Template create (highest + 1) and update | `Template_create_uses_highest_templateId_plus_1`, `Template_create_with_an_unknown_templateId_still_uses_highest_plus_1`, `Template_create_retries_when_another_create_took_the_id`, `Template_update_changes_the_existing_template`; integration `Put_template_*` |
| Calendar file: name, store-local times converted to UTC, store as location | `CalendarFileTests.*`; integration `Get_calendar_file_downloads_an_ics_with_name_times_and_store_location`; `RegistrationPage` ".ics calendar download" |
| Seed data | `SeedDataTests.*`; integration `List_templates_returns_the_four_seeded_templates` |

### Endpoints (section 5)

| Endpoint | Success | Errors |
| --- | --- | --- |
| GET /store-manager/stores/{storeId} | `Get_store_returns_camelCase_store_details` | `Get_unknown_store_returns_404` |
| GET /event-manager/stores/{storeId}/events | `List_events_returns_upcoming_events_sorted_with_registeredCount` | `List_events_for_unknown_store_returns_404` |
| POST /event-manager/stores/{storeId}/events | `Create_event_returns_201_with_the_created_event_and_initializes_capacity` | `Create_event_with_invalid_fields_returns_400_with_field_errors`, `Create_event_for_unknown_store_returns_404` |
| GET /event-manager/events/{eventId} | `Get_event_returns_the_event` | `Get_unknown_event_returns_404`, `Get_event_that_has_started_returns_404` |
| GET /event-manager/events/{eventId}/capacity | `Create_event_returns_201_…_initializes_capacity` | `Get_capacity_for_unknown_event_returns_404` |
| GET /event-manager/events/{eventId}/calendar.ics | `Get_calendar_file_downloads_an_ics_with_name_times_and_store_location` | `Get_calendar_file_for_unknown_event_returns_404`, `Calendar_file_for_event_that_already_started_is_not_found` |
| POST /event-manager/register | `Register_ADD_returns_200_…`, `Register_REMOVE_returns_200_…` | `…VERSION_CONFLICT`, `…EVENT_FULL`, `Register_with_invalid_name_returns_400_…`, `Register_for_unknown_event_returns_404`, `Register_REMOVE_for_unknown_registration_returns_404`, `Malformed_json_returns_400` |
| GET /event-manager/stores/{storeId}/templates | `List_templates_returns_the_four_seeded_templates` | `List_templates_for_unknown_store_returns_404` |
| PUT /event-manager/stores/{storeId}/templates | `Put_template_without_templateId_creates_highest_plus_1`, `Put_template_with_existing_templateId_updates_it` | `Put_template_with_invalid_fields_returns_400_…`, `Put_template_for_unknown_store_returns_404` |

## 6. Build notes

- Verified with `docker compose up --build`, then a browser walkthrough: create an event through the wizard, see it on the calendar and open its popup, create a template, register a player from `/` and see the count change, and open a registration link for an unknown event ("Event not found").
- Test results at the time of the first PR: backend 68 unit + 28 integration tests passing; frontend 22 component tests passing.
- After the .ics change: backend 75 unit + 30 integration tests passing; frontend 22 component tests passing.
- FullCalendar is pinned to v6 (`@fullcalendar/react`, `core`, `daygrid` 6.1.x). The v7 React package pulls in core v7, which doesn't match the v6 plugins.
