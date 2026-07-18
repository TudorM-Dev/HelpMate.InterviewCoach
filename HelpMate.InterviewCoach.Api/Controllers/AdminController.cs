using HelpMate.InterviewCoach.Api.Contracts;
using HelpMate.InterviewCoach.Api.Data;
using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Core.Interfaces;
using HelpMate.InterviewCoach.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpMate.InterviewCoach.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleSeeder.AdminRole)]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IInterviewRepository _repository;

    public AdminController(UserManager<ApplicationUser> userManager, IInterviewRepository repository)
    {
        _userManager = userManager;
        _repository = repository;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users.ToListAsync(cancellationToken);
        var sessions = await _repository.GetAllSessionsAsync(cancellationToken);

        var sessionCounts = sessions
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<AdminUserResponse>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            result.Add(new AdminUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                roles.ToList(),
                sessionCounts.GetValueOrDefault(user.Id)));
        }

        return Ok(result);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var sessions = await _repository.GetAllSessionsAsync(cancellationToken);

        var emailsById = await _userManager.Users
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var result = sessions.Select(session => new AdminSessionResponse(
            session.Id,
            session.UserId,
            emailsById.GetValueOrDefault(session.UserId),
            session.TargetRole,
            session.Status,
            session.CreatedAt,
            session.CompletedAt,
            session.Questions.Count,
            AverageScore(session)));

        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var sessions = await _repository.GetAllSessionsAsync(cancellationToken);
        var userCount = await _userManager.Users.CountAsync(cancellationToken);

        var scores = sessions
            .SelectMany(s => s.Questions)
            .Where(q => q.Answer?.Score is not null)
            .Select(q => q.Answer!.Score!.Value)
            .ToList();

        return Ok(new AdminStatsResponse(
            userCount,
            sessions.Count,
            sessions.Count(s => s.Status == InterviewSessionStatus.Completed),
            sessions.SelectMany(s => s.Questions).Count(q => q.Answer is not null),
            scores.Count == 0 ? null : Math.Round(scores.Average(), 2)));
    }

    private static double? AverageScore(InterviewSession session)
    {
        var scores = session.Questions
            .Where(q => q.Answer?.Score is not null)
            .Select(q => q.Answer!.Score!.Value)
            .ToList();

        return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
    }
}
