using Anthropic;
using HelpMate.InterviewCoach.Api.Components;
using HelpMate.InterviewCoach.Api.Data;
using HelpMate.InterviewCoach.Api.Middleware;
using HelpMate.InterviewCoach.Api.Services;
using HelpMate.InterviewCoach.Core.Interfaces;
using HelpMate.InterviewCoach.Core.Services;
using HelpMate.InterviewCoach.Infrastructure.Ai;
using HelpMate.InterviewCoach.Infrastructure.Data;
using HelpMate.InterviewCoach.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OllamaSharp;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<ApiClient>();

builder.Services.AddScoped(sp =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = accessor.HttpContext?.Request;

    var baseAddress = request is not null
        ? $"{request.Scheme}://{request.Host}"
        : "https://localhost:7163";

    var handler = new HttpClientHandler();

    if (sp.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return new HttpClient(handler)
    {
        BaseAddress = new Uri(baseAddress),
        Timeout = TimeSpan.FromMinutes(5)
    };
});

builder.Services.AddHttpContextAccessor();

// --- Persistence ---
builder.Services.AddDbContext<InterviewDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IInterviewRepository, EfInterviewRepository>();
builder.Services.AddScoped<InterviewService>();

// --- Identity ---
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<InterviewDbContext>()
    .AddDefaultTokenProviders();

// --- Authentication (JWT) ---
builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

var aiProvider = builder.Configuration["Ai:Provider"] ?? "Ollama";

if (aiProvider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(_ => new AnthropicClient
    {
        ApiKey = builder.Configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.")
    });

    builder.Services.AddScoped<IAiInterviewer, ClaudeInterviewer>();
}
else
{
    builder.Services.AddSingleton<IOllamaApiClient>(_ =>
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        return new OllamaApiClient(httpClient, builder.Configuration["Ollama:Model"] ?? "qwen2.5:7b");
    });

    builder.Services.AddScoped<IAiInterviewer, OllamaInterviewer>();
}

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InterviewDbContext>();
    await db.Database.MigrateAsync();

    await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);
    await RoleSeeder.SeedAdminAsync(scope.ServiceProvider, app.Configuration);
    await DemoSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();