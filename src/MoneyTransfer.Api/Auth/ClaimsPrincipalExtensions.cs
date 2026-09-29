using System.Security.Claims;

namespace MoneyTransfer.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string GetCustomerId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) 
                                                                        ?? throw new InvalidOperationException("Request has no authenticated customer.");                                                                        
}
