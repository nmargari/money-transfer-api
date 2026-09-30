namespace MoneyTransfer.Api.Data;

public class IdempotencyKey
{
    public required string CustomerId { get; set; }
    public required string Key { get; set; }
    public required string RequestHash { get; set; }
    public int? ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
