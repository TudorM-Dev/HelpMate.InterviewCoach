using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HelpMate.InterviewCoach.Infrastructure.Data;

public class InterviewDbContext : IdentityDbContext<ApplicationUser>
{
    public InterviewDbContext(DbContextOptions<InterviewDbContext> options)
        : base(options)
    {
    }

    public DbSet<InterviewSession> Sessions => Set<InterviewSession>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InterviewDbContext).Assembly);
    }
}