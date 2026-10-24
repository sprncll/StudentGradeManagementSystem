using System.ComponentModel.DataAnnotations;

namespace StudentGrades.Mvc.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>Shape of the API's POST /api/auth/login response.</summary>
public class ApiLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public string? FacultyNumber { get; set; }
}

/// <summary>Minimal ProblemDetails reader for API error bodies.</summary>
public class ApiProblem
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public int? Status { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}

/// <summary>What a dashboard page needs so its JavaScript can call the API.</summary>
public class DashboardViewModel
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Number { get; set; }
}

public class ErrorViewModel
{
    public int StatusCode { get; set; } = 500;
    public string Message { get; set; } = "Something went wrong.";
    public string? RequestId { get; set; }
}

public static class AppRoles
{
    public const string Faculty = "Faculty";
    public const string Student = "Student";
}

public static class AppClaimTypes
{
    public const string Number = "academic_number"; // student or faculty number, for display only
}
