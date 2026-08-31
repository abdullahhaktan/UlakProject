# syntax=docker/dockerfile:1
# Build context: repository root (needs db/scripts + src/*)

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Ulak.DbMigrator/Ulak.DbMigrator.csproj src/Ulak.DbMigrator/
RUN dotnet restore src/Ulak.DbMigrator/Ulak.DbMigrator.csproj

COPY src/Ulak.DbMigrator/ src/Ulak.DbMigrator/
COPY db/ db/
RUN dotnet publish src/Ulak.DbMigrator/Ulak.DbMigrator.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Ulak.DbMigrator.dll"]
