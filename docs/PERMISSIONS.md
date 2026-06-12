# Permissions & Authorization

The authorization model: roles, the permission matrix, how enforcement works, and how tenant isolation is kept
airtight.

**Related:** [BUSINESS_RULES.md](BUSINESS_RULES.md) · [DATABASE.md](DATABASE.md) · [AUTHENTICATION.md](AUTHENTICATION.md)

## Roles (three identities)

| Identity | Source | Resolved per request |
|---|---|---|
| **Platform Admin** | `AppUser.IsPlatformAdmin` → JWT `is_admin` | `ICurrentUser.IsAdmin` |
| **Tenant Owner** | `UserTenantRole.Role == Owner (1)` | `GetRoleAsync(userId, X-Tenant-Id)` |
| **Tenant Client** | `UserTenantRole.Role == Client (2)` | same, per tenant |
| *(Non-member)* | role lookup → `null` | every permission → `false` → 403 |

Tenant role is **not** a JWT claim — it is looked up from the DB on each request, keyed by `domainUserId` +
the `X-Tenant-Id` header, and memoized per request (`IRequestRoleCache`). A user can be Owner in one tenant and
Client in another. There is no separate Coach/Trainer enum (Owner = coach, Client = trainee; "self-trainer" is an
Owner with no clients — a UI label only).

Platform Admin is **orthogonal**: it bypasses EF filters and gates the exercise catalog + `/api/admin/*`, but it
is not a tenant member, so tenant-permission checks return `false` for an admin (role = null). Admin reaches
tenant data via the EF filter bypass, not the permissioned write paths.

## Permissions

`Permission` enum (`BuildingBlocks.Shared/Authorization/Permission.cs`):
`PlanCreate, PlanUpdate, PlanDelete, PlanAssign, PlanView, PlanViewAll, ClientView, ClientRemove, InviteCreate,
WorkoutLogCreate, WorkoutLogViewOwn, WorkoutLogViewAll, NutritionPlanCreate, NutritionPlanUpdate,
NutritionPlanDelete, NutritionPlanAssign, NutritionLogCreate, NutritionLogViewOwn, NutritionLogViewAll`.

| Permission | Owner | Client |
|---|:---:|:---:|
| PlanCreate / PlanUpdate / PlanDelete / PlanAssign | ✓ | — |
| PlanView | ✓ | ✓ |
| PlanViewAll | ✓ | — |
| ClientView | ✓ | ✓ |
| ClientRemove | ✓ | — |
| InviteCreate | ✓ | — |
| WorkoutLogCreate | ✓ | ✓ |
| WorkoutLogViewOwn | ✓ | ✓ |
| WorkoutLogViewAll | ✓ | — |
| NutritionPlanCreate / Update / Delete / Assign | ✓ | — |
| NutritionLogCreate | ✓ | ✓ |
| NutritionLogViewOwn | ✓ | ✓ |
| NutritionLogViewAll | ✓ | — |

The Nutrition log permissions mirror the Workout log family (Create/ViewOwn for trainees, ViewAll for
coaches). Trainee nutrition **writes** are **tenant-scoped** on `api/nutrition/log/*` — the four log-write
commands are `ITenantAuthorizedRequest` (`NutritionLogCreate`, held by Owner AND Client), gated declaratively by
`AuthorizationBehavior` + membership-validated `TenantResolutionMiddleware`, exactly like `StartSessionCommand`
on `api/sessions` (handlers still scope to `currentUser.UserId` for defense in depth). Off-plan logging needs no
active assignment: a self-logged day is stamped with the **active gym** (`ITenantContext.TenantId`), so it stays
tenant-isolated from coaches in other gyms. Trainee **reads** and the personal `MetricEntry` series stay
self-scoped on `api/me/nutrition/*` (handler-gated by `currentUser.UserId`, classified `ImperativeGuarded`).
Detail: [nutrition/API_AND_PERMISSIONS.md](nutrition/API_AND_PERMISSIONS.md).

