# Scenario A — Problems, options, and chosen solutions

Every entry follows the same shape: the options that were genuinely on the table, the one chosen, and
why the others lost. Entries marked **Documented only** are deliberately out of scope — the reasoning
is recorded so the boundary is a decision rather than an oversight.

---

## 0. The organising principle: fail fast, cheapest first

The booking request runs through a fixed pipeline, ordered by cost:

| Stage | Cost | Example rejection |
| --- | --- | --- |
| 1. Shape validation | none | missing `vehicleId` |
| 2. Pure business rules | none | start time in the past |
| 3. Reference data load | one query | service type not found |
| 4. Calendar rules | in memory | outside opening hours |
| 5. Availability search | indexed queries | no free bay |
| 6. Transactional commit | lock held | lost the race |

A request that violates opening hours is rejected at stage 4 without ever touching the appointments
table. Only requests that survive everything reach stage 6, where locks are taken — so the expensive,
contended stage sees the smallest possible traffic. This ordering is itself a design decision: it is
what keeps lock hold time short, which is what keeps throughput acceptable under concurrency.

---

## 1. Finite resource allocation

### 1.1 Detecting overlap on one resource

**Options**
- (a) Half-open interval predicate in SQL: `StartsAtUtc < @end AND EndsAtUtc > @start`
- (b) Load the day's appointments into memory and compare in C#
- (c) Discrete slot table; overlap becomes a uniqueness conflict

**Chosen: (a)**

**Why.** It is *sargable* — with the index `(ServiceBayId, StartsAtUtc) INCLUDE (EndsAtUtc)` the
database seeks rather than scans. It also stays a database predicate, which matters later: the same
predicate is what the engine takes range locks on in 3.1. Option (b) cannot be locked and transfers
rows for nothing. Option (c) is correct but forces every service duration onto a fixed grid; it is
only worth it if the no-overlap guarantee must be declarative (see 3.1, SQL Server variant).

### 1.2 Requiring a bay *and* a technician simultaneously

**Options**
- (a) One query with `CROSS JOIN` over bays × technicians, filtered by both availability predicates
- (b) Two independent queries — free bays, free qualified technicians — then pair them in code
- (c) Loop over bays; for each, search for a free technician

**Chosen: (b)**

**Why.** The two constraints are **independent**: whether a bay is free does not depend on which
technician is picked, and vice versa. So the problem decomposes — any free bay paired with any free
qualified technician is a valid assignment. That makes the work `m + n` instead of `m × n`, and each
query is a clean index seek.

Option (a) is not wrong, just needlessly quadratic. Option (c) is the same cost as (a) with extra
round trips.

**When this stops holding.** If a service ever requires a *specific kind* of bay (a lift for
undercarriage work), bay and technician stay independent but bays gain a capability filter. Only a
genuine coupling — "technician X may only use bay Y" — would force the cross product back. Recorded as
an assumption.

### 1.3 Choosing which bay and which technician

**Options**
- (a) First fit — lowest bay code, lowest technician id
- (b) Load balancing — the technician with the fewest hours booked that day
- (c) Specialist preservation — the *least* qualified technician who still qualifies

**Chosen: (c), implemented as an `ORDER BY` on top of first fit**

**Why.** Assigning the master technician to an oil change blocks them from the gearbox job that
arrives an hour later. Ordering candidates by skill count ascending keeps broadly-skilled staff free
for work only they can do, and it costs one `ORDER BY` clause — no scheduling algorithm, no lookahead.

Option (b) needs demand data the system does not have and can be actively worse: balancing load across
technicians spreads specialists thin. Option (a) is the fallback tie-break, which also keeps tests
deterministic.

The policy is expressed as an `ORDER BY` inside the repository query, not behind a strategy interface.
One policy with one implementation does not need a seam; extracting one is a mechanical change on the
day a second dealership-specific policy actually exists.

### 1.4 The vehicle is a resource too

**Options**
- (a) Apply the same overlap predicate to `VehicleId`
- (b) Apply it to `CustomerId` as well
- (c) Ignore — treat it as a client-side concern

**Chosen: (a) only**

**Why.** A car physically cannot be in two bays at once, so permitting it is data corruption, and the
check is the same predicate against one more index — effectively free.

