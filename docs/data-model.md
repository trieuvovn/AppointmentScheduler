# Scenario A — Data model

> DDL is written for **SQL Server**. Differences for PostgreSQL are noted at the end.
> All timestamps are stored in UTC; column names carry the `Utc` suffix to make that impossible to forget.

---

## 1. Table inventory

| # | Table | Why it exists | In scope |
| --- | --- | --- | --- |
| 1 | `Dealerships` | Tenant / location. Holds the time zone and, with `DealershipOpeningHours`, the business calendar. | Core |
| 2 | `DealershipOpeningHours` | Opening / closing time per day of week. An appointment must fit inside it. | Core |
| 3 | `ServiceBays` | The physical constrained resource. | Core |
| 4 | `Technicians` | The human constrained resource. | Core |
| 5 | `Skills` | Vocabulary shared by technicians and service types. | Core |
| 6 | `TechnicianSkills` | Which technician can do what. Many-to-many. | Core |
| 7 | `ServiceTypes` | Service catalogue. **Owns `DurationMinutes`** — the source of the appointment's end time. | Core |
| 8 | `ServiceTypeRequiredSkills` | Which skills a service demands. Many-to-many. | Core |
| 9 | `Customers` | Vehicle owner, named explicitly in requirement 3. | Core |
| 10 | `Vehicles` | The thing being serviced, named in requirement 1. | Core |
| 11 | `Appointments` | The transaction record. All overlap rules act on this table. | Core |
| 12 | `TechnicianShifts`, `TechnicianLeave` | Per-technician working hours and absence. | **Out of scope** — documented as an assumption |

"Qualified technician" is resolved as: every skill in `ServiceTypeRequiredSkills` for the requested
service must also appear in `TechnicianSkills` for the candidate technician.

---

## 2. DDL

### Reference data

