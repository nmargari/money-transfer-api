using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
                                            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

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

app.MapGet("/", () => "Hello World!");

app.Run();
