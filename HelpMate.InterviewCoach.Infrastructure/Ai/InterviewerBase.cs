using HelpMate.InterviewCoach.Core;
using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Core.Exceptions;
using HelpMate.InterviewCoach.Core.Interfaces;
using HelpMate.InterviewCoach.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HelpMate.InterviewCoach.Infrastructure.Ai;

public readonly record struct AiToolCall(string? Id, string? Name, JsonElement Arguments);

public abstract class InterviewerBase<TConversation> : IAiInterviewer
{
    protected const int MaxIterations = 6;

    private readonly InterviewService _interviewService;
    private readonly ILogger _logger;

    protected InterviewerBase(InterviewService interviewService, ILogger logger)
    {
        _interviewService = interviewService;
        _logger = logger;
    }


    protected abstract TConversation StartConversation(string systemPrompt, string userState);

    protected abstract Task<IReadOnlyList<AiToolCall>> SendAsync(
        TConversation conversation, CancellationToken cancellationToken);

    protected abstract void AddToolResult(TConversation conversation, AiToolCall call, string content);

    protected abstract void AddInstruction(TConversation conversation, string content);


    public async Task AdvanceSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var session = await _interviewService.GetOwnedSessionAsync(sessionId, userId, cancellationToken);

        if (session.Status == InterviewSessionStatus.Completed)
        {
            _logger.LogInformation("Session {SessionId} is already completed, nothing to advance", sessionId);
            return;
        }

        if (NextStepFor(session) is NextStep.WaitForCandidate)
        {
            _logger.LogInformation("Session {SessionId} is waiting for the candidate's answer", sessionId);
            return;
        }

        var conversation = StartConversation(BuildSystemPrompt(session), DescribeSessionState(session));

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            _logger.LogInformation("Agent iteration {Iteration} for session {SessionId}", iteration, sessionId);

            var toolCalls = await SendAsync(conversation, cancellationToken);

            _logger.LogInformation("Model requested {Count} tool call(s): {Names}",
                toolCalls.Count,
                string.Join(", ", toolCalls.Select(c => c.Name)));

            if (toolCalls.Count == 0)
            {
                AddInstruction(conversation, "You must act by calling one of the available tools. Do it now.");
                continue;
            }