```sql
CREATE TABLE Dealerships (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_Dealerships PRIMARY KEY,
    Name         nvarchar(200)    NOT NULL,
    TimeZoneId   varchar(64)      NOT NULL,   -- IANA id, e.g. 'Asia/Ho_Chi_Minh'
    CreatedAtUtc datetime2(3)     NOT NULL CONSTRAINT DF_Dealerships_Created DEFAULT SYSUTCDATETIME()
);

CREATE TABLE DealershipOpeningHours (
    DealershipId uniqueidentifier NOT NULL,
    DayOfWeek    tinyint          NOT NULL,   -- 0 = Sunday .. 6 = Saturday
    OpensAt      time(0)          NOT NULL,
    ClosesAt     time(0)          NOT NULL,
    CONSTRAINT PK_OpeningHours PRIMARY KEY (DealershipId, DayOfWeek),
    CONSTRAINT FK_OpeningHours_Dealership FOREIGN KEY (DealershipId) REFERENCES Dealerships(Id),
    CONSTRAINT CK_OpeningHours_Order CHECK (ClosesAt > OpensAt)
);

CREATE TABLE ServiceBays (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_ServiceBays PRIMARY KEY,
    DealershipId uniqueidentifier NOT NULL,
    Code         nvarchar(20)     NOT NULL,
    IsActive     bit              NOT NULL CONSTRAINT DF_ServiceBays_Active DEFAULT 1,
    Version      int              NOT NULL CONSTRAINT DF_ServiceBays_Version DEFAULT 0,
    CONSTRAINT FK_ServiceBays_Dealership FOREIGN KEY (DealershipId) REFERENCES Dealerships(Id),
    CONSTRAINT UQ_ServiceBays_Code UNIQUE (DealershipId, Code),
    CONSTRAINT UQ_ServiceBays_IdDealership UNIQUE (Id, DealershipId)   -- target for composite FK
);

CREATE TABLE Technicians (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_Technicians PRIMARY KEY,
    DealershipId uniqueidentifier NOT NULL,
    FullName     nvarchar(200)    NOT NULL,
    IsActive     bit              NOT NULL CONSTRAINT DF_Technicians_Active DEFAULT 1,
    Version      int              NOT NULL CONSTRAINT DF_Technicians_Version DEFAULT 0,
    CONSTRAINT FK_Technicians_Dealership FOREIGN KEY (DealershipId) REFERENCES Dealerships(Id),
    CONSTRAINT UQ_Technicians_IdDealership UNIQUE (Id, DealershipId)   -- target for composite FK
);

CREATE TABLE Skills (
    Id   uniqueidentifier NOT NULL CONSTRAINT PK_Skills PRIMARY KEY,
    Code varchar(40)      NOT NULL CONSTRAINT UQ_Skills_Code UNIQUE,
    Name nvarchar(120)    NOT NULL
);

CREATE TABLE TechnicianSkills (
    TechnicianId uniqueidentifier NOT NULL,
    SkillId      uniqueidentifier NOT NULL,
    CONSTRAINT PK_TechnicianSkills PRIMARY KEY (TechnicianId, SkillId),
    CONSTRAINT FK_TechnicianSkills_Technician FOREIGN KEY (TechnicianId) REFERENCES Technicians(Id),
    CONSTRAINT FK_TechnicianSkills_Skill      FOREIGN KEY (SkillId)      REFERENCES Skills(Id)
);

CREATE TABLE ServiceTypes (
    Id              uniqueidentifier NOT NULL CONSTRAINT PK_ServiceTypes PRIMARY KEY,
    Code            varchar(40)      NOT NULL CONSTRAINT UQ_ServiceTypes_Code UNIQUE,
    Name            nvarchar(200)    NOT NULL,
    DurationMinutes int              NOT NULL,
    IsActive        bit              NOT NULL CONSTRAINT DF_ServiceTypes_Active DEFAULT 1,
    CONSTRAINT CK_ServiceTypes_Duration CHECK (DurationMinutes > 0 AND DurationMinutes <= 8 * 60)
);

CREATE TABLE ServiceTypeRequiredSkills (
    ServiceTypeId uniqueidentifier NOT NULL,
    SkillId       uniqueidentifier NOT NULL,
    CONSTRAINT PK_ServiceTypeRequiredSkills PRIMARY KEY (ServiceTypeId, SkillId),
    CONSTRAINT FK_STRS_ServiceType FOREIGN KEY (ServiceTypeId) REFERENCES ServiceTypes(Id),
    CONSTRAINT FK_STRS_Skill       FOREIGN KEY (SkillId)       REFERENCES Skills(Id)
);

CREATE TABLE Customers (
    Id       uniqueidentifier NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    FullName nvarchar(200)    NOT NULL,
    Email    nvarchar(256)    NULL,
    Phone    nvarchar(32)     NULL
);

CREATE TABLE Vehicles (
    Id         uniqueidentifier NOT NULL CONSTRAINT PK_Vehicles PRIMARY KEY,
    CustomerId uniqueidentifier NOT NULL,
    Vin          char(17)       NOT NULL CONSTRAINT UQ_Vehicles_Vin UNIQUE,
    LicensePlate nvarchar(16)   NULL,
    Make         nvarchar(60)   NOT NULL,
    Model        nvarchar(60)   NOT NULL,
    ModelYear    smallint       NULL,
    CONSTRAINT FK_Vehicles_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
    CONSTRAINT UQ_Vehicles_IdCustomer UNIQUE (Id, CustomerId)         -- target for composite FK
);
```

### The transaction table

```sql
CREATE TABLE Appointments (
    Id             uniqueidentifier NOT NULL CONSTRAINT PK_Appointments PRIMARY KEY,
    DealershipId   uniqueidentifier NOT NULL,
    ServiceBayId   uniqueidentifier NOT NULL,
    TechnicianId   uniqueidentifier NOT NULL,
    ServiceTypeId  uniqueidentifier NOT NULL,
    VehicleId      uniqueidentifier NOT NULL,
    CustomerId     uniqueidentifier NOT NULL,

    StartsAtUtc    datetime2(0)     NOT NULL,
    EndsAtUtc      datetime2(0)     NOT NULL,   -- materialised, not computed at query time

    Status         varchar(20)      NOT NULL,
    IdempotencyKey varchar(64)      NULL,
    CreatedAtUtc   datetime2(3)     NOT NULL CONSTRAINT DF_Appointments_Created DEFAULT SYSUTCDATETIME(),
    Version        int              NOT NULL CONSTRAINT DF_Appointments_Version DEFAULT 0,

    CONSTRAINT CK_Appointments_Interval CHECK (EndsAtUtc > StartsAtUtc),
    CONSTRAINT CK_Appointments_Status CHECK (
        Status IN ('Confirmed', 'InProgress', 'Completed', 'Cancelled', 'NoShow')),

    CONSTRAINT FK_Appointments_ServiceType FOREIGN KEY (ServiceTypeId) REFERENCES ServiceTypes(Id),

    -- composite FKs: the bay and the technician must belong to the same dealership as the appointment
    CONSTRAINT FK_Appointments_Bay FOREIGN KEY (ServiceBayId, DealershipId)
        REFERENCES ServiceBays(Id, DealershipId),
    CONSTRAINT FK_Appointments_Technician FOREIGN KEY (TechnicianId, DealershipId)
        REFERENCES Technicians(Id, DealershipId),

    -- composite FK: the vehicle must actually belong to the customer on the appointment
    CONSTRAINT FK_Appointments_Vehicle FOREIGN KEY (VehicleId, CustomerId)
        REFERENCES Vehicles(Id, CustomerId)
);
```

