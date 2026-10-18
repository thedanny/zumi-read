using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace StorySprout.Api;

public record SignupRequest(string Email, string Password, string? Website);
public record LoginRequest(string Email, string Password);

public sealed class CurrentFamily(IHttpContextAccessor accessor)
{
    public Guid Id => Guid.Parse(accessor.HttpContext?.User.FindFirstValue("household_id") ?? throw new UnauthorizedAccessException());
}

public static class FamilyAuth
{
    public static string? NormalizeEmail(string? input)
    {
        var email = input?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email.Any(char.IsControl) ||
            !MailAddress.TryCreate(email, out var parsed) ||
            !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase) ||
            !parsed.Host.Contains('.')) return null;
        return email.ToLowerInvariant();
    }

    public static async Task SignIn(HttpContext context, FamilyAccount account)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim("household_id", account.HouseholdId.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }
}
