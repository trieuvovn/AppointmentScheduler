# Scenario A — Technical implementation plan

Backend track: REST API + persistent database, client layer stubbed with an OpenAPI contract,
`.http` request files, and an integration-test harness.

**Architecture:** Clean Architecture layers, organised internally as vertical slices, with the
repository pattern. **No CQRS** — one handler per use case, no mediator, no separate read model.

---

## 1. Stack

| Concern | Choice | Note |
| --- | --- | --- |
| Runtime | .NET 10 | LTS, supported to November 2028 — `net10.0` target framework |
| API | ASP.NET Core Minimal API | One endpoint class per slice |
| ORM | EF Core 10 | Mapping only — does **not** own the schema |
| Schema migrations | DbUp | Versioned `.sql` scripts in a standalone console project |
| Database | SQL Server 2022 | `mcr.microsoft.com/mssql/server:2022-latest` locally |
| Validation | FluentValidation | Shape at the API edge, rules in the domain |
| Logging | Serilog | JSON to console, correlation id enriched |
| Telemetry | OpenTelemetry | ASP.NET Core + EF Core instrumentation |
| API docs | `Microsoft.AspNetCore.OpenApi` + Scalar | |
| Time | `TimeProvider` (BCL) | No custom `IClock` — see 2.7 |
| Tests | xUnit + FluentAssertions + `Testcontainers.MsSql` + NSubstitute + `Microsoft.Extensions.TimeProvider.Testing` | |

### 1.1 Why SQL Server

PostgreSQL was the alternative considered. Its `EXCLUDE USING gist` constraint can declare
"no two appointments for this bay may overlap" as a database constraint, which is a stronger guarantee
than anything SQL Server offers — it holds even against a manual `INSERT`.

SQL Server is chosen anyway, for two reasons:

1. **It matches the stack Keyloop most likely runs.** An automotive retail platform of this heritage is
   a Microsoft shop; a submission that lands in the reviewer's own idiom is worth more than a
   marginally stronger constraint in a database they do not use.
2. **The chosen concurrency guard does not need the Postgres feature.** A version token on the parent
   row (see Stage 7) behaves identically on both engines, so the stronger constraint would have gone
   unused.

**What this costs, and how it is handled.** The SQL Server container starts in roughly 15 seconds and
wants about 2 GB, against PostgreSQL's ~2 seconds. Over a day of iterating on integration tests, that
compounds. Mitigation: one container per xUnit test collection, started once and reused, rather than
one per test class.

Concrete consequences of the choice:

| Area | Choice |
| --- | --- |
| EF provider | `Microsoft.EntityFrameworkCore.SqlServer`, referenced only in `Infrastructure/DependencyInjection.cs` |
| Migration runner | `dbup-sqlserver` |
| Test container | `Testcontainers.MsSql` |
| Timestamps | `datetime2(0)` — stores no offset, so UTC is a convention the code must enforce |
| Concurrency token | A plain `int Version` everywhere — `rowversion` is store-generated, so EF cannot emit the `UPDATE` the guard depends on |

---

## 2. How the three styles compose

| Style | What it governs | Concretely |
| --- | --- | --- |
| Clean Architecture | **Project boundaries** and dependency direction | `Api → Infrastructure → Application → Domain`, enforced by the compiler |
| Vertical slice | **Folder layout inside each project** | `Features/Booking/`, not `Handlers/` + `Services/` |
| Repository pattern | **The persistence seam** | Interface declared in Application, implemented in Infrastructure |

### 2.1 The rule that keeps them honest

The failure mode of "repository + Clean Architecture" is a single `IRepository<T>` god-interface that
grows a method per feature and ends up coupling every slice to every other.

The fix is to let the slice own its interface:

```
Application/Features/Availability/
├── IAvailabilityRepository.cs      ← declared by the slice that needs it
├── AvailabilitySearch.cs
└── GetAvailabilityHandler.cs
```

Interface Segregation applied per feature. A new slice declares the narrow interface it needs;
Infrastructure adds an implementation. No existing slice is touched, and no interface accumulates
methods for callers it will never serve.

### 2.2 The discipline that makes the seam real

**Repository methods return materialised results, never `IQueryable<T>`.**

Handing back `IQueryable` makes the abstraction decorative: the Application layer would still be
writing expressions that only EF Core can translate, and would still break when the provider changes.
Every query is fully expressed inside the implementation and returns `IReadOnlyList<T>` or a domain
type.

This is what buys the payoff in 2.4 — without it, handlers cannot be tested against fakes.

