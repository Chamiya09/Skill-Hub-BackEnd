# =============================================================================
# STAGE 1: Base Runtime Environment
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# =============================================================================
# STAGE 2: Build Environment
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project manifest first to leverage Docker layer caching for NuGet restore
COPY ["Skill-Hub-BackEnd.csproj", "./"]
RUN dotnet restore "Skill-Hub-BackEnd.csproj"

# Copy remaining source code and build
COPY . .
RUN dotnet build "Skill-Hub-BackEnd.csproj" -c $BUILD_CONFIGURATION -o /app/build

# =============================================================================
# STAGE 3: Publish Binaries
# =============================================================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Skill-Hub-BackEnd.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# =============================================================================
# STAGE 4: Final Production Image
# =============================================================================
FROM base AS final
WORKDIR /app

# Copy compiled binaries from publish stage
COPY --from=publish /app/publish .

# Run as non-root user (built into .NET 8 Alpine/Debian base images for container hardening)
USER app

ENTRYPOINT ["dotnet", "Skill-Hub-BackEnd.dll"]
