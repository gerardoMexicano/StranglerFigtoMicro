-- ============================================================
-- BASE DE DATOS: taxes_proto
-- ============================================================
IF DB_ID('taxes_proto') IS NOT NULL
BEGIN
    ALTER DATABASE taxes_proto SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE taxes_proto;
END
GO

CREATE DATABASE taxes_proto;
GO

USE taxes_proto;
GO

-- ============================================================
-- 1. Tabla CORE CFDI
-- ============================================================
CREATE TABLE CfdiCore (
    Id BIGINT IDENTITY PRIMARY KEY,
    UUID UNIQUEIDENTIFIER NOT NULL UNIQUE,
    TipoComprobante CHAR(1) NOT NULL,
    Serie NVARCHAR(50),
    Folio NVARCHAR(50),
    Fecha DATETIME2 NOT NULL,
    Moneda VARCHAR(10),
    SubTotal DECIMAL(18,2),
    Descuento DECIMAL(18,2),
    Total DECIMAL(18,2),
    MetodoPago VARCHAR(10),
    LugarExpedicion VARCHAR(10),
    EmisorRFC VARCHAR(13) NOT NULL,
    EmisorNombre NVARCHAR(300),
    ReceptorRFC VARCHAR(13) NOT NULL,
    ReceptorNombre NVARCHAR(300),
    UsoCFDI VARCHAR(10),
    FechaTimbrado DATETIME2,
    RfcProvCertif VARCHAR(13),
    XmlOriginal XML NOT NULL,
    JsonCompacto NVARCHAR(MAX),
    XmlSHA256 VARCHAR(64),
    FechaInsercion DATETIME2 DEFAULT GETDATE()
);

-- ============================================================
-- 2. Tabla Complementos / Raw
-- ============================================================
CREATE TABLE CfdiComplemento (
    Id BIGINT IDENTITY PRIMARY KEY,
    UUID UNIQUEIDENTIFIER NOT NULL UNIQUE,
    TipoComplemento VARCHAR(100),
    JsonData NVARCHAR(MAX),
    XmlFragment XML,
    FechaInsercion DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (UUID) REFERENCES CfdiCore(UUID)
);

-- ============================================================
-- 3. Capa Analytics / Nómina
-- ============================================================

-- NominaResumen
CREATE TABLE NominaResumen (
    Id BIGINT IDENTITY PRIMARY KEY,
    UUID UNIQUEIDENTIFIER NOT NULL UNIQUE,
    TipoNomina CHAR(1),
    FechaPago DATE,
    FechaInicialPago DATE,
    FechaFinalPago DATE,
    NumDiasPagados DECIMAL(10,2),
    TotalPercepciones DECIMAL(18,2),
    TotalDeducciones DECIMAL(18,2),
    TotalGravado DECIMAL(18,2),
    TotalExento DECIMAL(18,2),
    NumEmpleado VARCHAR(50),
    Departamento NVARCHAR(200),
    Puesto NVARCHAR(200),
    TipoContrato VARCHAR(10),
    TipoRegimen VARCHAR(10),
    PeriodicidadPago VARCHAR(10),
    SalarioBaseCotApor DECIMAL(18,2),
    SalarioDiarioIntegrado DECIMAL(18,2),
    FOREIGN KEY (UUID) REFERENCES CfdiCore(UUID)
);

-- NominaMovimiento
CREATE TABLE NominaMovimiento (
    Id BIGINT IDENTITY PRIMARY KEY,
    UUID UNIQUEIDENTIFIER NOT NULL,
    TipoMovimiento VARCHAR(20), 
    TipoCodigo VARCHAR(10),
    Clave VARCHAR(50),
    Concepto NVARCHAR(300),
    ImporteGravado DECIMAL(18,2) NULL,
    ImporteExento DECIMAL(18,2) NULL,
    Importe DECIMAL(18,2) NULL,
    FOREIGN KEY (UUID) REFERENCES CfdiCore(UUID)
);

-- Índices importantes
CREATE INDEX IX_CfdiCore_Fecha ON CfdiCore(Fecha);
CREATE INDEX IX_CfdiCore_EmisorRFC ON CfdiCore(EmisorRFC);
CREATE INDEX IX_CfdiCore_ReceptorRFC ON CfdiCore(ReceptorRFC);
CREATE INDEX IX_NominaResumen_FechaPago ON NominaResumen(FechaPago);
GO

-- ============================================================
-- ASIGNACIÓN DE PERMISOS EN taxes_proto
-- ============================================================
-- Crear usuarios mapeados a los logins de servidor
CREATE USER app_lector   FOR LOGIN app_lector;
CREATE USER app_escritor FOR LOGIN app_escritor;
CREATE USER app_invitado FOR LOGIN app_invitado;
CREATE USER app_admin    FOR LOGIN app_admin;

-- Crear el rol personalizado y permisos básicos
CREATE ROLE rol_lector_creador;
GRANT SELECT, INSERT ON SCHEMA::dbo TO rol_lector_creador;
DENY UPDATE, DELETE ON SCHEMA::dbo TO rol_lector_creador;

-- Asignar roles a los usuarios
ALTER ROLE db_datareader ADD MEMBER app_lector;

ALTER ROLE db_datareader ADD MEMBER app_escritor;
ALTER ROLE db_datawriter ADD MEMBER app_escritor;

ALTER ROLE rol_lector_creador ADD MEMBER app_invitado;

ALTER ROLE db_owner ADD MEMBER app_admin;
GO

-- ============================================================
-- ASIGNACIÓN DE PERMISOS EN WideWorldImporters
-- ============================================================
USE WideWorldImporters;
GO

-- Crear usuarios mapeados a los logins de servidor si no existen
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'app_lector')
    CREATE USER app_lector FOR LOGIN app_lector;

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'app_escritor')
    CREATE USER app_escritor FOR LOGIN app_escritor;

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'app_invitado')
    CREATE USER app_invitado FOR LOGIN app_invitado;

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'app_admin')
    CREATE USER app_admin FOR LOGIN app_admin;

-- Crear el rol personalizado si no existe
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'rol_lector_creador' AND type = 'R')
BEGIN
    CREATE ROLE rol_lector_creador;
    GRANT SELECT, INSERT ON SCHEMA::dbo TO rol_lector_creador;
    DENY UPDATE, DELETE ON SCHEMA::dbo TO rol_lector_creador;
END
GO

-- Asignar roles a los usuarios en WideWorldImporters
-- Solo añadimos si no son ya miembros (para evitar posibles errores, aunque ALTER ROLE es seguro, es mejor prevenir en scripts repetibles)
ALTER ROLE db_datareader ADD MEMBER app_lector;

ALTER ROLE db_datareader ADD MEMBER app_escritor;
ALTER ROLE db_datawriter ADD MEMBER app_escritor;

ALTER ROLE rol_lector_creador ADD MEMBER app_invitado;

ALTER ROLE db_owner ADD MEMBER app_admin;
GO
