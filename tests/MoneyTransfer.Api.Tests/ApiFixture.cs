using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransfer.Api.Data;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace MoneyTransfer.Api.Tests;

public class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new TestApiFactory(_postgres.GetConnectionString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var port = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Port;
        if (port != _postgres.GetMappedPublicPort(5432))
        {
            throw new InvalidOperationException("Tests are not connected to the test container.");
        }

        await db.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("TRUNCATE transfers, idempotency_keys, accounts, customers RESTART IDENTITY CASCADE");

        await DatabaseSeeder.SeedAsync(db);
    }

    public HttpClient CreateClient(string apiKey)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        return client;
    }

    public async Task<long> GetBalanceAsync(string accountId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        return await db.Accounts.Where(a => a.Id == accountId).Select(a => a.Balance).SingleAsync();
    }

    public async Task<int> CountTransfersAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        return await db.Transfers.CountAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed class TestApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("ConnectionStrings:Default", connectionString);
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    
}
