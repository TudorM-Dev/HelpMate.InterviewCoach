using HelpMate.InterviewCoach.Core.Entities;

namespace HelpMate.InterviewCoach.Api.Contracts;

public record CreateSessionRequest(string TargetRole, InterviewDifficulty Difficulty = InterviewDifficulty.Junior);

public record SubmitAnswerRequest(int QuestionId, string Text);

public record SessionSummaryResponse(
    int Id,
    string TargetRole,
    InterviewDifficulty Difficulty,
    InterviewSessionStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int QuestionCount,
    double? AverageScore);

public record SessionDetailResponse(
    int Id,
    string TargetRole,
    InterviewDifficulty Difficulty,
    InterviewSessionStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    double? AverageScore,
    IReadOnlyList<QuestionResponse> Questions);

public record QuestionResponse(
    int Id,
    string Text,
    int Order,
    AnswerResponse? Answer);

public record AnswerResponse(
    string Text,
    DateTime SubmittedAt,
    int? Score,
    string? FeedbackText);