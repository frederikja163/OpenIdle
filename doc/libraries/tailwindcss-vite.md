# @tailwindcss/vite

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 4.3.3 (declared `^4.3.0`)

## 1. Problem

[Tailwind CSS](./tailwindcss.md) v4 needs to run during the build: scan source files for utility classes, generate only the CSS for classes actually used, and inject the result into the bundle. In v4 that integration is a first-party [Vite](./vite.md) plugin rather than the PostCSS pipeline v3 used. Without it, `@import 'tailwindcss'` is an unresolved import and no utility CSS is generated at all.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@tailwindcss/vite** (chosen) | 4.3.3, 3 direct deps (`@tailwindcss/oxide`, `@tailwindcss/node`, `tailwindcss`) | The first-party v4 Vite integration; hooks Vite's transform pipeline directly, avoiding a PostCSS pass; fastest supported path | Very active: 42M weekly downloads, latest 2026-07-16, Tailwind Labs | MIT | High: the first-party, best-performing integration path for Tailwind v4 under Vite |
| `@tailwindcss/postcss` | 4.3.3 | Same engine via PostCSS instead of a Vite plugin | Same team | MIT | Low: slower, and an extra layer for no benefit given we already use Vite |
| Tailwind CLI | 4.3.3 | Generates CSS as a separate build step | Same team | MIT | Low: a second build process outside Vite; no HMR integration |
| Drop [Tailwind](./tailwindcss.md) | 0 bytes | Removes this package, `tailwindcss`, and `prettier-plugin-tailwindcss` — three entries and a native binary | n/a | n/a | Low: Tailwind was adopted on an explicit argument; that decision is made elsewhere and is not reopened here |
| Build in-house | n/a | Our own class scanner and generator | Us | n/a | Low: pointless — this is Tailwind's own integration for Tailwind's own engine |

Why the others lost: given Tailwind and Vite, this plugin is the supported path and the alternatives are strictly worse. The only decision that matters is made elsewhere.

## 3. Decision & rationale

Adopt, as a direct consequence of [Tailwind CSS](./tailwindcss.md) being adopted. This package has no independent justification — it exists solely to make Tailwind work, and there was never anything to decide here separately.

Taken on its own terms it is the correct choice. Tailwind v4 dropped the PostCSS-centric architecture of v3 in favour of a first-party Vite plugin, which is both faster and better integrated — it hooks Vite's transform pipeline directly, so class scanning participates in HMR rather than running as a separate pass. It is registered first in the `plugins` array in `vite.config.ts`, ahead of `sveltekit()`, which is the documented ordering.

The one thing worth recording independently is what it drags in. `tailwindcss` itself has zero npm dependencies, which makes Tailwind look lighter than it is; this plugin is where the actual weight lands. It pulls `@tailwindcss/oxide` — the Rust engine, distributed as prebuilt platform-specific binaries — plus `@tailwindcss/node`. So the accurate statement is not "Tailwind has no dependencies" but "Tailwind's dependencies live in its Vite plugin, and one of them is a native binary". That belongs in the Tailwind cost accounting, and is noted there.

Its configuration is otherwise correct and needs no attention of its own; everything that matters about it is decided one level up.

### Pros

- First-party to Tailwind Labs and version-locked to the `tailwindcss` core it integrates.
- Faster than the PostCSS path — it hooks Vite's transform pipeline directly, no separate pass.
- Integrates with HMR, so class changes reflect without a reload.
- Only 3 direct dependencies, all first-party or the Tailwind core itself.
- MIT, 42M weekly downloads, actively released.

### Cons

- No independent justification — purely a consequence of the Tailwind decision, and removed with it if that is ever reversed.
- Pulls the `@tailwindcss/oxide` Rust native binary, which is where Tailwind's real dependency weight actually sits.
- Version must move in lockstep with `tailwindcss`, so both are upgraded together or neither.
- Coupled to Vite's plugin API, so a Vite major can require a plugin update.

## 4. Build-vs-buy

Not applicable in any meaningful sense. This is the official integration between two tools we would have already chosen; writing our own bridge to Tailwind's engine would be strictly worse in every dimension and would still require Tailwind itself.

The genuine build-versus-buy question sits one level up, in [Tailwind CSS](./tailwindcss.md), where the alternative is Svelte's built-in scoped CSS plus a hand-written custom-property token set — roughly two hours of work, removing this package and two others. That analysis is recorded there and should not be re-litigated here.

## 5. Risk

### Undo risk — low

One import and one entry in the `plugins` array in `vite.config.ts`. Nothing else references it. It is removed together with Tailwind in a single small edit — note that the *plugin's* undo risk is low even though [Tailwind](./tailwindcss.md)'s is `high`, because the difficulty there is rewriting utility classes spread through markup, not unwiring the build.

### Security risk — low

MIT, first-party, actively maintained, no known CVEs, no install or postinstall scripts. Build-time only — it emits a CSS file and ships no JavaScript to the browser, so there is no runtime attack surface.

The item worth naming is `@tailwindcss/oxide`: a prebuilt, platform-specific Rust binary that executes on every build and cannot be meaningfully source-reviewed. Compromised native optional-dependency packages are a recognised npm attack pattern, and this is one of three such binaries in the build alongside `rolldown` and `lightningcss` (see [Vite](./vite.md)). The mitigation is the same throughout this project: `bun.lock` pins exact resolutions with integrity hashes, so a substituted binary cannot arrive without a deliberate, reviewable lockfile change. Dropping Tailwind would remove one of the three.
