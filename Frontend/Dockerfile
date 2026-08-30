# Build context is Frontend/:
#   docker build -f Frontend/Dockerfile -t openidle-frontend Frontend/
#
# One image serves every environment. The backend address is read at runtime
# from PUBLIC_WS_URL rather than compiled in, which is the whole reason the
# project runs @sveltejs/adapter-node instead of a static adapter — see
# doc/libraries/sveltejs-adapter-node.md.

FROM oven/bun:1.3.10-alpine AS build
WORKDIR /app

# --frozen-lockfile makes the build fail on a lockfile that disagrees with
# package.json rather than quietly resolving something else. The `prepare`
# script runs `svelte-kit sync`, which tsconfig.json depends on.
COPY package.json bun.lock ./
RUN bun install --frozen-lockfile

COPY . .
RUN bun run build

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
