# syntax=docker/dockerfile:1
# Build context: repository root
# DevExtreme client assets are vendored in wwwroot/lib/devextreme (committed), so no node stage.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/LinkLogistics.Shared/LinkLogistics.Shared.csproj src/LinkLogistics.Shared/
COPY src/LinkLogistics.Core/LinkLogistics.Core.csproj src/LinkLogistics.Core/
COPY src/LinkLogistics.Web/LinkLogistics.Web.csproj src/LinkLogistics.Web/
RUN dotnet restore src/LinkLogistics.Web/LinkLogistics.Web.csproj

COPY src/LinkLogistics.Shared/ src/LinkLogistics.Shared/
COPY src/LinkLogistics.Core/ src/LinkLogistics.Core/
COPY src/LinkLogistics.Web/ src/LinkLogistics.Web/
RUN dotnet publish src/LinkLogistics.Web/LinkLogistics.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "LinkLogistics.Web.dll"]
