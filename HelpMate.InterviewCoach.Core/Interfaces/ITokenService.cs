namespace HelpMate.InterviewCoach.Core.Interfaces;

public interface ITokenService
{
    string GenerateToken(string userId, string email, IEnumerable<string> roles);
}