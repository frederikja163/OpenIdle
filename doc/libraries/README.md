# Library decisions

Every third-party dependency used (or considered and rejected) by this project is documented here. Each file follows the template in [TEMPLATE.md](./TEMPLATE.md).

## Backend

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [C# / .NET](./csharp-dotnet.md) | adopted | 2026-08-02 | medium | low |
| [ASP.NET Core (Minimal APIs + DI)](./aspnet-core.md) | adopted | 2026-08-02 | low | low |

| [EF Core + SQLite](./ef-core.md) | adopted | 2026-08-03 | medium | low |
| [DTO XML contract](./dto-xml-contract.md) | in-house | 2026-08-04 | medium | low |
| [CommandLineParser](./commandlineparser.md) | adopted | 2026-08-06 | low | low |
| [Microsoft.CodeAnalysis.CSharp](./microsoft-codeanalysis-csharp.md) | adopted | 2026-08-06 | low | low |
| [Microsoft.CodeAnalysis.Analyzers](./microsoft-codeanalysis-analyzers.md) | adopted | 2026-08-06 | low | low |
| [NUnit (test framework)](./nunit.md) | adopted | 2026-08-04 | low | low |
| [Microsoft.AspNetCore.Mvc.Testing](./mvc-testing.md) | rejected | 2026-08-04 | low | low |

`CommandLineParser` is dev-tooling only — it parses args for the `Generator` console app (never shipped). It replaced an initial `System.CommandLine` 2.0.10 pick; the owner preferred the attribute-based syntax and the zero-dependency footprint, accepting the package's dormancy (no stable release since 2022) for a small fixed CLI. See the alternatives table in its document.

`Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` are build-time only — they are what the [DTO XML contract](./dto-xml-contract.md)'s source generator ([Generator.Backend](../../Generators/Backend)) compiles against, referenced `PrivateAssets="all"` and loaded by the compiler as analyzers. Never shipped to the runtime or the browser. Note the version skew: the generator is built against Roslyn 4.14.0 while the .NET 10 SDK hosts Roslyn 5.0.0 — safe (hosts load older-built analyzers), but the pin must never exceed the host's version on upgrades.

## Frontend

All frontend packages are declared as `devDependencies` — a packaging convention, not a statement about what reaches the browser. The client now ships third-party code in two places: the `cn()` helper at `Frontend/src/lib/utils/stylingUtils.ts`, which imports [clsx](./clsx.md) and [tailwind-merge](./tailwind-merge.md), and the [@lucide/svelte](./lucide-svelte.md) icons used by the app chrome. [shadcn-svelte](./shadcn-svelte.md)'s `button`, `badge`, `card`, `input` and `dialog` are vendored under `src/lib/components/ui/`, and `button` and `badge` bring `tailwind-variants` with them; the rest of the component set is declared and lockfile-pinned but not bundled. `dialog` is the component that needed [bits-ui](./bits-ui.md), so that dependency and its transitive tree now ship to the browser — measured at +14.9 KB gzip.

### Framework and build

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [Svelte](./svelte.md) | adopted | 2026-08-03 | high | low |
| [SvelteKit](./sveltekit.md) | adopted | 2026-08-03 | high | low |
| [@sveltejs/vite-plugin-svelte](./sveltejs-vite-plugin-svelte.md) | adopted | 2026-08-03 | low | low |
| [@sveltejs/adapter-node](./sveltejs-adapter-node.md) | adopted | 2026-08-27 | low | low |
| [@sveltejs/adapter-auto](./sveltejs-adapter-auto.md) | rejected | 2026-08-27 | low | low |
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

The browser-bound half of the frontend set, and as of the profiles delete confirmation all of it ships: the [@lucide/svelte](./lucide-svelte.md) icons, `cn()`'s [clsx](./clsx.md) and [tailwind-merge](./tailwind-merge.md), `tailwind-variants` behind Button and Badge, and [bits-ui](./bits-ui.md) behind the vendored `dialog`. The two below are the primary decisions:

| Library | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|
| [shadcn-svelte](./shadcn-svelte.md) | adopted | 2026-08-03 | medium | **medium** |
| [bits-ui](./bits-ui.md) | adopted | 2026-08-03 | medium | **medium** |

The rest were **entailed** by those two rather than chosen independently — installed by the `shadcn-svelte` CLI to satisfy its component set — but each is documented on its own merits, including whether it would survive on them:

| Library | Decision | Date | Risk (undo) | Risk (security) | Ships JS to browser |
|---|---|---|---|---|---|
| [tailwind-merge](./tailwind-merge.md) | adopted | 2026-08-03 | low | low | yes |
| [tailwind-variants](./tailwind-variants.md) | adopted | 2026-08-03 | low | low | **yes** — Button and Badge call `tv()` |
| [clsx](./clsx.md) | adopted | 2026-08-03 | low | low | yes |
| [@lucide/svelte](./lucide-svelte.md) | adopted | 2026-08-03 | low | low | yes (tree-shaken per icon) |
| [tw-animate-css](./tw-animate-css.md) | adopted | 2026-08-03 | low | low | **no** — CSS only |
| [@internationalized/date](./internationalized-date.md) | adopted (**unused**) | 2026-08-03 | low | low | **no** — nothing imports it |
| [@floating-ui/dom](./floating-ui-dom.md) | adopted | 2026-08-05 | low | low | yes (tooltip positioning) |

Not separately documented: the ten transitives behind `bits-ui`, enumerated with versions and licences in [bits-ui](./bits-ui.md) — since 2026-08-05, `@floating-ui/dom` and its two direct deps are promoted out of that count into [floating-ui-dom](./floating-ui-dom.md).

### Typefaces

The [OpenIdle Design System](../../Frontend/src/lib/styles/openidle/index.css) specifies three faces for three jobs — display, prose, numbers. All three are self-hosted through Fontsource, OFL-1.1, zero dependencies, no install scripts, and **no JavaScript reaching the browser**: the packages contribute CSS and woff2 only.

| Library | Job | Decision | Date | Risk (undo) | Risk (security) |
|---|---|---|---|---|---|
| [@fontsource/chakra-petch](./fontsource-chakra-petch.md) | HUD display, wordmark | **under-review** | 2026-08-04 | low | low |
| [@fontsource-variable/ibm-plex-sans](./fontsource-variable-ibm-plex-sans.md) | prose, document default | **under-review** | 2026-08-04 | low | low |
| [@fontsource/ibm-plex-mono](./fontsource-ibm-plex-mono.md) | every number | **under-review** | 2026-08-04 | low | low |

All three are `under-review` for the same reason, recorded in each document: **they are substitutions, not brand fonts.** The design system's own `tokens/fonts.css` is headed *"SUBSTITUTED FONTS — no font binaries were provided with the source material"* and its readme carries a standing *"Font substitution — action needed"* item. They are in use because the interface needs typefaces and these are good ones; the choice is provisional.

**`@fontsource-variable/inter` was uninstalled on 2026-08-04 and its decision document removed.** It was the `vega` preset's face and the previous `--font-body`; when the design system landed, nothing imported it any more. The privacy argument first made in that document — self-host, never the Google Fonts CDN — is restated in full in [@fontsource/chakra-petch](./fontsource-chakra-petch.md) and carried by the other two.

Two of these are honestly marginal on their own merits and are recorded as such — [@lucide/svelte](./lucide-svelte.md) (6.4 MB installed for three icons in current use) and [@internationalized/date](./internationalized-date.md) (declared for a peer requirement, imported nowhere). Each is kept because removing it costs recurring friction against the generator, not because it earned its place unaided.

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

Three decisions remain unsettled. The adapter placeholder that used to head this list was resolved on 2026-08-27: `adapter-auto` was replaced by **[@sveltejs/adapter-node](./sveltejs-adapter-node.md)** rather than the `adapter-static` this list recommended, because the frontend ships as its own container and one image has to serve both dev and prod with `PUBLIC_WS_URL` read at run time. The reasoning, and the "second backend" objection it answers, are in that document; what survives as a follow-up is `ssr = false`, which is still worth adopting and is recorded there rather than here.

1. **[@internationalized/date](./internationalized-date.md)** — declared to satisfy a [bits-ui](./bits-ui.md) peer requirement, but **imported by nothing**. It ships zero bytes today. It can be dropped outright if the project commits to native `<input type="date">` over a custom calendar component; the only consequence is an unmet-peer warning on install. Decide this before any date component is added, since adopting one makes it load-bearing.
2. **The three typefaces** — [Chakra Petch](./fontsource-chakra-petch.md), [IBM Plex Sans](./fontsource-variable-ibm-plex-sans.md), [IBM Plex Mono](./fontsource-ibm-plex-mono.md) — are substitutions the design system picked in the absence of any supplied font binaries, and it flags them for replacement itself. Settle whether OpenIdle commissions or licenses real brand faces, or promotes these to `adopted`. The swap is cheap either way: each face is named in exactly one `--font-*` declaration in [layout.css](../../Frontend/src/routes/layout.css). Of the three, [IBM Plex Mono](./fontsource-ibm-plex-mono.md) is the most reconsiderable on its own merits — `font-variant-numeric: tabular-nums` on the body face solves most of what it is bought for, at zero bytes.
3. **TS output handshake** — the [DTO XML contract](./dto-xml-contract.md) originally anticipated `fast-xml-parser` in the frontend; that idea was dropped when the TS emitter was implemented in C# inside the `Generator` CLI (`-t ts`), so no new frontend dependency exists. *Resolved 2026-08-30*: the generated TS reaches the frontend through a **wired build step** — `Frontend/package.json` runs the CLI before `dev`, `build` and `check`, and the `Frontend/Dockerfile` regenerates the schema in a .NET SDK stage from the same commit's `types.xml`. The generated file stays git-ignored (`**/*.generated.*`); nothing is ever checked in, and the image never ships a stale copy. See [deployment](../deployment.md).

## Configuration fixes

Not dependency decisions, but defects found during this audit:

- **[Playwright](./playwright.md)** — *fixed*: `test:e2e` ran bare `playwright install`, downloading *all three* browser families on every invocation while only Chromium is ever launched. The download is now pinned to `chromium` in a separate `test:e2e:setup` script that CI runs and caches once; `test:e2e` is `playwright test` alone. Its `webServer` already runs on Bun (`bun run build && bun run preview`).
- **[Vitest](./vitest.md)** — a `client` browser project runs `*.svelte.{test,spec}.*` in Chromium via the already-installed Playwright. That sets the division of labour with [Playwright](./playwright.md) — components under Vitest, full-build journeys under Playwright.
- **[globals](./globals.md)**: applied as a project-wide union of `browser` and `node`, telling ESLint that `document` is valid in `vite.config.ts` and `process` is valid inside a component. Scoping each to the files that need them would be more accurate.

## Standing notes

- **[eslint-config-prettier](./eslint-config-prettier.md) was compromised in July 2025 (CVE-2025-54313)** with a **Windows-targeted RCE** payload; this project is developed on Windows. We are on the clean 10.1.8. Never downgrade below it, treat any change to its lockfile entry as a security event, and do not run `bun update` unattended.
- **[TypeScript](./typescript.md) is deliberately held at 6.x**, not 7.x. TypeScript 7.0 shipped 2026-07-08 but has no stable programmatic compiler API, so [typescript-eslint](./typescript-eslint.md) cannot run on it and SvelteKit's peer range does not accept it. ([svelte-check](./svelte-check.md) can reach 7.x via its `--tsgo` or experimental-API flags; this project declines both for a CI gate, so it is a soft constraint rather than a hard one.) Revisit around **October 2026** when TypeScript 7.1 is expected.
- **Three Rust native binaries run during every build**: `rolldown` and `lightningcss` (via [Vite](./vite.md)) and `@tailwindcss/oxide` (via [@tailwindcss/vite](./tailwindcss-vite.md)). These are prebuilt, platform-specific, and not meaningfully reviewable. `bun.lock` pins exact resolutions with integrity hashes — that pinning is the mitigation, so lockfile changes are reviewable events.
- **[Tailwind CSS](./tailwindcss.md) has the highest undo risk in the frontend set** (`high`), because utility classes spread across every component by design. It was adopted on a deliberate argument — authoring speed plus an *enforced* rather than advisory design scale — over a genuinely viable zero-package alternative. Treat it as settled; reversing it later means restyling the whole client.
- **Biome would replace eight packages with one** — [ESLint](./eslint.md), [@eslint/js](./eslint-js.md), [typescript-eslint](./typescript-eslint.md), [eslint-plugin-svelte](./eslint-plugin-svelte.md), [eslint-config-prettier](./eslint-config-prettier.md), [globals](./globals.md), [Prettier](./prettier.md), and [prettier-plugin-svelte](./prettier-plugin-svelte.md). It is blocked today only by the lack of Svelte support. Re-open the lint and format decisions when that lands.
- **`shadcn-svelte add` writes network-fetched code directly into `src/`**, not into `node_modules`. Its output is therefore a reviewable diff and **must be read before committing** — that review is the mitigation for the registry being a supply-chain entry point. See [shadcn-svelte](./shadcn-svelte.md).
- **[shadcn-svelte](./shadcn-svelte.md) is scoped to application chrome** — login, profiles, settings, forms, modals. The game UI proper (resource counters, progress bars, inventory grids) stays hand-written against Tailwind's scale. This boundary is a *condition* of the adoption, not a preference: it is what holds the undo risk at `medium`. If it erodes, re-open the decision.
- **Client bundle weight is now a live concern.** Measured on this project: baseline 29.8 KB gzip, +18.4 KB for the first shadcn component (a fixed `tailwind-merge`/`tailwind-variants` cost), and **+14.9 KB more for the dialog via [bits-ui](./bits-ui.md)** — the figure re-measured when it was actually vendored, a little under the +17.2 KB the evaluation had predicted. Prefer native `<dialog>`/`<select>` wherever they suffice.
- `svelte-toolbelt` (transitive via [bits-ui](./bits-ui.md)) ships **no `license` field** in its `package.json`; its bundled LICENSE file is MIT under the same maintainer as shadcn-svelte. Harmless, but automated licence tooling will flag it.
- No install or postinstall scripts exist anywhere in the current dependency tree — **re-verified across the shadcn-svelte additions on 2026-08-03**. Treat the appearance of one as a red flag.
