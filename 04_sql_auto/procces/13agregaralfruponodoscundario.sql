USE master;
GO

-- Unirse al grupo
ALTER AVAILABILITY GROUP [AG_Laboratorio] JOIN WITH (CLUSTER_TYPE = NONE);
GO

-- Dar permiso para que el primario envíe las bases de datos
ALTER AVAILABILITY GROUP [AG_Laboratorio] GRANT CREATE ANY DATABASE;
GO