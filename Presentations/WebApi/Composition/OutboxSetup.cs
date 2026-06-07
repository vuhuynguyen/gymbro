using BuildingBlocks.Infrastructure.Persistence.Outbox;

namespace WebApi.Composition;

public static class OutboxSetup
{
    /// <summary>
    /// Binds <see cref="OutboxOptions"/> from the "Outbox" config section and registers the background
    /// <see cref="OutboxProcessor"/>. The scoped <c>OutboxDispatcher</c> itself is registered in
    /// <c>AddPersistence</c> so it is available even when the hosted loop is not (e.g. tests).
    /// </summary>
    public static IServiceCollection AddOutboxProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<OutboxCleanupService>();
        return services;
    }
}
