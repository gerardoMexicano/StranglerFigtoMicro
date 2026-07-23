USE master;
GO

-- 1. Crear la llave maestra de la base de datos (Master Key)
CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'TuPasswordSuperSegura123!';
GO

-- 2. Crear el certificado de este motor
CREATE CERTIFICATE cert_nodo_primario 
    WITH SUBJECT = 'Certificado AG Primario';
GO

-- 3. Respaldar SOLO la llave pública en la carpeta compartida
BACKUP CERTIFICATE cert_nodo_primario
    TO FILE = '/backups/cert_primario.cer';
GO