# Vite

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 8.2.0 (declared `^8.0.16`)

## 1. Problem

Browsers cannot run `.svelte` files, TypeScript, or Tailwind's CSS syntax. Something must transform source into browser-executable assets, serve them with fast reload during development, and produce a minified, code-split, cache-busted bundle for production. This is the build tool slot — non-optional for any modern frontend.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Vite 8** (chosen) | 8.2.0, 2.4 MB, 5 direct deps (`rolldown`, `lightningcss`, `postcss`, `picomatch`, `tinyglobby`) | Native-ESM dev server (near-instant startup, no dev bundling); as of v8 a single Rust bundler (Rolldown) replaces both esbuild and Rollup, giving 10–30× faster builds; mature plugin ecosystem | Extremely active: 161M weekly downloads, latest 2026-07-30, VoidZero-backed | MIT | High: required by [SvelteKit](./sveltekit.md); also shares config with [Vitest](./vitest.md) |
| Rollup | 4.x | The bundler Vite used through v7; excellent tree-shaking; huge plugin ecosystem | Active | MIT | Low: no dev server, so we would also need one. Now effectively superseded by Rolldown inside Vite |
| esbuild | 0.2x | Extremely fast Go-based bundler | Active | MIT | Low: deliberately limited feature set — no first-class CSS handling or HMR story |
| Webpack | 5.x | Most configurable and most battle-tested | Active but declining | MIT | Low: slow dev feedback, heavy configuration, large dependency tree. Wrong trade for a solo project |
| Parcel | 2.x | Zero-config | Active | MIT | Low: less control, much smaller Svelte ecosystem |
| Build in-house | n/a | Exactly our needs | Us | n/a | Low: this is a compiler toolchain. See build-vs-buy |

Why the others lost: none is a real contender, because [SvelteKit](./sveltekit.md) is built on Vite and does not support alternatives. Even setting that aside, Vite wins on merit for a solo project — the dev server is the tightest feedback loop available, and v8's Rolldown migration closed the build-speed gap that used to favour esbuild.

## 3. Decision & rationale

Adopt **Vite 8**. Strictly speaking this is a consequence of choosing SvelteKit rather than an independent decision — SvelteKit is a Vite application and offers no alternative bundler. It is documented separately anyway because Vite is doing three distinct jobs here and each is worth being explicit about: it builds the app, it hosts the dev server, and it provides the configuration that [Vitest](./vitest.md) reuses (our `vite.config.ts` is imported by the Vitest project config, so the test runner resolves modules exactly as the app does — a genuine correctness benefit, not just convenience).

Vite 8 is a significant version to be on. Rolldown became the default bundler in Vite 8 stable (March 2026) and reached 1.0 in May 2026, replacing both esbuild for dev transforms and Rollup for production builds with a single Rust implementation. That is a large architectural change absorbed in one major. Two practical consequences: Rollup-specific plugins and esbuild transform hooks no longer work, and the package is ESM-only requiring Node 20.19+ or 22.12+. Neither affects us today — we use no custom plugins beyond the first-party Svelte and Tailwind ones, and the project is ESM throughout (`"type": "module"`) — but both constrain what we can add later.

### Pros

- Only 5 direct dependencies for a complete build toolchain — remarkably lean for what it does.
- Dev server starts near-instantly and stays fast as the app grows, because it does not bundle in dev.
- Rolldown 1.0 has a locked `^1.0.0` public API under semver, so the big architectural churn is behind us.
- Shared config with Vitest means tests and app resolve modules identically.
- 161M weekly downloads makes it among the most-exercised packages in the ecosystem; bugs surface fast.
- MIT, backed by VoidZero with full-time maintainers.

### Cons

- Ships Rust native binaries (`rolldown`, `lightningcss`) — platform-specific artefacts that must be verified per architecture and are opaque to source review. See security risk.
- ESM-only and Node 20.19+/22.12+ minimum, so it constrains the runtime we develop on.
- The Rollup→Rolldown switch invalidated the Rollup plugin ecosystem; if we ever need a niche plugin, "there's a Rollup plugin for it" may no longer help.
- Only 2 npm maintainers on a package with 161M weekly downloads — an unusually concentrated compromise surface for something this widely installed.
- Fast major cadence (v8 in 2026 after v7 the same era) means an upgrade every year or so.

## 4. Build-vs-buy

Not a real build candidate. A production-grade build tool means an ES module graph resolver, a minifier, a tree-shaker, CSS handling with scoping and vendor prefixing, sourcemap generation and chaining, content-hashed output for cache busting, code splitting, and a dev server with HMR. That is person-years, and Rolldown alone represents a multi-year Rust effort by a funded team. There is no version of this that fits "hours not weeks". Buying is the only sane answer, and in any case SvelteKit mandates Vite specifically.

## 5. Risk

### Undo risk — low

Confined to `vite.config.ts` and the `dev`/`build`/`preview` scripts. No application source imports Vite. It is, however, effectively unremovable while [SvelteKit](./sveltekit.md) is in use — the low rating reflects mechanical coupling to our own code, not freedom to switch. Replacing Vite in practice means replacing SvelteKit, whose undo risk is rated `high`.

### Security risk — low

MIT, no known outstanding CVEs, extremely widely deployed, funded full-time maintenance, and no install or postinstall scripts anywhere in its tree. Two things keep this from being trivially low. First, the native Rust binaries (`rolldown`, `lightningcss`) are distributed as prebuilt platform-specific packages — we cannot meaningfully review them, and a compromised optional-dependency binary is a known npm attack pattern. Second, two npm maintainers on a package installed 161M times a week is a high-value, narrow target; the `eslint-config-prettier` incident documented in [eslint-config-prettier](./eslint-config-prettier.md) shows exactly how a single phished maintainer account plays out. The mitigation for both is the same and is already partly in place: `bun.lock` pins exact resolutions and integrity hashes, so risk only materialises when the lockfile is deliberately updated. Treat lockfile changes as reviewable, and do not run `bun update` casually.
