using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HelpMate.InterviewCoach.Infrastructure.Data;

public class EfInterviewRepository : IInterviewRepository
{
    private readonly InterviewDbContext _context;

    public EfInterviewRepository(InterviewDbContext context)
    {
        _context = context;
    }

    public async Task<InterviewSession?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.Sessions
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answer)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<InterviewSession>> GetSessionsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Sessions
            .Where(s => s.UserId == userId)
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answer)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountSessionsCreatedSinceAsync(string userId, DateTime since, CancellationToken cancellationToken = default)
    {
        return await _context.Sessions
            .CountAsync(s => s.UserId == userId && s.CreatedAt >= since, cancellationToken);
    }

    public async Task<IReadOnlyList<InterviewSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sessions
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answer)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task AddSessionAsync(InterviewSession session, CancellationToken cancellationToken = default)
    {
        _context.Sessions.Add(session);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}