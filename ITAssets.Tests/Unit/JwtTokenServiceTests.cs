using System.Text;
using ITAssets.Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ITAssets.Tests.Unit;

public class JwtTokenServiceTests
{
    private const string Key = "unit-test-key-with-at-least-32-characters!!";

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static JwtOptions MakeOptions(string key = Key) =>
        new() { Issuer = "iss", Audience = "aud", Key = key, ExpiresMinutes = 60 };

    private static TokenValidationParameters Parameters(string key = Key) => new()
    {
        ValidIssuer = "iss", ValidAudience = "aud", ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        NameClaimType = "unique_name", RoleClaimType = "role"
    };

    [Fact]
    public async Task Valid_token_contains_expected_claims()
    {
        var svc = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(MakeOptions()), TimeProvider.System);
        var (token, expires) = svc.CreateToken(7, "admin", "Administrador");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters());

        Assert.True(result.IsValid);
        Assert.Equal("7", result.ClaimsIdentity.FindFirst("sub")?.Value);
        Assert.Equal("Administrador", result.ClaimsIdentity.FindFirst("role")?.Value);
        Assert.True(expires > DateTime.UtcNow);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var past = new FixedTime(DateTimeOffset.UtcNow.AddHours(-5));
        var svc = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(MakeOptions()), past);
        var (token, _) = svc.CreateToken(1, "admin", "Administrador");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters());

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var svc = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(MakeOptions("another-secret-key-with-32-characters-min!")), TimeProvider.System);
        var (token, _) = svc.CreateToken(1, "admin", "Administrador");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters());

        Assert.False(result.IsValid);
    }
}
