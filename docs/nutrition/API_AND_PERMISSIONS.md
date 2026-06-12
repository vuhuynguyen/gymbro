# Nutrition — API Impact, Permissions & Security

The endpoint surface, the dual self-scoped / tenant-scoped model, the new permissions, the wire format, and the
security/data-ownership posture. Everything here extends existing mechanisms — no new auth, no new pipeline.

**Related:** [PERMISSIONS.md](../PERMISSIONS.md) · [USER_FLOWS.md](../USER_FLOWS.md) ·
[AUTHENTICATION.md](../AUTHENTICATION.md) (untouched) · [DOMAIN_MODEL.md](DOMAIN_MODEL.md).

> **As-built (Phase 2–3).** The shipped surface is: **coach** `NutritionController` `/api/nutrition/*` —
> `plans` (list/get/create/`{id}/structure`/delete), `assignments` (list/create), `logs` (`?traineeId=` list,
> `/{date}` get); **tenant-scoped trainee writes** `NutritionLogController` `/api/nutrition/log/*` —
> `items` (add ad-hoc), `items/status`, `items/substitute`, `items/{itemId}` (delete); **trainee reads + metrics**
> `MeController` `/api/me/nutrition/*` — `today`, `days`, `days/{date}`, and `metrics` (GET/POST — the check-in
> slice, **built**). Trainee logging is **tenant-scoped** (requires `X-Tenant-Id`, `NutritionLogCreate`), mirroring
> workout sessions on `api/sessions`; reads and the personal metric series stay self-scoped.
> Off-plan `items` writes **no longer require an active assignment** — with none, a plan-less self-logged day is
> provisioned and stamped with the **active gym** (the `X-Tenant-Id` header) (§3, [DOMAIN_MODEL §3](DOMAIN_MODEL.md)). The
> **idempotent-write / `clientItemId` / `/sync` batch** design in §4 remains part of the **offline phase and is
> NOT built** — writes are ordinary (non-idempotent) for MVP. See the [as-built status](README.md).

## 1. Controllers & routes (clean, header-versioned)

Three controllers, all thin (bind → one MediatR command/query → `Result.ToFailureResult`), all on clean routes
(no `/v1` — `X-Api-Version` negotiation as today).

