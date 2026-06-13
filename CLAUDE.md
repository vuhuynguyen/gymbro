# GymBro API — Claude Instructions

Start with [`README.md`](README.md). The [`docs/`](docs/) folder is the documentation source of truth — read the
one document that owns your topic; don't re-scan the repo.

## Read before any task

| Task | Read |
|---|---|
| Architecture / where code belongs / conventions | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Domain entities / persistence / tenancy | [`docs/DATABASE.md`](docs/DATABASE.md) |
| Auth / tenant / roles / permissions | [`docs/PERMISSIONS.md`](docs/PERMISSIONS.md) |
| Token lifecycle (access/refresh/revocation) | [`docs/AUTHENTICATION.md`](docs/AUTHENTICATION.md) |
| Lifecycle rules (plan/assignment/session/membership) | [`docs/BUSINESS_RULES.md`](docs/BUSINESS_RULES.md) |
| Flows / endpoints | [`docs/USER_FLOWS.md`](docs/USER_FLOWS.md) |
| Catalog seeding (exercises & foods) | [`docs/SEEDING.md`](docs/SEEDING.md) |
| Tests | [`docs/TESTING.md`](docs/TESTING.md) |
| Config / migrations / deploy | [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) |
| Not-yet-built design (nutrition future / exercise master-data) | [`docs/ROADMAP.md`](docs/ROADMAP.md) |

## Hard rules

- **Result<T> everywhere** — handlers never throw for business rules; controllers map `Result` → HTTP (thin controllers only).
- **EF global filters** — tenant isolation + soft-delete; admin bypasses both. Isolation is defense-in-depth (`TenantResolutionMiddleware` + `AuthorizationBehavior` + handler row-level checks) — never weaken it.
- **`X-Tenant-Id`** required for tenant-scoped calls; read it via `ICurrentUser`/`ITenantContext`, never raw.
- **FluentValidation** beside the command. MediatR pipeline: `ValidationBehavior` → `AuthorizationBehavior` → `PlatformAdminBehavior`.
- **Explicit DTO mapping** in `Application/Mapping/<Module>Mapping.cs` — not inline in handlers.
- **Namespaces** — `Modules.<Module>Module.<Layer>.<Subfolder>`.
- **Two migration chains exist — run both** (see [`docs/DATABASE.md`](docs/DATABASE.md) / [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)).

## Update rule

When you change behavior, update the **one** `docs/*` file that owns that fact. Do not duplicate facts across docs.
