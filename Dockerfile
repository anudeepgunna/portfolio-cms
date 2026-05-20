# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only csproj files first (better Docker layer caching)
COPY src/PortfolioCMS.Domain/PortfolioCMS.Domain.csproj             src/PortfolioCMS.Domain/
COPY src/PortfolioCMS.Application/PortfolioCMS.Application.csproj   src/PortfolioCMS.Application/
COPY src/PortfolioCMS.Infrastructure/PortfolioCMS.Infrastructure.csproj src/PortfolioCMS.Infrastructure/
COPY src/PortfolioCMS.API/PortfolioCMS.API.csproj                   src/PortfolioCMS.API/

RUN dotnet restore src/PortfolioCMS.API/PortfolioCMS.API.csproj

# Copy everything else and build
COPY . .
RUN dotnet publish src/PortfolioCMS.API/PortfolioCMS.API.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime (smaller final image) ─────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create uploads directory (used when Azure Blob is not configured)
RUN mkdir -p wwwroot/uploads

COPY --from=build /app/publish .

# Run as non-root for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "PortfolioCMS.API.dll"]
