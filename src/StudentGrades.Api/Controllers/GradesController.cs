using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentGrades.Api.Data;
using StudentGrades.Api.Models;
using StudentGrades.Api.Services;

namespace StudentGrades.Api.Controllers;

/// <summary>
/// Grade management. Every action requires a valid token ([Authorize] on the class).
/// Write actions are limited to the Faculty role; GET by id is also open to Students but only
/// for their own record (ownership is checked against the database mapping, not the request).
/// </summary>
[ApiController]
[Route("api/grades")]
[Authorize]
[Produces("application/json")]
public class GradesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AcademicIdentityService _identity;

    public GradesController(ApplicationDbContext db, AcademicIdentityService identity)
    {
        _db = db;
        _identity = identity;
    }

    // GET /api/grades?search=&semester=&schoolYear=&studentId=
    [HttpGet]
    [Authorize(Roles = AppRoles.Faculty)]
    [ProducesResponseType(typeof(IEnumerable<GradeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<GradeResponse>>> GetAll(
        [FromQuery] string? search, [FromQuery] string? semester,
        [FromQuery] string? schoolYear, [FromQuery] int? studentId, CancellationToken ct)
    {
        var query = _db.StudentGrades.AsNoTracking().AsQueryable();

        if (studentId is not null) query = query.Where(g => g.StudentId == studentId);
        if (!string.IsNullOrWhiteSpace(semester)) query = query.Where(g => g.Semester == semester.Trim());
        if (!string.IsNullOrWhiteSpace(schoolYear)) query = query.Where(g => g.SchoolYear == schoolYear.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(g =>
                g.SubjectCode.Contains(term) || g.SubjectName.Contains(term) ||
                g.Student.StudentNumber.Contains(term) ||
                g.Student.FirstName.Contains(term) || g.Student.LastName.Contains(term));
        }

        var grades = await Project(query)
            .OrderBy(g => g.StudentNumber).ThenBy(g => g.SchoolYear).ThenBy(g => g.Semester).ThenBy(g => g.SubjectCode)
            .ToListAsync(ct);
        return Ok(grades);
    }

    // GET /api/grades/5
    [HttpGet("{id:int}")]
    [Authorize(Roles = AppRoles.Faculty + "," + AppRoles.Student)]
    [ProducesResponseType(typeof(GradeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradeResponse>> GetById(int id, CancellationToken ct)
    {
        var query = _db.StudentGrades.AsNoTracking().Where(g => g.GradeId == id);

        if (!User.IsInRole(AppRoles.Faculty))
        {
            // Student: only their own grade. Another student's record returns 404 so that the
            // API does not even reveal that the GradeId exists.
            var myStudentId = await _identity.GetCurrentStudentIdAsync(User, ct);
            if (myStudentId is null) return NotLinked("student");
            query = query.Where(g => g.StudentId == myStudentId);
        }

        var grade = await Project(query).FirstOrDefaultAsync(ct);
        return grade is null ? GradeNotFound(id) : Ok(grade);
    }

    // POST /api/grades
    [HttpPost]
    [Authorize(Roles = AppRoles.Faculty)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GradeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GradeResponse>> Create([FromBody] GradeUpsertRequest request, CancellationToken ct)
    {
        // FacultyId comes from the signed-in account, never from the request body.
        var facultyId = await _identity.GetCurrentFacultyIdAsync(User, ct);
        if (facultyId is null) return NotLinked("faculty");

        var (values, error) = await NormalizeAndCheckAsync(request, existingGradeId: null, ct);
        if (error is not null) return error;

        var now = UtcNowSeconds();
        var entity = new StudentGrade
        {
            StudentId = values!.StudentId,
            SubjectCode = values.SubjectCode,
            SubjectName = values.SubjectName,
            FacultyId = facultyId.Value,
            Grade = values.Grade,
            Remarks = GradeRemarksCalculator.Compute(values.Grade), // server-computed, client value ignored
            Semester = values.Semester,
            SchoolYear = values.SchoolYear,
            DatePosted = now,
            DateModified = null
        };

        _db.StudentGrades.Add(entity);
        if (await TrySaveAsync(ct) is { } conflict) return conflict;

        var created = await Project(_db.StudentGrades.AsNoTracking().Where(g => g.GradeId == entity.GradeId)).FirstAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.GradeId }, created);
    }

    // PUT /api/grades/5
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Faculty)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GradeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GradeResponse>> Update(int id, [FromBody] GradeUpsertRequest request, CancellationToken ct)
    {
        var facultyId = await _identity.GetCurrentFacultyIdAsync(User, ct);
        if (facultyId is null) return NotLinked("faculty");

        var entity = await _db.StudentGrades.FirstOrDefaultAsync(g => g.GradeId == id, ct);
        if (entity is null) return GradeNotFound(id);

        var (values, error) = await NormalizeAndCheckAsync(request, existingGradeId: id, ct);
        if (error is not null) return error;

        entity.StudentId = values!.StudentId;
        entity.SubjectCode = values.SubjectCode;
        entity.SubjectName = values.SubjectName;
        entity.Grade = values.Grade;
        entity.Remarks = GradeRemarksCalculator.Compute(values.Grade); // always recomputed: no stale remark
        entity.Semester = values.Semester;
        entity.SchoolYear = values.SchoolYear;
        entity.DateModified = UtcNowSeconds();
        // FacultyId keeps the faculty member who originally posted the grade.

        if (await TrySaveAsync(ct) is { } conflict) return conflict;

        var updated = await Project(_db.StudentGrades.AsNoTracking().Where(g => g.GradeId == id)).FirstAsync(ct);
        return Ok(updated);
    }

    // DELETE /api/grades/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Faculty)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.StudentGrades.FirstOrDefaultAsync(g => g.GradeId == id, ct);
        if (entity is null) return GradeNotFound(id);

        _db.StudentGrades.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // -----------------------------------------------------------------------------------------

    private sealed record GradeValues(int StudentId, string SubjectCode, string SubjectName, int Grade, string Semester, string SchoolYear);

    /// <summary>Trims/normalizes input, verifies the student exists, and rejects duplicates.</summary>
    private async Task<(GradeValues? Values, ActionResult? Error)> NormalizeAndCheckAsync(
        GradeUpsertRequest request, int? existingGradeId, CancellationToken ct)
    {
        // [ApiController] already returned 400 if data annotations failed, so values are present here.
        var values = new GradeValues(
            StudentId: request.StudentId!.Value,
            SubjectCode: request.SubjectCode!.Trim().ToUpperInvariant(),
            SubjectName: request.SubjectName!.Trim(),
            Grade: request.Grade!.Value,
            Semester: request.NormalizedSemester(),
            SchoolYear: request.SchoolYear!.Trim());

        var studentIsActive = await _db.Students.AnyAsync(s => s.StudentId == values.StudentId && s.IsActive, ct);
        if (!studentIsActive)
        {
            ModelState.AddModelError(nameof(GradeUpsertRequest.StudentId), $"No active student with StudentId {values.StudentId} exists.");
            return (null, ValidationProblem(ModelState));
        }

        var duplicate = await _db.StudentGrades.AnyAsync(g =>
            g.StudentId == values.StudentId && g.SubjectCode == values.SubjectCode &&
            g.Semester == values.Semester && g.SchoolYear == values.SchoolYear &&
            (existingGradeId == null || g.GradeId != existingGradeId), ct);
        if (duplicate) return (null, DuplicateConflict());

        return (values, null);
    }

    /// <summary>Saves; maps a unique-constraint race (two faculty posting at once) to 409.</summary>
    private async Task<ActionResult?> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
        {
            return DuplicateConflict();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            // CHECK or FOREIGN KEY constraint (CK_StudentGrades_Grade, FK_StudentGrades_*): database-level safety net.
            return Problem(statusCode: 400, title: "Bad Request", detail: "The grade violates a database constraint (grade range or related record).");
        }
    }

    private static IQueryable<GradeResponse> Project(IQueryable<StudentGrade> query) =>
        query.Select(g => new GradeResponse
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
        });

    private static DateTime UtcNowSeconds()
    {
        var now = DateTime.UtcNow; // matches datetime2(0): no fractional seconds
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    private ObjectResult GradeNotFound(int id) =>
        Problem(statusCode: 404, title: "Not Found", detail: $"Grade record {id} was not found.");

    private ObjectResult DuplicateConflict() =>
        Problem(statusCode: 409, title: "Conflict",
            detail: "This student already has a grade for this subject, semester and school year. Edit the existing record instead.");

    private ObjectResult NotLinked(string kind) =>
        Problem(statusCode: 403, title: "Forbidden",
            detail: $"Your account is not linked to an active {kind} record.");
}
