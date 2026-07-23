-- CONTAINED: La cláusula que le dice a SQL que cree las bases de datos de sistema aisladas.
-- CLUSTER_TYPE = NONE: Le indica que no busque un clúster de Windows (WSFC) ni de Linux (Pacemaker) para gestionar la infraestructura.
-- SEEDING_MODE = AUTOMATIC: Nos ahorra tener que hacer el proceso manual de backup/restore por cada base de datos que agreguemos en el futuro; SQL lo hará por la red.
USE master;
GO

CREATE AVAILABILITY GROUP [AG_Laboratorio]
WITH (
    CONTAINED, 
    CLUSTER_TYPE = NONE
)
FOR REPLICA ON
N'sql-primary' WITH 
(
    ENDPOINT_URL = N'tcp://sql-primary:5022',
    AVAILABILITY_MODE = SYNCHRONOUS_COMMIT,
    FAILOVER_MODE = MANUAL,
    SEEDING_MODE = AUTOMATIC
),
N'sql-secondary' WITH 
(
    ENDPOINT_URL = N'tcp://sql-secondary:5022',
    AVAILABILITY_MODE = SYNCHRONOUS_COMMIT,
    FAILOVER_MODE = MANUAL,
    SEEDING_MODE = AUTOMATIC
);
GO