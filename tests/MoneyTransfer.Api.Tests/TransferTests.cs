using System.Net;
using Xunit;

namespace MoneyTransfer.Api.Tests;

[Collection("api")]
public class TransferTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Successful_transfer_moves_money_and_records_the_transfer()
    {
        var alice = Fixture.CreateClient("alice-key");

        var response = await PostTransferAsync(alice, "t-1", TransferBody("alice-eur", "bob-eur", 2_500));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transfer = await ReadJsonAsync(response);
        Assert.Equal("alice-eur", transfer.GetProperty("source_account_id").GetString());
        Assert.Equal("bob-eur", transfer.GetProperty("destination_account_id").GetString());
        Assert.Equal(2_500, transfer.GetProperty("amount").GetInt64());

        Assert.Equal(97_500, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(27_500, await Fixture.GetBalanceAsync("bob-eur"));
        Assert.Equal(1, await Fixture.CountTransfersAsync());
    }

    [Fact]
    public async Task Transfer_with_insufficient_funds_is_rejected_and_changes_nothing()
    {
        var alice = Fixture.CreateClient("alice-key");

        var response = await PostTransferAsync(alice, "t-1", TransferBody("alice-eur", "bob-eur", 100_001));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("insufficient_funds", (await ReadJsonAsync(response)).GetProperty("code").GetString());

        Assert.Equal(100_000, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(25_000, await Fixture.GetBalanceAsync("bob-eur"));
        Assert.Equal(0, await Fixture.CountTransfersAsync());
    }

    [Fact]
    public async Task Customer_cannot_send_money_from_another_customers_account()
    {
        var bob = Fixture.CreateClient("bob-key");

        var response = await PostTransferAsync(bob, "t-1", TransferBody("alice-eur", "bob-eur", 2_500));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("source_account_not_found", (await ReadJsonAsync(response)).GetProperty("code").GetString());

        Assert.Equal(100_000, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(25_000, await Fixture.GetBalanceAsync("bob-eur"));
        Assert.Equal(0, await Fixture.CountTransfersAsync());
    }

    [Fact]
    public async Task Same_request_and_idempotency_key_twice_moves_money_only_once()
    {
        var alice = Fixture.CreateClient("alice-key");
        var body = TransferBody("alice-eur", "bob-eur", 2_500);

        var first = await PostTransferAsync(alice, "t-1", body);
        var second = await PostTransferAsync(alice, "t-1", body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((await ReadJsonAsync(first)).GetProperty("id").GetInt64(),
                     (await ReadJsonAsync(second)).GetProperty("id").GetInt64());
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());

        Assert.Equal(97_500, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(1, await Fixture.CountTransfersAsync());
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_with_a_different_body_returns_conflict()
    {
        var alice = Fixture.CreateClient("alice-key");

        var first = await PostTransferAsync(alice, "t-1", TransferBody("alice-eur", "bob-eur", 2_500));
        var second = await PostTransferAsync(alice, "t-1", TransferBody("alice-eur", "bob-eur", 3_000));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("idempotency_key_reused", (await ReadJsonAsync(second)).GetProperty("code").GetString());

        Assert.Equal(97_500, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(1, await Fixture.CountTransfersAsync());
    }

    [Fact]
    public async Task Same_request_sent_twice_at_the_same_time_moves_money_only_once()
    {
        var alice = Fixture.CreateClient("alice-key");
        var body = TransferBody("alice-eur", "bob-eur", 2_500);

        var responses = await Task.WhenAll(PostTransferAsync(alice, "t-1", body),
                                           PostTransferAsync(alice, "t-1", body));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = await Task.WhenAll(responses.Select(async r => (await ReadJsonAsync(r)).GetProperty("id").GetInt64()));
        Assert.Equal(ids[0], ids[1]);
        Assert.Single(responses, r => r.Headers.Contains("Idempotent-Replayed"));

        Assert.Equal(97_500, await Fixture.GetBalanceAsync("alice-eur"));
        Assert.Equal(1, await Fixture.CountTransfersAsync());
    }
}