`CustomerId` is deliberately **not** constrained: a customer with two cars booking both for the same
morning is legitimate. Constraining it would reject valid business.

### 1.5 "Qualified" technician

**Options**
- (a) Single `RequiredSkillId` column on `ServiceTypes`
- (b) Many-to-many `ServiceTypeRequiredSkills`; technician must hold *all* required skills
- (c) Skill levels (apprentice / certified / master) with a minimum threshold

**Chosen: (b)**

**Why.** Real services need multiple certifications — EV battery work needs high-voltage *and*
mechanical; a windscreen on a modern car needs glass *and* ADAS calibration. Several of these are
legal requirements, not preferences. Option (a) cannot express them.

The cost is a subset test, which is ugly in raw SQL but reads like the business rule in LINQ:

```csharp
.Where(t => serviceType.RequiredSkills.All(rs => t.Skills.Any(ts => ts.SkillId == rs.SkillId)))
```

EF Core translates this to nested `NOT EXISTS`. Option (c) adds a dimension the brief never asks for.

**Documented only:** a job split across several technicians by stage. The brief says "a qualified
Technician", singular, so one appointment has one technician. The extension is sketched in
`data-model.md`.

---

## 2. Duration and time boundaries

### 2.1 Deriving the end time

**Options**
- (a) Server computes `EndsAtUtc = start + ServiceType.DurationMinutes` and stores it
- (b) Client supplies the end time
- (c) Compute it at query time by joining `ServiceTypes`

**Chosen: (a)**

**Why.** (b) lets a client book a two-hour job into a thirty-minute window — availability must never
depend on client-supplied values. (c) makes the overlap predicate a per-row expression, which no index
can serve, turning every availability check into a scan.

Storing it also gives correct history: changing a service type's duration must not retroactively move
appointments already booked.

### 2.2 Opening hours

**Options**
- (a) Validate in the domain layer against `DealershipOpeningHours` before searching resources
- (b) A database constraint
- (c) Pre-generate bookable windows per day and match against them

**Chosen: (a)**

**Why.** It is a pure function of the request and rarely-changing reference data, so it needs no lock
and no transaction — it belongs at stage 4 of the pipeline, before anything expensive. It also
produces a specific error ("the workshop closes at 17:00") rather than a bare conflict.

(b) is impossible: the rule spans tables. (c) is 1.1 option (c) in disguise and carries the same
quantisation cost.

The appointment must fit **entirely inside a single** opening-hours row. With a lunch break stored as
two rows for one day, a job spanning the break is correctly rejected.

### 2.3 Technician shifts and leave — **Documented only**

**Why deferred.** It introduces no new concept: a shift is an interval, absence is an interval, and
both plug into the existing predicate as one more `NOT EXISTS`. It multiplies reference data and
seeding effort without demonstrating anything the bay and technician checks do not already
demonstrate. Assumption recorded: technician working hours equal dealership opening hours.

### 2.4 Buffer between appointments — **Documented only**

**Why deferred, and how it stays cheap.** The brief never mentions clean-up time. The important part
is not implementing it but making it a one-place change: the overlap predicate reads its interval from
a single domain member, `Appointment.OccupancyWindow`, rather than from `StartsAtUtc`/`EndsAtUtc`
directly. Introducing a buffer becomes a change to that one property plus a column — not a change to
every query.

Note this is *not* a reason to switch the comparison to `>=`; a buffer lengthens the occupied
interval, it does not change the half-open semantics.

### 2.5 Time zones

**Options**
- (a) Store UTC; convert at the edges using `Dealerships.TimeZoneId` (IANA)
- (b) Store dealership local time
- (c) Store `DateTimeOffset`

**Chosen: (a)**

**Why.** Comparing and ordering instants must be unambiguous, and only UTC gives that. Opening hours
are genuinely local wall-clock values, so they stay as `time` and the requested instant is converted
into dealership-local time for that one comparison. .NET 6+ resolves IANA ids on Windows too, so
`Asia/Ho_Chi_Minh` is portable.

(b) breaks ordering across the daylight-saving transition — local times repeat or vanish. (c) stores an
offset, not a zone, so it still cannot answer "what are the opening hours that day".

