using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.WorkoutSessionModule.Entities;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Exercises the outbox dispatcher's <c>FOR UPDATE SKIP LOCKED</c> claiming path against real Postgres
/// (the InMemory unit tests can only cover the non-relational fallback). Proves a seeded message is claimed,
/// published, and marked processed end-to-end. Skips automatically when no database is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OutboxClaimingTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task Dispatcher_claims_and_marks_a_message_processed_on_real_postgres()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        // Seed one outbox message directly and capture its id.
        var messageId = await fixture.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var message = OutboxMessage.Create(
                new SessionCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), fixture.TenantId, DateTimeOffset.UtcNow),
                DateTime.UtcNow);
            db.Set<OutboxMessage>().Add(message);
            await db.SaveChangesAsync();
            return message.Id;
        });

        // Dispatch through the relational claim path (SKIP LOCKED inside a transaction).
        var dispatched = await fixture.InScopeAsync(sp =>
            sp.GetRequiredService<OutboxDispatcher>().ProcessBatchAsync(50, 10, CancellationToken.None));

        Assert.True(dispatched >= 1);

        // Our specific message is now processed (robust to other rows the shared fixture may hold).
        await fixture.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var row = await db.Set<OutboxMessage>().AsNoTracking().FirstAsync(m => m.Id == messageId);
            Assert.NotNull(row.ProcessedOnUtc);
            Assert.Equal(1, row.Attempts);
        });
    }
}
