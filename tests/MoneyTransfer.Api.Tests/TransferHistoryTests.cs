using System.Net;
using System.Text.Json;
using Xunit;

namespace MoneyTransfer.Api.Tests;

[Collection("api")]
public class TransferHistoryTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Pagination_returns_every_transfer_exactly_once_even_when_new_ones_arrive()
    {
        var alice = Fixture.CreateClient("alice-key");
        var bob = Fixture.CreateClient("bob-key");

        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.Created,
                         (await PostTransferAsync(alice, $"h-{i}", TransferBody("alice-eur", "bob-eur", i * 100))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Created,
                     (await PostTransferAsync(bob, "h-back", TransferBody("bob-eur", "alice-eur", 50))).StatusCode);

        var seen = new List<long>();
        var (firstPage, cursor) = await GetPageAsync(alice, "/accounts/alice-eur/transfers?limit=2");
        seen.AddRange(firstPage);

        Assert.Equal(HttpStatusCode.Created,
                     (await PostTransferAsync(alice, "h-new", TransferBody("alice-eur", "bob-eur", 700))).StatusCode);

        var pages = 1;
        while (cursor is not null)
        {
            var (ids, next) = await GetPageAsync(alice,
                                                 $"/accounts/alice-eur/transfers?limit=2&cursor={Uri.EscapeDataString(cursor)}");
            seen.AddRange(ids);
            cursor = next;
            pages++;
        }

        Assert.Equal(new long[] { 6, 5, 4, 3, 2, 1 }, seen);
        Assert.Equal(3, pages);

        var (refreshed, _) = await GetPageAsync(alice, "/accounts/alice-eur/transfers?limit=2");
        Assert.Equal(7, refreshed[0]);
    }

    [Fact]
    public async Task Customer_cannot_see_the_history_of_an_account_they_do_not_own()
    {
        var bob = Fixture.CreateClient("bob-key");

        var response = await bob.GetAsync("/accounts/alice-eur/transfers");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(List<long> Ids, string? NextCursor)> GetPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadJsonAsync(response);
        var ids = page.GetProperty("data").EnumerateArray()
                                          .Select(t => t.GetProperty("id").GetInt64())
                                          .ToList();
        var next = page.GetProperty("next_cursor");

        return (ids, next.ValueKind == JsonValueKind.Null ? null : next.GetString());
    }
}
