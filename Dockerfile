# syntax=docker/dockerfile:1

# Build frontend (Vite React app)
FROM node:22-alpine AS ui-build
WORKDIR /ui

COPY ui/package.json ui/package-lock.json ./
RUN npm ci

COPY ui/ ./
RUN npm run build

# Use .NET 10 SDK and pin the newer SDK used by the repo so Roslyn matches local builds.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release
ARG DOTNET_SDK_VERSION=10.0.201
ARG TARGETARCH
WORKDIR /src

# Update OS packages and install curl for dotnet-install
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install the SDK version used by the repository so source generators run with the expected compiler.
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --version "$DOTNET_SDK_VERSION" --install-dir /usr/share/dotnet --no-path \
    && rm /tmp/dotnet-install.sh

# Copy only project metadata first to maximize restore-layer caching
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY Moongate.slnx ./
COPY src/Moongate.Network/Moongate.Network.csproj src/Moongate.Network/
COPY src/Moongate.Abstractions/Moongate.Abstractions.csproj src/Moongate.Abstractions/
COPY src/Moongate.Core/Moongate.Core.csproj src/Moongate.Core/
COPY src/Moongate.Network.Packets/Moongate.Network.Packets.csproj src/Moongate.Network.Packets/
COPY src/Moongate.Generators/Moongate.Generators.csproj src/Moongate.Generators/
COPY src/Moongate.Persistence/Moongate.Persistence.csproj src/Moongate.Persistence/
COPY src/Moongate.Plugin.Abstractions/Moongate.Plugin.Abstractions.csproj src/Moongate.Plugin.Abstractions/
COPY src/Moongate.Scripting/Moongate.Scripting.csproj src/Moongate.Scripting/
COPY src/Moongate.Email/Moongate.Email.csproj src/Moongate.Email/
COPY src/Moongate.Server.Metrics/Moongate.Server.Metrics.csproj src/Moongate.Server.Metrics/
COPY src/Moongate.Server.Abstractions/Moongate.Server.Abstractions.csproj src/Moongate.Server.Abstractions/
COPY src/Moongate.UO.Data/Moongate.UO.Data.csproj src/Moongate.UO.Data/
COPY src/Moongate.Server/Moongate.Server.csproj src/Moongate.Server/

RUN dotnet restore src/Moongate.Server/Moongate.Server.csproj

# Copy sources required by Moongate.Server
COPY src/Moongate.Network/ src/Moongate.Network/
COPY src/Moongate.Abstractions/ src/Moongate.Abstractions/
COPY src/Moongate.Core/ src/Moongate.Core/
COPY src/Moongate.Network.Packets/ src/Moongate.Network.Packets/
COPY src/Moongate.Generators/ src/Moongate.Generators/
COPY src/Moongate.Persistence/ src/Moongate.Persistence/
COPY src/Moongate.Plugin.Abstractions/ src/Moongate.Plugin.Abstractions/
COPY src/Moongate.Scripting/ src/Moongate.Scripting/
COPY src/Moongate.Email/ src/Moongate.Email/
COPY src/Moongate.Server.Metrics/ src/Moongate.Server.Metrics/
COPY src/Moongate.Server.Abstractions/ src/Moongate.Server.Abstractions/
COPY src/Moongate.UO.Data/ src/Moongate.UO.Data/
COPY src/Moongate.Server/ src/Moongate.Server/

# Publish framework-dependent server build for Alpine
RUN set -eux; \
    dotnet publish src/Moongate.Server/Moongate.Server.csproj \
      -c "$BUILD_CONFIGURATION" \
      -o /out \
      --no-restore
# Use latest ASP.NET runtime with security updates
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Update OS packages and install wget for the healthcheck
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y --no-install-recommends wget ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/moongate

RUN groupadd --system moongate \
    && useradd --system --gid moongate --home-dir /app --create-home moongate

COPY --from=publish /out/ ./
COPY --from=ui-build /ui/dist ./ui/dist

RUN mkdir -p /app /app/data /app/logs /app/plugins /app/scripts /uo && chown -R moongate:moongate /opt/moongate /app /uo

ENV MOONGATE_ROOT_DIRECTORY=/app
ENV MOONGATE_UO_DIRECTORY=/uo
ENV MOONGATE_IS_DOCKER=true
ENV MOONGATE_UI_DIST=/opt/moongate/ui/dist
EXPOSE 2593/tcp
EXPOSE 12000/udp
EXPOSE 8088/tcp

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8088/health | grep -q '^ok$' || exit 1

USER moongate
ENTRYPOINT ["dotnet", "/opt/moongate/Moongate.Server.dll"]
