# Business Rules

The lifecycle rules that govern plans, assignments, sessions, and membership — preconditions, allowed and
forbidden actions, and the edge cases that are easy to get wrong.

**Related:** [USER_FLOWS.md](USER_FLOWS.md) · [PERMISSIONS.md](PERMISSIONS.md) · [DATABASE.md](DATABASE.md)

Handlers return `Result<T>` and **must not throw** for business failures. Entity factories throw `DomainException`
for invariant violations; `GlobalExceptionHandler` maps that to **400** with the invariant message. Internal-state
guards that should be unreachable (e.g. "TenantId is not set") throw `InvalidOperationException` → 500 on purpose —
they signal a bug, not bad input.

## Workout plan lifecycle

A "plan" is an **immutable version chain** — rows sharing one `TemplateId`, each with an incrementing `Version`.

- **Preconditions:** caller is Owner in the tenant (`PlanCreate`/`PlanUpdate`); plan name required (≤ 200).
- **Create:** `WorkoutPlan.Create` → new `Id` **and** new `TemplateId`, `Version = 1`. A plan is assignable the moment it is created — there is **no "publish" step**.
- **Edit structure (`PUT /{id}/structure`):** never mutates in place. `CreateNewVersion` deep-copies the whole tree (workouts → exercises → prescribed sets, order preserved) into a **new row**, same `TemplateId`, `Version + 1`. Reads select the latest non-deleted version. **Carries metadata too** (name/description/duration/workouts-per-week) so a builder save applies metadata **and** structure as **one** new version — the client must **not** chain a separate `PUT /{id}` before it (the metadata PUT would fork a newer version, leaving the structure PUT targeting a now-stale id → `409`).
- **Edit metadata (`PUT /{id}`):** also creates a **new version** (name/description/duration land on `Version + 1`, same `TemplateId`). Use it for metadata-only edits; a builder save that also changes structure uses `PUT /{id}/structure` (above), not both.
- **Both edit PUTs return the new version id** (`200 { id }`, not `204`). Because each edit forks a new id, the caller must re-point to the returned id before its next edit — otherwise that next edit targets the now-stale id and is rejected (`409`, below).
- **Edit targets the latest version only:** both edit paths reject (`409`) when the supplied id is not the latest version in the template, so an edit can never silently fork off the newest one.
- **Archive (`PUT /{id}/archive` · `/unarchive`):** `IsArchived` retires a template (reversible). An archived plan is hidden from the active list, **cannot be edited / newly assigned / reached by `apply-latest`** (each `409`). Existing assignments keep working by design — archiving stops *new* distribution, not *in-flight* training. Revoke the assignment to stop training.
- **Delete guard:** soft-delete is blocked (`409`) while a **live `PlanAssignment` pins this plan version** — revoke the assignment first. A version with only historical sessions is still deletable (sessions carry their own snapshot).
- **Edge cases:** unique index `(TemplateId, Version)` filtered `IsDeleted=false` (versions reusable after soft-delete); soft-deleted plans hidden from non-admins; old versions are retained but there is no version-history UI.

## Assignment lifecycle

`PlanAssignment` pins one plan **version** to a trainee with a point-in-time `SnapshotJson` and client-visibility
controls.

- **Preconditions:** Owner with `PlanAssign`; `frequencyDaysPerWeek ∈ [1,7]`; `planVersion ≥ 1`. Assign pins `PlanVersion = plan.Version` at assign time.
  - **Trainee must be a tenant member** — non-member rejected (`PlanAssignment.TraineeNotMember`, 400). Any member is allowed (Owner or Client), so an Owner self-assigning their own plan is permitted.
  - **No duplicate live assignment** — a unique partial index `(TenantId, TraineeId, PlanId)` filtered `IsDeleted=false` plus a handler pre-check (`409`) prevent assigning the same plan to the same trainee twice.
