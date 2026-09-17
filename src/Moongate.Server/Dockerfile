FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["src/Moongate.Core/Moongate.Core.csproj", "src/Moongate.Core/"]
COPY ["src/Moongate.Persistence/Moongate.Persistence.csproj", "src/Moongate.Persistence/"]
COPY ["src/Moongate.Network/Moongate.Network.csproj", "src/Moongate.Network/"]
COPY ["src/Moongate.Server.Core/Moongate.Server.Core.csproj", "src/Moongate.Server.Core/"]
COPY ["src/Moongate.Server/Moongate.Server.csproj", "src/Moongate.Server/"]
RUN dotnet restore "src/Moongate.Server/Moongate.Server.csproj"
COPY . .
WORKDIR "/src/src/Moongate.Server"
RUN dotnet build "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Moongate.Server"]
