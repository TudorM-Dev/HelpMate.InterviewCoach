using HelpMate.InterviewCoach.Core.Entities;
using HelpMate.InterviewCoach.Core.Interfaces;
using HelpMate.InterviewCoach.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace HelpMate.InterviewCoach.Api.Data;

/// <summary>
/// Seeds a read-only showcase account holding finished interviews. Visitors can see exactly
/// what the AI coach produces without spending any tokens, which keeps a public demo free to run.
/// </summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DemoSeeder));

        var email = configuration["Demo:Email"];
        var password = configuration["Demo:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Demo:Email / Demo:Password not configured, skipping demo seeding");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var repository = services.GetRequiredService<IInterviewRepository>();

        var demoUser = await userManager.FindByEmailAsync(email);

        if (demoUser is null)
        {
            demoUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = configuration["Demo:DisplayName"] ?? "Demo Candidate",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(demoUser, password);

            if (!result.Succeeded)
            {
                logger.LogError("Failed to seed demo account: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(demoUser, RoleSeeder.UserRole);
        }

        var existing = await repository.GetSessionsForUserAsync(demoUser.Id);

        if (existing.Count > 0)
        {
            return;
        }

        foreach (var session in BuildSessions(demoUser.Id))
        {
            await repository.AddSessionAsync(session);
        }

        await repository.SaveChangesAsync();
        logger.LogInformation("Seeded demo interview sessions for {Email}", email);
    }

    private static IEnumerable<InterviewSession> BuildSessions(string userId)
    {
        yield return BuildSession(
            userId,
            ".NET Backend Junior",
            daysAgo: 9,
            [
                ("What is dependency injection and why is it useful?",
                 "It means a class gets its dependencies from outside instead of creating them itself. "
                 + "In ASP.NET Core you register services in Program.cs and the framework passes them into constructors. "
                 + "It makes code easier to test because you can pass a fake implementation.",
                 8,
                 "Strong answer. You covered the definition, the framework mechanism and the testing benefit. "
                 + "To go further you could mention service lifetimes (transient, scoped, singleton) and why a "
                 + "singleton depending on a scoped service is a problem."),

                ("What does an index do in a relational database, and when would you avoid adding one?",
                 "An index makes reads faster because the database can find rows without scanning the whole table. "
                 + "I would avoid indexing a column that is rarely filtered on, or a table that is written to constantly, "
                 + "because every INSERT and UPDATE also has to update the index.",
                 9,
                 "Excellent. You gave both sides of the trade-off, which is what the question is really testing. "
                 + "A nice addition would be mentioning that indexes also cost storage, and that a low-cardinality "
                 + "column such as a boolean rarely benefits from one."),

                ("An endpoint in your API has become slow in production. How do you approach it?",
                 "I would look at the logs first and try to reproduce it locally. Then I would check the database "
                 + "queries the endpoint runs, because usually the problem is there.",
                 5,
                 "You have the right instinct to start from evidence rather than guessing, but the answer is thin. "
                 + "Mention measuring before changing anything (timings, APM, or simple stopwatch logging), looking "
                 + "for N+1 query patterns, checking the query plan, and only then considering caching. Also worth "
                 + "saying you would confirm the fix with the same measurement."),

                ("How would you design the REST endpoints for managing interview sessions?",
                 "POST /api/sessions to create one, GET /api/sessions to list mine, GET /api/sessions/{id} for a "
                 + "single one. I would return 201 with a Location header on create, 404 if the session does not "
                 + "exist or is not mine, and 401 if there is no token.",
                 9,
                 "Very good. Resource-oriented routes, correct status codes, and you noticed that 'not mine' should "
                 + "look the same as 'does not exist' so the API does not leak which ids are real. That last point "
                 + "is one many candidates miss."),

                ("What is the difference between a unit test and an integration test?",
                 "A unit test checks one class on its own with fake dependencies, so it is fast. An integration "
                 + "test checks that several parts work together, for example the service plus a real database.",
                 8,
                 "Correct and clearly put. You could strengthen it by saying what each is good at catching: unit "
                 + "tests pin down business rules and run in milliseconds, integration tests catch wiring and "
                 + "mapping mistakes that mocks would hide.")
            ]);

        yield return BuildSession(
            userId,
            "SQL & Databases",
            daysAgo: 3,
            [
                ("Explain the difference between INNER JOIN and LEFT JOIN.",
                 "INNER JOIN returns only rows that match in both tables. LEFT JOIN returns every row from the left "
                 + "table, and NULLs on the right side where there is no match.",
                 9,
                 "Exactly right, and concisely stated. A good follow-up you could volunteer: filtering the right-hand "
                 + "table in the WHERE clause silently turns a LEFT JOIN back into an INNER JOIN, which is a very "
                 + "common bug."),

                ("What is a transaction, and what does ACID mean?",
                 "A transaction groups several statements so they either all succeed or all fail. ACID stands for "
                 + "Atomicity, Consistency, Isolation and Durability.",
                 6,
                 "The transaction part is correct and the acronym is right, but you only expanded the letters "
                 + "without explaining them. Say what each one guarantees in practice, especially Isolation, since "
                 + "that is where real systems differ through isolation levels and where most interview follow-ups go."),

                ("You need to store interview sessions and their questions. How would you model that?",
                 "One table for sessions and one for questions, with a SessionId foreign key on questions. I would "
                 + "index SessionId because I always query questions by session, and cascade delete so questions "
                 + "disappear with their session.",
                 9,
                 "Strong modelling answer. You justified the index with an access pattern rather than adding one by "
                 + "reflex, and cascade delete is the right call for data that cannot exist on its own."),

                ("What is the N+1 query problem?",
                 "It is when you run one query to get a list, and then one more query for each item in that list. "
                 + "So 100 rows means 101 queries. In EF Core you fix it with Include so the data comes back in one go.",
                 9,
                 "Clear explanation with a concrete number and the correct fix for the stack. You could add that "
                 + "lazy loading is the usual way this sneaks in, which is why EF Core leaves it off by default."),

                ("When would you denormalise a schema?",
                 "When reads are slow.",
                 3,
                 "Too short to show understanding. Denormalisation trades write complexity and the risk of "
                 + "inconsistent copies for read speed, so it is justified when a read path is measurably hot and "
                 + "normalised joins have already been tuned. Mention a concrete example, such as storing a "
                 + "precomputed average score, and how you would keep it correct.")
            ]);
    }

    private static InterviewSession BuildSession(
        string userId,
        string targetRole,
        int daysAgo,
        (string Question, string Answer, int Score, string Feedback)[] items)
    {
        var createdAt = DateTime.UtcNow.AddDays(-daysAgo);

        var session = new InterviewSession
        {
            UserId = userId,
            TargetRole = targetRole,
            Status = InterviewSessionStatus.Completed,
            CreatedAt = createdAt,
            CompletedAt = createdAt.AddMinutes(18)
        };

        var order = 1;

        foreach (var (text, answer, score, feedback) in items)
        {
            var askedAt = createdAt.AddMinutes(order * 3);

            session.Questions.Add(new Question
            {
                Text = text,
                Order = order,
                CreatedAt = askedAt,
                Answer = new Answer
                {
                    Text = answer,
                    SubmittedAt = askedAt.AddMinutes(2),
                    Score = score,
                    FeedbackText = feedback
                }
            });

            order++;
        }

        return session;
    }
}
