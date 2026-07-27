# aspnet, not runtime: Moongate.Server references Moongate.Http.Plugin, which carries a FrameworkReference
# to Microsoft.AspNetCore.App, so the published app asks the host for that shared framework. On the plain
# runtime image it is absent and the process dies on the first line with "No frameworks were found" — which
# no build-time check catches, because the image builds perfectly and only fails when started.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 2593

FROM node:24-alpine AS ui
WORKDIR /ui
# Manifests first so the dependency layer survives source-only changes.
COPY ["ui/package.json", "ui/package-lock.json", "./"]
RUN npm ci
COPY ui/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Repo-wide MSBuild settings imported by every project: they must be present at restore
# time so the restored graph matches what the later build evaluates.
COPY ["Directory.Build.props", "global.json", "./"]

# Copy every referenced project file before restore: Moongate.Server pulls in the whole
# graph — the src libraries plus the four plugin projects it references. All twelve must be
# here, because `dotnet restore` does NOT fail on a missing ProjectReference: it prints
# "Skipping project ... because it was not found" and carries on, so an incomplete set
# restores a partial graph, silently, and this layer stops being worth caching.
COPY ["src/Moongate.Core/Moongate.Core.csproj", "src/Moongate.Core/"]
COPY ["src/Moongate.Ultima/Moongate.Ultima.csproj", "src/Moongate.Ultima/"]
COPY ["src/Moongate.UO.Data/Moongate.UO.Data.csproj", "src/Moongate.UO.Data/"]
COPY ["src/Moongate.Network/Moongate.Network.csproj", "src/Moongate.Network/"]
COPY ["src/Moongate.Persistence/Moongate.Persistence.csproj", "src/Moongate.Persistence/"]
COPY ["src/Moongate.Scripting/Moongate.Scripting.csproj", "src/Moongate.Scripting/"]
COPY ["src/Moongate.Server.Abstractions/Moongate.Server.Abstractions.csproj", "src/Moongate.Server.Abstractions/"]
COPY ["src/Moongate.Server/Moongate.Server.csproj", "src/Moongate.Server/"]
COPY ["plugins/Moongate.Console.Admin.Plugin/Moongate.Console.Admin.Plugin.csproj", "plugins/Moongate.Console.Admin.Plugin/"]
COPY ["plugins/Moongate.Http.Plugin/Moongate.Http.Plugin.csproj", "plugins/Moongate.Http.Plugin/"]
COPY ["plugins/Moongate.News.Plugin/Moongate.News.Plugin.csproj", "plugins/Moongate.News.Plugin/"]
COPY ["plugins/Moongate.Smtp.Plugin/Moongate.Smtp.Plugin.csproj", "plugins/Moongate.Smtp.Plugin/"]
RUN dotnet restore "src/Moongate.Server/Moongate.Server.csproj"

COPY . .
WORKDIR "/src/src/Moongate.Server"
RUN dotnet build "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Framework-dependent single-file executable: the managed assemblies are bundled into one
# native apphost (Moongate.Server), while the shared runtime still comes from the base image.
RUN dotnet publish "./Moongate.Server.csproj" -c $BUILD_CONFIGURATION -r linux-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=embedded -p:GenerateDocumentationFile=false -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# The portal, from the node stage, into the layout the server probes: ui/dist beside the executable.
# The final stage rather than the build stage, because it never takes part in compilation — it is content
# the running server reads, and copying it last keeps the .NET layers cacheable across UI-only changes.
COPY --from=ui /ui/dist/ ./ui/dist/

ENTRYPOINT ["./Moongate.Server"]
