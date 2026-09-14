/*
    0201 — DEMO ONLY. Ten customers, one vehicle each.

    VINs are synthetic but structurally valid: 17 characters, and excluding I, O and Q exactly as
    the real standard does. Vin is char(17) and UNIQUE, so a short value would be space-padded
    into a silent mismatch rather than rejected — writing them out at full width is deliberate.

    Customer 10 owns an EV, which is the only vehicle for which EV_BATTERY_CHECK makes sense and
    therefore the one that exercises the single-qualified-technician path end to end.
*/

INSERT INTO Customers (Id, FullName, Email, Phone)
SELECT Id, FullName, Email, Phone
FROM (VALUES
    ('6f5e1080-0000-4000-8000-000000000001', N'Harriet Vance',   N'harriet.vance@example.com',   N'+44 7700 900001'),
    ('6f5e1080-0000-4000-8000-000000000002', N'Marcus Bell',     N'marcus.bell@example.com',     N'+44 7700 900002'),
    ('6f5e1080-0000-4000-8000-000000000003', N'Priya Raman',     N'priya.raman@example.com',     N'+44 7700 900003'),
    ('6f5e1080-0000-4000-8000-000000000004', N'Tomas Lindqvist', N'tomas.lindqvist@example.com', N'+44 7700 900004'),
    ('6f5e1080-0000-4000-8000-000000000005', N'Grace Adeyemi',   N'grace.adeyemi@example.com',   N'+44 7700 900005'),
    ('6f5e1080-0000-4000-8000-000000000006', N'Liam Doherty',    N'liam.doherty@example.com',    N'+44 7700 900006'),
    ('6f5e1080-0000-4000-8000-000000000007', N'Sofia Marchetti', N'sofia.marchetti@example.com', N'+44 7700 900007'),
    ('6f5e1080-0000-4000-8000-000000000008', N'Wei Zhang',       N'wei.zhang@example.com',       N'+44 7700 900008'),
    ('6f5e1080-0000-4000-8000-000000000009', N'Amara Nwosu',     N'amara.nwosu@example.com',     N'+44 7700 900009'),
    ('6f5e1080-0000-4000-8000-00000000000a', N'Jonas Keller',    N'jonas.keller@example.com',    N'+44 7700 900010')
) AS Source(Id, FullName, Email, Phone)
WHERE NOT EXISTS (SELECT 1 FROM Customers c WHERE c.Id = CAST(Source.Id AS uniqueidentifier));
GO

INSERT INTO Vehicles (Id, CustomerId, Vin, LicensePlate, Make, Model, ModelYear)
SELECT Id, CustomerId, Vin, LicensePlate, Make, Model, ModelYear
FROM (VALUES
    ('7a6f1190-0000-4000-8000-000000000001', '6f5e1080-0000-4000-8000-000000000001', 'WVWZZZ1KZAW000001', N'RD21 AAA', N'Volkswagen', N'Golf',     2021),
    ('7a6f1190-0000-4000-8000-000000000002', '6f5e1080-0000-4000-8000-000000000002', 'WF0AXXGCDA1000002', N'RD19 BBB', N'Ford',       N'Focus',    2019),
    ('7a6f1190-0000-4000-8000-000000000003', '6f5e1080-0000-4000-8000-000000000003', 'SJNFAAZE1UA000003', N'RD22 CCC', N'Nissan',     N'Qashqai',  2022),
    ('7a6f1190-0000-4000-8000-000000000004', '6f5e1080-0000-4000-8000-000000000004', 'VF1RFA00X54000004', N'RD18 DDD', N'Renault',    N'Clio',     2018),
    -- No plate: a delivered car that is not yet registered. LicensePlate is nullable for exactly this.
    ('7a6f1190-0000-4000-8000-000000000005', '6f5e1080-0000-4000-8000-000000000005', 'WBA8E9G50GNU00005', NULL,       N'BMW',        N'3 Series', 2023),
    ('7a6f1190-0000-4000-8000-000000000006', '6f5e1080-0000-4000-8000-000000000006', 'WDD2050421F000006', N'RD20 FFF', N'Mercedes',   N'C-Class',  2020),
    ('7a6f1190-0000-4000-8000-000000000007', '6f5e1080-0000-4000-8000-000000000007', 'ZFA33400007000007', N'RD17 GGG', N'Fiat',       N'500',      2017),
    ('7a6f1190-0000-4000-8000-000000000008', '6f5e1080-0000-4000-8000-000000000008', 'JTDKB20U893000008', N'RD16 HHH', N'Toyota',     N'Prius',    2016),
    ('7a6f1190-0000-4000-8000-000000000009', '6f5e1080-0000-4000-8000-000000000009', 'SHHFK2750JU000009', N'RD19 JJJ', N'Honda',      N'Civic',    2019),
    -- The EV. The only vehicle for which EV_BATTERY_CHECK is meaningful.
    ('7a6f1190-0000-4000-8000-00000000000a', '6f5e1080-0000-4000-8000-00000000000a', '5YJ3E1EA7KF000010', N'RD23 KKK', N'Tesla',      N'Model 3',  2023)
) AS Source(Id, CustomerId, Vin, LicensePlate, Make, Model, ModelYear)
WHERE NOT EXISTS (SELECT 1 FROM Vehicles v WHERE v.Id = CAST(Source.Id AS uniqueidentifier));
GO
