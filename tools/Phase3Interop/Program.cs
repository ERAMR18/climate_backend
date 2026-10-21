using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

// Test helper accepts confidential inputs on stdin and returns only the requested result.
using JsonDocument document = JsonDocument.Parse(await Console.In.ReadToEndAsync());
JsonElement input = document.RootElement;
string operation = input.GetProperty("operation").GetString()!;
PasswordHasher<object> hasher = new();
switch (operation)
{
    case "hash":
        Console.WriteLine(JsonSerializer.Serialize(new { hash = hasher.HashPassword(new object(), input.GetProperty("password").GetString()!) }));
        break;
    case "verifyHash":
        PasswordVerificationResult result = hasher.VerifyHashedPassword(new object(), input.GetProperty("hash").GetString()!, input.GetProperty("password").GetString()!);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = result != PasswordVerificationResult.Failed }));
        break;
    case "verifyToken":
        JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
        System.Security.Claims.ClaimsPrincipal principal = handler.ValidateToken(input.GetProperty("token").GetString()!, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = "ClimateMonitoring.Identity",
            ValidateAudience = true, ValidAudience = "ClimateMonitoring.Client",
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(input.GetProperty("secret").GetString()!)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        }, out _);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = principal.Identity!.IsAuthenticated, administrator = principal.IsInRole("Administrator") }));
        break;
    default:
        throw new InvalidOperationException("Unknown interop operation.");
}
