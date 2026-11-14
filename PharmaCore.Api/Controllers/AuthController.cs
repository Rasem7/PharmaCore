using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PharmaCore.Application.Abstractions;

namespace PharmaCore.Api.Controllers;

public sealed record LoginRequest(string UserName, string Password);
public sealed record LoginResponse(string AccessToken, string UserName, IReadOnlyList<string> Permissions);

[ApiController, Route("api/auth")]
public sealed class AuthController(IUserRepository users, IPasswordHasher hasher, ITokenService tokens) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrEmpty(req.Password))
            return Problem("User name and password are required.", statusCode: 400);

        var user = await users.FindByUserNameAsync(req.UserName.Trim(), ct);
        // Same response for unknown user, bad password, disabled, or locked: avoids account enumeration.
        var invalid = Problem("Invalid credentials.", statusCode: 401);
        if (user is null || !user.IsActive) return invalid;
        if (user.LockedUntilUtc is { } until && until > DateTime.UtcNow) return invalid;

        var ok = hasher.Verify(req.Password, user.PasswordHash);
        await users.RegisterLoginResultAsync(user.Id, ok, ct);
        if (!ok) return invalid;

        var perms = await users.GetPermissionsAsync(user.Id, ct);
        return new LoginResponse(tokens.CreateToken(user.Id, user.TenantId, user.UserName, perms), user.UserName, perms);
    }
}
