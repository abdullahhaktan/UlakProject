# syntax=docker/dockerfile:1
# Build context: repository root (needs db/scripts + src/*)

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/LinkLogistics.DbMigrator/LinkLogistics.DbMigrator.csproj src/LinkLogistics.DbMigrator/
RUN dotnet restore src/LinkLogistics.DbMigrator/LinkLogistics.DbMigrator.csproj

COPY src/LinkLogistics.DbMigrator/ src/LinkLogistics.DbMigrator/
COPY db/ db/
RUN dotnet publish src/LinkLogistics.DbMigrator/LinkLogistics.DbMigrator.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "LinkLogistics.DbMigrator.dll"]
