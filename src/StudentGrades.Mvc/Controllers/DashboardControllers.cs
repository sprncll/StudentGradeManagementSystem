using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentGrades.Mvc.Models;

namespace StudentGrades.Mvc.Controllers;

/// <summary>Builds the model that hands the API base URL and the user's JWT to the page's JavaScript.</summary>
public abstract class DashboardControllerBase : Controller
{
    private readonly IConfiguration _config;

    protected DashboardControllerBase(IConfiguration config) => _config = config;

    protected async Task<IActionResult> DashboardViewAsync()
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token))
        {
            // Old/invalid cookie without a token: force a fresh sign-in.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        return View(new DashboardViewModel
        {
            ApiBaseUrl = (_config["GradeApi:BaseUrl"] ?? string.Empty).TrimEnd('/'),
            AccessToken = token,
            DisplayName = User.Identity?.Name ?? string.Empty,
            Number = User.FindFirstValue(AppClaimTypes.Number)
        });
    }
}

/// <summary>
/// Faculty page. [Authorize(Roles)] here only decides who may open the PAGE; every grade
/// operation is authorized again by the Web API using the JWT.
/// </summary>
[Authorize(Roles = AppRoles.Faculty)]
public class FacultyController : DashboardControllerBase
{
    public FacultyController(IConfiguration config) : base(config) { }

    public Task<IActionResult> Index() => DashboardViewAsync();
}

/// <summary>Student page (read-only). The page has no create/edit/delete controls.</summary>
[Authorize(Roles = AppRoles.Student)]
public class StudentController : DashboardControllerBase
{
    public StudentController(IConfiguration config) : base(config) { }

    public Task<IActionResult> Index() => DashboardViewAsync();
}

public class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.IsInRole(AppRoles.Faculty)) return RedirectToAction("Index", "Faculty");
        if (User.IsInRole(AppRoles.Student)) return RedirectToAction("Index", "Student");
        return RedirectToAction("Login", "Account");
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View("Error", new ErrorViewModel
    {
        StatusCode = 500,
        Message = "An unexpected error occurred. Please try again.",
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });

    [AllowAnonymous]
    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code) => View("Error", new ErrorViewModel
    {
        StatusCode = code,
        Message = code switch
        {
            404 => "The page you requested does not exist.",
            403 => "You do not have permission to view this page.",
            _ => "The request could not be completed."
        },
        RequestId = HttpContext.TraceIdentifier
    });
}
