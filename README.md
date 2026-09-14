# AppointmentScheduler

A service-appointment booking API for a dealership network, replacing a manual booking system.

Built to the plan in [docs/technical-plan.md](docs/technical-plan.md) against the data model in
[docs/data-model.md](docs/data-model.md).

## Status

**Stage 1 — Domain.** Entities, value objects and the booking rules that need no database.
The schema and persistence follow in Stage 2.

| Stage | State |
| --- | --- |
| 0 — Skeleton | Done |
| 1 — Domain | Done |
| 2 — Schema and persistence | Next |

## Architecture

Clean Architecture layers, organised internally as vertical slices, with the repository pattern.
No CQRS: one handler per use case, no mediator, no separate read model.

```
Api → Infrastructure → Application → Domain
```

Dependencies point inwards and the compiler enforces it. `AppointmentScheduler.DatabaseMigration`
sits outside that graph entirely — it is SQL plus DbUp, and references no other project.

| Project | Role |
| --- | --- |
| `AppointmentScheduler.Domain` | Entities and value objects. No project or package references at all. |
| `AppointmentScheduler.Application` | Use-case handlers and the repository interfaces they declare. |
| `AppointmentScheduler.Infrastructure` | EF Core mapping and repository implementations. |
| `AppointmentScheduler.Api` | Minimal API endpoints; the composition root. |
| `AppointmentScheduler.DatabaseMigration` | DbUp console app. Owns the schema. |

## The domain

`TimeSlot` is the centrepiece — a half-open interval `[Start, End)` carrying the overlap rule the
whole system depends on:

```csharp
public bool Overlaps(TimeSlot other) => Start < other.End && End > other.Start;
```

Both comparisons are strict, so **touching slots do not overlap** and back-to-back bookings are
legal. That single property is covered by an explicit truth table in `TimeSlotTests` — entirely
before, touching at the front, overlapping the front, contained, identical, overlapping the tail,
enveloping, touching at the tail, entirely after.

The other rules that live here:

| Rule | Where |
| --- | --- |
| End time derives from the service duration at booking time | `Appointment.Book` ← `ServiceType.Duration` |
| Which statuses hold a bay and technician | `Appointment.OccupyingStatuses` |
| Guarded status transitions | `Appointment.CanTransitionTo`, throwing `InvalidStatusTransitionException` |
| A technician must hold *every* required skill | `Technician.IsQualifiedFor` |
| A slot must fit inside that day's opening hours | `Dealership.IsWithinOpeningHours` |
| Local opening hours resolve against the dealership's IANA time zone | `Dealership.OpeningWindowOn` |

`Appointment.OccupyingStatuses` is deliberately the one C# definition of "occupies". The Stage 2
filtered indexes carry the same `WHERE Status IN ('Confirmed', 'InProgress')` predicate, and the
Stage 3 predicate-agreement test is what keeps the two in step.

## Prerequisites

- .NET 10 SDK
- Docker (for the database, from Stage 2 onwards)

## Build and test

```bash
dotnet build
dotnet test
```

No EF tooling is required. The domain suite is 158 tests and runs in about 0.3 s with no container.

## Run

```bash
docker compose up -d                     # SQL Server 2022 on localhost:1433
dotnet run --project src/AppointmentScheduler.Api
```

Then:

```bash
curl http://localhost:5109/health        # {"status":"Healthy"}
```

The database is not yet touched by the API; `docker compose up` becomes load-bearing in Stage 2,
where it is followed by `dotnet run --project src/AppointmentScheduler.DatabaseMigration -- --demo`.

## Conventions worth knowing

- **DbUp owns the schema; EF Core only maps to it.** There is no `Migrations/` folder and no
  `Database.Migrate()` call, by design — a database has one source of truth for its shape.
- **UTC is a convention the code enforces**, not a guarantee the database makes: `datetime2` stores
  no offset. Columns and properties carry a `Utc` suffix so it is hard to forget.
- **`Version` is a plain `int`, not `rowversion`.** The booking guard has to issue a deliberate
  `UPDATE` against an unchanged row to take its lock, and EF Core will not emit one for a
  store-generated column. Entities that need it implement `IVersioned`.
- **Warnings are errors** (`Directory.Build.props`), and package versions are centralised in
  `Directory.Packages.props`.
- **Architecture guard tests** assert the dependency direction that the compiler cannot: that
  Domain stays free of EF Core, of every other project, and of third-party packages, and that
  Application never sees Infrastructure or Api. They live in `ArchitectureTests` in the Domain
  and Application test projects, and read compiled assembly references rather than project files.
- **`global.json` opts into Microsoft.Testing.Platform**, which xunit.v3 requires on the .NET 10 SDK.
- **FluentAssertions is pinned to 7.x**, the last Apache-2.0 release; 8.x requires a paid licence
  for commercial use.
