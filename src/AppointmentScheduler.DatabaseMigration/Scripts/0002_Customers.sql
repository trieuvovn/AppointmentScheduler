CREATE TABLE Customers (
    Id       uniqueidentifier NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    FullName nvarchar(200)    NOT NULL,
    Email    nvarchar(256)    NULL,
    Phone    nvarchar(32)     NULL
);
GO

CREATE TABLE Vehicles (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_Vehicles PRIMARY KEY,
    CustomerId   uniqueidentifier NOT NULL,
    Vin          char(17)         NOT NULL CONSTRAINT UQ_Vehicles_Vin UNIQUE,
    LicensePlate nvarchar(16)     NULL,
    Make         nvarchar(60)     NOT NULL,
    Model        nvarchar(60)     NOT NULL,
    ModelYear    smallint         NULL,
    CONSTRAINT FK_Vehicles_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
    CONSTRAINT UQ_Vehicles_IdCustomer UNIQUE (Id, CustomerId)
);
GO
