# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json nuget.config Directory.Build.props ./
COPY LedgerLite.slnx ./
COPY src/LedgerLite.Domain/LedgerLite.Domain.csproj src/LedgerLite.Domain/
COPY src/LedgerLite.Application/LedgerLite.Application.csproj src/LedgerLite.Application/
COPY src/LedgerLite.Infrastructure/LedgerLite.Infrastructure.csproj src/LedgerLite.Infrastructure/
COPY src/LedgerLite.Api/LedgerLite.Api.csproj src/LedgerLite.Api/
RUN dotnet restore LedgerLite.slnx

COPY . .
RUN dotnet publish src/LedgerLite.Api/LedgerLite.Api.csproj -c Release -o /app/publish --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LedgerLite.Api.dll"]
