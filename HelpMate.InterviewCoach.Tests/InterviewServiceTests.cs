using HelpMate.InterviewCoach.Core;
using HelpMate.InterviewCoach.Core.Entities;
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

    [Fact]
    public async Task CreateSessionAsync_WhenDailyLimitReached_ThrowsRuleViolation()
    {
        for (var i = 0; i < InterviewRules.MaxSessionsPerUserPerDay; i++)
        {
            await _service.CreateSessionAsync(OwnerId, ".NET Backend Junior");
        }

        await Assert.ThrowsAsync<InterviewRuleViolationException>(
            () => _service.CreateSessionAsync(OwnerId, ".NET Backend Junior"));
    }

    [Fact]
    public async Task CreateSessionAsync_DailyLimitIsPerUser_OtherUsersAreNotAffected()
    {
        for (var i = 0; i < InterviewRules.MaxSessionsPerUserPerDay; i++)
        {
            await _service.CreateSessionAsync(OwnerId, ".NET Backend Junior");
        }

        var session = await _service.CreateSessionAsync(OtherUserId, ".NET Backend Junior");

        Assert.Equal(OtherUserId, session.UserId);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WhenQuestionBelongsToAnotherSession_ThrowsRuleViolation()
    {
        var mySession = await _service.CreateSessionAsync(OwnerId, "SQL");
        var otherSession = await _service.CreateSessionAsync(OtherUserId, "SQL");
        var otherQuestion = await _service.SaveQuestionAsync(otherSession.Id, OtherUserId, "Their question");

        await Assert.ThrowsAsync<InterviewRuleViolationException>(
            () => _service.SubmitAnswerAsync(mySession.Id, OwnerId, otherQuestion.Id, "Sneaky answer"));
    }

    [Fact]
    public async Task SubmitAnswerAsync_WhenQuestionAlreadyAnswered_ThrowsRuleViolation()
    {
        var session = await _service.CreateSessionAsync(OwnerId, "SQL");
        var question = await _service.SaveQuestionAsync(session.Id, OwnerId, "What is an index?");
        await _service.SubmitAnswerAsync(session.Id, OwnerId, question.Id, "First answer");

        await Assert.ThrowsAsync<InterviewRuleViolationException>(
            () => _service.SubmitAnswerAsync(session.Id, OwnerId, question.Id, "Second answer"));
    }

    [Fact]
    public async Task CompleteSessionAsync_MarksSessionCompleted()
    {
        var session = await _service.CreateSessionAsync(OwnerId, "SQL");

        var completed = await _service.CompleteSessionAsync(session.Id, OwnerId);

        Assert.Equal(InterviewSessionStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task SaveQuestionAsync_WhenSessionIsCompleted_ThrowsRuleViolation()
    {
        var session = await _service.CreateSessionAsync(OwnerId, "SQL");
        await _service.CompleteSessionAsync(session.Id, OwnerId);

        await Assert.ThrowsAsync<InterviewRuleViolationException>(
            () => _service.SaveQuestionAsync(session.Id, OwnerId, "Too late"));
    }
}
