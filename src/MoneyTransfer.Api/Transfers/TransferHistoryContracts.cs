namespace MoneyTransfer.Api.Transfers;

public record TransferHistoryItem
(
    long Id,
    string Direction,
    string SourceAccountId,
    string DestinationAccountId,
    long Amount,
    string Currency,
    DateTimeOffset CreatedAt
);

public record TransferHistoryPage
(
    IReadOnlyList<TransferHistoryItem> Data,
    string? NextCursor
);

public record PageRequest
(
    int Limit, 
    long? AfterId
);
