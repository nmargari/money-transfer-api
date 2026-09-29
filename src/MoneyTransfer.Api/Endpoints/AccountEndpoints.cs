using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Auth;
using MoneyTransfer.Api.Data;

namespace MoneyTransfer.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/accounts", GetAccountsAsync);
    }

    private static async Task<IResult> GetAccountsAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken)
    {
        var customerId = user.GetCustomerId();

        var accounts = await db.Accounts.Where(a => a.CustomerId == customerId)
                                        .OrderBy(a => a.Id)
                                        .Select(a => new AccountResponse(a.Id, a.Currency, a.Balance))
                                        .ToListAsync(cancellationToken);

        return TypedResults.Ok(new { data = accounts });
    }
}

public record AccountResponse(string Id, string Currency, long Balance);