using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RequestsPortal.Tests.Processing;

internal sealed class PauseAfterClaimInterceptor : DbCommandInterceptor
{
    private int paused;
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Reached => reached.Task;

    public void Release()
    {
        released.TrySetResult();
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result == 1
            && command.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("LeaseToken", StringComparison.Ordinal)
            && command.CommandText.Contains("AttemptCount", StringComparison.Ordinal)
            && Interlocked.Exchange(ref paused, 1) == 0)
        {
            reached.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}
