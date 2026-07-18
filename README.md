# HelpMate Interview Coach

An AI interview coach. You pick the role you are preparing for, and an AI agent runs a technical
interview: it asks questions one at a time, reads your free-text answers, scores them out of ten
and explains what was missing. Every session is saved so you can see yourself improve.

Built with ASP.NET Core 10, Entity Framework Core, ASP.NET Core Identity with JWT, Blazor, and a
tool-using AI agent.

> **Live demo:** _not deployed yet_
> **Demo account:** `demo@helpmate.local` / `Demo123!` — two finished interviews to browse.

---

## What this project demonstrates

**An AI agent that calls real backend functions, not a chatbot.** The model is given three tools
that map onto real operations — `save_question`, `save_answer_feedback`, `complete_session` — and
it decides when to call them. The tools are the application's own domain operations, so business
rules are enforced on the model exactly as they are on a user: ask for a sixth question in a
five-question interview and the tool call is rejected, with the reason handed back to the model so
it can adapt.

**Authentication and authorisation done properly.** ASP.NET Core Identity issues JWTs; endpoints
are protected by default; every read and write is scoped to the signed-in user; the Admin role
unlocks a separate set of endpoints.

---

## Architecture

Four projects, a monolith, with dependencies pointing inwards.

```
Api             Controllers, Blazor UI, composition root
Infrastructure  EF Core, Identity, the AI agent          →  Core
Core            Entities, business rules, interfaces     →  (nothing)
Tests           Unit tests                               →  Core
```

`Core` references no other project and no infrastructure package. It owns the entities, the
interview rules and the interfaces (`IInterviewRepository`, `IAiInterviewer`, `ITokenService`);
`Infrastructure` implements them. That inversion is what makes the AI provider and the database
swappable, and what lets the business rules be tested with no database, no network and no AI.

A few consequences worth pointing out:

- **`InterviewSession.UserId` is a plain string, not a navigation property to `ApplicationUser`.**
  Identity lives in `Infrastructure`; if the entity referenced it, `Core` would depend on Identity.
  Ownership is enforced by comparing that string, and the whole authentication layer was added
  later without touching `Core`.
- **Configuration uses the Fluent API, not data annotations.** `[Key]` and `[Required]` are EF
  types, and putting them on entities would drag EF into `Core`.
- **The tools the agent calls are `InterviewService` methods.** There is no separate rule engine
  for the AI. The model is subject to the same invariants as a human user.

---

## How a session works

```
POST /api/sessions              create a session for a target role
POST /api/sessions/{id}/advance the agent asks the next question, or scores the last answer
POST /api/sessions/{id}/answers submit an answer
GET  /api/sessions              your history
GET  /api/sessions/{id}         one full transcript
```

`advance` returns nothing from the agent itself — the agent's output *is* its side effects, written
through its tools. The endpoint then reads the session back and returns it, so the database stays
the single source of truth.

Sequencing (evaluate the pending answer, then ask the next question, then finish) is computed in
code rather than left to the model, which small local models get wrong. The model still owns the
parts that need judgement: what to ask, and how good an answer is.

---

## Security decisions

- **The model never supplies identity.** `save_question` takes only the question text. The session
  id and user id are bound by the server from the authenticated request. A prompt injection in a
  user's answer cannot redirect a write to another session, because the parameter does not exist.
- **"Not found" and "not yours" return the same 404.** Distinguishing them would let someone
  enumerate which session ids are real.
- **Failed logins do not say which half was wrong**, for the same reason.
- **Scores from the model are validated server-side.** A model that returns 47 out of 10 is
  rejected.
- **Two caps bound AI spend**: five questions per session and three sessions per user per day.
- **No secret is in `appsettings.json`.** The JWT signing key, the admin password and the demo
  password come from user secrets in development and environment variables in production. Seeding
  is skipped entirely when credentials are not configured, so no environment silently gets a known
  password.

---

## Running it locally

Requires the .NET 10 SDK and [Ollama](https://ollama.com) for the AI agent.

```bash
# 1. AI model
ollama pull qwen2.5:7b

# 2. Secrets (development)
cd HelpMate.InterviewCoach.Api
dotnet user-secrets set "Jwt:Key" "<any long random string>"
dotnet user-secrets set "Admin:Email" "admin@helpmate.local"
dotnet user-secrets set "Admin:Password" "Admin123!"
dotnet user-secrets set "Demo:Email" "demo@helpmate.local"
dotnet user-secrets set "Demo:Password" "Demo123!"
cd ..

# 3. Database
dotnet ef database update \
  --project HelpMate.InterviewCoach.Infrastructure \
  --startup-project HelpMate.InterviewCoach.Api

# 4. Run
dotnet run --project HelpMate.InterviewCoach.Api --launch-profile https
```

Then open <https://localhost:7163>. Roles, the admin account and the demo interviews are seeded on
first start.

```bash
dotnet test    # unit tests for the interview rules
```

---

## Configuration

| Key | Where | Notes |
| --- | --- | --- |
| `Jwt:Key` | secret | Signing key. Anyone holding it can mint valid tokens. |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | Not secret. |
| `Ollama:Endpoint`, `Ollama:Model` | `appsettings.json` | Swap the model without touching code. |
| `Admin:*`, `Demo:*` | secret | Seeding is skipped when unset. |
| `ConnectionStrings:DefaultConnection` | `appsettings.json` | SQLite in development. |

---

## Notes and trade-offs

**SQLite in development, SQL Server as the production target.** Provider-specific code is confined
to one `UseSqlite` call and one package reference.

**The AI provider is swappable.** `IAiInterviewer` is a `Core` interface; `OllamaInterviewer` is one
implementation. A hosted-model implementation is a new file in `Infrastructure` and one line in
`Program.cs` — nothing else changes. Ollama runs locally and free, which is what a small local model
buys: no per-token cost while developing, at the price of shallower feedback than a frontier model.

**Exceptions carry messages written for the model.** A rejected tool call returns its message to the
agent as the tool result, so a business rule becomes feedback that corrects the agent's behaviour.
The cost is that the debugger stops on expected control flow; that is a deliberate trade.

**Repository methods are named after what the application needs**, not a generic `IRepository<T>`.
A generic repository over EF Core adds a layer without adding meaning, and usually leaks `IQueryable`
back out, which defeats the point of the abstraction.

---

## Status

Working: domain rules with unit tests, persistence, register/login with JWT, per-user ownership,
role-based admin, the AI agent, and the Blazor UI.

Next: deployment with a public demo link, and a hosted-model implementation of `IAiInterviewer`
alongside the local one.
