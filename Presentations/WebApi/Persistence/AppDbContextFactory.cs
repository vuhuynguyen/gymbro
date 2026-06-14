using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebApi.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. Lives at the composition root because it must supply the full set of
/// model contributors (<see cref="AppModelConfigurations.All"/>) — the persistence kernel itself references no
/// module, so it cannot assemble that set.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=127.0.0.1;Port=5432;Database=GymBroDb";

        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            new DesignTimeDbContextServices(),
            AppModelConfigurations.All);
    }
}
