# PharmaCore

Pharmacy management system for Egyptian pharmacies: ASP.NET Core API (Dapper, SQL Server) + Angular + Electron.

> **Status: early foundation. Written but not yet compiled or run.** The authoring environment had no .NET SDK and no network for package installs, so nothing here has been built, tested or executed. See `docs/architecture/status.md` for exactly what exists and what is unverified.

## Layout

| Path | Purpose |
|---|---|
| `backend/PharmaCore.Domain` | Entities and business rules |
| `backend/PharmaCore.Application` | DTOs, validators, services, repository interfaces |
| `backend/PharmaCore.Persistence` | Dapper repositories, connection factory, SQL migrator |
| `backend/PharmaCore.Infrastructure` | Password hashing (PBKDF2), JWT |
| `backend/PharmaCore.Api` | Controllers, auth, health checks, Swagger, Serilog |
| `backend/PharmaCore.Tests` | xUnit tests (hasher, validators) |
| `database/migrations` | Numbered SQL scripts, applied once each and tracked in `dbo.SchemaVersions` |
| `frontend/pharmacore-angular` | Angular standalone app (AR/EN, RTL/LTR) |
| `desktop/pharmacore-electron` | Electron shell around the same Angular build |

`PharmaCore.Sync` and `PharmaCore.Migration` are deliberately not created yet (no empty placeholders). Only the `OutboxMessages` table exists.

## Prerequisites

.NET 10 SDK, SQL Server (LocalDB, Express or Docker), Node.js 22+, npm.

## Run the backend

```bash
cd backend/PharmaCore.Api
dotnet user-secrets set "ConnectionStrings:PharmaCore" "Server=localhost;Database=PharmaCore;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SigningKey" "<random string, 32+ chars>"
dotnet user-secrets set "DevSeed:AdminPassword" "<a dev password>"
dotnet run          # http://localhost:5080/swagger
```

Create an empty `PharmaCore` database first. In Development the app applies `database/migrations` on startup and creates user `admin` with the seed password if no users exist.

Tests: `dotnet test backend/PharmaCore.slnx`

## Run the frontend

```bash
cd frontend/pharmacore-angular
npm install
npm start           # http://localhost:4200
```

The API URL defaults to `http://localhost:5080` (`src/app/core/api-config.ts`).

## Run the desktop shell

```bash
cd frontend/pharmacore-angular && npm run build:electron
cd ../../desktop/pharmacore-electron && npm install && npm start      # or: npm run dist  (Windows installer)
```

For live development: `npm run start:dev` (loads the Angular dev server).
