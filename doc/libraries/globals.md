# globals

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 17.9.0 (declared `^17.6.0`)

## 1. Problem

ESLint's `no-undef` rule needs to know which identifiers exist without being declared. `window`, `document`, `fetch`, and `localStorage` are legitimate in browser code; `process`, `Buffer`, and `__dirname` are legitimate in our Node-run config files. Without a list, ESLint flags every one of them as undefined. The lists are long — several hundred names each — and they change as browsers and Node add APIs.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **globals** (chosen) | 17.9.0, 0 direct deps | A maintained JSON map of environment name → global identifiers, covering `browser`, `node`, `es2025`, worker, and others. Maintained by Sindre Sorhus | Very active: 262M weekly downloads, latest 2026-08-02 (yesterday) | MIT | High: zero dependencies; used in `eslint.config.js` for `browser` and `node` |
| Build in-house (hand-written globals list) | ~20 lines if minimal | No dependency; only the globals we actually use | Us | n/a | Medium: viable but decays. See build-vs-buy |
| Disable `no-undef` entirely | 0 bytes | No list needed at all | n/a | n/a | **Medium-high**: our config already disables it. See below |
| `env: { browser: true }` (eslintrc style) | 0 bytes | ESLint used to ship these lists internally | n/a | MIT | Low: removed in flat config; ESLint now defers to this package |

Why the others lost: the old `env` mechanism no longer exists in flat config. A hand-written list works but goes stale. The "just disable `no-undef`" option is the interesting one and is addressed below.

## 3. Decision & rationale

Adopt — with an inconsistency in the current configuration flagged for resolution.

`eslint.config.js` does two things that sit awkwardly together. It sets `languageOptions.globals` to the union of `globals.browser` and `globals.node`, and it also sets `"no-undef": "off"`, following typescript-eslint's guidance that `no-undef` should not be used on TypeScript projects (TypeScript already reports undefined identifiers, and does it more accurately). **With `no-undef` disabled, the primary consumer of these lists is switched off.**

The package is not entirely redundant — `languageOptions.globals` also informs `no-redeclare`, `no-shadow`, and scope analysis used by other rules, so the lists still do something. But the headline justification does not apply, and this is worth knowing rather than assuming the package is load-bearing.

It stays adopted because it costs nothing meaningful: zero dependencies, tiny, released as recently as yesterday, and it is what ESLint's own flat-config documentation directs users to. Removing it to save a near-zero-cost entry would be false economy. But if the lint configuration is ever simplified, this is a legitimate candidate to drop, and the honest summary is that it is currently the least load-bearing package in the frontend set.

A secondary point: merging `globals.browser` and `globals.node` across the whole project is imprecise. It tells ESLint that `document` is valid inside `vite.config.ts` and that `process` is valid inside a Svelte component. Scoping browser globals to `src/` and Node globals to the config files would be more accurate, and is a small config improvement worth making if the lint setup is revisited.

### Pros

- Zero direct dependencies.
- Actively maintained — latest release 2026-08-02, tracking new browser and Node APIs as they ship.
- The canonical answer in ESLint flat config; every example and tutorial uses it.
- 262M weekly downloads, MIT.
- Data only: no executable logic beyond exporting an object.

### Cons

- Its main purpose (`no-undef`) is explicitly disabled in our configuration, so most of its value is unrealised.
- Applied as a project-wide union of `browser` and `node`, which is less accurate than scoping each to the files that need it.
- One more `package.json` entry for what is fundamentally a JSON data file.
- Frequent releases mean frequent lockfile churn for changes that will never affect us.

## 4. Build-vs-buy

Closer than the package's size suggests. The globals our code actually relies on are few — `window`, `document`, `fetch`, `WebSocket`, `localStorage`, `console`, `process` — and declaring them inline would be a handful of lines. Given `no-undef` is off, we could plausibly delete the entry entirely and lose very little.

Buying wins for the same reason as [@eslint/js](./eslint-js.md): a hand-written list is a snapshot that never learns about new platform APIs, and when it goes stale the failure is a confusing false positive rather than an obvious error. At zero dependencies and near-zero size, there is nothing to gain by owning it. The real decision is not build-versus-buy but whether the entry is needed at all — and until the lint configuration is revisited, keeping the ecosystem-standard approach is the lower-effort correct answer.

## 5. Risk

### Undo risk — low

One import and one `languageOptions.globals` line in `eslint.config.js`. Given `no-undef` is disabled, removing it would likely produce no new errors at all. The cheapest removal in the project.

### Security risk — low

Data-only: the package exports a JSON object of identifier names. No executable logic, no dependencies, no native binaries, no install or postinstall scripts. Development-only, never reaches the browser. MIT, no known CVEs, maintained by a long-established npm author.

The residual concern is generic and worth one line: at 262M weekly downloads it is a high-value supply-chain target, and a future release could in principle introduce executable code where there is none today. Exact pinning in `bun.lock` covers this, and a package that has no reason to ship code is one where the appearance of an install script should be treated as an immediate red flag — the same standing rule recorded in [eslint-config-prettier](./eslint-config-prettier.md).
