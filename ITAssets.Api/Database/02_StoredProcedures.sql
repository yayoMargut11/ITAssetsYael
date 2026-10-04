/* =====================================================================
   02_StoredProcedures.sql
   Ejecutar DESPUES del script de tablas y sp_AssignAsset.
   Es re-ejecutable (CREATE OR ALTER).

   CODIGOS DE ERROR DE NEGOCIO (THROW) -> el mensaje es un codigo estable
   que la API traduce a HTTP:
     50001 ASSET_NOT_FOUND                    -> 404
     50002 ASSET_NOT_AVAILABLE                -> 409
     50003 EMPLOYEE_NOT_FOUND                 -> 404
     50004 EMPLOYEE_INACTIVE                  -> 422
     50010 ASSET_CODE_DUPLICATE               -> 409
     50011 SERIAL_DUPLICATE                   -> 409
     50012 SUPPLIER_REQUIRED_FOR_RENTAL       -> 422
     50013 SUPPLIER_NOT_FOUND                 -> 404
     50014 INVALID_VALUE                      -> 400/422
     50020 ASSET_ASSIGNED_RETURN_FIRST        -> 409
     50021 ASSET_RETIRED                      -> 409
     50022 INVALID_STATUS_TRANSITION          -> 422
     50023 CONCURRENCY_CONFLICT               -> 409
     50030 NO_ACTIVE_ASSIGNMENT               -> 409
     50040 EMPLOYEE_NUMBER_DUPLICATE          -> 409
     50041 SUPPLIER_NAME_DUPLICATE            -> 409
     2601/2627 (indice unico)                 -> 409 (red de seguridad)
   ===================================================================== */
USE ITAssetsDb;
GO

/* ---------------------------- AUTH ---------------------------------- */

CREATE OR ALTER PROCEDURE dbo.sp_GetUserByUsername
  @Username NVARCHAR(100)
AS
BEGIN
  SET NOCOUNT ON;
  SELECT u.Id, u.Username, u.PasswordHash, r.Name AS RoleName,
         u.IsActive, u.FailedAttempts, u.LockoutEnd
  FROM Users u JOIN Roles r ON r.Id = u.RoleId
  WHERE u.Username = @Username;
END
GO

-- Incrementa intentos fallidos; al llegar al maximo bloquea la cuenta
CREATE OR ALTER PROCEDURE dbo.sp_RegisterFailedLogin
  @UserId INT,
  @MaxAttempts INT = 5,
  @LockoutMinutes INT = 15
AS
BEGIN
  SET NOCOUNT ON;

  UPDATE dbo.Users
  SET FailedAttempts =
        CASE WHEN LockoutEnd IS NOT NULL AND LockoutEnd <= SYSUTCDATETIME()
             THEN 1                       -- el bloqueo expiró: reinicia contador
             ELSE FailedAttempts + 1 END,
      LockoutEnd =
        CASE WHEN LockoutEnd IS NOT NULL AND LockoutEnd <= SYSUTCDATETIME()
             THEN CASE WHEN 1 >= @MaxAttempts
                       THEN DATEADD(MINUTE, @LockoutMinutes, SYSUTCDATETIME())
                       ELSE NULL END
             WHEN LockoutEnd IS NOT NULL   -- sigue bloqueado: no extender
             THEN LockoutEnd
             WHEN FailedAttempts + 1 >= @MaxAttempts
             THEN DATEADD(MINUTE, @LockoutMinutes, SYSUTCDATETIME())
             ELSE NULL END
  OUTPUT inserted.FailedAttempts, inserted.LockoutEnd
  WHERE Id = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegisterSuccessfulLogin
  @UserId INT
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE Users SET FailedAttempts = 0, LockoutEnd = NULL WHERE Id = @UserId;
END
GO

/* ---------------------------- SUPPLIERS ----------------------------- */

-- @Services: lista separada por comas, ej. 'Compra,Mantenimiento'
CREATE OR ALTER PROCEDURE dbo.sp_CreateSupplier
  @Name NVARCHAR(150), @Email NVARCHAR(150) = NULL, @Phone NVARCHAR(30) = NULL,
  @Services NVARCHAR(200) = NULL
