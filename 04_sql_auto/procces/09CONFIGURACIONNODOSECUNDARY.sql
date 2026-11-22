--Levantará su endpoint con su certificado y dejará entrar al primario.

USE master;
GO

-- 1. Crear el Endpoint usando su PROPIO certificado
CREATE ENDPOINT [Hadr_endpoint] 
    AS TCP (LISTENER_PORT = 5022)
    FOR DATA_MIRRORING (
        ROLE = ALL, 
        AUTHENTICATION = CERTIFICATE cert_nodo_secundario, 
        ENCRYPTION = REQUIRED ALGORITHM AES
    );
GO

-- Encender el endpoint
ALTER ENDPOINT [Hadr_endpoint] STATE = STARTED;
GO

-- 2. Crear un login y usuario para el nodo primario
CREATE LOGIN login_primario WITH PASSWORD = '$(AG_PASSWORD)';
CREATE USER usr_primario FOR LOGIN login_primario;
GO

-- 3. Importar el certificado del PRIMARIO y asociarlo al usuario
CREATE CERTIFICATE cert_nodo_primario 
    AUTHORIZATION usr_primario 
    FROM FILE = '/backups/cert_primario.cer';
GO

-- 4. Dar permiso al nodo primario para conectarse al endpoint
GRANT CONNECT ON ENDPOINT::[Hadr_endpoint] TO login_primario;
GO