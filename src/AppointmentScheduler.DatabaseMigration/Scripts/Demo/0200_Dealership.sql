/*
    0200 — DEMO ONLY. Excluded unless --demo is passed (plan 3.4).

    One dealership, 3 bays, 4 technicians with deliberately differing skills.

    The skill spread is the point. Stage 3 orders candidate technicians by skill count ascending,
    so that the generalist stays free for the job only they can do. With four technicians:

      Alina  — GENERAL_SERVICE only                              (1 skill, always picked first)
      Bao    — GENERAL_SERVICE, BRAKES, TYRES_ALIGNMENT          (3)
      Chidi  — ENGINE_DIAGNOSTIC, AIR_CONDITIONING               (2)
      Dagny  — every skill, including ELECTRIC_VEHICLE           (6, the only EV technician)

    Dagny alone can take EV_BATTERY_CHECK, which is what makes "unqualified technician" a
    reachable rejection path rather than a hypothetical one.

    Opening hours are 08:00-18:00 Monday to Friday and 09:00-13:00 Saturday, closed Sunday.
    A closed day is the absence of a row, not a zero-length row — CK_OpeningHours_Order would
    reject ClosesAt = OpensAt anyway. Saturday closing at 13:00 gives Stage 3 a short window in
    which a 240-minute EV check cannot fit, exercising "service crosses closing".
*/

DECLARE @DealershipId uniqueidentifier = '1a0b0000-0000-4000-8000-00000000000d';

INSERT INTO Dealerships (Id, Name, TimeZoneId)
SELECT @DealershipId, N'Keyloop Motors — Reading', 'Europe/London'
WHERE NOT EXISTS (SELECT 1 FROM Dealerships WHERE Id = @DealershipId);

INSERT INTO DealershipOpeningHours (DealershipId, DayOfWeek, OpensAt, ClosesAt)
SELECT @DealershipId, DayOfWeek, OpensAt, ClosesAt
FROM (VALUES
    (1, '08:00', '18:00'),   -- Monday
    (2, '08:00', '18:00'),
    (3, '08:00', '18:00'),
    (4, '08:00', '18:00'),
    (5, '08:00', '18:00'),   -- Friday
    (6, '09:00', '13:00')    -- Saturday; Sunday (0) has no row and is therefore closed
) AS Source(DayOfWeek, OpensAt, ClosesAt)
WHERE NOT EXISTS (
    SELECT 1 FROM DealershipOpeningHours h
    WHERE h.DealershipId = @DealershipId AND h.DayOfWeek = Source.DayOfWeek);
GO

DECLARE @DealershipId uniqueidentifier = '1a0b0000-0000-4000-8000-00000000000d';

INSERT INTO ServiceBays (Id, DealershipId, Code, IsActive)
SELECT Id, @DealershipId, Code, 1
FROM (VALUES
    ('4d3c0e60-0000-4000-8000-000000000001', N'BAY-1'),
    ('4d3c0e60-0000-4000-8000-000000000002', N'BAY-2'),
    ('4d3c0e60-0000-4000-8000-000000000003', N'BAY-3')
) AS Source(Id, Code)
WHERE NOT EXISTS (
    SELECT 1 FROM ServiceBays b
    WHERE b.DealershipId = @DealershipId AND b.Code = Source.Code);

INSERT INTO Technicians (Id, DealershipId, FullName, IsActive)
SELECT Id, @DealershipId, FullName, 1
FROM (VALUES
    ('5e4d0f70-0000-4000-8000-000000000001', N'Alina Petrova'),
    ('5e4d0f70-0000-4000-8000-000000000002', N'Bao Nguyen'),
    ('5e4d0f70-0000-4000-8000-000000000003', N'Chidi Okafor'),
    ('5e4d0f70-0000-4000-8000-000000000004', N'Dagny Holt')
) AS Source(Id, FullName)
WHERE NOT EXISTS (SELECT 1 FROM Technicians t WHERE t.Id = Source.Id);
GO

INSERT INTO TechnicianSkills (TechnicianId, SkillId)
SELECT t.Id, sk.Id
FROM (VALUES
    -- Alina: the generalist with the narrowest skill set, so the selection policy picks her first.
    ('5e4d0f70-0000-4000-8000-000000000001', 'GENERAL_SERVICE'),

    ('5e4d0f70-0000-4000-8000-000000000002', 'GENERAL_SERVICE'),
    ('5e4d0f70-0000-4000-8000-000000000002', 'BRAKES'),
    ('5e4d0f70-0000-4000-8000-000000000002', 'TYRES_ALIGNMENT'),

    ('5e4d0f70-0000-4000-8000-000000000003', 'ENGINE_DIAGNOSTIC'),
    ('5e4d0f70-0000-4000-8000-000000000003', 'AIR_CONDITIONING'),

    -- Dagny holds everything, and is the only technician who can take an EV_BATTERY_CHECK.
    ('5e4d0f70-0000-4000-8000-000000000004', 'GENERAL_SERVICE'),
    ('5e4d0f70-0000-4000-8000-000000000004', 'ENGINE_DIAGNOSTIC'),
    ('5e4d0f70-0000-4000-8000-000000000004', 'BRAKES'),
    ('5e4d0f70-0000-4000-8000-000000000004', 'TYRES_ALIGNMENT'),
    ('5e4d0f70-0000-4000-8000-000000000004', 'AIR_CONDITIONING'),
    ('5e4d0f70-0000-4000-8000-000000000004', 'ELECTRIC_VEHICLE')
) AS Source(TechnicianId, SkillCode)
JOIN Technicians t ON t.Id = CAST(Source.TechnicianId AS uniqueidentifier)
JOIN Skills     sk ON sk.Code = Source.SkillCode
WHERE NOT EXISTS (
    SELECT 1 FROM TechnicianSkills x
    WHERE x.TechnicianId = t.Id AND x.SkillId = sk.Id);
GO