AS
BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY
    IF LTRIM(RTRIM(ISNULL(@Name, ''))) = '' THROW 50014, 'INVALID_VALUE', 1;
    IF EXISTS (SELECT 1 FROM Suppliers WHERE Name = @Name) THROW 50041, 'SUPPLIER_NAME_DUPLICATE', 1;

    -- Valida que todos los servicios sean validos
    IF @Services IS NOT NULL AND EXISTS (
         SELECT 1 FROM STRING_SPLIT(@Services, ',')
         WHERE LTRIM(RTRIM(value)) <> ''
           AND LTRIM(RTRIM(value)) NOT IN ('Compra','Mantenimiento','Arrendamiento'))
      THROW 50014, 'INVALID_VALUE', 1;

    BEGIN TRAN;
    INSERT Suppliers (Name, Email, Phone) VALUES (@Name, @Email, @Phone);
    DECLARE @Id INT = SCOPE_IDENTITY();

    IF @Services IS NOT NULL
      INSERT SupplierServices (SupplierId, ServiceType)
      SELECT DISTINCT @Id, LTRIM(RTRIM(value))
      FROM STRING_SPLIT(@Services, ',') WHERE LTRIM(RTRIM(value)) <> '';
    COMMIT;

    SELECT @Id AS SupplierId;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
  END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetSuppliers
  @Search NVARCHAR(100) = NULL, @Page INT = 1, @PageSize INT = 20
AS
BEGIN
  SET NOCOUNT ON;
  SELECT s.Id, s.Name, s.Email, s.Phone, s.IsActive,
         ISNULL((SELECT STRING_AGG(ss.ServiceType, ',') FROM SupplierServices ss
                 WHERE ss.SupplierId = s.Id), '') AS Services,
         COUNT(*) OVER() AS TotalCount
  FROM Suppliers s
  WHERE @Search IS NULL OR s.Name LIKE '%' + @Search + '%'
  ORDER BY s.Name
  OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetSupplierById
  @Id INT
AS
BEGIN
  SET NOCOUNT ON;
  SELECT s.Id, s.Name, s.Email, s.Phone, s.IsActive,
         ISNULL((SELECT STRING_AGG(ss.ServiceType, ',') FROM SupplierServices ss
                 WHERE ss.SupplierId = s.Id), '') AS Services
  FROM Suppliers s WHERE s.Id = @Id;
END
GO

/* ---------------------------- EMPLOYEES ----------------------------- */

CREATE OR ALTER PROCEDURE dbo.sp_CreateEmployee
  @EmployeeNumber NVARCHAR(30), @FullName NVARCHAR(150), @Email NVARCHAR(150)
AS
BEGIN
  SET NOCOUNT ON;
  IF LTRIM(RTRIM(ISNULL(@EmployeeNumber, ''))) = ''
     OR LTRIM(RTRIM(ISNULL(@FullName, ''))) = ''
     OR LTRIM(RTRIM(ISNULL(@Email, ''))) = ''
    THROW 50014, 'INVALID_VALUE', 1;
  -- Validacion basica de formato (la validacion fuerte se hace en la API)
  IF @Email NOT LIKE '_%@_%._%' OR @Email LIKE '% %' THROW 50014, 'INVALID_VALUE', 1;
  IF EXISTS (SELECT 1 FROM Employees WHERE EmployeeNumber = @EmployeeNumber)
    THROW 50040, 'EMPLOYEE_NUMBER_DUPLICATE', 1;

  INSERT Employees (EmployeeNumber, FullName, Email) VALUES (@EmployeeNumber, @FullName, @Email);
  SELECT SCOPE_IDENTITY() AS EmployeeId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetEmployees
  @Search NVARCHAR(100) = NULL, @IsActive BIT = NULL,
  @Page INT = 1, @PageSize INT = 20
