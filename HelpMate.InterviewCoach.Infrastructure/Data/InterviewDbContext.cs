using HelpMate.InterviewCoach.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpMate.InterviewCoach.Infrastructure.Data;

public class InterviewDbContext : DbContext
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