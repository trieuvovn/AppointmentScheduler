# Scenario A — Test scenarios

The catalogue of what gets tested, at which tier, and why. Scenario ids are stable and referenced from
commit messages and from the video walkthrough.

---

## 1. Three tiers, three jobs

| Tier | Project | Dependencies | Runtime | What it proves |
| --- | --- | --- | --- | --- |
| **Domain** | `AppointmentScheduler.Domain.Tests` | none | < 1s | The rules are correct in isolation |
| **Application** | `AppointmentScheduler.Application.Tests` | fake repositories | < 2s | The handlers orchestrate and reject correctly |
| **Integration** | `AppointmentScheduler.Api.IntegrationTests` | Testcontainers + real HTTP | ~30s | The database, the mapping and the concurrency guard behave as designed |

The split exists because of the repository seam: handlers depend on interfaces, so every rejection path
can be proven without a database. Integration tests are then reserved for what genuinely needs one —
migrations, constraints, index behaviour, locking.

**An in-memory EF provider is never used.** It has no locking semantics and no constraint enforcement,
so a concurrency or constraint test would pass against it while proving nothing.

---

## 2. Test data

Two mechanisms, deliberately separate, because they serve different audiences.

| | Purpose | Shape |
| --- | --- | --- |
| **`Demo/` seed scripts** | Humans — Scalar, `appointments.http`, the video | Fixed GUIDs, one dealership, 3 bays, 4 technicians, 5 service types, 10 customers |
| **Test data builder** | Automated tests | Each test builds its own dealership with exactly the resources that test needs |

Tests never rely on the demo seed. Each builds an isolated dealership, so test order cannot affect
results and two tests cannot contend for the same bay.

```csharp
var ctx = await Given.ADealership()
                     .OpenFrom("08:00").To("17:00")
                     .WithBays(1)
                     .WithTechnician(skills: ["BRAKES"])
                     .WithServiceType("BRAKE_PADS", minutes: 60, requires: ["BRAKES"])
                     .WithVehicle()
                     .BuildAsync();
```

Customers and vehicles are inserted directly through the `DbContext`. That is why no customer- or
vehicle-registration endpoint is needed: nothing, human or automated, has to create one over HTTP.

### 2.1 The trap that makes the concurrency test lie

> The demo seed has **3 bays and 4 technicians**. Fire two identical bookings at it with
> `Task.WhenAll` and **both succeed** — the second request is legitimately assigned bay 2.
>
> The test fails, and it looks like the concurrency guard is broken when in fact the scenario was
> wrong.

A race can only be observed when the contested resource is unique. Every concurrency scenario therefore
builds a dealership with **exactly one bay and exactly one qualified technician**.

### 2.2 Time is pinned, on both sides

**Rule: every date in every test is a literal. No test computes a date from `DateTimeOffset.UtcNow`.**

Two distinct failure modes make this non-negotiable, and fixing only one of them is worse than fixing
neither, because the result still looks green.

**Relative dates drift with the weekday.**

```csharp
var startsAt = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(9);   // "09:00 tomorrow"
```

The dealership opens Monday to Saturday. Run the suite on a Saturday and "tomorrow" is a Sunday, the
booking is correctly rejected, and the test goes red for a reason that has nothing to do with the code.
Green six days a week. The same trick with `AddHours(2)` fails only in the afternoon, when the derived
end time crosses closing.

**Hardcoding only the appointment date is a time bomb.** The rules compare `startsAt` against *now*, so
pinning one side leaves the comparison moving:

```csharp
var startsAt = new DateTimeOffset(2027, 1, 5, 9, 0, 0, TimeSpan.Zero);   // 113 days out today
// A-16 asserts: beyond the ninety-day horizon → rejected
```

Ninety days before that date, the request falls *inside* the horizon and is accepted. A-16 does not go
red — it quietly stops testing what it claims to test.

**So both sides are pinned.** The suite fixes the present at a single instant:

```csharp
public static readonly DateTimeOffset TestNow = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
```

