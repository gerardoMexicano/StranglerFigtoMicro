-- Conéctate a tu nodo primario (puerto 1443) y ejecuta este script. Esto levantará su propio 
-- endpoint y le dará permiso de entrada al secundario.
USE master;
GO

-- 1. Crear el Endpoint usando su PROPIO certificado
CREATE ENDPOINT [Hadr_endpoint] 
    AS TCP (LISTENER_PORT = 5022)
    FOR DATA_MIRRORING (
        ROLE = ALL, 
        AUTHENTICATION = CERTIFICATE cert_nodo_primario, 
        ENCRYPTION = REQUIRED ALGORITHM AES
    );
GO

-- Encender el endpoint
ALTER ENDPOINT [Hadr_endpoint] STATE = STARTED;
GO

-- 2. Crear un login y usuario para el nodo secundario
CREATE LOGIN login_secundario WITH PASSWORD = 'TuPasswordSuperSegura123!';
CREATE USER usr_secundario FOR LOGIN login_secundario;
GO

-- 3. Importar el certificado del SECUNDARIO y asociarlo al usuario
CREATE CERTIFICATE cert_nodo_secundario 
    AUTHORIZATION usr_secundario 
    FROM FILE = '/backups/cert_secundario.cer';
GO

-- 4. Dar permiso al nodo secundario para conectarse al endpoint
GRANT CONNECT ON ENDPOINT::[Hadr_endpoint] TO login_secundario;
GO