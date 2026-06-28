# AutoNexus — Fleet Management Platform

A full-stack fleet management application built with **ASP.NET Core 8** and **React + TypeScript**. Manage vehicles, maintenance interventions, and dealership branches across multiple locations, with role-based access control.

> **Live demo credentials** — see the [Demo Accounts](#demo-accounts) section below.

---

## Features

- **Dashboard** — real-time KPIs (vehicle count, availability rate, intervention stats) with Recharts visualizations
- **Vehicle management** — full CRUD, VIN validation, status lifecycle (Available → InIntervention → Sold / OutOfService), soft delete, pagination + search/filter
- **Intervention tracking** — plan, start, complete or cancel maintenance interventions; status transitions with mandatory cancellation reason
- **Store management** — multi-branch support; store managers and technicians are scoped to their branch
- **Role-based access** — three roles (Admin, StoreManager, Technician) enforced on both the API and the UI
- **Secure authentication** — JWT stored in httpOnly cookies, refresh token rotation, token revocation (JTI blacklist), rate-limited login

---

## Tech Stack

### Backend
| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| Architecture | Clean Architecture + DDD |
| Messaging | MediatR (CQRS) |
| ORM | Entity Framework Core 8 |
| Database | SQL Server 2022 |
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

---

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

### Key design decisions

- **Clean Architecture** — strict dependency rule: Domain has no external dependencies; Application depends only on Domain; Infrastructure and API depend inward.
- **CQRS via MediatR** — commands and queries are cleanly separated; pipeline behaviors handle cross-cutting concerns (validation, audit logging).
- **Rich domain model** — entities encapsulate their business rules (e.g. `Vehicle.ChangeStatus` throws if the vehicle is sold; `Vin.Create` enforces the 17-character ISO standard).
- **Result pattern** — handlers return `Result<T>` instead of throwing exceptions, making error paths explicit at the controller layer.
- **httpOnly cookies** — the JWT access token is never exposed to JavaScript; a refresh token with rotation handles session renewal transparently.

---

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker + Docker Compose](https://www.docker.com/)

### Run with Docker Compose (recommended)

```bash
# Start SQL Server + API
docker compose up --build
```

The API will be available at `http://localhost:5000`.

### Run locally (development)

**1. Start SQL Server**
```bash
docker compose up sqlserver -d
```

**2. Start the API**
```bash
cd src/FleetManager.Api
dotnet run
# API: https://localhost:5290 — Swagger UI at /
```

**3. Start the frontend**
```bash
cd client
npm install
npm run dev
# Frontend: http://localhost:5173
```

The database is automatically migrated and seeded with demo data on first startup.

---

## Demo Accounts

All accounts share the same password: **`Fleet@2024`**

| Role | Email | Access |
|---|---|---|
| **Admin** | `admin@fleetmanager.fr` | Full access — all stores, all data |
| **Store Manager** | `directeur.paris@fleetmanager.fr` | Paris branch only |
| **Technician** | `tech1.paris@fleetmanager.fr` | Paris branch, read-only on vehicles |

These accounts are pre-loaded by the database seeder, which also creates 3 branches, 12 vehicles, and 6 interventions in various states.

On the login page, click any role card to auto-fill the credentials.

---

## Running Tests

```bash
cd tests/FleetManager.Tests
dotnet test
```

---

## API Documentation

Swagger UI is available at the API root (`/`) when running in Development mode. The API is versioned via URL segments (`/api/v1/...`).

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
