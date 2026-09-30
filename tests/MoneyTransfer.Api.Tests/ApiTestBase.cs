using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyTransfer.Api.Tests;

public abstract class ApiTestBase(ApiFixture fixture) : IAsyncLifetime
{
    protected ApiFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static object TransferBody(string source, 
                                         string destination, 
                                         long amount, 
                                         string currency = "EUR") => new { source_account_id = source, 
                                                                           destination_account_id = destination, 
                                                                           amount, 
                                                                           currency };

    protected static async Task<HttpResponseMessage> PostTransferAsync(HttpClient client, string idempotencyKey, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return await client.SendAsync(request);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();
}
