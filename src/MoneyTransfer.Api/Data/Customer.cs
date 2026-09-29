namespace MoneyTransfer.Api.Data;

public class Customer
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string ApiKeyHash { get; set; }
}
