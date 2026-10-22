# Game Store Events

A web app where a game store lists upcoming events, players register for a seat, and the store admin creates events from reusable templates. Scope is a single store, "Basic Game Store".

- **Backend:** .NET 10 Web API (`backend/`), EF Core in-memory database, services behind interfaces.
- **Frontend:** React + Vite + TypeScript (`frontend/`), qrcode.react, FullCalendar.
- **Packaging:** Docker Compose runs both; nginx serves the app and proxies the API on the same origin.

See [applog.md](applog.md) for the logic and decisions made while building.

## Prerequisites

- Docker with the Compose plugin (`docker compose version`).

To run or test without Docker you also need:

- .NET SDK 10.0
- Node.js 22 and npm

## Run with Docker

```sh
docker compose up --build
```

Then open:

| URL | What |
| --- | --- |
| http://localhost:8080/ | Upcoming events (players) |
| http://localhost:8080/registration/{eventId} | Registration page for one event (linked from each event, and from its QR code), with a QR code and an .ics calendar download |
| http://localhost:8080/admin | Store events admin: Create Event, View Events, Manage Templates |
| http://localhost:5080/store-manager/stores/0199f0a0-0000-7000-8000-000000000001 | The API directly |

There are no events at first: create one on `/admin`. Data lives in memory and resets when the `api` container restarts.

Stop with `Ctrl+C`, or `docker compose down`.

## Configuration

| Setting | Where | Default | Purpose |
| --- | --- | --- | --- |
| `APP_BASE_URL` | `web` service (shell env or `.env` file next to `docker-compose.yml`) | `http://localhost:8080` | Public base URL used for the registration page's QR code and link (`APP_BASE_URL/registration/{eventId}`). Set it to an address phones can reach, e.g. `APP_BASE_URL=http://192.168.1.20:8080 docker compose up`. Read at container start, so no rebuild is needed. |
| `API_UPSTREAM` | `web` service | `http://api:8080` | Where nginx forwards `/store-manager` and `/event-manager` requests. |
| `STORE_ID` | `web` service | the seeded store | Store the landing page loads. |
| `Database__Name` | `api` service | `GameStore` | Name of the in-memory database. |
| Ports | `docker-compose.yml` | web `8080`, api `5080` | Host ports. |

When running the frontend outside Docker, `VITE_APP_BASE_URL` and `VITE_STORE_ID` do the same as `APP_BASE_URL` and `STORE_ID`; with neither set, links use the page's own origin.

## Run without Docker (development)

```sh
# Terminal 1: API on http://localhost:5080
cd backend
dotnet run --project src/GameStore.Api --urls http://localhost:5080

# Terminal 2: app on http://localhost:5173 (proxies API calls to :5080; override with API_URL)
cd frontend
npm install
npm run dev
```

## Tests

```sh
# Backend: unit tests (services with mocked repositories) and integration tests (in-process API host)
cd backend
dotnet test

# Frontend: component tests (Vitest + Testing Library)
cd frontend
npm install
npm test
```

Or run them inside Docker, with no local SDKs:

```sh
docker build --target test backend
docker build --target test frontend
```

| Suite | Covers |
| --- | --- |
| `backend/tests/GameStore.UnitTests` | Register success, VERSION_CONFLICT, EVENT_FULL, REMOVE, validation for players/events/templates, event creation and its Capacity row, template create (highest id + 1) and update, upcoming filter in the store time zone, seed data |
| `backend/tests/GameStore.IntegrationTests` | Success and error responses for every endpoint, and concurrent registrations never overselling seats |
| `frontend/src/__tests__` | Registration retry logic, "Event Full" state, the 4-step wizard, Manage Templates, landing page |

## API

All bodies are JSON with camelCase fields. Dates are `YYYY-MM-DD`, times `HH:mm` (store-local), durations whole minutes. Validation failures return 400 Problem Details with field `errors`; unknown ids return 404.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/store-manager/stores/{storeId}` | Store details |
| GET | `/event-manager/stores/{storeId}/events` | Store's upcoming events, with `registeredCount` |
| POST | `/event-manager/stores/{storeId}/events` | Create event (201) |
| GET | `/event-manager/events/{eventId}` | One upcoming event |
| GET | `/event-manager/events/{eventId}/capacity` | `{ remainingCapacity, versionId }` |
| GET | `/event-manager/events/{eventId}/calendar.ics` | iCalendar file for the event (name, start/end in UTC converted from the store time zone, store name as location), used by the registration page's "Add to Google Calendar (.ics)" link |
| POST | `/event-manager/register` | `ADD` or `REMOVE`; 409 Problem Details with `reason` `VERSION_CONFLICT` or `EVENT_FULL` |
| GET | `/event-manager/stores/{storeId}/templates` | Store's templates |
| PUT | `/event-manager/stores/{storeId}/templates` | Create (no or unknown `templateId`) or update a template |

The seeded store id is `0199f0a0-0000-7000-8000-000000000001`.

## Layout

```
backend/
  src/GameStore.Core            domain, DTOs, validation, services and repository interfaces
  src/GameStore.Infrastructure  EF Core in-memory DbContext, repositories, seeding
  src/GameStore.Api             controllers (store-manager, event-manager), Problem Details mapping
  tests/                        unit and integration tests
frontend/
  src/pages                     landing, registration, admin
  src/admin                     create-event wizard, calendar, template manager
  src/lib                       API client, registration retry flow, date formatting, config
  docker/                       nginx config and runtime config script
docker-compose.yml
```
