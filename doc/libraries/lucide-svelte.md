# @lucide/svelte

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 1.28.0

## 1. Problem

Interface icons — a close cross on a dialog, chevrons on menus and selects, check marks on toggles. [shadcn-svelte](./shadcn-svelte.md)'s generated components reference them directly (`dialog-content.svelte` imports an `XIcon`), so an icon source has to exist for those components to compile. Beyond that, the game itself will want icons of a kind no general-purpose set provides.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@lucide/svelte** (chosen) | 1.28.0, 6.4 MB unpacked, **0 dependencies**. Deep imports (`@lucide/svelte/icons/x`) are tree-shaken, so only icons actually imported ship — roughly 0.5 KB each | ~1,600 icons as Svelte components; consistent 24px grid and stroke weight; props for size, colour, stroke width | Very active; the icon set shadcn/ui standardised on | **ISC** | High: the preset's configured library, so generated components import from it by name |
| Build in-house (copy individual SVGs into the repo) | 0 bytes | Paste the handful of SVGs actually needed into `src/lib/assets/` or inline them | Us | ISC (icons themselves) | **Medium–high: genuinely viable and the strongest challenger** — see section 4 |
| Iconify | varies | Tens of thousands of icons across many sets, on-demand | Active | Mixed per set | Low: far more machinery than a fixed handful of icons justifies |
| Heroicons / Phosphor / Tabler | varies | Comparable quality alternative sets | Active | MIT | Low: equivalent, but not what the chosen `vega` preset configures |

Why the others lost: Iconify solves a breadth problem this project does not have. The rival icon sets are interchangeable in quality and lose only because the preset selects Lucide — had `lyra` or `mira` been chosen, Phosphor or Hugeicons would sit here instead. Copying SVGs by hand is the serious alternative and very nearly wins.

## 3. Decision & rationale

Adopt **@lucide/svelte 1.28.0**, entailed by the [shadcn-svelte](./shadcn-svelte.md) preset choice rather than selected on its own.

**This is the weakest of the entailed decisions and should be recorded as such.** Icons are static SVG markup. The set is ISC-licensed, so the individual files can simply be copied into the repository with no dependency at all. Three icons are currently imported — `gamepad-2`, `infinity` and `users`, used by the app chrome — at roughly half a kilobyte each after tree-shaking. Weighed on that usage alone, a 6.4 MB package for three glyphs is difficult to defend.

Two things keep it. The first is the familiar regeneration argument: `shadcn-svelte add` writes `import XIcon from '@lucide/svelte/icons/x'` into every component that needs an icon, and each new component brings its own. Removing the package means rewriting those imports after every add and every update — a recurring chore that grows with the component count. The second is that the tree-shaken cost is genuinely small: deep imports mean the 6.4 MB figure never reaches the browser, and each icon actually used costs on the order of half a kilobyte.

**The condition attached is the same one that governs [shadcn-svelte](./shadcn-svelte.md): this is for application chrome.** Game iconography — resources, buildings, upgrades — is bespoke artwork that no general-purpose set supplies, and will be authored as project assets. This package should not become the default answer to "we need a picture".

Note the licence: **ISC**, not MIT. Functionally equivalent — a permissive licence requiring attribution — but it is the first non-MIT code-bearing package in the frontend set.

### Pros

- Tree-shaken per icon via deep imports; only what is imported ships, ~0.5 KB each.
- Zero dependencies.
- Generated components import from it by name, so nothing needs patching after `add` or `update`.
- Visually consistent set — uniform grid, stroke weight and optical sizing — which matters for a project with no designer.
- Icons take props (size, colour, stroke width) and inherit `currentColor`, so they theme with the token layer automatically.
- ISC, actively maintained, very widely deployed.

### Cons

- **6.4 MB unpacked for three icons in current use** (the app chrome). By far the worst ratio of installed size to actual use in the project.
- The most replaceable package in the set: the icons are ISC-licensed SVGs that could simply be copied.
- ISC rather than MIT — harmless, but the first licence variation in the frontend set.
- Only as useful as the preset that chose it; switching presets would switch icon libraries.
- Contributes nothing to the game UI proper.

## 4. Build-vs-buy

"Building" here means copying: open the icon on the Lucide site, paste the SVG into a `.svelte` file or inline it, done. Five minutes per icon, zero dependencies, ISC permits it, and the result is fully ours. For the three icons in use today this is unambiguously the cheaper answer, and this project's rule of thumb — build what fits in hours — points at it plainly.

**Buying wins on trajectory rather than on present state, and the margin is thin.** The icon count will not stay at one: menus want chevrons, toggles want checks, forms want warnings, dismissible things want crosses, and every component `shadcn-svelte add` generates arrives with its imports already written. Hand-copying converts each of those from a no-op into a small manual task, indefinitely — and each copied file is one more asset to keep visually consistent with the rest by eye.

Against a per-icon cost of about half a kilobyte and zero dependencies of its own, that ongoing friction is not worth trading away. But the calculus is genuinely close, and if this project ever leaves shadcn-svelte while keeping the components, **replacing this package with a dozen copied SVGs would be a sound cleanup** rather than a regression.

## 5. Risk

### Undo risk — low

Three import sites today (all in the app chrome), each a one-line change to a component we own. Removal means copying the SVGs in use and rewriting those imports; the ongoing cost is re-doing it after each `shadcn-svelte add`. Nothing structural depends on it.

### Security risk — low

ISC, zero dependencies, no install or postinstall scripts, no native binaries, no known CVEs. Very widely deployed, so a compromised release would be noticed quickly.

The realistic concern is size rather than behaviour: a 6.4 MB package of generated components is not meaningfully reviewable by hand, so we rely on the lockfile's exact resolutions and integrity hashes — the same mitigation the project applies to its Rust build binaries. What limits the exposure is that the content is inert: icons are SVG path data rendered as markup, with no I/O, no dynamic evaluation, and no access to anything beyond the elements they render. Tree-shaking also means only the handful of icons actually imported reaches the browser, so the shipped surface is far smaller than the installed one.
