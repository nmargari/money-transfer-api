using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Data;

namespace MoneyTransfer.Api.Transfers;

public record TransferError(int Status, string Code, string Detail);

public record TransferResult(TransferResponse? Transfer, TransferError? Error, bool IsReplay = false)
{
    public int Status => Error?.Status ?? StatusCodes.Status201Created; 

    public static TransferResult Success(TransferResponse transfer) => new(transfer, null);

    public static TransferResult Failure(int status, string code, string detail) => new(null, new TransferError(status, code, detail));
}

public class TransferService(AppDbContext db)
{
    public async Task<TransferResult> CreateAsync(string customerId, string idempotencyKey, TransferCommand command, CancellationToken cancellationToken)
    {
        var requestHash = ComputeRequestHash(command);
        
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var inserted = await db.Database.ExecuteSqlAsync($"""
                                                         INSERT INTO idempotency_keys (customer_id, key, request_hash) 
                                                         VALUES ({customerId}, {idempotencyKey}, {requestHash}) 
                                                         ON CONFLICT (customer_id, key) DO NOTHING 
                                                         """, cancellationToken);

        if(inserted == 0)
        {
            return await ReplayAsync(customerId, idempotencyKey, requestHash, cancellationToken);
        }

        var result = await ExecuteTransferAsync(customerId, command, cancellationToken);

        var responseBody = result.Error is not null ? JsonSerializer.Serialize(result.Error) : JsonSerializer.Serialize(result.Transfer);

        await db.Database.ExecuteSqlAsync($"""
                                          UPDATE idempotency_keys 
                                          SET response_status = {result.Status}, response_body = {responseBody} 
                                          WHERE customer_id = {customerId} AND key = {idempotencyKey} 
                                          """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<TransferResult> ReplayAsync(string customerId, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var stored = await db.IdempotencyKeys.AsNoTracking().SingleAsync(k => k.CustomerId == customerId && k.Key == idempotencyKey, cancellationToken);

        if(stored.RequestHash !=  requestHash)
        {
            return  TransferResult.Failure(StatusCodes.Status409Conflict, "idempotency_key_reused", "This Idempotency-Key was already used with a differenct request.");        
        }

        var result = stored.ResponseStatus == StatusCodes.Status201Created ? 
                                            TransferResult.Success(JsonSerializer.Deserialize<TransferResponse>(stored.ResponseBody!)!) :
                                            new TransferResult(null, JsonSerializer.Deserialize<TransferError>(stored.ResponseBody!));

        return result with { IsReplay = true };

    }

    private async Task<TransferResult> ExecuteTransferAsync(string customerId, TransferCommand command, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts.FromSql($"""
                                                 SELECT * FROM accounts
                                                 WHERE id IN ({command.SourceAccountId}, {command.DestinationAccountId})
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

        return TransferResult.Success(new TransferResponse(transfer.Id, 
                                                           transfer.SourceAccountId,
                                                           transfer.DestinationAccountId,
                                                           transfer.Amount,
                                                           transfer.Currency,
                                                           transfer.CreatedAt));
    }

    private static string ComputeRequestHash(TransferCommand command) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(command)));
}