AS
BEGIN
  SET NOCOUNT ON;
  SELECT Id, EmployeeNumber, FullName, Email, IsActive, COUNT(*) OVER() AS TotalCount
  FROM Employees
  WHERE (@IsActive IS NULL OR IsActive = @IsActive)
    AND (@Search IS NULL OR EmployeeNumber LIKE '%' + @Search + '%'
         OR FullName LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')
  ORDER BY FullName
  OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetEmployeeById
  @Id INT
AS
BEGIN
  SET NOCOUNT ON;
  SELECT Id, EmployeeNumber, FullName, Email, IsActive FROM Employees WHERE Id = @Id;
END
GO

-- Util para activar/desactivar colaboradores (y probar la regla de inactivos)
CREATE OR ALTER PROCEDURE dbo.sp_SetEmployeeActive
  @Id INT, @IsActive BIT
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE Employees SET IsActive = @IsActive WHERE Id = @Id;
  IF @@ROWCOUNT = 0 THROW 50003, 'EMPLOYEE_NOT_FOUND', 1;
END
GO

/* ---------------------------- ASSETS -------------------------------- */

CREATE OR ALTER PROCEDURE dbo.sp_CreateAsset
  @AssetCode NVARCHAR(30), @SerialNumber NVARCHAR(100) = NULL,
  @Category NVARCHAR(50), @Brand NVARCHAR(50), @Model NVARCHAR(80),
  @OwnershipType NVARCHAR(20), @SupplierId INT = NULL,
  @CurrentLocation NVARCHAR(100) = NULL,
  @PurchaseDate DATE = NULL, @RentalEndDate DATE = NULL,
  @UserId INT
AS
BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY
    IF @OwnershipType NOT IN ('Propio','Arrendado') THROW 50014, 'INVALID_VALUE', 1;
    IF @OwnershipType = 'Arrendado' AND @SupplierId IS NULL
      THROW 50012, 'SUPPLIER_REQUIRED_FOR_RENTAL', 1;
    IF @SupplierId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Suppliers WHERE Id = @SupplierId AND IsActive = 1)
      THROW 50013, 'SUPPLIER_NOT_FOUND', 1;
    IF @RentalEndDate IS NOT NULL AND @PurchaseDate IS NOT NULL AND @RentalEndDate < @PurchaseDate
      THROW 50014, 'INVALID_VALUE', 1;
    IF EXISTS (SELECT 1 FROM Assets WHERE AssetCode = @AssetCode) THROW 50010, 'ASSET_CODE_DUPLICATE', 1;
    IF @SerialNumber IS NOT NULL AND EXISTS (SELECT 1 FROM Assets WHERE SerialNumber = @SerialNumber)
      THROW 50011, 'SERIAL_DUPLICATE', 1;

    BEGIN TRAN;
    INSERT Assets (AssetCode, SerialNumber, Category, Brand, Model, OwnershipType, SupplierId,
                   Status, CurrentLocation, PurchaseDate, RentalEndDate)
    VALUES (@AssetCode, @SerialNumber, @Category, @Brand, @Model, @OwnershipType, @SupplierId,
            'Disponible', @CurrentLocation, @PurchaseDate, @RentalEndDate);
    DECLARE @Id INT = SCOPE_IDENTITY();

    INSERT AssetMovements (AssetId, MovementType, PreviousValue, NewValue, PerformedByUserId, Notes)
    VALUES (@Id, 'Alta', NULL, 'Disponible', @UserId, 'Alta del activo ' + @AssetCode);
    COMMIT;

    SELECT @Id AS AssetId;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;   -- si fue carrera, los indices unicos lanzan 2601/2627 y la API los mapea a 409
  END CATCH
END
GO

/* Actualiza datos del activo. Cambios de estado y ubicacion generan movimientos.
   @RowVer es opcional: si se envia, aplica concurrencia optimista. */
CREATE OR ALTER PROCEDURE dbo.sp_UpdateAsset
  @Id INT, @SerialNumber NVARCHAR(100) = NULL,
  @Category NVARCHAR(50), @Brand NVARCHAR(50), @Model NVARCHAR(80),
  @OwnershipType NVARCHAR(20), @SupplierId INT = NULL,
  @Status NVARCHAR(20), @CurrentLocation NVARCHAR(100) = NULL,
  @PurchaseDate DATE = NULL, @RentalEndDate DATE = NULL,
  @UserId INT, @Notes NVARCHAR(500) = NULL,
  @RowVer VARBINARY(8) = NULL
