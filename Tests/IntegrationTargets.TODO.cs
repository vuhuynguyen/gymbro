// TODO (integration tests — require Postgres/Docker, not runnable in this environment).
//
// Add a Testcontainers-backed integration suite covering the DB-dependent invariants that
// pure unit tests cannot exercise. Priority targets:
//
//   1. Tenant isolation: an Owner in tenant A cannot read/mutate tenant B's plans, assignments,
//      sessions, members or invites (EF global filters + TenantResolutionMiddleware end to end).
//   2. ListSessions scoping (S3): a Client only sees their own sessions; an Owner can see a
//      trainee's sessions only via the WorkoutLogViewAll permission (CanAccessResourceAsync).
//   3. Session lifecycle: start -> log sets -> complete/abandon, with ownership (TraineeId == caller)
//      and status-transition guards enforced; no IDOR on nested exercise/set mutations.
//   4. Cross-store delete (DB3): AdminDeleteUser removes the domain User AND the Identity AppUser
//      via the UserDeletedNotification handler (idempotent when the AppUser is already gone).
//
// Do NOT add these as plain xUnit facts — they need a real database fixture.
