namespace BuildingBlocks.Application.Caching;

public static class CachePolicies
{
    public static readonly TimeSpan SecurityStamp = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan GenerationCounter = TimeSpan.FromDays(365);
}
