using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Enums;

namespace RequestsPortal.Core.Persistence;

public sealed class RequestProcessingRepository
{
    private const int ClaimBatchSize = 10;
    private readonly RequestsDbContext context;
    private readonly TimeProvider clock;

    public RequestProcessingRepository(RequestsDbContext context, TimeProvider clock)
    {
        this.context = context;
        this.clock = clock;
    }

    public async Task<ServiceRequest?> TryClaimAsync(
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await FindAvailable(now)
            .OrderBy(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .Select(request => request.Id)
            .Take(ClaimBatchSize)
            .ToListAsync(cancellationToken);

        foreach (var id in candidates)
        {
            var token = Guid.NewGuid().ToString("N");
            var leaseUntil = now.Add(leaseDuration);
            var changed = await FindAvailable(now).Where(request => request.Id == id)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(request => request.Status, RequestStatus.Processing)
                    .SetProperty(request => request.LeaseToken, token)
                    .SetProperty(request => request.LeaseUntil, leaseUntil)
                    .SetProperty(request => request.AttemptCount, request => request.AttemptCount + 1)
                    .SetProperty(request => request.UpdatedAt, now), cancellationToken);

            if (changed != 1)
            {
                continue;
            }

            // Solo se devuelve la reserva creada por esta llamada, nunca la de otro Worker.
            var readAt = clock.GetUtcNow().UtcDateTime;
            var owned = await FindOwned(id, token, readAt).SingleOrDefaultAsync(cancellationToken);

            if (owned is not null && owned.LeaseUntil > clock.GetUtcNow().UtcDateTime)
            {
                return owned;
            }
        }

        return null;
    }

    public async Task<bool> CompleteAsync(
        ServiceRequest claimed,
        ProviderReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimed.LeaseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(receipt.ReceiptId);

        if (receipt.RequestId != claimed.Id)
        {
            throw new ArgumentException("El recibo corresponde a otra solicitud.", nameof(receipt));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await FindOwned(claimed.Id, claimed.LeaseToken, now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(request => request.Status, RequestStatus.Completed)
                .SetProperty(request => request.ProviderReceiptId, receipt.ReceiptId)
                .SetProperty(request => request.LastError, (string?)null)
                .SetProperty(request => request.LeaseToken, (string?)null)
                .SetProperty(request => request.LeaseUntil, (DateTime?)null)
                .SetProperty(request => request.UpdatedAt, now), cancellationToken);

        return changed == 1;
    }

    public async Task<bool> RecordFailureAsync(
        ServiceRequest claimed,
        string message,
        bool confirmed,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimed.LeaseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var failed = confirmed && claimed.ConfirmedFailureCount + 1 >= maxAttempts;
        var nextAttempt = now.Add(retryDelay);

        var changed = await FindOwned(claimed.Id, claimed.LeaseToken, now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(request => request.Status, failed ? RequestStatus.Failed : RequestStatus.Pending)
                .SetProperty(request => request.ConfirmedFailureCount,
                    request => request.ConfirmedFailureCount + (confirmed ? 1 : 0))
                .SetProperty(request => request.LastError, message)
                .SetProperty(request => request.NextAttemptAt, nextAttempt)
                .SetProperty(request => request.LeaseToken, (string?)null)
                .SetProperty(request => request.LeaseUntil, (DateTime?)null)
                .SetProperty(request => request.UpdatedAt, now), cancellationToken);

        return changed == 1;
    }

    private IQueryable<ServiceRequest> FindAvailable(DateTime now)
    {
        return context.Requests.AsNoTracking().Where(request =>
            (request.Status == RequestStatus.Pending && request.NextAttemptAt <= now)
            || (request.Status == RequestStatus.Processing
                && (request.LeaseUntil == null || request.LeaseUntil <= now)));
    }

    private IQueryable<ServiceRequest> FindOwned(Guid id, string token, DateTime now)
    {
        return context.Requests.AsNoTracking().Where(request =>
            request.Id == id
            && request.Status == RequestStatus.Processing
            && request.LeaseToken == token
            && request.LeaseUntil > now);
    }
}