### Indexes

```sql
-- the overlap query for a bay; the two active statuses are the only ones that occupy a resource
CREATE NONCLUSTERED INDEX IX_Appointments_Bay_Window
    ON Appointments (ServiceBayId, StartsAtUtc)
    INCLUDE (EndsAtUtc)
    WHERE Status IN ('Confirmed', 'InProgress');

CREATE NONCLUSTERED INDEX IX_Appointments_Technician_Window
    ON Appointments (TechnicianId, StartsAtUtc)
    INCLUDE (EndsAtUtc)
    WHERE Status IN ('Confirmed', 'InProgress');

-- day view for a dealership
CREATE NONCLUSTERED INDEX IX_Appointments_Dealership_Window
    ON Appointments (DealershipId, StartsAtUtc) INCLUDE (EndsAtUtc, Status);

-- a repeated booking request must not create a second appointment
CREATE UNIQUE NONCLUSTERED INDEX UX_Appointments_IdempotencyKey
    ON Appointments (IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
```

---

## 3. Decisions worth defending

### 3.1 `EndsAtUtc` is stored, not derived

`ServiceTypes.DurationMinutes` is the source of truth when an appointment is *created*, but the end
time is written onto the row. Deriving it at query time (`JOIN ServiceTypes ... DATEADD(...)`) would
mean the overlap predicate is computed per row, so no index can serve it and every availability check
degrades into a scan.

A second, business-level benefit: when someone edits a service type from 60 to 90 minutes, existing
appointments keep the duration they were booked with. That is the correct behaviour, and storing the
column gives it for free.

### 3.2 `DealershipId` is duplicated on `Appointments` — deliberately

It is derivable through `ServiceBayId`, so this is denormalisation. It buys two things:

- the "show me today's schedule for this dealership" query hits one index with no joins;
- the **composite foreign keys** above become possible, which structurally prevent an appointment
  that pairs a bay from dealership A with a technician from dealership B. Without them that pairing
  is a silent data-corruption bug that no unit test will catch.

The same trick on `(VehicleId, CustomerId)` blocks booking someone else's car.

### 3.3 Status participates in the index filter

A cancelled appointment must stop occupying its resources the moment it is cancelled. Putting
`WHERE Status IN ('Confirmed', 'InProgress')` in the filtered index means the availability query and
the index agree on the definition of "occupied". `IN` is used rather than `<> 'Cancelled'` because the
optimiser matches an `IN` filter far more reliably.

### 3.4 No slot table

Time is continuous here: `Appointments` stores real intervals and overlap is evaluated with
`StartsAtUtc < @end AND EndsAtUtc > @start`. A discrete slot table is only worth introducing if the
booking guarantee has to be enforced declaratively by a unique index (see the concurrency section of
the design document).

### 3.5 One concurrency token: a plain `int Version`

`ServiceBays`, `Technicians` and `Appointments` each carry a `Version int` column, mapped with
`IsConcurrencyToken()`. SQL Server's native `rowversion` is deliberately **not** used anywhere.

**Why `rowversion` loses.** It is store-generated: the engine bumps it when a row is updated, and EF
Core never writes to it. That is fine for detecting a lost update, but the booking guard needs
something else — it must **deliberately issue an `UPDATE`** against an otherwise unchanged parent row
in order to acquire that row's exclusive lock. EF Core only emits an `UPDATE` when a writable property
actually changed, and `rowversion` is not writable. A column the application controls is therefore not
a stylistic preference; it is what makes the mechanism work.

