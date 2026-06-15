namespace Gymbro.Tests.Authorization;

/// <summary>
/// How an exempted request is protected, given it does not implement
/// <see cref="BuildingBlocks.Application.Authorization.ITenantAuthorizedRequest"/> or
/// <see cref="BuildingBlocks.Application.Authorization.IPlatformAdminRequest"/>.
/// </summary>
internal enum ExemptionKind
{
    /// <summary>Authenticated, but no tenant-permission gate (identity / tenant lifecycle).</summary>
    AuthenticationOnly,

    /// <summary>
    /// Tenant-scoped, but the static <c>ITenantAuthorizedRequest</c> gate (single required permission)
    /// can't express the rule — the handler runs an imperative permission or row-level check itself.
    /// <see cref="TenantScopedRequestConventionTests"/> asserts each of these handlers actually
    /// references an authorization primitive, so the exemption can't silently widen access.
    /// </summary>
    ImperativeGuarded,

    /// <summary>
    /// Internal cross-module lookup with no caller-facing authorization of its own. Never dispatched
    /// directly from an HTTP request — only reached in-process from another handler that has already
    /// run its own guard. Adding one of these to a controller would need its own gate.
    /// </summary>
    InternalLookup,
}

/// <summary>The reason a request is exempt plus how access is actually enforced.</summary>
internal sealed record Exemption(ExemptionKind Kind, string Reason);

