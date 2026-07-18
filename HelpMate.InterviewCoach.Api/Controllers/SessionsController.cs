using HelpMate.InterviewCoach.Api.Contracts;
using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Core.Interfaces;
using HelpMate.InterviewCoach.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace HelpMate.InterviewCoach.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly InterviewService _interviewService;
    private readonly IAiInterviewer _aiInterviewer;

    public SessionsController(InterviewService interviewService, IAiInterviewer aiInterviewer)
    {
        _interviewService = interviewService;
        _aiInterviewer = aiInterviewer;
    }

    private string CurrentUserId =>
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new InvalidOperationException("Token does not contain a user id.");

    [HttpPost]
    public async Task<IActionResult> CreateSession(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await _interviewService.CreateSessionAsync(
            CurrentUserId, request.TargetRole, cancellationToken);

        return CreatedAtAction(
            nameof(GetSession),
            new { id = session.Id },
            ToSummary(session));
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var sessions = await _interviewService.GetSessionsForUserAsync(CurrentUserId, cancellationToken);

        return Ok(sessions.Select(ToSummary));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSession(int id, CancellationToken cancellationToken)
    {
        var session = await _interviewService.GetOwnedSessionAsync(id, CurrentUserId, cancellationToken);

        return Ok(ToDetail(session));
    }

    [HttpPost("{id:int}/advance")]
    public async Task<IActionResult> Advance(int id, CancellationToken cancellationToken)
    {
        await _aiInterviewer.AdvanceSessionAsync(id, CurrentUserId, cancellationToken);

        var session = await _interviewService.GetOwnedSessionAsync(id, CurrentUserId, cancellationToken);
        return Ok(ToDetail(session));
    }

    [HttpPost("{id:int}/answers")]
    public async Task<IActionResult> SubmitAnswer(
        int id,
        SubmitAnswerRequest request,
        CancellationToken cancellationToken)
    {
        await _interviewService.SubmitAnswerAsync(
            id, CurrentUserId, request.QuestionId, request.Text, cancellationToken);

        var session = await _interviewService.GetOwnedSessionAsync(id, CurrentUserId, cancellationToken);
        return Ok(ToDetail(session));
    }

    private static SessionSummaryResponse ToSummary(InterviewSession session) => new(
        session.Id,
        session.TargetRole,
        session.Status,
        session.CreatedAt,
        session.CompletedAt,
        session.Questions.Count,
        CalculateAverageScore(session));

    private static SessionDetailResponse ToDetail(InterviewSession session) => new(
        session.Id,
        session.TargetRole,
        session.Status,
        session.CreatedAt,
        session.CompletedAt,
        CalculateAverageScore(session),
        session.Questions
            .OrderBy(q => q.Order)
            .Select(q => new QuestionResponse(
                q.Id,
                q.Text,
                q.Order,
                q.Answer is null
                    ? null
                    : new AnswerResponse(
                        q.Answer.Text,
                        q.Answer.SubmittedAt,
                        q.Answer.Score,
                        q.Answer.FeedbackText)))
            .ToList());

    private static double? CalculateAverageScore(InterviewSession session)
    {
        var scores = session.Questions
            .Where(q => q.Answer?.Score is not null)
            .Select(q => q.Answer!.Score!.Value)
            .ToList();

        return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
    }
}