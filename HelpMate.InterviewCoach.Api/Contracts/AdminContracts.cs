using HelpMate.InterviewCoach.Core.Entities;

namespace HelpMate.InterviewCoach.Api.Contracts;

public record AdminUserResponse(
    string Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    int SessionCount);

public record AdminSessionResponse(
    int Id,
    string UserId,
    string? UserEmail,
    string TargetRole,
    InterviewSessionStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int QuestionCount,
    double? AverageScore);

public record AdminStatsResponse(
    int UserCount,
    int SessionCount,
    int CompletedSessionCount,
    int AnswerCount,
    double? AverageScore);
