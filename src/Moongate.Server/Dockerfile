FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS base
# The server keeps its logs, config, plugins and saves under one root. Hold that
# root outside /app and create it owned by the runtime user, so a fresh named
# volume inherits that ownership instead of landing root-owned and unwritable.
# Prepare config too: a nested TOML mount must not create its parent as root.
ENV MOONGATE_ROOT=/data
RUN mkdir -p /data/config && chown $APP_UID:$APP_UID /data /data/config
USER $APP_UID
WORKDIR /app
VOLUME ["/data"]
EXPOSE 2593
EXPOSE 2595/tcp

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["src/Moongate.Boot/Moongate.Boot.csproj", "src/Moongate.Boot/"]
COPY ["src/Moongate.Core/Moongate.Core.csproj", "src/Moongate.Core/"]
COPY ["src/Moongate.Persistence/Moongate.Persistence.csproj", "src/Moongate.Persistence/"]
COPY ["src/Moongate.Persistence.Migrations/Moongate.Persistence.Migrations.csproj", "src/Moongate.Persistence.Migrations/"]
COPY ["src/Moongate.MigrationRunner/Moongate.MigrationRunner.csproj", "src/Moongate.MigrationRunner/"]
COPY ["src/Moongate.Network/Moongate.Network.csproj", "src/Moongate.Network/"]
COPY ["src/Moongate.Network.Packets/Moongate.Network.Packets.csproj", "src/Moongate.Network.Packets/"]
COPY ["src/Moongate.Server.Core/Moongate.Server.Core.csproj", "src/Moongate.Server.Core/"]
COPY ["src/Moongate.Server.Ultima/Moongate.Server.Ultima.csproj", "src/Moongate.Server.Ultima/"]
COPY ["src/Moongate.Ultima/Moongate.Ultima.csproj", "src/Moongate.Ultima/"]
COPY ["src/Moongate.Scripting/Moongate.Scripting.csproj", "src/Moongate.Scripting/"]
COPY ["src/Moongate.Server/Moongate.Server.csproj", "src/Moongate.Server/"]
COPY ["src/Moongate.UoxItemConverter/Moongate.UoxItemConverter.csproj", "src/Moongate.UoxItemConverter/"]
RUN dotnet restore "src/Moongate.Server/Moongate.Server.csproj"
RUN dotnet restore "src/Moongate.Boot/Moongate.Boot.csproj"
RUN dotnet restore "src/Moongate.MigrationRunner/Moongate.MigrationRunner.csproj"
RUN dotnet restore "src/Moongate.UoxItemConverter/Moongate.UoxItemConverter.csproj"
COPY . .
WORKDIR "/src/src/Moongate.Server"
RUN dotnet build "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM build AS migration-runner-publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/src/Moongate.MigrationRunner"
RUN dotnet publish "./Moongate.MigrationRunner.csproj" -c $BUILD_CONFIGURATION -o /app/migration-runner

FROM build AS mgboot-publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/src/Moongate.Boot"
RUN dotnet publish "./Moongate.Boot.csproj" -c $BUILD_CONFIGURATION -o /app/mgboot

FROM build AS uoxconv-publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/src/Moongate.UoxItemConverter"
RUN dotnet publish "./Moongate.UoxItemConverter.csproj" -c $BUILD_CONFIGURATION -o /app/uoxconv

FROM build AS sample-plugin-publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/samples/Moongate.Sample.Plugin"
RUN dotnet publish "./Moongate.Sample.Plugin.csproj" -c $BUILD_CONFIGURATION -o /app/sample-plugin

FROM base AS sample-plugin
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=mgboot-publish /app/mgboot/mgboot ./mgboot
COPY --from=migration-runner-publish /app/migration-runner ./migration-runner
COPY --from=uoxconv-publish /app/uoxconv/mg-uoxconv ./mg-uoxconv
COPY --from=sample-plugin-publish /app/sample-plugin /opt/moongate/sample-plugin
ENTRYPOINT ["./Moongate.Server"]

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=mgboot-publish /app/mgboot/mgboot ./mgboot
COPY --from=migration-runner-publish /app/migration-runner ./migration-runner
COPY --from=uoxconv-publish /app/uoxconv/mg-uoxconv ./mg-uoxconv
ENTRYPOINT ["./Moongate.Server"]
