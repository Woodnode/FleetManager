# AutoNexus, fleet management

AutoNexus manages the vehicles, maintenance interventions and branches of a group of car dealerships. The back end is ASP.NET Core 10 and the front end React with TypeScript. Access depends on the user's role, and the demo accounts are listed [below](#demo-accounts).

## Features

The dashboard shows live figures (vehicle count, availability rate, intervention statistics) with Recharts charts.

Vehicles can be created, edited and deleted. The VIN is validated, the status follows a fixed lifecycle (Available, then InIntervention, then Sold or OutOfService), deletion is soft, and lists are paginated with search and filters.

Interventions are planned, started, completed or cancelled. Each status change is checked, and a cancellation requires a reason.

The application handles several branches. Store managers and technicians only see their own branch.

There are three roles (Admin, StoreManager, Technician), and the API enforces them as well as the UI.

The JWT lives in an httpOnly cookie. Refresh tokens are stored as SHA-256 hashes and rotated; replaying a used one revokes the whole session. Logout blacklists the access token's JTI until it expires, an admin can deactivate an account, and login and refresh are rate-limited per client IP.

## Tech stack

### Backend

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 |
| Architecture | Clean Architecture + DDD |
| Messaging | MediatR (CQRS) |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL 16 (Npgsql) |
| Validation | FluentValidation (pipeline behavior) |
| Auth | JWT Bearer + httpOnly cookies + Refresh Tokens |
| Logging | Serilog (console + rolling file) |
| API Docs | Swagger / OpenAPI with XML comments |
| Versioning | Asp.Versioning (URL segment) |
| Testing | xUnit + FluentAssertions + NSubstitute |

### Frontend

| Layer | Technology |
|---|---|
| Framework | React 18 + TypeScript |
| Build | Vite |
| Routing | React Router v6 (lazy-loaded routes) |
| Server state | TanStack Query (React Query) |
| Forms | React Hook Form + Zod |
| HTTP | Axios (interceptors: auto token refresh) |
| Styling | Tailwind CSS |
| Charts | Recharts |

## Architecture

```
FleetManagerV2/
├── src/
│   ├── FleetManager.Domain/          # Entities, Value Objects, Domain Exceptions
│   │   ├── Entities/                 # Vehicle, Intervention, Store, User, RefreshToken
│   │   ├── ValueObjects/             # Vin, Email
│   │   ├── Enums/                    # VehicleStatus, InterventionStatus, UserRole…
│   │   └── Interfaces/               # Repository contracts, ISoftDeletable
│   │
│   ├── FleetManager.Application/     # Use cases (CQRS handlers, validators, DTOs)
│   │   ├── Vehicles/                 # Commands + Queries
│   │   ├── Interventions/
│   │   ├── Stores/
│   │   ├── Auth/
│   │   ├── Behaviors/                # ValidationBehavior, AuditBehavior (MediatR pipeline)
│   │   └── Common/                   # Result<T>, Error, PagedResult
│   │
│   ├── FleetManager.Infrastructure/  # EF Core, repositories, JWT, BCrypt, seeders
│   │   ├── Persistence/
│   │   │   ├── Configurations/       # EF Fluent API mappings
│   │   │   ├── Migrations/
│   │   │   ├── Repositories/
│   │   │   └── DatabaseSeeder.cs
│   │   └── Services/                 # JwtTokenGenerator, PasswordHasher, CurrentUserService
│   │
│   └── FleetManager.Api/             # Controllers, Middleware, DTOs, Program.cs
│       ├── Controllers/
│       ├── Middleware/               # ExceptionHandlingMiddleware, SecurityHeadersMiddleware
│       └── DTOs/
│
├── tests/
│   └── FleetManager.Tests/           # Domain unit tests, Application handler tests
│
└── client/                           # React + TypeScript frontend
    └── src/
        ├── api/                      # Axios API clients per domain
        ├── components/               # Layout, ProtectedRoute, UI primitives
        ├── contexts/                 # AuthContext
        ├── pages/                    # Dashboard, Vehicles, Interventions, Stores, Login
        ├── schemas/                  # Zod validation schemas
        └── types/                    # Shared TypeScript interfaces
```

### Design decisions

The code follows Clean Architecture with a strict dependency rule. The Domain depends on nothing, the Application depends only on the Domain, and Infrastructure and API point inward.

Commands and queries are separate MediatR requests. Cross-cutting concerns (validation, audit logging) run as pipeline behaviors.

Entities hold their own business rules. For example, `Vehicle.ChangeStatus` throws if the vehicle is sold, and `Vin.Create` enforces the 17-character ISO format.

Handlers return `Result<T>` instead of throwing, so the controllers handle error paths explicitly.

