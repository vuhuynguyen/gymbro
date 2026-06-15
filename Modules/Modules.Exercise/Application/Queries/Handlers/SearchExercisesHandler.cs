using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Application.Queries;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public class SearchExercisesHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth,
    ExerciseCatalogCache catalogCache)
    : IRequestHandler<SearchExercisesQuery, Result<List<ExerciseDto>>>
{
    public async Task<Result<List<ExerciseDto>>> Handle(
        SearchExercisesQuery request,
        CancellationToken cancellationToken)
    {
        // Clamp pagination before it reaches the cache key / reader — an unbounded pageSize would both
        // blow up cache-key cardinality and force a huge materialization. (Audit finding 3.)
        request = request with
        {
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };

        if (!currentUser.IsAdmin)
        {
            if (!tenantContext.TenantId.HasValue)
            {
                return Result<List<ExerciseDto>>.Failure(
                    Validation("TenantContext.TenantId", "Tenant context is required."));
            }

            var canViewCatalog = await tenantAuth.HasPermissionAsync(
                tenantContext.TenantId.Value,
                Permission.PlanView,
                cancellationToken);

            if (!canViewCatalog)
            {
                return Result<List<ExerciseDto>>.Failure(
                    Unauthorized(
                        "Exercise.Search.Unauthorized",
                        "You do not have permission to search the exercise catalog."));
            }
        }

        var rows = await catalogCache.GetSearchPageAsync(request, cancellationToken);
        return Result<List<ExerciseDto>>.Success(rows);
    }
}