### `FoodController` — `/api/foods/*` (catalog, mirrors `ExerciseController`)

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/foods` | member (`PlanView`-equivalent read) | search/filter; distributed-cached like exercises |
| GET | `/api/foods/{id}` | member | full food + nutrients + servings |
| POST/PUT/DELETE | `/api/foods*` (global) | **platform admin** (`IPlatformAdminRequest`) | global catalog writes — 403 for non-admins even on the open route, exactly like exercises |
| POST/PUT/DELETE | `/api/foods/custom*` | Owner (`NutritionPlanCreate`) | tenant-custom foods (`OwnerTenantId = gym`) |

### `NutritionController` — `/api/nutrition/*` (tenant-scoped: coach surface + plan authoring)

| Method | Route | Auth (permission) | Mirrors |
|---|---|---|---|
| POST/PUT/DELETE | `/api/nutrition/plans*` | `NutritionPlanCreate/Update/Delete` | `WorkoutPlanController` plans |
| PUT | `/api/nutrition/plans/{id}/structure` | `NutritionPlanUpdate` | `…/structure` (one save = one new version) |
| POST | `/api/nutrition/assignments` | `NutritionPlanAssign` | assign + visibility + schedule |
| PUT | `/api/nutrition/assignments/{id}` (config, pause, resume, apply-latest) | `NutritionPlanAssign` | assignment lifecycle |
| GET | `/api/nutrition/logs?traineeId=&from=&to=` | `NutritionLogViewAll` | **coach** monitoring of a gym's clients |
| GET | `/api/nutrition/logs/{date}?traineeId=` | `NutritionLogViewAll` | coach drill-in to a client's day |
| GET | `/api/nutrition/adherence?traineeId=&from=&to=` | `NutritionLogViewAll` | **NOT BUILT** — coach adherence dashboard (designed; see [ANALYTICS_AND_COACHING.md](ANALYTICS_AND_COACHING.md)) |

### `NutritionLogController` — `/api/nutrition/log/*` (tenant-scoped trainee **writes** — mirrors `api/sessions`)

Trainee daily-log writes are **tenant-scoped**, exactly like workout sessions (`StartSessionCommand` on
`api/sessions`): each requires `X-Tenant-Id` (membership-validated by `TenantResolutionMiddleware`) and
`Permission.NutritionLogCreate` (held by **Owner AND Client**), gated declaratively by `AuthorizationBehavior`.
Handlers still scope every mutation to the caller's own day (`currentUser.UserId`); a nutrition day is unique
per `(trainee, date)` globally, so its tenant is simply the gym active when the day was first created.

| Method | Route | Auth (permission) | Returns |
|---|---|---|---|
| POST | `/api/nutrition/log/items` | `NutritionLogCreate` | add an off-plan item; with no active assignment a plan-less self-logged day is provisioned, **stamped with the active gym** (the `X-Tenant-Id` header). `201` with the new item id. A non-member of the active gym → `401 Unauthorized` (declarative gate); no tenant in context → clean `400` |
| POST | `/api/nutrition/log/items/status` | `NutritionLogCreate` | complete/skip a planned item on own day |
| POST | `/api/nutrition/log/items/substitute` | `NutritionLogCreate` | swap a food (records provenance) |
| DELETE | `/api/nutrition/log/items/{itemId}?date=` | `NutritionLogCreate` | remove an ad-hoc item — `204` |

### `MeController` += `/api/me/nutrition/*` (self-scoped, **cross-gym** — trainee reads + personal metrics)

Reads and the personal metric series stay **self-scoped** (no `X-Tenant-Id`), aggregating across all gyms.

| Method | Route | Returns |
|---|---|---|
| GET | `/api/me/nutrition/today` | today's `DailyNutritionLog` (lazily created if absent), across gyms |
| GET | `/api/me/nutrition/days/{date}` | a specific day's log + items |
| GET | `/api/me/nutrition/days?from=&to=` | the trainee's nutrition timeline (paged) |
| GET | `/api/me/nutrition/metrics?date=` | **BUILT** — the caller's own `MetricEntry` check-in entries for the date (default UTC today), `{items:[{type,value,unit,localDate,loggedAtUtc}]}` **newest first** (the client takes the first entry per type as "latest") |
| POST | `/api/me/nutrition/metrics` | **BUILT** — append `{type, value, unit?, localDate?}` to the caller's own series (`localDate` defaults to UTC today); `MetricEntry` is intentionally NOT tenant-bound (personal body data); photos/wearables still deferred |
| GET | `/api/me/nutrition/summary?from=&to=` | **NOT BUILT** — adherence/streak/macro trend (the personal analytics read model) |
| POST | `/api/me/nutrition/sync` | **NOT BUILT** — **batch** apply a queue of offline mutations (see [REMINDERS_AND_OFFLINE §3](REMINDERS_AND_OFFLINE.md)) |

## 2. Wire format (reused verbatim)

- **Enums:** camelCase strings out, case/int-tolerant in — the established contract
  ([[gymbro-api-enum-wire-format]]). `LoggedItem.Status` serializes as `"completed"`, etc. The Flutter client's
  tolerant `WireEnum.parse` and the Angular DTOs handle this with zero new machinery.
- **Errors:** branch on HTTP status; body is `{code,message}` (auth) or bare string (`ToFailureResult`). The
  `ToFailureResult` extension already maps `*.NotFound→404`, `*.Conflict→409`, `*.Forbidden→403`, else 400 — new
  error codes (`NutritionPlan.NotFound`, `DailyLog.Closed`, `LoggedItem.NotFound`) slot in for free.
- **Empty states:** `GET /api/me/nutrition/today` returns the freshly-seeded day (never 204) so the client always
  has a row to bind reminders/offline writes to; `GET …/days/{future}` returns 404 (no future days).

## 3. The dual-surface model — why nutrition is `/api/me`-first

A person eats **one** set of meals per day regardless of how many gyms they belong to. So the trainee experience
is **self-scoped and cross-gym**, and the coach experience is **tenant-scoped** — the *exact* split the session
feature already ships and documents ([USER_FLOWS.md](../USER_FLOWS.md) "Unified personal training";
[gymbroapp ARCHITECTURE §8](../../../gymbroapp/docs/ARCHITECTURE.md) "The two API surfaces").

| Surface | Header | Scope | Bypass | Used by |
|---|---|---|---|---|
| `/api/me/nutrition/*` (reads + metrics) | **no** `X-Tenant-Id` | self, cross-gym | `QueryOwnAcrossGyms` (`IgnoreQueryFilters` + `TraineeId == currentUser.UserId`, re-apply `!IsDeleted`) | today, history, day read, metrics, summary |
| `/api/nutrition/log/*` (trainee writes) | `X-Tenant-Id` **required** | one gym (active) | none — declaratively gated (`NutritionLogCreate`) | trainee item status/substitute/ad-hoc/remove |
| `/api/nutrition/*` | `X-Tenant-Id` **required** | one gym | none (tenant filter applies) | coach plan authoring, client monitoring, adherence |

- **Why reads stay self-scoped:** without it, a trainee in two gyms would either see fragmented per-gym nutrition or
  need a tenant header for their own breakfast — both wrong. The audited bypass is built for exactly this.
- **Why writes are tenant-scoped:** trainee logging mirrors workout sessions — the write happens *in the context of a
  gym* (the active `X-Tenant-Id`), so the four log-write commands are `ITenantAuthorizedRequest`
  (`NutritionLogCreate`, held by Owner AND Client), declaratively gated by `AuthorizationBehavior` +
  membership-validated `TenantResolutionMiddleware`, exactly like `StartSessionCommand`. They are **not**
  exemptions. The handlers still scope to `currentUser.UserId` (defense in depth); a foreign id resolves to 404.
- **How the reads align:** the `/api/me/nutrition/*` queries + the personal metric write stay in
  `TenantAuthorizationExemptions` as **`ImperativeGuarded`** (same classification as `GetMyWorkoutHistoryQuery`),
  each scoped strictly to `currentUser.UserId`. The `TenantScopedRequestConventionTests` keep this honest at build time.
- **Alternative considered:** make the trainee always pass their coach's tenant. Rejected — breaks multi-gym, and
  contradicts the platform's own decision for sessions.

## 4. Idempotent writes (enables offline + safe retries) — *designed, deferred to Phase 4*

> **Not built.** Everything in this section — `clientItemId`, the idempotency key, the `/sync` batch — is part
> of the deferred offline phase. Shipped writes are ordinary (non-idempotent).

Trainee log writes accept a **client-generated `clientItemId` (GUID)** and an **idempotency key**. The server
upserts: a re-sent create with a known `clientItemId` returns the existing item (200) rather than creating a
duplicate.

- **Why:** offline replay and flaky-network retries must be safe. A user who taps "ate it" in a dead zone, whose
  app retries on reconnect, must not double-log.
- **How it aligns:** extends the platform's existing idempotency posture — the outbox is at-least-once with
  "handlers must be idempotent," and `StartSessionHandler` already tolerates a duplicate-insert race via the
  unique index. We make the *write contract* idempotent the same way (unique `(DailyNutritionLogId, ClientItemId)`
  + upsert).
- **Validation unchanged:** FluentValidation runs in the pipeline as always; idempotency is about *create
  semantics*, not skipping validation.

## 5. New permissions (extends the existing enum)

Add to `Permission` (`BuildingBlocks.Shared/Authorization/Permission.cs`) — a direct parallel of the workout
family:

```
NutritionPlanCreate, NutritionPlanUpdate, NutritionPlanDelete, NutritionPlanAssign,
NutritionLogCreate, NutritionLogViewOwn, NutritionLogViewAll
```

| Permission | Owner | Client |
|---|:--:|:--:|
| NutritionPlanCreate / Update / Delete / Assign | ✓ | — |
| NutritionLogCreate (log own days) | ✓ | ✓ |
| NutritionLogViewOwn | ✓ | ✓ |
| NutritionLogViewAll (a gym's clients) | ✓ | — |

Catalog reads reuse the existing `PlanView` (any member); catalog *global* writes reuse `IPlatformAdminRequest`.
The permission **matrix is server-only** — the frontends keep no mirror to sync ([PERMISSIONS.md](../PERMISSIONS.md)).

### Enforcement (no new mechanism)

- **Static tenant permission** (plan authoring, coach reads) → `ITenantAuthorizedRequest.RequiredPermission` →
  enforced in `AuthorizationBehavior`.
- **Platform-admin** (global catalog writes) → `IPlatformAdminRequest` → `PlatformAdminBehavior`.
- **Row-level / trainee-scoped** (a coach viewing a specific client's days) → `ResourceAccessGuard.
  CanViewTraineeWorkoutLogsAsync`'s nutrition sibling (`CanViewTraineeNutritionAsync`), bounding
  `NutritionLogViewAll` to the coach's own gym — the established row-level guard pattern.
- **Self-scoped** (`/api/me/nutrition/*`) → imperative, `currentUser.UserId` only, on the exemption allowlist.

## 6. Security & data ownership

- **Tenant isolation stays defense-in-depth** — membership-validated `TenantResolutionMiddleware` +
  `AuthorizationBehavior` + handler row-level checks, all reused unchanged. Nutrition adds no raw-header reads
  (`ICurrentUser`/`ITenantContext` only).
- **Health/diet data is sensitive.** Body weight, body fat, photos, mood, digestion notes are personal health
  signals. Ownership rule: a trainee's nutrition + metrics belong to the **trainee**; a coach sees a client's data
  **only while that client is a member of the coach's gym** (the membership-validated tenant scope) and **only the
  fields the visibility mode exposes**. Leaving a gym should sever the coach's forward visibility (existing
  membership semantics already do this for sessions).
- **Photos** (meal/progress) are media → follow the [master-data MEDIA_STRATEGY](../master-data/MEDIA_STRATEGY.md):
  **never served from the app server**, object storage (R2/S3) behind a CDN, **signed URLs** for private user
  photos (stricter than public exercise media — these are private by default). `PhotoRef` stores the storage key,
  not a public URL. *(Not built — `PhotoRef`/photo capture is part of the deferred `MetricEntry` phase.)*
- **Push device tokens** (deferred phase) are credentials → store hashed/opaque, scoped per user, revocable on
  logout-all (reuse the SecurityStamp/refresh-revocation instincts).
- **Audit:** every write is `CreatedBy`/`ModifiedBy`-stamped automatically. Coach reads of client data are
  already within the audited request pipeline; if regulatory needs arise, an access-log on
  `NutritionLogViewAll` reads is a small additive hosted-service concern, not a redesign.
- **GDPR/erasure:** nutrition + metrics are covered by the existing user-delete cascade
  (`UserDeletedNotification`) — a deleted user's nutrition data soft-deletes with their account; a hard-erasure
  path would extend the existing admin-delete transaction. No new deletion philosophy.

## 7. OpenAPI / contract drift

The portal's optional `npm run check:api` regenerates a typed client from a committed `openapi.json` and the
Flutter app hand-mirrors DTOs (tolerant enums). Nutrition endpoints appear in the same generated spec; the Flutter
models follow the existing hand-written + tolerant-parse convention ([[gymbro-mobile-refresh-cookie-reality]]).
No codegen toolchain is introduced.
