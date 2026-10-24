using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentGrades.Api.Data;

namespace StudentGrades.Api.Services;

/// <summary>
/// Turns the authenticated principal (from the validated JWT) into the academic record it is
/// linked to, by reading AspNetUsers.StudentId / AspNetUsers.FacultyId from the database.
/// Nothing supplied by the browser (route, query string, body) is used for this decision.
/// </summary>
public class AcademicIdentityService
{
    private readonly ApplicationDbContext _db;

    public AcademicIdentityService(ApplicationDbContext db) => _db = db;

    public static string? GetUserId(ClaimsPrincipal user) => user.FindFirstValue(AppClaims.UserId);

    /// <summary>StudentId linked to the signed-in account, or null if the account is not linked to an active student.</summary>
    public async Task<int?> GetCurrentStudentIdAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        if (userId is null) return null;

        return await _db.Users
            .Where(u => u.Id == userId && u.StudentId != null && u.Student!.IsActive)
            .Select(u => u.StudentId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>FacultyId linked to the signed-in account, or null if the account is not linked to an active faculty member.</summary>
    public async Task<int?> GetCurrentFacultyIdAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        if (userId is null) return null;

        return await _db.Users
            .Where(u => u.Id == userId && u.FacultyId != null && u.Faculty!.IsActive)
            .Select(u => u.FacultyId)
            .FirstOrDefaultAsync(ct);
    }
}
