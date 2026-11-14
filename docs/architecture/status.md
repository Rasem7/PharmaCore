# Implementation status

Last updated: 2026-11-06. Repository was empty at start (no WPF/Blazor scaffold to migrate).

## Written, NOT yet compiled or run
- Backend: JWT login with lockout and rate limiting, PBKDF2 hashing, permission-policy authorization, Products CRUD/search/pagination on Dapper (tenant-scoped, parameterized, LIKE-escaped), health check (incl. SQL Server), Serilog, Swagger, startup migrator, dev-only admin seed from configuration.
- Database: `001_initial_core_and_products.sql` (tenants, branches, warehouses, users, roles, permissions, audit, products, outbox).
- Angular: login, shell (sidebar/header), dashboard (API health indicator only), products list + create/edit form, AR/EN with RTL/LTR switching, auth guard/interceptor.
- Electron: window, sandboxed preload, narrow IPC (config, print dialog).
- Tests: password hasher and product validator (xUnit).

## Added 2026-11-09 (also NOT compiled or run)
- Inventory backend: `002_inventory.sql` (batches, stock balances with non-negative CHECK, append-only movements with idempotency key), `FefoAllocator` (pure, with unit tests), receive/adjust/stock/expiring endpoints under `/api/inventory`, transactional Dapper repository. No Angular inventory page yet. No API to list warehouses yet (the dev seed creates one; its id must be read from the DB).

## Added 2026-11-09 (evening): verified on the developer's machine
Backend builds, 13 tests passed, API ran, migrations 001-002 applied (16 tables), login worked, Angular shell loaded in Arabic/RTL and the dashboard showed the API as connected.

## Added after that: drug reference catalogue (NOT yet compiled or run)
`003_drug_reference.sql`, `DrugNormalizer`, `CsvParser`, staged CSV import, search and alternatives endpoints under `/api/drug-reference`, Angular "Drug reference" page, prefill into Products. See `docs/migration/drug-reference-data.md`.

## Verified on the developer's machine (2026-11-09/10)
Drug reference: migration 003 applied, 25,070-row CSV imported (25,066 inserted, 0 rejected, 4 in-file duplicates collapsed), search, alternatives and prefill into Products reported working.

## Added 2026-11-10 (NOT yet compiled or run)
Inventory UI (`/inventory`): warehouse picker, stock table with expiry badges (display only), receive-stock form with product search and a reusable idempotency key, adjustment with mandatory reason, `GET /api/inventory/warehouses`.

## Not implemented
Drug interactions (no data source chosen), XLSX import, product-to-catalogue link, Inventory UI, stock transfers/counting, POS, purchasing, customers, finance, reports, dashboard KPIs, users/roles UI, branch-level isolation beyond tenant scoping, audit of product changes, Sync project (outbox publisher, inbox, conflicts, checkpoints), Migration Center, receipt printing, barcode scanner handling, local branch server, Swagger bearer auth definition, Angular `ng` i18n extraction (a JSON-dictionary service is used instead).

## Decisions
- JSON-dictionary i18n service instead of Angular compile-time i18n, so language switches at runtime without separate builds (needed for desktop).
- Hash routing so the build works under `file://` in Electron.
- Custom SCSS design system instead of Angular Material (fewer dependencies, full RTL control).
- Tenant id comes from the JWT, never from request bodies.
- Package versions use wildcards/carets and have not been resolved; pin them after the first successful restore.

## Known risks to check on first build
Package version availability for .NET 10 / Angular 20 / Electron 38; Swashbuckle with the .NET 10 OpenApi package; `PharmaCore.slnx` support in your SDK.
