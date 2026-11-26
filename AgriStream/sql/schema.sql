-- Azure SQL schema (the MVC app creates this automatically on first run via EF Core EnsureCreated;
-- this script is provided as evidence / for manual setup).

CREATE TABLE dbo.Users (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,          -- ASP.NET Core PasswordHasher (PBKDF2), never plain text
    Role         NVARCHAR(20)  NOT NULL,          -- Farmer | Agronomist | Admin
    CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Farmer','Agronomist','Admin'))
);

-- Master device registry: updated by the queue-triggered function
CREATE TABLE dbo.Devices (
    DeviceId    NVARCHAR(20)  NOT NULL PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    FarmName    NVARCHAR(100) NOT NULL,
    Status      NVARCHAR(30)  NOT NULL DEFAULT 'Idle',   -- Idle | Irrigating
    LastCommand NVARCHAR(200) NULL,
    LastUpdated DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
