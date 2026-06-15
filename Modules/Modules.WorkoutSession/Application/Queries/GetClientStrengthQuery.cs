using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

/// <summary>
/// COACH, TENANT-SCOPED (own gym only): one client's top-lift e1RM trends, built from sessions in the active
/// gym ONLY. Requires <c>X-Tenant-Id</c>; gated by <c>WorkoutLogViewAll</c> + <c>ResourceAccessGuard</c> (the
/// traineeId MUST be a member of the active tenant — otherwise 403/404, NEVER a silent rescope to self).
///
/// <para>CRITICAL (FEASIBILITY R2): this is a SEPARATE handler from the trainee per-lift series — it reads
/// through <c>sessionRepository.Query()</c> with the EF tenant filter ON, so the coach sees only the client's
/// IN-GYM work and never their cross-gym history. It NEVER calls <c>QueryOwnAcrossGyms</c>.</para>
///
/// <see cref="Take"/> bounds the number of top lifts returned (most-trained first). Classified
/// ImperativeGuarded in TenantAuthorizationExemptions.
/// </summary>
public sealed record GetClientStrengthQuery(Guid TraineeId, int Take)
    : IRequest<Result<IReadOnlyList<LiftTrendDto>>>;
