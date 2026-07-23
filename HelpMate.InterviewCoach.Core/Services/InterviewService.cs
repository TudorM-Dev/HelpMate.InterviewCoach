using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Core.Exceptions;
using HelpMate.InterviewCoach.Core.Interfaces;

namespace HelpMate.InterviewCoach.Core.Services;

public class InterviewService
{
    private readonly IInterviewRepository _repository;

    public InterviewService(IInterviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<InterviewSession> CreateSessionAsync(
        string userId,
        string targetRole,
        InterviewDifficulty difficulty = InterviewDifficulty.Junior,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = DateTime.UtcNow.Date;
        var sessionsToday = await _repository.CountSessionsCreatedSinceAsync(
            userId, startOfDay, cancellationToken);

        if (sessionsToday >= InterviewRules.MaxSessionsPerUserPerDay)
        {
            throw new InterviewRuleViolationException(
                $"You have reached the limit of {InterviewRules.MaxSessionsPerUserPerDay} interview sessions per day. " +
                "Please come back tomorrow.");
        }

        var session = new InterviewSession
        {
            UserId = userId,
            TargetRole = targetRole,
            Difficulty = difficulty,
            Status = InterviewSessionStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddSessionAsync(session, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return session;
    }

    public async Task<InterviewSession> GetOwnedSessionAsync(
        int sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetSessionAsync(sessionId, cancellationToken);

        if (session is null || session.UserId != userId)
        {
            throw new SessionNotFoundException(sessionId);
        }

        return session;
    }

    public async Task<IReadOnlyList<InterviewSession>> GetSessionsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetSessionsForUserAsync(userId, cancellationToken);
    }

    public async Task<Question> SaveQuestionAsync(
        int sessionId,
        string userId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, cancellationToken);

        if (session.Status != InterviewSessionStatus.InProgress)
        {
            throw new InterviewRuleViolationException(
                $"Session {sessionId} is already completed and cannot accept new questions.");
        }

        if (session.Questions.Count >= InterviewRules.MaxQuestionsPerSession)
        {
            throw new InterviewRuleViolationException(
                $"This session already has the maximum of {InterviewRules.MaxQuestionsPerSession} questions. " +
                "Complete the session instead of asking another one.");
        }

        var question = new Question
        {
            SessionId = session.Id,
            Text = text,
            Order = session.Questions.Count + 1,
            CreatedAt = DateTime.UtcNow
        };

        session.Questions.Add(question);
        await _repository.SaveChangesAsync(cancellationToken);

        return question;
    }

    public async Task<Answer> SubmitAnswerAsync(
        int sessionId,
        string userId,
        int questionId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, cancellationToken);
        var question = FindQuestion(session, questionId);

        if (question.Answer is not null)
        {
            throw new InterviewRuleViolationException(
                $"Question {questionId} has already been answered.");
        }

        var answer = new Answer
        {
            QuestionId = question.Id,
            Text = text,
            SubmittedAt = DateTime.UtcNow
        };

        question.Answer = answer;
        await _repository.SaveChangesAsync(cancellationToken);

        return answer;
    }

    public async Task SaveAnswerFeedbackAsync(
        int sessionId,
        string userId,
        int questionId,
        int score,
        string feedbackText,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, cancellationToken);
        var question = FindQuestion(session, questionId);

        if (question.Answer is null)
        {
            throw new InterviewRuleViolationException(
                $"Question {questionId} has not been answered yet, so it cannot be evaluated.");
        }

        if (score < InterviewRules.MinScore || score > InterviewRules.MaxScore)
        {
            throw new InterviewRuleViolationException(
                $"Score must be between {InterviewRules.MinScore} and {InterviewRules.MaxScore}, but was {score}.");
        }

        question.Answer.Score = score;
        question.Answer.FeedbackText = feedbackText;

        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<InterviewSession> CompleteSessionAsync(
        int sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, cancellationToken);

        if (session.Status == InterviewSessionStatus.Completed)
        {
            throw new InterviewRuleViolationException(
                $"Session {sessionId} is already completed.");
        }

        session.Status = InterviewSessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;

        await _repository.SaveChangesAsync(cancellationToken);

        return session;
    }

    private static Question FindQuestion(InterviewSession session, int questionId)
    {
        return session.Questions.FirstOrDefault(q => q.Id == questionId)
            ?? throw new InterviewRuleViolationException(
                $"Question {questionId} does not belong to session {session.Id}.");
    }
}