| Tier | How "now" is supplied | Cost |
| --- | --- | --- |
| Domain | Not supplied at all — rules take `now` as a parameter, so tests pass two literals | nothing |
| Application | `new FakeTimeProvider(TestNow)` into the handler | one line |
| Integration | `FakeTimeProvider` registered in `ApiFactory` | one line |

`TimeProvider` is the BCL abstraction; `FakeTimeProvider` ships in
`Microsoft.Extensions.TimeProvider.Testing`. No custom clock interface exists in this solution.

The consequence is that **all 25 domain scenarios need no fake of any kind**, and the remaining tiers
need two lines between them. Every scenario below then behaves identically whether it runs today, on a
Saturday, or next year.

---

## 3. Naming

```
Method_Scenario_ExpectedOutcome
```

```csharp
Overlaps_WhenSlotsTouchAtTheBoundary_ReturnsFalse
Book_WhenNoQualifiedTechnicianIsFree_ReturnsConflict
Book_WhenTwoRequestsRaceForTheLastBay_CreatesExactlyOneAppointment
```

---

## 4. Domain tier

### 4.1 Interval overlap — the truth table

Reference interval **A = 09:00–11:00**. `[start, end)` is half-open.

| Id | B | Shape | Overlaps? |
| --- | --- | --- | --- |
| D-01 | 07:00–08:00 | entirely before | ❌ |
| D-02 | 08:00–09:00 | touches A's start | ❌ |
| D-03 | 08:30–09:30 | overlaps the front | ✅ |
| D-04 | 09:30–10:30 | contained | ✅ |
| D-05 | 10:30–11:30 | overlaps the tail | ✅ |
| D-06 | 08:30–11:30 | envelops | ✅ |
| D-07 | 09:00–11:00 | identical | ✅ |
| D-08 | 11:00–12:00 | touches A's end | ❌ |
| D-09 | 13:00–14:00 | entirely after | ❌ |

**D-02 and D-08 are the ones that matter.** They are the only assertions that fail if `<` and `>`
are written as `<=` and `>=`, and that mistake silently fragments the calendar — a job ending at 11:00
would block the 11:00 slot forever.

### 4.2 TimeSlot construction

| Id | Scenario | Expected |
| --- | --- | --- |
| D-10 | End before start | throws |
| D-11 | End equal to start | throws — a zero-length appointment is meaningless |

### 4.3 Fitting inside opening hours

| Id | Scenario | Expected |
| --- | --- | --- |
| D-12 | Slot entirely inside the window | ✅ within |
| D-13 | Slot exactly equal to the window | ✅ within |
| D-14 | Slot starts before the window opens | ❌ |
| D-15 | Slot ends after the window closes | ❌ |
| D-16 | Slot spans a lunch break, covered by neither window alone | ❌ |

D-16 encodes the rule that an appointment must fit inside **one** opening-hours row, not the union of
several.

### 4.4 Derived end time

| Id | Scenario | Expected |
| --- | --- | --- |
| D-17 | 60-minute service starting 09:00 | ends 10:00 |
| D-18 | Service duration of zero or negative | rejected at construction |

### 4.5 Status transitions

| Id | From → To | Expected |
| --- | --- | --- |
| D-19 | Confirmed → InProgress → Completed | allowed |
| D-20 | Confirmed → Cancelled | allowed |
| D-21 | Confirmed → NoShow | allowed |
| D-22 | Completed → Cancelled | throws |
| D-23 | Cancelled → InProgress | throws |
| D-24 | Cancelled → Cancelled | throws |
| D-25 | Only `Confirmed` and `InProgress` report as occupying resources | asserted directly |

D-25 is the definition every availability query depends on, so it is pinned in the domain tier rather
than inferred from query behaviour.

---

## 5. Application tier

Fake repositories via NSubstitute. No database, no HTTP.

### 5.1 Booking — the happy path

| Id | Scenario | Expected |
| --- | --- | --- |
| A-01 | A free bay and a free qualified technician exist | Confirmed, both assigned, end time derived from the service type |

