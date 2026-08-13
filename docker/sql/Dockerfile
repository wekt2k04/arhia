# SQL Server 2025 — image officielle (support natif vector(768) pour le RAG)
# Usage (Docker Desktop) — le mot de passe SA est OBLIGATOIRE au runtime (-e),
# jamais gravé dans l'image :
#   docker build -t agirh-sql docker/sql
#   docker run -d --name agirh-sql -p 1433:1433 -e MSSQL_SA_PASSWORD=YourStrong!Passw0rd agirh-sql
# Relancer plus tard : docker start agirh-sql
FROM mcr.microsoft.com/mssql/server:2025-latest

# Licence acceptée (obligatoire)
ENV ACCEPT_EULA=Y

EXPOSE 1433
