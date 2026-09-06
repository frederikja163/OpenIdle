# Build context is the REPOSITORY ROOT, not Backend/:
#   docker build -f Backend/Dockerfile -t openidle-backend .
# Backend.csproj references Generators/Core and Generators/Backend as analyzers
# and ../types.xml as an AdditionalFile, so a Backend/-only context cannot
# compile. See .dockerignore for what is kept out of that context.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is redone only when a dependency changes
# rather than on every source edit. Generators/Generator is the standalone TS
# emitter CLI and is not in Backend's reference graph, so it is not restored.
COPY Backend/Backend.csproj Backend/
COPY Generators/Core/Generator.Core.csproj Generators/Core/
COPY Generators/Backend/Generator.Backend.csproj Generators/Backend/
RUN dotnet restore Backend/Backend.csproj

COPY types.xml ./
COPY Generators/ Generators/
COPY Backend/ Backend/

# Build provenance for the version footer: CI passes the published commit and
# its committer date (unix seconds) as build-args, and Backend.csproj stamps
# them into the assembly. Declared after the source COPYs so a new value
# invalidates only the publish layer, never the restore. Empty for a local
# `docker build`, which the backend then reports as a local build.
ARG GIT_COMMIT=
ARG GIT_COMMIT_TIME=
RUN dotnet publish Backend/Backend.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:GitCommit=$GIT_COMMIT \
    -p:GitCommitTime=$GIT_COMMIT_TIME

# Debian, not Alpine: SQLitePCLRaw.bundle_e_sqlite3 ships a glibc-linked native
# library, and the musl variant would have to be swapped in to use Alpine.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# For HEALTHCHECK below — the runtime image ships no HTTP client of its own.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The database is a plain file, so it only survives a redeploy on a mounted
# volume. Set as an env var rather than baked into appsettings.json so the same
# image can be pointed elsewhere without a rebuild.
#
# AllowedWsOrigins is deliberately NOT defaulted here: empty means "any origin",
# which is right for local development and wrong for every deployed environment,
# so each one states its own (see deploy/docker-compose.*.yml).
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/openidle.db"

# The aspnet image ships a non-root `app` user but does not select it, and /data
# has to be writable by that user before the switch.
RUN mkdir -p /data && chown -R app:app /data
VOLUME ["/data"]

COPY --from=build --chown=app:app /app/publish .

USER app
EXPOSE 8080

# The port here has to match ASPNETCORE_URLS above; they are a pair.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1

# EF migrations are applied by Program.cs on boot, so there is no separate
# migration step to run before this.
ENTRYPOINT ["dotnet", "Backend.dll"]