### 5.2 Booking — resource rejections

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| A-02 | No bay free for the whole slot | 409 | 1.1, 1.2 |
| A-03 | Bay free, no technician free | 409 | 1.2 |
| A-04 | Technician free but lacks the required skill | 409 | 1.5 |
| A-05 | Service requires two skills, technician holds one | 409 | 1.5 |
| A-06 | Service requires two skills, technician holds both | Confirmed | 1.5 |
| A-07 | The vehicle already has an overlapping appointment elsewhere | 409 | 1.4 |
| A-08 | Two qualified technicians; one holds fewer skills | the fewer-skilled one is chosen | 1.3 |

A-06 exists so that A-05 cannot pass for the wrong reason — together they prove a subset test rather
than an intersection test.

### 5.3 Booking — calendar rejections

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| A-09 | Start before opening time | 422 / domain rejection | 2.2 |
| A-10 | Start valid but the service would run past closing | rejected | 2.2 |
| A-11 | Slot straddles the lunch break | rejected | 2.2 |
| A-12 | Dealership has no opening-hours row for that weekday | rejected | 2.2 |
| A-13 | Opening hours evaluated in dealership-local time, not UTC | correct accept/reject either side of the boundary | 2.5 |

### 5.4 Booking — request rejections

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| A-14 | Start time in the past | rejected | 5.3 |
| A-15 | Start time less than the minimum lead time ahead | rejected | 5.3 |
| A-16 | Start time beyond the ninety-day horizon | rejected | 5.3 |
| A-17 | Service type not found or inactive | rejected | 5.3 |
| A-18 | Vehicle not found | rejected | 5.3 |

### 5.5 The design property

| Id | Scenario | Expected |
| --- | --- | --- |
| A-19 | A request that fails the opening-hours check | the availability repository is **never called** |

A-19 asserts the fail-fast ordering rather than an output. It is what stops the pipeline from silently
degrading into "query everything, then validate", which would lengthen lock hold time under load.

### 5.6 Availability

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| A-20 | A day with no bookings | the full opening window is offered | 5.1 |
| A-21 | The only bay is busy 10:00–11:00 | that window is absent from the result | 1.1 |
| A-22 | No technician holds the required skill that day | empty result | 1.5 |
| A-23 | Dealership closed that day | empty result | 2.2 |

### 5.7 Cancellation

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| A-24 | Cancel a Confirmed appointment | succeeds, status becomes Cancelled | 4.1 |
| A-25 | Cancel an already-cancelled appointment | 409 | 4.3 |
| A-26 | Cancel a Completed appointment | 409 | 4.3 |
| A-27 | Cancel an appointment that does not exist | 404 | 5.3 |

---

## 6. Integration tier

Real SQL Server via `Testcontainers.MsSql`, provisioned by `DatabaseMigrator.Run()` — the same entry
point production uses. One container per test collection, reused.

### 6.1 Schema and migration

| Id | Scenario | Expected |
| --- | --- | --- |
| I-01 | Migrator against an empty container | succeeds |
| I-02 | Migrator run a second time | no-op, no error — proves the scripts are safe to redeploy |
| I-03 | Migrator without `--demo` | no demo rows present |
| I-04 | Schema smoke test touching every `DbSet` | no column-mapping error |

I-04 is the drift guard: DbUp owns the schema and EF Core only maps to it, so a renamed or missing
column has no other way of being caught before production.

### 6.2 Constraints the database enforces

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| I-05 | Appointment pairing a bay and a technician from different dealerships | rejected by the composite FK | 1.4 |
| I-06 | Appointment whose vehicle is not owned by the stated customer | rejected by the composite FK | 1.4 |
| I-07 | Appointment with `EndsAtUtc <= StartsAtUtc` | rejected by the CHECK constraint | 1.1 |

These three prove that the invariants survive a writer that bypasses the application entirely.

### 6.3 Predicate agreement

| Id | Scenario | Expected |
| --- | --- | --- |
| I-08 | Seed a spread of appointments; compare repository results against `TimeSlot.Overlaps` for the same inputs | identical for every case |

