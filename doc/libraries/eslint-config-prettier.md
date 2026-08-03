# eslint-config-prettier

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 10.1.8 (declared `^10.1.8`) — **must not be downgraded**, see security risk

## 1. Problem

[ESLint](./eslint.md) and [Prettier](./prettier.md) both have opinions about formatting. Several ESLint rules (`indent`, `quotes`, `semi`, `comma-dangle`, and their typescript-eslint and Svelte equivalents) enforce exactly what Prettier rewrites. Left alone, the two tools fight: Prettier formats a file, ESLint reports the result as an error, and `bun run lint` fails on code Prettier just produced. Something has to turn off every ESLint rule that Prettier owns.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **eslint-config-prettier** (chosen) | 10.1.8, 0 direct deps | A curated list of every formatting-related rule across ESLint core, typescript-eslint, and plugin ecosystems, all set to `off`. Tracks new rules as upstream adds them | Stable but slow: 64M weekly downloads, latest 2025-07-18 (over a year ago) | MIT | High: solves the conflict completely with zero dependencies |
| Build in-house (hand-written "off" block in `eslint.config.js`) | ~30 lines | No dependency. Fully explicit and auditable | Us | n/a | **Medium-high**: the strongest challenger in the whole lint set. See build-vs-buy |
| `eslint-plugin-prettier` | 5.x | Runs Prettier *as* an ESLint rule, so formatting errors appear as lint errors | Active | MIT | Low: slower, noisier, and explicitly discouraged by Prettier's own documentation. Also compromised in the same 2025 incident |
| Drop [Prettier](./prettier.md), format via ESLint | n/a | One fewer tool; no conflict to resolve | n/a | n/a | Low: ESLint deprecated its formatting rules; this is moving against the ecosystem |
| Do nothing | 0 bytes | Nothing to install | n/a | n/a | Low: lint and format actively contradict each other; `bun run lint` fails on formatted code |

Why the others lost: `eslint-plugin-prettier` is discouraged upstream and is slower. Formatting via ESLint runs against the direction ESLint itself has taken. Doing nothing leaves a broken `lint` script. The hand-written alternative is treated seriously below.

## 3. Decision & rationale

Adopt — but this is the closest call in the frontend dependency set, and the reasoning cuts both ways.

The package is, quite literally, an object with about 30 keys mapping rule names to `"off"`. There is no logic in it. Under this project's build-before-buy principle, "a config file we could paste into `eslint.config.js` in ten minutes" is normally a clear build case, and that argument is not wrong here.

What tips it to adopt is maintenance rather than effort. The list is not static: it must cover formatting rules across ESLint core, `typescript-eslint`, and `eslint-plugin-svelte` simultaneously, and each of those adds and renames rules across versions. A hand-written block is correct on the day it is written and silently rots afterwards — a newly-added stylistic rule in a typescript-eslint minor starts failing CI on Prettier-formatted code, and the cause is not obvious. Delegating that tracking costs zero dependencies, which is as cheap as buying gets.

**The security history is the more important part of this document, and it changes how the package must be handled.** In July 2025 the maintainer was phished and an attacker used the stolen npm token to publish malicious versions of this package — 8.10.1, 9.1.1, 10.1.6, and 10.1.7 — tracked as CVE-2025-54313. The payload ran an `install.js` that launched `node-gyp.dll`, **a Windows-targeted remote-code-execution path**. This project is developed on Windows. Had the timing differed, a routine `bun install` on this machine would have executed it. The same attack also poisoned `eslint-plugin-prettier`, `synckit`, `@pkgr/core`, and `napi-postinstall`.

We are on 10.1.8, which is the clean version published after the incident. The practical consequences are recorded in the risk section and are binding: never downgrade below 10.1.8, and treat the version as pinned rather than floating.

### Pros

- Zero direct dependencies — nothing beneath it to audit.
- Resolves the ESLint/Prettier conflict completely and correctly, including plugin rules we would likely miss by hand.
- Tracks upstream rule additions across three separate rule sources, which is the part that actually decays if hand-maintained.
- Composes cleanly in flat config as a single entry placed after the rule sets it disables.
- MIT; still 64M weekly downloads.

### Cons

- **Has been compromised once already, with a Windows RCE payload, on a project developed on Windows.** No other dependency here has that history.
- No release since 2025-07-18. Benign for a static rule list, but it means the maintainer is not actively engaged, and the post-incident hardening posture is unknown.
- Content is trivial — roughly 30 lines of `"off"` — so we are accepting a supply-chain hop for something with essentially no implementation.
- Solves a problem created entirely by running two overlapping tools; adopting Biome (see [ESLint](./eslint.md)) would delete the problem rather than patch it.
- Its correctness is invisible: nothing tells us if the list has fallen behind a new typescript-eslint rule.

## 4. Build-vs-buy

The genuine borderline case in this project, and it deserves the honest answer rather than the convenient one.

Building it is roughly ten minutes: copy the rule names into a `rules: { ... }` block in `eslint.config.js`. Zero dependencies, fully auditable, and — given the CVE history — one fewer package with a demonstrated compromise executing on a Windows dev machine. The security argument for building is real and is the strongest such argument anywhere in this dependency set.

Buying wins on drift, narrowly. The list must stay correct against three independently-versioned rule sources, and a stale entry produces a confusing CI failure rather than an obvious one. Since the package has zero dependencies, buying adds one publish hop and nothing else.

**This decision should be revisited if either condition holds:** the package goes another year without a release while `typescript-eslint` or `eslint-plugin-svelte` add formatting rules, or a second security incident occurs. In either case, inline the list and drop the dependency — the build option is permanently ten minutes away, which is what makes accepting the dependency tolerable in the first place. Adopting Biome would moot the question entirely.

## 5. Risk

### Undo risk — low

One import and one entry in `eslint.config.js`. Replacing it with an inline `rules` block is a ten-minute change available at any moment. This is the easiest dependency in the project to remove, which is precisely why the elevated security risk below is acceptable.

### Security risk — medium

Rated `medium` on demonstrated history, not on hypothesis. This package was compromised in July 2025 (CVE-2025-54313): a phished maintainer token was used to publish versions 8.10.1, 9.1.1, 10.1.6, and 10.1.7 containing an `install.js` that executed `node-gyp.dll` — a Windows-specific RCE. The package had billions of cumulative downloads and roughly 12,000 dependents, making it a deliberately chosen high-leverage target. It is reasonable to expect it to be targeted again.

Two aggravating factors specific to this project: development happens on **Windows**, which is what the payload targeted; and the package has had **no release since 2025-07-18**, so we have no signal about the maintainer's current security posture.

Mitigating factors: we are on 10.1.8, the clean post-incident release; the package has zero dependencies; `bun.lock` pins exact versions with integrity hashes, so no unreviewed version can arrive without a deliberate lockfile change; and there are currently no install or postinstall scripts anywhere in the installed tree.

Required handling:

- **Never downgrade below 10.1.8.** Versions 8.10.1, 9.1.1, 10.1.6, and 10.1.7 are malicious and deprecated on npm.
- Treat any change to this package's entry in `bun.lock` as a reviewable security event, not routine maintenance.
- Do not run `bun update` unattended.
- If a future release adds an `install` or `postinstall` script, stop and inline the rule list instead — this package has no legitimate reason to run code at install time.
- Consider installing with install scripts disabled globally as defence in depth; nothing in the current tree needs them.
