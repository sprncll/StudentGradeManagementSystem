# Student Grade Management System

Final Practical Examination for Web Systems and Technologies.

It has two projects:

| Project | Type | URL (Development) | Purpose |
|---|---|---|---|
| `src/StudentGrades.Api` | ASP.NET Core Web API (.NET 8) | https://localhost:7080 (also http://localhost:5080) | REST endpoints, Identity, JWT, validation, remark computation, authorization |
| `src/StudentGrades.Mvc` | ASP.NET Core MVC (.NET 8) | https://localhost:7090 | Sign-in page, Faculty dashboard, Student dashboard. The dashboards call the API with JavaScript `fetch()` |

```
Browser (MVC pages + fetch)  ──Bearer JWT──►  Web API  ──EF Core──►  SQL Server
        ▲                                        │                   StudentGradeManagementDB
        └── MVC sign-in cookie (holds the JWT)   └── ASP.NET Core Identity (AspNetUsers.StudentId / FacultyId)
```

---

## 1. Prerequisites

* .NET 8 SDK (`dotnet --version` should print 8.x). See section 9 if you only have .NET 9 or 10.
* SQL Server 2019 or newer. Express, Developer and LocalDB all work.
* SQL Server Management Studio (SSMS) or Azure Data Studio, to run the SQL script.
* Visual Studio 2022 (17.8 or newer) or VS Code with the C# Dev Kit.
* Postman, desktop app.
* Optional: the EF Core CLI (`dotnet tool install --global dotnet-ef --version 8.*`).
* A trusted HTTPS development certificate. Run this once: `dotnet dev-certs https --trust`

## 2. Database setup

1. Open `database/01_StudentGradeManagementDB.sql` in SSMS and execute it.
   It creates `StudentGradeManagementDB` with the `Students`, `Faculty` and `StudentGrades` tables, all keys and constraints, the `GetGradeRemarks` procedure and the sample data.
   You can safely run it again because every seed step is idempotent.
2. Check the connection string in `src/StudentGrades.Api/appsettings.json`:

   ```json
   "DefaultConnection": "Server=localhost;Database=StudentGradeManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
   ```

   Common alternatives:

   | SQL Server edition | `Server=` value |
   |---|---|
   | Default instance | `localhost` or `.` |
   | SQL Express | `localhost\\SQLEXPRESS` (JSON needs the double backslash) |
   | LocalDB | `(localdb)\\MSSQLLocalDB` |
   | SQL login instead of Windows login | replace `Trusted_Connection=True` with `User Id=sa;Password=...` (do not commit this) |

## 3. Identity tables (EF Core migration) and demo accounts

The SQL script holds the academic data only. Login accounts live in the ASP.NET Core Identity tables, which an EF Core migration creates:

* `Migrations/20261024000000_InitialIdentity.cs` creates `AspNetUsers`, `AspNetRoles` and the other Identity tables. It also adds these mapping columns and constraints:
  * `AspNetUsers.StudentId`: FK to `Students(StudentId)`, unique when not null
  * `AspNetUsers.FacultyId`: FK to `Faculty(FacultyId)`, unique when not null
  * `CK_AspNetUsers_SingleAcademicLink`: a login can't be linked to both a student and a faculty member
* The academic tables are mapped with `ExcludeFromMigrations()`, so EF never tries to create or change the instructor's tables.

**You don't need to run anything.** When the API starts it:

1. checks that the script's tables exist (and stops with a clear message if they don't),
2. applies the migration (`Database:ApplyMigrationsOnStartup = true`),
3. creates the `Faculty` and `Student` roles,
4. in Development only, creates one Identity login for every active Faculty and Student row. Each login is matched by email and linked through `FacultyId`/`StudentId`. Identity hashes the passwords; none are stored in plaintext.

If you prefer to run the migration by hand:

```bash
cd src/StudentGrades.Api
dotnet ef database update
```

