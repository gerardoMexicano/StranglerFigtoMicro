USE master;
GO

-- 1. Crear la llave maestra de la base de datos
CREATE MASTER KEY ENCRYPTION BY PASSWORD = '$(AG_PASSWORD)';
GO

-- 2. Crear el certificado de este motor
CREATE CERTIFICATE cert_nodo_secundario 
    WITH SUBJECT = 'Certificado AG Secundario';
GO

-- 3. Respaldar la llave pública en la carpeta compartida
BACKUP CERTIFICATE cert_nodo_secundario
    TO FILE = '/backups/cert_secundario.cer';
GO