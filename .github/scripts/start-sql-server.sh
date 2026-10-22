#!/usr/bin/env bash
# Flowza CI (T031, PLT-09): start a throwaway SQL Server 2019 and create the flowza_* databases with the same script
# the owner runs on a PC (docs/setup/create-databases.sql, compatibility level 150).
#
# Both passwords are random for every run, masked in the log and never stored anywhere: the container lives only
# for this job, so no GitHub secret is needed. The tests read the application password from FLOWZA_Sql__Password
# (tests/Flowza.Testing/TestSqlServer.cs); server "localhost" and login "flowza_app" are their defaults.
set -euo pipefail

image="mcr.microsoft.com/mssql/server:2019-latest"
container="flowza-sql"

# Hex characters plus fixed upper/lower/digit/symbol parts: meets SQL Server's password policy, and needs no quoting.
sa_password="Sa$(openssl rand -hex 16)!1"
app_password="App$(openssl rand -hex 16)!1"
echo "::add-mask::${sa_password}"
echo "::add-mask::${app_password}"

docker run --detach --name "${container}" --publish 1433:1433 \
  --env ACCEPT_EULA=Y --env MSSQL_PID=Developer --env "MSSQL_SA_PASSWORD=${sa_password}" \
  "${image}" > /dev/null

# sqlcmd inside the container: newer 2019 images ship mssql-tools18 (needs -C for the self-signed certificate),
# older ones mssql-tools.
run_sql() {
  docker exec "${container}" bash -c '
    if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then exec /opt/mssql-tools18/bin/sqlcmd -C "$@"; fi
    exec /opt/mssql-tools/bin/sqlcmd "$@"' sqlcmd -S localhost -U sa -P "${sa_password}" -b "$@"
}

echo "Waiting for SQL Server 2019 ..."
for attempt in $(seq 1 60); do
  if run_sql -Q "SELECT 1" > /dev/null 2>&1; then
    break
  fi
  if [ "${attempt}" -eq 60 ]; then
    echo "SQL Server did not start within 2 minutes." >&2
    docker logs "${container}" | tail -n 50 >&2
    exit 1
  fi
  sleep 2
done
run_sql -h -1 -Q "SET NOCOUNT ON; SELECT N'SQL Server ' + CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32));"

run_sql -Q "CREATE LOGIN [flowza_app] WITH PASSWORD = N'${app_password}', CHECK_POLICY = ON;"
docker cp docs/setup/create-databases.sql "${container}:/tmp/create-databases.sql"
run_sql -i /tmp/create-databases.sql

echo "FLOWZA_Sql__Password=${app_password}" >> "${GITHUB_ENV}"
echo "SQL Server 2019 is ready with the 10 flowza_* databases."
