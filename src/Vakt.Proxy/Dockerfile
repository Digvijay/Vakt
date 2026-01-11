FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy CPM configuration
COPY ["src/Directory.Packages.props", "src/"]

# Copy Project Files
COPY ["src/Vakt.Proxy/Vakt.Proxy.csproj", "src/Vakt.Proxy/"]
COPY ["src/Vakt.Core/Vakt.Core.csproj", "src/Vakt.Core/"]
COPY ["src/ProjectVakt.ServiceDefaults/ProjectVakt.ServiceDefaults.csproj", "src/ProjectVakt.ServiceDefaults/"]

# Restore
RUN dotnet restore "src/Vakt.Proxy/Vakt.Proxy.csproj"

# Copy Everything
COPY . .

# Build
WORKDIR "/src/src/Vakt.Proxy"
RUN dotnet build "Vakt.Proxy.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Vakt.Proxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install utilities
# curl no longer needed for model download (handled by C# code)
USER app

# Copy Artifacts
COPY --from=publish /app/publish .

# Fix Permissions (if needed for model dir, though app runs as 'app' usually)
# We need to ensure the app can WRITE to the models directory if it downloads it.
# The app user needs write permission to /app/models or we create it here.
USER root
RUN mkdir -p /app/models && chown -R app:app /app/models
USER app

ENV ModelPath="models/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"

ENTRYPOINT ["dotnet", "Vakt.Proxy.dll"]