### 2.3 What "no CQRS" removes

| Removed | Why |
| --- | --- |
| MediatR, `ICommandHandler` / `IQueryHandler` | The endpoint calls the handler directly. A mediator adds a hop and hides the call graph. |
| Command and query objects | The handler takes a request record and returns a response record. |
| Separate read and write models | One model serves both; the read shape does not diverge here. |
| `IResourceSelectionStrategy` | The selection policy is one `ORDER BY`. Extract a seam when a second policy exists. |

**Kept:** `IUnitOfWork` — repositories do not commit; the handler decides the transaction boundary.

**Not written at all:** a custom `IClock`. Time-dependent rules need a seam, but .NET has shipped one
since version 8 — see 2.7.

### 2.4 What the extra ceremony buys

Because handlers depend on interfaces rather than `DbContext`, **use-case tests run with fake
repositories and no database**. `AppointmentScheduler.Application.Tests` covers every rejection path — no bay
free, technician unqualified, outside opening hours, service crosses closing — in milliseconds, with
no container. Integration tests are then reserved for the things that genuinely need a real database:
migrations, constraints, index behaviour and concurrency.

That split is the return on the abstraction. It is also worth stating explicitly in the design
document, because "why did you add a repository over an ORM that already has one" is the obvious
interview question.

### 2.5 The cost, stated plainly

One feature now spans three projects: endpoint in Api, handler and interface in Application,
implementation in Infrastructure. Navigating a feature means opening the same-named folder in three
places. That is the price of compiler-enforced boundaries, and it is paid back by 2.4.

### 2.6 DbUp owns the schema; EF Core only maps to it

A database can have exactly one source of truth for its shape. Adding DbUp therefore means **EF Core
migrations are not used at all** — no `Migrations/` folder, no `dotnet ef migrations add`, no
`Database.Migrate()` at start-up. EF Core keeps its `IEntityTypeConfiguration` classes, but their job
shrinks to mapping onto a schema something else created.

Running both would be the worst outcome: two mechanisms writing DDL, each unaware of the other, and a
schema nobody can reason about.

**What this buys**

- **The application never needs DDL rights.** The API's database login can be restricted to
  `SELECT`/`INSERT`/`UPDATE`/`DELETE`, while only the migration job holds `CREATE`/`ALTER`. An
  application that cannot drop a table cannot be made to drop one. This is the strongest argument for
  the split and the one worth stating in the design document.
- **The DDL is exactly what was intended.** Composite foreign keys, filtered indexes with an `IN`
  predicate, check constraints — all written directly rather than coaxed out of a model differ.
- **A DBA can review it.** Plain SQL in source control, not a generated C# migration.
- **One artefact for every environment.** The same console app with the same scripts runs against a
  developer container, CI, and production.

**What it costs, and the guard**

The risk is **drift**: the EF model says a column exists, the scripts never created it, and nothing
notices until a query fails in production.

Two cheap guards close it:

1. **Integration tests run against a DbUp-provisioned database.** The test fixture starts the
   container and runs the migrator, exactly as production does. Any mismatch surfaces as a failing
   query rather than a deployment incident.
2. **A schema smoke test** that touches every `DbSet`, forcing EF to emit a `SELECT` naming every
   mapped column:

```csharp
[Fact]
public async Task Ef_model_matches_the_migrated_schema()
{
    await _db.Appointments.Take(1).ToListAsync();
    await _db.ServiceBays.Take(1).ToListAsync();
    await _db.Technicians.Take(1).ToListAsync();
    // one line per DbSet
}
```

A renamed or missing column fails here in milliseconds, with the column name in the exception.

### 2.7 Time: `TimeProvider`, and as little of it as possible

Two rules compare a requested start against the present: a minimum lead time, and a ninety-day booking
horizon. That makes "now" an input, and an input that cannot be controlled makes tests lie.

**Hardcoding the appointment date is not enough.** Fixing only one side of the comparison produces a
test that changes meaning as the calendar moves:

```csharp
var startsAt = new DateTimeOffset(2027, 1, 5, 9, 0, 0, TimeSpan.Zero);  // 113 days out today
// asserts: beyond the horizon → rejected
```

Ninety days before that date, the same assertion starts failing — and the horizon test that asserted
*rejection* silently becomes a test of *acceptance*. It does not go red; it goes meaningless.

**No custom `IClock`.** .NET has shipped `TimeProvider` in the BCL since version 8, with
`TimeProvider.System` for production and `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`)
for tests. Hand-rolling an equivalent interface adds two files, forfeits `FakeTimeProvider`'s timer
support, and invites the reviewer's obvious question.

