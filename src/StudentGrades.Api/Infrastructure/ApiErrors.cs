using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace StudentGrades.Api.Infrastructure;

public static class ApiErrors
{
    /// <summary>
    /// Replaces the default automatic-400 body with clean, field-keyed messages.
    /// In particular, JSON values that cannot be converted to an int (85.5, "85", "abc", true)
    /// become: Grade -> "Grade must be a whole number from 0 to 100."
    /// </summary>
    public static IActionResult InvalidModelStateResponse(ActionContext context)
    {
        var modelState = context.ModelState;
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // When the JSON itself could not be read, the framework also adds a noisy
        // "The request field is required." error for the action parameter. Skip it.
        bool bodyUnreadable = modelState.Keys.Any(k => k.StartsWith('$') || k.Length == 0);

        foreach (var (key, entry) in modelState)
        {
            if (entry.Errors.Count == 0) continue;
            bool isJsonError = key.StartsWith('$') || key.Length == 0;
            if (bodyUnreadable && !isJsonError) continue;

            var field = ToFieldName(key);
            var messages = entry.Errors
                .Select(e => isJsonError
                    ? JsonErrorMessage(field)
                    : (string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message ?? "Invalid value." : e.ErrorMessage))
                .Distinct()
                .ToArray();

            errors[field] = errors.TryGetValue(field, out var existing) ? existing.Concat(messages).Distinct().ToArray() : messages;
        }

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Detail = "The request was rejected. Fix the listed fields and try again.",
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string ToFieldName(string key)
    {
        // "$.grade" -> "Grade", "$" or "" -> "Body", "Grade" -> "Grade"
        if (key.Length == 0 || key == "$") return "Body";
        var name = key.StartsWith("$.") ? key[2..] : key;
        name = name.Split('.', '[')[0];
        return name.Length == 0 ? "Body" : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string JsonErrorMessage(string field) => field switch
    {
        "Grade" => "Grade must be a whole number from 0 to 100. Decimal, text and other non-integer values are not accepted.",
        "StudentId" => "StudentId must be a whole number.",
        "Body" => "The request body is missing or is not valid JSON.",
        _ => $"{field} has a value of the wrong type."
    };

    /// <summary>JSON body for 401 responses (default JwtBearer 401s have an empty body).</summary>
    public static async Task WriteUnauthorizedAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse(); // suppress the default empty 401
        if (context.Response.HasStarted) return;

        string detail = context.AuthenticateFailure switch
        {
            SecurityTokenExpiredException => "Your access token has expired. Sign in again to get a new token.",
            not null => "The access token is invalid.",
            _ => "Authentication is required. Send a valid token in the header: Authorization: Bearer <token>."
        };

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = detail,
            Instance = context.Request.Path
        }, options: null, contentType: "application/problem+json");
    }

    /// <summary>JSON body for 403 responses (authenticated, but the role is not allowed).</summary>
    public static async Task WriteForbiddenAsync(ForbiddenContext context)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "Your account is signed in but is not allowed to perform this action.",
            Instance = context.Request.Path
        }, options: null, contentType: "application/problem+json");
    }
}
