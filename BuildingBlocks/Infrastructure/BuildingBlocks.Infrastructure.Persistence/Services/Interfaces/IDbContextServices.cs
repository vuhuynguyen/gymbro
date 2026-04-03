using BuildingBlocks.Shared.Abstractions;
using MediatR;

namespace BuildingBlocks.Infrastructure.Persistence.Services.Interfaces;

public interface IDbContextServices
{
    ICurrentUser CurrentUser { get; }
    IPublisher Publisher { get; }
}