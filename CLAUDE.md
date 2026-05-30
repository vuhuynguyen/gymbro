# GymBro API — Claude Instructions

Project guide (build/run/migrate/conventions): [`docs/BACKEND.md`](docs/BACKEND.md)
Root brief: [`../CLAUDE.md`](../CLAUDE.md) · System single source of truth: [`../docs/README.md`](../docs/README.md)

## Read before any task (the 6 docs own these facts — don't re-scan)

| Task | Read |
|---|---|
| Architecture / where code belongs | [`../docs/MODULES.md`](../docs/MODULES.md), [`../docs/SYSTEM_OVERVIEW.md`](../docs/SYSTEM_OVERVIEW.md) |
| Domain entities / persistence | [`../docs/DATABASE.md`](../docs/DATABASE.md) |
| Endpoints / module APIs | [`../docs/MODULES.md`](../docs/MODULES.md) |
| Auth / tenant / roles / permissions | [`../docs/PERMISSIONS.md`](../docs/PERMISSIONS.md) |
| Lifecycle rules (plan/assignment/session/membership) | [`../docs/BUSINESS_RULES.md`](../docs/BUSINESS_RULES.md) |
| Flows (onboarding, invites) | [`../docs/USER_FLOWS.md`](../docs/USER_FLOWS.md) |

## Hard rules

- **Result<T> everywhere** — handlers never throw; controllers map `Result` → HTTP (thin controllers only).
- **EF global filters** — tenant isolation + soft-delete; admin bypasses both. Isolation: `AuthorizationBehavior` + handler row-level checks — never weaken (see `../docs/PERMISSIONS.md`).
- **`X-Tenant-Id`** required for tenant-scoped calls; read it via `ICurrentUser`/`ITenantContext`, never raw.
- **FluentValidation** beside the command. MediatR pipeline: `ValidationBehavior` → `AuthorizationBehavior`.
- **Namespaces** — `Modules.<Module>Module.<Layer>.<Subfolder>`.

## Update rule

When you change behavior, update the **one** `../docs/*` file that owns that fact (see `../docs/README.md`). Do not
duplicate facts across docs. Two migration chains exist — run both (`../docs/DATABASE.md`).
