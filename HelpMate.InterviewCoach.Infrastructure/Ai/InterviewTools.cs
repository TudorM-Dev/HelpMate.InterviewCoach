using OllamaSharp.Models.Chat;

namespace HelpMate.InterviewCoach.Infrastructure.Ai;

public static class InterviewTools
{
    public const string SaveQuestion = "save_question";
    public const string SaveAnswerFeedback = "save_answer_feedback";
    public const string CompleteSession = "complete_session";

    public static IReadOnlyList<Tool> All =>
    [
        new Tool
        {
            Function = new Function
            {
                Name = SaveQuestion,
                Description = "Record the next interview question for the candidate. "
                            + "Use this every time you want to ask something new.",
                Parameters = new Parameters
                {
                    Properties = new Dictionary<string, Property>
                    {
                        ["text"] = new()
                        {
                            Type = "string",
                            Description = "The interview question to ask the candidate."
                        }
                    },
                    Required = ["text"]
                }
            }
        },
        new Tool
        {
            Function = new Function
            {
                Name = SaveAnswerFeedback,
                Description = "Record your evaluation of an answer the candidate has already submitted. "
                            + "Only use this for questions that have an answer.",
                Parameters = new Parameters
                {
                    Properties = new Dictionary<string, Property>
                    {
                        ["question_id"] = new()
                        {
                            Type = "integer",
                            Description = "The id of the question being evaluated."
                        },
                        ["score"] = new()
                        {
                            Type = "integer",
                            Description = "Score from 0 to 10 for the candidate's answer."
                        },
                        ["feedback"] = new()
                        {
                            Type = "string",
                            Description = "Concrete feedback: what was correct, what was missing."
                        }
                    },
                    Required = ["question_id", "score", "feedback"]
                }
            }
        },
        new Tool
        {
            Function = new Function
            {
                Name = CompleteSession,
                Description = "Close the interview. Use this when every question has been answered "
                            + "and evaluated, or when the question limit has been reached.",
                Parameters = new Parameters
                {
                    Properties = new Dictionary<string, Property>(),
                    Required = []
                }
            }
        }
    ];
}