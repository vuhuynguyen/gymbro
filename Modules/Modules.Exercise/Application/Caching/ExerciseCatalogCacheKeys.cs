namespace Modules.ExerciseModule.Application.Caching;

public static class ExerciseCatalogCacheKeys
{
    public static string DetailScoped(Guid exerciseId, string scope) =>
        $"exercise:detail:{exerciseId:N}:{scope}";

    public static string SearchScoped(
        string scope,
        string? search,
        string? muscleGroup,
        string? type,
        string? movementType,
        string? difficulty,
        string? equipment,
        int page,
        int pageSize) =>
        $"exercise:search:{scope}:{search ?? ""}:{muscleGroup ?? ""}:{type ?? ""}:{movementType ?? ""}:{difficulty ?? ""}:{equipment ?? ""}:{page}:{pageSize}";
}
