# Contexte de build attendu : la racine du depot (voir docker-compose.yml, build.context: .)
# car Agirh.Api reference Domain/Core/Infrastructure par ProjectReference.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Agirh.Domain/Agirh.Domain.csproj src/Agirh.Domain/
COPY src/Agirh.Core/Agirh.Core.csproj src/Agirh.Core/
COPY src/Agirh.Infrastructure/Agirh.Infrastructure.csproj src/Agirh.Infrastructure/
COPY src/Agirh.Api/Agirh.Api.csproj src/Agirh.Api/
RUN dotnet restore src/Agirh.Api/Agirh.Api.csproj

COPY src/ src/
RUN dotnet publish src/Agirh.Api/Agirh.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# TrouverRacineDepot (Program.cs) remonte l'arborescence a la recherche d'Agirh.sln pour
# localiser rag/models/ et rag/corpus/ de facon identique en local et en conteneur - seule la
# presence du fichier compte, pas son contenu.
COPY Agirh.sln ./
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Agirh.Api.dll"]
