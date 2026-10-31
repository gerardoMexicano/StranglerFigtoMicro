-- ============================================================
-- 05 - asistencias: check-ins del Checador de Asistencia
-- BASE DE DATOS: Laboratory
--
-- /legacy/Usuarios.aspx solo mostraba la ubicación en el mapa y no
-- guardaba nada. La versión migrada (/newapp/usuarios + 05_backend,
-- POST /api/attendance/check-ins) registra cada check-in aquí.
--
-- user_id es el "sub" del token de Keycloak; username es
-- preferred_username, solo para leer la tabla a mano.
-- registrado_en lo pone SQL Server, no el navegador.
--
-- 05_backend escribe con ConnectionStrings__DefaultConnection
-- (BACKEND_DB_CONNECTION): ese login necesita SELECT e INSERT en
-- dbo.asistencias, p. ej. app_escritor (db_datawriter). Con
-- app_lector el POST responde 500.
--
-- Idempotente: se puede ejecutar más de una vez.
-- ============================================================
USE Laboratory;
GO

IF OBJECT_ID('dbo.asistencias', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.asistencias (
        id                BIGINT             IDENTITY(1,1) PRIMARY KEY,
        user_id           NVARCHAR(64)       NOT NULL,
        username          NVARCHAR(100)      NOT NULL,
        latitud           DECIMAL(9,6)       NOT NULL CHECK (latitud  BETWEEN -90  AND 90),
        longitud          DECIMAL(9,6)       NOT NULL CHECK (longitud BETWEEN -180 AND 180),
        precision_metros  DECIMAL(9,2)       NOT NULL CHECK (precision_metros >= 0),
        registrado_en     DATETIMEOFFSET(0)  NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );
END
GO

-- "Mis últimos check-ins": filtra por usuario y ordena por fecha
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_asistencias_user_id_registrado_en' AND object_id = OBJECT_ID('dbo.asistencias'))
    CREATE INDEX IX_asistencias_user_id_registrado_en
        ON dbo.asistencias (user_id, registrado_en DESC)
        INCLUDE (latitud, longitud, precision_metros);
GO

-- Verificación
SELECT TOP (10) id, username, latitud, longitud, precision_metros, registrado_en
FROM dbo.asistencias
ORDER BY registrado_en DESC;
GO
