using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Application.Caching;
using Modules.ExerciseModule.Application.DTOs;
using Modules.ExerciseModule.Application.Mapping;
using Modules.ExerciseModule.Entities;
using BuildingBlocks.Application.Authorization;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.ExerciseModule.Application.Queries.Handlers;

public class GetExerciseByIdHandler(
    IExerciseRepository repository,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth,
    IMemoryCache cache)
    : IRequestHandler<GetExerciseByIdQuery, Result<ExerciseDetailDto>>
{
    private static readonly TimeSpan DetailCacheTtl = TimeSpan.FromMinutes(2);

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

        var cacheScope = currentUser.IsAdmin
            ? "admin"
            : tenantContext.TenantId!.Value.ToString("N");
        var cacheKey = ExerciseCatalogCacheKeys.DetailScoped(request.Id, cacheScope);
        if (cache.TryGetValue(cacheKey, out ExerciseDetailDto? cached) && cached != null)
            return Result<ExerciseDetailDto>.Success(cached);

        var exercise = await repository.Query()
            .AsNoTracking()
            .Where(x => x.Id == request.Id && !x.IsDeleted)
            .Select(ExerciseMapping.ExerciseDetailProjection)
            .FirstOrDefaultAsync(cancellationToken);

        if (exercise == null)
            return Result<ExerciseDetailDto>.Failure(Error.NotFound("Exercise not found."));

        cache.Set(cacheKey, exercise, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DetailCacheTtl
        });

        return Result<ExerciseDetailDto>.Success(exercise);
    }
}