If you can't use EF tools at all, run `database/02_IdentitySchema_Fallback.sql` after script 01. It creates the same tables and marks the migration as applied.

### Demo credentials (Development only)

These come from `src/StudentGrades.Api/appsettings.Development.json` under `SeedUsers`. They are demonstration passwords, not real ones.

| Role | Email | Password | Linked record |
|---|---|---|---|
| Faculty | andrea.garcia@example.com | `Faculty#2026` | FAC-001 Andrea Garcia |
| Faculty | michael.ramos@example.com | `Faculty#2026` | FAC-002 Michael Ramos |
| Student | juan.delacruz@example.com | `Student#2026` | 2026-0001 Juan Dela Cruz |
| Student | maria.santos@example.com | `Student#2026` | 2026-0002 Maria Santos |
| Student | pedro.reyes@example.com | `Student#2026` | 2026-0003 Pedro Reyes |

If you add a new row to `Students` or `Faculty`, restart the API and it creates that person's login automatically.

## 4. Configuration and secrets

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | API `appsettings.json` | see section 2 |
| `Jwt:Key` | **user-secrets or env var only** | In Development, if it isn't set, the API generates a random key at startup, so tokens stop working after a restart (just sign in again). Outside Development the API refuses to start without it. |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | API `appsettings.json` | token lifetime defaults to 60 minutes |
| `Cors:AllowedOrigins` | API `appsettings.json` | must contain the MVC site's URL |
| `SeedUsers:*` | API `appsettings.Development.json` | demo logins; turned off (`Enabled: false`) in the base `appsettings.json` |
| `GradeApi:BaseUrl` | MVC `appsettings.json` | where the MVC site and its JavaScript reach the API |

To set a fixed signing key, which is optional and keeps tokens valid across restarts:

```bash
cd src/StudentGrades.Api
dotnet user-secrets set "Jwt:Key" "replace-with-a-long-random-string-of-at-least-32-characters"
```

## 5. Running the system

Start the **API first**, then the MVC site.

**Visual Studio:** open `StudentGradeManagementSystem.sln`, right-click the solution, choose *Configure Startup Projects…*, select *Multiple startup projects*, set both to *Start* (API on top), and use the `https` profile. Press F5.

**Command line** (two terminals):

```bash
dotnet run --project src/StudentGrades.Api --launch-profile https
dotnet run --project src/StudentGrades.Mvc --launch-profile https
```

* API Swagger UI: https://localhost:7080/swagger. Use `POST /api/auth/login`, then click **Authorize** and paste the token.
* Web app: https://localhost:7090. Sign in with an account from the table in section 3.

## 6. API reference

All request and response bodies are JSON. Errors use the standard `ProblemDetails` format (`title`, `status`, `detail`, and for 400 responses an `errors` object).

