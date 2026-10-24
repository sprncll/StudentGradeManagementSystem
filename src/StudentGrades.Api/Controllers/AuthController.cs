using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentGrades.Api.Data;
using StudentGrades.Api.Models;
using StudentGrades.Api.Services;

namespace StudentGrades.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly TokenService _tokens;

    public AuthController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, TokenService tokens)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
    }

    /// <summary>Exchanges email + password for a JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email!.Trim());

        // Same message for "no such user" and "wrong password" so emails cannot be probed.
        if (user is null)
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Invalid email or password.");

        if (await _userManager.IsLockedOutAsync(user))
            return Problem(statusCode: 401, title: "Unauthorized",
                detail: "This account is temporarily locked because of repeated failed sign-in attempts. Try again in a few minutes.");

        if (!await _userManager.CheckPasswordAsync(user, request.Password!))
        {
            await _userManager.AccessFailedAsync(user); // counts toward lockout
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Invalid email or password.");
        }
        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        // Role-specific mapping is mandatory: a Faculty login must be linked to an active Faculty
        // row and a Student login to an active Student row, otherwise no token is issued.
        if (roles.Contains(AppRoles.Faculty) && user.FacultyId is int facultyId)
        {
            var faculty = await _db.FacultyMembers.AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacultyId == facultyId && f.IsActive);
            if (faculty is not null)
            {
                var name = $"{faculty.FirstName} {faculty.LastName}";
                var (token, expires) = _tokens.CreateToken(user, AppRoles.Faculty, name);
                return Ok(new LoginResponse
                {
                    AccessToken = token,
                    ExpiresAtUtc = expires,
                    Role = AppRoles.Faculty,
                    Email = user.Email!,
                    DisplayName = name,
                    FacultyNumber = faculty.FacultyNumber
                });
            }
        }

        if (roles.Contains(AppRoles.Student) && user.StudentId is int studentId)
        {
            var student = await _db.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.IsActive);
            if (student is not null)
            {
                var name = $"{student.FirstName} {student.LastName}";
                var (token, expires) = _tokens.CreateToken(user, AppRoles.Student, name);
                return Ok(new LoginResponse
                {
                    AccessToken = token,
                    ExpiresAtUtc = expires,
                    Role = AppRoles.Student,
                    Email = user.Email!,
                    DisplayName = name,
                    StudentNumber = student.StudentNumber
                });
            }
        }

        return Problem(statusCode: 403, title: "Forbidden",
            detail: "This account is not linked to an active faculty or student record. Contact the administrator.");
    }

    /// <summary>Returns who the current token belongs to (handy for Postman).</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        userId = User.FindFirst(AppClaims.UserId)?.Value,
        email = User.FindFirst(AppClaims.Email)?.Value,
        name = User.Identity?.Name,
        roles = User.FindAll(AppClaims.Role).Select(c => c.Value)
    });
}
