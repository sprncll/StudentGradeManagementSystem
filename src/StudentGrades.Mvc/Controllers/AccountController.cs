using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentGrades.Mvc.Models;

namespace StudentGrades.Mvc.Controllers;

public class AccountController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IHttpClientFactory httpClientFactory, ILogger<AccountController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToDashboard();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var client = _httpClientFactory.CreateClient("GradeApi");
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/auth/login", new { model.Email, model.Password });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Grade API unreachable at {BaseAddress}", client.BaseAddress);
            ModelState.AddModelError(string.Empty,
                $"Cannot reach the Grade API at {client.BaseAddress}. Make sure the API project is running, then try again.");
            return View(model);
        }

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ReadProblemAsync(response);
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => problem?.Detail ?? "Invalid email or password.",
                HttpStatusCode.Forbidden => problem?.Detail ?? "This account is not allowed to sign in.",
                HttpStatusCode.BadRequest => problem?.Errors is { Count: > 0 }
                    ? string.Join(" ", problem.Errors.SelectMany(e => e.Value))
                    : problem?.Detail ?? "The sign-in request was invalid.",
                _ => $"The Grade API returned an unexpected error ({(int)response.StatusCode}). Please try again."
            };
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        var login = await response.Content.ReadFromJsonAsync<ApiLoginResponse>();
        if (login is null || string.IsNullOrEmpty(login.AccessToken) ||
            (login.Role != AppRoles.Faculty && login.Role != AppRoles.Student))
        {
            ModelState.AddModelError(string.Empty, "The Grade API returned an unexpected sign-in response.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, login.DisplayName),
            new(ClaimTypes.Email, login.Email),
            new(ClaimTypes.Role, login.Role),
            new(AppClaimTypes.Number, login.StudentNumber ?? login.FacultyNumber ?? string.Empty)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTime.SpecifyKind(login.ExpiresAtUtc, DateTimeKind.Utc), // cookie ends when the JWT ends
            AllowRefresh = false
        };
        // The JWT is kept inside the encrypted, HttpOnly auth cookie (not in localStorage).
        properties.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = login.AccessToken } });

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return login.Role == AppRoles.Faculty
            ? RedirectToAction("Index", "Faculty")
            : RedirectToAction("Index", "Student");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToDashboard() =>
        User.IsInRole(AppRoles.Faculty) ? RedirectToAction("Index", "Faculty")
        : User.IsInRole(AppRoles.Student) ? RedirectToAction("Index", "Student")
        : View(new LoginViewModel());

    private static async Task<ApiProblem?> ReadProblemAsync(HttpResponseMessage response)
    {
        try { return await response.Content.ReadFromJsonAsync<ApiProblem>(); }
        catch { return null; } // body was empty or not JSON
    }
}