**DST edge case.** On a transition day a local time may be invalid or ambiguous. Requests are checked
with `TimeZoneInfo.IsInvalidTime` / `IsAmbiguousTime`; invalid times are rejected with a clear message,
ambiguous times resolve to the standard-time interpretation. Documented rather than silently guessed.

---

## 3. Correctness under concurrency

### 3.1 The race condition

Two requests read "bay free", both conclude yes, both insert. No line of code is wrong; the result is
still two overlapping appointments.

**Options** (the database is SQL Server, so the PostgreSQL-only answer is listed for completeness)
- (a) Application-level check only
- (b) External distributed lock (Redis, etc.)
- (c) `SERIALIZABLE` transaction
- (d) `UPDLOCK, HOLDLOCK` range locks on the availability query
- (e) `sp_getapplock` keyed per resource
- (f) **A version token on the parent row, updated before the availability check**
- (g) Slot table with a unique key on `(ResourceId, SlotStart)`
- (h) `EXCLUDE USING gist` — PostgreSQL only, unavailable here
- (i) Serialise all bookings through a single-threaded queue

**Chosen: (f)**

Inside the booking transaction, the bay and technician rows are touched *before* the overlap check:

```csharp
await _appointments.LockResourcesAsync(bay.Id, technician.Id, ct);   // an UPDATE — exclusive row locks

// only this transaction can reach these resources now
if (conflict) return Result.Conflict();
```

**Why.** The guarantee still lives in the database, but it is expressed entirely in EF Core — no table
hints, no raw SQL, no isolation-level tuning.

The mechanism is worth stating precisely, because it is not the usual optimistic-concurrency story: an
`UPDATE` takes an exclusive row lock. A second booking for the same bay blocks on that `UPDATE` until
the first transaction commits, then proceeds and sees the appointment the first one wrote. The result
is **pessimistic serialisation obtained through an ordinary EF write**, so the second caller gets a
truthful 409 rather than a retry storm. The `IsConcurrencyToken` mapping remains as a second line of
defence for the paths that read before writing.

Contention is acceptable because of the volume: one bay takes roughly eight bookings a day, so the
window in which two requests collide on the same row is vanishingly small — and when they do, one
waits milliseconds.

**Why not the others.** (a) provides nothing. (b) is a lock held in a system that is not the source of
truth — if it expires early or the holder pauses, the database still accepts the write, so it produces
a *feeling* of safety rather than safety. (i) trades all concurrency for correctness and becomes the
bottleneck.

(c), (d) and (e) all work. They lose on cost, not correctness: (c) makes every read in the transaction
take range locks and needs retry plumbing plus careful `DbContext` state handling on each attempt;
(d) is the narrowest lock available but requires `FromSql` and strict index discipline; (e) is
predictable but advisory — nothing forces a future write path to take it.

(g) is the strongest *declarative* guarantee SQL Server can offer and would eliminate two other bug
classes as a side effect, but it quantises every service duration onto a fixed grid and multiplies row
counts by roughly sixteen. Recorded as the alternative to revisit if the booking rules ever need a
constraint the application cannot bypass.

**Ordering.** Two version rows are updated per booking, so `LockResourcesAsync` always writes them in
ascending id order — otherwise two transactions can lock them in opposite directions and deadlock.
Keeping that rule inside one repository method means no caller can get it wrong.

**One token type.** `Version` is a plain `int` on every versioned entity; SQL Server's `rowversion` is
not used anywhere. It is store-generated, so EF Core cannot emit the `UPDATE` this guard depends on —
and once one table needs `int`, running two schemes only creates a question about which applies where.
The increment is applied inside `AppointmentDbContext.SaveChangesAsync` for any modified `IVersioned`
entity — not in `UnitOfWork`, because a repository holding the context could otherwise save directly
and disable the check with no error and no failing test. Overriding the context puts the enforcement at
the one point every write path must cross.

**Verification.** This is the one decision that cannot be judged by reading the code, so it is proven
by test: two bookings for the same bay and slot fired with `Task.WhenAll`, asserting exactly one 201,
one 409, and exactly one row in the table. Run against a real database via Testcontainers — an
in-memory provider has no locking semantics and would pass while proving nothing.

