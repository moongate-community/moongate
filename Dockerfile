# Base image for the final container
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS base
WORKDIR /app

# Install curl for healthcheck
RUN apk add --no-cache git


# Build image
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH=x64
RUN apk add --no-cache gcc musl-dev
WORKDIR /src
COPY ["./", "./"]
COPY . .
WORKDIR "/src/src/Moongate.Server"
RUN dotnet restore "Moongate.Server.csproj" -a $TARGETARCH
RUN dotnet build "Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build -a $TARGETARCH
# Publish image with single file
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH=x64
RUN dotnet publish "Moongate.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish \
    -a $TARGETARCH \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=Link \
    -p:PublishAot=true


RUN rm .git/ -Rf
RUN rm .github/ -Rf
RUN rm .gitignore -Rf
RUN rm .dockerignore -Rf
RUN rm ./assets -Rf
RUN rm src -Rf
# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV MOONGATE_SERVER_ROOT=/app

# Set non-root user for better security
# Creating user inside container rather than using $APP_UID since Alpine uses different user management
RUN adduser -D -h /app moongate && \
    chown -R moongate:moongate /app

# Create directories for data persistence
RUN mkdir -p /app/data /app/logs /app/scripts && \
    chown -R moongate:moongate /app/data /app/logs /app/scripts


USER moongate
ENTRYPOINT ["./Moongate.Server"]
