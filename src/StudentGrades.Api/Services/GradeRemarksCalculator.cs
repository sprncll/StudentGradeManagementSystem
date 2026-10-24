namespace StudentGrades.Api.Services;

/// <summary>
/// Server-side remark computation. Same scale as dbo.GetGradeRemarks in the SQL script.
///   95-100 CMS | 90-94 SP | 85-89 SAP | 80-84 GP | 75-79 EP | 0-74 BEP/FAILED
/// Called on every create and every update, so a new numeric grade can never keep an old remark.
/// </summary>
public static class GradeRemarksCalculator
{
    public const int MinGrade = 0;
    public const int MaxGrade = 100;

    public static string Compute(int grade)
    {
        if (grade < MinGrade || grade > MaxGrade)
            throw new ArgumentOutOfRangeException(nameof(grade), grade, "Grade must be a whole number from 0 to 100.");

        return grade switch
        {
            >= 95 => "CMS",
            >= 90 => "SP",
            >= 85 => "SAP",
            >= 80 => "GP",
            >= 75 => "EP",
            _ => "BEP/FAILED"
        };
    }
}
