# Requiere: export MSSQL_SA_PASSWORD=... AG_PASSWORD=... (o: set -a; . ../.env; set +a)
sqlcmd -S localhost,1443 -U sa -P "$MSSQL_SA_PASSWORD" -v AG_PASSWORD="$AG_PASSWORD"