### 3.2 Idempotency

**Options**
- (a) `Idempotency-Key` header + unique index; a repeat returns the original appointment
- (b) Client-generated appointment id with `PUT` semantics
- (c) Nothing

**Chosen: (a)**

**Why.** Retries happen at layers the application does not control — a double click, a proxy timeout,
a mobile network. The unique index makes the guarantee authoritative rather than advisory: a duplicate
key violation is caught and answered with `200 OK` plus the original appointment, so the caller sees
the same result whether or not the first attempt landed.

(b) is clean REST but pushes id generation onto every client. (c) is how customers end up with two
appointments.

---

## 4. Appointment lifecycle

### 4.1 Cancellation must free resources

**Options**
- (a) Status change to `Cancelled`, filtered out of every availability query
- (b) Hard delete
- (c) Move to an archive table

**Chosen: (a)**

**Why.** History matters — no-show rates and cancellation patterns are exactly what a dealership wants
later, and a deleted row answers nothing. (c) splits one concept across two tables and complicates
every query for no gain.

**The risk, and the mitigation.** The failure mode is forgetting the status filter in one query, which
leaves a phantom booking occupying a bay forever. So the filter exists in exactly two places, and they
are written to agree: a single `IQueryable` extension `Occupying()` used by every availability query,
and the matching `WHERE Status IN ('Confirmed','InProgress')` in the filtered index. A new query that
forgets the helper also misses the index — it gets slow before it gets wrong, which is the failure
mode you want.

### 4.2 Rescheduling atomically

**Options**
- (a) Cancel the old, then book the new
- (b) Book the new and cancel the old inside one transaction
- (c) Update the existing row's times in place

**Chosen: (b)**

**Why.** (a) has a window where the customer holds nothing: if the new slot is gone, they have lost the
old one too. Wrapping both in one transaction means a failure leaves the original untouched.

**The gotcha.** When shifting an appointment by thirty minutes in the same bay, the appointment's *own*
occupancy overlaps the requested window and it blocks itself. The availability predicate therefore
carries `AND Id <> @appointmentId` when rescheduling. This is easy to miss and produces a baffling
"no availability" for a slot that is visibly free.

(c) is the same thing as (b) mechanically but loses the audit trail of what was originally booked.

### 4.3 Status transitions

**Options**
- (a) `Status` column with transitions enforced by the domain entity
- (b) Event sourcing
- (c) Free-form status with no enforcement

**Chosen: (a)**

**Why.** The legal moves are few and fixed — `Confirmed → InProgress → Completed`, with `Cancelled`
and `NoShow` as terminal branches — so a guard method on the entity covers it and is trivially unit
tested without a database. Event sourcing solves a problem this domain does not have. (c) guarantees
that something will eventually write a status nothing else recognises.

Only `Confirmed` and `InProgress` occupy resources; that definition lives in one place and is shared
with 4.1.

---

## 5. Experience and operations

### 5.1 Answering "no availability"

**Options**
- (a) Bare `409 Conflict`
- (b) `409` enriched with the next available slots
- (c) A separate `GET /availability` endpoint so the client searches before committing

**Chosen: (c) as the primary flow, (b) as an enhancement**

**Why.** Search and commit are different operations with different costs: search is read-only,
cacheable and safe to call repeatedly; commit takes locks. Separating them is how booking systems are
normally shaped, and it turns "no availability" from an error into the normal path — the client only
posts slots it has reason to believe are free.

Once the availability engine exists, (b) is nearly free, since the 409 handler calls the same engine.
(a) alone is technically correct and practically useless.

### 5.2 Query performance

**Options**
- (a) Filtered composite indexes only
- (b) A materialised availability cache
- (c) Read replica for calendar views

**Chosen: (a)**

**Why — with the numbers.** One dealership with six bays running eight slots a day generates roughly
17,000 appointments a year. A hundred dealerships is under two million rows, and every availability
query is filtered by a single resource id and a narrow time range. That is an index seek returning a
handful of rows; there is nothing here for a cache to fix.

(b) would add invalidation — a genuinely hard problem — to solve a problem that does not exist yet, and
a stale availability cache produces exactly the double-booking the design works to prevent.

