# HelpMate Interview Coach

**An AI agent that runs technical interviews.** Pick the role you are preparing for, and it asks
you questions one at a time, reads your free-text answers, scores them out of ten and explains what
was missing. Every session is saved so you can watch yourself improve.

![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API%20%2B%20Blazor-512BD4)
![EF Core](https://img.shields.io/badge/EF%20Core-SQLite-blue)
![Auth](https://img.shields.io/badge/Auth-Identity%20%2B%20JWT-green)
![Status](https://img.shields.io/badge/status-work%20in%20progress-orange)

---

## 🚧 Status: work in progress

The application runs end to end today — you can register, run a full AI interview, get scored
feedback and browse your history. What is not finished is the public deployment.

| | |
| --- | --- |
| ✅ **Working** | Domain rules with unit tests · EF Core persistence · Identity + JWT · per-user ownership · role-based admin · the AI agent with tool use · Blazor UI |
| 🚧 **In progress** | Public deployment with a live demo link |
| 📋 **Planned** | A hosted-model implementation of `IAiInterviewer` (pending API access), Scalar UI for browsing the API |

The AI agent currently runs against a **local model through [Ollama](https://ollama.com)**, which
is free to run but means the app has to be started locally to try the live interview. Because the
AI provider sits behind an interface, swapping in a hosted model is one new file and one line of
configuration — that is the next piece of work.

**To try it now:** clone it and follow [Running it locally](#running-it-locally). It takes about
five minutes. The demo account is seeded with two finished interviews you can browse immediately.

---

## Screenshots

**Your history, with scores over time**

![Dashboard](docs/dashboard.png)

**A finished interview: question, your answer, the coach's verdict**

![Transcript](docs/transcript.png)

---

## What this project demonstrates

### An AI agent that calls real backend functions

Not a chatbot with a system prompt. The model is given three tools that map onto real operations —
`save_question`, `save_answer_feedback`, `complete_session` — and it decides when to call them.

The tools **are** the application's own domain operations, so the model is subject to the same
rules as a human user. Ask for a sixth question in a five-question interview and the call is
rejected — and the rejection message is handed back to the model, which reads it and closes the
interview instead:

```
Rejected: This session already has the maximum of 5 questions.
          Complete the session instead of asking another one.
```

A business rule becomes feedback that corrects the agent's behaviour. Nothing about the model is
trusted: it cannot exceed the question limit, cannot return a score of 47 out of 10, and cannot
write to a session that is not the caller's.

### Authentication and authorisation done properly

ASP.NET Core Identity issues JWTs. Endpoints are protected by default, every read and write is
scoped to the signed-in user, and the Admin role unlocks a separate set of endpoints. The UI is
just another client of the same public API — it authenticates with the same token an external
consumer would use.

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

Some consequences worth pointing out:

- **`InterviewSession.UserId` is a plain string, not a navigation property to `ApplicationUser`.**
  Identity lives in `Infrastructure`; if the entity referenced it, `Core` would depend on Identity.
  Ownership is enforced by comparing that string — and the entire authentication layer was added
  later without touching `Core` at all.
- **Configuration uses the Fluent API, not data annotations.** `[Key]` and `[Required]` are EF
  types, and putting them on entities would drag EF into `Core`.
- **The agent's tools are `InterviewService` methods.** There is no separate rule engine for the AI.

---

## How a session works

```
POST /api/sessions               create a session for a target role
POST /api/sessions/{id}/advance  the agent asks the next question, or scores the last answer
POST /api/sessions/{id}/answers  submit an answer
GET  /api/sessions               your history
GET  /api/sessions/{id}          one full transcript
```

`advance` returns nothing from the agent itself — the agent's output **is** its side effects,
written through its tools. The endpoint then reads the session back and returns it, so the database
stays the single source of truth.

Sequencing (evaluate the pending answer → ask the next question → finish) is computed in code
rather than left to the model, because small local models get it wrong. The model still owns the
parts that need judgement: what to ask, and how good an answer is.

---

## Security decisions

- **The model never supplies identity.** `save_question` takes only the question text; the session
  id and user id are bound by the server from the authenticated request. A prompt injection in a
  user's answer cannot redirect a write to another session, because the parameter does not exist.
- **"Not found" and "not yours" return the same 404.** Distinguishing them would let someone
  enumerate which session ids are real.
- **Failed logins do not say which half was wrong**, for the same reason.
- **Scores from the model are validated server-side.**
- **Two caps bound AI spend:** five questions per session, three sessions per user per day.
- **No secret is in `appsettings.json`.** The JWT signing key and the seeded account passwords come
  from user secrets in development and environment variables in production. Seeding is skipped
  entirely when credentials are not configured, so no environment silently gets a known password.

---

## Running it locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[Ollama](https://ollama.com) for the AI agent.

```bash
# 1. AI model (about 4.7 GB)
ollama pull qwen2.5:7b

# 2. Secrets
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

Open <https://localhost:7163>. Roles, the admin account and the demo interviews are seeded on first
start.

**Fastest way to see what it does:** click *See a sample interview* on the landing page. It signs
you in as the demo account, which already holds two finished interviews with real questions,
answers and feedback.

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

**The AI provider is swappable.** `IAiInterviewer` is a `Core` interface and `OllamaInterviewer` is
one implementation. Running a small model locally costs nothing while developing, at the price of
shallower feedback than a frontier model would give.

**Exceptions carry messages written for the model.** A rejected tool call returns its message to
the agent as the tool result. The cost is that a debugger stops on expected control flow; that is a
deliberate trade.

**Repository methods are named after what the application needs**, not a generic `IRepository<T>`.
A generic repository over EF Core adds a layer without adding meaning, and usually leaks
`IQueryable` back out, which defeats the point of the abstraction.

---

## Roadmap

- [ ] Deploy with a public demo link
- [ ] Hosted-model implementation of `IAiInterviewer` so the live demo can run interviews
- [ ] Scalar UI for browsing the API in the browser
- [ ] Integration tests against an in-memory database

---

Built as a portfolio project. The two skills it is meant to show are **authentication and
authorisation** and **AI agent integration with tool use** — everything else is there to give those
two somewhere real to live.
