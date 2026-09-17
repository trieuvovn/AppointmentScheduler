# AppointmentScheduler

A service-appointment booking API for a dealership network — **Keyloop Technical Assessment,
Scenario A: The Unified Service Scheduler**, backend track.

A service occupies one bay and one qualified technician for its entire duration. This API decides
whether a requested appointment can be honoured, allocates both resources, and persists the booking.

📄 **[System Design Document](docs/system-design.md)** — architecture, data flow, technology
justifications, observability strategy, and how GenAI was used in the design phase.

---

## Quick start

**Prerequisites** depend on which option you pick:

| | Needs Docker | Needs .NET 10 SDK | Needs a local SQL Server |
| --- | --- | --- | --- |
| **A** — everything in Docker | yes | no | no |
| **B** — SQL in Docker, API on host | yes | yes | no |
| **C** — no Docker at all | no | yes | yes |

Option A is the fewest moving parts; option C is the one to use on a machine that already has SQL
Server. (`dotnet test` needs the SDK either way, and Docker for the integration suite — see
[Build and test](#build-and-test).)

### Option A — everything in Docker (one command)

```bash
docker compose up -d --build
```

That starts SQL Server, waits for it to pass its health check, runs the migrator with `--demo` to
completion, and only then starts the API on <http://localhost:5109>. The dependency conditions are in
`docker-compose.yml`, so the ordering is enforced rather than hoped for — the API cannot come up
against an unmigrated database.

First run pulls the SQL Server image and builds both containers, so allow a few minutes. Watch it come
up, then confirm it is ready:

```bash
docker compose logs -f api          # Ctrl-C once it is listening
curl http://localhost:5109/health/ready
```

`/health/ready` actually queries the database, so a 200 means the whole chain is good. Tear down with
`docker compose down`, or `docker compose down -v` to discard the database volume and start clean.

### Option B — SQL Server in Docker, API on the host

Useful for debugging or attaching an IDE. Runs against the same container database.

```bash
docker compose up -d sqlserver
dotnet run --project src/AppointmentScheduler.DatabaseMigration -- --demo \
  -c "Server=localhost;Database=AppointmentScheduler;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True"
dotnet run --project src/AppointmentScheduler.Api
```

The connection string is passed explicitly because `appsettings.Development.json` defaults to
`localhost` with **Windows integrated auth**, which is the right default for a local SQL Server
instance but not for the container, which uses SA credentials. To avoid repeating the flag, set it once
in the environment instead:

```bash
export ConnectionStrings__AppointmentScheduler="Server=localhost;Database=AppointmentScheduler;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True"
```

(PowerShell: `$env:ConnectionStrings__AppointmentScheduler = "…"`.)

### Option C — SQL Server already installed on the machine

No Docker at all. This is the path the checked-in defaults are written for, so it needs no connection
string and no flags:

```bash
dotnet run --project src/AppointmentScheduler.DatabaseMigration -- --demo
dotnet run --project src/AppointmentScheduler.Api
```

Both projects already default to `Server=localhost` with **Windows integrated auth** — the migrator
from its own [appsettings.json](src/AppointmentScheduler.DatabaseMigration/appsettings.json), the API
from [appsettings.Development.json](src/AppointmentScheduler.Api/appsettings.Development.json). The
migrator creates the `AppointmentScheduler` database if it does not exist, via DbUp's
`EnsureDatabase.For.SqlDatabase`, so there is nothing to set up by hand beyond the server itself.

Works with LocalDB, SQL Server Express, or a full local instance. If yours is not on the default
instance, or uses SQL authentication, override the connection string — the migrator takes
`-c` / `--connection-string`, and both projects read the
`ConnectionStrings__AppointmentScheduler` environment variable:

```bash
# named instance
dotnet run --project src/AppointmentScheduler.DatabaseMigration -- --demo \
  -c "Server=localhost\\SQLEXPRESS;Database=AppointmentScheduler;Integrated Security=True;TrustServerCertificate=True"

# LocalDB
dotnet run --project src/AppointmentScheduler.DatabaseMigration -- --demo \
  -c "Server=(localdb)\\MSSQLLocalDB;Database=AppointmentScheduler;Integrated Security=True;TrustServerCertificate=True"
```

Your Windows account needs rights to create a database on that instance. Note that `--demo` is parsed
out of `args` before the configuration binder runs, so it is safe to place it anywhere on the command
line — including immediately before `-c`.

No EF tooling is needed on any of these paths — the migration project creates the database if absent, then
applies schema, reference data, and (only with `--demo`) a seeded dealership. `--demo` must be typed
explicitly, so demo data cannot reach an environment that did not ask for it.

Then:

```bash
# every bookable oil-change start time on a seeded Monday
curl "http://localhost:5109/api/v1/availability?dealershipId=1a0b0000-0000-4000-8000-00000000000d&serviceTypeId=3c2b0d50-0000-4000-8000-000000000001&date=2026-03-02"
```

`requests/appointments.http` carries the full set of calls with the seeded identifiers already filled
in, including the closed-Sunday case and a Saturday EV check that exercises "the service would cross
closing time". Open it in VS Code (REST Client) or Rider and send the requests directly.

### Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/availability` | Bookable start times for a dealership, service type and local date |
| `POST` | `/api/v1/appointments` | Book one of them — **200 OK** with the allocated bay and technician |
| `GET` | `/health/live` | Process is up |
| `GET` | `/health/ready` | Process is up **and** the database answers |
| `GET` | `/scalar/v1` | Scalar API UI (Development only) |
| `GET` | `/openapi/v1.json` | OpenAPI document (Development only) |

Booking a slot that is already taken returns **409 Conflict** as RFC 7807 `ProblemDetails`, carrying the
same `X-Correlation-Id` in both the response header and the JSON body.

**Why booking returns `200`, not `201 Created`.** `201` is meant to be paired with a `Location` header
pointing at the new resource, and there is no `GET /api/v1/appointments/{id}` route to point at —
cancellation and retrieval are outside the three core requirements. A `Location` header aimed at a
`404` is worse than no header at all, so the endpoint returns `200` with the complete appointment in
the body, which gives a client everything `201` plus a follow-up `GET` would have. Adding the route
later turns this into a two-line change in one file. A stated deferral, not an oversight.

**Seeded demo data** — *Keyloop Motors — Reading*, `Europe/London`, open Mon–Fri 08:00–18:00 and
Saturday 09:00–13:00:

| | Id |
| --- | --- |
| Dealership | `1a0b0000-0000-4000-8000-00000000000d` |
| Oil change (30 min) | `3c2b0d50-0000-4000-8000-000000000001` |
| EV battery check (240 min) | `3c2b0d50-0000-4000-8000-000000000007` |
| Vehicle | `7a6f1190-0000-4000-8000-000000000001` |
| Customer | `6f5e1080-0000-4000-8000-000000000001` |

Three bays and four technicians with deliberately different skill sets, so the qualification rules have
something to bite on.

---

## Build and test

**Prerequisites:** .NET 10 SDK. Docker must be **running** for the integration suite — it starts a real
SQL Server container.

```bash
dotnet build
dotnet test
```

| Suite | Tests | Needs Docker | Result |
| --- | --- | --- | --- |
| `AppointmentScheduler.Domain.Tests` | 158 | no | all passed |
| `AppointmentScheduler.Application.Tests` | 45 | no | all passed |
| `AppointmentScheduler.Api.IntegrationTests` | 34 | **yes** | all passed with Docker running |
| **Total** | **237** | | **237 passed, 0 failed** |

203 of the 237 need no database at all, so most of the suite runs without Docker.

To run only those, which need no Docker:

```bash
dotnet test tests/AppointmentScheduler.Domain.Tests
dotnet test tests/AppointmentScheduler.Application.Tests
```

Run them one project at a time. This solution opts into Microsoft.Testing.Platform (see `global.json`),
whose runner does not accept several project paths in one `dotnet test` invocation — passing two exits
with `Zero tests ran` rather than an obvious error.

If Docker is not running, the 34 integration tests fail at fixture start with a `Docker.DotNet`
connection error. That is the environment, not the code — start Docker and re-run.

Integration tests start their own SQL Server through `Testcontainers.MsSql`, or reuse an existing
instance if `APPOINTMENTSCHEDULER_TEST_SQL` holds a connection string — worth setting locally, since it
skips the per-run container start. They provision it by calling `DatabaseMigrator.Run` — the same entry
point production uses — so a drift between the DbUp scripts and the EF model surfaces as a failing test
rather than a deployment incident.

Three tests are worth knowing about:

| Test | What it protects |
| --- | --- |
| `TimeSlotTests` truth table | The overlap rule across all nine interval shapes. Two assertions exist solely to catch `<=` where `<` belongs — a mistake that silently makes back-to-back bookings impossible. |
| `AvailabilityPredicateAgreementTests` | The overlap rule exists twice — once as C#, once as a LINQ predicate EF translates to SQL. This computes the expected result from the domain rule itself rather than a hardcoded list, so the two cannot drift apart. |
| `ArchitectureTests` | Reads compiled assembly references to assert Domain depends on nothing at all, and Application never sees Infrastructure or Api. |

---

## Layout

```
src/
  AppointmentScheduler.Domain            no project or package references at all
  AppointmentScheduler.Application       handlers + the repository interfaces they declare
  AppointmentScheduler.Infrastructure    EF Core mapping, repositories, UnitOfWork
  AppointmentScheduler.Api               Minimal API endpoints; composition root
  AppointmentScheduler.DatabaseMigration DbUp console app — owns the schema, references nothing
tests/
  ...Domain.Tests  ...Application.Tests  ...Api.IntegrationTests
```

Clean Architecture sets the project boundaries (`Api → Infrastructure → Application → Domain`);
vertical slices set the folder layout inside each, so `Features/Booking/` appears in four projects
rather than a feature being scattered across `Controllers/`, `Services/` and `Repositories/`.

No CQRS — one handler per use case, no mediator, no separate read model.

---

## The rule everything rests on

```csharp
public bool Overlaps(TimeSlot other) => Start < other.End && End > other.Start;
```

An appointment is a half-open interval `[Start, End)`. Both comparisons are strict, so **touching slots
do not overlap** — a job ending at 11:00 does not block an 11:00 start.

Everything else in the system is arranged so this rule has exactly one definition. Full reasoning is in
the [System Design Document](docs/system-design.md).

---

## Conventions worth knowing

- **DbUp owns the schema; EF Core only maps to it.** No `Migrations/` folder and no
  `Database.Migrate()` call, by design — a database has one source of truth for its shape, and the API
  holds no DDL rights.
- **UTC is a convention the code enforces**, not a guarantee the database makes: `datetime2` stores no
  offset. Columns and properties carry a `Utc` suffix so it is hard to forget.
- **`Version` is a plain `int`, not `rowversion`.** The booking guard has to issue a deliberate
  `UPDATE` against an unchanged row to take its lock, and EF Core will not emit one for a
  store-generated column. The increment lives in `AppointmentDbContext.SaveChangesAsync`, not in
  `UnitOfWork`, so no write path can bypass it.
- **`Occupying()` is the single definition of "holds a bay and a technician"**, and the filtered indexes
  carry the identical predicate. A query that forgets the helper also misses the index — it gets slow
  before it gets wrong.
- **Warnings are errors** (`Directory.Build.props`); package versions are centralised
  (`Directory.Packages.props`).
- **`global.json` opts into Microsoft.Testing.Platform**, which xunit.v3 requires on the .NET 10 SDK.
- **FluentAssertions is pinned to 7.x**, the last Apache-2.0 release; 8.x requires a paid licence for
  commercial use.

---

## AI Collaboration Narrative

### Strategy: design in writing first, then implement against it

I used the AI as a design partner before it wrote any implementation. The whole architecture was argued
out in markdown — [technical-plan.md](docs/technical-plan.md),
[data-model.md](docs/data-model.md), [design-decisions.md](docs/design-decisions.md),
[test-scenarios.md](docs/test-scenarios.md) — and only then implemented stage by stage against those
documents.

The reason is practical. Reviewing a design document is cheap and disagreement is easy to express in
prose; reviewing generated code you have no mental model for is expensive and degrades into skimming.
By the time any code existed, I already knew what it was supposed to do, so review had something to
measure against.

I directed the architecture rather than accepting the first proposal — it went through three revisions
at my instruction before settling on Clean Architecture boundaries with vertical-slice folders inside
them. The design-phase half of this story is in the
[System Design Document, section 6](docs/system-design.md#6-how-genai-was-used-in-the-design-phase).

Two habits did most of the work:

- **Ask for options, not recommendations.** A recommendation can only be accepted or refused; a
  comparison can be argued with, and arguing with it is where the design actually got decided. That is
  the shape `design-decisions.md` is written in — eighteen problems, each with the alternatives and
  why they lost.
- **Ask for the mechanism, not the conclusion.** Every wrong answer below survived a reading of the
  code and died when asked to explain *how* it works, step by step.

### The loop each stage ran through

`technical-plan.md` splits the build into stages. Each ran the same way, and the stage documents
([stage3.md](docs/stage3.md), [stage4.md](docs/stage4.md), [stage6.md](docs/stage6.md)) are the plans
as written *before* the code, not write-ups after it:

1. **Plan the stage in prose** — files to add, decisions to make, what "done" means, where it might
   overrun budget. `stage6.md` is a fair sample: it records the plan, an explicit scope cut, and the
   ownership question about where `Telemetry.cs` had to live to respect the dependency direction.
2. **Implement against that plan**, not against a fresh prompt — so the AI's context was a document
   I had already reviewed rather than a restatement of the brief.
3. **Read every generated file before committing.** Not skimmed: the point of stage 1 is that by now I
   knew what the file was supposed to do, so reading it was checking a claim, not forming one.
4. **Make the test fail first where the test was the point.** The concurrency and overlap tests were
   checked against a deliberately broken implementation, because a test that has never failed has not
   been shown to test anything.

### Verification: what I rejected during implementation

The useful part of this narrative is not the code the AI produced, but the reasoning I refused.

**A concurrency mechanism that could not work.** The AI recommended SQL Server's `rowversion` as the
concurrency token — the idiomatic choice, and wrong here. The guard depends on deliberately issuing an
`UPDATE` against an otherwise unchanged row to acquire its lock, and EF Core will never emit an
`UPDATE` for a store-generated column because it never writes to one. The schema uses a plain `int` the
application controls. Not a portability preference: it is what makes the mechanism function at all.

**An enforcement point that could be bypassed.** The AI placed the `Version` increment in `UnitOfWork`.
I asked what stops a repository — which holds the `DbContext` — from calling `SaveChangesAsync`
directly. Nothing did, and doing so would have disabled the concurrency guard **silently**: no error,
no failing test, just an unenforced invariant. The increment moved into
`AppointmentDbContext.SaveChangesAsync`, which every write path must cross, and the context became
`internal`.

**A test that would have lied.** The AI's proposed concurrency test fired two parallel bookings at the
demo seed — which has three bays. Both would legitimately have succeeded and the test would have failed
for a reason unrelated to the guard. A race is only observable when the contested resource is unique,
so every concurrency scenario has to build a dealership with exactly one bay and one qualified
technician.

**Scope it wanted to add.** A generic `IRepository<T>`, a `SharedKernel` project, a hand-rolled
`IClock`, and duplicate DTOs at the API layer — all abstraction with a single implementation to justify
it. `TimeProvider` has been in the BCL since .NET 8; the rest were dropped.

The pattern across all four: the wrong answers were **confident and well-formed**. The `rowversion`
suggestion would have compiled and looked correct in review. What caught it was not reading the code —
it was insisting on an explanation of the *mechanism* until the contradiction surfaced. Reviewing
output tells you whether something looks right; reviewing reasoning tells you whether it is.

### Quality assurance

- **All 237 tests pass**, and 203 of them need no container, so most of the suite runs anywhere.
- **Two tests exist purely to stop duplicated logic drifting**: the `TimeSlot` truth table, and
  `AvailabilityPredicateAgreementTests`, which derives its expectation from the domain rule rather than
  from a hardcoded list.
- **Architecture asserted, not assumed** — `ArchitectureTests` read compiled assembly references.
- **Warnings are errors**, package versions are centralised, and every generated file was read before
  being committed.
- **Decisions recorded with their alternatives.** `design-decisions.md` lists, for each of eighteen
  problems, the options considered and why the others lost — so the reasoning can be checked, not just
  the result.
- **The compiler enforces the boundaries the AI was told to respect.** `AppointmentDbContext` is
  `internal` and `Domain` has zero references — so a plausible-looking suggestion that crosses a layer
  fails the build rather than passing review.
- **Claims in this README were re-run, not remembered.** The test counts in the table above came from
  executing each suite while writing this section; the 237 total is what the runner reported.

---

## Scope

| Area | State |
| --- | --- |
| Availability search (`GET /api/v1/availability`) | Built |
| Booking (`POST /api/v1/appointments`) | Built |
| Observability — logs, traces, metrics, health | Built |
| Resource-lock concurrency guard | **Mechanism built.** `LockResourcesAsync` takes exclusive row locks in ascending id order before the pre-commit re-check; optimistic-token behaviour is covered by `PersistenceTests`. The end-to-end parallel-HTTP race test is **not yet written** — see below. |
| Idempotency | Partial — the header is persisted and protected by a filtered unique index; replay does not yet return the original response. |
| Cancellation | **Not built, deliberately.** Outside the three core requirements. `Appointment.Cancel()` and the transition guards exist in the domain, so adding the endpoint is additive. |
| Rescheduling, technician shifts, buffer time, auth | Out of scope, each with a recorded assumption in the design document. |

### Known gap

The concurrency guard is implemented and wired, but the test that proves it end to end — two
simultaneous `POST`s against a dealership with one bay, asserting exactly one `200`, one `409`, and one
row — is not written. It is the only claim in this submission not backed by a test, and it is the first
thing I would add, because the correctness of a lock is the one property you cannot read off the code.

---

## Documentation

| Document | Contents |
| --- | --- |
| [docs/system-design.md](docs/system-design.md) | **System Design Document** — architecture, data flow, technology justifications, observability, GenAI in design |
| [docs/design-decisions.md](docs/design-decisions.md) | Eighteen problems this scenario contains, and for each one the options considered and why the others lost |
| [docs/data-model.md](docs/data-model.md) | Full DDL, the indexes and composite foreign keys, and the reasoning behind each |
| [docs/test-scenarios.md](docs/test-scenarios.md) | The test catalogue by tier, with every scenario traced back to a requirement |
| [docs/technical-plan.md](docs/technical-plan.md) | The staged build plan, project structure and time budget, written before any code |

If you read one, read `design-decisions.md`. The other documents describe what was built;
that one records what was considered and rejected, which is where the design actually happened.
