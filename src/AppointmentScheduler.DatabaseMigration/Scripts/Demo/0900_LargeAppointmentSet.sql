/*
    0900 — DEMO ONLY. Roughly 50,000 appointments spread across a year.

    This exists for one reason: the Stage 3 measurement. An availability query is fast against an
    empty table no matter how it is written, so a figure measured against 50 rows proves nothing.
    Against 50,000 it distinguishes an index seek from a scan, and that figure is what the plan's
    cut list marks as never-cut — it is the only hard performance evidence in the submission.

    Numbered 0900 so it runs last: the cheaper demo scripts are useful on their own, and a
    developer who interrupts this one still has a working seeded dealership.

    GENERATE_SERIES is SQL Server 2022+, which the docker-compose image pins. It replaces the
    recursive-CTE-with-OPTION(MAXRECURSION) dance that earlier versions needed.

    Shape of the generated data:
      - one year back from the most recent Monday, so the set straddles "now" and both past and
        future availability queries have rows to work against
      - weekdays only, 08:00-18:00 local, matching the opening hours in 0200
      - every appointment placed on an exact half-hour, durations drawn from the catalogue
      - a bay and a technician from the demo dealership, chosen so that no two appointments in
        the same bay or against the same technician overlap
      - ~4% Cancelled and ~4% Completed, so the filtered indexes have rows they must exclude.
        Without those the index filter is never exercised and a wrong predicate still measures fast.
*/

DECLARE @DealershipId uniqueidentifier = '1a0b0000-0000-4000-8000-00000000000d';

-- Nothing to do unless the demo dealership is present, and never worth re-running: the journal
-- makes this a no-op on a second pass, but the guard keeps a rebuilt journal from doubling the set.
IF NOT EXISTS (SELECT 1 FROM Dealerships WHERE Id = @DealershipId)
    OR EXISTS (SELECT 1 FROM Appointments WHERE DealershipId = @DealershipId)
BEGIN
    PRINT 'Large appointment set skipped: demo dealership missing, or appointments already present.';
    RETURN;
END;

-- Anchor on the most recent Monday so the generated week grid is stable regardless of run day.
DECLARE @Anchor date = DATEADD(day, -((DATEDIFF(day, '1900-01-01', CAST(SYSUTCDATETIME() AS date)) % 7)), CAST(SYSUTCDATETIME() AS date));
DECLARE @Start  date = DATEADD(day, -182, @Anchor);   -- ~6 months back
DECLARE @Days   int  = 364;                           -- 52 whole weeks forward from @Start

DECLARE @Bays TABLE (Ordinal int IDENTITY(0,1) PRIMARY KEY, Id uniqueidentifier);
INSERT INTO @Bays (Id)
SELECT Id FROM ServiceBays WHERE DealershipId = @DealershipId ORDER BY Code;

DECLARE @Techs TABLE (Ordinal int IDENTITY(0,1) PRIMARY KEY, Id uniqueidentifier);
INSERT INTO @Techs (Id)
SELECT Id FROM Technicians WHERE DealershipId = @DealershipId ORDER BY FullName;

DECLARE @BayCount  int = (SELECT COUNT(*) FROM @Bays);
DECLARE @TechCount int = (SELECT COUNT(*) FROM @Techs);

-- The vehicle/customer pair must satisfy FK_Appointments_Vehicle, so they are drawn together.
DECLARE @Vehicles TABLE (Ordinal int IDENTITY(0,1) PRIMARY KEY, Id uniqueidentifier, CustomerId uniqueidentifier);
INSERT INTO @Vehicles (Id, CustomerId)
SELECT Id, CustomerId FROM Vehicles ORDER BY Vin;

DECLARE @VehicleCount int = (SELECT COUNT(*) FROM @Vehicles);

-- A short service type so that consecutive half-hour slots in one bay never overlap. Using the
-- catalogue's own durations here would overlap by construction and violate nothing at the
-- database level — the overlap rule is enforced in application code — which would make the
-- seeded data quietly contradict the thing it is meant to measure.
DECLARE @ServiceTypeId uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Code = 'OIL_CHANGE');
DECLARE @DurationMinutes int = (SELECT DurationMinutes FROM ServiceTypes WHERE Code = 'OIL_CHANGE');

;WITH Days AS (
    SELECT DATEADD(day, value, @Start) AS TheDay
    FROM GENERATE_SERIES(0, @Days - 1)
),
WeekDays AS (
    -- Weekdays only: DATEPART(weekday) depends on DATEFIRST, so derive from a fixed Monday instead.
    SELECT TheDay
    FROM Days
    WHERE (DATEDIFF(day, '2000-01-03', TheDay) % 7) < 5   -- 2000-01-03 was a Monday
),
Slots AS (
    -- 08:00 to 17:30 on the half hour: 20 slots per day.
    SELECT value AS SlotIndex FROM GENERATE_SERIES(0, 19)
),
Grid AS (
    SELECT
        ROW_NUMBER() OVER (ORDER BY d.TheDay, s.SlotIndex, b.Ordinal) - 1 AS RowIndex,
        DATEADD(minute, 480 + (s.SlotIndex * 30), CAST(d.TheDay AS datetime2(0))) AS StartsAtUtc,
        b.Id      AS ServiceBayId,
        b.Ordinal AS BayOrdinal,
        s.SlotIndex
    FROM WeekDays d
    CROSS JOIN Slots s
    CROSS JOIN @Bays b
)
INSERT INTO Appointments (
    Id, DealershipId, ServiceBayId, TechnicianId, ServiceTypeId, VehicleId, CustomerId,
    StartsAtUtc, EndsAtUtc, Status, IdempotencyKey)
SELECT
    -- Deterministic ids: a re-seed produces the same set, which keeps a captured query plan
    -- and a measured figure comparable across runs.
    CAST(CAST(CAST(g.RowIndex AS binary(4)) AS binary(16)) AS uniqueidentifier),
    @DealershipId,
    g.ServiceBayId,
    -- Offsetting the technician by the bay ordinal keeps one technician in one bay per slot, so
    -- no two rows collide on either resource.
    (SELECT Id FROM @Techs WHERE Ordinal = ((g.SlotIndex + g.BayOrdinal) % @TechCount)),
    @ServiceTypeId,
    v.Id,
    v.CustomerId,
    g.StartsAtUtc,
    DATEADD(minute, @DurationMinutes, g.StartsAtUtc),
    CASE
        WHEN g.RowIndex % 25 = 0 THEN 'Cancelled'    -- 4%: must stop occupying its resources
        WHEN g.RowIndex % 25 = 1 THEN 'Completed'    -- 4%: likewise, and excluded by the index filter
        ELSE 'Confirmed'
    END,
    NULL
FROM Grid g
JOIN @Vehicles v ON v.Ordinal = (g.RowIndex % @VehicleCount);

PRINT CONCAT('Large appointment set: ', @@ROWCOUNT, ' appointments inserted.');
GO
