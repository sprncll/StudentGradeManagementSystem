using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace StudentGrades.Api.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    public string? Password { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAtUtc { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public string? FacultyNumber { get; set; }
}

/// <summary>
/// Body for POST /api/grades and PUT /api/grades/{id}.
///
/// Deliberately has NO Remarks, FacultyId, GradeId, DatePosted or DateModified properties:
/// any such values sent by a client are ignored by the JSON deserializer and can never reach
/// the database. Remarks are computed on the server; FacultyId comes from the signed-in account.
///
/// Grade is int? so that a missing grade produces "Grade is required." instead of silently
/// defaulting to 0. A decimal (85.5), a string ("85") or text is rejected by the JSON
/// deserializer and turned into a 400 response by ValidationResponseFactory.
/// </summary>
public class GradeUpsertRequest : IValidatableObject
{
    [Required(ErrorMessage = "StudentId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "StudentId must be a positive whole number.")]
    public int? StudentId { get; set; }

    [Required(ErrorMessage = "SubjectCode is required.")]
    [StringLength(30, ErrorMessage = "SubjectCode must be at most 30 characters.")]
    [RegularExpression(@"^\s*[A-Za-z0-9\-]+\s*$", ErrorMessage = "SubjectCode may contain only letters, digits and hyphens.")]
    public string? SubjectCode { get; set; }

    [Required(ErrorMessage = "SubjectName is required.")]
    [StringLength(200, ErrorMessage = "SubjectName must be at most 200 characters.")]
    public string? SubjectName { get; set; }

    [Required(ErrorMessage = "Grade is required.")]
    [Range(0, 100, ErrorMessage = "Grade must be a whole number from 0 to 100.")]
    public int? Grade { get; set; }

    [Required(ErrorMessage = "Semester is required.")]
    public string? Semester { get; set; }

    [Required(ErrorMessage = "SchoolYear is required.")]
    public string? SchoolYear { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // [Required] already rejects null, empty and whitespace-only strings.
        if (!string.IsNullOrWhiteSpace(Semester) &&
            !AcademicTerms.Semesters.Contains(Semester.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"Semester must be one of: {string.Join(", ", AcademicTerms.Semesters)}.",
                new[] { nameof(Semester) });
        }

        if (!string.IsNullOrWhiteSpace(SchoolYear))
        {
            var match = Regex.Match(SchoolYear.Trim(), @"^(\d{4})-(\d{4})$");
            if (!match.Success || int.Parse(match.Groups[2].Value) != int.Parse(match.Groups[1].Value) + 1)
            {
                yield return new ValidationResult(
                    "SchoolYear must look like 2026-2027 (two consecutive years).",
                    new[] { nameof(SchoolYear) });
            }
        }
    }

    /// <summary>Returns the canonical semester spelling, e.g. "1st semester" -> "1st Semester".</summary>
    public string NormalizedSemester() =>
        AcademicTerms.Semesters.First(s => s.Equals(Semester!.Trim(), StringComparison.OrdinalIgnoreCase));
}

public class GradeResponse
{
    public int GradeId { get; set; }
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string SchoolYear { get; set; } = string.Empty;
    public DateTime DatePosted { get; set; }
    public DateTime? DateModified { get; set; }
}

public class StudentProfileResponse
{
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class StudentLookupResponse
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
