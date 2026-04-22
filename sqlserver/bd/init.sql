-- ============================================================
-- BASE DE DATOS: Laboratory
-- Versión mejorada con seguridad, auditoría y roles claros
-- ============================================================
IF DB_ID('Laboratory') IS NOT NULL
BEGIN
    ALTER DATABASE Laboratory SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE Laboratory;
END
GO
CREATE DATABASE Laboratory;
GO

USE Laboratory;
GO

-- ============================================================
-- TABLA: usuarios
-- Contraseñas almacenadas como hash (no texto plano)
-- ============================================================
CREATE TABLE usuarios (
    id           INT            IDENTITY(1,1) PRIMARY KEY,
    username     NVARCHAR(50)   NOT NULL UNIQUE,
    password     NVARCHAR(255)  NOT NULL,  -- Almacenar HASH (bcrypt/SHA-256), nunca texto plano
    rol          NVARCHAR(20)   NOT NULL DEFAULT 'usuario'
                                CHECK (rol IN ('admin', 'usuario', 'invitado')),
    activo       BIT            NOT NULL DEFAULT 1,
    created_at   DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    updated_at   DATETIME2      NOT NULL DEFAULT SYSDATETIME()
);
GO

-- ============================================================
-- DATOS INICIALES
-- IMPORTANTE: Reemplazar los valores de 'password' por hashes
-- reales generados desde tu aplicación (bcrypt, PBKDF2, etc.)
-- ============================================================
INSERT INTO usuarios (username, password, rol) VALUES
    ('admin',    'admin123',    'admin'),
    ('usuario1', 'usuario123',  'usuario'),
    ('invitado', 'invitado123', 'invitado');
GO

-- ============================================================
-- LOGINS A NIVEL DE SERVIDOR
-- Cada login tiene un propósito claro y separado
-- ============================================================
CREATE LOGIN app_lector   WITH PASSWORD = 'Lect0r_S3gur0!2026';
CREATE LOGIN app_escritor WITH PASSWORD = 'Escr1t0r_S3gur0!2026';
CREATE LOGIN app_invitado WITH PASSWORD = 'Inv1tado_S3gur0!2026';
CREATE LOGIN app_admin    WITH PASSWORD = 'Adm1n_S3gur0!2026';
GO

-- ============================================================
-- USUARIOS A NIVEL DE BASE DE DATOS
-- Prefijo "app_" para distinguirlos de datos en tablas
-- ============================================================
CREATE USER app_lector   FOR LOGIN app_lector;
CREATE USER app_escritor FOR LOGIN app_escritor;
CREATE USER app_invitado FOR LOGIN app_invitado;
CREATE USER app_admin    FOR LOGIN app_admin;
GO

-- ============================================================
-- ROL PERSONALIZADO: rol_lector_creador
-- Permite SELECT e INSERT, pero NIEGA UPDATE y DELETE
-- Aplica a todas las tablas presentes y futuras del schema dbo
-- ============================================================
CREATE ROLE rol_lector_creador;
GO

GRANT  SELECT, INSERT ON SCHEMA::dbo TO rol_lector_creador;
DENY   UPDATE, DELETE ON SCHEMA::dbo TO rol_lector_creador;
GO

-- ============================================================
-- ASIGNACIÓN DE ROLES Y PERMISOS
-- ============================================================

-- app_lector: solo puede leer (SELECT en todo)
ALTER ROLE db_datareader ADD MEMBER app_lector;

-- app_escritor: puede leer y escribir (SELECT + INSERT/UPDATE/DELETE)
ALTER ROLE db_datareader ADD MEMBER app_escritor;
ALTER ROLE db_datawriter ADD MEMBER app_escritor;

-- app_invitado: puede leer e insertar, pero NO modificar ni eliminar
ALTER ROLE rol_lector_creador ADD MEMBER app_invitado;

-- app_admin: control total de la base de datos
ALTER ROLE db_owner ADD MEMBER app_admin;
GO

-- ============================================================
-- VERIFICACIÓN
-- ============================================================

-- Ver usuarios registrados
SELECT id, username, rol, activo, created_at
FROM usuarios;
GO

-- Ver miembros por rol de base de datos
SELECT
    r.name  AS rol,
    m.name  AS miembro
FROM sys.database_role_members rm
JOIN sys.database_principals r ON rm.role_principal_id  = r.principal_id
JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
WHERE r.name IN ('db_datareader', 'db_datawriter', 'db_owner', 'rol_lector_creador')
ORDER BY r.name, m.name;
GO