## Capability matrix

| Feature | Role | View | Create | Edit | Delete | Assign |
|---|---|:--:|:--:|:--:|:--:|:--:|
| Exercise catalog (global) | Platform Admin | ✓ | ✓ | ✓ | ✓ | — |
| Exercise catalog | Owner / Client | ✓ (read via `PlanView`) | — | — | — | — |
| Workout plans | Owner | ✓ (all in tenant) | ✓ | ✓ | ✓ | — |
| Workout plans | Client | ✓ (assigned only) | — | — | — | — |
| Plan assignments | Owner | ✓ (all) | ✓ | ✓ | ✓ | ✓ (`PlanAssign`) |
| Plan assignments | Client | ✓ (own only) | — | — | — | — |
| Invites | Owner | ✓ | ✓ | — | ✓ (revoke) | — |
| Join via code | any authed user | — | — | — | — | redeem → Client |
| Tenant members | Owner | ✓ (all) | — | — | ✓ (`ClientRemove`) | — |
| Tenant members | Client | ✓ (self + Owners) | — | — | — | — |
| Workout sessions / logs | Owner | ✓ (own + all via `ViewAll`) | ✓ (own) | ✓ (own) | ✓ (own sets) | — |
| Workout sessions / logs | Client | ✓ (own only) | ✓ (own) | ✓ (own) | ✓ (own sets) | — |
| Progress / reports | Owner | ✓ (any trainee in tenant) | — | — | — | — |
| Progress / reports | Client | ✓ (own only) | — | — | — | — |
| Tenants / users (cross-tenant) | Platform Admin | ✓ | (self-service) | promote/demote | ✓ | — |
| Create own tenant / leave | any authed user | — | ✓ (→ Owner) | — | leave (not last Owner) | — |

There is no approval workflow in the product.

## Enforcement model

1. **`[Authorize]`** on controllers → 401 if no JWT. There are no named policies or fallback policy; `[Authorize]` enforces *authentication only*. Unattributed actions are anonymous (register/login/refresh/logout/forgot/reset — these authenticate via the refresh cookie or request body). JWT validation also runs a per-request **SecurityStamp** check so revoked tokens 401 before expiry (see [AUTHENTICATION.md](AUTHENTICATION.md)). The whole `AuthController` is **rate-limited** per client IP (`auth` = 10/min, `auth-refresh` = 30/min) → 429 over limit.
2. **Tenant permission (static):** a validated tenant (`X-Tenant-Id` → `ITenantContext.TenantId`) + `HasPermissionAsync(tenantId, Permission.X)` → 400 if tenant missing, 403 if false. For requests needing exactly one static permission, this runs in `AuthorizationBehavior`; others call it in the handler.
3. **Row-level:** compare `ICurrentUser.UserId` to the resource owner/trainee, or use `CanAccessResourceAsync(tenant, ownPerm, allPerm, resourceUserId, resourceTenantId?)` — grants if the role has the *own* permission AND `resourceUserId == caller`, **or** the *all* permission AND the resource lives in the caller's own gym (`resourceTenantId == tenant`; a `null` resource tenant keeps the legacy behavior where the EF filter does the scoping). Passing the resource's tenant bounds a `WorkoutLogViewAll` coach to **their own gym** instead of relying on the global filter alone. Trainee-scoped session/report reads use `ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync`.
4. **Platform-admin operations:** requests implement `IPlatformAdminRequest`; `PlatformAdminBehavior` checks `ICurrentUser.IsAdmin` before the handler, on top of the controller-level `PlatformAdmin` policy (defense in depth).
5. **Frontend:** `adminGuard()` gates `/exercises` and `/admin/*`; `roleGuard(['Owner'])` gates trainer screens. The UI is never the security boundary — all enforcement is server-side.

