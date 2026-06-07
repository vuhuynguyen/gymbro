# GymBro API — Backend Guide

> **Purpose:** The one necessary backend doc — solution layout, build/run/migrate, conventions, and how to add a feature.
> **Read when:** Building/changing a handler, endpoint, entity, validator, or migration in `gymbro/`.
> **Answers:** Where do things live? How do I run/migrate/test? What patterns are mandatory? How do I add a feature safely?
> **Related (system facts live in the central SSOT):** [`../../docs/README.md`](../../docs/README.md) → MODULES (boundaries/APIs), PERMISSIONS (authorization), BUSINESS_RULES (lifecycles), DATABASE (ownership/tenancy). Repo conventions: [`../CLAUDE.md`](../CLAUDE.md).

## Solution layout
```
gymbro/
├── Presentations/WebApi/        # thin MVC controllers, Program.cs (composition root), Middleware, Requests, Composition (DbSeeder)
├── Modules/Modules.{Identity,User,Exercise,WorkoutPlan,WorkoutSession}/
│       Application/ (Commands, Queries, Handlers, Validators, DTOs)  ·  Entities/  ·  Infrastructure/ (Identity only)
└── BuildingBlocks/
        BuildingBlocks.Shared/         # Result/Error, domain primitives, marker interfaces, ICurrentUser/ITenantContext
        BuildingBlocks.Application/    # pipeline behaviors (Validation/Authorization/PlatformAdmin) + markers, ResultPipelineHelper, IUnitOfWork/IRepository
        Infrastructure/                # AppDbContext (filters/soft-delete/audit/event dispatch), repositories, CurrentUser, TokenService
```
Cross-module events use **MediatR notifications** (no event-bus project).
Module ownership, public APIs, and forbidden dependencies: [`../../docs/MODULES.md`](../../docs/MODULES.md).

## Run / migrate / seed
```bash
# Apply BOTH migration chains (forgetting one half-migrates the DB)
dotnet ef database update --project BuildingBlocks/Infrastructure/BuildingBlocks.Infrastructure.Persistence --startup-project Presentations/WebApi
dotnet ef database update --project Modules/Modules.Identity --startup-project Presentations/WebApi
dotnet run --project Presentations/WebApi      # OpenAPI + Scalar in dev
```
Config `ConnectionStrings:Database` + `Jwt:*` via **user-secrets/env**, never committed (`UserSecretsId` on `WebApi.csproj`). `ConnectionStrings:Redis` (Redis Cloud / StackExchange) is registered via `AddGymBroDistributedInfrastructure` — shared `IDistributedCache` for the exercise catalog (generation invalidation) and SecurityStamp revocation (`ISecurityStampCacheService.EvictAsync`), plus Redis-backed auth rate limiting (`RedisRateLimiting.AspNetCore`). **Required** unless `Cache:Provider=Memory` is set explicitly (tests / single-instance only). See [`../../docs/DEPLOYMENT.md`](../../docs/DEPLOYMENT.md). Secret rotation completed 2026-05-30 — see [`SECRETS_RUNBOOK.md`](SECRETS_RUNBOOK.md). Seeded admin (Development only): `admin@gymbro.local` / `Admin@123456`. Two DbContexts (`AppDbContext`, `IdentityDbContext`) — details in [`../../docs/DATABASE.md`](../../docs/DATABASE.md).

**Operational endpoints:** `GET /health` (liveness) + `GET /health/ready` (DB + Redis readiness), both anonymous. Cache metrics via `GymBro.Cache` meter (`cache.hits`, `cache.misses`, `cache.sets`, `cache.removes`). The exercise-catalog cache invalidates via `ICacheGenerationCounter` (generation-suffixed keys bumped on catalog writes). Structured JSON logging (per-request TraceId/RequestId scopes) outside Development; readable console in dev. Owned by [`../../docs/SYSTEM_OVERVIEW.md`](../../docs/SYSTEM_OVERVIEW.md).

## Mandatory conventions
- **`Result`/`Result<T>` everywhere** — handlers never throw for business rules; controllers map `Error.Code` → HTTP. Unexpected exceptions → `GlobalExceptionHandler` (no stack traces to clients).
- **Thin controllers** — bind request → dispatch one MediatR command/query → map `Result`. No business logic.
- **API versioning is header-based (not in the URL)** — controllers keep clean routes (`[Route("api/sessions")]`). `ApiVersionMiddleware` + `WebApi.Http.ApiVersioning` negotiate the version from `X-Api-Version` (or the `Accept` media-type `v=` param), default to latest, reject an explicit unsupported version (400), and echo the resolved `X-Api-Version`. A breaking change adds an entry to `ApiVersioning.Supported` and branches on the resolved version — no route or cookie-path churn. (When online, this thin layer can be swapped for `Asp.Versioning.Mvc` with the same header reader.)
- **One handler per use case**; **FluentValidation** validators live beside the command. MediatR pipeline (in order): `ValidationBehavior` → `AuthorizationBehavior` → `PlatformAdminBehavior` — all in `BuildingBlocks.Application/Authorization` (the `TenantRoleResolver` membership lookup, memoized per request via `IRequestRoleCache`, lives in `Modules.User/Application/Authorization`). See [`../../docs/PERMISSIONS.md`](../../docs/PERMISSIONS.md).
- **Namespaces** `Modules.<Module>Module.<Layer>.<Subfolder>` (note: folders are `Modules.X`, namespaces `Modules.XModule`).
- **Authorization** — hybrid model: implement `ITenantAuthorizedRequest` when a request needs validated tenant + one static permission (`AuthorizationBehavior` runs before the handler); keep ownership, `CanAccessResourceAsync`, route-`tenantId`, and admin bypass in handlers. Full model: [`../../docs/PERMISSIONS.md`](../../docs/PERMISSIONS.md).
- **Tests** — run `dotnet test Tests/Gymbro.Tests.csproj`. ⚠ Integration tests (Testcontainers) **self-skip without Docker** (or a `GYMBRO_TEST_DB` connection). Full strategy, inventory, and gaps: [`../../docs/TESTING.md`](../../docs/TESTING.md).

## How to add a feature (checklist)
1. **Domain:** add/extend the aggregate under `Modules.<Feature>/Entities` (private setters + factory invariants).
2. **Persistence:** add an `IEntityTypeConfiguration` + `DbSet` in `AppDbContext`; pick the right marker (`ITenantEntity`/`ISharedEntity`/`ISoftDelete`); add a migration (correct context). See [`../../docs/DATABASE.md`](../../docs/DATABASE.md).
3. **Application:** add the command/query + handler (returns `Result<T>`) + validator, in the same module. For a single static tenant permission, implement `ITenantAuthorizedRequest` on the request; otherwise enforce auth in the handler (`HasPermissionAsync`, `CanAccessResourceAsync`, `ResourceAccessGuard`, ownership). Update `TenantAuthorizationExemptions` only when imperative auth is intentional.
4. **API:** add the controller action with a clean route (`[Route("api/…")]` — versioning is header-based, see conventions); map `Result` → HTTP (no throwing for business errors).
5. **Docs:** update the **one** central `/docs` file that owns the changed fact (MODULES for APIs/deps, PERMISSIONS for authz, BUSINESS_RULES for lifecycle, DATABASE for entities). Do not duplicate.
