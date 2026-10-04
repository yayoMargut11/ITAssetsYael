using ITAssets.Api.Data;
using ITAssets.Api.Middleware;
using ITAssets.Api.Models;

namespace ITAssets.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
}

public class AuthService : IAuthService
{
    // Hash de relleno: se verifica cuando el usuario no existe para que el tiempo de respuesta
    // no revele si el usuario existe (mitiga enumeración de usuarios por timing).
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-password-not-used", 11);

    private readonly IAuthRepository _repo;
    private readonly IJwtTokenService _jwt;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAuthRepository repo, IJwtTokenService jwt, TimeProvider time, ILogger<AuthService> logger)
    {
        _repo = repo; _jwt = jwt; _time = time; _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var user = await _repo.GetByUsernameAsync(request.Username.Trim(), ct);

        if (user is null)
        {
            SafeVerify(request.Password, DummyHash);
            throw InvalidCredentials();
        }

        if (user.LockoutEnd is { } lockEnd)
        {
            if (lockEnd > now)
                throw new AppException(423, "ACCOUNT_LOCKED", "Cuenta bloqueada temporalmente por intentos fallidos. Inténtalo más tarde.");
            await _repo.ResetAttemptsAsync(user.Id, ct); // el bloqueo expiró: se reinicia el contador
        }

        var passwordOk = SafeVerify(request.Password, user.PasswordHash);
        if (!passwordOk)
        {
            await _repo.RegisterFailedAsync(user.Id, ct);
            _logger.LogWarning("Intento de login fallido para el usuario {UserId}", user.Id);
            throw InvalidCredentials();
        }

        if (!user.IsActive) throw InvalidCredentials();

        if (user.FailedAttempts > 0) await _repo.ResetAttemptsAsync(user.Id, ct);

        var (token, expires) = _jwt.CreateToken(user.Id, user.Username, user.RoleName);
        return new LoginResponse { Token = token, ExpiresAtUtc = expires, Username = user.Username, Role = user.RoleName };
    }

    private static bool SafeVerify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }

    private static AppException InvalidCredentials() =>
        new(401, "INVALID_CREDENTIALS", "Usuario o contraseña incorrectos.");
}
