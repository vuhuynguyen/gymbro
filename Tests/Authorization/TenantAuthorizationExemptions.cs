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
            ["GetTenantMembersQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Handler gates on Permission.ClientView and filters the member list by the caller's role."),
            ["RemoveMemberCommand"] = new(ExemptionKind.ImperativeGuarded,
                "Handler gates on Permission.ClientRemove (Owner-only) before any lookup or mutation."),
            ["SearchExercisesQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Admin sees the global catalog; otherwise the handler gates tenant catalog access on "
                + "Permission.PlanView — two distinct rules a single static permission can't express."),
            ["GetExerciseByIdQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Same admin-vs-tenant split as SearchExercisesQuery; handler gates on Permission.PlanView."),
            ["GetPlanAssignmentByIdQuery"] = new(ExemptionKind.ImperativeGuarded,
                "Row-level ownership: handler allows admins or the assignment's own trainee "
                + "(assignment.TraineeId == currentUser.UserId), not a tenant-wide permission."),

            // --- InternalLookup: no caller-facing auth; only reached behind a guarded handler in-process ---
            ["GetWorkoutForSnapshotQuery"] = new(ExemptionKind.InternalLookup,
                "Internal: read by the session-start handler to snapshot a planned workout; never dispatched "
                + "from a controller. The caller's access is already enforced by the start command."),
            ["ResolveExerciseNamesQuery"] = new(ExemptionKind.InternalLookup,
                "Internal enrichment: maps exercise ids to display names for handlers that have already "
                + "authorized the parent read; exposes no tenant-scoped row data of its own."),
            ["ValidateExerciseIdsQuery"] = new(ExemptionKind.InternalLookup,
                "Internal validation: checks that referenced exercise ids exist; returns no row data and is "
                + "only invoked by already-authorized create/update handlers."),
        };

    public static bool IsExempt(string requestTypeName) => Entries.ContainsKey(requestTypeName);

    /// <summary>All exemptions with their documented reason and enforcement kind.</summary>
    public static IReadOnlyDictionary<string, Exemption> All => Entries;
}
