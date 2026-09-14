INSERT INTO ServiceTypes (Id, Code, Name, DurationMinutes, IsActive)
SELECT Id, Code, Name, DurationMinutes, IsActive
FROM (VALUES
    ('3c2b0d50-0000-4000-8000-000000000001', 'OIL_CHANGE',        N'Oil and filter change',       30, 1),
    ('3c2b0d50-0000-4000-8000-000000000002', 'ANNUAL_SERVICE',    N'Annual service',              120, 1),
    ('3c2b0d50-0000-4000-8000-000000000003', 'BRAKE_REPLACEMENT', N'Brake pad replacement',        90, 1),
    ('3c2b0d50-0000-4000-8000-000000000004', 'WHEEL_ALIGNMENT',   N'Four-wheel alignment',         60, 1),
    ('3c2b0d50-0000-4000-8000-000000000005', 'AC_REGAS',          N'Air conditioning re-gas',      45, 1),
    ('3c2b0d50-0000-4000-8000-000000000006', 'DIAGNOSTIC',        N'Engine diagnostic',            60, 1),
    ('3c2b0d50-0000-4000-8000-000000000007', 'EV_BATTERY_CHECK',  N'EV battery health check',      240, 1)
) AS Source(Id, Code, Name, DurationMinutes, IsActive)
WHERE NOT EXISTS (SELECT 1 FROM ServiceTypes st WHERE st.Code = Source.Code);
GO

INSERT INTO ServiceTypeRequiredSkills (ServiceTypeId, SkillId)
SELECT st.Id, sk.Id
FROM (VALUES
    ('OIL_CHANGE',        'GENERAL_SERVICE'),
    ('ANNUAL_SERVICE',    'GENERAL_SERVICE'),
    ('BRAKE_REPLACEMENT', 'BRAKES'),
    ('WHEEL_ALIGNMENT',   'TYRES_ALIGNMENT'),
    ('AC_REGAS',          'AIR_CONDITIONING'),
    ('DIAGNOSTIC',        'ENGINE_DIAGNOSTIC'),
    ('EV_BATTERY_CHECK',  'ELECTRIC_VEHICLE'),
    ('EV_BATTERY_CHECK',  'ENGINE_DIAGNOSTIC')
) AS Source(ServiceTypeCode, SkillCode)
JOIN ServiceTypes st ON st.Code = Source.ServiceTypeCode
JOIN Skills       sk ON sk.Code = Source.SkillCode
WHERE NOT EXISTS (
    SELECT 1 FROM ServiceTypeRequiredSkills x
    WHERE x.ServiceTypeId = st.Id AND x.SkillId = sk.Id);
GO
