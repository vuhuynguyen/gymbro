namespace BuildingBlocks.Shared.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAdmin { get; }
}
