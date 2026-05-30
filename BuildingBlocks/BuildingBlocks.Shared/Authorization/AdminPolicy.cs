using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;

namespace BuildingBlocks.Shared.Authorization;

public static class AdminPolicy
{
    private static readonly Error Violation =
        CommonErrors.Unauthorized("AdminOnly", "Platform admin access required.");

    public static Result Authorize(ICurrentUser user) =>
        user.IsAdmin ? Result.Success() : Result.Failure(Violation);

    /// <summary>Returns a failure result if the user is not a platform admin; otherwise null (proceed).</summary>
    public static Result<T>? Deny<T>(ICurrentUser user) =>
        user.IsAdmin ? null : Result<T>.Failure(Violation);

    public static Result? Deny(ICurrentUser user) =>
        user.IsAdmin ? null : Result.Failure(Violation);
}
