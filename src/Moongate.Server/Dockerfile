FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 2593

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Repo-wide MSBuild settings imported by every project: they must be present at restore
# time so the restored graph matches what the later build evaluates.
COPY ["Directory.Build.props", "global.json", "./"]

# Copy every referenced project file before restore: Moongate.Server pulls in the whole
# graph (Network, Persistence, Scripting -> Core, UO.Data -> Ultima), so restore fails
# unless all of them are present. Kept as a separate layer for dependency caching.
COPY ["src/Moongate.Core/Moongate.Core.csproj", "src/Moongate.Core/"]
COPY ["src/Moongate.Ultima/Moongate.Ultima.csproj", "src/Moongate.Ultima/"]
COPY ["src/Moongate.UO.Data/Moongate.UO.Data.csproj", "src/Moongate.UO.Data/"]
COPY ["src/Moongate.Network/Moongate.Network.csproj", "src/Moongate.Network/"]
COPY ["src/Moongate.Persistence/Moongate.Persistence.csproj", "src/Moongate.Persistence/"]
COPY ["src/Moongate.Scripting/Moongate.Scripting.csproj", "src/Moongate.Scripting/"]
COPY ["src/Moongate.Server/Moongate.Server.csproj", "src/Moongate.Server/"]
RUN dotnet restore "src/Moongate.Server/Moongate.Server.csproj"

COPY . .
WORKDIR "/src/src/Moongate.Server"
RUN dotnet build "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Moongate.Server.dll"]
