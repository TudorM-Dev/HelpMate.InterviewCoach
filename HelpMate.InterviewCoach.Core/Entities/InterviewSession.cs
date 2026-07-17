namespace HelpMate.InterviewCoach.Core.Entities;

public class InterviewSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string TargetRole { get; set; } = null!;
    public InterviewSessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
