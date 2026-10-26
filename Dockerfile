# Development image for the Growcery API (src/Growcery.Api and the projects it references).
# Uses the .NET SDK image so the container can build and run the app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev

# Working directory inside container
WORKDIR /app

# Expose HTTP port
EXPOSE 3030

# Copy the solution and project files, keeping their folders, to enable a cached restore layer.
COPY Growcery.slnx ./
COPY src/Growcery.Api/Growcery.Api.csproj src/Growcery.Api/
COPY src/Growcery.Core/Growcery.Core.csproj src/Growcery.Core/
COPY src/Growcery.Data/Growcery.Data.csproj src/Growcery.Data/

# Restore packages.
RUN dotnet restore src/Growcery.Api/Growcery.Api.csproj || true

# Run from the API project folder so its appsettings and wwwroot are the content root.
WORKDIR /app/src/Growcery.Api

# Default command; development-specific command and environment come from docker-compose.yml.
CMD ["dotnet", "run"]
