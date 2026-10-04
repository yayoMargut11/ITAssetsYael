using ITAssets.Api.Data;
using ITAssets.Api.Middleware;
using ITAssets.Api.Models;
using ITAssets.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ITAssets.Tests.Unit;

public class AuthServiceTests
{
    private const string Password = "Secret#123";

    private sealed class FakeRepo : IAuthRepository
    {
        public UserRecord? User;
        public int Failed, Reset;
        public Task<UserRecord?> GetByUsernameAsync(string username, CancellationToken ct) =>
            Task.FromResult(User is not null && User.Username == username ? User : null);
        public Task RegisterFailedAsync(int userId, CancellationToken ct) { Failed++; return Task.CompletedTask; }
        public Task ResetAttemptsAsync(int userId, CancellationToken ct) { Reset++; return Task.CompletedTask; }
    }

    private sealed class FakeJwt : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAtUtc) CreateToken(int userId, string username, string role) =>
            ("token-" + userId, DateTime.UtcNow.AddHours(1));
    }

    private static UserRecord User(bool active = true, DateTime? lockout = null, int failed = 0) => new()
    {
        Id = 1, Username = "admin", RoleName = "Administrador", IsActive = active,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4), // work factor bajo: solo para pruebas
        LockoutEnd = lockout, FailedAttempts = failed
    };

    private static (AuthService svc, FakeRepo repo) Create(UserRecord? user)
    {
        var repo = new FakeRepo { User = user };
        return (new AuthService(repo, new FakeJwt(), TimeProvider.System, NullLogger<AuthService>.Instance), repo);
    }

    [Fact]
    public async Task Correct_credentials_return_token()
    {
        var (svc, _) = Create(User());
        var res = await svc.LoginAsync(new LoginRequest { Username = "admin", Password = Password }, default);
        Assert.Equal("token-1", res.Token);
        Assert.Equal("Administrador", res.Role);
    }

    [Fact]
    public async Task Wrong_password_returns_401_and_counts_failure()
    {
        var (svc, repo) = Create(User());
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.LoginAsync(new LoginRequest { Username = "admin", Password = "mala" }, default));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal(1, repo.Failed);
    }

    [Fact]
    public async Task Unknown_user_returns_same_401_as_wrong_password()
    {
        var (svc, repo) = Create(null);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.LoginAsync(new LoginRequest { Username = "nadie", Password = "x" }, default));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", ex.Code);
        Assert.Equal(0, repo.Failed);
    }

    [Fact]
    public async Task Locked_account_is_rejected_even_with_correct_password()
    {
        var (svc, _) = Create(User(lockout: DateTime.UtcNow.AddMinutes(10), failed: 5));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.LoginAsync(new LoginRequest { Username = "admin", Password = Password }, default));
        Assert.Equal(423, ex.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_cannot_login()
    {
        var (svc, _) = Create(User(active: false));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.LoginAsync(new LoginRequest { Username = "admin", Password = Password }, default));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Successful_login_resets_failed_attempts()
    {
        var (svc, repo) = Create(User(failed: 3));
        await svc.LoginAsync(new LoginRequest { Username = "admin", Password = Password }, default);
        Assert.Equal(1, repo.Reset);
    }
}