            foreach (var call in toolCalls)
            {
                var outcome = await ExecuteToolAsync(call, sessionId, userId, cancellationToken);
                AddToolResult(conversation, call, outcome.Content);

                if (outcome.EndsTurn)
                {
                    return;
                }
            }
        }

        _logger.LogWarning("Agent hit the iteration limit for session {SessionId}", sessionId);
    }


    private sealed record ToolOutcome(string Content, bool EndsTurn);

    private static readonly string[] QuestionFocusAreas =
    [
        "a core language or framework concept",
        "data storage: databases, queries or persistence",
        "a practical troubleshooting scenario the candidate might face on the job",
        "API design, architecture or how they would structure code",
        "testing, performance or engineering best practices"
    ];

    private async Task<ToolOutcome> ExecuteToolAsync(
        AiToolCall call, int sessionId, string userId, CancellationToken cancellationToken)
    {
        var name = call.Name;
        var arguments = call.Arguments;

        try
        {
            switch (name)
            {
                case InterviewTools.SaveQuestion:
                    var question = await _interviewService.SaveQuestionAsync(
                        sessionId, userId, GetString(arguments, "text"), cancellationToken);
                    return new ToolOutcome($"Question saved with id {question.Id}.", EndsTurn: true);

                case InterviewTools.SaveAnswerFeedback:
                    await _interviewService.SaveAnswerFeedbackAsync(
                        sessionId,
                        userId,
                        GetInt(arguments, "question_id"),
                        GetInt(arguments, "score"),
                        GetString(arguments, "feedback"),
                        cancellationToken);
                    return new ToolOutcome(
                        await DescribeStepAfterFeedbackAsync(sessionId, userId, cancellationToken),
                        EndsTurn: false);

                case InterviewTools.CompleteSession:
                    await _interviewService.CompleteSessionAsync(sessionId, userId, cancellationToken);
                    return new ToolOutcome("Session completed.", EndsTurn: true);

                default:
                    return new ToolOutcome(
                        $"Unknown tool '{name}'. Use only the tools you were given.", EndsTurn: false);
            }
        }
        catch (InterviewRuleViolationException ex)
        {
            return new ToolOutcome($"Rejected: {ex.Message}", EndsTurn: false);
        }
    }

    private async Task<string> DescribeStepAfterFeedbackAsync(
        int sessionId,
        string userId,
        CancellationToken cancellationToken)
    {
        var session = await _interviewService.GetOwnedSessionAsync(sessionId, userId, cancellationToken);

        return NextStepFor(session) switch
        {
            NextStep.CompleteSession =>
                $"Feedback saved. All {InterviewRules.MaxQuestionsPerSession} questions are done. "
                + $"Now call {InterviewTools.CompleteSession}.",
            NextStep.AskQuestion =>
                $"Feedback saved. Now call {InterviewTools.SaveQuestion} to ask question number "
                + $"{session.Questions.Count + 1}. It must be about "
                + $"{QuestionFocusAreas[session.Questions.Count % QuestionFocusAreas.Length]}, "
                + "on a clearly different topic from every earlier question.",
            _ => "Feedback saved."
        };
    }

    private enum NextStep
    {
        EvaluateAnswer,
        AskQuestion,
        CompleteSession,
        WaitForCandidate
    }

    private static NextStep NextStepFor(InterviewSession session)
    {
        var pending = session.Questions.FirstOrDefault(q => q.Answer is not null && q.Answer.Score is null);
        if (pending is not null)
        {
            return NextStep.EvaluateAnswer;
        }

        if (session.Questions.Any(q => q.Answer is null))
        {
            return NextStep.WaitForCandidate;
        }

        return session.Questions.Count >= InterviewRules.MaxQuestionsPerSession
            ? NextStep.CompleteSession
            : NextStep.AskQuestion;
    }

    private static string GetString(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value)
            ? value.ToString()
            : throw new InterviewRuleViolationException($"Missing required argument '{name}'.");

    private static int GetInt(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value))
        {
            throw new InterviewRuleViolationException($"Missing required argument '{name}'.");
        }

        return value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : int.Parse(value.ToString());
    }

    private static string BuildSystemPrompt(InterviewSession session) =>
        $"""
        You are conducting a technical job interview for the role: {session.TargetRole}.
        Target candidate level: {session.Difficulty}.

        {DifficultyGuidance(session.Difficulty)}

        You act only through tools. Never write a question or feedback as plain text.

        Available actions:
        - {InterviewTools.SaveQuestion}: ask the candidate one new question.
        - {InterviewTools.SaveAnswerFeedback}: record your evaluation of an answer.
        - {InterviewTools.CompleteSession}: close the interview.

        The user message tells you exactly which action to take. Follow it precisely.
        When it names a focus area for the next question, interpret that area at the target
        level above and never go beyond it.

        Quality rules:
        - Ask questions suited to the role and the target level.
        - Never repeat a question that was already asked.
        - Score answers from 0 to 10, relative to the target level: a strong answer for this
          level scores high even if a more senior candidate could go deeper.
        - Feedback must be concrete: say what was correct and what was missing or wrong.
        """;

    private static string DifficultyGuidance(InterviewDifficulty difficulty) => difficulty switch
    {
        InterviewDifficulty.Internship =>
            "The candidate is a student applying for an internship. Ask only fundamentals: what a "
            + "concept is, why it exists, and simple 'which one would you use here' questions. No "
            + "system design, no scaling, no performance tuning, no obscure edge cases. A good answer "
            + "at this level is a clear explanation in plain words, not production experience.",

        InterviewDifficulty.Junior =>
            "The candidate has up to about two years of experience. Ask core concepts and everyday "
            + "practical situations they would actually meet on the job. Light trade-offs are fine; "
            + "avoid architecture and scaling questions.",

        InterviewDifficulty.Mid =>
            "The candidate has several years of experience. Ask applied problem solving, trade-offs "
            + "and design decisions where more than one answer is reasonable.",

        InterviewDifficulty.Senior =>
            "The candidate is senior. Ask about architecture, scaling, failure modes and judgement "
            + "calls, and expect them to justify their trade-offs.",

        _ => string.Empty
    };

    private static string DescribeSessionState(InterviewSession session)
    {
        var state = new StringBuilder();

        if (session.Questions.Count > 0)
        {
            state.AppendLine("Interview so far:");

            foreach (var question in session.Questions.OrderBy(q => q.Order))
            {
                state.AppendLine($"[question id {question.Id}] {question.Text}");

                if (question.Answer is null)
                {
                    state.AppendLine("    not answered yet");
                }
                else
                {
                    state.AppendLine($"    candidate answered: \"{question.Answer.Text}\"");
                    state.AppendLine(question.Answer.Score is null
                        ? "    NOT EVALUATED YET"
                        : $"    already evaluated with score {question.Answer.Score}");
                }
            }

            state.AppendLine();
        }

        switch (NextStepFor(session))
        {
            case NextStep.EvaluateAnswer:
                var pending = session.Questions.First(q => q.Answer is not null && q.Answer.Score is null);
                state.AppendLine($"YOUR TASK: evaluate this one answer, and nothing else.");
                state.AppendLine();
                state.AppendLine($"Question (id {pending.Id}): {pending.Text}");
                state.AppendLine($"Candidate's answer: {pending.Answer!.Text}");
                state.AppendLine();
                state.AppendLine(
                    $"Call {InterviewTools.SaveAnswerFeedback} with question_id={pending.Id}. "
                    + "Judge only whether this answer correctly addresses this question. "
                    + "If the answer is about a different topic than the question, say so and score it low. "
                    + "Do not ask a new question yet.");
                break;

            case NextStep.CompleteSession:
                state.AppendLine(
                    $"YOUR TASK: every question has been answered and evaluated. "
                    + $"Call {InterviewTools.CompleteSession}.");
                break;

            default:
                var number = session.Questions.Count + 1;
                var focus = QuestionFocusAreas[(number - 1) % QuestionFocusAreas.Length];

                state.AppendLine(
                    $"YOUR TASK: ask question number {number} by calling {InterviewTools.SaveQuestion}. "
                    + $"This question must be about {focus}. "
                    + "It must cover a clearly different topic from every question listed above - "
                    + "do not rephrase an earlier question or stay on the same subject.");
                break;
        }

        return state.ToString();
    }
}