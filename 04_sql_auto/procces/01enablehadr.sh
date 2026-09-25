docker exec -u root -it sql-primary //opt/mssql/bin/mssql-conf set hadr.hadrenabled 1
docker stop sql-primary
docker start sql-primary

docker exec -u root -it sql-secondary //opt/mssql/bin/mssql-conf set hadr.hadrenabled 1
docker stop sql-secondary
docker start sql-secondary