**The domain takes `now` as a parameter and knows nothing about providers:**

```csharp
// Domain — a pure function, no dependencies
public static Result CanBeBookedAt(DateTimeOffset startsAt, DateTimeOffset now, BookingHorizon horizon);
```

```csharp
// Application — the only place that knows where "now" comes from
var now = _timeProvider.GetUtcNow();
var check = BookingRules.CanBeBookedAt(request.StartsAt, now, _horizon);
```

The payoff is that the **entire domain test tier needs no fake at all** — both sides of every
comparison are literals passed in. `AppointmentScheduler.Domain` keeps its promise of no project
references and no NuGet.

The seam is therefore needed in exactly two places, one line each: `FakeTimeProvider` injected into the
handler in application tests, and registered in `ApiFactory` for integration tests. Every date in every
test is then a literal.

---

## 3. Project structure

```
AppointmentScheduler/
├── AppointmentScheduler.sln
├── Directory.Build.props              # nullable enable, warnings as errors
├── Directory.Packages.props           # central package versions
├── docker-compose.yml                 # local database only
├── README.md                          # build/run/test + AI collaboration narrative
│
├── docs/
├── requests/appointments.http         # the stubbed client
│
├── src/
│   ├── AppointmentScheduler.Domain/              # no project references, no NuGet
│   │   ├── Appointments/
│   │   │   ├── Appointment.cs         # guarded status transitions
│   │   │   ├── AppointmentStatus.cs
│   │   │   └── BookingError.cs
│   │   ├── Resources/
│   │   │   ├── Dealership.cs
│   │   │   ├── OpeningHours.cs
│   │   │   ├── ServiceBay.cs
│   │   │   ├── Technician.cs
│   │   │   └── Skill.cs
│   │   ├── Catalogue/ServiceType.cs
│   │   ├── Customers/{Customer,Vehicle}.cs
│   │   └── Common/TimeSlot.cs         # the overlap rule lives here
│   │
│   ├── AppointmentScheduler.Application/         → Domain
│   │   ├── Common/
│   │   │   ├── IUnitOfWork.cs
│   │   │   ├── BookingHorizon.cs      # min lead time + max horizon, from config
│   │   │   └── Result.cs
│   │   └── Features/
│   │       ├── Availability/
│   │       │   ├── IAvailabilityRepository.cs
│   │       │   ├── AvailabilitySearch.cs          # shared by two slices — see 3.1
│   │       │   ├── GetAvailabilityRequest.cs
│   │       │   ├── GetAvailabilityResponse.cs
│   │       │   ├── GetAvailabilityValidator.cs
│   │       │   └── GetAvailabilityHandler.cs
│   │       ├── Booking/
│   │       │   ├── IAppointmentRepository.cs
│   │       │   ├── BookAppointmentRequest.cs
│   │       │   ├── BookAppointmentResponse.cs
│   │       │   ├── BookAppointmentValidator.cs
│   │       │   └── BookAppointmentHandler.cs
│   │       └── Cancellation/
│   │           ├── CancelAppointmentRequest.cs
│   │           ├── CancelAppointmentResponse.cs
│   │           └── CancelAppointmentHandler.cs
│   │
│   ├── AppointmentScheduler.Infrastructure/      → Application
│   │   ├── Persistence/
│   │   │   ├── AppointmentDbContext.cs # internal; mapping + the Version++ override
│   │   │   ├── UnitOfWork.cs
│   │   │   ├── Configurations/         # one per entity
│   │   │   ├── OccupyingExtensions.cs  # the single definition of "occupies"
│   │   │   └── Repositories/
│   │   │       ├── AvailabilityRepository.cs
│   │   │       └── AppointmentRepository.cs
│   │   └── DependencyInjection.cs     # the only file naming the provider
│   │
│   ├── AppointmentScheduler.Api/                 → Infrastructure (composition root)
│   │   ├── Features/
│   │   │   ├── Availability/GetAvailabilityEndpoint.cs
│   │   │   ├── Booking/BookAppointmentEndpoint.cs
│   │   │   └── Cancellation/CancelAppointmentEndpoint.cs
│   │   ├── Common/
│   │   │   ├── CorrelationIdMiddleware.cs
│   │   │   ├── ExceptionToProblemDetails.cs
│   │   │   └── Telemetry.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── AppointmentScheduler.DatabaseMigration/   # standalone console app, references nothing
│       ├── Scripts/
│       │   ├── Schema/
│       │   │   ├── 0001_ReferenceTables.sql
│       │   │   ├── 0002_Customers.sql
│       │   │   ├── 0003_Appointments.sql
│       │   │   └── 0004_Indexes.sql
│       │   ├── ReferenceData/
│       │   │   ├── 0100_Skills.sql
│       │   │   └── 0101_ServiceTypes.sql
│       │   └── Demo/                  # dev only, never run in production
│       │       ├── 0200_Dealership.sql
│       │       ├── 0201_CustomersAndVehicles.sql
│       │       └── 0900_LargeAppointmentSet.sql
│       ├── DatabaseMigrator.cs        # public entry point, called by Program and by tests
│       └── Program.cs                 # thin CLI wrapper
│
└── tests/
    ├── AppointmentScheduler.Domain.Tests/            # milliseconds, no container
    │   └── TimeSlotTests.cs
    ├── AppointmentScheduler.Application.Tests/       # fake repositories, no container
    │   └── Features/
    │       ├── BookAppointmentHandlerTests.cs
    │       └── GetAvailabilityHandlerTests.cs
    └── AppointmentScheduler.Api.IntegrationTests/    # Testcontainers, real HTTP
        ├── ApiFactory.cs
        └── Features/
            ├── BookAppointmentTests.cs
            ├── CancelAppointmentTests.cs
            └── ConcurrencyTests.cs
```

