#!/bin/bash

# Iniciar o SQL Server em background
/opt/mssql/bin/sqlservr &

# Aguardar o SQL Server estar pronto para conexões
echo "Aguardando o SQL Server iniciar..."
until /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" &>/dev/null; do
    sleep 2
done

echo "SQL Server pronto! Criando bancos de dados..."
# Executar o script de criação dos bancos
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -i init.sql

echo "Bancos criados com sucesso. Mantendo container ativo."
# Manter o processo principal vivo
wait
