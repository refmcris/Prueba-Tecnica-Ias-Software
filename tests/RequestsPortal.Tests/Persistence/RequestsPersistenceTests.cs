using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Enums;
using Xunit;

namespace RequestsPortal.Tests.Persistence;

public sealed class RequestsPersistenceTests
{
    [Fact]
    public async Task Request_survives_reopening_the_database_with_utc_dates()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var request = CreateRequest();

        await using (var context = database.CreateContext())
        {
            context.Requests.Add(request);
            await context.SaveChangesAsync();
        }

        await using var reopened = database.CreateContext();
        var saved = await reopened.Requests.AsNoTracking().SingleAsync();

        Assert.Equal(request.Id, saved.Id);
        Assert.Equal(request.CustomerId, saved.CustomerId);
        Assert.Equal(request.ExternalReference, saved.ExternalReference);
        Assert.Equal(request.ServiceCode, saved.ServiceCode);
        Assert.Equal(request.Description, saved.Description);
        Assert.Equal(RequestStatus.Pending, saved.Status);
        Assert.Equal(request.CreatedAtUtc, saved.CreatedAtUtc);
        Assert.Equal(request.UpdatedAtUtc, saved.UpdatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, saved.UpdatedAtUtc.Kind);
    }

    [Fact]
    public async Task Same_customer_and_reference_are_rejected_by_the_unique_index()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var context = database.CreateContext())
        {
            context.Requests.Add(CreateRequest());
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var duplicate = CreateRequest();
            duplicate.Description = "Different content with the same business key";
            context.Requests.Add(duplicate);

            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());

            var sqlite = Assert.IsType<SqliteException>(exception.InnerException);
            Assert.Equal(2067, sqlite.SqliteExtendedErrorCode);
        }

        await using var verification = database.CreateContext();
        var saved = await verification.Requests.AsNoTracking().SingleAsync();
        Assert.Equal("Original description", saved.Description);
    }

    [Fact]
    public async Task Different_customers_can_use_the_same_reference()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var first = CreateRequest();
        var second = CreateRequest();
        second.CustomerId = "CUSTOMER-2";

        context.Requests.AddRange(first, second);
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Requests.CountAsync());
    }

    [Fact]
    public async Task Business_key_comparisons_are_case_sensitive()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var first = CreateRequest();
        var second = CreateRequest();
        second.ExternalReference = "reference-1";
        var third = CreateRequest();
        third.CustomerId = "customer-1";

        context.Requests.AddRange(first, second, third);
        await context.SaveChangesAsync();

        Assert.Equal(3, await context.Requests.CountAsync());
    }

    [Fact]
    public async Task Unknown_status_is_rejected_by_the_database()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var request = CreateRequest();
        request.Status = (RequestStatus)999;
        context.Requests.Add(request);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var sqlite = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(275, sqlite.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task Applying_migrations_again_preserves_existing_requests()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var context = database.CreateContext())
        {
            context.Requests.Add(CreateRequest());
            await context.SaveChangesAsync();
        }

        await using var reopened = database.CreateContext();
        await reopened.Database.MigrateAsync();

        Assert.Equal(1, await reopened.Requests.CountAsync());
        Assert.Empty(await reopened.Database.GetPendingMigrationsAsync());
    }

    private static ServiceRequest CreateRequest()
    {
        var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

        return new ServiceRequest
        {
            Id = Guid.NewGuid(),
            ExternalReference = "REFERENCE-1",
            CustomerId = "CUSTOMER-1",
            ServiceCode = "SERVICE-1",
            Description = "Original description",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