AS
BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY
    IF @OwnershipType NOT IN ('Propio','Arrendado') THROW 50014, 'INVALID_VALUE', 1;
    IF @Status NOT IN ('Disponible','Asignado','Mantenimiento','Retirado') THROW 50014, 'INVALID_VALUE', 1;
    IF @OwnershipType = 'Arrendado' AND @SupplierId IS NULL THROW 50012, 'SUPPLIER_REQUIRED_FOR_RENTAL', 1;
    IF @SupplierId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Suppliers WHERE Id = @SupplierId AND IsActive = 1)
      THROW 50013, 'SUPPLIER_NOT_FOUND', 1;
    IF @RentalEndDate IS NOT NULL AND @PurchaseDate IS NOT NULL AND @RentalEndDate < @PurchaseDate
      THROW 50014, 'INVALID_VALUE', 1;

    BEGIN TRAN;

    DECLARE @CurStatus NVARCHAR(20), @CurLocation NVARCHAR(100), @CurRowVer VARBINARY(8);
    SELECT @CurStatus = Status, @CurLocation = CurrentLocation, @CurRowVer = CAST(RowVer AS VARBINARY(8))
    FROM Assets WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id;

    IF @CurStatus IS NULL THROW 50001, 'ASSET_NOT_FOUND', 1;
    IF @RowVer IS NOT NULL AND @RowVer <> @CurRowVer THROW 50023, 'CONCURRENCY_CONFLICT', 1;
    IF @CurStatus = 'Retirado' THROW 50021, 'ASSET_RETIRED', 1;

    IF @Status <> @CurStatus
    BEGIN
      -- Asignado solo se alcanza/abandona con sp_AssignAsset / sp_ReturnAsset
      IF @CurStatus = 'Asignado' THROW 50020, 'ASSET_ASSIGNED_RETURN_FIRST', 1;
      IF @Status = 'Asignado' THROW 50022, 'INVALID_STATUS_TRANSITION', 1;
    END

    IF @SerialNumber IS NOT NULL AND EXISTS (SELECT 1 FROM Assets WHERE SerialNumber = @SerialNumber AND Id <> @Id)
      THROW 50011, 'SERIAL_DUPLICATE', 1;

    UPDATE Assets
    SET SerialNumber = @SerialNumber, Category = @Category, Brand = @Brand, Model = @Model,
        OwnershipType = @OwnershipType, SupplierId = @SupplierId, Status = @Status,
        CurrentLocation = @CurrentLocation, PurchaseDate = @PurchaseDate,
        RentalEndDate = @RentalEndDate, UpdatedAt = SYSUTCDATETIME()
    WHERE Id = @Id;

    IF @Status <> @CurStatus
      INSERT AssetMovements (AssetId, MovementType, PreviousValue, NewValue, PerformedByUserId, Notes)
      VALUES (@Id, 'CambioEstado', @CurStatus, @Status, @UserId, @Notes);

    IF ISNULL(@CurrentLocation, '') <> ISNULL(@CurLocation, '')
      INSERT AssetMovements (AssetId, MovementType, PreviousValue, NewValue, PerformedByUserId, Notes)
      VALUES (@Id, 'CambioUbicacion', @CurLocation, @CurrentLocation, @UserId, @Notes);

    COMMIT;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
  END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetAssetById
  @Id INT
AS
BEGIN
  SET NOCOUNT ON;
  SELECT a.Id, a.AssetCode, a.SerialNumber, a.Category, a.Brand, a.Model, a.OwnershipType,
         a.SupplierId, s.Name AS SupplierName, a.Status, a.CurrentLocation,
         a.PurchaseDate, a.RentalEndDate, a.CreatedAt, a.UpdatedAt,
         CONVERT(VARCHAR(18), CAST(a.RowVer AS VARBINARY(8)), 1) AS RowVer,
         e.Id AS AssignedEmployeeId, e.FullName AS AssignedEmployeeName
  FROM Assets a
  LEFT JOIN Suppliers s ON s.Id = a.SupplierId
  LEFT JOIN Assignments asg ON asg.AssetId = a.Id AND asg.ReturnedAt IS NULL
  LEFT JOIN Employees e ON e.Id = asg.EmployeeId
  WHERE a.Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetAssets
  @Search NVARCHAR(100) = NULL, @Status NVARCHAR(20) = NULL, @Category NVARCHAR(50) = NULL,
  @Page INT = 1, @PageSize INT = 20
