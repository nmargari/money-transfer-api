using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Api.Auth;
using MoneyTransfer.Api.Transfers;

namespace MoneyTransfer.Api.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/transfers", CreateTransferAsync);
    }

    private static async Task<IResult> CreateTransferAsync(CreateTransferRequest request,
                                                           [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                                                           ClaimsPrincipal user,
                                                           TransferService transferService,
                                                           HttpContext httpContext,
                                                           CancellationToken cancellationToken)
    {
        var (command, errors) = TransferRequestValidator.Validate(request, idempotencyKey);
        if(command is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await transferService.CreateAsync(user.GetCustomerId(), idempotencyKey!, command, cancellationToken);

        if(result.IsReplay)
        {
            httpContext.Response.Headers["Idempotent-Replayed"] = "true";
        }

        if(result.Error is { } error)
        {
            return TypedResults.Problem(statusCode : error.Status,
                                        detail : error.Detail,
                                        extensions : new Dictionary<string, object?> { ["code"] = error.Code });
        }

        return TypedResults.Json(result.Transfer, statusCode : StatusCodes.Status201Created);
    }
}
