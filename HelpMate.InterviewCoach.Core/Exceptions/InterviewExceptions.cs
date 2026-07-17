namespace HelpMate.InterviewCoach.Core.Exceptions;

public class SessionNotFoundException : Exception
{
    public SessionNotFoundException(int sessionId)
        : base($"Interview session {sessionId} was not found.")
    {
    }
}

public class InterviewRuleViolationException : Exception
{
    public InterviewRuleViolationException(string message)
        : base(message)
    {
    }
}
