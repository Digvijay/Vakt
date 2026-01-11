FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install dependencies for OnnxRuntime (CPU)
RUN apt-get update && apt-get install -y libgomp1 curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Vakt.Intelligence/Vakt.Intelligence.csproj", "src/Vakt.Intelligence/"]
COPY ["src/Vakt.Core/Vakt.Core.csproj", "src/Vakt.Core/"]
COPY ["src/Directory.Packages.props", "src/"]
RUN dotnet restore "./src/Vakt.Intelligence/Vakt.Intelligence.csproj"
COPY . .
WORKDIR "/src/src/Vakt.Intelligence"
RUN dotnet build "./Vakt.Intelligence.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Vakt.Intelligence.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY src/Vakt.Intelligence/scripts/entrypoint.sh .
RUN chmod +x entrypoint.sh
ENTRYPOINT ["./entrypoint.sh"]