Given that one table must use `int`, using it everywhere is better than running two schemes and having
to explain which applies where.

**The failure mode, and how it is closed.** A plain `int` only protects anything if it is incremented
on every write. EF adds the original value to the `WHERE` clause automatically, but if nothing ever
changes the value, the predicate always matches and the check is silently inert.

So the increment is not left to callers. It happens once, inside `AppointmentDbContext` itself:

```csharp
// Domain
public interface IVersioned { int Version { get; set; } }

// Infrastructure — AppointmentDbContext
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

The override, rather than `UnitOfWork`, is deliberate. `UnitOfWork` covers the intended path but not
the dangerous one: a repository inside Infrastructure holds the context and can call
`SaveChangesAsync` on it directly, bypassing the unit of work and disabling the guard with no error and
no test failure. Overriding the context means **every** write path — including that repository, and
including the integration tests' own data builder — passes through the same code.

**How the booking guard forces the lock.** The Application layer never touches EF, so "take the lock"
is expressed as a repository method the booking slice declares:

```csharp
// Application/Features/Booking/IAppointmentRepository.cs
Task LockResourcesAsync(Guid serviceBayId, Guid technicianId, CancellationToken ct);
```

The implementation marks both rows modified — in ascending id order, so two concurrent transactions
cannot lock them in opposite directions and deadlock — and saves. The override above supplies the
increment, and SQL Server supplies the exclusive row locks.

Only entities that are updated after creation carry a version. Reference data — dealerships, skills,
service types, customers, vehicles — is not edited by this system and needs none.

### 3.6 Time zone handling

`datetime2` stores no offset, so UTC is a **convention the application must enforce**, not something
the database guarantees. `Dealerships.TimeZoneId` holds an IANA id; opening hours are local wall-clock
`time` values and are converted against that zone when validating a requested slot.

### 3.7 VIN is the identity; the plate is a label

`Vin` is `NOT NULL UNIQUE`: a 17-character manufacturer-assigned identifier that stays with the car for
its whole life and is the same across every dealer system. That is what makes it the join key when
records from different systems have to be reconciled.

`LicensePlate` is nullable and **not** unique, on purpose:

- A newly delivered car has no plate until it is registered.
- Plates change on resale and transfer between vehicles in some markets, so uniqueness would reject
  legitimate data — and historical rows would claim a plate the car no longer has.

It is stored because it is how a service advisor and a customer actually identify a car at the counter.
No index yet: nothing in scope searches by plate, and an unused index is pure write cost. Add one when
a lookup endpoint exists.

---

## 4. PostgreSQL equivalents — considered, not chosen

SQL Server 2022 is the chosen engine (`technical-plan.md` §1.1). This table records what the
alternative would have looked like, and in particular the one capability that was given up: an
exclusion constraint that makes overlapping appointments impossible at the storage layer, rather than
prevented by application code inside a transaction.

| SQL Server | PostgreSQL |
| --- | --- |
| `uniqueidentifier` | `uuid` |
| `datetime2(0)` + UTC convention | `timestamptz` — stores the instant, conversion is native |
| `nvarchar(n)` | `text` or `varchar(n)` |
| `bit` | `boolean` |
| `int Version` (application-controlled) | identical — the choice in 3.5 is engine-independent |
| Filtered index `WHERE ...` | Partial index `WHERE ...` — same idea |
| Overlap prevented by locking in the transaction | Can additionally be prevented declaratively: `EXCLUDE USING gist (ServiceBayId WITH =, tstzrange(StartsAtUtc, EndsAtUtc) WITH &&) WHERE (Status <> 'Cancelled')` |

---

## 5. Assumptions recorded

1. A technician belongs to exactly one dealership; no sharing across locations.
2. Technician working hours equal dealership opening hours — shifts and leave are out of scope.
3. No buffer or clean-up time between appointments; intervals are half-open `[start, end)`.
4. Vehicle ownership is captured at booking time; a later change of owner does not rewrite history.
5. Pricing, parts, invoicing and loan cars are out of scope.
6. No notification is sent on confirmation — the `201 Created` response *is* the confirmation. There is
   therefore no outbox table: a transactional outbox exists to publish messages reliably, and this
   system publishes none.
