using System.Net;
using Xunit;

namespace MoneyTransfer.Api.Tests;

[Collection("api")]
public class ConcurrencyTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Two_concurrent_transfers_exceeding_the_balance_cannot_make_it_negative()
    {
        var bob = Fixture.CreateClient("bob-key");

        var responses = await Task.WhenAll(PostTransferAsync(bob, "c-1", TransferBody("bob-eur", "alice-eur", 20_000)),
                                           PostTransferAsync(bob, "c-2", TransferBody("bob-eur", "alice-eur", 20_000)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.UnprocessableEntity);

        Assert.Equal(5_000, await Fixture.GetBalanceAsync("bob-eur"));
        Assert.Equal(120_000, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(1, await Fixture.CountTransfersAsync());
    }

    [Fact]
    public async Task Opposite_transfers_at_the_same_time_all_succeed_without_deadlock()
    {
        var alice = Fixture.CreateClient("alice-key");
        var bob = Fixture.CreateClient("bob-key");

        var requests = Enumerable.Range(1, 10).SelectMany(i => new[]
        {
            PostTransferAsync(alice, $"a-{i}", TransferBody("alice-eur", "bob-eur", 100)),
            PostTransferAsync(bob, $"b-{i}", TransferBody("bob-eur", "alice-eur", 100)),
        });

        var responses = await Task.WhenAll(requests);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(100_000, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(25_000, await Fixture.GetBalanceAsync("bob-eur"));
        Assert.Equal(20, await Fixture.CountTransfersAsync());
    }
}
