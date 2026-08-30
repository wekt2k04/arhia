# Contexte de build attendu : la racine du depot (voir docker-compose.yml, build.context: .)
# car Arhia.Api reference Domain/Core/Infrastructure par ProjectReference.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Arhia.Domain/Arhia.Domain.csproj src/Arhia.Domain/
COPY src/Arhia.Core/Arhia.Core.csproj src/Arhia.Core/
COPY src/Arhia.Infrastructure/Arhia.Infrastructure.csproj src/Arhia.Infrastructure/
COPY src/Arhia.Api/Arhia.Api.csproj src/Arhia.Api/
RUN dotnet restore src/Arhia.Api/Arhia.Api.csproj

COPY src/ src/
RUN dotnet publish src/Arhia.Api/Arhia.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# FindRepoRoot (Program.cs) remonte l'arborescence a la recherche d'Arhia.sln pour
# localiser rag/models/ et rag/corpus/ de facon identique en local et en conteneur - seule la
# presence du fichier compte, pas son contenu.
COPY Arhia.sln ./
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Arhia.Api.dll"]
