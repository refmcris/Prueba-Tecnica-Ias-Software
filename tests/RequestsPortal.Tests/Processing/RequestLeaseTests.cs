using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Persistence;
using Xunit;

namespace RequestsPortal.Tests.Processing;

public sealed class RequestLeaseTests
{
    [Fact]
    public async Task Claim_sets_an_exclusive_token_expiration_and_attempt_number()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        await using var context = database.Requests.CreateContext();
        var repository = new RequestProcessingRepository(context, database.Clock);

        var claimed = await repository.TryClaimAsync(TimeSpan.FromSeconds(30));

        Assert.NotNull(claimed);
        Assert.Equal(original.Id, claimed.Id);
        Assert.Equal(RequestStatus.Processing, claimed.Status);
        Assert.Equal(1, claimed.AttemptCount);
        Assert.Equal(0, claimed.ConfirmedFailureCount);
        Assert.False(string.IsNullOrWhiteSpace(claimed.LeaseToken));
        Assert.Equal(database.Clock.GetUtcNow().AddSeconds(30).UtcDateTime, claimed.LeaseUntil);
        Assert.Equal(DateTimeKind.Utc, claimed.LeaseUntil!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, claimed.NextAttemptAt.Kind);
        Assert.Null(await repository.TryClaimAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task Concurrent_claimants_acquire_one_reservation()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        await database.CreateRequestAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claims = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            await using var context = database.Requests.CreateContext();
            var repository = new RequestProcessingRepository(context, database.Clock);
            return await repository.TryClaimAsync(TimeSpan.FromSeconds(30));
        })).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(claims);

        Assert.Single(results, claim => claim is not null);
        await using var verification = database.Requests.CreateContext();
        Assert.Equal(1, (await verification.Requests.SingleAsync()).AttemptCount);
    }

    [Fact]
    public async Task Expired_owner_cannot_complete_or_record_a_failure_without_a_successor()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        await database.CreateRequestAsync();
        await using var context = database.Requests.CreateContext();
        var repository = new RequestProcessingRepository(context, database.Clock);
        var claimed = await repository.TryClaimAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(claimed);
        database.Clock.Advance(TimeSpan.FromSeconds(30));

        Assert.False(await repository.CompleteAsync(claimed, ReceiptFor(claimed.Id)));
        Assert.False(await repository.RecordFailureAsync(claimed, "Fallo", true, 1, TimeSpan.FromSeconds(1)));
        var saved = await database.ReadAsync(claimed.Id);
        Assert.Equal(RequestStatus.Processing, saved.Status);
        Assert.Equal(0, saved.ConfirmedFailureCount);
    }

    [Fact]
    public async Task Old_owner_cannot_modify_a_successor_or_reverse_completed()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        await database.CreateRequestAsync();
        await using var firstContext = database.Requests.CreateContext();
        await using var secondContext = database.Requests.CreateContext();
        var first = new RequestProcessingRepository(firstContext, database.Clock);
        var second = new RequestProcessingRepository(secondContext, database.Clock);
        var oldClaim = await first.TryClaimAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(oldClaim);
        database.Clock.Advance(TimeSpan.FromSeconds(31));
        var current = await second.TryClaimAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(current);
        Assert.NotEqual(oldClaim.LeaseToken, current.LeaseToken);
        Assert.Equal(2, current.AttemptCount);

        Assert.False(await first.CompleteAsync(oldClaim, ReceiptFor(oldClaim.Id)));
        Assert.False(await first.RecordFailureAsync(oldClaim, "Fallo anterior", true, 1, TimeSpan.Zero));
        Assert.True(await second.CompleteAsync(current, ReceiptFor(current.Id)));
        Assert.False(await second.RecordFailureAsync(current, "Fallo tardío", true, 1, TimeSpan.Zero));
        Assert.False(await second.CompleteAsync(current, ReceiptFor(current.Id)));
        Assert.Equal(RequestStatus.Completed, (await database.ReadAsync(current.Id)).Status);
    }

    [Theory]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Failed)]
    public async Task Terminal_requests_cannot_be_claimed(RequestStatus status)
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        await using var context = database.Requests.CreateContext();
        await context.Requests.Where(request => request.Id == original.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(request => request.Status, status));
        var repository = new RequestProcessingRepository(context, database.Clock);

        Assert.Null(await repository.TryClaimAsync(TimeSpan.FromSeconds(30)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paused_claimant_cannot_return_an_expired_or_replaced_lease(bool replaceOwner)
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var pause = new PauseAfterClaimInterceptor();
        await using var context = database.CreateInterceptedContext(pause);
        var repository = new RequestProcessingRepository(context, database.Clock);
        var first = repository.TryClaimAsync(TimeSpan.FromSeconds(30));
        string? successorToken = null;

        try
        {
            await pause.Reached.WaitAsync(TimeSpan.FromSeconds(5));
            database.Clock.Advance(TimeSpan.FromSeconds(31));

            if (replaceOwner)
            {
                await using var successorContext = database.Requests.CreateContext();
                var successor = new RequestProcessingRepository(successorContext, database.Clock);
                var claimed = await successor.TryClaimAsync(TimeSpan.FromSeconds(30));
                Assert.NotNull(claimed);
                successorToken = claimed.LeaseToken;
            }
        }
        finally
        {
            pause.Release();
        }

        Assert.Null(await first.WaitAsync(TimeSpan.FromSeconds(5)));

        if (replaceOwner)
        {
            Assert.Equal(successorToken, (await database.ReadAsync(original.Id)).LeaseToken);
        }
    }

    [Fact]
    public async Task Receipt_for_another_request_cannot_complete_the_claim()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        await using var context = database.Requests.CreateContext();
        var repository = new RequestProcessingRepository(context, database.Clock);
        var claimed = await repository.TryClaimAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(claimed);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.CompleteAsync(claimed, ReceiptFor(Guid.NewGuid())));
        Assert.Equal(RequestStatus.Processing, (await database.ReadAsync(original.Id)).Status);
    }

    [Fact]
    public async Task Legacy_processing_request_without_a_lease_can_be_recovered()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        await using var context = database.Requests.CreateContext();
        await context.Requests.Where(request => request.Id == original.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(request => request.Status, RequestStatus.Processing));

        Assert.True(await database.ProcessNextAsync());
        Assert.Equal(RequestStatus.Completed, (await database.ReadAsync(original.Id)).Status);
    }

    private static ProviderReceipt ReceiptFor(Guid id)
    {
        return new ProviderReceipt
        {
            RequestId = id,
            ReceiptId = $"receipt-{id:N}",
            CompletedAt = DateTime.UtcNow
        };
    }
}
