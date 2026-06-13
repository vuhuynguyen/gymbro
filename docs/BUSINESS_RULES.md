# Business Rules

The lifecycle rules that govern plans, assignments, sessions, and membership — preconditions, allowed and
forbidden actions, and the edge cases that are easy to get wrong.

**Related:** [USER_FLOWS.md](USER_FLOWS.md) · [PERMISSIONS.md](PERMISSIONS.md) · [DATABASE.md](DATABASE.md)

Handlers return `Result<T>` and **must not throw** for business failures. Entity factories throw `DomainException`
for invariant violations; `GlobalExceptionHandler` maps that to **400** with the invariant message. Internal-state
guards that should be unreachable (e.g. "TenantId is not set") throw `InvalidOperationException` → 500 on purpose —
they signal a bug, not bad input.

## Workout plan lifecycle

A "plan" is a `TemplateId` chain of versioned rows. Authoring is **draft-first**: a single mutable **draft head**
absorbs every edit, and **only `publish` advances the published version** that trainees and assignments see. Published
versions are immutable. (`IsDraft` distinguishes the two; published versions form the immutable history.)

- **Preconditions:** caller is Owner in the tenant (`PlanCreate`/`PlanUpdate`); plan name required (≤ 200).
- **Create:** `WorkoutPlan.Create` → new `Id` **and** new `TemplateId`, `Version = 1`, **`IsDraft = true`**. A new plan is an unpublished draft — it must be **published before it can be assigned**.
- **Edit structure (`PUT /{id}/structure`) / metadata (`PUT /{id}`):** never bump the published version. Both edits land on the single **draft head**: replacing an existing draft keeps its `Version` (the old draft row is dropped in the same unit of work), while the first edit after a publish **forks a new draft** at `latestPublished + 1`. `CreateDraft` deep-copies the whole tree into a fresh untracked row (so a single `AddAsync` persists it; no in-place child mutation), `IsDraft = true`. Structure edits **carry metadata too**, so a builder save applies metadata **and** structure as one draft (don't chain a separate `PUT /{id}` → `409` on the stale id).
- **Publish (`PUT /{id}/publish`):** flips the draft head to published (`IsDraft = false`), returning the published `version`. This is the **only** action that advances the version. `409` if there is no draft to publish ("nothing to publish"). Repeated edits between two publishes never inflate the version — they keep replacing the one draft.
- **Both edit PUTs return the draft head id** (`200 { id }`, not `204`). Because replacing a draft mints a new row id, the caller must re-point to the returned id before its next edit (else `409` on the stale id).
- **Edit/publish target the latest version only:** reject (`409`) when the supplied id is not the head of the template.
- **Archive (`PUT /{id}/archive` · `/unarchive`):** `IsArchived` retires a template (reversible). An archived plan is hidden from the active list, **cannot be edited / published / newly assigned / reached by `apply-latest`** (each `409`). Existing assignments keep working by design — archiving stops *new* distribution, not *in-flight* training. Revoke the assignment to stop training.
- **Delete guard:** soft-delete is blocked (`409`) while a **live `PlanAssignment` pins this plan version** — revoke the assignment first. A version with only historical sessions is still deletable (sessions carry their own snapshot).
- **Edge cases:** unique index `(TemplateId, Version)` filtered `IsDeleted=false AND IsDraft=false` — **drafts are exempt** so the draft head can be replaced at the same version number without tripping it; published versions stay unique. Soft-deleted plans hidden from non-admins; published versions are retained but there is no version-history UI.

## Assignment lifecycle

`PlanAssignment` pins one plan **version** to a trainee with a point-in-time `SnapshotJson` and client-visibility
controls.

- **Preconditions:** Owner with `PlanAssign`; `frequencyDaysPerWeek ∈ [1,7]`. Assign always pins the **latest published** version of the plan's template (never a draft head, even if the picker passes a draft id); a template with **no published version yet** is rejected (`409` "Publish the plan before assigning it").
  - **Trainee must be a tenant member** — non-member rejected (`PlanAssignment.TraineeNotMember`, 400). Any member is allowed (Owner or Client), so an Owner self-assigning their own plan is permitted.
  - **No duplicate live assignment** — a unique partial index `(TenantId, TraineeId, PlanId)` filtered `IsDeleted=false` plus a handler pre-check (`409`) prevent assigning the same published version to the same trainee twice.
- **Allowed:** create; `UpdateConfiguration` (start date, frequency, visibility flags — no snapshot); `apply-latest` (re-point to the newest **published** version; **preserves the current snapshot when the caller supplies none**); pause/resume; soft-delete.
- **Pause / resume:** `IsActive` (default `true`). A trainee may hold **multiple active assignments** and picks one at workout start (no auto-select). Pausing keeps history but hides the assignment from the trainee's start-workout picker (`GET /assignments?activeOnly=true`); the coach list still shows it with a "Paused" badge. Resuming sets it active again.
- **Version impact:** neither editing nor publishing the source plan changes existing assignments — they stay pinned until an explicit `apply-latest`. Publishing a newer version surfaces a "New vX" indicator on the assignment (and enables `apply-latest`); unpublished draft edits never do. This is the historical-integrity guarantee.
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
  - `DisableTraineeEditing` — blocks **structural** changes during a session (add ad-hoc exercise / skip / substitute / remove; `403`). It does **not** block logging, editing, or deleting the trainee's own set results — recording actual performance is always allowed.
- **`Blind`** — suppresses the planned-workout snapshot at session start (no snapshot stored, exercises not seeded).

The UI lets a trainee open `Full` and `Guided` plans; only `Blind` is locked.

**Self-train UX:** assignment is coach→client (the trainee picker offers Clients only). A coach who wants to follow
their own plan uses **"Train this myself"** on the Plans page, which self-assigns at `Full` visibility. A
self-assignment records the owner as both the plan's `CreatedBy` and the assignment's `TraineeId` (same id) — both
are correct.

## Session lifecycle

**State machine:** `InProgress → Completed` **or** `InProgress → Abandoned`. There is **no Paused state**.

- **Preconditions to start:** `WorkoutLogCreate`; **no other `InProgress` session for the user — across *all* gyms** (else 409), a person performs one workout at a time regardless of tenant; if `Source == FromAssignment`, `assignment.TraineeId == caller`. The existence check (`GetActiveForTraineeAsync`) and the backing unique index are user-scoped (see [DATABASE.md](DATABASE.md)).
- **Start:** sets `Status=InProgress`, `StartedAt=now`; captures `WorkoutNameSnapshot` + `SnapshotJson` of the planned workout **unless `VisibilityMode == Blind`**, and **seeds the planned exercises** so the trainee sees the workout immediately.
- **Durable exercise names:** every `PerformedExercise` stores `ExerciseName` captured at log time (seed, ad-hoc add, or substitute). Reads prefer the stored name, so renaming or deleting an `Exercise` later never rewrites history.
- **Allowed while InProgress only:** add/skip/substitute/remove performed exercises; log/edit/delete sets; complete; abandon.
  - **Skip** requires zero logged sets on that exercise (else 409). **Substitute** preserves provenance (`SubstitutedFromExerciseId`).
  - **Remove** (`DELETE /{id}/exercises/{exerciseId}`) deletes the performed exercise outright — the `PerformedExercise → Sets` FK cascade drops its logged sets too — unlike Skip, which keeps the row as a skipped record. Same `WorkoutLogCreate` + own-session + `InProgress` guards.
- **Complete:** only from `InProgress` (else 409); marks performed exercises completed, computes `DurationSeconds`, finalizes **`PrCount`**, sets `Completed`, raises `SessionCompletedEvent` (handler currently logs only — no analytics recompute).
- **PR count (`PrCount`, stored read-model):** number of exercises in the session whose session-best e1RM beats the trainee's prior best for that lift (across all earlier sessions, any status). Computed **once at Complete** and stored, so the session list reads it directly. `0` for in-progress/abandoned sessions. The detail view still computes per-set `IsPr`/`Prs[]` live, but its prior-best is SQL-aggregated and bounded to the session's exercises.
- **Abandon ("cancel"):** only from `InProgress`; **keeps logged sets**; raises no event.
- **Forbidden:** editing/deleting sets after a terminal state; a second active session; completing/abandoning a non-`InProgress` session.
- **Estimated 1RM (Epley):** on every set log/edit, `EstimatedOneRepMaxKg = round(weight × (1 + reps/30), 1)`, computed only for working sets with positive reps + weight (else null). Backs PRs and progression.
- **Edge cases:** `GET /api/sessions/active` → 204 when none; it is **self-scoped** (user-wide, no tenant required), so it returns the user's single resumable session in whichever gym it was started. "Pause" is a **UI-only stopwatch** — no endpoint, not persisted, lost on refresh. Ad-hoc sessions (no assignment) carry no snapshot.
- **Tracking modes (`ExerciseTrackingType`):** every `Exercise` declares how it is logged — `Strength`, `Bodyweight`,
  `Cardio`, `Timed`, `Hiit`, `Mobility`, or `Custom` (default `Strength`). The mode is the single source of truth for
  which set metrics matter; it is denormalized onto `PerformedExercise` at add/substitute time (durable history) so the
  loggers and validation never re-resolve it per set. The matrix lives once in `BuildingBlocks.Shared.Tracking`
  (`ExerciseTrackingRules`) and is mirrored by the Angular/Flutter clients.
- **Set metrics:** a `PerformedSet` carries `Reps`, `WeightKg`, `DurationSeconds`, `DistanceM`, `Calories`,
  `AvgHeartRate`, `Rounds`, `Rpe`, `RestSeconds` — all optional. **No metric is unconditionally required.** Instead,
  `LogSet` enforces a **mode-aware primary-metric rule**: Strength/Bodyweight need reps; Cardio needs duration or
  distance; Timed needs duration; Hiit needs rounds or a work duration; Mobility/Custom accept a metric-less *completed*
  set (mark-done). Field validators still range-check each metric when present (weight > 0, reps ≥ 1, HR 1–250, etc.).
  Plan prescription is mode-aware too: `TargetReps` is **not** required; cardio/HIIT plans prescribe
  `TargetDurationSeconds` / `TargetDistanceM` / `TargetRounds`. "Notes" is a field on performed-exercise and on session
  complete/abandon, not a separate feature.
- **Drop / rest-pause sets (rollup):** a `PerformedSet` may carry `ParentSetId` pointing to a *lead* set in the same
  performed exercise. The lead plus its stages are **one logical set** — every *set count* (`TotalSets`, progress,
  history/progress analytics) counts only parentless rows, while **volume and PR detection still include every stage**.
  `LogSet` validates the parent belongs to the same exercise and is itself parentless (one level — a stage can't have
  stages). A coach prescribes a drop set in the plan as a `Working` row followed by `Drop`-typed rows.
- **Supersets:** `PlanWorkoutExercise.SupersetGroupId` (and the denormalized `PerformedExercise.SupersetGroupId`,
  captured at seed/add time) group exercises that are performed as a superset. Exercises sharing a non-null id in one
  workout/session rotate (A→B→A→B); the rest timer fires only after a *round* completes. Grouping is a UI/flow concern —
  it does not change volume, PR, or set-count math.
- **Logged rest:** `PerformedSet.RestSeconds` records the **actual** rest taken before a set (auto-captured from the
  in-app rest timer, editable; drop stages carry none). It is distinct from the plan's prescribed/target rest, which
  seeds the countdown.

## Nutrition daily-log lifecycle

The dietary mirror of the session lifecycle (full detail: [nutrition/DOMAIN_MODEL.md](nutrition/DOMAIN_MODEL.md)).
A `DailyNutritionLog` is one row per **(trainee, local date)**, created lazily by snapshot-on-touch.

- **Provisioning (get-or-create, `NutritionDayProvisioner`):** existing day → else a day seeded from the active
  assignment (`Source = FromAssignment`, snapshot + planned `LoggedItem`s) → else, for **off-plan logging with no
  active assignment**, a plan-less **self-logged** day (`Source = NutritionSource.Adhoc`, no snapshot). Off-plan
  logging therefore **no longer requires an active assignment** (the earlier MVP limitation is lifted).
- **Trainee logging is tenant-scoped; the self-logged day is stamped with the active gym.** The four log-write
  commands are `ITenantAuthorizedRequest` (`NutritionLogCreate`) on `api/nutrition/log/*`, requiring `X-Tenant-Id`
  (membership-validated) — mirroring workout sessions. A self-logged day is stamped with the **active gym**
  (`ITenantContext.TenantId`); a nutrition day is unique per `(trainee, date)` globally, so its `TenantId` is the
  gym active when it was first created. `TenantId` stays non-null and the EF tenant filter keeps the day invisible
  to coaches in other gyms. A non-member of the active gym is rejected by the declarative authorization gate; no
  tenant in context → a clean validation failure, not a 500 — no row is created.
- **Reads never create.** A read (`today`/`days`) with no assignment and no existing day returns a non-persisted
  empty day; only a **write** provisions the self-logged row.
- **Close:** at local midnight (or first interaction the next day) still-`Planned` items become `Missed`,
  `AdherencePct` is finalized, and `DailyLogClosedEvent` is raised through the outbox.

## Membership lifecycle (tenant + Owner/Client)

- **Account vs membership are independent.** Registration creates an account and (via `UserRegisteredNotification`) a `User` + a personal `Tenant` + an `Owner` role. **Registration takes no invite code.**
  - **Cross-store consistency:** the Identity `AppUser` and the provisioned `User`/`Tenant`/role live in separate EF contexts but the same physical database, so both commits run inside **one shared transaction** (`ICrossStoreTransaction`). Any failure rolls both back — no orphaned `AppUser`, no token issued. Admin delete is the mirror (domain soft-delete + Identity cleanup share one transaction). This covers same-process failures; a durable safety net (`CrossStoreReconciliationService`) reports any residual drift (see [DATABASE.md](DATABASE.md)).
- **Join:** redeem an invite code → `UserTenantRole(Client)`. The role is carried by the invite, not chosen; invites are **single-use, 7-day expiry, and always grant Client** (join can never grant Owner).
- **Allowed:** any authed user may create a tenant (becomes Owner) or join via code. Owners generate/list/revoke invites (`InviteCreate`) and view all members; members may leave.
- **Forbidden / guarded:** the **last Owner cannot leave** a tenant; already-a-member join → 409; member removal is Owner-only (`ClientRemove`), an Owner cannot be removed (transfer ownership first), and self-removal is redirected to the leave endpoint.
- **Edge cases:** `RevokeInvite` reuses the `IsUsed` flag (no distinct "revoked" state); two invite mechanisms exist (targeted `InviteUser(email)` vs shareable `GenerateInvite()`).

## Exercise catalog lifecycle

- **Preconditions:** writes are **platform-admin-only** (`IPlatformAdminRequest` + `PlatformAdminBehavior` — even on the open `/api/exercises` route, which 403s non-admins). Reads need `PlanView` (any member) or admin.
- **Invariants:** name required; **≥ 1 muscle with ≥ 1 primary**, each `MuscleGroup` unique; calories/duration ≥ 0; tags deduped (case-insensitive); instructions get sequential `StepOrder`. The catalog is **global** (`TenantId` null) and pre-seeded. `TrackingType` is **optional on create/update** — when omitted it is derived from `Type`/`Equipment` (`ExerciseTrackingDefaults.Derive`: Cardio→Cardio, Mobility/Stretching→Mobility, bodyweight-equipment strength→Bodyweight, else Strength); the migration backfills existing rows the same way.
- **Edge cases:** raw `Enum.Parse` on Type/MovementType/Difficulty/Equipment → 500 on a bad string; legacy shims accept a single `MuscleGroup` string and default media type to "Image". Catalog management is platform-admin-only in the UI (`adminGuard()`) — coaches cannot manage it via the portal.

## Cross-cutting invariants

- **Soft-delete is opt-in** (`ISoftDelete`). Entities without it hard-delete: `UserTenantRole`, `Invite`, `PerformedExercise`, `PerformedSet`, `Translation`.
- **Audit:** `CreatedOnUtc`/`ModifiedOnUtc` and `CreatedBy`/`ModifiedBy` are auto-stamped from `ICurrentUser` in `AppDbContext.SaveChangesAsync`. Because plan edits are *inserts* of a new version, a `WorkoutPlan` row carries `CreatedBy` (the editor of that version) and its `ModifiedBy` stays null.
- **Domain events** are written to a transactional **outbox** in the same transaction as the change that raised them, then dispatched out-of-band by `OutboxProcessor` (at-least-once; handlers must be idempotent). The only domain event today is `SessionCompletedEvent`.
