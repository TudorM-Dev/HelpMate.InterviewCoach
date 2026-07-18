using Microsoft.AspNetCore.Identity;

namespace HelpMate.InterviewCoach.Api.Data;

public static class RoleSeeder
{
    public const string UserRole = "User";
    public const string AdminRole = "Admin";

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { UserRole, AdminRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}