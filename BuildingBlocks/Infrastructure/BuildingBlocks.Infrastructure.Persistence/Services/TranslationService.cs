using BuildingBlocks.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Services;

public class TranslationService : ITranslationService
{
    private readonly AppDbContext _db;

    public TranslationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetAsync(
        Guid entityId,
        string entityType,
        string key,
        string language,
        string? fallback = null)
    {
        var value = await _db.Translations
            .Where(t =>
                t.EntityId == entityId &&
                t.EntityType == entityType &&
                t.Language == language &&
                t.Key == key)
            .Select(t => t.Value)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(value))
            return value;

        // fallback to default language (en)
        if (language != "en")
        {
            value = await _db.Translations
                .Where(t =>
                    t.EntityId == entityId &&
                    t.EntityType == entityType &&
                    t.Language == "en" &&
                    t.Key == key)
                .Select(t => t.Value)
                .FirstOrDefaultAsync();
        }

        return value ?? fallback;
    }

    public async Task<Dictionary<string, string>> GetManyAsync(
        Guid entityId,
        string entityType,
        string language)
    {
        var translations = await _db.Translations
            .Where(t =>
                t.EntityId == entityId &&
                t.EntityType == entityType &&
                t.Language == language)
            .ToListAsync();

        return translations.ToDictionary(x => x.Key, x => x.Value);
    }
}