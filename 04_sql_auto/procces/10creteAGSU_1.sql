--aplicar a ambos
CREATE LOGIN AgAdmin WITH PASSWORD = 'TuPasswordSuperSegura123!';
ALTER SERVER ROLE sysadmin ADD MEMBER AgAdmin;