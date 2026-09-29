using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyTransfer.Api.Data;

namespace MoneyTransfer.Api.Auth;

public class ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
                                            ILoggerFactory logger,
                                            UrlEncoder encoder,
                                            AppDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = headerValues.ToString();
        if(string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("API key is empty.");
        }

        var apiKeyHash = ApiKeyHasher.Hash(apiKey);

        var customerId = await db.Customers.Where(c => c.ApiKeyHash == apiKeyHash)
                                            .Select(c => c.Id)
                                            .SingleOrDefaultAsync(Context.RequestAborted);

        if(customerId is null)
        {
            return AuthenticateResult.Fail("Api key is invalid.");
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, customerId)], SchemeName);
        
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
