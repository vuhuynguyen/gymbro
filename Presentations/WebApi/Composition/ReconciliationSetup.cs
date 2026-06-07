namespace WebApi.Composition;

public static class ReconciliationSetup
{
    /// <summary>
    /// Binds <see cref="ReconciliationOptions"/> from the "Reconciliation" config section and registers the
    /// background <see cref="CrossStoreReconciliationService"/>. The service short-circuits when
    /// <c>Reconciliation:Enabled</c> is false, so it is always registered but cheaply disabled via config.
    /// </summary>
    public static IServiceCollection AddCrossStoreReconciliation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ReconciliationOptions>(configuration.GetSection(ReconciliationOptions.SectionName));
        services.AddHostedService<CrossStoreReconciliationService>();
        return services;
    }
}
