using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Contracts;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Exceptions;
using RequestsPortal.Core.Validation;

namespace RequestsPortal.Core.Persistence;

public sealed class RequestRepository
{
    private const int UniqueConstraintErrorCode = 2067;

    private readonly RequestsDbContext context;
    private readonly TimeProvider clock;

    public RequestRepository(RequestsDbContext context, TimeProvider clock)
    {
        this.context = context;
        this.clock = clock;
    }

    public async Task<CreateRequestResult> CreateAsync(
        CreateRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalized = CreateRequestValidator.NormalizeAndValidate(command);
        var now = clock.GetUtcNow().UtcDateTime;

        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            ExternalReference = normalized.ExternalReference,
            CustomerId = normalized.CustomerId,
            ServiceCode = normalized.ServiceCode,
            Description = normalized.Description,
            Status = RequestStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Requests.Add(request);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return new CreateRequestResult
            {
                Request = request,
                Created = true
            };
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: UniqueConstraintErrorCode })
        {
            // El índice único resuelve también los envíos simultáneos.
            context.ChangeTracker.Clear();

            var existing = await context.Requests.AsNoTracking().SingleOrDefaultAsync(
                item => item.CustomerId == normalized.CustomerId
                    && item.ExternalReference == normalized.ExternalReference,
                cancellationToken);

            if (existing is null)
            {
                throw;
            }

            if (existing.ServiceCode != normalized.ServiceCode
                || existing.Description != normalized.Description)
            {
                throw new RequestConflictException();
            }

            return new CreateRequestResult
            {
                Request = existing,
                Created = false
            };
        }
    }

    public Task<ServiceRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return context.Requests.AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceRequest>> ListAsync(
        RequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Requests.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        return await query.OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);
    }
}
