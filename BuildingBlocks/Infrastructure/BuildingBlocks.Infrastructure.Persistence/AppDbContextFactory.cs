using BuildingBlocks.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BuildingBlocks.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=127.0.0.1;Port=5432;Database=GymBroDb";

        optionsBuilder.UseNpgsql(connectionString);

        var dbContextServices = new DesignTimeDbContextServices();

        return new AppDbContext(optionsBuilder.Options, dbContextServices);
    }
}