The overlap rule exists twice — once as C# in the domain, once as a LINQ predicate EF translates to
SQL. I-08 is the only thing keeping the two in step, which is why it is on the never-cut list.

### 6.4 End-to-end booking

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| I-09 | `POST /api/v1/appointments` with a valid request | `201`, `Location` header, row persisted with customer, vehicle, technician and bay all set | **Requirement 3** |
| I-10 | `GET /api/v1/availability` after I-09 | the booked slot is no longer offered | **Requirement 2** |
| I-11 | Repeat the same booking | `409` with a `ProblemDetails` body | 1.1 |

### 6.5 Lifecycle

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| I-12 | Book → cancel → book the same slot again | the third call succeeds | 4.1 |
| I-13 | Availability after a cancellation | the slot is offered again | 4.1 |

I-12 is the test that catches a forgotten status filter. A phantom booking that occupies a bay forever
is invisible in every other test.

### 6.6 Concurrency — Stage 7

All of these build a dealership with **exactly one bay and one qualified technician**.

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| I-14 | Two identical bookings fired with `Task.WhenAll` | exactly one `201`, exactly one `409`, exactly one row in the table | 3.1 |
| I-15 | Two bookings for **different** slots on the same bay, in parallel | both `201` — the guard serialises without over-blocking | 3.1 |
| I-16 | Two bookings for different bays, in parallel | both `201` | 3.1 |
| I-17 | I-14 repeated ten times | stable — no flakiness, no deadlock | 3.1 |

I-15 and I-16 matter as much as I-14. Without them, a guard that simply rejected every concurrent
request would pass the suite while making the system useless.

### 6.7 Error contract

| Id | Scenario | Expected | Covers |
| --- | --- | --- | --- |
| I-18 | Missing required field | `400` with `ProblemDetails` naming the field | 5.3 |
| I-19 | Malformed GUID | `400` | 5.3 |
| I-20 | Unknown dealership id | `404` | 5.3 |

### 6.8 Observability

| Id | Scenario | Expected |
| --- | --- | --- |
| I-21 | Any response | carries a correlation id, and a log line carries the same value |
| I-22 | A successful booking | produces spans `booking.validate`, `booking.search_resources`, `booking.commit` |

---

## 7. Requirement traceability

| Brief requirement | Proven by |
| --- | --- |
| 1. Request an appointment for a specific vehicle, service type, dealership and time | I-09, plus A-14 … A-18 for the rejection surface |
| 2. Check availability of both a bay **and** a qualified technician for the **entire** duration | A-02 … A-06 (logic), D-01 … D-09 (the interval rule), I-10 (observable effect) |
| 3. Persist an appointment associating customer, vehicle, technician and bay | I-09 asserts all four associations; I-05 and I-06 prove they cannot be inconsistent |

---

## 8. Never cut

If the schedule collapses, these four survive. They carry most of the evaluated signal and each covers
a failure mode nothing else would catch.

| Id | Why it survives |
| --- | --- |
| **D-02, D-08** | The `<=` mistake is invisible in manual testing and breaks back-to-back bookings |
| **I-08** | The only thing keeping the C# rule and the SQL predicate in agreement |
| **I-12** | The only test that catches a forgotten cancelled-status filter |
| **I-14** | The only evidence the concurrency guard actually works — unreadable from the code alone |

---

## 9. Deliberately not tested

| Area | Why |
| --- | --- |
| Technician shifts and leave | Out of scope — assumption recorded that working hours equal opening hours |
| Buffer between appointments | Out of scope — the occupancy interval is isolated so adding one later is a single change |
| Rescheduling | Optional in Stage 5; if built, add a test that the appointment does not block itself (`Id != @appointmentId`) |
| Idempotency | Optional in Stage 7; if built, assert a repeated `Idempotency-Key` returns `200` with the original appointment rather than creating a second |
| Authentication and authorisation | Out of scope — the API is treated as internal, called by an already-authenticated dealer system |
| Load and stress behaviour | Out of scope. Query latency is measured once against 50,000 seeded appointments (Stage 3) and recorded in the README, which is evidence rather than a test |
