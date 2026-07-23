using Anthropic;
using Anthropic.Models.Messages;
using HelpMate.InterviewCoach.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HelpMate.InterviewCoach.Infrastructure.Ai;

public class ClaudeInterviewer : InterviewerBase<ClaudeInterviewer.Conversation>
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    public ClaudeInterviewer(
        AnthropicClient client,
        InterviewService interviewService,
        IConfiguration configuration,
        ILogger<ClaudeInterviewer> logger)
        : base(interviewService, logger)
    {
        _client = client;
        _model = configuration["Anthropic:Model"] ?? "claude-sonnet-5";
    }

    public sealed class Conversation
    {
        public required string System { get; init; }
        public List<MessageParam> Messages { get; } = [];
    }

    private static readonly ToolUnion[] Tools =
    [
        new Tool
        {
            Name = InterviewTools.SaveQuestion,
            Description = "Record the next interview question for the candidate. "
                        + "Use this every time you want to ask something new.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["text"] = JsonSerializer.SerializeToElement(
                        new { type = "string", description = "The interview question to ask the candidate." })
                },
                Required = ["text"]
            }
        },
        new Tool
        {
            Name = InterviewTools.SaveAnswerFeedback,
            Description = "Record your evaluation of an answer the candidate has already submitted.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["question_id"] = JsonSerializer.SerializeToElement(
                        new { type = "integer", description = "The id of the question being evaluated." }),
                    ["score"] = JsonSerializer.SerializeToElement(
                        new { type = "integer", description = "Score from 0 to 10 for the candidate's answer." }),
                    ["feedback"] = JsonSerializer.SerializeToElement(
                        new { type = "string", description = "Concrete feedback: what was correct, what was missing." })
                },
                Required = ["question_id", "score", "feedback"]
            }
        },
        new Tool
        {
            Name = InterviewTools.CompleteSession,
            Description = "Close the interview. Use this when every question has been answered "
                        + "and evaluated, or when the question limit has been reached.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>(),
                Required = []
            }
        }
    ];

    protected override Conversation StartConversation(string systemPrompt, string userState)
    {
        var conversation = new Conversation { System = systemPrompt };

        conversation.Messages.Add(new MessageParam
        {
            Role = Role.User,
            Content = userState
        });

        return conversation;
    }

    protected override async Task<IReadOnlyList<AiToolCall>> SendAsync(
        Conversation conversation, CancellationToken cancellationToken)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 4096,
            System = conversation.System,
            Messages = conversation.Messages,
            Tools = Tools
        });

        var assistantContent = new List<ContentBlockParam>();
        var toolCalls = new List<AiToolCall>();

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out TextBlock? text))
            {
                assistantContent.Add(new TextBlockParam { Text = text.Text });
            }
            else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
            {
                assistantContent.Add(new ToolUseBlockParam
                {
                    ID = toolUse.ID,
                    Name = toolUse.Name,
                    Input = toolUse.Input
                });

                toolCalls.Add(new AiToolCall(
                    toolUse.ID,
                    toolUse.Name,
                    JsonSerializer.SerializeToElement(toolUse.Input)));
            }
        }

        conversation.Messages.Add(new MessageParam
        {
            Role = Role.Assistant,
            Content = assistantContent
        });

        return toolCalls;
    }

    protected override void AddToolResult(Conversation conversation, AiToolCall call, string content) =>
        conversation.Messages.Add(new MessageParam
        {
            Role = Role.User,
            Content = new List<ContentBlockParam>
            {
                new ToolResultBlockParam { ToolUseID = call.Id!, Content = content }
            }
        });

    protected override void AddInstruction(Conversation conversation, string content) =>
        conversation.Messages.Add(new MessageParam
        {
            Role = Role.User,
            Content = content
        });
}