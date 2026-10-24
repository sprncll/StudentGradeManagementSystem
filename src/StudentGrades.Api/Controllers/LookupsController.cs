using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentGrades.Api.Data;
using StudentGrades.Api.Models;

namespace StudentGrades.Api.Controllers;

/// <summary>Drop-down data for the faculty grade form.</summary>
[ApiController]
[Route("api/lookups")]
[Authorize(Roles = AppRoles.Faculty)]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LookupsController(ApplicationDbContext db) => _db = db;

    // GET /api/lookups/students
    [HttpGet("students")]
    public async Task<ActionResult<IEnumerable<StudentLookupResponse>>> Students(CancellationToken ct) =>
        Ok(await _db.Students.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.StudentNumber)
            .Select(s => new StudentLookupResponse
            {
                StudentId = s.StudentId,
                StudentNumber = s.StudentNumber,
                FullName = s.FirstName + " " + s.LastName
            })
            .ToListAsync(ct));

    // GET /api/lookups/semesters
    [HttpGet("semesters")]
    public ActionResult<IEnumerable<string>> Semesters() => Ok(AcademicTerms.Semesters);
}
