using HelpMate.InterviewCoach.Core.Entities;

namespace HelpMate.InterviewCoach.Core.Interfaces;

public interface IInterviewRepository
{
    Task<InterviewSession?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InterviewSession>> GetSessionsForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> CountSessionsCreatedSinceAsync(string userId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every session in the system, regardless of owner. Administrative use only.
    /// </summary>
    Task<IReadOnlyList<InterviewSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default);

    Task AddSessionAsync(InterviewSession session, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
