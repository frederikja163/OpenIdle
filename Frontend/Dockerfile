# Build context is the repository root, like the backend image:
#   docker build -f Frontend/Dockerfile -t openidle-frontend .
#
# The root context exists for a reason: the frontend's protocol schema is
# regenerated inside the image from the same commit's ../types.xml, and the
# generator lives in ../Generators. The Bun Alpine stage has no dotnet, so a
# short .NET SDK stage produces the TS file first. The generated file is
# git-ignored (and .dockerignored) and therefore absent from the context — it
# never ships stale. See doc/deployment.md.
#
# One image serves every environment. The backend address is read at runtime
# from PUBLIC_WS_URL rather than compiled in, which is the whole reason the
# project runs @sveltejs/adapter-node instead of a static adapter — see
# doc/libraries/sveltejs-adapter-node.md.

# The .NET stage: build the generator CLI once and run it against types.xml to
# emit the TsSchema value src/lib/debug/protocol.generated.ts needs. The root
# .dockerignore keeps bins, objs, node_modules and local state out of the COPY.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS generate
WORKDIR /src
COPY . .
RUN mkdir -p /out \
    && dotnet run --project Generators/Generator/Generator.csproj \
        --configuration Release -- \
        -i types.xml -t TsSchema -o /out/protocol.generated.ts

FROM oven/bun:1.3.10-alpine AS build
WORKDIR /app

# --frozen-lockfile makes the build fail on a lockfile that disagrees with
# package.json rather than quietly resolving something else. The `prepare`
# script runs `svelte-kit sync`, which tsconfig.json depends on.
COPY Frontend/package.json Frontend/bun.lock ./
RUN bun install --frozen-lockfile

COPY Frontend/ ./
# Overlay the freshly generated schema over any local copy that slipped past
# .dockerignore, so the image always builds from its own commit's types.xml.
COPY --from=generate /out/protocol.generated.ts ./src/lib/debug/protocol.generated.ts

# Not `bun run build`: the local `build` script starts with `bun run generate`,
# which would invoke dotnet. The generate stage above already did that work.
RUN bunx vite build

FROM oven/bun:1.3.10-alpine AS runtime
WORKDIR /app

# adapter-node emits a self-contained server — build/ imports nothing outside
# node: builtins — so no node_modules is shipped. package.json comes along only
# for its "type": "module".
COPY --from=build /app/build ./build
COPY --from=build /app/package.json ./package.json

# HOST must be 0.0.0.0 or the server is unreachable from outside the container.
# ORIGIN is set per environment (adapter-node uses it to validate form actions);
# PUBLIC_WS_URL and PUBLIC_ALLOW_WS_OVERRIDE likewise — see deploy/.
ENV NODE_ENV=production \
    HOST=0.0.0.0 \
    PORT=3000

USER bun
EXPOSE 3000

# Uses bun rather than wget: Alpine ships busybox wget, which does not accept
# GNU wget long options, and reading PORT here keeps the check correct if the
# port is overridden.
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD bun -e "fetch('http://127.0.0.1:' + (Bun.env.PORT || 3000) + '/healthz').then(r => process.exit(r.ok ? 0 : 1)).catch(() => process.exit(1))"

CMD ["bun", "./build/index.js"]