| Method | Route | Who | Success | Typical errors |
|---|---|---|---|---|
| POST | `/api/auth/login` | anyone | 200 + `{accessToken, role, displayName, …}` | 400, 401 (bad credentials or locked), 403 (account not linked) |
| GET | `/api/auth/me` | any signed-in user | 200 | 401 |
| GET | `/api/grades?search=&semester=&schoolYear=&studentId=` | Faculty | 200 list | 401, 403 |
| GET | `/api/grades/{id}` | Faculty: any record. Student: **own record only** | 200 | 401, 404 (missing, or another student's record) |
| POST | `/api/grades` | Faculty | **201** + `Location` header | 400, 401, 403, 409 duplicate |
| PUT | `/api/grades/{id}` | Faculty | 200 updated record | 400, 401, 403, 404, 409 |
| DELETE | `/api/grades/{id}` | Faculty | **204** | 401, 403, 404 |
| GET | `/api/student/me` | Student | 200 profile (name, student number) | 401, 403 |
| GET | `/api/student/grades` | Student | 200 own grades | 401, 403 |
| GET | `/api/lookups/students`, `/api/lookups/semesters` | Faculty | 200 | 401, 403 |

POST and PUT body (`remarks`, `facultyId`, dates and other extra fields are **ignored**):

```json
{
  "studentId": 1,
  "subjectCode": "WEB102",
  "subjectName": "Advanced Web Systems",
  "grade": 88,
  "semester": "1st Semester",
  "schoolYear": "2026-2027"
}
```

Valid semesters are `1st Semester`, `2nd Semester` and `Summer`. A school year must be two consecutive years, like `2026-2027`.

## 7. Testing with Postman

1. Start the API.
2. In Postman, choose **Import** and pick `postman/StudentGradeManagement.postman_collection.json`.
3. The collection variables hold `baseUrl = http://localhost:5080` and the demo credentials. To use HTTPS instead, set `baseUrl` to `https://localhost:7080`. You may need to turn off *SSL certificate verification* in Postman settings for the dev certificate.
4. Run the whole collection with the **Collection Runner**, in order. Folder `00` logs in and stores the tokens. Folder `01` finds the grade and student IDs that later folders use. Every request has automatic assertions in its *Tests* tab, and the collection can be re-run because it generates unique subject codes.
5. Take the screenshots listed in `docs/TEST-PLAN.md`. Each one should show the method, URL, the auth (Bearer token of which role), the status and the response body.

| Folder | Covers exam requirement |
|---|---|
| 00 Authentication | logins for faculty and student, wrong password gives 401 |
| 01 Faculty CRUD | Faculty GET / POST (201) / PUT with remark change / duplicate gives 409 / not found gives 404 |
| 02 Validation | grade 105, -1, 85.5, "ninety" and a missing grade all give 400 |
| 03 Client-data tampering | fake `remarks` and `facultyId` are ignored; the server computes the remark |
| 04 Student read access | `/api/student/me`, `/api/student/grades`, own grade by id |
| 05 Student ownership | `?studentId=other` is ignored; another student's grade gives 404; `/api/grades` gives 403 |
| 06 Student write restriction | POST, PUT and DELETE as a student give 403 |
| 07 Authentication | no token or an invalid token gives 401 |
| 08 Faculty DELETE | 204, then 404 |

## 8. How the important parts work (for the defense)

**Authentication flow**

1. The user submits email and password on the MVC login page. The MVC controller sends them, server to server, to `POST /api/auth/login`.
2. The API checks them with ASP.NET Core Identity (`UserManager.CheckPasswordAsync`, which compares hashed passwords). Five failed attempts lock the account for 5 minutes.
3. The API confirms the role mapping: a Faculty login must have an active `FacultyId`, and a Student login an active `StudentId`. It then issues a signed JWT (HMAC-SHA256, 60 minutes) containing `sub` (the user id), `name`, `email` and `role`.
4. The MVC site signs the user in with an encrypted, HttpOnly cookie that stores the JWT. The cookie expires when the token does.
5. The dashboard page gives the token to its JavaScript, which calls the API with `Authorization: Bearer <token>`.

**Authorization (server side)**

* `[Authorize]` on every grade controller means no token or a bad token gets **401**. A custom JSON body explains why.
* `[Authorize(Roles = "Faculty")]` on GET-all, POST, PUT, DELETE and the lookups means a student token gets **403**.
* The MVC `[Authorize(Roles=…)]` attributes only decide which *page* opens. Hiding buttons isn't treated as security.

**Data ownership**

* `GET /api/student/grades` has **no student-id parameter**. `AcademicIdentityService` reads the `sub` claim from the validated token, then loads `AspNetUsers.StudentId` from the database, and the query filters on that value. A caller can add `?studentId=2` and it changes nothing.
* `GET /api/grades/{id}` as a student adds `WHERE StudentId = <my StudentId>`, so another student's record returns **404** and doesn't even reveal that it exists.
* `FacultyId` on a new grade comes from the signed-in faculty account, never from the request body.
* The target record is loaded and checked on every read, update and delete, so a missing record returns 404.

**Validation**

* `GradeUpsertRequest.Grade` is an `int?` with `[Required]` and `[Range(0,100)]`. Out-of-range values (105, -1) and a missing grade return 400.
* Non-integers (85.5, "85", "ninety") can't be converted to `int` by `System.Text.Json`, so the request is rejected with 400. `ApiErrors.InvalidModelStateResponse` rewrites the message as *"Grade must be a whole number from 0 to 100…"*.
* Other checks: subject code format, a semester from the list, and a school year like `2026-2027`. The student must exist and be active. A duplicate (student, subject, semester, school year) returns **409**, backed by the `UQ_StudentGrades_StudentSubjectTerm` constraint.
* The database enforces the same rules again: `CK_StudentGrades_Grade CHECK (Grade BETWEEN 0 AND 100)`, foreign keys and the unique constraint. All SQL is generated by EF Core and parameterized.

**Remark computation**

* `Services/GradeRemarksCalculator.Compute(grade)` maps 95–100 to CMS, 90–94 to SP, 85–89 to SAP, 80–84 to GP, 75–79 to EP and 0–74 to BEP/FAILED. This is the same scale as `dbo.GetGradeRemarks`.
* It runs on **every** POST and **every** PUT, so a new number can never keep an old remark.
* The request DTO has **no `Remarks` property**, so any `remarks` the client sends is thrown away during deserialization.

## 9. Troubleshooting

| Problem | Fix |
|---|---|
| API stops with "Missing table(s): Students…" | Run `database/01_StudentGradeManagementDB.sql` against the same server as the connection string. |
| `SqlException: Cannot open database` or "A network-related error" | Fix `Server=` in the connection string (Express and LocalDB names are in section 2). |
| Login page says "Cannot reach the Grade API" | Start the API first and check `GradeApi:BaseUrl` in the MVC `appsettings.json`. |
| Dashboard shows "Cannot reach the Grade API" or a CORS error in the browser console | Make sure the MVC URL is listed in the API's `Cors:AllowedOrigins`, and run `dotnet dev-certs https --trust`. |
| Requests that worked earlier now return 401 | The API restarted with a random dev key, or the token expired. Sign in again, or set `Jwt:Key` with user-secrets. |
| Using .NET 9 or 10 instead of 8 | Change `<TargetFramework>` in both `.csproj` files to `net9.0` or `net10.0`, and set the `Microsoft.*` package versions to the matching 9.x or 10.x. Then delete the `Migrations` folder and run `dotnet ef migrations add InitialIdentity` again. Newer EF versions check the model snapshot. |

## 10. Project structure

```
StudentGradeManagementSystem.sln
database/
  01_StudentGradeManagementDB.sql      instructor's academic schema and seed data
  02_IdentitySchema_Fallback.sql       optional: Identity tables without EF tools
postman/
  StudentGradeManagement.postman_collection.json
docs/
  TEST-PLAN.md                         test matrix and screenshot checklist
src/StudentGrades.Api/
  Controllers/  AuthController, GradesController, StudentGradesController, LookupsController
  Data/         ApplicationDbContext (Identity + academic tables)
  Infrastructure/ ApiErrors (400/401/403 bodies), DatabaseInitializer (migrate + seed logins)
  Migrations/   InitialIdentity + model snapshot
  Models/       Entities (Student, Faculty, StudentGrade, ApplicationUser), DTOs
  Services/     GradeRemarksCalculator, TokenService, AcademicIdentityService
src/StudentGrades.Mvc/
  Controllers/  AccountController (login/logout), Faculty/Student/Home controllers
  Views/        Account/Login, Faculty/Index, Student/Index, Shared/_Layout
  wwwroot/js/   api-client.js (fetch wrapper), faculty-dashboard.js, student-dashboard.js
```

The pages load Bootstrap 5 from the jsDelivr CDN, so the demo machine needs internet access. Without it the pages still work, just unstyled.
