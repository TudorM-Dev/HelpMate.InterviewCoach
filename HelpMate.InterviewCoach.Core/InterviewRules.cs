namespace HelpMate.InterviewCoach.Core;

public static class InterviewRules
{
    public const int MaxQuestionsPerSession = 5;

    /// <summary>
    /// Caps how much AI work a single user can trigger per day. Protects the API budget
    /// when the app is publicly reachable.
    /// </summary>
    public const int MaxSessionsPerUserPerDay = 3;

    public const int MinScore = 0;
    public const int MaxScore = 10;
}
