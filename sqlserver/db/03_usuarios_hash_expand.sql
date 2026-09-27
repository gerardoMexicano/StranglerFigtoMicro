-- ============================================================
-- 03 - usuarios: contraseñas con hash (fase EXPAND)
-- BASE DE DATOS: Laboratory
--
-- Agrega columnas nuevas y calcula el hash a partir del texto plano
-- SIN borrar la columna password: el legacy actual sigue funcionando
-- mientras se despliega la versión que valida contra el hash.
--
-- Orden de despliegue:
--   1. Este script (03)
--   2. Desplegar WebApp_MVC y WebApp_WebForms que validan con hash
--   3. 04_usuarios_hash_contract.sql (elimina el texto plano)
--
-- Algoritmo (debe coincidir con ValidarCredenciales del legacy):
--   password_hash = SHA2_512( password_salt + UTF-16LE(password) )
--   UTF-16LE es lo que produce CAST(NVARCHAR AS VARBINARY) y
--   Encoding.Unicode.GetBytes() en .NET.
--
-- Idempotente: se puede ejecutar más de una vez.
-- ============================================================
USE Laboratory;
GO

IF COL_LENGTH('dbo.usuarios', 'password_salt') IS NULL
    ALTER TABLE dbo.usuarios ADD password_salt VARBINARY(16) NULL;

IF COL_LENGTH('dbo.usuarios', 'password_hash') IS NULL
    ALTER TABLE dbo.usuarios ADD password_hash VARBINARY(64) NULL;

-- Para medir la migración a Keycloak (usuarios activos que no han entrado)
IF COL_LENGTH('dbo.usuarios', 'ultimo_login') IS NULL
    ALTER TABLE dbo.usuarios ADD ultimo_login DATETIME2 NULL;
GO

-- SQL dinámico: si 04 ya eliminó la columna password, este bloque se omite
-- en lugar de fallar al compilar.
IF COL_LENGTH('dbo.usuarios', 'password') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;

    -- Un salt aleatorio por fila (CRYPT_GEN_RANDOM se evalúa por fila)
    UPDATE dbo.usuarios
    SET    password_salt = CRYPT_GEN_RANDOM(16)
    WHERE  password_hash IS NULL;

    EXEC sp_executesql N'
        UPDATE dbo.usuarios
        SET    password_hash = HASHBYTES(''SHA2_512'', password_salt + CAST(password AS VARBINARY(510))),
               updated_at    = SYSDATETIME()
        WHERE  password_hash IS NULL;';

    COMMIT TRANSACTION;
END
GO

-- ============================================================
-- VERIFICACIÓN: ninguna fila sin hash y salts sin repetir
-- ============================================================
SELECT
    COUNT(*)                              AS total,
    SUM(IIF(password_hash IS NULL, 1, 0)) AS sin_hash,
    COUNT(DISTINCT password_salt)         AS salts_distintos
FROM dbo.usuarios;
GO
