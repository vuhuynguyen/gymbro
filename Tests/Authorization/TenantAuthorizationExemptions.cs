namespace Gymbro.Tests.Authorization;

/// <summary>
/// Minimal allowlist for tenant-related MediatR requests that do not use
/// <see cref="BuildingBlocks.Application.Authorization.ITenantAuthorizedRequest"/>.
/// Prefer shrinking this set as handlers migrate to declarative authorization.
/// </summary>
internal static class TenantAuthorizationExemptions
{
    /// <summary>Authenticated endpoints with no tenant permission gate (identity, tenant lifecycle).</summary>
    private static readonly HashSet<string> AuthenticationOnly = new(StringComparer.Ordinal)
    {
        "RegisterCommand",
        "LoginCommand",
        "RequestPasswordResetCommand",
        "ResetPasswordCommand",
        "ChangePasswordCommand",
        "GetMeQuery",
        "CreateTenantCommand",
        "JoinTenantCommand",
        "LeaveTenantCommand",
        "GetMyTenantsQuery",
        "PromoteUserToAdminCommand",
    };

    /// <summary>Platform admin handlers (AdminPolicy in handler + controller policy defense-in-depth).</summary>
    private static readonly HashSet<string> PlatformAdmin = new(StringComparer.Ordinal)
    {
        "CreateExerciseCommand",
        "UpdateExerciseCommand",
        "DeleteExerciseCommand",
        "AdminGetTenantsQuery",
        "AdminDeleteTenantCommand",
        "AdminGetTenantMembersQuery",
        "AdminRemoveMemberCommand",
        "AdminGetUsersQuery",
        "AdminDeleteUserCommand",
    };

    /// <summary>
    /// Tenant-scoped requests with imperative authorization (resource access, hybrid permissions, admin bypass).
    /// Remove entries when the request implements <c>ITenantAuthorizedRequest</c>.
    /// </summary>
    private static readonly HashSet<string> ImperativeTenantScoped = new(StringComparer.Ordinal)
    {
        "ListSessionsQuery",
        "GetSessionByIdQuery",
        "GetTenantMembersQuery",
        "RemoveMemberCommand",
        "SearchExercisesQuery",
        "GetExerciseByIdQuery",
        "GetPlanAssignmentByIdQuery",
        "GetWorkoutForSnapshotQuery",
        "ResolveExerciseNamesQuery",
        "ValidateExerciseIdsQuery",
    };

    public static bool IsExempt(string requestTypeName) =>
        AuthenticationOnly.Contains(requestTypeName)
        || PlatformAdmin.Contains(requestTypeName)
        || ImperativeTenantScoped.Contains(requestTypeName);
}
