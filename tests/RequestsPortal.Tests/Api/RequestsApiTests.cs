using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using RequestsPortal.Api.Contracts;
using RequestsPortal.Core.Enums;
using Xunit;

namespace RequestsPortal.Tests.Api;

public sealed class RequestsApiTests
{
    [Fact]
    public async Task Create_returns_pending_resource_and_a_location_that_can_be_read()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/requests", CreateBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RequestDto>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Pending", created.Status);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Equal($"http://localhost/api/requests/{created.Id}", response.Headers.Location?.ToString());

        var fetched = await client.GetFromJsonAsync<RequestDto>(response.Headers.Location);
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Identical_duplicate_returns_the_original_without_modifying_it()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var body = CreateBody();
        var original = await CreateAsync(client, body);

        using var response = await client.PostAsJsonAsync("/api/requests", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(original, await response.Content.ReadFromJsonAsync<RequestDto>());
        Assert.Equal(1, await factory.CountRequestsAsync());
    }

    [Theory]
    [InlineData("serviceCode")]
    [InlineData("description")]
    public async Task Changed_content_returns_conflict_and_preserves_the_original(string field)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var body = CreateBody();
        var original = await CreateAsync(client, body);
        body[field] = "Different content";

        using var response = await client.PostAsJsonAsync("/api/requests", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(409, problem?.Status);
        Assert.Equal(original, await client.GetFromJsonAsync<RequestDto>($"/api/requests/{original.Id}"));
    }

    [Theory]
    [InlineData("externalReference")]
    [InlineData("customerId")]
    [InlineData("serviceCode")]
    [InlineData("description")]
    public async Task Missing_or_blank_required_fields_return_validation_errors(string field)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();

        foreach (var invalidValue in new string?[] { null, "", "   " })
        {
            var body = CreateBody();
            body[field] = invalidValue;
            using var response = await client.PostAsJsonAsync("/api/requests", body);
            await AssertValidationErrorAsync(response, field);
        }

        var missing = CreateBody();
        missing.Remove(field);
        using var missingResponse = await client.PostAsJsonAsync("/api/requests", missing);
        await AssertValidationErrorAsync(missingResponse, field);
        Assert.Equal(0, await factory.CountRequestsAsync());
    }

    [Theory]
    [InlineData("externalReference", 128)]
    [InlineData("customerId", 100)]
    [InlineData("serviceCode", 80)]
    [InlineData("description", 2000)]
    public async Task Length_limits_accept_the_boundary_and_reject_one_more_character(string field, int limit)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var body = CreateBody();
        body[field] = new string('x', limit + 1);

        using var rejected = await client.PostAsJsonAsync("/api/requests", body);
        await AssertValidationErrorAsync(rejected, field);

        body[field] = new string('x', limit);
        using var accepted = await client.PostAsJsonAsync("/api/requests", body);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(1, await factory.CountRequestsAsync());
    }

    [Fact]
    public async Task Outer_spaces_are_trimmed_before_validation_and_duplicate_comparison()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var body = CreateBody();
        body["description"] = new string('x', 2000);
        var padded = body.ToDictionary(item => item.Key, item => (string?)$"  {item.Value}  ");
        var original = await CreateAsync(client, padded);

        using var duplicate = await client.PostAsJsonAsync("/api/requests", body);

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(original, await duplicate.Content.ReadFromJsonAsync<RequestDto>());
        Assert.Equal(body["externalReference"], original.ExternalReference);
        Assert.Equal(body["customerId"], original.CustomerId);
        Assert.Equal(body["serviceCode"], original.ServiceCode);
        Assert.Equal(body["description"], original.Description);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{\"customerId\":123}")]
    public async Task Invalid_json_or_body_returns_bad_request(string json)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/requests", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await factory.CountRequestsAsync());
    }

    [Fact]
    public async Task Missing_request_returns_not_found()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }

    [Fact]
    public async Task List_returns_requests_and_an_empty_array_before_creation()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        Assert.Empty((await client.GetFromJsonAsync<RequestDto[]>("/api/requests"))!);

        var first = await CreateAsync(client);
        var body = CreateBody();
        body["externalReference"] = "REFERENCE-2";
        var second = await CreateAsync(client, body);
        var requests = await client.GetFromJsonAsync<RequestDto[]>("/api/requests");

        Assert.NotNull(requests);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), requests.Select(request => request.Id).Order());
    }

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Processing)]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Failed)]
    public async Task Filter_returns_only_the_requested_status(RequestStatus status)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var original = await CreateAsync(client);
        await factory.SetStatusAsync(original.Id, status);

        var matching = await client.GetFromJsonAsync<RequestDto[]>($"/api/requests?status={status}");
        var otherStatus = status == RequestStatus.Failed ? RequestStatus.Completed : RequestStatus.Failed;
        var other = await client.GetFromJsonAsync<RequestDto[]>($"/api/requests?status={otherStatus}");

        Assert.Equal(original.Id, Assert.Single(matching!).Id);
        Assert.Empty(other!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("pending")]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("%20Pending%20")]
    [InlineData("Pending,Completed")]
    [InlineData("Pending&status=Completed")]
    [InlineData("Pending&status=Pending")]
    public async Task Invalid_or_repeated_status_filter_returns_bad_request(string status)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/requests?status={status}");

        await AssertValidationErrorAsync(response, "status");
    }

    [Fact]
    public async Task Concurrent_identical_posts_create_exactly_one_request()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => client.PostAsJsonAsync("/api/requests", CreateBody())));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(11, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            var resources = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<RequestDto>()));
            Assert.Single(resources.Select(resource => resource!.Id).Distinct());
            Assert.Equal(1, await factory.CountRequestsAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Concurrent_posts_with_different_content_preserve_one_winner()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
        {
            var body = CreateBody();
            body["description"] = $"Content {index}";
            return client.PostAsJsonAsync("/api/requests", body);
        }));

        try
        {
            var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Equal(11, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            var original = await winner.Content.ReadFromJsonAsync<RequestDto>();
            Assert.Equal(original, await client.GetFromJsonAsync<RequestDto>($"/api/requests/{original!.Id}"));
            Assert.Equal(1, await factory.CountRequestsAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Failed)]
    public async Task Duplicate_does_not_restart_a_terminal_request(RequestStatus status)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var original = await CreateAsync(client);
        await factory.SetStatusAsync(original.Id, status);
        var before = await client.GetFromJsonAsync<RequestDto>($"/api/requests/{original.Id}");

        using var response = await client.PostAsJsonAsync("/api/requests", CreateBody());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await response.Content.ReadFromJsonAsync<RequestDto>());
    }

    [Fact]
    public async Task Client_cannot_set_id_dates_or_status_in_the_create_body()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var body = CreateBody();
        var suppliedId = Guid.NewGuid();
        body["id"] = suppliedId.ToString();
        body["status"] = "Completed";
        body["createdAt"] = "2000-01-01T00:00:00Z";
        body["updatedAt"] = "2000-01-01T00:00:00Z";
        body["attemptCount"] = "999";
        body["providerReceiptId"] = "client-supplied";
        body["leaseToken"] = "client-supplied";

        var created = await CreateAsync(client, body);

        Assert.NotEqual(suppliedId, created.Id);
        Assert.Equal("Pending", created.Status);
        Assert.True(created.CreatedAt.Year > 2000);
        Assert.Equal(0, created.AttemptCount);
        Assert.Null(created.ProviderReceiptId);
        using var json = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync($"/api/requests/{created.Id}"));
        Assert.False(json.RootElement.TryGetProperty("leaseToken", out _));
        Assert.False(json.RootElement.TryGetProperty("leaseUntil", out _));
        Assert.False(json.RootElement.TryGetProperty("confirmedFailureCount", out _));
    }

    [Fact]
    public async Task Public_routes_do_not_allow_direct_status_updates()
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        var original = await CreateAsync(client);
        using var response = await client.PutAsJsonAsync($"/api/requests/{original.Id}", new { status = "Completed" });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(original, await client.GetFromJsonAsync<RequestDto>($"/api/requests/{original.Id}"));
    }

    [Fact]
    public async Task Unexpected_errors_return_problem_details_without_internal_exception_details()
    {
        await using var factory = new RequestApiFactory(new ThrowingTimeProvider());
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/requests", CreateBody());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("detalle-interno-de-prueba", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await factory.CountRequestsAsync());
    }

    [Theory]
    [InlineData("/api/requests/not-a-guid")]
    [InlineData("/api/unknown")]
    public async Task Unmatched_routes_return_not_found_as_problem_details(string path)
    {
        await using var factory = new RequestApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }

    private static Dictionary<string, string?> CreateBody()
    {
        return new Dictionary<string, string?>
        {
            ["externalReference"] = "REFERENCE-1",
            ["customerId"] = "CUSTOMER-1",
            ["serviceCode"] = "SERVICE-1",
            ["description"] = "Original description"
        };
    }

    private static async Task<RequestDto> CreateAsync(
        HttpClient client,
        Dictionary<string, string?>? body = null)
    {
        using var response = await client.PostAsJsonAsync("/api/requests", body ?? CreateBody());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var resource = await response.Content.ReadFromJsonAsync<RequestDto>();
        Assert.NotNull(resource);
        return resource;
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.True(problem.Errors.ContainsKey(field), $"Falta el error de validación para {field}.");
    }

    private sealed class ThrowingTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            throw new InvalidOperationException("detalle-interno-de-prueba");
        }
    }
}
