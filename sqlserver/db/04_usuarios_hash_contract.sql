-- ============================================================
-- 04 - usuarios: contraseñas con hash (fase CONTRACT)
-- BASE DE DATOS: Laboratory
--
-- Ejecutar SOLO después de desplegar el legacy que valida con hash
-- (ver 03_usuarios_hash_expand.sql). Elimina la contraseña en texto
-- plano y vuelve obligatorias las columnas de hash.
--
-- Idempotente: se puede ejecutar más de una vez.
-- ============================================================
USE Laboratory;
GO

-- Seguridad: no borrar el texto plano si quedó alguna fila sin hash
IF EXISTS (SELECT 1 FROM dbo.usuarios WHERE password_hash IS NULL OR password_salt IS NULL)
BEGIN
    RAISERROR('Hay usuarios sin hash. Ejecuta primero 03_usuarios_hash_expand.sql.', 16, 1);
    SET NOEXEC ON;
END
GO

ALTER TABLE dbo.usuarios ALTER COLUMN password_salt VARBINARY(16) NOT NULL;
ALTER TABLE dbo.usuarios ALTER COLUMN password_hash VARBINARY(64) NOT NULL;
GO

IF COL_LENGTH('dbo.usuarios', 'password') IS NOT NULL
    ALTER TABLE dbo.usuarios DROP COLUMN password;
GO

SET NOEXEC OFF;
GO

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
SELECT id, username, rol, activo, ultimo_login,
       DATALENGTH(password_hash) AS bytes_hash
FROM dbo.usuarios;
GO
