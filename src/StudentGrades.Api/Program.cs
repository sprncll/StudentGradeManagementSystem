using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentGrades.Api.Data;
using StudentGrades.Api.Infrastructure;
using StudentGrades.Api.Models;
using StudentGrades.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------------------------------------------------------------------------
// Database (EF Core => parameterized SQL only)
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.")));

// ---------------------------------------------------------------------------
// ASP.NET Core Identity: hashed passwords, roles, lockout.
// AddIdentityCore (not AddIdentity) is used so that no cookie scheme is registered: this is a
// token-based API and unauthenticated requests must get 401, not a redirect to a login page.
// ---------------------------------------------------------------------------
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ---------------------------------------------------------------------------
// JWT bearer authentication
// ---------------------------------------------------------------------------
var jwtKey = config["Jwt:Key"];
bool generatedDevKey = false;
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Jwt:Key is not configured. Set it with user-secrets or an environment variable (Jwt__Key).");

    // Development convenience: a random in-memory key, so no secret is ever committed.
    // Tokens become invalid when the API restarts (just sign in again).
    jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    generatedDevKey = true;
}
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");

var jwtSettings = new JwtSettings(
    Issuer: config["Jwt:Issuer"] ?? "StudentGrades.Api",
    Audience: config["Jwt:Audience"] ?? "StudentGrades.Clients",
    ExpiryMinutes: config.GetValue("Jwt:ExpiryMinutes", 60),
    SigningKey: new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)));
builder.Services.AddSingleton(jwtSettings);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub", "role", "name" exactly as issued
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSettings.SigningKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ApiErrors.WriteUnauthorizedAsync, // 401 + JSON body
            OnForbidden = ApiErrors.WriteForbiddenAsync     // 403 + JSON body
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// CORS: the MVC site's JavaScript calls this API directly with fetch().
// ---------------------------------------------------------------------------
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy("MvcClient", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// ---------------------------------------------------------------------------
// MVC controllers, validation responses, problem details
// ---------------------------------------------------------------------------
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AcademicIdentityService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ApiErrors.InvalidModelStateResponse);
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Student Grade Management API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste the accessToken from POST /api/auth/login",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (generatedDevKey)
    app.Logger.LogWarning("Jwt:Key not configured; using a random development key. Tokens are invalidated on restart.");

// Apply the Identity migration and create demo login accounts (see README).
await DatabaseInitializer.InitializeAsync(app);

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
app.UseExceptionHandler();   // unhandled exceptions -> 500 problem+json (no stack traces leaked)
app.UseStatusCodePages();    // bare 404/405 etc. -> problem+json body

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("MvcClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
