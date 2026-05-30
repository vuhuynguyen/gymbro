using BuildingBlocks.Application.Authorization;
using Xunit;

namespace Gymbro.Tests.Authorization;

public sealed class TenantScopedRequestConventionTests
{
    [Fact]
    public void Discovered_authorized_controller_requests_are_classified()
    {
        var discovered = AuthorizedControllerRequestDiscovery.DiscoverRequestTypes();
        Assert.NotEmpty(discovered);

        var unclassified = discovered
            .Where(t => !typeof(ITenantAuthorizedRequest).IsAssignableFrom(t))
            .Where(t => !TenantAuthorizationExemptions.IsExempt(t.Name))
            .Select(t => t.Name)
            .ToList();

        Assert.True(
            unclassified.Count == 0,
            "Each authorized-controller MediatR request must implement ITenantAuthorizedRequest "
            + "or be listed in TenantAuthorizationExemptions: "
            + string.Join(", ", unclassified));
    }

    [Fact]
    public void Declarative_requests_do_not_appear_on_imperative_exemption_list()
    {
        var declarative = AuthorizedControllerRequestDiscovery.DiscoverRequestTypes()
            .Where(t => typeof(ITenantAuthorizedRequest).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        var onExemption = declarative
            .Where(TenantAuthorizationExemptions.IsExempt)
            .ToList();

        Assert.True(
            onExemption.Count == 0,
            "Types implementing ITenantAuthorizedRequest must be removed from TenantAuthorizationExemptions: "
            + string.Join(", ", onExemption));
    }
}
