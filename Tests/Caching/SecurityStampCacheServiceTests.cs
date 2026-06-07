using BuildingBlocks.Application.Caching;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Caching;

public sealed class SecurityStampCacheServiceTests
{
    [Fact]
    public async Task EvictAsync_clears_shared_cache_so_rotated_stamp_rejects_old_token()
    {
        var appUserId = Guid.NewGuid().ToString();
        var user = new AppUser
        {
            Id = Guid.Parse(appUserId),
            UserName = "user@test.local",
            Email = "user@test.local"
        };

        var userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
        userManager.FindByIdAsync(appUserId).Returns(user);
        userManager.GetSecurityStampAsync(user).Returns("stamp-v1", "stamp-v2");

        var cache = new MemoryDistributedCache(
            new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));
        var keyNamespace = CacheKeyNamespace.FromEnvironment("test");

        var sut = new SecurityStampCacheService(cache, keyNamespace, userManager);

        Assert.True(await sut.IsStampValidAsync(appUserId, "stamp-v1"));
        Assert.True(await sut.IsStampValidAsync(appUserId, "stamp-v1"));

        await sut.EvictAsync(appUserId);

        Assert.False(await sut.IsStampValidAsync(appUserId, "stamp-v1"));
    }
}
