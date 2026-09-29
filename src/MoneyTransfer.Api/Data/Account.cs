namespace MoneyTransfer.Api.Data;

public class Account
{
    public required string Id { get; set; }
    public required string CustomerId { get; set; }
    public required string Currency { get; set; }
    public long Balance { get; set; }
}