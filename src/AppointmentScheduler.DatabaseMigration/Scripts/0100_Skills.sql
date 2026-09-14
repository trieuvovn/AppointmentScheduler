INSERT INTO Skills (Id, Code, Name)
SELECT Id, Code, Name
FROM (VALUES
    ('2f1a0c40-0000-4000-8000-000000000001', 'GENERAL_SERVICE',  N'General service'),
    ('2f1a0c40-0000-4000-8000-000000000002', 'ENGINE_DIAGNOSTIC', N'Engine diagnostics'),
    ('2f1a0c40-0000-4000-8000-000000000003', 'BRAKES',            N'Brake systems'),
    ('2f1a0c40-0000-4000-8000-000000000004', 'TYRES_ALIGNMENT',   N'Tyres and wheel alignment'),
    ('2f1a0c40-0000-4000-8000-000000000005', 'AIR_CONDITIONING',  N'Air conditioning'),
    ('2f1a0c40-0000-4000-8000-000000000006', 'ELECTRIC_VEHICLE',  N'High-voltage / EV systems')
) AS Source(Id, Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM Skills s WHERE s.Code = Source.Code);
GO