Recorded for later: at real scale the first moves are partitioning by dealership and routing
read-only calendar views to a replica. Neither changes the booking path.

### 5.3 Validation

**Options**
- (a) All validation at the API boundary
- (b) All validation in the domain
- (c) Split by kind

**Chosen: (c)**

**Why.** The two kinds have different homes. *Shape* — required fields, parseable dates, positive
values — belongs at the boundary with FluentValidation, returning field-level errors in a
`ProblemDetails` response. *Rules* — the start time is in the future, within the booking horizon, the
service type is active, the vehicle belongs to the customer — belong in the domain, where they are
testable without HTTP and cannot be bypassed by a second caller.

Putting everything at the boundary (a) means the domain trusts its inputs, which holds only until
something else calls it. Putting everything in the domain (b) produces poor error messages, because by
then the request has lost its field structure.

Concrete horizon rules: start must be at least one hour ahead and at most ninety days ahead. Both are
configuration, not constants.

---

## 6. Build for the future

The brief asks that the design "consider scalability, performance, reliability, maintainability, and
observability". Four of those five are **operational** qualities — how the system behaves as load,
data and time grow. Extensibility, reusability and pluggability are not on the list. That distinction
drove what was built and, more importantly, what was not.

### 6.1 Deliberately not built

A generic `IRepository<T>` and a `SharedKernel` project were considered and rejected.

- **Shared Kernel** is a DDD term for a slice of model shared between *bounded contexts* under joint
  governance. This system has one context and one deployable, so the name would describe nothing that
  exists.
- **A generic repository** has to expose something like `Find(Expression<Func<T, bool>>)` to be usable
  at all, which puts EF Core's translation rules back into the Application layer and makes the seam
  decorative. Repository methods here return materialised lists precisely to avoid that.
- It **contradicts the per-slice interface rule**: one interface serving every feature is exactly the
  coupling that rule exists to prevent.
- Neither serves any of the five qualities named in the brief.

Recording the rejection matters as much as the rejection: the absence is a decision, not an oversight.

### 6.2 How each quality is addressed

| Quality | In the build | Evidence a reviewer can check |
| --- | --- | --- |
| **Scalability** | Availability search decomposed to `m + n` rather than `m × n`; filtered indexes; stateless API; growth path documented in 6.4 | The search issues two index seeks regardless of resource count |
| **Performance** | Index seeks, no N+1, `AsNoTracking()` on read paths, fail-fast ordering that keeps lock hold time minimal | A measured latency figure in the README (6.3) |
| **Reliability** | Database-level concurrency guard, transactional writes, composite foreign keys, command timeouts, readiness probe that actually queries the database | The parallel double-booking test |
| **Maintainability** | Three test tiers, an architecture guard test, one single definition of "occupies", and this document | The predicate-agreement test keeps the overlap rule identical in C# and SQL |
| **Observability** | Structured logs with correlation id, traces with business-named spans, domain counters | A trace reads as booking steps, not as one HTTP span |

### 6.3 Evidence over assertion

Two cheap artefacts turn claims into evidence.

**A measured number.** The seed can generate 50,000 appointments across a year; the availability
query is then timed against it and the figure recorded in the README:

> `Availability search: ~4 ms p95 against 50,000 seeded appointments (index seek, 3 rows returned).`

This also substantiates the decision *not* to add a cache — it shows the query was measured rather
than assumed.

**Business-named telemetry.** Default HTTP and EF Core instrumentation is table stakes. The spans
that carry meaning are named after the domain:

```csharp
using var activity = Telemetry.Source.StartActivity("booking.search_resources");
activity?.SetTag("dealership.id", dealershipId);
activity?.SetTag("service.duration_minutes", serviceType.DurationMinutes);
activity?.SetTag("candidates.bays", freeBays.Count);
activity?.SetTag("candidates.technicians", freeTechnicians.Count);
```

A trace then reads *search 3 ms → select 0.1 ms → commit 12 ms* instead of
`POST /appointments 15 ms`. Paired with `booking_conflicts_total`, this is what would let an operator
answer "are customers losing races, and where is the time going?" — which is the actual point of
observability.

### 6.4 What changes at 100×

