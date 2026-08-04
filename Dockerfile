# General Dockerfile for ASP.NET Core MVC
# Uses the .NET SDK image so the container can build and run the app.
# Adjust the base SDK tag if you want a specific .NET version (e.g., 8.0, 7.0).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev

# Working directory inside container
WORKDIR /app

# Expose HTTP port
EXPOSE 5000

# Copy solution and project files to enable cached restore layer.
COPY *.sln ./
COPY */*.csproj ./

# Restore packages.
RUN dotnet restore --no-cache || true

# Default command; development-specific command and environment come from docker-compose.yml.
CMD ["dotnet", "run"]
