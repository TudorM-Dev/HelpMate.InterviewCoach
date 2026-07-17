using HelpMate.InterviewCoach.Core.Interfaces;
using HelpMate.InterviewCoach.Core.Services;
using HelpMate.InterviewCoach.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddOpenApi();


builder.Services.AddDbContext<InterviewDbContext>(options =>
options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IInterviewRepository, EfInterviewRepository>();
builder.Services.AddScoped<InterviewService>();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
