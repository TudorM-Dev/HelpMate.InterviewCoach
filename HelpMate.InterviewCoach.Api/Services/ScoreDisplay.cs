namespace HelpMate.InterviewCoach.Api.Services;

public static class ScoreDisplay
{
    public static string CssClass(double? score) => score switch
    {
        null => "score",
        >= 8 => "score score-good",
        >= 5 => "score score-mid",
        _ => "score score-low"
    };
}
