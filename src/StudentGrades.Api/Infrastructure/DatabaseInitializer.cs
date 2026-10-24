using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentGrades.Api.Data;
using StudentGrades.Api.Models;

namespace StudentGrades.Api.Infrastructure;

/// <summary>
/// Startup tasks:
///  1. Verifies the academic tables from database/01_StudentGradeManagementDB.sql exist.
///  2. Applies the EF Core migration that creates the Identity tables (if enabled).
///  3. Creates the Faculty/Student roles.
///  4. (Development only, SeedUsers:Enabled) creates one Identity login per active Faculty and
///     Student row, matched by email and linked through AspNetUsers.FacultyId / StudentId.
///     Passwords are hashed by Identity; nothing is stored in plaintext.
/// Every step is idempotent, so restarting the API never duplicates anything.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var config = services.GetRequiredService<IConfiguration>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        try
        {
            await EnsureAcademicSchemaExistsAsync(db);

            if (config.GetValue("Database:ApplyMigrationsOnStartup", true))
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Identity migrations are up to date.");
            }

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in AppRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            if (config.GetValue("SeedUsers:Enabled", false))
            {
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                await SeedLoginsAsync(db, userManager, config, logger);
            }
        }
        catch (SqlException ex)
        {
            logger.LogCritical(ex,
                "Could not prepare the database. Check ConnectionStrings:DefaultConnection in appsettings.json " +
                "and make sure database/01_StudentGradeManagementDB.sql has been run on that SQL Server instance.");
            throw;
        }
    }

    private static async Task EnsureAcademicSchemaExistsAsync(ApplicationDbContext db)
    {
        // Students, Faculty and StudentGrades are owned by the SQL script, not by EF migrations.
        var missing = new List<string>();
        foreach (var table in new[] { "Students", "Faculty", "StudentGrades" })
        {
            var exists = await db.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = {table} AND schema_id = SCHEMA_ID(N'dbo')")
                .SingleAsync();
            if (exists == 0) missing.Add(table);
        }

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Missing table(s): {string.Join(", ", missing)}. Run database/01_StudentGradeManagementDB.sql first, then restart the API.");
    }

    private static async Task SeedLoginsAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
        var facultyPassword = config["SeedUsers:FacultyPassword"];
        var studentPassword = config["SeedUsers:StudentPassword"];
        if (string.IsNullOrWhiteSpace(facultyPassword) || string.IsNullOrWhiteSpace(studentPassword))
        {
            logger.LogWarning("SeedUsers is enabled but FacultyPassword/StudentPassword are not set; skipping demo logins.");
            return;
        }

        var facultyRows = await db.FacultyMembers.AsNoTracking().Where(f => f.IsActive).ToListAsync();
        foreach (var f in facultyRows)
        {
            await EnsureLoginAsync(userManager, logger, f.Email, AppRoles.Faculty, facultyPassword,
                link: u => u.FacultyId = f.FacultyId,
                isLinkedCorrectly: u => u.FacultyId == f.FacultyId && u.StudentId == null);
        }

        var studentRows = await db.Students.AsNoTracking().Where(s => s.IsActive).ToListAsync();
        foreach (var s in studentRows)
        {
            await EnsureLoginAsync(userManager, logger, s.Email, AppRoles.Student, studentPassword,
                link: u => u.StudentId = s.StudentId,
                isLinkedCorrectly: u => u.StudentId == s.StudentId && u.FacultyId == null);
        }
    }

    private static async Task EnsureLoginAsync(
        UserManager<ApplicationUser> userManager, ILogger logger,
        string email, string role, string password,
        Action<ApplicationUser> link, Func<ApplicationUser, bool> isLinkedCorrectly)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            link(user);
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError("Could not create login {Email}: {Errors}", email,
                    string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("Created {Role} login {Email}.", role, email);
        }
        else if (!isLinkedCorrectly(user))
        {
            // Never silently re-point an existing account to a different record.
            logger.LogWarning("Login {Email} exists but is not linked to the expected {Role} record; left unchanged.", email, role);
            return;
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
    }
}
