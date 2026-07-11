# ============================================================
# Stage 1: Build and publish the application
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files first for better caching
COPY ["BudgetPilotAPI/BudgetPilotAPI.csproj", "BudgetPilotAPI/"]

# Restore NuGet packages
RUN dotnet restore "BudgetPilotAPI/BudgetPilotAPI.csproj"

# Copy all source code
COPY . .

# Clean any previous build artifacts and publish fresh
# (restore is done as a separate step above; the obj/ directory must exist
# for the assets file that the restore step creates)
WORKDIR "/src/BudgetPilotAPI"
RUN rm -rf bin && \
    dotnet publish "BudgetPilotAPI.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/publish \
        /p:UseAppHost=false

# ============================================================
# Stage 2: Final runtime image
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Expose the port the application listens on
EXPOSE 8080

# Set environment variables for the container
ENV ASPNETCORE_URLS=http://+:8080

# Switch to non-root user for security
USER $APP_UID

# Set the entry point for the application
ENTRYPOINT ["dotnet", "BudgetPilotAPI.dll"]
