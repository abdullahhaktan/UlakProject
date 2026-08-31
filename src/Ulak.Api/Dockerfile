# syntax=docker/dockerfile:1
# Build context: repository root

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Ulak.Shared/Ulak.Shared.csproj src/Ulak.Shared/
COPY src/Ulak.Core/Ulak.Core.csproj src/Ulak.Core/
COPY src/Ulak.Infrastructure/Ulak.Infrastructure.csproj src/Ulak.Infrastructure/
COPY src/Ulak.Api/Ulak.Api.csproj src/Ulak.Api/
RUN dotnet restore src/Ulak.Api/Ulak.Api.csproj

COPY src/Ulak.Shared/ src/Ulak.Shared/
COPY src/Ulak.Core/ src/Ulak.Core/
COPY src/Ulak.Infrastructure/ src/Ulak.Infrastructure/
COPY src/Ulak.Api/ src/Ulak.Api/
RUN dotnet publish src/Ulak.Api/Ulak.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Ulak.Api.dll"]
