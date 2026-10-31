-- ============================================================
-- 06 - salas y reservaciones: Reservación de salas
-- BASE DE DATOS: Laboratory
--
-- Funcionalidad nueva (no existía en WebForms). La pantalla es
-- Angular (06_frontend) pero se entra por /legacy/AngularPage:
-- el gateway manda esa ruta al frontend nuevo. Los datos los
-- sirve 05_backend en /api/rooms.
--
-- inicia_en / termina_en son hora local de las salas
-- (America/Mexico_City), sin zona horaria. user_id es el "sub"
-- del token de Keycloak; username es preferred_username, para
-- mostrar quién reservó.
--
-- 05_backend escribe con ConnectionStrings__DefaultConnection
-- (BACKEND_DB_CONNECTION): ese login necesita SELECT en dbo.salas
-- y SELECT, INSERT y DELETE en dbo.reservaciones, p. ej.
-- app_escritor (db_datawriter + db_datareader).
--
-- Idempotente: se puede ejecutar más de una vez.
-- ============================================================
USE Laboratory;
GO

IF OBJECT_ID('dbo.salas', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.salas (
        id         INT            IDENTITY(1,1) PRIMARY KEY,
        nombre     NVARCHAR(60)   NOT NULL UNIQUE,
        capacidad  INT            NOT NULL CHECK (capacidad > 0),
        ubicacion  NVARCHAR(100)  NOT NULL,
        activa     BIT            NOT NULL DEFAULT 1
    );
END
GO

-- Salas iniciales: solo las que todavía no existen
MERGE dbo.salas AS destino
USING (VALUES
    (N'Sala Juárez',  8,  N'Planta baja, junto a recepción'),
    (N'Sala Hidalgo', 12, N'Primer piso, ala norte'),
    (N'Sala Morelos', 4,  N'Primer piso, ala sur')
) AS origen (nombre, capacidad, ubicacion)
ON destino.nombre = origen.nombre
WHEN NOT MATCHED THEN
    INSERT (nombre, capacidad, ubicacion) VALUES (origen.nombre, origen.capacidad, origen.ubicacion);
GO

IF OBJECT_ID('dbo.reservaciones', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.reservaciones (
        id          BIGINT             IDENTITY(1,1) PRIMARY KEY,
        sala_id     INT                NOT NULL REFERENCES dbo.salas (id),
        inicia_en   DATETIME2(0)       NOT NULL,
        termina_en  DATETIME2(0)       NOT NULL,
        asunto      NVARCHAR(120)      NOT NULL,
        user_id     NVARCHAR(64)       NOT NULL,
        username    NVARCHAR(100)      NOT NULL,
        creada_en   DATETIMEOFFSET(0)  NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT CK_reservaciones_horario CHECK (termina_en > inicia_en)
    );
END
GO

-- Empalmes (UPDLOCK, HOLDLOCK en 05_backend) y la agenda de un día: por sala y hora de inicio
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_reservaciones_sala_id_inicia_en' AND object_id = OBJECT_ID('dbo.reservaciones'))
    CREATE INDEX IX_reservaciones_sala_id_inicia_en
        ON dbo.reservaciones (sala_id, inicia_en)
        INCLUDE (termina_en);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_reservaciones_inicia_en' AND object_id = OBJECT_ID('dbo.reservaciones'))
    CREATE INDEX IX_reservaciones_inicia_en
        ON dbo.reservaciones (inicia_en)
        INCLUDE (sala_id, termina_en, asunto, user_id, username);
GO

-- Verificación
SELECT id, nombre, capacidad, ubicacion, activa FROM dbo.salas ORDER BY nombre;

SELECT TOP (10) r.id, s.nombre AS sala, r.inicia_en, r.termina_en, r.asunto, r.username
FROM dbo.reservaciones AS r
JOIN dbo.salas AS s ON s.id = r.sala_id
ORDER BY r.inicia_en DESC;
GO
