namespace MoneyTransfer.Api.Data;

public class Transfer
{
    public long Id { get; set;}
    public required string SourceAccountId { get; set; }
    public required string DestinationAccountId { get; set; }
    public long Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}