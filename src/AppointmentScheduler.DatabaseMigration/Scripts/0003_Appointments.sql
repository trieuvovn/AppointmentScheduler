CREATE TABLE Appointments (
    Id             uniqueidentifier NOT NULL CONSTRAINT PK_Appointments PRIMARY KEY,
    DealershipId   uniqueidentifier NOT NULL,
    ServiceBayId   uniqueidentifier NOT NULL,
    TechnicianId   uniqueidentifier NOT NULL,
    ServiceTypeId  uniqueidentifier NOT NULL,
    VehicleId      uniqueidentifier NOT NULL,
    CustomerId     uniqueidentifier NOT NULL,
    StartsAtUtc    datetime2(0)     NOT NULL,
    EndsAtUtc      datetime2(0)     NOT NULL,

    Status         varchar(20)      NOT NULL,
    IdempotencyKey varchar(64)      NULL,
    CreatedAtUtc   datetime2(3)     NOT NULL CONSTRAINT DF_Appointments_Created DEFAULT SYSUTCDATETIME(),
    Version        int              NOT NULL CONSTRAINT DF_Appointments_Version DEFAULT 0,

    CONSTRAINT CK_Appointments_Interval CHECK (EndsAtUtc > StartsAtUtc),
    CONSTRAINT CK_Appointments_Status CHECK (
        Status IN ('Confirmed', 'InProgress', 'Completed', 'Cancelled', 'NoShow')),

    CONSTRAINT FK_Appointments_ServiceType FOREIGN KEY (ServiceTypeId) REFERENCES ServiceTypes(Id),

    -- The bay and the technician must belong to the same dealership as the appointment. Without
    -- these, pairing a bay from dealership A with a technician from dealership B is a silent
    -- data-corruption bug that no unit test would catch.
    CONSTRAINT FK_Appointments_Bay FOREIGN KEY (ServiceBayId, DealershipId)
        REFERENCES ServiceBays(Id, DealershipId),
    CONSTRAINT FK_Appointments_Technician FOREIGN KEY (TechnicianId, DealershipId)
        REFERENCES Technicians(Id, DealershipId),

    -- The same trick on (VehicleId, CustomerId) blocks booking someone else's car.
    CONSTRAINT FK_Appointments_Vehicle FOREIGN KEY (VehicleId, CustomerId)
        REFERENCES Vehicles(Id, CustomerId)
);
GO
