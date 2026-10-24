using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentGrades.Api.Models;

namespace StudentGrades.Api.Data;

/// <summary>
/// One DbContext for both the academic tables and the Identity tables.
///
/// The academic tables (Students, Faculty, StudentGrades) are created by the instructor's SQL
/// script, so they are marked ExcludeFromMigrations(): EF Core maps them but never tries to
/// create or alter them. The EF migration only creates the ASP.NET Core Identity tables and the
/// foreign keys from AspNetUsers.StudentId / AspNetUsers.FacultyId to those academic tables.
/// All data access goes through EF Core, so every query is parameterized.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Faculty> FacultyMembers => Set<Faculty>();
    public DbSet<StudentGrade> StudentGrades => Set<StudentGrade>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        builder.Entity<Student>(e =>
        {
            e.ToTable("Students", t => t.ExcludeFromMigrations());
            e.HasKey(s => s.StudentId);
            e.Property(s => s.StudentNumber).HasMaxLength(30).IsRequired();
            e.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
            e.Property(s => s.LastName).HasMaxLength(100).IsRequired();
            e.Property(s => s.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(s => s.StudentNumber).IsUnique().HasDatabaseName("UQ_Students_StudentNumber");
            e.HasIndex(s => s.Email).IsUnique().HasDatabaseName("UQ_Students_Email");
        });

        builder.Entity<Faculty>(e =>
        {
            e.ToTable("Faculty", t => t.ExcludeFromMigrations());
            e.HasKey(f => f.FacultyId);
            e.Property(f => f.FacultyNumber).HasMaxLength(30).IsRequired();
            e.Property(f => f.FirstName).HasMaxLength(100).IsRequired();
            e.Property(f => f.LastName).HasMaxLength(100).IsRequired();
            e.Property(f => f.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(f => f.FacultyNumber).IsUnique().HasDatabaseName("UQ_Faculty_FacultyNumber");
            e.HasIndex(f => f.Email).IsUnique().HasDatabaseName("UQ_Faculty_Email");
        });

        builder.Entity<StudentGrade>(e =>
        {
            e.ToTable("StudentGrades", t =>
            {
                t.ExcludeFromMigrations();
                t.HasCheckConstraint("CK_StudentGrades_Grade", "[Grade] BETWEEN 0 AND 100");
            });
            e.HasKey(g => g.GradeId);
            e.Property(g => g.SubjectCode).HasMaxLength(30).IsRequired();
            e.Property(g => g.SubjectName).HasMaxLength(200).IsRequired();
            e.Property(g => g.Remarks).HasMaxLength(30).IsRequired();
            e.Property(g => g.Semester).HasMaxLength(50).IsRequired();
            e.Property(g => g.SchoolYear).HasMaxLength(20).IsRequired();
            e.Property(g => g.DatePosted).HasColumnType("datetime2(0)");
            e.Property(g => g.DateModified).HasColumnType("datetime2(0)");

            // Mirrors UQ_StudentGrades_StudentSubjectTerm: one final grade per student/subject/term.
            e.HasIndex(g => new { g.StudentId, g.SubjectCode, g.Semester, g.SchoolYear })
             .IsUnique()
             .HasDatabaseName("UQ_StudentGrades_StudentSubjectTerm");

            e.HasOne(g => g.Student).WithMany().HasForeignKey(g => g.StudentId)
             .HasConstraintName("FK_StudentGrades_Students").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.Faculty).WithMany().HasForeignKey(g => g.FacultyId)
             .HasConstraintName("FK_StudentGrades_Faculty").OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            // A login may be linked to a student OR a faculty record, never both.
            e.ToTable(t => t.HasCheckConstraint(
                "CK_AspNetUsers_SingleAcademicLink", "[StudentId] IS NULL OR [FacultyId] IS NULL"));

            // One-to-one: each academic record can be linked to at most one login.
            e.HasOne(u => u.Student).WithOne().HasForeignKey<ApplicationUser>(u => u.StudentId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(u => u.Faculty).WithOne().HasForeignKey<ApplicationUser>(u => u.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