### MediatR pipeline (hybrid authorization)

Registered in `Program.cs`, order matters: `ValidationBehavior` → `AuthorizationBehavior` → `PlatformAdminBehavior`.

| Piece | Location | Role |
|---|---|---|
| `Permission`, `TenantRole` | `BuildingBlocks.Shared/Authorization/` | Permission enum + tenant role values |
| `ITenantAuthorizedRequest` | `BuildingBlocks.Application/Authorization/` | Marks a request needing validated tenant + one static `RequiredPermission` |
| `AuthorizationBehavior` | same | Before the handler: null tenant → 400; permission denied → 403. Skips requests not implementing the interface |
| `IPlatformAdminRequest` / `PlatformAdminBehavior` | same | Marker + behavior for admin-only operations: non-admin → 403 (`AdminOnly`) |
| `IPermissionService`, `ITenantAuthorizationService` | same | Static permission matrix + tenant-scoped checks |
| `ITenantRoleResolver` / `TenantRoleResolver` | Application interface / `Modules.User` impl | DB membership lookup for role resolution |
| `ResourceAccessGuard` | `BuildingBlocks.Application/Authorization/` | Shared trainee-scoped read guard |

**When adding an endpoint:** implement `ITenantAuthorizedRequest` if the prelude is only tenant + one
permission; implement `IPlatformAdminRequest` for admin-only operations; otherwise keep
ownership / `CanAccessResourceAsync` / route-`tenantId` checks in the handler. Imperative cases are listed in
`Tests/Authorization/TenantAuthorizationExemptions.cs`, each with a documented reason and `ExemptionKind`
(`AuthenticationOnly` / `ImperativeGuarded` / `InternalLookup`).

**Convention tests** keep this honest: `TenantScopedRequestConventionTests` reflects over the controllers and
fails the build if any dispatched request is neither declaratively authorized nor on the exemption allowlist; it
also asserts each `ImperativeGuarded` handler references an authorization primitive in source.

### Tenant isolation (security-critical) — defense in depth

**`X-Tenant-Id` is never trusted raw.** `TenantResolutionMiddleware` (wired after authentication) reads the
header and stores the id in `HttpContext.Items` **only after** verifying the authenticated caller is a member of
that tenant via `IUserTenantRoleRepository.GetByUserAndTenantAsync` (platform admins bypass, matching the EF admin
bypass). `CurrentUser.TenantId` reads only that validated item — never the raw header. A spoofed header naming a
tenant the caller doesn't belong to is ignored → `TenantId` resolves to `null` → tenant-scoped EF queries match
nothing **and** permission checks fail.

Isolation is therefore enforced at two layers — membership-validated middleware **and** per-request permission
checks. Keep both; do not remove handler checks that gate row-level or hybrid access. Cross-tenant read and write
isolation is covered by the integration suite (see [TESTING.md](TESTING.md)).

**Unified personal reads (`api/me/*`) — the one sanctioned filter bypass.** The unified personal training
endpoints read a user's own data *across* gyms, so they intentionally bypass the EF tenant filter
(`IgnoreQueryFilters`) while re-applying soft-delete and scoping **strictly to `currentUser.UserId`** — never a
client-supplied id (see `QueryOwnAcrossGyms` and `ResolveOwnPlanContextQuery`). Because they are self-scoped, not
tenant-scoped, they are listed in `TenantAuthorizationExemptions` as **`ImperativeGuarded`**
(`GetMyWorkoutHistoryQuery`, `GetMyWorkoutSessionByIdQuery`, `GetMyPersonalRecordsQuery`, `GetMyProgressQuery`,
plus the now self-scoped `GetActiveSessionQuery`). This is the **only** approved tenant-filter bypass: it returns
exclusively the caller's own rows, and a foreign session id resolves to **404**. Coach/owner views of a gym's
members remain on the tenant-scoped, filtered endpoints — no cross-gym visibility.