Every project's `Features/` folder carries the same slice names, and both test projects mirror them.
Finding everything about booking means opening `Features/Booking/` in four places.

### 3.1 The one piece deliberately shared

`AvailabilitySearch` is used by both `GetAvailabilityHandler` and `BookAppointmentHandler`. Vertical
slicing tolerates duplication, but not here: the two **must** agree, and a search that reports "free"
while booking reports "occupied" is the worst bug this system can have.

It is extracted, and it lives inside `Features/Availability/` next to its users — not in a distant
`Services/` folder. That placement is the point.

### 3.2 The repository interfaces

```csharp
// Application/Features/Availability/IAvailabilityRepository.cs
public interface IAvailabilityRepository
{
    Task<IReadOnlyList<ServiceBay>> FindFreeBaysAsync(
        Guid dealershipId, TimeSlot slot, CancellationToken ct);

    Task<IReadOnlyList<Technician>> FindFreeQualifiedTechniciansAsync(
        Guid dealershipId, Guid serviceTypeId, TimeSlot slot, CancellationToken ct);
}
```

Two methods, both returning materialised domain entities, both taking the domain's own `TimeSlot`.
The overlap predicate and the skill subset test live in the implementation, where EF Core belongs.

```csharp
// Application/Common/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct);
}
```

Two methods. No `BeginTransaction`, no `Commit`, no `Rollback` — and that is a decision, not an
omission.

**Why a delegate rather than `Begin`/`Commit`.** With `EnableRetryOnFailure`, EF Core requires a
user-initiated transaction to be wrapped in `CreateExecutionStrategy().ExecuteAsync(...)`, because a
transient failure means **re-running the whole block**. An explicit `Begin`/`Commit` pair never holds
the caller's code, so there is nothing to re-run: either retries become impossible, or the Application
layer has to learn about execution strategies, which defeats the seam. The delegate form holds the
work, so the implementation can retry it and the Application layer never learns retries exist.

The second reason is ordinary: with `Begin`/`Commit`, an exception between the two leaves a transaction
open and its row locks held until the connection is recycled. One missing `try/finally` is all it
takes. With a delegate, the `using` lives inside the implementation and the caller has no opportunity
to forget. Rollback is expressed by throwing; the implementation reuses an open transaction if one
already exists, so nested calls are safe.

**Why `SaveChangesAsync` does not commit.** The two are different operations, and the booking guard
depends on the difference:

```csharp
await _uow.ExecuteInTransactionAsync(async ct =>
{
    await _appointments.LockResourcesAsync(bayId, techId, ct);  // SaveChanges #1 — UPDATE, takes locks
    if (conflict) return Result.Conflict();

    _appointments.Add(appointment);
    await _uow.SaveChangesAsync(ct);                            // SaveChanges #2 — INSERT
}, ct);                                                          // commit happens here
```

Two saves inside one transaction. `SaveChangesAsync` sends statements; commit makes them durable and
releases the locks. If saving also committed, the lock taken in step one would be released before the
check in step two — and the guard would collapse.

### 3.3 One contract pair per use case

