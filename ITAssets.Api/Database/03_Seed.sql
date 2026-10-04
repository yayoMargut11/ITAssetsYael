/* =====================================================================
   03_Seed.sql  - Datos de prueba (idempotente: se puede ejecutar varias veces)

   USUARIOS DE PRUEBA (documentarlos en el README):
     admin     / Admin#2026      (rol Administrador)
     operador  / Operador#2026   (rol Operador)
   Las contrasenas se guardan como hash BCrypt (work factor 11).
   ===================================================================== */
USE ITAssetsDb;
GO

-- Roles
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Administrador') INSERT Roles (Name) VALUES ('Administrador');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Operador')      INSERT Roles (Name) VALUES ('Operador');

-- Usuarios
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
  INSERT Users (Username, PasswordHash, RoleId)
  VALUES ('admin', '$2b$11$ThLLyN/1r0Sh4i/4o.egiOosoS.hcc3VYPqjGr0781jgICmeYwnZe',
          (SELECT Id FROM Roles WHERE Name = 'Administrador'));

IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'operador')
  INSERT Users (Username, PasswordHash, RoleId)
  VALUES ('operador', '$2b$11$mX43TYt0gVE7zzGGhfT01u6TBV/OEVHL.0V3bVn4930qMCkyjSgKK',
          (SELECT Id FROM Roles WHERE Name = 'Operador'));

-- Proveedores y servicios
IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'Dell Mexico')
  INSERT Suppliers (Name, Email, Phone) VALUES ('Dell Mexico', 'ventas@dell.example.com', '5555550101');
IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'LeasingTech')
  INSERT Suppliers (Name, Email, Phone) VALUES ('LeasingTech', 'contacto@leasingtech.example.com', '5555550102');
IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'FixIT Servicios')
  INSERT Suppliers (Name, Email, Phone) VALUES ('FixIT Servicios', 'soporte@fixit.example.com', '5555550103');

INSERT SupplierServices (SupplierId, ServiceType)
SELECT s.Id, v.ServiceType
FROM (VALUES ('Dell Mexico','Compra'), ('LeasingTech','Arrendamiento'),
             ('LeasingTech','Mantenimiento'), ('FixIT Servicios','Mantenimiento')) v(SName, ServiceType)
JOIN Suppliers s ON s.Name = v.SName
WHERE NOT EXISTS (SELECT 1 FROM SupplierServices x WHERE x.SupplierId = s.Id AND x.ServiceType = v.ServiceType);

-- Colaboradores (el ultimo esta inactivo para probar la regla)
INSERT Employees (EmployeeNumber, FullName, Email, IsActive)
SELECT v.Num, v.Name, v.Email, v.Active
FROM (VALUES ('E-1001','Ana Lopez','ana.lopez@empresa.example.com',1),
             ('E-1002','Carlos Ruiz','carlos.ruiz@empresa.example.com',1),
             ('E-1003','Maria Torres','maria.torres@empresa.example.com',1),
             ('E-1004','Luis Inactivo','luis.inactivo@empresa.example.com',0)) v(Num, Name, Email, Active)
WHERE NOT EXISTS (SELECT 1 FROM Employees e WHERE e.EmployeeNumber = v.Num);

-- Activos de ejemplo (con su movimiento de Alta)
DECLARE @AdminId INT = (SELECT Id FROM Users WHERE Username = 'admin');
DECLARE @Dell INT = (SELECT Id FROM Suppliers WHERE Name = 'Dell Mexico');
DECLARE @Leasing INT = (SELECT Id FROM Suppliers WHERE Name = 'LeasingTech');

INSERT Assets (AssetCode, SerialNumber, Category, Brand, Model, OwnershipType, SupplierId,
               Status, CurrentLocation, PurchaseDate, RentalEndDate)
SELECT v.Code, v.Serial, v.Cat, v.Brand, v.Model, v.Own, v.Sup, v.Status, v.Loc, v.PDate, v.REnd
FROM (VALUES
  ('TI-0001001','SN-DELL-0001','Laptop','DELL','Latitude 5440','Propio',@Dell,'Disponible','Oficina CDMX','2025-03-10',NULL),
  ('TI-0001002','SN-DELL-0002','Laptop','DELL','Latitude 5440','Propio',@Dell,'Disponible','Oficina CDMX','2025-03-10',NULL),
  ('TI-0001003','SN-HP-0003','Laptop','HP','EliteBook 840','Arrendado',@Leasing,'Disponible','Oficina CDMX','2025-06-01','2027-06-01'),
  ('TI-0002001','SN-LG-0004','Monitor','LG','27UL500','Propio',@Dell,'Disponible','Almacen','2024-11-15',NULL),
  ('TI-0003001',NULL,'Impresora','Epson','L3250','Propio',NULL,'Mantenimiento','Taller','2024-05-20',NULL),
  ('TI-0004001','SN-APL-0005','Celular','Apple','iPhone 13','Propio',NULL,'Retirado','Almacen','2022-01-05',NULL)
) v(Code, Serial, Cat, Brand, Model, Own, Sup, Status, Loc, PDate, REnd)
WHERE NOT EXISTS (SELECT 1 FROM Assets a WHERE a.AssetCode = v.Code);

INSERT AssetMovements (AssetId, MovementType, PreviousValue, NewValue, PerformedByUserId, Notes)
SELECT a.Id, 'Alta', NULL, a.Status, @AdminId, 'Alta inicial (seed)'
FROM Assets a
WHERE NOT EXISTS (SELECT 1 FROM AssetMovements m WHERE m.AssetId = a.Id AND m.MovementType = 'Alta');
GO

-- Verificacion rapida
SELECT 'Users' T, COUNT(*) N FROM Users UNION ALL SELECT 'Employees', COUNT(*) FROM Employees
UNION ALL SELECT 'Suppliers', COUNT(*) FROM Suppliers UNION ALL SELECT 'Assets', COUNT(*) FROM Assets;
GO
