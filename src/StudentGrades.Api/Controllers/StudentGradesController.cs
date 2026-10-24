using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentGrades.Api.Data;
using StudentGrades.Api.Models;
using StudentGrades.Api.Services;

namespace StudentGrades.Api.Controllers;

/// <summary>
/// Read-only endpoints for the signed-in student.
/// There is intentionally no studentId parameter anywhere in this controller: the StudentId is
/// resolved from the authenticated account (JWT "sub" -> AspNetUsers.StudentId). A request such
/// as /api/student/grades?studentId=2 simply ignores the query string and still returns only
/// the caller's own grades. There are no POST/PUT/DELETE actions here (405 Method Not Allowed).
/// </summary>
[ApiController]
[Route("api/student")]
[Authorize(Roles = AppRoles.Student)]
[Produces("application/json")]
public class StudentGradesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AcademicIdentityService _identity;

    public StudentGradesController(ApplicationDbContext db, AcademicIdentityService identity)
    {
        _db = db;
        _identity = identity;
    }

    // GET /api/student/me
    [HttpGet("me")]
    [ProducesResponseType(typeof(StudentProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentProfileResponse>> Me(CancellationToken ct)
    {
        var studentId = await _identity.GetCurrentStudentIdAsync(User, ct);
        if (studentId is null) return NotLinked();

        var profile = await _db.Students.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new StudentProfileResponse
            {
                StudentNumber = s.StudentNumber,
                FirstName = s.FirstName,
                LastName = s.LastName,
                FullName = s.FirstName + " " + s.LastName,
                Email = s.Email
            })
            .FirstAsync(ct);

        return Ok(profile);
    }

    // GET /api/student/grades
    [HttpGet("grades")]
    [ProducesResponseType(typeof(IEnumerable<GradeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GradeResponse>>> MyGrades(CancellationToken ct)
    {
        var studentId = await _identity.GetCurrentStudentIdAsync(User, ct);
        if (studentId is null) return NotLinked();

        var grades = await _db.StudentGrades.AsNoTracking()
            .Where(g => g.StudentId == studentId) // ownership filter from the account mapping
            .OrderBy(g => g.SchoolYear).ThenBy(g => g.Semester).ThenBy(g => g.SubjectCode)
            .Select(g => new GradeResponse
            {
                GradeId = g.GradeId,
                StudentId = g.StudentId,
                StudentNumber = g.Student.StudentNumber,
                StudentName = g.Student.FirstName + " " + g.Student.LastName,
                SubjectCode = g.SubjectCode,
                SubjectName = g.SubjectName,
                FacultyId = g.FacultyId,
                FacultyName = g.Faculty.FirstName + " " + g.Faculty.LastName,
                Grade = g.Grade,
                Remarks = g.Remarks,
                Semester = g.Semester,
                SchoolYear = g.SchoolYear,
                DatePosted = g.DatePosted,
                DateModified = g.DateModified
            })
            .ToListAsync(ct);

        return Ok(grades);
    }

    private ObjectResult NotLinked() =>
        Problem(statusCode: 403, title: "Forbidden", detail: "Your account is not linked to an active student record.");
}