Each slice owns exactly one `Request`/`Response` pair, declared in Application. The API layer binds to
it directly rather than defining a second, near-identical DTO of its own.

This is a deliberate deferral, not an omission. Separate API contracts earn their keep when two API
versions run side by side, when several transports share one use case, or when the wire shape
genuinely diverges from the use-case shape. None of those is true here, and introducing the split
later is mechanical: rename the existing type to `Input`, add a `Request`, add two mapping lines — the
compiler points at every call site. Deferring a cheap future change is the right trade; six nearly
identical records with no behaviour between them is not.

Two rules hold regardless:

- **Never share a pair between use cases.** A field added for booking must not leak into cancellation.
- **Never return a domain entity.** `Appointment` serialised straight to the wire drags navigation
  properties along, risks cycles, and turns every domain refactor into a breaking API change. The pair
  that was collapsed is *API DTO ↔ Application DTO*; the boundary that always stays is *DTO ↔ entity*.

Record this in the design document as a stated assumption so the absence reads as a decision.

### 3.4 The migration project

`AppointmentScheduler.DatabaseMigration` is a standalone console app. It references no other project in the
solution — it is SQL plus DbUp — so it sits outside the Clean Architecture dependency graph entirely
rather than bending it.

**Three script categories, deliberately separated**

| Folder | Runs where | Why separate |
| --- | --- | --- |
| `Schema/` | Every environment | The shape of the database |
| `ReferenceData/` | Every environment | Skills and service types are part of the schema's meaning, not sample data |
| `Demo/` | Developer and CI only | A seeded dealership and 50,000 appointments must never reach production |

DbUp runs scripts in the alphabetical order of their embedded-resource names, so the zero-padded
numeric prefixes are load-bearing. Demo scripts are selected by a filter, not by a separate journal:

```csharp
public static class DatabaseMigrator
{
    public static DatabaseUpgradeResult Run(string connectionString, bool includeDemoData = false)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrator).Assembly,
                name => includeDemoData || !name.Contains(".Demo."))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        return upgrader.PerformUpgrade();
    }
}
```

`Program.cs` stays a thin wrapper over this, and the integration-test fixture calls the same method —
so tests provision their container exactly the way production provisions its database. That shared
entry point is what makes the drift guard in 2.6 meaningful.

**Three DbUp details worth knowing before starting**

- Scripts must be marked `EmbeddedResource` in the `.csproj`, not `Content`. A script that silently
  does not run is the classic first hour lost to DbUp.
- `WithTransactionPerScript()` rolls back a failed script on its own; without it a half-applied script
  leaves the database in a state the journal disagrees with.
- DbUp performs `$variable$` substitution by default. Any script containing a literal `$` needs
  `.WithVariablesDisabled()` or escaping.

**Local flow, which is also the README**

```bash
docker compose up -d
dotnet run --project src/AppointmentScheduler.DatabaseMigration -- --demo
dotnet run --project src/AppointmentScheduler.Api
```

Three commands, no EF tooling required on the reviewer's machine — a better first-run story than
`dotnet ef database update`.

---

## 4. Stages

Each stage ends in a working, committed, demonstrable state.

### Stage 0 — Skeleton (≈1h)

