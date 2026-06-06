// TODO (integration tests — require Postgres/Docker, not runnable in this environment).
//
// Add a Testcontainers-backed integration suite covering the DB-dependent invariants that
// pure unit tests cannot exercise. Priority targets:
//
//   1. Tenant isolation: an Owner in tenant A cannot read/mutate tenant B's plans, assignments,
//      sessions, members or invites (EF global filters + TenantResolutionMiddleware end to end).
//      PARTIAL — reads: the session-read slice is covered (a cross-tenant GetSessionById 404s via the
//      global filter, see Integration/CrossTraineeAccessTests.cs). Writes: plan update/delete (404 via
//      the tenant filter) and member removal (403 via the permission check) are covered, see
//      Integration/CrossTenantWriteIsolationTests.cs. Still pending: assignment and invite write paths.
//   2. ListSessions scoping (S3): a Client only sees their own sessions; an Owner can see a
//      trainee's sessions only via the WorkoutLogViewAll permission (CanAccessResourceAsync).
//      DONE — see Integration/CrossTraineeAccessTests.cs (cross-trainee reads through the
//      ResourceAccessGuard exemptions from Finding 8, against a real Postgres via Testcontainers).
//   3. Session lifecycle: start -> log sets -> complete/abandon, with ownership (TraineeId == caller)
//      and status-transition guards enforced; no IDOR on nested exercise/set mutations.
//   4. Cross-store delete (DB3): AdminDeleteUser removes the domain User AND the Identity AppUser
//      via the UserDeletedNotification handler (idempotent when the AppUser is already gone).
//      DONE — see Integration/CrossStoreTransactionTests.cs (covers register + delete atomicity and
//      rollback on a forced second-store failure, against a real Postgres via Testcontainers).
//   5. Refresh-token rotation + reuse detection: a rotated ("spent") token replayed must fail AND
//      revoke the whole family (logging out a thief and the victim together).
//      DONE — see Integration/RefreshTokenReuseTests.cs (issue -> validate -> rotate -> reuse -> family
//      burn, against a real Postgres via Testcontainers).
//
// Do NOT add these as plain xUnit facts — they need a real database fixture (see Integration/PostgresFixture.cs).
