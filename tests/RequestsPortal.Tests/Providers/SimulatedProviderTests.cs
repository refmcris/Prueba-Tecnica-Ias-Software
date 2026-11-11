using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Exceptions;
using RequestsPortal.Core.Options;
using RequestsPortal.Core.Persistence;
using RequestsPortal.Core.Providers;
using Xunit;

namespace RequestsPortal.Tests.Providers;

public sealed class SimulatedProviderTests
{
    [Fact]
    public async Task Repeated_operation_returns_the_same_receipt_and_one_effect()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var provider = database.CreateProvider();
        var requestId = Guid.NewGuid();

        var first = await provider.ProcessAsync(requestId);
        var repeated = await provider.ProcessAsync(requestId);
        var stored = await provider.GetReceiptAsync(requestId);

        Assert.Equal(first, repeated);
        Assert.Equal(first, stored);
        Assert.Equal(requestId, first.RequestId);
        Assert.Equal(DateTimeKind.Utc, first.CompletedAt.Kind);
        Assert.Equal(1, await database.CountEffectsAsync());
        Assert.Equal(1, (await database.ReadOperationAsync(requestId))!.InvocationCount);
    }

    [Fact]
    public async Task Different_requests_produce_different_receipts()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var provider = database.CreateProvider();

        var first = await provider.ProcessAsync(Guid.NewGuid());
        var second = await provider.ProcessAsync(Guid.NewGuid());

        Assert.NotEqual(first.ReceiptId, second.ReceiptId);
        Assert.Equal(2, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Unknown_request_has_no_receipt()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var provider = database.CreateProvider();

        Assert.Null(await provider.GetReceiptAsync(Guid.NewGuid()));
        Assert.Equal(0, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Receipt_survives_reopening_the_database_and_reinitializing_migrations()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        var first = await database.CreateProvider().ProcessAsync(requestId);

        var reopened = new ProviderDatabase(database.DataDirectory);
        await reopened.InitializeAsync();
        var provider = new SimulatedProvider(reopened, new ProviderOptions { DelaySeconds = 0 }, TimeProvider.System);
        var repeated = await provider.ProcessAsync(requestId);

        Assert.Equal(first, repeated);
        Assert.Equal(first, await provider.GetReceiptAsync(requestId));
        Assert.Equal(DateTimeKind.Utc, repeated.CompletedAt.Kind);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Transient_failure_creates_no_effect_and_can_recover_after_reopening()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var options = new ProviderOptions { Mode = ProviderMode.TransientThenSuccess, DelaySeconds = 0 };
        var provider = database.CreateProvider(options);
        var requestId = Guid.NewGuid();

        await Assert.ThrowsAsync<ProviderTransientException>(() => provider.ProcessAsync(requestId));
        Assert.Null(await provider.GetReceiptAsync(requestId));
        Assert.Equal(0, await database.CountEffectsAsync());
        Assert.Equal(1, (await database.ReadOperationAsync(requestId))!.InvocationCount);

        var restarted = database.CreateProvider(options);
        var receipt = await restarted.ProcessAsync(requestId);

        Assert.Equal(receipt, await restarted.GetReceiptAsync(requestId));
        Assert.Equal(1, await database.CountEffectsAsync());
        Assert.Equal(2, (await database.ReadOperationAsync(requestId))!.InvocationCount);
    }

    [Fact]
    public async Task Confirmed_transient_failures_never_create_an_effect()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var provider = database.CreateProvider(new ProviderOptions
        {
            Mode = ProviderMode.AlwaysTransient,
            DelaySeconds = 0
        });
        var requestId = Guid.NewGuid();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Assert.ThrowsAsync<ProviderTransientException>(() => provider.ProcessAsync(requestId));
        }

        Assert.Null(await provider.GetReceiptAsync(requestId));
        Assert.Equal(0, await database.CountEffectsAsync());
        Assert.Equal(3, (await database.ReadOperationAsync(requestId))!.InvocationCount);
    }

    [Fact]
    public async Task Lost_confirmation_keeps_the_effect_and_retries_return_its_receipt()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var provider = database.CreateProvider(new ProviderOptions
        {
            Mode = ProviderMode.ResponseLostAfterCommit,
            DelaySeconds = 0
        });
        var requestId = Guid.NewGuid();

        await Assert.ThrowsAsync<ProviderOutcomeUnknownException>(() => provider.ProcessAsync(requestId));

        var stored = await provider.GetReceiptAsync(requestId);
        Assert.NotNull(stored);
        Assert.Equal(1, await database.CountEffectsAsync());
        Assert.Equal(stored, await database.CreateProvider().ProcessAsync(requestId));
        Assert.Equal(1, (await database.ReadOperationAsync(requestId))!.InvocationCount);
    }

    [Fact]
    public async Task Existing_receipt_is_replayed_without_delay_or_a_new_failure()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        var first = await database.CreateProvider().ProcessAsync(requestId);
        var clock = new FakeTimeProvider();
        var provider = database.CreateProvider(new ProviderOptions
        {
            Mode = ProviderMode.AlwaysTransient,
            DelaySeconds = 3
        }, clock);

        var repeated = await provider.ProcessAsync(requestId).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(first, repeated);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Default_delay_waits_three_seconds_using_a_controlled_clock()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var clock = new FakeTimeProvider();
        var initialTime = clock.GetUtcNow();
        var provider = database.CreateProvider(new ProviderOptions(), clock);
        var requestId = Guid.NewGuid();
        var processing = provider.ProcessAsync(requestId);

        Assert.False(processing.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(processing.IsCompleted);
        Assert.Equal(0, await database.CountEffectsAsync());

        clock.Advance(TimeSpan.FromSeconds(1));
        var receipt = await processing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(initialTime.AddSeconds(3).UtcDateTime, receipt.CompletedAt);
        Assert.Equal(1, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Simulated_delay_does_not_block_another_database_write()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var clock = new FakeTimeProvider();
        var slow = database.CreateProvider(new ProviderOptions(), clock);
        var processing = slow.ProcessAsync(Guid.NewGuid());

        try
        {
            var other = await database.CreateProvider().ProcessAsync(Guid.NewGuid())
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(other);
            Assert.False(processing.IsCompleted);
        }
        finally
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            await processing.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(2, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task In_flight_call_reuses_a_receipt_committed_while_it_was_waiting()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var clock = new FakeTimeProvider();
        var requestId = Guid.NewGuid();
        var slow = database.CreateProvider(new ProviderOptions(), clock);
        var processing = slow.ProcessAsync(requestId);

        var first = await database.CreateProvider().ProcessAsync(requestId);
        clock.Advance(TimeSpan.FromSeconds(3));
        var repeated = await processing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(first, repeated);
        Assert.Equal(1, await database.CountEffectsAsync());
        Assert.Equal(1, (await database.ReadOperationAsync(requestId))!.InvocationCount);
    }

    [Fact]
    public async Task Cancellation_during_delay_leaves_no_operation_or_effect()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var clock = new FakeTimeProvider();
        var provider = database.CreateProvider(new ProviderOptions(), clock);
        using var cancellation = new CancellationTokenSource();
        var requestId = Guid.NewGuid();
        var processing = provider.ProcessAsync(requestId, cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Null(await database.ReadOperationAsync(requestId));
        Assert.Equal(0, await database.CountEffectsAsync());
    }

    [Fact]
    public async Task Cancellation_before_processing_leaves_no_operation()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var provider = database.CreateProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var requestId = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.ProcessAsync(requestId, cancellation.Token));

        Assert.Null(await database.ReadOperationAsync(requestId));
    }

    [Fact]
    public async Task Concurrent_provider_instances_create_exactly_one_effect()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var providers = new[] { database.CreateProvider(), database.CreateProvider() };
        var requestId = Guid.NewGuid();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = Enumerable.Range(0, 12).Select(index => Task.Run(async () =>
        {
            await start.Task;
            return await providers[index % providers.Length].ProcessAsync(requestId);
        })).ToArray();

        start.SetResult();
        var receipts = await Task.WhenAll(calls);

        Assert.Single(receipts.Distinct());
        Assert.Equal(1, await database.CountEffectsAsync());
        Assert.Equal(1, (await database.ReadOperationAsync(requestId))!.InvocationCount);
    }

    [Fact]
    public async Task Concurrent_transient_retries_preserve_one_effect_and_a_durable_attempt_count()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var options = new ProviderOptions { Mode = ProviderMode.TransientThenSuccess, DelaySeconds = 0 };
        var providers = new[] { database.CreateProvider(options), database.CreateProvider(options) };
        var requestId = Guid.NewGuid();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = Enumerable.Range(0, 12).Select(index => Task.Run(async () =>
        {
            await start.Task;

            try
            {
                return await providers[index % providers.Length].ProcessAsync(requestId);
            }
            catch (ProviderTransientException)
            {
                return null;
            }
        })).ToArray();

        start.SetResult();
        var receipts = await Task.WhenAll(calls);

        Assert.Equal(1, receipts.Count(receipt => receipt is null));
        Assert.Single(receipts.OfType<ProviderReceipt>().Distinct());
        Assert.Equal(1, await database.CountEffectsAsync());
        Assert.Equal(2, (await database.ReadOperationAsync(requestId))!.InvocationCount);
    }

    [Fact]
    public async Task Empty_request_id_is_rejected_without_writing()
    {
        await using var database = await ProviderTestDatabase.CreateAsync();
        var provider = database.CreateProvider();

        await Assert.ThrowsAsync<ArgumentException>(() => provider.ProcessAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetReceiptAsync(Guid.Empty));
        await using var context = database.Database.CreateContext();
        Assert.Equal(0, await context.Operations.CountAsync());
    }
}
