using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using MediatR;

namespace BuildingBlocks.Infrastructure.Persistence.Services;

public class DesignTimeDbContextServices : IDbContextServices
{
    public ICurrentUser CurrentUser { get; } = new DesignTimeCurrentUser();
    public IPublisher Publisher { get; } = new NoOpPublisher();
}