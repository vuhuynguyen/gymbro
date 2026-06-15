using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

/// <summary>
/// COACH, TENANT-SCOPED (own gym only): one client's acute-vs-chronic training LOAD (Phase 4). Requires
/// <c>X-Tenant-Id</c>; gated by <c>WorkoutLogViewAll</c> + <c>ResourceAccessGuard</c> (the traineeId MUST be a
/// member of the active tenant — otherwise 403/404, NEVER a silent rescope to self).
///
/// <para>CRITICAL (FEASIBILITY R2): a SEPARATE handler from the self-scoped trainee path — it reads through
/// <c>sessionRepository.Query()</c> with the EF tenant filter ON, so a client who trains across gyms is seen
/// here ONLY by their in-gym volume. It NEVER calls <c>QueryOwnAcrossGyms</c>.</para>
///
/// <para>Exposes the acute (7-day) and chronic (28-day weekly-average) volumes SEPARATELY plus a soft trend
/// band — NEVER an ACWR ratio (FEASIBILITY R10). Volume-based load only (RPE/duration too sparse). Classified
/// ImperativeGuarded in TenantAuthorizationExemptions.</para>
/// </summary>
public sealed record GetClientLoadQuery(Guid TraineeId)
    : IRequest<Result<AcuteChronicLoadDto>>;
