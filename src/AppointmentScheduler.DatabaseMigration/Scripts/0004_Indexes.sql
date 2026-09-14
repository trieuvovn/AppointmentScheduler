CREATE NONCLUSTERED INDEX IX_Appointments_Bay_Window
    ON Appointments (ServiceBayId, StartsAtUtc)
    INCLUDE (EndsAtUtc)
    WHERE Status IN ('Confirmed', 'InProgress');
GO

CREATE NONCLUSTERED INDEX IX_Appointments_Technician_Window
    ON Appointments (TechnicianId, StartsAtUtc)
    INCLUDE (EndsAtUtc)
    WHERE Status IN ('Confirmed', 'InProgress');
GO

CREATE NONCLUSTERED INDEX IX_Appointments_Dealership_Window
    ON Appointments (DealershipId, StartsAtUtc)
    INCLUDE (EndsAtUtc, Status);
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_Appointments_IdempotencyKey
    ON Appointments (IdempotencyKey)
    WHERE IdempotencyKey IS NOT NULL;
GO
