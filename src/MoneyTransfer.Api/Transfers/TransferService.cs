using System.Diagnostics.Tracing;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Data;

namespace MoneyTransfer.Api.Transfers;

public record TransferError(int Status, string Code, string Detail);

public record TransferResult(TransferResponse? Transfer, TransferError? Error)
{
    public static TransferResult Success(TransferResponse transfer) => new(transfer, null);

    public static TransferResult Failure(int status, string code, string detail) => new(null, new TransferError(status, code, detail));

}

public class TransferService(AppDbContext db)
{
    public async Task<TransferResult> CreateAsync(string customerId, TransferCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var accounts = await db.Accounts.FromSql($"""
                                                 SELECT * FROM accounts
                                                 WHERE id IN({command.SourceAccountId}, {command.DestinationAccountId})
                                                 ORDER BY id
                                                 FOR UPDATE
                                                 """)
                                        .ToListAsync(cancellationToken);

        var source = accounts.SingleOrDefault(a => a.Id == command.SourceAccountId);
        var destination = accounts.SingleOrDefault(a => a.Id == command.DestinationAccountId);

        if(source is null || source.CustomerId != customerId)
        {
            return TransferResult.Failure(StatusCodes.Status404NotFound, "source_account_not_found", "Source account was not found.");
        }

        if(destination is null)
        {
            return TransferResult.Failure(StatusCodes.Status404NotFound, "destination_account_not_found", "Destination account was not found.");

        }

        if(source.Currency != command.Currency || destination.Currency != command.Currency)
        {
            return TransferResult.Failure(StatusCodes.Status422UnprocessableEntity, "currency_mismatch", "The transfer currency must match the currency of both accounts.");
        }

        if(source.Balance < command.Amount)
        {
            return TransferResult.Failure(StatusCodes.Status422UnprocessableEntity, "insufficient_funds", "The source account does not have enough funds.");
        }

        source.Balance -= command.Amount;
        destination.Balance += command.Amount;

        var transfer = new Transfer
        {
            SourceAccountId = source.Id,
            DestinationAccountId = destination.Id,
            Amount = command.Amount,
            Currency = command.Currency
        };
        db.Transfers.Add(transfer);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TransferResult.Success(new TransferResponse(transfer.Id, 
                                                           transfer.SourceAccountId,
                                                           transfer.DestinationAccountId,
                                                           transfer.Amount,
                                                           transfer.Currency,
                                                           transfer.CreatedAt));
    }
}