AS
BEGIN
  SET NOCOUNT ON;
  IF @Page < 1 SET @Page = 1;
  IF @PageSize < 1 SET @PageSize = 20;
  IF @PageSize > 100 SET @PageSize = 100;

  SELECT a.Id, a.AssetCode, a.SerialNumber, a.Category, a.Brand, a.Model, a.OwnershipType,
         a.SupplierId, s.Name AS SupplierName, a.Status, a.CurrentLocation,
         a.PurchaseDate, a.RentalEndDate, a.CreatedAt, a.UpdatedAt,
         e.Id AS AssignedEmployeeId, e.FullName AS AssignedEmployeeName,
         COUNT(*) OVER() AS TotalCount
  FROM Assets a
  LEFT JOIN Suppliers s ON s.Id = a.SupplierId
  LEFT JOIN Assignments asg ON asg.AssetId = a.Id AND asg.ReturnedAt IS NULL
  LEFT JOIN Employees e ON e.Id = asg.EmployeeId
  WHERE (@Status IS NULL OR a.Status = @Status)
    AND (@Category IS NULL OR a.Category = @Category)
    AND (@Search IS NULL OR a.AssetCode LIKE '%' + @Search + '%'
         OR a.SerialNumber LIKE '%' + @Search + '%' OR a.Brand LIKE '%' + @Search + '%'
         OR a.Model LIKE '%' + @Search + '%' OR a.Category LIKE '%' + @Search + '%')
  ORDER BY a.AssetCode
  OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* ------------------------- RETURN & HISTORY ------------------------- */

CREATE OR ALTER PROCEDURE dbo.sp_ReturnAsset
  @AssetId INT, @UserId INT, @ReturnCondition NVARCHAR(200), @Notes NVARCHAR(500) = NULL
AS
BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY
    IF LTRIM(RTRIM(ISNULL(@ReturnCondition, ''))) = '' THROW 50014, 'INVALID_VALUE', 1;

    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM Assets WITH (UPDLOCK, HOLDLOCK) WHERE Id = @AssetId;
    IF @Status IS NULL THROW 50001, 'ASSET_NOT_FOUND', 1;

    DECLARE @AssignmentId INT, @EmployeeId INT;
    SELECT @AssignmentId = Id, @EmployeeId = EmployeeId
    FROM Assignments WITH (UPDLOCK) WHERE AssetId = @AssetId AND ReturnedAt IS NULL;
    IF @AssignmentId IS NULL THROW 50030, 'NO_ACTIVE_ASSIGNMENT', 1;

    UPDATE Assignments
    SET ReturnedAt = SYSUTCDATETIME(), ReturnedByUserId = @UserId,
        ReturnCondition = @ReturnCondition,
        Notes = CASE WHEN @Notes IS NULL THEN Notes
                     WHEN Notes IS NULL THEN @Notes
                     ELSE LEFT(Notes + ' | ' + @Notes, 500) END
    WHERE Id = @AssignmentId;

    UPDATE Assets SET Status = 'Disponible', UpdatedAt = SYSUTCDATETIME() WHERE Id = @AssetId;

    INSERT AssetMovements (AssetId, MovementType, PreviousValue, NewValue, EmployeeId, PerformedByUserId, Notes)
    VALUES (@AssetId, 'Devolucion', 'Asignado', 'Disponible', @EmployeeId, @UserId,
            LEFT('Condicion: ' + @ReturnCondition + ISNULL(' | ' + @Notes, ''), 500));

    COMMIT;
    SELECT @AssignmentId AS AssignmentId;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
  END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetAssetHistory
  @AssetId INT, @Page INT = 1, @PageSize INT = 50
AS
BEGIN
  SET NOCOUNT ON;
  IF NOT EXISTS (SELECT 1 FROM Assets WHERE Id = @AssetId) THROW 50001, 'ASSET_NOT_FOUND', 1;
  IF @Page < 1 SET @Page = 1;
  IF @PageSize < 1 SET @PageSize = 50;
  IF @PageSize > 200 SET @PageSize = 200;

  SELECT m.Id, m.AssetId, m.MovementType, m.PreviousValue, m.NewValue,
         m.EmployeeId, e.FullName AS EmployeeName,
         m.PerformedByUserId, u.Username AS PerformedBy,
         m.PerformedAt, m.Notes, COUNT(*) OVER() AS TotalCount
  FROM AssetMovements m
  JOIN Users u ON u.Id = m.PerformedByUserId
  LEFT JOIN Employees e ON e.Id = m.EmployeeId
  WHERE m.AssetId = @AssetId
  ORDER BY m.PerformedAt DESC, m.Id DESC
  OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO
