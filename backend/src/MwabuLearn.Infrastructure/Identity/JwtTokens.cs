using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Infrastructure.Identity;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKeyBase64 { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public static bool IsValid(JwtOptions value)
    {
        if (string.IsNullOrWhiteSpace(value.SigningKeyBase64)) return false;
        try
        {
            var key = Convert.FromBase64String(value.SigningKeyBase64);
            return Uri.TryCreate(value.Issuer, UriKind.Absolute, out var issuer) && issuer.Scheme == Uri.UriSchemeHttps &&
                !string.IsNullOrWhiteSpace(value.Audience) && value.Audience.Length <= 200 &&
                value.AccessTokenMinutes is >= 5 and <= 60 && key.Length >= 32 && key.Distinct().Count() >= 16;
        }
        catch (FormatException) { return false; }
    }
    public static TokenValidationParameters ValidationParameters(JwtOptions value) => new()
    {
        ValidateIssuer = true, ValidIssuer = value.Issuer,
        ValidateAudience = true, ValidAudience = value.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(value.SigningKeyBase64)),
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Sub, IncludeTokenOnFailedValidation = false
    };
}

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options)
{
    public LoginResponse Create(ApplicationUser user)
    {
        var settings = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(settings.AccessTokenMinutes);
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("ver", user.AccessTokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer32)
        ];
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expires,
            new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKeyBase64)), SecurityAlgorithms.HmacSha256));
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires);
    }
}
