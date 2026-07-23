using HelpMate.InterviewCoach.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Text.Json;

namespace HelpMate.InterviewCoach.Infrastructure.Ai;

public class OllamaInterviewer : InterviewerBase<List<Message>>
{
    private readonly IOllamaApiClient _client;
    private readonly string _model;

    public OllamaInterviewer(
        IOllamaApiClient client,
        InterviewService interviewService,
        IConfiguration configuration,
        ILogger<OllamaInterviewer> logger)
        : base(interviewService, logger)
    {
        _client = client;
        _model = configuration["Ollama:Model"] ?? "qwen2.5:7b";
    }

    protected override List<Message> StartConversation(string systemPrompt, string userState) =>
    [
        new() { Role = ChatRole.System, Content = systemPrompt },
        new() { Role = ChatRole.User, Content = userState }
    ];

    protected override async Task<IReadOnlyList<AiToolCall>> SendAsync(
        List<Message> conversation, CancellationToken cancellationToken)
    {
        var request = new ChatRequest
        {
            Model = _model,
            Messages = conversation,
            Tools = InterviewTools.All,
            Stream = false
        };

        var builder = new MessageBuilder();

        await foreach (var chunk in _client.ChatAsync(request, cancellationToken))
        {
            if (chunk is not null)
            {
                builder.Append(chunk);
            }
        }

        var reply = builder.ToMessage();
        conversation.Add(reply);

        return reply.ToolCalls?
            .Select(call => new AiToolCall(
                null,
                call.Function?.Name,
                JsonSerializer.SerializeToElement(call.Function?.Arguments)))
            .ToList() ?? [];
    }

    protected override void AddToolResult(List<Message> conversation, AiToolCall call, string content) =>
        conversation.Add(new Message
        {
            Role = ChatRole.Tool,
            ToolName = call.Name,
            Content = content
        });

    protected override void AddInstruction(List<Message> conversation, string content) =>
        conversation.Add(new Message
        {
            Role = ChatRole.User,
            Content = content
        });
}