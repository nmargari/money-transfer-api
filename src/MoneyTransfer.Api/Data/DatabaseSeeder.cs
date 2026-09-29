using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Auth;

namespace MoneyTransfer.Api.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        await InsertCustomerAsync(db, "alice", "Alice", "alice-key");
        await InsertCustomerAsync(db, "bob", "Bob", "bob-key");

        await InsertAccountAsync(db, "alice-eur", "alice", "EUR", 100_000);
        await InsertAccountAsync(db, "bob-eur", "bob", "EUR", 25_000);
        await InsertAccountAsync(db, "alice-usd", "alice", "USD", 50_000);

        await transaction.CommitAsync();
    }

    private static Task InsertCustomerAsync(AppDbContext db, string id, string name, string apiKey)
    {
        var apiKeyHash = ApiKeyHasher.Hash(apiKey);

        return db.Database.ExecuteSqlAsync($"""
                                            INSERT INTO customers (id, name, api_key_hash)
                                            VALUES ({id}, {name}, {apiKeyHash})
                                            ON CONFLICT (id) DO NOTHING;
                                            """);
    }

    private static Task InsertAccountAsync(AppDbContext db, string id, string customerId, string currency, long balance)
    {
        return db.Database.ExecuteSqlAsync($"""
                                            INSERT INTO accounts (id, customer_id, currency, balance)
                                            VALUES ({id}, {customerId}, {currency}, {balance})
                                            ON CONFLICT (id) DO NOTHING;
                                            """);
    }
}
