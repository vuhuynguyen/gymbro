# Testing

The test strategy, what is covered, and how to run it.

**Related:** [PERMISSIONS.md](PERMISSIONS.md) (its convention tests enforce the authorization rules) · [ARCHITECTURE.md](ARCHITECTURE.md)

## How to run

- **Backend** — `dotnet test Solution.sln` (or `dotnet test Tests/Gymbro.Tests.csproj`). xUnit + NSubstitute + Testcontainers.
- **Integration tests need a database** — a reachable Docker daemon (Testcontainers starts `postgres:16-alpine`) **or** a `GYMBRO_TEST_DB` connection string. Locally, without either, they self-skip (dev convenience). **In CI this is not allowed to pass silently:** when `CI=true` and no database is reachable, `PostgresFixture` **throws** instead of skipping, so a misconfigured pipeline fails loudly.
- **CI** — `.github/workflows/ci.yml` restores, builds in Release (warnings-as-errors), scans for vulnerable packages, and runs the full suite against a Postgres **service container** with a line-coverage gate, so integration tests actually execute.
- **Frontend** — `cd ../GymBroPortal && ng test` (Karma + Jasmine, `ChromeHeadlessNoSandbox`). See the SPA repo's docs.

## Backend suite (`Tests/`)

| Layer | Folder | Covers |
|---|---|---|
| **Domain** | `Tests/Domain/` | Aggregate invariants — `WorkoutPlanVersioningTests` (immutable version chain, deep-copy), `WorkoutSessionTests` (state machine + guards + duration + event), `PerformedSetTests` (Epley e1RM, edit/recalc). |
| **Authorization** | `Tests/Authorization/` | `AuthorizationBehaviorTests`, `PlatformAdminBehaviorTests`, `PermissionServiceTests`, `TenantRoleResolverTests` (per-request memoization), `TenantResolutionMiddlewareTests` (X-Tenant-Id spoof rejection), plus the convention tests below. |
| **Command handlers** | `Tests/Commands/` | Mocked handler tests for the high-consequence lifecycle handlers: `Register`, `AdminDeleteUser`, `StartSession`, `RemoveMember`, `DeleteWorkoutPlan` (delete-guard), `UpdatePlanAssignmentToLatestVersion` (apply-latest snapshot preservation + archived guard), `SetPlanAssignmentActive` (pause/resume), `CreatePlanAssignment` (member + duplicate guards), `Complete/AbandonSession` (state machine + ownership), `LeaveTenant` (last-owner guard), `SetWorkoutPlanArchived`. |
| **Outbox** | `Tests/Outbox/` | Serializer round-trip, `SaveChanges` drains domain events into the outbox, dispatcher publishes/marks/records-failure and respects the attempt cap. |
| **Middleware** | `Tests/Middleware/` | `GlobalExceptionHandlerTests` — `DomainException` → 400 with message; other exceptions → 500 with no leak. |
| **Caching** | `Tests/Caching/` | `DistributedCacheExtensionsTests` (single-flight, negative envelope), `CacheTelemetryTests`, `SecurityStampCacheServiceTests`. |
| **Persistence** | `Tests/Persistence/` | `EntityFilterConventionTests` (every mapped entity is tenant/shared/soft-delete-filtered or on a documented allowlist), `ModuleBoundaryConventionTests` (build-fails on cross-module `.Entities` references), `GlobalFilterLiveReadTests` (admin-bypass filter re-evaluates per query). |
| **Integration** | `Tests/Integration/` | Real-Postgres end-to-end (below). |

### Convention tests — the architectural guardrails

- `Tests/Authorization/TenantScopedRequestConventionTests` reflects over the WebApi controllers and **fails the build** if any dispatched MediatR request is neither `ITenantAuthorizedRequest` nor `IPlatformAdminRequest` nor on the documented exemption allowlist. The enforcement arm of [PERMISSIONS.md](PERMISSIONS.md).
- `Tests/Persistence/ModuleBoundaryConventionTests` fails the build if a module's compiled IL reaches into another module's `.Entities` namespace. The enforcement arm of [ARCHITECTURE.md](ARCHITECTURE.md).

### Integration suite (Testcontainers)

`PostgresFixture` spins a throwaway Postgres, applies **both** migration chains, wires the real MediatR pipeline +
persistence + a mutable principal, and seeds a two-tenant fixture.

| Test | Proves |
|---|---|
| `TenantIsolationFilterTests` | EF global filter re-evaluates per request (cross-tenant row hidden). |
| `CrossTraineeAccessTests` | Session read scoping / IDOR via `ResourceAccessGuard`. |
| `CrossTenantWriteIsolationTests` | Cross-tenant write isolation — plan update/delete 404 (filter), member-remove 403 (permission). |
| `CrossTenantAssignmentAndInviteIsolationTests` | Cross-tenant assignment + invite scoping; authz fails closed when the resolved tenant isn't the caller's. |
| `SessionMutationIdorTests` | Cross-trainee/cross-tenant complete/abandon rejected before mutation. |
| `RefreshTokenReuseTests` | Rotation + reuse detection burns the token family. |
| `CrossStoreTransactionTests` | Register / admin-delete cross-store atomicity + rollback on a forced second-store failure. |
| `ExerciseDetail/SearchCacheInvalidationTests` | Cache eviction on catalog mutation. |

## Frontend specs

Pure-logic tests (`session-metrics.spec.ts`, `plan-meta.spec.ts`) plus the core auth flow
(`core/auth/auth.spec.ts` — silent-refresh single-flight + session-clear on failure; `error-interceptor.spec.ts` —
401 → refresh → replay with fresh headers). The rest are smoke tests. CI runs them headless.

## Known gaps

- **Handler breadth** — the highest-consequence lifecycle handlers have dedicated tests; the remaining mutation handlers lean on the integration + convention layers.
- **Frontend coverage is light** — the auth refresh/interceptor flow is covered; other stateful pieces (tenant-switch reset, active-session sync, guard load-order) are not yet.
