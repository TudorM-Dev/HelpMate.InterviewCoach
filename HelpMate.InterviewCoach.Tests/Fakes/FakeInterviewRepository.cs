using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Core.Interfaces;

namespace HelpMate.InterviewCoach.Tests.Fakes;

public class FakeInterviewRepository : IInterviewRepository
{
    private readonly List<InterviewSession> _sessions = [];
    private int _nextSessionId = 1;
    private int _nextQuestionId = 1;

    public int SaveChangesCallCount { get; private set; }

    public Task<InterviewSession?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.FirstOrDefault(s => s.Id == sessionId));
    }

    public Task<IReadOnlyList<InterviewSession>> GetSessionsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<InterviewSession> result = _sessions.Where(s => s.UserId == userId).ToList();
        return Task.FromResult(result);
    }

    public Task<int> CountSessionsCreatedSinceAsync(string userId, DateTime since, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.Count(s => s.UserId == userId && s.CreatedAt >= since));
    }

    public Task<IReadOnlyList<InterviewSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<InterviewSession> result = _sessions.ToList();
        return Task.FromResult(result);
    }

    public Task AddSessionAsync(InterviewSession session, CancellationToken cancellationToken = default)
    {
        session.Id = _nextSessionId++;
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task RemoveSessionAsync(InterviewSession session, CancellationToken cancellationToken = default)
    {
        _sessions.Remove(session);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        // Mimic the database assigning identity values on insert
        foreach (var question in _sessions.SelectMany(s => s.Questions).Where(q => q.Id == 0))
        {
            question.Id = _nextQuestionId++;
        }

        return Task.CompletedTask;
    }
}