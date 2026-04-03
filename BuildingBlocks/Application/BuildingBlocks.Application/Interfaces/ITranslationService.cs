namespace BuildingBlocks.Application.Interfaces;

public interface ITranslationService
{
    Task<string?> GetAsync(
        Guid entityId,
        string entityType,
        string key,
        string language,
        string? fallback = null);

    Task<Dictionary<string, string>> GetManyAsync(
        Guid entityId,
        string entityType,
        string language);
}