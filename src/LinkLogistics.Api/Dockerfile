# syntax=docker/dockerfile:1
# Build context: repository root

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/LinkLogistics.Shared/LinkLogistics.Shared.csproj src/LinkLogistics.Shared/
COPY src/LinkLogistics.Core/LinkLogistics.Core.csproj src/LinkLogistics.Core/
COPY src/LinkLogistics.Infrastructure/LinkLogistics.Infrastructure.csproj src/LinkLogistics.Infrastructure/
COPY src/LinkLogistics.Api/LinkLogistics.Api.csproj src/LinkLogistics.Api/
RUN dotnet restore src/LinkLogistics.Api/LinkLogistics.Api.csproj

COPY src/LinkLogistics.Shared/ src/LinkLogistics.Shared/
COPY src/LinkLogistics.Core/ src/LinkLogistics.Core/
COPY src/LinkLogistics.Infrastructure/ src/LinkLogistics.Infrastructure/
COPY src/LinkLogistics.Api/ src/LinkLogistics.Api/
RUN dotnet publish src/LinkLogistics.Api/LinkLogistics.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "LinkLogistics.Api.dll"]
