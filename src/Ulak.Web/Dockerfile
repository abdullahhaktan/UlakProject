# syntax=docker/dockerfile:1
# Build context: repository root
# DevExtreme client assets are vendored in wwwroot/lib/devextreme (committed), so no node stage.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Ulak.Shared/Ulak.Shared.csproj src/Ulak.Shared/
COPY src/Ulak.Core/Ulak.Core.csproj src/Ulak.Core/
COPY src/Ulak.Web/Ulak.Web.csproj src/Ulak.Web/
RUN dotnet restore src/Ulak.Web/Ulak.Web.csproj

COPY src/Ulak.Shared/ src/Ulak.Shared/
COPY src/Ulak.Core/ src/Ulak.Core/
COPY src/Ulak.Web/ src/Ulak.Web/
RUN dotnet publish src/Ulak.Web/Ulak.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Ulak.Web.dll"]
