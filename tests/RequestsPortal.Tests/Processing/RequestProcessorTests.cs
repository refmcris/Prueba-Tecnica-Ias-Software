using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Options;
using RequestsPortal.Core.Persistence;
using RequestsPortal.Core.Processing;
using RequestsPortal.Core.Providers;
using Xunit;

namespace RequestsPortal.Tests.Processing;

public sealed class RequestProcessorTests
{
    [Fact]
    public async Task Pending_request_completes_with_one_receipt()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();

        Assert.True(await database.ProcessNextAsync());
        var completed = await database.ReadAsync(original.Id);

        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(1, completed.AttemptCount);
        Assert.Equal(0, completed.ConfirmedFailureCount);
        Assert.NotNull(completed.ProviderReceiptId);
        Assert.Null(completed.LastError);
        Assert.Null(completed.LeaseToken);
        Assert.Null(completed.LeaseUntil);
        Assert.Equal(original.CreatedAt, completed.CreatedAt);
        Assert.Equal(1, await database.CountEffectsAsync());
        Assert.False(await database.ProcessNextAsync());
    }

    [Fact]
    public async Task Empty_database_has_no_work()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();

        Assert.False(await database.ProcessNextAsync());
        Assert.Equal(0, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Transient_failure_waits_before_retrying_and_then_completes()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var provider = database.CreateProvider(ProviderMode.TransientThenSuccess);

        Assert.True(await database.ProcessNextAsync(provider));
        var pending = await database.ReadAsync(original.Id);
        Assert.Equal(RequestStatus.Pending, pending.Status);
        Assert.Equal(1, pending.AttemptCount);
        Assert.Equal(1, pending.ConfirmedFailureCount);
        Assert.NotNull(pending.LastError);
        Assert.Equal(database.Clock.GetUtcNow().AddSeconds(1).UtcDateTime, pending.NextAttemptAt);
        Assert.False(await database.ProcessNextAsync(provider));

        database.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await database.ProcessNextAsync(provider));
        var completed = await database.ReadAsync(original.Id);
        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(2, completed.AttemptCount);
        Assert.Equal(1, completed.ConfirmedFailureCount);
        Assert.Null(completed.LastError);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Three_confirmed_failures_end_in_failed_without_an_effect()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var provider = database.CreateProvider(ProviderMode.AlwaysTransient);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.True(await database.ProcessNextAsync(provider));
            var saved = await database.ReadAsync(original.Id);
            Assert.Equal(attempt, saved.AttemptCount);
            Assert.Equal(attempt, saved.ConfirmedFailureCount);
            Assert.Equal(attempt == 3 ? RequestStatus.Failed : RequestStatus.Pending, saved.Status);
            database.Clock.Advance(TimeSpan.FromSeconds(Math.Min(attempt, 2)));
        }

        Assert.False(await database.ProcessNextAsync(provider));
        Assert.Null((await database.ReadAsync(original.Id)).ProviderReceiptId);
        Assert.Equal(0, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Unknown_outcomes_do_not_exhaust_the_confirmed_failure_budget()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var provider = new UnknownOutcomeProvider(database.CreateProvider());
        var options = new ProcessingOptions { MaxAttempts = 1 };

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.True(await database.ProcessNextAsync(provider, options));
            var pending = await database.ReadAsync(original.Id);
            Assert.Equal(RequestStatus.Pending, pending.Status);
            Assert.Equal(0, pending.ConfirmedFailureCount);
            Assert.Equal(attempt, pending.AttemptCount);
            database.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.True(await database.ProcessNextAsync(provider, options));
        var completed = await database.ReadAsync(original.Id);
        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(4, completed.AttemptCount);
        Assert.Equal(0, completed.ConfirmedFailureCount);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Lost_confirmation_is_reconciled_before_recording_a_failure()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();

        await database.ProcessNextAsync(database.CreateProvider(ProviderMode.ResponseLostAfterCommit));
        var completed = await database.ReadAsync(original.Id);

        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(1, completed.AttemptCount);
        Assert.Equal(0, completed.ConfirmedFailureCount);
        Assert.Null(completed.LastError);
        Assert.NotNull(completed.ProviderReceiptId);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Crash_after_provider_commit_recovers_the_same_receipt_after_reopening()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var provider = database.CreateProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.ProcessNextAsync(new CrashAfterCommitProvider(provider)));

        var abandoned = await database.ReadAsync(original.Id);
        var receipt = await provider.GetReceiptAsync(original.Id);
        Assert.Equal(RequestStatus.Processing, abandoned.Status);
        Assert.Null(abandoned.ProviderReceiptId);
        Assert.NotNull(receipt);
        Assert.Equal(1, await database.CountEffectsAsync());
        database.Clock.Advance(TimeSpan.FromSeconds(31));

        var reopenedRequests = new RequestsDatabase(database.DataDirectory);
        var reopenedProvider = new ProviderDatabase(database.DataDirectory);
        await reopenedRequests.InitializeAsync();
        await reopenedProvider.InitializeAsync();
        await using var context = reopenedRequests.CreateContext();
        var processor = new RequestProcessor(
            new RequestProcessingRepository(context, database.Clock),
            new SimulatedProvider(reopenedProvider, new ProviderOptions { DelaySeconds = 0 }, database.Clock),
            database.Clock,
            new ProcessingOptions(),
            NullLogger<RequestProcessor>.Instance);

        Assert.True(await processor.ProcessNextAsync());
        var completed = await database.ReadAsync(original.Id);
        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(receipt.ReceiptId, completed.ProviderReceiptId);
        Assert.Equal(2, completed.AttemptCount);
        Assert.Equal(0, completed.ConfirmedFailureCount);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Recovering_an_abandoned_claim_does_not_count_as_a_confirmed_failure()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        await using var context = database.Requests.CreateContext();
        var repository = new RequestProcessingRepository(context, database.Clock);
        Assert.NotNull(await repository.TryClaimAsync(TimeSpan.FromSeconds(30)));
        database.Clock.Advance(TimeSpan.FromSeconds(31));
        var provider = database.CreateProvider(ProviderMode.AlwaysTransient);
        var options = new ProcessingOptions { MaxAttempts = 2 };

        await database.ProcessNextAsync(provider, options);
        var pending = await database.ReadAsync(original.Id);
        Assert.Equal(RequestStatus.Pending, pending.Status);
        Assert.Equal(2, pending.AttemptCount);
        Assert.Equal(1, pending.ConfirmedFailureCount);

        database.Clock.Advance(TimeSpan.FromSeconds(1));
        await database.ProcessNextAsync(provider, options);
        var failed = await database.ReadAsync(original.Id);
        Assert.Equal(RequestStatus.Failed, failed.Status);
        Assert.Equal(3, failed.AttemptCount);
        Assert.Equal(2, failed.ConfirmedFailureCount);
        Assert.Equal(0, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Second_worker_cannot_take_a_live_reservation()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var blocking = new BlockingProvider(database.CreateProvider());
        var first = database.ProcessNextAsync(blocking);

        try
        {
            await blocking.Started.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(RequestStatus.Processing, (await database.ReadAsync(original.Id)).Status);
            Assert.False(await database.ProcessNextAsync());
        }
        finally
        {
            blocking.Release();
            await first;
        }

        Assert.Equal(RequestStatus.Completed, (await database.ReadAsync(original.Id)).Status);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Provider_timeout_reschedules_without_confirming_a_failure()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var provider = new TimeoutProvider();
        var processing = database.ProcessNextAsync(provider);
        await provider.Started.WaitAsync(TimeSpan.FromSeconds(5));

        database.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(await processing.WaitAsync(TimeSpan.FromSeconds(5)));
        var pending = await database.ReadAsync(original.Id);

        Assert.Equal(RequestStatus.Pending, pending.Status);
        Assert.Equal(1, pending.AttemptCount);
        Assert.Equal(0, pending.ConfirmedFailureCount);
        Assert.NotNull(pending.LastError);
        Assert.Null(pending.LeaseToken);
        Assert.Equal(0, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Shutdown_cancellation_leaves_a_recoverable_lease()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var provider = new TimeoutProvider();
        using var cancellation = new CancellationTokenSource();
        var processing = database.ProcessNextAsync(provider, cancellationToken: cancellation.Token);
        await provider.Started.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);
        var abandoned = await database.ReadAsync(original.Id);
        Assert.Equal(RequestStatus.Processing, abandoned.Status);
        Assert.Equal(0, abandoned.ConfirmedFailureCount);

        database.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True(await database.ProcessNextAsync());
        Assert.Equal(RequestStatus.Completed, (await database.ReadAsync(original.Id)).Status);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Processing_is_visible_during_the_configured_provider_delay()
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var processing = database.ProcessNextAsync(database.CreateProvider(delaySeconds: 3));

        Assert.False(processing.IsCompleted);
        Assert.Equal(RequestStatus.Processing, (await database.ReadAsync(original.Id)).Status);
        database.Clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(await processing.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(RequestStatus.Completed, (await database.ReadAsync(original.Id)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_provider_call_cannot_overwrite_a_newer_result(bool confirmedFailure)
    {
        await using var database = await ProcessingTestDatabase.CreateAsync();
        var original = await database.CreateRequestAsync();
        var blocked = new BlockingProvider(database.CreateProvider(), ignoreCancellation: true);
        var options = new ProcessingOptions { MaxAttempts = 1 };
        var first = database.ProcessNextAsync(blocked, options);

        try
        {
            await blocked.Started.WaitAsync(TimeSpan.FromSeconds(5));
            database.Clock.Advance(TimeSpan.FromSeconds(31));
            var mode = confirmedFailure ? ProviderMode.AlwaysTransient : ProviderMode.Success;
            Assert.True(await database.ProcessNextAsync(database.CreateProvider(mode), options));
        }
        finally
        {
            blocked.Release();
            await first;
        }

        var final = await database.ReadAsync(original.Id);
        Assert.Equal(confirmedFailure ? RequestStatus.Failed : RequestStatus.Completed, final.Status);
        Assert.Equal(2, final.AttemptCount);
        Assert.Equal(confirmedFailure ? 1 : 0, final.ConfirmedFailureCount);
        Assert.Equal(confirmedFailure ? 0 : 1, await database.CountEffectsAsync());
        await using var context = database.Provider.CreateContext();
        Assert.Equal(2, (await context.Operations.SingleAsync()).HighestAttempt);
    }
}
