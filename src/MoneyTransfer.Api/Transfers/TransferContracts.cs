namespace MoneyTransfer.Api.Transfers;

public record CreateTransferRequest
(
    string? SourceAccountId,
    string? DestinationAccountId,
    long? Amount,
    string? Currency);

public record TransferCommand
(
    string SourceAccountId,
    string DestinationAccountId,
    long Amount,
    string Currency);

public record TransferResponse
(
    long Id,
    string SourceAccountId,
    string DestinationAccountId,
    long Amount,
    string Currency,
    DateTimeOffset CreatedAt
);