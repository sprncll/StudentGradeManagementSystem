/*
Student Grade Management System - SQL Server setup
This script creates the academic data schema and sample data.
Authentication accounts and role assignments must be implemented with
ASP.NET Core Identity (or another instructor-approved authentication system).
Do NOT store plaintext passwords in these academic tables.
*/

IF DB_ID(N'StudentGradeManagementDB') IS NULL
    CREATE DATABASE StudentGradeManagementDB;
GO

USE StudentGradeManagementDB;
GO

/* 1. Students */
IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Students
    (
        StudentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
        StudentNumber NVARCHAR(30) NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(256) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Students_IsActive DEFAULT (1),
        CONSTRAINT UQ_Students_StudentNumber UNIQUE (StudentNumber),
        CONSTRAINT UQ_Students_Email UNIQUE (Email)
    );
END;
GO

/* 2. Faculty */
IF OBJECT_ID(N'dbo.Faculty', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Faculty
    (
        FacultyId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Faculty PRIMARY KEY,
        FacultyNumber NVARCHAR(30) NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(256) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Faculty_IsActive DEFAULT (1),
        CONSTRAINT UQ_Faculty_FacultyNumber UNIQUE (FacultyNumber),
        CONSTRAINT UQ_Faculty_Email UNIQUE (Email)
    );
END;
GO

/* 3. Student grades */
IF OBJECT_ID(N'dbo.StudentGrades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentGrades
    (
        GradeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentGrades PRIMARY KEY,
        StudentId INT NOT NULL,
        SubjectCode NVARCHAR(30) NOT NULL,
        SubjectName NVARCHAR(200) NOT NULL,
        FacultyId INT NOT NULL,
        Grade INT NOT NULL,
        Remarks NVARCHAR(30) NOT NULL,
        Semester NVARCHAR(50) NOT NULL,
        SchoolYear NVARCHAR(20) NOT NULL,
        DatePosted DATETIME2(0) NOT NULL CONSTRAINT DF_StudentGrades_DatePosted DEFAULT (SYSUTCDATETIME()),
        DateModified DATETIME2(0) NULL,
        CONSTRAINT FK_StudentGrades_Students FOREIGN KEY (StudentId)
            REFERENCES dbo.Students(StudentId),
        CONSTRAINT FK_StudentGrades_Faculty FOREIGN KEY (FacultyId)
            REFERENCES dbo.Faculty(FacultyId),
        CONSTRAINT CK_StudentGrades_Grade CHECK (Grade BETWEEN 0 AND 100),
        CONSTRAINT UQ_StudentGrades_StudentSubjectTerm
            UNIQUE (StudentId, SubjectCode, Semester, SchoolYear)
    );

    CREATE INDEX IX_StudentGrades_StudentId ON dbo.StudentGrades(StudentId);
    CREATE INDEX IX_StudentGrades_FacultyId ON dbo.StudentGrades(FacultyId);
END;
GO

/* 4. Grade-remark helper. The API must call this or implement equivalent C# logic.
   This procedure is NOT automatically invoked by INSERT/UPDATE statements. */
CREATE OR ALTER PROCEDURE dbo.GetGradeRemarks
    @Grade INT,
    @Remarks NVARCHAR(30) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @Grade IS NULL OR @Grade < 0 OR @Grade > 100
        THROW 50001, 'Grade must be a whole number from 0 to 100.', 1;

    SET @Remarks =
        CASE
            WHEN @Grade BETWEEN 95 AND 100 THEN N'CMS'
            WHEN @Grade BETWEEN 90 AND 94 THEN N'SP'
            WHEN @Grade BETWEEN 85 AND 89 THEN N'SAP'
            WHEN @Grade BETWEEN 80 AND 84 THEN N'GP'
            WHEN @Grade BETWEEN 75 AND 79 THEN N'EP'
            WHEN @Grade BETWEEN 0 AND 74 THEN N'BEP/FAILED'
        END;
END;
GO

/* 5. Seed students independently: reruns can fill missing rows. */
IF NOT EXISTS (SELECT 1 FROM dbo.Students WHERE StudentNumber = N'2026-0001')
    INSERT INTO dbo.Students (StudentNumber, FirstName, LastName, Email)
    VALUES (N'2026-0001', N'Juan', N'Dela Cruz', N'juan.delacruz@example.com');

IF NOT EXISTS (SELECT 1 FROM dbo.Students WHERE StudentNumber = N'2026-0002')
    INSERT INTO dbo.Students (StudentNumber, FirstName, LastName, Email)
    VALUES (N'2026-0002', N'Maria', N'Santos', N'maria.santos@example.com');

IF NOT EXISTS (SELECT 1 FROM dbo.Students WHERE StudentNumber = N'2026-0003')
    INSERT INTO dbo.Students (StudentNumber, FirstName, LastName, Email)
    VALUES (N'2026-0003', N'Pedro', N'Reyes', N'pedro.reyes@example.com');
GO

/* 6. Seed faculty independently. */
IF NOT EXISTS (SELECT 1 FROM dbo.Faculty WHERE FacultyNumber = N'FAC-001')
    INSERT INTO dbo.Faculty (FacultyNumber, FirstName, LastName, Email)
    VALUES (N'FAC-001', N'Andrea', N'Garcia', N'andrea.garcia@example.com');

IF NOT EXISTS (SELECT 1 FROM dbo.Faculty WHERE FacultyNumber = N'FAC-002')
    INSERT INTO dbo.Faculty (FacultyNumber, FirstName, LastName, Email)
    VALUES (N'FAC-002', N'Michael', N'Ramos', N'michael.ramos@example.com');
GO

/* 7. Sample grades are inserted only when the table is empty.
   Remarks are computed in this seed INSERT; the API must do the same
   whenever it creates or updates a grade. */
IF NOT EXISTS (SELECT 1 FROM dbo.StudentGrades)
BEGIN
    INSERT INTO dbo.StudentGrades
        (StudentId, SubjectCode, SubjectName, FacultyId, Grade, Remarks, Semester, SchoolYear)
    SELECT s.StudentId, v.SubjectCode, v.SubjectName, f.FacultyId, v.Grade,
           CASE
               WHEN v.Grade BETWEEN 95 AND 100 THEN N'CMS'
               WHEN v.Grade BETWEEN 90 AND 94 THEN N'SP'
               WHEN v.Grade BETWEEN 85 AND 89 THEN N'SAP'
               WHEN v.Grade BETWEEN 80 AND 84 THEN N'GP'
               WHEN v.Grade BETWEEN 75 AND 79 THEN N'EP'
               WHEN v.Grade BETWEEN 0 AND 74 THEN N'BEP/FAILED'
           END,
           N'1st Semester', N'2026-2027'
    FROM (VALUES
        (N'2026-0001', N'WEB101', N'Web Systems and Technologies', 98),
        (N'2026-0001', N'NET101', N'Networking Fundamentals', 92),
        (N'2026-0002', N'WEB101', N'Web Systems and Technologies', 87),
        (N'2026-0002', N'NET101', N'Networking Fundamentals', 82),
        (N'2026-0003', N'WEB101', N'Web Systems and Technologies', 76),
        (N'2026-0003', N'NET101', N'Networking Fundamentals', 68)
    ) AS v(StudentNumber, SubjectCode, SubjectName, Grade)
    INNER JOIN dbo.Students AS s ON s.StudentNumber = v.StudentNumber
    INNER JOIN dbo.Faculty AS f ON f.FacultyNumber = N'FAC-001';
END;
GO

/* 8. Optional test queries */
SELECT * FROM dbo.Students ORDER BY StudentId;
SELECT * FROM dbo.Faculty ORDER BY FacultyId;

SELECT
    g.GradeId,
    s.StudentNumber,
    CONCAT(s.FirstName, N' ', s.LastName) AS StudentName,
    g.SubjectCode,
    g.SubjectName,
    CONCAT(f.FirstName, N' ', f.LastName) AS FacultyName,
    g.Grade,
    g.Remarks,
    g.Semester,
    g.SchoolYear,
    g.DatePosted,
    g.DateModified
FROM dbo.StudentGrades AS g
INNER JOIN dbo.Students AS s ON s.StudentId = g.StudentId
INNER JOIN dbo.Faculty AS f ON f.FacultyId = g.FacultyId
ORDER BY s.StudentNumber, g.SubjectCode;
GO

DECLARE @TestRemarks NVARCHAR(30);
EXEC dbo.GetGradeRemarks @Grade = 95, @Remarks = @TestRemarks OUTPUT;
SELECT @TestRemarks AS RemarksForGrade95; -- Expected: CMS
GO

/*
IMPORTANT APPLICATION IMPLEMENTATION NOTES
1. The MVC page is not a security boundary. Protect API endpoints server-side.
2. Map authenticated Identity accounts to StudentId or FacultyId using a
   custom Identity user property or a separate mapping table and EF migration.
3. For GET /api/student/grades, derive StudentId from the authenticated account;
   do not accept a caller-selected StudentId as the authorization source.
4. Compute Remarks in the API for every create/update and ignore client Remarks.
5. This script is a starter schema, not a migration framework. If an older version
   of these tables already exists, review schema changes and migrate deliberately.
*/
