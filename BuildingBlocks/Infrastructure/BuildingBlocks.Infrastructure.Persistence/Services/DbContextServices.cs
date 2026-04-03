using BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;
using BuildingBlocks.Shared.Abstractions;
using MediatR;

namespace BuildingBlocks.Infrastructure.Persistence.Services;

public class DbContextServices : IDbContextServices
{
    public ICurrentUser CurrentUser { get; }
    public IPublisher Publisher { get; }

    public DbContextServices(
        ICurrentUser currentUser,
        IPublisher publisher)
    {
        CurrentUser = currentUser;
        Publisher = publisher;
    }
}