- **Allowed:** create; `UpdateConfiguration` (start date, frequency, visibility flags — no snapshot); `apply-latest` (re-point to newest version; **preserves the current snapshot when the caller supplies none**); pause/resume; soft-delete.
- **Pause / resume:** `IsActive` (default `true`). A trainee may hold **multiple active assignments** and picks one at workout start (no auto-select). Pausing keeps history but hides the assignment from the trainee's start-workout picker (`GET /assignments?activeOnly=true`); the coach list still shows it with a "Paused" badge. Resuming sets it active again.
- **Version impact:** editing the source plan does **not** change existing assignments — they stay pinned until an explicit `apply-latest`. This is the historical-integrity guarantee.
- **Permissive by default:** all four hide/lock flags (`HideExercises`, `HideSetsReps`, `HideFutureWorkouts`, `DisableTraineeEditing`) default to **false** in EF config — a freshly assigned plan is fully visible and trainee-editable unless an Owner restricts it.

### Visibility modes

`PlanVisibilityMode { Full=1, Guided=2, Blind=3 }` — enforced **server-side, filter-on-read, viewer-aware**.
Filtering applies only to the **trainee's own view**; a coach (`PlanViewAll`) or admin always sees the full plan,
and the stored snapshot is never redacted.

- **`Full`** — the trainee sees the whole plan / snapshot.
- **`Guided`** — the trainee's plan preview and session snapshot are filtered by the hide flags (each is a *narrow* control, not a blanket lock):
  - `HideSetsReps` — hides the prescription. The plan preview shows set rows with targets stripped; the session snapshot drops prescribed sets entirely on the trainee's read, so the trainee logs their own sets, guided live. The stored snapshot stays complete for coach/admin.
  - `HideExercises` — strips exercise names/ids **in the plan preview only**. Names are shown during an active session (the trainee needs them to perform the work). This is a preview control, not an in-session lock.
  - `HideFutureWorkouts` — the plan preview shows only the trainee's current program week. A view hint; it does not restrict which workout a trainee may start (use `Blind` for strict gating).
  - `DisableTraineeEditing` — blocks **structural** changes during a session (add ad-hoc exercise / skip / substitute; `403`). It does **not** block logging, editing, or deleting the trainee's own set results — recording actual performance is always allowed.
- **`Blind`** — suppresses the planned-workout snapshot at session start (no snapshot stored, exercises not seeded).

The UI lets a trainee open `Full` and `Guided` plans; only `Blind` is locked.

**Self-train UX:** assignment is coach→client (the trainee picker offers Clients only). A coach who wants to follow
their own plan uses **"Train this myself"** on the Plans page, which self-assigns at `Full` visibility. A
self-assignment records the owner as both the plan's `CreatedBy` and the assignment's `TraineeId` (same id) — both
are correct.

## Session lifecycle

**State machine:** `InProgress → Completed` **or** `InProgress → Abandoned`. There is **no Paused state**.

- **Preconditions to start:** `WorkoutLogCreate`; **no other `InProgress` session** for the trainee (else 409); if `Source == FromAssignment`, `assignment.TraineeId == caller`.
- **Start:** sets `Status=InProgress`, `StartedAt=now`; captures `WorkoutNameSnapshot` + `SnapshotJson` of the planned workout **unless `VisibilityMode == Blind`**, and **seeds the planned exercises** so the trainee sees the workout immediately.
- **Durable exercise names:** every `PerformedExercise` stores `ExerciseName` captured at log time (seed, ad-hoc add, or substitute). Reads prefer the stored name, so renaming or deleting an `Exercise` later never rewrites history.
- **Allowed while InProgress only:** add/skip/substitute performed exercises; log/edit/delete sets; complete; abandon.
  - **Skip** requires zero logged sets on that exercise (else 409). **Substitute** preserves provenance (`SubstitutedFromExerciseId`).