Solution, five source projects (four layered plus `AppointmentScheduler.DatabaseMigration`), three test projects,
references wired in the Clean Architecture direction. The migration project references nothing. `Directory.Build.props` with `<Nullable>enable</Nullable>` and
`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. `docker-compose.yml`. A `/health` endpoint.

Add the architecture guard test now, while it is cheap:

```csharp
typeof(Appointment).Assembly.GetReferencedAssemblies()
    .Should().NotContain(a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore"));
```

**Done when.** `dotnet build` and `dotnet test` are green; `GET /health` returns 200.

---

### Stage 1 — Domain (≈3h)

All entities and value objects. No EF, no HTTP. The centrepiece:

```csharp
public readonly record struct TimeSlot(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Overlaps(TimeSlot other) => Start < other.End && End > other.Start;
    public bool IsWithin(TimeSlot window) => Start >= window.Start && End <= window.End;
}
```

Plus `Appointment` with guarded status transitions and `ServiceType.Duration` driving slot length.

**Problems covered.** 1.1, 2.1, 4.3.

**Tests.** The overlap truth table as a `[Theory]` — entirely before, overlapping the front, contained,
overlapping the tail, enveloping, touching, entirely after. Touching must be `false`; that single
assertion protects against the `>=` mistake.

**Done when.** Truth table passes, illegal transitions throw, suite runs under a second.

> Highest value per hour in the plan. Do not rush it.

---

### Stage 2 — Schema and persistence (≈4h)

Two halves, in this order — the database exists before anything maps to it.

**2a. The migration project.** `DatabaseMigrator` plus the scripts from 3.4.

- `Schema/` — tables, composite foreign keys, filtered indexes with the `IN` predicate, and the
  `CHECK (EndsAtUtc > StartsAtUtc)` constraint. Written by hand, which is the point: each one comes
  out exactly as `data-model.md` specifies rather than as a model differ chooses to express it.
- `ReferenceData/` — skills and service types.
- `Demo/` — one dealership, 3 bays, 4 technicians with differing skills, 10 customers with vehicles.
  Plus `0900_LargeAppointmentSet.sql` generating 50,000 appointments across a year, which is what makes
  the Stage 3 measurement possible — `GENERATE_SERIES` is available in SQL Server 2022.

**Include the `Version` column on `ServiceBay` and `Technician` in `0001_ReferenceTables.sql`**, unused
for now. This removes the schema change from Stage 7 entirely — and with DbUp that matters more than it
did with EF migrations, since a late script has to be written, ordered and re-run on every environment.

**2b. EF mapping.** `AppointmentDbContext`, one `IEntityTypeConfiguration` per entity, `UnitOfWork`, and
`OccupyingExtensions.cs` — the single definition of "this appointment occupies its resources", written
to match the filtered index predicate exactly.

Wire up versioning here rather than in Stage 7. `IVersioned` lives in Domain, `Version` is mapped with
`IsConcurrencyToken()`, and the increment goes in **`AppointmentDbContext`, not `UnitOfWork`**:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    foreach (var entry in ChangeTracker.Entries<IVersioned>()
                          .Where(e => e.State == EntityState.Modified))
    {
        entry.Entity.Version++;
    }
    return await base.SaveChangesAsync(ct);
}
```

A plain `int` protects nothing unless it is bumped on every write. Putting the increment in
`UnitOfWork` would cover the intended path but not the one that matters: a repository inside
Infrastructure holds the `DbContext` and can call `SaveChangesAsync` on it directly, bypassing the
unit of work and silently disabling the concurrency guard. Overriding the context closes that route —
every write path, including a misbehaving repository and the integration tests' own data builder,
goes through the same override.

The principle generalises: **put enforcement at the narrowest point every path must cross**, not at the
point you hope callers will use. It is the same reason a database constraint beats an application check.

Make the context `internal` for the same reason, with one exception for the tests that legitimately
need it:

```csharp
internal sealed class AppointmentDbContext : DbContext
```

```xml
<InternalsVisibleTo Include="AppointmentScheduler.Api.IntegrationTests" />
```

The Application layer already cannot see it — it does not reference Infrastructure — so this closes the
remaining gap and states the intent: the context is an implementation detail, not an API.

No `Migrations/` folder, no `Database.Migrate()` anywhere in the API.

**Problems covered.** 1.4, 2.5, 5.2.

**Tests.**
- The migrator runs clean against an empty container, and **runs twice without error** — DbUp's journal
  makes the second run a no-op, and asserting that is what proves the scripts are safe to redeploy.
- The schema smoke test from 2.6, touching every `DbSet`.
- The composite foreign key rejects a bay and technician from different dealerships.

**Done when.** `docker compose up -d` followed by one `dotnet run` produces a seeded database that the
API can query, and the test suite provisions its own container the same way.

---

### Stage 3 — Availability slice (≈4.5h)

`IAvailabilityRepository` + `AvailabilityRepository` + `AvailabilitySearch` +
`GetAvailabilityHandler` + `GetAvailabilityEndpoint`.

Order inside the search:

1. Load opening hours for that weekday; convert using the dealership time zone
2. Reject a slot that does not fit entirely inside one opening-hours window
3. Ask the repository for free bays — overlap predicate, one index seek
4. Ask for free qualified technicians — overlap predicate plus skill subset test
5. Order candidates by skill count ascending, then by id; take the first of each

**Problems covered.** 1.1, 1.2 (two independent searches, not a cross product), 1.3, 1.5, 2.2, 5.2.

**Tests.**
- *Application, no container:* every rejection path against fake repositories — outside opening hours,
  service runs past closing, straddles a lunch break, no candidate returned.
- *Integration:* the predicate-agreement test — seed a set of appointments, then assert the repository
  returns exactly the bays for which `TimeSlot.Overlaps` is false. The rule exists twice, once as C#
  and once as a LINQ predicate EF translates, and this test is what keeps them in step.

**Measure it.** Run the availability query against the database provisioned with
`0900_LargeAppointmentSet.sql` and record the figure in the README:

> `Availability search: <measured p95> against 50,000 seeded appointments (index seek, N rows returned).`

Thirty minutes of work that converts "performance was considered" into evidence, and substantiates
the decision *not* to add a cache. Capture the query plan too if it is quick — a screenshot of an
index seek is worth a paragraph of prose in the design document.

**Done when.** `GET /api/v1/availability` returns correct windows for a seeded day, and the measured
figure is in the README.

---

### Stage 4 — Booking slice (≈4h)

`IAppointmentRepository`, `BookAppointmentHandler`, `BookAppointmentEndpoint`. The fail-fast pipeline:
shape validation → pure rules → reference data → calendar rules → availability → insert. Returns `201`
with `Location`, or `409` with `ProblemDetails`.

No concurrency guard yet. Keep the write inside `ExecuteInTransactionAsync` so Stage 7 is an edit, not
a rewrite.

**Problems covered.** 1.2, 1.3, 1.4, 2.1, 2.2, 5.3.

**Tests.**
- *Application:* happy path plus one test per rejection reason, all against fakes.
- *Integration:* booking over HTTP lands in the database; the same vehicle cannot occupy two bays.

**Done when.** A booking made over HTTP appears in the database and the slot stops appearing free.

---

### Stage 5 — Cancel slice (≈1.5h)

`CancelAppointmentHandler` + endpoint. Every availability query already goes through `Occupying()`, so
cancellation frees resources with no further change.

Reschedule only if Stage 4 finished early — and if built, remember `AND Id != @appointmentId` in the
availability check, or the appointment blocks itself.

**Problems covered.** 4.1, 4.3.

**Tests.** Book → cancel → rebook the same slot succeeds. This catches a forgotten status filter.

---

### Stage 6 — Cross-cutting (≈2.5h)

`ExceptionToProblemDetails` mapping domain errors to RFC 7807; correlation-id middleware; Serilog JSON
enriched with it; OpenTelemetry traces for ASP.NET Core + EF Core; counters
`bookings_confirmed_total` and `booking_conflicts_total`; `/health/live` and a `/health/ready` that
actually queries the database; command timeouts; OpenAPI document and Scalar UI;
`requests/appointments.http` covering every endpoint.

**Business-named spans.** Default instrumentation gives one span per HTTP request, which every project
that switches OpenTelemetry on will have. Name the spans after the domain instead:

```csharp
using var activity = Telemetry.Source.StartActivity("booking.search_resources");
activity?.SetTag("dealership.id", dealershipId);
activity?.SetTag("service.duration_minutes", serviceType.DurationMinutes);
activity?.SetTag("candidates.bays", freeBays.Count);
activity?.SetTag("candidates.technicians", freeTechnicians.Count);
```

Three spans are enough: `booking.validate`, `booking.search_resources`, `booking.commit`. A trace then
reads *search 3 ms → select 0.1 ms → commit 12 ms* rather than `POST /appointments 15 ms`, which is
the difference between telemetry that is present and telemetry that answers a question.

**Problems covered.** Observability requirement; 5.1 (the enriched 409 reusing `AvailabilitySearch`).

**Done when.** A failed booking emits one structured log line carrying the correlation id, the same id
appears in the response, and a successful booking produces a trace whose spans name the booking steps.

---

### Stage 7 — Concurrency guard (≈1h)

Small, because Stage 2 already added the column, the mapping and the central increment. What is left is
one repository method and one call.

Declare it on the slice's own interface, so the Application layer asks for an outcome rather than a
mechanism:

```csharp
// Application/Features/Booking/IAppointmentRepository.cs
Task LockResourcesAsync(Guid serviceBayId, Guid technicianId, CancellationToken ct);
```

The Infrastructure implementation marks both rows modified **in ascending id order** — so two
transactions can never lock them in opposite directions and deadlock — and saves, which is what
acquires the exclusive row locks.

Then, inside `ExecuteInTransactionAsync`, call it **before** the availability check:

```csharp
await _appointments.LockResourcesAsync(bay.Id, technician.Id, ct);  // second caller blocks here

if (!await _availability.IsStillFreeAsync(bay.Id, technician.Id, slot, ct))
    return Result.Conflict();

_appointments.Add(appointment);
await _uow.SaveChangesAsync(ct);
```

The ordering is the entire mechanism: the second request waits on the `UPDATE`, and by the time it
proceeds, the first request's appointment is committed and visible to its re-check.

**Problems covered.** 3.1.

**Tests.**

```csharp
var results = await Task.WhenAll(
    client.PostAsJsonAsync("/api/v1/appointments", request),
    client.PostAsJsonAsync("/api/v1/appointments", request));

results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
(await CountAppointments()).Should().Be(1);
```

Against a real database via Testcontainers — an in-memory provider has no locking semantics and would
pass while proving nothing.

**Done when.** That test passes reliably across ten consecutive runs.

---

### Stage 8 — Submission (≈3h)

Routinely underestimated; budget for it properly.

`README.md` with build/run/test instructions that work on a clean machine, plus the **AI Collaboration
Narrative**: strategy for directing the AI, how output was verified, how quality was assured. Fold
`docs/` into the System Design Document. Record the video.

**Video shape (5–10 min).** Intro and scenario choice (1m) → architecture walkthrough (2m) → AI
collaboration story (1–2m) → live demo (2m) → learnings (1m).

Lead the demo with the overlap truth table and the concurrency test.

---

## 5. Problem coverage

| ID | Problem | Stage |
| --- | --- | --- |
| 1.1 | Interval overlap | 1, 3 |
| 1.2 | Bay and technician together | 3 |
| 1.3 | Resource selection | 3 |
| 1.4 | Vehicle double-booking | 2, 4 |
| 1.5 | Qualified technician | 3 |
| 2.1 | Derived end time | 1, 4 |
| 2.2 | Opening hours | 3 |
| 2.3 | Shifts and leave | — documented |
| 2.4 | Buffer time | — documented |
| 2.5 | Time zones | 2, 3 |
| 3.1 | Race condition | 7 |
| 3.2 | Idempotency | 7, optional |
| 4.1 | Cancellation frees resources | 2, 5 |
| 4.2 | Reschedule | 5, optional |
| 4.3 | Status transitions | 1, 5 |
| 5.1 | No availability response | 3 (`GET /availability`), 6 (enriched 409) |
| 5.2 | Query performance | 2, 3 |
| 5.3 | Validation | 4 |

---

## 6. Budget

| Stage | Hours | Cumulative |
| --- | --- | --- |
| 0 Skeleton | 1 | 1 |
| 1 Domain | 3 | 4 |
| 2 Schema (DbUp) + persistence | 4 | 8 |
| 3 Availability slice + measurement | 4.5 | 12.5 |
| 4 Booking slice | 4 | 16.5 |
| 5 Cancel slice | 1.5 | 18 |
| 6 Cross-cutting + business spans | 2.5 | 20.5 |
| 7 Concurrency | 1 | 21.5 |
| 8 Submission | 3 | 24.5 |

---

## 7. Cut lines

Drop in this order, top first:

1. Reschedule (Stage 5 optional)
2. Idempotency (Stage 7 optional)
3. Counters, keeping logs and the business-named spans
4. The enriched 409, keeping the bare 409
5. Scalar UI, keeping the raw OpenAPI document

**Never cut:** the Stage 1 overlap tests, the Stage 3 predicate-agreement test, the Stage 3 measured
latency figure, the Stage 5 cancel-then-rebook test, the Stage 7 concurrency test, the README.

The measurement survives the cut list because it is thirty minutes that produces the only hard
evidence in the submission — everything else about performance is an assertion.

---

## 8. Risks

| Risk | Mitigation |
| --- | --- |
| EF model drifts from the DbUp schema | Tests provision via `DatabaseMigrator`, plus the schema smoke test (2.6) |
| A demo script reaches production | Demo scripts live in their own folder and are excluded unless `--demo` is passed (3.4) |
| Repository degrades into a god-interface | One interface per slice, declared by the slice (2.1) |
| The abstraction is decorative | Repositories return materialised lists, never `IQueryable` (2.2) |
| A repository calls `SaveChangesAsync` directly and silently disables the concurrency guard | The increment lives in the `DbContext` override, so no write path can bypass it; the context is `internal` (Stage 2b) |
| The overlap rule drifts between C# and the EF predicate | The Stage 3 predicate-agreement test |
| Stage 7 squeezed out | Column already in the Stage 2 schema; Stage 7 is one property and one handler edit |
| SQL Server container start (~15s) makes the suite feel slow enough to skip | One container per xUnit collection, started once and reused — never one per test class |
| Three-project navigation slows everything down | Identical `Features/` names across projects; open four folders, not four searches |
| README and video left to the last hour | Stage 8 budgeted at three hours, started before the code feels finished |