/// <summary>
/// Allowlist for tenant-related MediatR requests that intentionally do not use declarative
/// authorization (<c>ITenantAuthorizedRequest</c> / <c>IPlatformAdminRequest</c>). Every entry
/// records <b>why</b> it's exempt and <b>how</b> it's protected instead — the exemption set is a
/// manually-maintained seam, so the reason lives next to the entry and the
/// convention tests enforce it. Prefer shrinking this set as handlers migrate to declarative gating.
/// </summary>
internal static class TenantAuthorizationExemptions
{
    private static readonly IReadOnlyDictionary<string, Exemption> Entries =
        new Dictionary<string, Exemption>(StringComparer.Ordinal)
        {
            // --- AuthenticationOnly: identity & tenant lifecycle, no tenant-permission gate ---
            ["RegisterCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Anonymous sign-up; runs before the caller has any tenant membership."),
            ["LoginCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Anonymous credential exchange; issues the token tenant checks later rely on."),
            ["RequestPasswordResetCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Anonymous account-recovery entry point; no tenant context exists yet."),
            ["ResetPasswordCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Anonymous reset completion, authorized by the emailed reset token, not a tenant role."),
            ["ChangePasswordCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Acts on the caller's own identity account; not scoped to any tenant."),
            ["GetMeQuery"] = new(ExemptionKind.AuthenticationOnly,
                "Returns the caller's own profile/memberships; self-scoped by definition."),
            ["SetMyTimeZoneCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Sets the caller's own profile time-zone (currentUser.UserId); self-scoped to their identity, "
                + "not gated by any tenant role."),
            ["CreateTenantCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Creates the tenant itself, so no pre-existing tenant role could gate it."),
            ["JoinTenantCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Authorized by the invite token; the caller is not yet a member of the target tenant."),
            ["LeaveTenantCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Caller removes their own membership; self-scoped, no elevated permission required."),
            ["GetMyTenantsQuery"] = new(ExemptionKind.AuthenticationOnly,
                "Lists only the caller's own memberships; self-scoped by definition."),
            ["RefreshTokenCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Exchanges a rotating refresh-cookie secret for a new access token; authorized by the "
                + "opaque token, not a tenant role, and runs before any tenant is resolved."),
            ["LogoutCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Revokes the caller's own refresh token (identified by the cookie); no tenant context "
                + "and no elevated permission involved."),
            ["RevokeAllSessionsCommand"] = new(ExemptionKind.AuthenticationOnly,
                "Acts on the caller's own identity account (rotates their SecurityStamp); self-scoped, "
                + "not gated by any tenant role."),

            // --- ImperativeGuarded: tenant-scoped, handler runs its own permission / row-level check ---
            ["ListSessionsQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Hybrid own-vs-all read: ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync gates the "
                + "effective trainee, and the query is scoped to the caller unless they hold WorkoutLogViewAll."),
            ["GetSessionByIdQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Row-level read: ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync checks the loaded "
                + "session's TraineeId, so a caller without WorkoutLogViewAll can only read their own."),
            ["GetActiveSessionQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Self-scoped, cross-gym: returns only the caller's own active session via "
                + "GetActiveForTraineeAsync(currentUser.UserId); a per-user invariant, not a tenant permission."),
            ["GetMyWorkoutHistoryQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Unified personal history: QueryOwnAcrossGyms(currentUser.UserId) returns only the caller's "
                + "own sessions across all gyms; never accepts a client-supplied trainee id."),
            ["GetMyWorkoutSessionByIdQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Self-scoped detail: loads the session only when its TraineeId == currentUser.UserId "
                + "(via QueryOwnAcrossGyms), so another user's session id resolves to NotFound, never a leak."),
            ["GetMyPersonalRecordsQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Lifetime PRs computed from QueryOwnAcrossGyms(currentUser.UserId) only; the caller's own "
                + "estimated-1RM history across all gyms, no tenant-wide permission involved."),
            ["GetMyProgressQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Personal analytics aggregated from QueryOwnAcrossGyms(currentUser.UserId) only; the caller's "
                + "own volume/frequency across all gyms, never another trainee's data."),
            ["GetMyProgressOverviewQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Progress-home overview (adherence/consistency/strength/PR teaser) aggregated from "
                + "QueryOwnAcrossGyms(currentUser.UserId) only; the goal lookup is likewise scoped to "
                + "currentUser.UserId, so it never reads another trainee's data."),
            ["GetMyExerciseE1rmSeriesQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Per-lift e1RM drill-down computed from QueryOwnAcrossGyms(currentUser.UserId) only; an exercise "
                + "id is just a filter on the caller's own sessions, so a foreign or never-trained lift returns "
                + "an empty series, never another trainee's data."),
            ["GetMyStrengthLiftsQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Full strength-lift list gathered from QueryOwnAcrossGyms(currentUser.UserId) only; the optional "
                + "muscle filter narrows the caller's own lifts in memory, so it never reaches another trainee's "
                + "data — a per-user surface, not a tenant permission."),
            ["GetClientRosterQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Coach roster (tenant-scoped, own gym only): the handler gates on "
                + "tenantAuth.HasPermissionAsync(tenantId, WorkoutLogViewAll) and computes every per-client "
                + "signal over the TENANT-FILTERED session query (EF filter ON) — never QueryOwnAcrossGyms (R2)."),
            ["GetClientStrengthQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Coach per-client e1RM trends (tenant-scoped, own gym only): ResourceAccessGuard gates the "
                + "caller to their own gym and the handler verifies the trainee is a member of the active "
                + "tenant (404 otherwise); reads the TENANT-FILTERED session query, NEVER QueryOwnAcrossGyms (R2)."),
            ["GetClientLoadQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Coach per-client acute-vs-chronic load (tenant-scoped, own gym only): ResourceAccessGuard gates "
                + "the caller to their own gym and the handler verifies the trainee is a member of the active "
                + "tenant (404 otherwise); 7-/28-day volume is summed over the TENANT-FILTERED session query, "
                + "NEVER QueryOwnAcrossGyms (R2)."),
            ["GetTenantMembersQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Handler gates on Permission.ClientView and filters the member list by the caller's role."),
            ["RemoveMemberCommand"] = new(ExemptionKind.ImperativeGuarded,
                "Handler gates on Permission.ClientRemove (Owner-only) before any lookup or mutation."),
            ["SearchExercisesQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Admin sees the global catalog; otherwise the handler gates tenant catalog access on "
                + "Permission.PlanView — two distinct rules a single static permission can't express."),
            ["GetExerciseByIdQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Same admin-vs-tenant split as SearchExercisesQuery; handler gates on Permission.PlanView."),
            ["SearchFoodsQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Admin sees the global catalog; otherwise the handler gates tenant catalog access on "
                + "Permission.PlanView — the same admin-vs-tenant split as SearchExercisesQuery."),
            ["GetFoodByIdQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Same admin-vs-tenant split as SearchFoodsQuery; handler gates on Permission.PlanView."),
            ["GetPlanAssignmentByIdQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Row-level ownership: handler allows admins or the assignment's own trainee "
                + "(assignment.TraineeId == currentUser.UserId), not a tenant-wide permission."),

            // Self-scoped nutrition READS (api/me/nutrition/*): the caller's own daily log across all gyms,
            // scoped strictly to currentUser.UserId — a per-user surface, not a tenant permission.
            // (Trainee nutrition WRITES are now tenant-scoped ITenantAuthorizedRequest commands on
            // api/nutrition/log — declaratively gated, so they are NOT exempt here.)
            ["GetMyNutritionTodayQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Snapshot-on-touch for the caller's own day (currentUser.UserId) across all gyms; never "
                + "accepts a client-supplied trainee id."),
            ["GetMyNutritionDayQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Self-scoped day read keyed on currentUser.UserId; a foreign date/user resolves to NotFound."),
            ["GetMyNutritionHistoryQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Personal nutrition history via QueryOwnAcrossGyms(currentUser.UserId) only; no cross-user surface."),
            ["GetMyNutritionAdherenceQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Personal nutrition-plan adherence trend via QueryOwnAcrossGyms(currentUser.UserId) only; the "
                + "caller's own planned daily logs across all gyms, never another trainee's data."),
            ["LogMetricEntryCommand"] = new(ExemptionKind.ImperativeGuarded,
                "Self-scoped daily check-in append: the handler stamps owner = currentUser.UserId and never "
                + "accepts a client-supplied trainee id; the personal metric series has no tenant context."),
            ["GetMyNutritionMetricsQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Self-scoped metric read via GetOwnForDateAsync(currentUser.UserId) only; the personal "
                + "check-in series is cross-gym and exposes no other user's rows."),
            ["GetMyMetricSeriesQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Self-scoped body-metric trend via GetOwnSeriesAsync(currentUser.UserId) only; the personal "
                + "check-in series is cross-gym (MetricEntry is not ITenantEntity) and exposes no other user's rows."),

            // --- InternalLookup: no caller-facing auth; only reached behind a guarded handler in-process ---
            ["GetWorkoutForSnapshotQuery"] = new(ExemptionKind.InternalLookup,
                "Internal: read by the session-start handler to snapshot a planned workout; never dispatched "
                + "from a controller. The caller's access is already enforced by the start command."),
            ["ResolveExerciseNamesQuery"] = new(ExemptionKind.InternalLookup,
                "Internal enrichment: maps exercise ids to display names for handlers that have already "
                + "authorized the parent read; exposes no tenant-scoped row data of its own."),
            ["ResolveExerciseTrackingTypesQuery"] = new(ExemptionKind.InternalLookup,
                "Internal enrichment: maps exercise ids to tracking modes so the session module can denormalize "
                + "the mode onto a performed exercise; exposes no tenant-scoped row data of its own."),
            ["ResolveExerciseMuscleGroupsQuery"] = new(ExemptionKind.InternalLookup,
                "Internal enrichment: maps exercise ids to their primary muscle group (camelCase string) so the "
                + "session module can label Progress strength lifts; exposes no tenant-scoped row data of its own."),
            ["ValidateExerciseIdsQuery"] = new(ExemptionKind.InternalLookup,
                "Internal validation: checks that referenced exercise ids exist; returns no row data and is "
                + "only invoked by already-authorized create/update handlers."),
            ["ResolveFoodSummariesQuery"] = new(ExemptionKind.InternalLookup,
                "Internal enrichment: maps food ids to their snapshot summaries so the Nutrition module can "
                + "denormalize food data onto plan/log items; exposes no tenant-scoped row data of its own."),
            ["ValidateFoodIdsQuery"] = new(ExemptionKind.InternalLookup,
                "Internal validation: checks that referenced food ids exist; returns no row data and is only "
                + "invoked by already-authorized plan/log handlers."),
            ["ResolveTenantMemberNamesQuery"] = new(ExemptionKind.InternalLookup,
                "Internal enrichment: maps a tenant's Client members to display names for the coach roster; "
                + "reached only from GetClientRosterHandler AFTER it has gated on WorkoutLogViewAll for the "
                + "active tenant, and never dispatched from a controller."),
            ["ResolveActiveAssignmentGoalsQuery"] = new(ExemptionKind.InternalLookup,
                "Internal lookup: resolves the in-gym active-assignment weekly goal per trainee for the coach "
                + "roster, tenant-filtered (filter ON, own gym only). Reached only from GetClientRosterHandler "
                + "after its WorkoutLogViewAll gate, and never dispatched from a controller."),
        };

    public static bool IsExempt(string requestTypeName) => Entries.ContainsKey(requestTypeName);

    /// <summary>All exemptions with their documented reason and enforcement kind.</summary>
    public static IReadOnlyDictionary<string, Exemption> All => Entries;
}
