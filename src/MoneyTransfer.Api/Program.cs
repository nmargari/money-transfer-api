using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Auth;
using MoneyTransfer.Api.Data;
using MoneyTransfer.Api.Endpoints;
using MoneyTransfer.Api.Errors;
using MoneyTransfer.Api.Transfers;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
                                            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, configureOptions : null);
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options => 
                                          options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddScoped<TransferHistoryService>();

var app = builder.Build();

var command = args.FirstOrDefault();
if(command is "migrate" or "seed")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if(command == "migrate")
    {
        await db.Database.MigrateAsync();
    }
    else if(command == "seed")
    {
        await DatabaseSeeder.SeedAsync(db);
    }    

    Console.WriteLine($"Command '{command}' completed.");

    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("").RequireAuthorization();
api.MapAccountEndpoints();
api.MapTransferEndpoints();

app.Run();

public partial class Program { }