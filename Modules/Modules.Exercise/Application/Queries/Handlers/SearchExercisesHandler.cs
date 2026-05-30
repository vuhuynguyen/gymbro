using BuildingBlocks.Shared.Abstractions;
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

public class SearchExercisesHandler(
    IExerciseRepository repository,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantAuthorizationService tenantAuth,
    IMemoryCache cache)
    : IRequestHandler<SearchExercisesQuery, Result<List<ExerciseDto>>>
{
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromSeconds(90);

    public async Task<Result<List<ExerciseDto>>> Handle(
        SearchExercisesQuery request,
        CancellationToken cancellationToken)
    {
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

        var cacheScope = currentUser.IsAdmin
            ? "admin"
            : tenantContext.TenantId!.Value.ToString("N");
        var cacheKey = ExerciseCatalogCacheKeys.SearchScoped(
            cacheScope,
            request.Search,
            request.MuscleGroup,
            request.Type,
            request.MovementType,
            request.Difficulty,
            request.Equipment,
            request.Page,
            request.PageSize);
        if (cache.TryGetValue(cacheKey, out List<ExerciseDto>? cached) && cached != null)
            return Result<List<ExerciseDto>>.Success(cached);

        var query = repository.Query().AsNoTracking();

        query = query.Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x => x.DefaultName.Contains(request.Search));
        }

        if (!string.IsNullOrWhiteSpace(request.MuscleGroup)
            && Enum.TryParse<MuscleGroup>(request.MuscleGroup, true, out var muscle))
        {
            query = query.Where(x => x.Muscles.Any(m => m.Muscle == muscle));
        }

        if (!string.IsNullOrWhiteSpace(request.Type)
            && Enum.TryParse<ExerciseType>(request.Type, true, out var type))
        {
            query = query.Where(x => x.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(request.MovementType)
            && Enum.TryParse<MovementType>(request.MovementType, true, out var movementType))
        {
            query = query.Where(x => x.MovementType == movementType);
        }

        if (!string.IsNullOrWhiteSpace(request.Difficulty)
            && Enum.TryParse<DifficultyLevel>(request.Difficulty, true, out var difficulty))
        {
            query = query.Where(x => x.Difficulty == difficulty);
        }

        if (!string.IsNullOrWhiteSpace(request.Equipment)
            && Enum.TryParse<Equipment>(request.Equipment, true, out var equipment))
        {
            query = query.Where(x => x.Equipment == equipment);
        }

        var datasource = await query
            .OrderBy(x => x.DefaultName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ExerciseMapping.ExerciseDtoProjection)
            .ToListAsync(cancellationToken);

        cache.Set(cacheKey, datasource, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SearchCacheTtl
        });

        return Result<List<ExerciseDto>>.Success(datasource);
    }
}
