using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Auth;
using MoneyTransfer.Api.Data;
using MoneyTransfer.Api.Transfers;

namespace MoneyTransfer.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/accounts", GetAccountsAsync);
        routes.MapGet("/accounts/{accountId}/transfers", GetAccountTransfersAsync);
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

    private static async Task<IResult> GetAccountTransfersAsync(string accountId,
                                                                string? limit,
                                                                string? cursor,
                                                                ClaimsPrincipal user,
                                                                TransferHistoryService historyService,
                                                                CancellationToken cancellationToken)
    {
        var (page, errors) = PageRequestValidator.Validate(limit, cursor);
        if (page is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await historyService.GetPageAsync(user.GetCustomerId(), accountId, page, cancellationToken);

        if (result is null)
        {
            return TypedResults.Problem(statusCode : StatusCodes.Status404NotFound,
                                        detail : "Account was not found.",
                                        extensions : new Dictionary<string, object?> { ["code"] = "account_not_found" });
        }

        return TypedResults.Ok(result);
    }
}

public record AccountResponse(string Id, string Currency, long Balance);
