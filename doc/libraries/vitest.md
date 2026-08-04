# Vitest

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 4.1.10 (declared `^4.1.8`)

## 1. Problem

The client will contain logic worth testing independently of the DOM: parsing and validating socket messages from the C# backend, formatting large numbers (an idle game displays values that grow past what a naive formatter handles), computing offline progress deltas, and deriving state from server snapshots. These are pure functions where a bug produces wrong numbers on a player's screen. Testing them requires a runner that understands TypeScript and the project's module resolution without a separate build step.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Vitest 4** (chosen) | 4.1.10, 2.1 MB, 20 direct deps | Runs on [Vite](./vite.md), so it reuses `vite.config.ts` directly — identical module resolution, aliases, and plugins as the app. Jest-compatible API, native TS/ESM, browser mode available for component tests | Very active: 88M weekly downloads, 477 releases, latest 2026-07-06, VoidZero-backed | MIT | High: config sharing with Vite is a genuine correctness benefit, not just convenience |
| Jest | 30.x | The long-standing default; largest ecosystem | Active (Meta/OpenJS) | MIT | Low: needs separate transform config for TS and ESM, and would resolve modules differently from our Vite build |
| `node --test` | built into Node | Zero dependencies — genuinely free | n/a | n/a | **Medium-high**: the minimalist option, and a serious contender. See below |
| `bun test` | built into Bun | Zero dependencies; this project already installs with Bun; very fast; Jest-compatible API | Active (Oven) | MIT | **Medium-high**: arguably the strongest challenger. See below |
| No unit tests | 0 bytes | Nothing to install | n/a | n/a | Low: the backend is tested; leaving client number-handling untested is inconsistent |
| Build in-house | n/a | Exactly our needs | Us | n/a | Medium: a minimal assert-and-report harness is genuinely small. See build-vs-buy |

Why the others lost: Jest is the weakest option here — it would need its own TypeScript transform and would resolve modules differently from the actual build, which is precisely the bug class a test runner should not introduce. `node --test` and `bun test` are addressed directly below.

## 3. Decision & rationale

Adopt **Vitest 4**, with the zero-dependency alternatives acknowledged rather than waved away.

`bun test` deserves a straight answer, because this project installs with Bun and Bun ships a fast, Jest-compatible test runner at zero dependency cost. Under a minimal-dependencies principle that is a real argument. It loses on one specific point: it does not use `vite.config.ts`. Our Vite config carries the SvelteKit plugin, the Tailwind plugin, and SvelteKit's `$lib` aliasing, and a runner that does not read it resolves modules differently from the app. Tests that pass against different resolution than production uses are tests that can be wrong in the one direction that matters. `node --test` has the same gap plus a weaker assertion API and no `.svelte` story at all.

Vitest's decisive property is that `vite.config.ts` *is* the test config — visible in our setup, where `defineConfig` is imported from `vitest/config` and the test project `extends: './vite.config.ts'`. Tests resolve imports exactly as the build does. For a project that will eventually want to test Svelte components (Vitest browser mode) that gap only widens.

One gap in the initial configuration is worth recording rather than presenting the setup as complete: it started with **only a `server` project** (`environment: 'node'`, excluding `*.svelte.spec.ts`). There was no client or browser project, so Svelte component tests had nowhere to run — the exclusion pattern anticipated them, but no project picked them up, and such tests passed green without executing. **Resolved 2026-08-04**: a `client` browser project now collects `*.svelte.{test,spec}.*` and runs them in Chromium via the already-installed [Playwright](./playwright.md), which also sets the division of labour: components under Vitest, full-build journeys under Playwright. `expect: { requireAssertions: true }` guards against silently passing empty tests, and applies to both projects.

### Pros

- Shares `vite.config.ts`, so tests and production resolve modules identically — a correctness property, not a convenience.
- Native TypeScript and ESM with no additional transform configuration.
- Jest-compatible API, so existing knowledge and documentation transfer.
- Watch mode is fast, built on Vite's dev-server machinery.
- `requireAssertions` is configured, preventing assertion-free tests from passing.
- Browser mode offers a path to component testing without adopting a separate tool.
- MIT, 88M weekly downloads, VoidZero-backed with full-time maintenance.

### Cons

- 20 direct dependencies — the largest direct fan-out in the frontend set after ESLint. Mitigated by roughly half being first-party `@vitest/*` sub-packages.
- Depends on Vite itself, so a Vite major can force a Vitest major in lockstep.
- Configured for server-side tests initially; the browser project needed for component tests was added on 2026-08-04 (`@vitest/browser` + `@vitest/browser-playwright`) and requires Playwright's `chromium` to be installed to run.
- `bun test` would deliver much of this at zero dependency cost; we are paying 20 dependencies for config sharing.

## 4. Build-vs-buy

Closer than for most tooling. A minimal harness — collect `*.spec.ts`, run them, assert, report failures — is genuinely a day's work, especially on top of Node's built-in `assert`. Under the hours-not-weeks rule that is a legitimate build case, and it would be honest to say so.

Buying wins on the parts that are not the harness: watch mode with dependency-graph invalidation, parallel execution, mocking and spies, snapshot handling, coverage reporting, and — the decisive one — reusing Vite's module resolution and plugin pipeline so tests see the same modules the app does. Reimplementing that last item alone means embedding Vite, at which point we have written Vitest.

The stronger challenge is not "build it" but "use `bun test`, which is already installed". That is answered above: it is the config sharing we are buying, and it is worth the 20 dependencies only for as long as the Vite config carries real complexity. If the client ever becomes a plain SPA with trivial resolution, `bun test` becomes the better answer and this should be revisited.

## 5. Risk

### Undo risk — low

Confined to the `test` block in `vite.config.ts`, two `package.json` scripts, and the `*.spec.ts` files themselves. No application code imports it. Because the API is Jest-compatible, migrating to `bun test` or `node --test` would leave most test bodies intact — the coupling is in configuration, not in the tests.

### Security risk — low

Development-only; never runs in production and ships nothing to the browser. MIT, actively maintained by a funded team, no known outstanding CVEs, no install or postinstall scripts.

Two notes. Its 20 direct dependencies make it the widest fan-out in the frontend set, though roughly half are first-party `@vitest/*` packages under the same maintainers, so the distinct-maintainer surface is smaller than the count suggests. And it depends on [Vite](./vite.md), inheriting the native-binary considerations recorded there. Exact resolutions in `bun.lock` are the mitigation, as elsewhere.
