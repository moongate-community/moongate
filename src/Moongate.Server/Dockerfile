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
# Optional private administration endpoint; disabled by default.
EXPOSE 2590/tcp

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Build.targets", "global.json", "./"]
COPY ["src/Moongate.Admin.Contracts/Moongate.Admin.Contracts.csproj", "src/Moongate.Admin.Contracts/"]
COPY ["src/Moongate.Server.Admin/Moongate.Server.Admin.csproj", "src/Moongate.Server.Admin/"]
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
# Restore the same architecture and self-contained assets used by publish.
# Keep NuGet packages in this layer so external CI caches carry them too.
RUN dotnet restore "src/Moongate.Server/Moongate.Server.csproj" -a "$TARGETARCH" -p:Configuration="$BUILD_CONFIGURATION" -p:SelfContained=true \
    && dotnet restore "src/Moongate.Boot/Moongate.Boot.csproj" -a "$TARGETARCH" -p:Configuration="$BUILD_CONFIGURATION" -p:SelfContained=true \
    && dotnet restore "src/Moongate.MigrationRunner/Moongate.MigrationRunner.csproj" -a "$TARGETARCH" -p:Configuration="$BUILD_CONFIGURATION" -p:SelfContained=true \
    && dotnet restore "src/Moongate.UoxItemConverter/Moongate.UoxItemConverter.csproj" -a "$TARGETARCH" -p:Configuration="$BUILD_CONFIGURATION" -p:SelfContained=true

FROM restore AS build
COPY [".editorconfig", "LICENSE", "THIRD-PARTY-NOTICES.md", "./"]
COPY src/ src/
COPY migrations/ migrations/
COPY moongate_root/data/ moongate_root/data/

FROM build AS publish
RUN dotnet publish "src/Moongate.Server/Moongate.Server.csproj" -c "$BUILD_CONFIGURATION" -a "$TARGETARCH" --self-contained true --no-restore -o /app/publish

FROM build AS migration-runner-publish
RUN dotnet publish "src/Moongate.MigrationRunner/Moongate.MigrationRunner.csproj" -c "$BUILD_CONFIGURATION" -a "$TARGETARCH" --self-contained true --no-restore -o /app/migration-runner

FROM build AS mgboot-publish
RUN dotnet publish "src/Moongate.Boot/Moongate.Boot.csproj" -c "$BUILD_CONFIGURATION" -a "$TARGETARCH" --self-contained true --no-restore -o /app/mgboot

FROM build AS uoxconv-publish
RUN dotnet publish "src/Moongate.UoxItemConverter/Moongate.UoxItemConverter.csproj" -c "$BUILD_CONFIGURATION" -a "$TARGETARCH" --self-contained true --no-restore -o /app/uoxconv

FROM restore AS sample-plugin-restore
COPY ["samples/Moongate.Sample.Plugin/Moongate.Sample.Plugin.csproj", "samples/Moongate.Sample.Plugin/"]
RUN dotnet restore "samples/Moongate.Sample.Plugin/Moongate.Sample.Plugin.csproj" -a "$TARGETARCH" -p:Configuration="$BUILD_CONFIGURATION" -p:SelfContained=false

FROM sample-plugin-restore AS sample-plugin-publish
COPY [".editorconfig", "LICENSE", "THIRD-PARTY-NOTICES.md", "./"]
COPY src/ src/
COPY samples/Moongate.Sample.Plugin/ samples/Moongate.Sample.Plugin/
RUN dotnet publish "samples/Moongate.Sample.Plugin/Moongate.Sample.Plugin.csproj" -c "$BUILD_CONFIGURATION" -a "$TARGETARCH" --self-contained false --no-restore -o /app/sample-plugin

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
