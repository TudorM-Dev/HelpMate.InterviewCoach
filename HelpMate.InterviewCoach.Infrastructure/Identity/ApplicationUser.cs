using Microsoft.AspNetCore.Identity;

namespace HelpMate.InterviewCoach.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = null!;
}