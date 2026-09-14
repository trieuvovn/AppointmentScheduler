
CREATE TABLE Dealerships (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_Dealerships PRIMARY KEY,
    Name         nvarchar(200)    NOT NULL,
    TimeZoneId   varchar(64)      NOT NULL,
    CreatedAtUtc datetime2(3)     NOT NULL CONSTRAINT DF_Dealerships_Created DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE DealershipOpeningHours (
    DealershipId uniqueidentifier NOT NULL,
    DayOfWeek    tinyint          NOT NULL,
    OpensAt      time(0)          NOT NULL,
    ClosesAt     time(0)          NOT NULL,
    CONSTRAINT PK_OpeningHours PRIMARY KEY (DealershipId, DayOfWeek),
    CONSTRAINT FK_OpeningHours_Dealership FOREIGN KEY (DealershipId) REFERENCES Dealerships(Id),
    CONSTRAINT CK_OpeningHours_Order CHECK (ClosesAt > OpensAt)
);
GO

CREATE TABLE ServiceBays (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_ServiceBays PRIMARY KEY,
    DealershipId uniqueidentifier NOT NULL,
    Code         nvarchar(20)     NOT NULL,
    IsActive     bit              NOT NULL CONSTRAINT DF_ServiceBays_Active DEFAULT 1,
    Version      int              NOT NULL CONSTRAINT DF_ServiceBays_Version DEFAULT 0,
    CONSTRAINT FK_ServiceBays_Dealership FOREIGN KEY (DealershipId) REFERENCES Dealerships(Id),
    CONSTRAINT UQ_ServiceBays_Code UNIQUE (DealershipId, Code),
    CONSTRAINT UQ_ServiceBays_IdDealership UNIQUE (Id, DealershipId)
);
GO

CREATE TABLE Technicians (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_Technicians PRIMARY KEY,
    DealershipId uniqueidentifier NOT NULL,
    FullName     nvarchar(200)    NOT NULL,
    IsActive     bit              NOT NULL CONSTRAINT DF_Technicians_Active DEFAULT 1,
    Version      int              NOT NULL CONSTRAINT DF_Technicians_Version DEFAULT 0,
    CONSTRAINT FK_Technicians_Dealership FOREIGN KEY (DealershipId) REFERENCES Dealerships(Id),
    CONSTRAINT UQ_Technicians_IdDealership UNIQUE (Id, DealershipId)
);
GO

CREATE TABLE Skills (
    Id   uniqueidentifier NOT NULL CONSTRAINT PK_Skills PRIMARY KEY,
    Code varchar(40)      NOT NULL CONSTRAINT UQ_Skills_Code UNIQUE,
    Name nvarchar(120)    NOT NULL
);
GO

CREATE TABLE TechnicianSkills (
    TechnicianId uniqueidentifier NOT NULL,
    SkillId      uniqueidentifier NOT NULL,
    CONSTRAINT PK_TechnicianSkills PRIMARY KEY (TechnicianId, SkillId),
    CONSTRAINT FK_TechnicianSkills_Technician FOREIGN KEY (TechnicianId) REFERENCES Technicians(Id),
    CONSTRAINT FK_TechnicianSkills_Skill      FOREIGN KEY (SkillId)      REFERENCES Skills(Id)
);
GO

CREATE TABLE ServiceTypes (
    Id              uniqueidentifier NOT NULL CONSTRAINT PK_ServiceTypes PRIMARY KEY,
    Code            varchar(40)      NOT NULL CONSTRAINT UQ_ServiceTypes_Code UNIQUE,
    Name            nvarchar(200)    NOT NULL,
    DurationMinutes int              NOT NULL,
    IsActive        bit              NOT NULL CONSTRAINT DF_ServiceTypes_Active DEFAULT 1,
    CONSTRAINT CK_ServiceTypes_Duration CHECK (DurationMinutes > 0 AND DurationMinutes <= 8 * 60)
);
GO

CREATE TABLE ServiceTypeRequiredSkills (
    ServiceTypeId uniqueidentifier NOT NULL,
    SkillId       uniqueidentifier NOT NULL,
    CONSTRAINT PK_ServiceTypeRequiredSkills PRIMARY KEY (ServiceTypeId, SkillId),
    CONSTRAINT FK_STRS_ServiceType FOREIGN KEY (ServiceTypeId) REFERENCES ServiceTypes(Id),
    CONSTRAINT FK_STRS_Skill       FOREIGN KEY (SkillId)       REFERENCES Skills(Id)
);
GO
