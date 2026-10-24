using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using StudentGrades.Api.Models;

namespace StudentGrades.Api.Services;

/// <summary>Resolved JWT settings (the signing key is read once at startup in Program.cs).</summary>
public sealed record JwtSettings(string Issuer, string Audience, int ExpiryMinutes, SymmetricSecurityKey SigningKey);

/// <summary>Short claim names used in the JWT (inbound claim mapping is turned off in Program.cs).</summary>
public static class AppClaims
{
    public const string UserId = JwtRegisteredClaimNames.Sub; // "sub"
    public const string Role = "role";
    public const string Name = "name";
    public const string Email = JwtRegisteredClaimNames.Email;
}

public class TokenService
{
    private readonly JwtSettings _settings;

    public TokenService(JwtSettings settings) => _settings = settings;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, string role, string displayName)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        // The token says WHO the user is and WHAT role they have. It intentionally does not carry
        // StudentId/FacultyId: the API looks those up from AspNetUsers on every request, so the
        // ownership decision is always made from the server-side account mapping.
        var claims = new List<Claim>
        {
            new(AppClaims.UserId, user.Id),
            new(AppClaims.Email, user.Email ?? string.Empty),
            new(AppClaims.Name, displayName),
            new(AppClaims.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(_settings.SigningKey, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
