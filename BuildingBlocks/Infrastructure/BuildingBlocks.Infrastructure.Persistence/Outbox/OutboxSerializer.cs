using System.Collections.Concurrent;
using System.Text.Json;
using MediatR;

namespace BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// (De)serializes outbox payloads and resolves their CLR type for re-dispatch. Type resolution is cached
/// and tolerant: it first tries the stored assembly-qualified name, then falls back to a full-name match
/// across loaded assemblies (so a stored name survives an assembly version bump).
/// </summary>
public static class OutboxSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);
    private static readonly ConcurrentDictionary<string, Type> TypeCache = new();

    public static string Serialize(object domainEvent, Type type) =>
        JsonSerializer.Serialize(domainEvent, type, Options);

    /// <summary>Resolves the stored type, deserializes the payload, and returns it as a MediatR notification.</summary>
    public static INotification Deserialize(OutboxMessage message)
    {
        var type = ResolveType(message.Type);

        var payload = JsonSerializer.Deserialize(message.Content, type, Options)
            ?? throw new InvalidOperationException(
                $"Outbox payload deserialized to null for type '{message.Type}'.");

        return payload as INotification
            ?? throw new InvalidOperationException(
                $"Outbox type '{message.Type}' is not an INotification and cannot be published.");
    }

    private static Type ResolveType(string typeName) =>
        TypeCache.GetOrAdd(typeName, static name =>
        {
            var type = Type.GetType(name);
            if (type is not null)
                return type;

            var bareName = name.Split(',')[0].Trim();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(bareName);
                if (type is not null)
                    return type;
            }

            throw new InvalidOperationException($"Could not resolve outbox message type '{name}'.");
        });
}
