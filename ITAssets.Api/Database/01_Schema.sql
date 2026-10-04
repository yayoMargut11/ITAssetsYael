/* 01_Schema.sql - Tablas, índices y sp_AssignAsset. Ejecutar primero (después de CREATE DATABASE ITAssetsDb). */
USE ITAssetsDb;
GO

CREATE TABLE Roles (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(50) NOT NULL UNIQUE);

CREATE TABLE Users (
  Id INT IDENTITY PRIMARY KEY,
  Username NVARCHAR(100) NOT NULL UNIQUE,
  PasswordHash NVARCHAR(200) NOT NULL,          -- BCrypt
  RoleId INT NOT NULL REFERENCES Roles(Id),
  IsActive BIT NOT NULL DEFAULT 1,
  FailedAttempts INT NOT NULL DEFAULT 0,
  LockoutEnd DATETIME2 NULL
);

CREATE TABLE Suppliers (
  Id INT IDENTITY PRIMARY KEY,
  Name NVARCHAR(150) NOT NULL UNIQUE,
  Email NVARCHAR(150) NULL,
  Phone NVARCHAR(30) NULL,
  IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE SupplierServices (
  SupplierId INT NOT NULL REFERENCES Suppliers(Id),
  ServiceType NVARCHAR(30) NOT NULL
    CHECK (ServiceType IN ('Compra','Mantenimiento','Arrendamiento')),
  PRIMARY KEY (SupplierId, ServiceType)
);

CREATE TABLE Employees (
  Id INT IDENTITY PRIMARY KEY,
  EmployeeNumber NVARCHAR(30) NOT NULL UNIQUE,
  FullName NVARCHAR(150) NOT NULL,
  Email NVARCHAR(150) NOT NULL,
  IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Assets (
  Id INT IDENTITY PRIMARY KEY,
  AssetCode NVARCHAR(30) NOT NULL UNIQUE,
  SerialNumber NVARCHAR(100) NULL,
  Category NVARCHAR(50) NOT NULL,
  Brand NVARCHAR(50) NOT NULL,
  Model NVARCHAR(80) NOT NULL,
  OwnershipType NVARCHAR(20) NOT NULL CHECK (OwnershipType IN ('Propio','Arrendado')),
  SupplierId INT NULL REFERENCES Suppliers(Id),
  Status NVARCHAR(20) NOT NULL DEFAULT 'Disponible'
    CHECK (Status IN ('Disponible','Asignado','Mantenimiento','Retirado')),
  CurrentLocation NVARCHAR(100) NULL,
  PurchaseDate DATE NULL,
  RentalEndDate DATE NULL,
  CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  RowVer ROWVERSION,
  CONSTRAINT CK_Assets_Rental CHECK (OwnershipType <> 'Arrendado' OR SupplierId IS NOT NULL)
);
CREATE UNIQUE INDEX UX_Assets_Serial ON Assets(SerialNumber) WHERE SerialNumber IS NOT NULL;

CREATE TABLE Assignments (
  Id INT IDENTITY PRIMARY KEY,
  AssetId INT NOT NULL REFERENCES Assets(Id),
  EmployeeId INT NOT NULL REFERENCES Employees(Id),
  AssignedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  AssignedByUserId INT NOT NULL REFERENCES Users(Id),
  ReturnedAt DATETIME2 NULL,
  ReturnedByUserId INT NULL REFERENCES Users(Id),
  ReturnCondition NVARCHAR(200) NULL,
  Notes NVARCHAR(500) NULL
);
-- Garantía final: solo una asignación activa por activo
CREATE UNIQUE INDEX UX_Assignments_ActiveAsset ON Assignments(AssetId) WHERE ReturnedAt IS NULL;

CREATE TABLE AssetMovements (
  Id BIGINT IDENTITY PRIMARY KEY,
  AssetId INT NOT NULL REFERENCES Assets(Id),
  MovementType NVARCHAR(30) NOT NULL,   -- Alta, Asignacion, Devolucion, CambioEstado, CambioUbicacion
  PreviousValue NVARCHAR(100) NULL,
  NewValue NVARCHAR(100) NULL,
  EmployeeId INT NULL,
  PerformedByUserId INT NOT NULL REFERENCES Users(Id),
  PerformedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  Notes NVARCHAR(500) NULL
);
CREATE INDEX IX_Movements_Asset ON AssetMovements(AssetId, PerformedAt DESC);
GO

CREATE OR ALTER PROCEDURE dbo.sp_AssignAsset
  @AssetId INT, @EmployeeId INT, @UserId INT, @Notes NVARCHAR(500) = NULL
AS
BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY
    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20), @EmpActive BIT;

    -- Bloquea la fila del activo: el segundo usuario espera aquí
    SELECT @Status = Status FROM Assets WITH (UPDLOCK, HOLDLOCK) WHERE Id = @AssetId;
    IF @Status IS NULL THROW 50001, 'ASSET_NOT_FOUND', 1;
    IF @Status <> 'Disponible' THROW 50002, 'ASSET_NOT_AVAILABLE', 1;

    SELECT @EmpActive = IsActive FROM Employees WHERE Id = @EmployeeId;
    IF @EmpActive IS NULL THROW 50003, 'EMPLOYEE_NOT_FOUND', 1;
    IF @EmpActive = 0 THROW 50004, 'EMPLOYEE_INACTIVE', 1;

    INSERT Assignments (AssetId, EmployeeId, AssignedByUserId, Notes)
    VALUES (@AssetId, @EmployeeId, @UserId, @Notes);
    DECLARE @AssignmentId INT = SCOPE_IDENTITY();

    UPDATE Assets SET Status = 'Asignado', UpdatedAt = SYSUTCDATETIME() WHERE Id = @AssetId;

    INSERT AssetMovements (AssetId, MovementType, PreviousValue, NewValue, EmployeeId, PerformedByUserId, Notes)
    VALUES (@AssetId, 'Asignacion', 'Disponible', 'Asignado', @EmployeeId, @UserId, @Notes);

    COMMIT;
    SELECT @AssignmentId AS AssignmentId;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
  END CATCH
END
GO
