using HelpMate.InterviewCoach.Core;
using HelpMate.InterviewCoach.Core.Exceptions;
using HelpMate.InterviewCoach.Core.Services;
using HelpMate.InterviewCoach.Tests.Fakes;

namespace HelpMate.InterviewCoach.Tests;

public class InterviewServiceTests
{
    private const string OwnerId = "user-1";
    private const string OtherUserId = "user-2";

    private readonly FakeInterviewRepository _repository = new();
    private readonly InterviewService _service;

    public InterviewServiceTests()
    {
        _service = new InterviewService(_repository);
    }

    [Fact]
    public async Task SaveQuestionAsync_WhenSessionIsFull_ThrowsRuleViolation()
    {
        var session = await _service.CreateSessionAsync(OwnerId, ".NET Backend Junior");

        for (var i = 0; i < InterviewRules.MaxQuestionsPerSession; i++)
        {
            await _service.SaveQuestionAsync(session.Id, OwnerId, $"Question {i + 1}");
        }

        await Assert.ThrowsAsync<InterviewRuleViolationException>(
            () => _service.SaveQuestionAsync(session.Id, OwnerId, "One question too many"));
    }

    [Fact]
    public async Task GetOwnedSessionAsync_WhenSessionBelongsToAnotherUser_ThrowsSessionNotFound()
    {
        var session = await _service.CreateSessionAsync(OwnerId, ".NET Backend Junior");

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _service.GetOwnedSessionAsync(session.Id, OtherUserId));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task SaveAnswerFeedbackAsync_WhenScoreIsOutOfRange_ThrowsRuleViolation(int score)
    {
        var session = await _service.CreateSessionAsync(OwnerId, "SQL");
        var question = await _service.SaveQuestionAsync(session.Id, OwnerId, "What is an index?");
        await _service.SubmitAnswerAsync(session.Id, OwnerId, question.Id, "It speeds up reads.");

        await Assert.ThrowsAsync<InterviewRuleViolationException>(
            () => _service.SaveAnswerFeedbackAsync(session.Id, OwnerId, question.Id, score, "Feedback"));
    }
}