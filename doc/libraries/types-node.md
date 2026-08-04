# @types/node

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 24.13.3 (declared `^24`) — intentionally tracks Node 24 LTS, not npm `latest`

## 1. Problem

Although this is a browser client, several files in the project run in Node rather than the browser: `vite.config.ts`, `eslint.config.js` (which imports `node:path` and uses `import.meta.dirname`), `prettier.config.js`, and `playwright.config.ts`. [TypeScript](./typescript.md) ships type definitions for the browser and for the language itself, but not for Node's built-in modules. Without them, every `node:*` import in our configuration files is an unresolved module and every Node global is an error — which would mean either untyped config files or a lint configuration that cannot be checked.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@types/node 24** (chosen) | 24.13.3, 2.6 MB, 1 direct dep (`undici-types`) | Community-maintained Node type definitions via DefinitelyTyped; major version tracks the Node major it describes | Extremely active: 409M weekly downloads — the most-downloaded package in this set. Latest 26.1.2 published 2026-07-27 | MIT | High: accurate types for exactly the Node surface our config files touch |
| `@types/node` at `latest` (26.x) | 26.1.2 | Describes Node 26 | Same | MIT | Low: would type against a Node major we do not run. Wrong on purpose |
| No Node types | 0 bytes | One fewer dependency | n/a | n/a | Low: config files become untyped or error; `bun run check` fails |
| Build in-house (hand-write the few types we use) | ~20 lines | We use a tiny slice: `node:path`, `import.meta.dirname`, `process.env` | Us | n/a | Medium: genuinely small. See build-vs-buy |
| `@types/bun` instead | current | This project installs with Bun; Bun ships its own types which include Node compatibility | Active (Oven) | MIT | Medium: arguably the more accurate description of our actual runtime, but the configs are consumed by Vite/ESLint under Node semantics, and the Svelte ecosystem assumes `@types/node` |

Why the others lost: pinning to `latest` would be actively wrong — the whole point of the major version is to match a Node release. Dropping types breaks the check script. `@types/bun` is a defensible alternative worth revisiting if the toolchain becomes Bun-only, but today Vite, ESLint, and Playwright all describe themselves in terms of Node.

## 3. Decision & rationale

Adopt, at **major 24 specifically**. The `^24` range looks stale next to npm's `latest` of 26.1.2, and it is worth recording why it is not. `@types/node`'s major version tracks the Node.js major it describes: `@types/node@24` describes Node 24, which is the current LTS line. Typing against Node 26 while running Node 24 LTS would let us use APIs that do not exist at runtime — the failure mode is a green type check and a crash. The version should follow whichever Node LTS we deploy and develop on, and should be bumped when we move Node majors, not when npm publishes a new `latest`.

The scope of use is small and worth being honest about: this is a browser application, and Node types exist here purely to make four configuration files type-check. That is a legitimate need, but it means the dependency's value is measured in tens of lines of config, not in application code.

### Pros

- Makes `vite.config.ts`, `eslint.config.js`, and `playwright.config.ts` type-check rather than error.
- Major version tracks Node LTS, giving an unambiguous correct answer to "which version should we be on".
- Development-only, types-only: erased entirely at build, nothing reaches the browser.
- 409M weekly downloads through DefinitelyTyped's review process; errors get caught fast.
- Only one direct dependency (`undici-types`, for the `fetch`/`undici` surface).

### Cons

- 2.6 MB of type definitions to support what amounts to a handful of imports in config files.
- Community-maintained rather than published by the Node project, so definitions can lag or drift slightly from actual runtime behaviour.
- The `^24` pin needs manual attention at Node LTS upgrades — nothing automatic will flag that it has fallen behind the runtime, and a stale pin fails in the safe direction while a too-new one fails in the dangerous direction.
- Slightly incoherent with the project installing via Bun: our real runtime is Bun, and these types describe Node. In practice the overlap is total for the APIs we touch, but the mismatch is real.

## 4. Build-vs-buy

A closer call than it appears. Our actual Node surface is tiny — `node:path` (one import, one `resolve` call), `import.meta.dirname`, and the odd `process.env` read. Declaring those by hand in `app.d.ts` would be perhaps 20 lines and half an hour, and under this project's "hours not weeks" rule that is the kind of thing we would normally build rather than install.

Buying wins for two reasons that outweigh the size argument. First, hand-written ambient declarations are unchecked assertions — if we declare `path.resolve` slightly wrong, TypeScript believes us and the error surfaces at runtime, which is worse than no types. Second, third-party packages already reference `@types/node` in their own declarations, so it tends to arrive transitively regardless; declaring it explicitly at a version we control is better than inheriting whatever resolution picks. The dependency is types-only and disappears at build time, so the 2.6 MB is disk, not weight.

## 5. Risk

### Undo risk — low

Nothing imports it directly; it is ambient. Removing it produces type errors in four configuration files and nothing else. No application code, no runtime behaviour, no build output depends on it.

### Security risk — low

Types-only — the package contains no executable code, no native binaries, and no install scripts, so there is nothing for it to do at runtime or install time beyond being read by the compiler. MIT. DefinitelyTyped has a review process and enormous usage. The residual concern is generic to the npm ecosystem rather than specific here: a compromised publish of a package installed 409M times a week would be severe, but with no executable code the attack would have to target the compiler's consumption of declaration files, which is a far narrower path than a postinstall script. Lockfile pinning in `bun.lock` covers the realistic case.
