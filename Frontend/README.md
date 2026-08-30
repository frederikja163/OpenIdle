# sv

Everything you need to build a Svelte project, powered by [`sv`](https://github.com/sveltejs/cli).

## Creating a project

If you're seeing this, you've probably already done this step. Congrats!

```sh
# create a new project
bun x sv create my-app
```

To recreate this project with the same configuration:

```sh
# recreate this project
bun x sv@0.17.0 create --template minimal --types ts --add prettier eslint vitest="usages:unit" playwright tailwindcss="plugins:none" --install bun Frontend
```

## Developing

Once you've created a project and installed dependencies with `bun install`, start a development server:

```sh
bun run dev

# or start the server and open the app in a new browser tab
bun run dev -- --open
```

## Environment

Three files, with one job each:

- `.env` — tracked non-secret defaults, read by `bun run dev`, `build` and
  `preview`. A deployed build does not read it: it takes `PUBLIC_*` values from
  the process environment instead.
- `.env.example` — the template for per-machine overrides (see above).
- `.env.local` — gitignored, holds this machine's real values and any secrets,
  and overrides the other two.

| Variable        | Default                  |
| --------------- | ------------------------ |
| `PUBLIC_WS_URL` | `ws://localhost:5066/ws` |
| `PUBLIC_DEBUG`  | `true`                   |

## Building

To create a production version of your app:

```sh
bun run build
```

You can preview the production build with `bun run preview`.

> To deploy your app, you may need to install an [adapter](https://svelte.dev/docs/kit/adapters) for your target environment.
