# AutoNexus, fleet management

AutoNexus manages the vehicles, maintenance interventions and branches of a group of car dealerships. The back end is ASP.NET Core 8 and the front end React with TypeScript. Access depends on the user's role, and the demo accounts are listed [below](#demo-accounts).

## Features

The dashboard shows live figures (vehicle count, availability rate, intervention statistics) with Recharts charts.

Vehicles can be created, edited and deleted. The VIN is validated, the status follows a fixed lifecycle (Available, then InIntervention, then Sold or OutOfService), deletion is soft, and lists are paginated with search and filters.

Interventions are planned, started, completed or cancelled. Each status change is checked, and a cancellation requires a reason.

The application handles several branches. Store managers and technicians only see their own branch.

There are three roles (Admin, StoreManager, Technician), and the API enforces them as well as the UI.

The JWT lives in an httpOnly cookie. Refresh tokens are rotated, revoked tokens are blacklisted by JTI, and the login endpoint is rate-limited.

## Tech stack

### Backend

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| Architecture | Clean Architecture + DDD |
| Messaging | MediatR (CQRS) |
| ORM | Entity Framework Core 8 |
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

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
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
| Technician | `tech1.paris@fleetmanager.fr` | Paris branch, read-only on vehicles |

The database seeder creates these accounts, along with 3 branches, 12 vehicles and 6 interventions in various states. On the login page, clicking a role card fills in its credentials.

## Running tests

```bash
cd tests/FleetManager.Tests
dotnet test
```

## API documentation

In Development mode, Swagger UI is served at the API root (`/`). The API is versioned through the URL (`/api/v1/...`).

Main endpoints:

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/v1/auth/login` | Login (rate-limited: 5 req/min) |
| POST | `/api/v1/auth/logout` | Logout + token revocation |
| POST | `/api/v1/auth/refresh` | Refresh access token |
| GET | `/api/v1/vehicles` | List vehicles (paginated, filterable) |
| POST | `/api/v1/vehicles` | Create vehicle |
| PATCH | `/api/v1/vehicles/{id}/status` | Change vehicle status |
| GET | `/api/v1/interventions` | List interventions (paginated, filterable) |
| POST | `/api/v1/interventions` | Create intervention |
| PATCH | `/api/v1/interventions/{id}/status` | Advance intervention status |
| GET | `/api/v1/stores` | List all branches |
| GET | `/health` | Health check |
