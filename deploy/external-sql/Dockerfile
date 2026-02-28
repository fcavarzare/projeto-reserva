FROM mcr.microsoft.com/mssql/server:2022-latest

# Mudar para root para configurar permissões
USER root

# Copiar scripts de inicialização
WORKDIR /usr/config
COPY init.sql .
COPY entrypoint.sh .

# Dar permissão de execução
RUN chmod +x entrypoint.sh

# Voltar para o usuário mssql por segurança
USER mssql

# Definir variáveis de ambiente padrão (Podem ser sobrescritas no docker run)
ENV ACCEPT_EULA=Y
ENV MSSQL_SA_PASSWORD=Your_password123

# Expor a porta 1433
EXPOSE 1433

# Comando de entrada
ENTRYPOINT ["./entrypoint.sh"]