JavaScript never sees the JWT access token, which stays in an httpOnly cookie. A rotating refresh token renews the session without the user noticing.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker + Docker Compose](https://www.docker.com/)

### Configure secrets

Secrets are never committed. Copy the template and fill in random values:

```bash
cp .env.example .env
# POSTGRES_PASSWORD=...   JWT_SECRET=... (32+ characters, e.g. `openssl rand -base64 32`)
```

### Run with Docker Compose (recommended)

```bash
# Start PostgreSQL + API
docker compose up --build
```

The API is then available at `http://localhost:5000`.

### Run locally (development)

Start PostgreSQL:

```bash
docker compose up postgres -d
```

Start the API. The connection string in `appsettings.json` has no password, so provide it through an environment variable (PowerShell shown, same value as `POSTGRES_PASSWORD` in `.env`):

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=fleetmanager;Username=fleetmanager;Password=<POSTGRES_PASSWORD>"
cd src/FleetManager.Api
dotnet run
# API: https://localhost:5290, Swagger UI at /
```

Start the frontend:

```bash
cd client
npm install
npm run dev
# Frontend: http://localhost:5173
```

On first startup, the database is migrated and filled with demo data.

## Demo accounts

All accounts share the same password, `Fleet@2024`.

| Role | Email | Access |
|---|---|---|
| Admin | `admin@fleetmanager.fr` | All stores and all data |
| Store Manager | `directeur.paris@fleetmanager.fr` | Paris branch only |
| Technician | `tech1.paris@fleetmanager.fr` | Paris branch, read-only on vehicles; starts and closes their own interventions |

The database seeder creates these accounts, along with 3 branches, 12 vehicles and 6 interventions in various states. On the login page, clicking a role card fills in its credentials.

On the public demo (`Demo__ResetEnabled=true`), the data is wiped and seeded again every night at 04:00 UTC, so changes made with these shared accounts, accounts created with them included, do not last.

## Mobile app (.NET MAUI)

`mobile/AutoNexusMobile` is the Android and iOS companion app. It uses the same API and the same visual identity as the web client (Inter, Lucide icons, `#4c6ef5` brand, light and dark themes).

It covers the web screens (dashboard, vehicles, interventions, stores, archives), each limited to what the user's role may do. Technicians start on "Ma journée", which lists today's assignments and the overdue ones. From an intervention they start or close the job; closing asks for a comment and the odometer reading, which updates the vehicle.

Scanning the VIN barcode on the door jamb or windscreen (ZXing.Net.MAUI) opens the vehicle, and the VIN can be typed when the label is unreadable. Changes made elsewhere show up live through the SignalR hub, as in the web client.

The app sends `X-Client: mobile` on authentication calls, and the API then returns the tokens in the response body (the browser keeps its httpOnly cookies). `mobile/AutoNexusMobile.Core` holds the API client, the session and the business rules; `mobile/AutoNexusMobile.Tests` tests it without the MAUI workload.

```bash
# Android emulator, against the local API (http profile, port 5290, reached through 10.0.2.2)
dotnet build mobile/AutoNexusMobile -t:Install -f net10.0-android -c Debug
```

The login screen offers the demo accounts in Debug builds only. A Release build needs the API host at compile time: `dotnet publish -p:AutoNexusApiHost=https://...`. Fonts and icons are regenerated from the web packages with `mobile/AutoNexusMobile/scripts/generer_ressources.py`.

## Running tests

```bash
dotnet test FleetManager.sln                          # API, domain, application, integration
dotnet test mobile/AutoNexusMobile.Tests              # mobile core (.NET 10)
cd client && npm run lint && npx tsc -b && npm test   # web client
```

## API documentation

In Development mode, Swagger UI is served at the API root (`/`). The API is versioned through the URL (`/api/v1/...`).

Main endpoints:

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/v1/auth/login` | Login (rate-limited: 5 req/min per IP) |
| POST | `/api/v1/auth/logout` | Logout + token revocation |
| POST | `/api/v1/auth/refresh` | Refresh access token |
| GET | `/api/v1/vehicles` | List vehicles (paginated, filterable) |
| POST | `/api/v1/vehicles` | Create vehicle |
| PATCH | `/api/v1/vehicles/{id}/status` | Change vehicle status (Admin, StoreManager) |
| GET | `/api/v1/interventions` | List interventions (paginated, filterable) |
| GET | `/api/v1/interventions/mine` | Technician's day: assigned interventions and overdue open ones |
| POST | `/api/v1/interventions` | Create intervention (Admin, StoreManager) |
| PATCH | `/api/v1/interventions/{id}/status` | Start, complete (optional odometer reading) or cancel; a technician only acts on their own |
| PATCH | `/api/v1/users/{id}/active` | Activate or deactivate an account (Admin) |
| GET | `/api/v1/stores` | List all branches |
| GET | `/health` | Health check |
