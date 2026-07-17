namespace HelpMate.InterviewCoach.Core.Entities;

public class Question
{
    public int Id { get; set; }

    public int SessionId { get; set; }// foreign key
    public InterviewSession Session { get; set; } = null!;// navigation property

    public string Text { get; set; } = null!;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public Answer? Answer { get; set; }
}
