namespace HelpMate.InterviewCoach.Core.Entities;

public class Answer
{
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public string Text { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public string? FeedbackText { get; set; }
    public int? Score { get; set; }
}
