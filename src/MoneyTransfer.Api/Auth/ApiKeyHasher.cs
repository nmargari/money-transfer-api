using System.Security.Cryptography;
using System.Text;

namespace MoneyTransfer.Api.Auth;

public static class ApiKeyHasher
{
    public static string Hash(string apiKey) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
}