Current volume: one dealership with six bays running eight appointments a day produces roughly
**17,000 rows a year**. A hundred dealerships is under two million rows, and every availability query
is filtered to one resource id over a narrow time range — an index seek returning single-digit rows.
Nothing here needs a cache, a queue, or a second service.

When that stops being true, in order:

1. **Partition `Appointments` by `DealershipId`.** The access pattern is already dealership-scoped, so
   this requires no query changes.
2. **Route read-only calendar views to a replica.** The booking path stays on the primary; only the
   "show me this week" queries move.
3. **Only then** consider materialising availability.

Caching availability is listed last deliberately, and with a warning: **a stale availability cache
reproduces exactly the double-booking this design exists to prevent.** It is the one optimisation
whose failure mode is a correctness bug rather than a slow page, so it should be the last thing tried
and the first thing suspected.

---

## 7. Scope summary

| ID | Problem | Decision | Build |
| --- | --- | --- | --- |
| 1.1 | Interval overlap | Half-open predicate, indexed | ✅ |
| 1.2 | Bay and technician together | Decomposed into two independent searches | ✅ |
| 1.3 | Which pair to assign | Least-qualified-that-qualifies, as an `ORDER BY` | ✅ |
| 1.4 | Vehicle double-booking | Same predicate on `VehicleId` | ✅ |
| 1.5 | Qualified technician | Many-to-many, subset test | ✅ |
| 2.1 | End time | Server-computed, stored | ✅ |
| 2.2 | Opening hours | Domain validation before resource search | ✅ |
| 2.3 | Shifts and leave | Assumption: equal to opening hours | ❌ |
| 2.4 | Buffer time | Isolated behind `OccupancyWindow` | ❌ |
| 2.5 | Time zones | UTC storage, IANA zone per dealership | ✅ |
| 3.1 | Race condition | Version token on the parent row, locked before the check | ✅ mechanism built; end-to-end race test outstanding |
| 3.2 | Idempotency | `Idempotency-Key` + unique index | ⚠️ key persisted; replay not honoured |
| 4.1 | Cancellation frees resources | `Occupying()` filter + matching index predicate | ⚠️ filter built; cancel endpoint deliberately not built |
| 4.2 | Reschedule | Single transaction, self-exclusion | ❌ not built |
| 4.3 | Status transitions | Guarded transitions on the entity | ✅ in the domain; no endpoint drives them yet |
| 5.1 | No availability | `GET /availability` returns 200 with an empty slot list | ✅ 409 enrichment not built |
| 5.2 | Performance | Indexes only, justified by volume | ✅ |
| 5.3 | Validation | Split: shape at the edge, rules in the domain | ✅ |

**As shipped: ten fully built, three built with a stated caveat, two partial, three deliberately out of
scope.** ⚠️ marks work that is partly present and whose remaining half is named; ❌ marks scope
excluded on purpose. Every ❌ and ⚠️ carries a recorded assumption rather than silence, and the shipped
state is summarised in the README.

---

## 8. Platform decisions taken

| Decision | Choice | Recorded in |
| --- | --- | --- |
| Database engine | SQL Server 2022 | `technical-plan.md` §1.1 |
| Schema ownership | DbUp scripts; EF Core maps only, no EF migrations | `technical-plan.md` §2.6 |
| Concurrency guard | Version token on the parent row, touched before the check | §3.1 above |
| API and use-case contracts | One `Request`/`Response` pair per use case, shared by both layers | `technical-plan.md` §3.3 |
| Time zone | `Dealerships.TimeZoneId`, a row-level column | §2.5 above |

PostgreSQL was the serious alternative, and it offers the one thing SQL Server cannot: `EXCLUDE USING
gist`, which declares the no-overlap rule as a constraint that holds even against a manual `INSERT`.
It was not chosen because the platform this system belongs to is a Microsoft one, and because the
guard actually selected in 3.1 works identically on both engines — so the stronger constraint would
have gone unused.

The consequence is that the no-overlap rule is enforced by **application code inside a transaction**
rather than by a constraint. That is a real difference in strength, stated here rather than glossed
over, and it is why the parallel-booking test in Stage 7 is treated as non-negotiable: it is the only
evidence that the guard actually holds.
