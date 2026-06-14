using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using WebApi.Persistence;

namespace Gymbro.Tests;

/// <summary>
/// The model contributors a test-built <c>AppDbContext</c> needs to materialize the full model. Delegates to the
/// composition root's <see cref="AppModelConfigurations.All"/> so tests stay in lockstep with production —
/// kernel (outbox) + every module + the cross-module FK configs.
/// </summary>
internal static class TestModelConfigurations
{
    public static IEnumerable<IModelConfiguration> All() => AppModelConfigurations.All;
}
