using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Application.Queries;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public class GetExerciseByIdHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth,
    ExerciseCatalogCache catalogCache)
    : IRequestHandler<GetExerciseByIdQuery, Result<ExerciseDetailDto>>
{
    public async Task<Result<ExerciseDetailDto>> Handle(
        GetExerciseByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            if (!tenantContext.TenantId.HasValue)
            {
                return Result<ExerciseDetailDto>.Failure(
                    Validation("TenantContext.TenantId", "Tenant context is required."));
            }

            var canViewCatalog = await tenantAuth.HasPermissionAsync(
                tenantContext.TenantId.Value,
                Permission.PlanView,
                cancellationToken);

            if (!canViewCatalog)
            {
                return Result<ExerciseDetailDto>.Failure(
                    Unauthorized(
                        "Exercise.GetById.Unauthorized",
                        "You do not have permission to view this exercise."));
            }
        }

        var envelope = await catalogCache.GetDetailAsync(request.Id, cancellationToken);

        if (envelope is null || !envelope.Exists)
            return Result<ExerciseDetailDto>.Failure(Error.NotFound("Exercise not found."));

        return Result<ExerciseDetailDto>.Success(envelope.Value!);
    }
}
