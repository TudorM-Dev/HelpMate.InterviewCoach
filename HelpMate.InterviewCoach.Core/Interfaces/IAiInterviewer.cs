namespace HelpMate.InterviewCoach.Core.Interfaces;

public interface IAiInterviewer
{
    Task AdvanceSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default);
}