- **Complete:** only from `InProgress` (else 409); marks performed exercises completed, computes `DurationSeconds`, finalizes **`PrCount`**, sets `Completed`, raises `SessionCompletedEvent` (handler currently logs only — no analytics recompute).
- **PR count (`PrCount`, stored read-model):** number of exercises in the session whose session-best e1RM beats the trainee's prior best for that lift (across all earlier sessions, any status). Computed **once at Complete** and stored, so the session list reads it directly. `0` for in-progress/abandoned sessions. The detail view still computes per-set `IsPr`/`Prs[]` live, but its prior-best is SQL-aggregated and bounded to the session's exercises.
- **Abandon ("cancel"):** only from `InProgress`; **keeps logged sets**; raises no event.
- **Forbidden:** editing/deleting sets after a terminal state; a second active session; completing/abandoning a non-`InProgress` session.
- **Estimated 1RM (Epley):** on every set log/edit, `EstimatedOneRepMaxKg = round(weight × (1 + reps/30), 1)`, computed only for working sets with positive reps + weight (else null). Backs PRs and progression.
- **Edge cases:** `GET /api/sessions/active` → 204 when none (single resumable session). "Pause" is a **UI-only stopwatch** — no endpoint, not persisted, lost on refresh. Ad-hoc sessions (no assignment) carry no snapshot.
- **Modalities:** sets support strength (reps/weight), time (`DurationSeconds`), and distance (`DistanceM`). "Notes" is a field on performed-exercise and on session complete/abandon, not a separate feature.

## Membership lifecycle (tenant + Owner/Client)

- **Account vs membership are independent.** Registration creates an account and (via `UserRegisteredNotification`) a `User` + a personal `Tenant` + an `Owner` role. **Registration takes no invite code.**
  - **Cross-store consistency:** the Identity `AppUser` and the provisioned `User`/`Tenant`/role live in separate EF contexts but the same physical database, so both commits run inside **one shared transaction** (`ICrossStoreTransaction`). Any failure rolls both back — no orphaned `AppUser`, no token issued. Admin delete is the mirror (domain soft-delete + Identity cleanup share one transaction). This covers same-process failures; a durable safety net (`CrossStoreReconciliationService`) reports any residual drift (see [DATABASE.md](DATABASE.md)).
- **Join:** redeem an invite code → `UserTenantRole(Client)`. The role is carried by the invite, not chosen; invites are **single-use, 7-day expiry, and always grant Client** (join can never grant Owner).
- **Allowed:** any authed user may create a tenant (becomes Owner) or join via code. Owners generate/list/revoke invites (`InviteCreate`) and view all members; members may leave.
- **Forbidden / guarded:** the **last Owner cannot leave** a tenant; already-a-member join → 409; member removal is Owner-only (`ClientRemove`), an Owner cannot be removed (transfer ownership first), and self-removal is redirected to the leave endpoint.
- **Edge cases:** `RevokeInvite` reuses the `IsUsed` flag (no distinct "revoked" state); two invite mechanisms exist (targeted `InviteUser(email)` vs shareable `GenerateInvite()`).

## Exercise catalog lifecycle

- **Preconditions:** writes are **platform-admin-only** (`IPlatformAdminRequest` + `PlatformAdminBehavior` — even on the open `/api/exercises` route, which 403s non-admins). Reads need `PlanView` (any member) or admin.
- **Invariants:** name required; **≥ 1 muscle with ≥ 1 primary**, each `MuscleGroup` unique; calories/duration ≥ 0; tags deduped (case-insensitive); instructions get sequential `StepOrder`. The catalog is **global** (`TenantId` null) and pre-seeded.
- **Edge cases:** raw `Enum.Parse` on Type/MovementType/Difficulty/Equipment → 500 on a bad string; legacy shims accept a single `MuscleGroup` string and default media type to "Image". Catalog management is platform-admin-only in the UI (`adminGuard()`) — coaches cannot manage it via the portal.

## Cross-cutting invariants

- **Soft-delete is opt-in** (`ISoftDelete`). Entities without it hard-delete: `UserTenantRole`, `Invite`, `PerformedExercise`, `PerformedSet`, `Translation`.
- **Audit:** `CreatedOnUtc`/`ModifiedOnUtc` and `CreatedBy`/`ModifiedBy` are auto-stamped from `ICurrentUser` in `AppDbContext.SaveChangesAsync`. Because plan edits are *inserts* of a new version, a `WorkoutPlan` row carries `CreatedBy` (the editor of that version) and its `ModifiedBy` stays null.
- **Domain events** are written to a transactional **outbox** in the same transaction as the change that raised them, then dispatched out-of-band by `OutboxProcessor` (at-least-once; handlers must be idempotent). The only domain event today is `SessionCompletedEvent`.
