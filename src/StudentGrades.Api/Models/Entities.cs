using Microsoft.AspNetCore.Identity;

namespace StudentGrades.Api.Models;

/// <summary>Row in dbo.Students (created by database/01_StudentGradeManagementDB.sql).</summary>
public class Student
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Row in dbo.Faculty (created by the SQL script).</summary>
public class Faculty
{
    public int FacultyId { get; set; }
    public string FacultyNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Row in dbo.StudentGrades (created by the SQL script).</summary>
public class StudentGrade
{
    public int GradeId { get; set; }
    public int StudentId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public int Grade { get; set; }

    /// <summary>Always produced by <see cref="Services.GradeRemarksCalculator"/>. Never copied from a request.</summary>
    public string Remarks { get; set; } = string.Empty;

    public string Semester { get; set; } = string.Empty;
    public string SchoolYear { get; set; } = string.Empty;
    public DateTime DatePosted { get; set; }
    public DateTime? DateModified { get; set; }

    public Student Student { get; set; } = null!;
    public Faculty Faculty { get; set; } = null!;
}

/// <summary>
/// ASP.NET Core Identity user extended with a link to exactly one academic record.
/// A Student login has StudentId set; a Faculty login has FacultyId set (never both:
/// enforced by the CK_AspNetUsers_SingleAcademicLink check constraint).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public int? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
}

public static class AppRoles
{
    public const string Faculty = "Faculty";
    public const string Student = "Student";
    public static readonly string[] All = { Faculty, Student };
}

public static class AcademicTerms
{
    public static readonly string[] Semesters = { "1st Semester", "2nd Semester", "Summer" };
}
