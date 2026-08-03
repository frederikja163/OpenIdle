# Library decisions

Every third-party dependency used (or considered and rejected) by this project is documented here. Each file follows the template in [TEMPLATE.md](./TEMPLATE.md).

## Backend

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [C# / .NET](./csharp-dotnet.md) | adopted | 2026-08-02 | medium | low |
| [ASP.NET Core (Minimal APIs + DI)](./aspnet-core.md) | adopted | 2026-08-02 | low | low |

## Frontend

All frontend packages are declared as `devDependencies`. Until 2026-08-03 that also meant the client shipped **zero third-party runtime code** — everything was build, lint, format, or test tooling. **[shadcn-svelte](./shadcn-svelte.md) ended that**: its component set and [bits-ui](./bits-ui.md) are bundled into the client. The `devDependencies` declaration is now a packaging convention, not a statement about what reaches the browser.

### Framework and build

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [Svelte](./svelte.md) | adopted | 2026-08-03 | high | low |
| [SvelteKit](./sveltekit.md) | adopted | 2026-08-03 | high | low |
| [@sveltejs/vite-plugin-svelte](./sveltejs-vite-plugin-svelte.md) | adopted | 2026-08-03 | low | low |
| [@sveltejs/adapter-auto](./sveltejs-adapter-auto.md) | **under-review** | 2026-08-03 | low | **medium** |
| [svelte-check](./svelte-check.md) | adopted | 2026-08-03 | low | low |
| [Vite](./vite.md) | adopted | 2026-08-03 | low | low |
| [TypeScript](./typescript.md) | adopted | 2026-08-03 | medium | low |
| [@types/node](./types-node.md) | adopted | 2026-08-03 | low | low |

### Styling

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [Tailwind CSS](./tailwindcss.md) | adopted | 2026-08-03 | **high** | low |
| [@tailwindcss/vite](./tailwindcss-vite.md) | adopted | 2026-08-03 | low | low |

### Components

The first packages in this project whose code is **shipped to the browser**. The two below are the primary decisions:

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [shadcn-svelte](./shadcn-svelte.md) | adopted | 2026-08-03 | medium | **medium** |
| [bits-ui](./bits-ui.md) | adopted | 2026-08-03 | medium | **medium** |

The rest were **entailed** by those two rather than chosen independently — installed by the `shadcn-svelte` CLI to satisfy its component set — but each is documented on its own merits, including whether it would survive on them:

| Library | Decision | Date | Risk (undo) | Risk (security) | Ships JS to browser |
|---|---|---|---|---|---|
| [tailwind-merge](./tailwind-merge.md) | adopted | 2026-08-03 | low | low | yes |
| [tailwind-variants](./tailwind-variants.md) | adopted | 2026-08-03 | low | low | yes |
| [clsx](./clsx.md) | adopted | 2026-08-03 | low | low | yes |
| [@lucide/svelte](./lucide-svelte.md) | adopted | 2026-08-03 | low | low | yes (tree-shaken per icon) |
| [tw-animate-css](./tw-animate-css.md) | adopted | 2026-08-03 | low | low | **no** — CSS only |
| [@fontsource-variable/inter](./fontsource-variable-inter.md) | adopted | 2026-08-03 | low | low | **no** — CSS + woff2 |
| [@internationalized/date](./internationalized-date.md) | adopted (**unused**) | 2026-08-03 | low | low | **no** — nothing imports it |

Not separately documented: the ten transitives behind `bits-ui`, enumerated with versions and licences in [bits-ui](./bits-ui.md).

Two of these are honestly marginal on their own merits and are recorded as such — [@lucide/svelte](./lucide-svelte.md) (6.4 MB installed for one icon in current use) and [@internationalized/date](./internationalized-date.md) (declared for a peer requirement, imported nowhere). Each is kept because removing it costs recurring friction against the generator, not because it earned its place unaided.

[clsx](./clsx.md) is a special case: it looks marginal at ~240 bytes, but **it is not removable and never was**. [Svelte](./svelte.md) itself declares it as a dependency for `class={{…}}` handling, as does `svelte-toolbelt` via [bits-ui](./bits-ui.md). The shadcn CLI only promoted it to a direct declaration. Hand-rolling it was measured on this project and made the bundle **57 bytes larger**, because clsx still shipped via Svelte while our duplicate shipped alongside it.

### Linting

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [ESLint](./eslint.md) | adopted | 2026-08-03 | low | low |
| [@eslint/js](./eslint-js.md) | adopted | 2026-08-03 | low | low |
| [typescript-eslint](./typescript-eslint.md) | adopted | 2026-08-03 | low | low |
| [eslint-plugin-svelte](./eslint-plugin-svelte.md) | adopted | 2026-08-03 | low | low |
| [eslint-config-prettier](./eslint-config-prettier.md) | adopted | 2026-08-03 | low | **medium** |
| [globals](./globals.md) | adopted | 2026-08-03 | low | low |

### Formatting

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [Prettier](./prettier.md) | adopted | 2026-08-03 | low | low |
| [prettier-plugin-svelte](./prettier-plugin-svelte.md) | adopted | 2026-08-03 | low | low |
| [prettier-plugin-tailwindcss](./prettier-plugin-tailwindcss.md) | adopted | 2026-08-03 | low | low |

### Testing

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [Vitest](./vitest.md) | adopted | 2026-08-03 | low | low |
| [Playwright](./playwright.md) | adopted | 2026-08-03 | low | **medium** |

## Open items

Two decisions remain unsettled:

1. **[@sveltejs/adapter-auto](./sveltejs-adapter-auto.md)** — a placeholder that detects nothing on a self-hosted VPS and downloads an unpinned adapter at build time, defeating the lockfile. Should be replaced with `@sveltejs/adapter-static` plus `ssr = false`, per the SPA constraint recorded in [SvelteKit](./sveltekit.md).
2. **[@internationalized/date](./internationalized-date.md)** — declared to satisfy a [bits-ui](./bits-ui.md) peer requirement, but **imported by nothing**. It ships zero bytes today. It can be dropped outright if the project commits to native `<input type="date">` over a custom calendar component; the only consequence is an unmet-peer warning on install. Decide this before any date component is added, since adopting one makes it load-bearing.

## Configuration fixes

Not dependency decisions, but defects found during this audit:

- **[Playwright](./playwright.md)**: `test:e2e` runs bare `playwright install`, downloading *all three* browser families on every invocation while only Chromium is ever launched. Pin it to `chromium` and move it out of the per-run test command into a cacheable setup step. Its `webServer` also shells out to `npm` on a Bun project.
- **[Vitest](./vitest.md)**: only a `server` project is configured, so Svelte component tests have nowhere to run despite the config anticipating them. Adding a browser project also sets the division of labour with Playwright — components under Vitest, full-build journeys under Playwright.
- **[globals](./globals.md)**: applied as a project-wide union of `browser` and `node`, telling ESLint that `document` is valid in `vite.config.ts` and `process` is valid inside a component. Scoping each to the files that need them would be more accurate.

## Standing notes

- **[eslint-config-prettier](./eslint-config-prettier.md) was compromised in July 2025 (CVE-2025-54313)** with a **Windows-targeted RCE** payload; this project is developed on Windows. We are on the clean 10.1.8. Never downgrade below it, treat any change to its lockfile entry as a security event, and do not run `bun update` unattended.
- **[TypeScript](./typescript.md) is deliberately held at 6.x**, not 7.x. TypeScript 7.0 shipped 2026-07-08 but has no stable programmatic compiler API, so [typescript-eslint](./typescript-eslint.md) and [svelte-check](./svelte-check.md) cannot run on it, and SvelteKit's peer range does not accept it. Revisit around **October 2026** when TypeScript 7.1 is expected.
- **Three Rust native binaries run during every build**: `rolldown` and `lightningcss` (via [Vite](./vite.md)) and `@tailwindcss/oxide` (via [@tailwindcss/vite](./tailwindcss-vite.md)). These are prebuilt, platform-specific, and not meaningfully reviewable. `bun.lock` pins exact resolutions with integrity hashes — that pinning is the mitigation, so lockfile changes are reviewable events.
- **[Tailwind CSS](./tailwindcss.md) has the highest undo risk in the frontend set** (`high`), because utility classes spread across every component by design. It was adopted on a deliberate argument — authoring speed plus an *enforced* rather than advisory design scale — over a genuinely viable zero-package alternative. Treat it as settled; reversing it later means restyling the whole client.
- **Biome would replace eight packages with one** — [ESLint](./eslint.md), [@eslint/js](./eslint-js.md), [typescript-eslint](./typescript-eslint.md), [eslint-plugin-svelte](./eslint-plugin-svelte.md), [eslint-config-prettier](./eslint-config-prettier.md), [globals](./globals.md), [Prettier](./prettier.md), and [prettier-plugin-svelte](./prettier-plugin-svelte.md). It is blocked today only by the lack of Svelte support. Re-open the lint and format decisions when that lands.
- **`shadcn-svelte add` writes network-fetched code directly into `src/`**, not into `node_modules`. Its output is therefore a reviewable diff and **must be read before committing** — that review is the mitigation for the registry being a supply-chain entry point. See [shadcn-svelte](./shadcn-svelte.md).
- **[shadcn-svelte](./shadcn-svelte.md) is scoped to application chrome** — login, profiles, settings, forms, modals. The game UI proper (resource counters, progress bars, inventory grids) stays hand-written against Tailwind's scale. This boundary is a *condition* of the adoption, not a preference: it is what holds the undo risk at `medium`. If it erodes, re-open the decision.
- **Client bundle weight is now a live concern.** Measured on this project: baseline 29.8 KB gzip, +18.4 KB for the first shadcn component (a fixed `tailwind-merge`/`tailwind-variants` cost), +17.2 KB more for a dialog via [bits-ui](./bits-ui.md). Prefer native `<dialog>`/`<select>` wherever they suffice.
- `svelte-toolbelt` (transitive via [bits-ui](./bits-ui.md)) ships **no `license` field** in its `package.json`; its bundled LICENSE file is MIT under the same maintainer as shadcn-svelte. Harmless, but automated licence tooling will flag it.
- No install or postinstall scripts exist anywhere in the current dependency tree — **re-verified across the shadcn-svelte additions on 2026-08-03**. Treat the appearance of one as a red flag.
