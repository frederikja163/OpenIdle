# @fontsource/chakra-petch

- Status: under-review
- Date: 2026-08-04
- Decided by: project owner
- Version / commit pinned: 5.3.0

## 1. Problem

The [OpenIdle Design System](../../Frontend/src/lib/styles/openidle/index.css) assigns three typefaces three distinct jobs, and Chakra Petch has the display job: HUD titles, skill names, action names, panel headers, buttons, tabs and the wordmark. Its argument is that a squared, slightly technical face reads as *instrument panel* rather than *fantasy scroll*, which is the whole visual thesis of the product — and that the wordmark, having no logo mark to sit beside, has to carry the brand as type alone. A webfont has to be delivered somehow: self-hosted from our own origin, fetched from a third-party CDN, or abandoned for whatever the system provides.

**This typeface is a substitution, and the status above reflects that.** The design system's `tokens/fonts.css` carries the header *"SUBSTITUTED FONTS — no font binaries were provided with the source material"*, and its readme has a standing *"Font substitution — action needed"* item. Chakra Petch is the design system's nearest match to an intended feel, not a typeface OpenIdle chose or owns. It is adopted operationally — the interface needs *a* display face and this is a good one — but the decision is provisional and should be revisited if real brand fonts ever exist.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@fontsource/chakra-petch** (chosen) | 5.3.0, 1.1 MB unpacked, 40 woff2 files, **0 dependencies**, no install scripts | Self-hosted; one `@import` per weight pulls correct `@font-face` blocks with `unicode-range` subsetting already split. Squared terminals and flat-sided bowls at a normal text weight — technical without being a display-only novelty | Active; Fontsource packages the whole Google Fonts catalogue | **OFL-1.1** (font, by Cadson Demak); packaging MIT | High: delivers the design system's specified face on our own origin at essentially no engineering cost |
| Google Fonts CDN | 0 bytes installed | The `@import` the design system ships verbatim | Google | OFL-1.1 | **Low: sends every visitor's IP and User-Agent to a third party on every page load.** A German regional court (LG München I, 2022) held that this violates the GDPR absent consent; this project is developed in Denmark and would serve EU players. The historical counter-argument — a shared cross-site cache — no longer holds, because browsers partition their HTTP caches per top-level site, so the font is downloaded from someone either way |
| Build in-house (download the woff2, hand-write `@font-face`) | 0 bytes installed | Full control; exactly the files we choose | Us | OFL-1.1 | Medium: genuinely simple and the closest challenger — see section 4 |
| No display face — reuse the body font | 0 bytes | One fewer family to download. The HUD would set titles in IBM Plex Sans at a heavier weight | n/a | n/a | Medium: costs nothing and works, but collapses the design system's three-faces-three-jobs rule into two, and the wordmark loses the only thing distinguishing it from body text |
| A different squared/technical face (Rajdhani, Saira, Oxanium) | comparable | Interchangeable in quality and licence terms | Active | OFL-1.1 | Medium: no worse, but there is no argument for overriding the design system's pick with an equivalent one |

Why the others lost: the CDN is rejected on privacy grounds, not performance. Dropping the display face entirely is the serious minimal position and loses only because the wordmark has no mark to lean on — type *is* the brand here, so a distinct display face is doing real work rather than decorating. Rival squared faces are equivalent; churning the choice for no gain is worse than deferring to the design system. Hand-rolling `@font-face` is weighed in section 4.

## 3. Decision & rationale

Adopt **@fontsource/chakra-petch 5.3.0** *provisionally*, self-hosted, weights 500/600/700 only. The status above is `under-review` and stays there: the delivery decision below is settled, the choice of face is not.

**Self-hosting is the substantive half of this decision and it is a privacy decision before a performance one.** Loading fonts from `fonts.googleapis.com` transmits every visitor's IP address and `User-Agent` to Google on every page load, which the LG München I ruling treats as a GDPR violation without consent. Self-hosting removes the third-party origin entirely: no consent-banner implications, no data leaving our infrastructure, no dependence on another operator's uptime or TLS. Because cache partitioning killed the cross-site cache benefit, we give up nothing by doing so.

Given self-hosting is settled, this package is simply the least-effort way to do it. Note this is where our setup *diverges deliberately from the design system as published*: [layout.css](../../Frontend/src/routes/layout.css) imports the Fontsource weights instead of mirroring the upstream `tokens/fonts.css`, which is a Google Fonts `@import`. That divergence is recorded in [index.css](../../Frontend/src/lib/styles/openidle/index.css).

Chakra Petch has **no variable build on Fontsource** — only static weights — so the import is per-weight rather than one file covering the axis. Only 500, 600 and 700 are imported, because those are the only weights the `--type-display-*` and `--type-label-*` tokens reference. This costs less than it appears: `@font-face` declarations do not trigger a download unless a rule actually matches them, so an unused weight costs CSS bytes, not bandwidth.

### Pros

- **No third-party origin at runtime** — no visitor data leaves our infrastructure, sidestepping the GDPR exposure of the Google Fonts CDN.
- One `@import` per weight replaces hand-written `@font-face` blocks, `unicode-range` subsetting and file management.
- Subsets are split, so Thai, Vietnamese and extended-Latin ranges are never fetched by Latin-only players.
- Zero dependencies and **no install or postinstall scripts** — consistent with the standing note in [README](./README.md) that any script appearing in this tree is a red flag.
- No JavaScript reaches the browser; the package contributes CSS and binary font files only.
- Updates arrive through the lockfile like any other package.

### Cons

- **The typeface is a substitution, not a brand decision** — see section 1. Everything below is downstream of a choice made for us.
- 1.1 MB unpacked for what is ultimately a few woff2 files and some CSS.
- **No variable build**, unlike IBM Plex Sans: three weights means three `@import`s and three potential downloads rather than one file covering the axis.
- Adds a webfont to first paint. The design system sets the wordmark in this face, so a fallback flash is visible in the chrome specifically.
- A `package.json` entry for an asset, which is arguably not what a package manager is for.
- Thai and Vietnamese subsets ship in the package for a product with no plans for either.

## 4. Build-vs-buy

Doing this by hand is easy and the estimate is honest: download three woff2 files, drop them in `static/`, write three `@font-face` rules with `font-display: swap` and appropriate `unicode-range` values. Under an hour, zero dependencies, and a leaner result than 1.1 MB in `node_modules`. By this project's rule of thumb — build what fits in hours — that is the indicated answer.

**Buying wins on the details that are easy to get slightly wrong and never notice.** Correct `unicode-range` subsetting is the main one: get it wrong and either every visitor downloads Thai glyphs they will never render, or a player's name in an unexpected script silently falls back to a system font. Fontsource has already split those ranges. Font updates and the `font-display` defaults come along for the ride, and the whole thing stays under lockfile control rather than becoming a binary checked into `static/` with no version anyone can name.

The margin is small, and the manual route remains the fallback if this dependency is ever unwanted — the licence permits it and nothing else depends on the package.

## 5. Risk

### Undo risk — low

Three `@import` lines in [layout.css](../../Frontend/src/routes/layout.css), one `--font-display` declaration in its `@theme` block, and one `package.json` entry. Removing it degrades to `Segoe UI` and then the system sans; the interface stays entirely functional and only the typeface changes. Replacing it with hand-written `@font-face` rules is the under-an-hour job above.

Worth noting the undo risk is low **because** the font is referenced through a token. Nothing in the codebase names Chakra Petch except the `--font-display` declaration; components say `.oi-display-md` or `font-display`. Swapping the face when real brand fonts arrive is a one-line change.

### Security risk — low

OFL-1.1 font data with MIT packaging, zero dependencies, **no install or postinstall scripts**, no native binaries, no known CVEs. No JavaScript reaches the browser, so there is no runtime code-execution surface of the kind that applies to [bits-ui](./bits-ui.md).

Two residual notes. Font files are parsed by the browser's font engine, historically a source of memory-safety bugs, so a malicious woff2 is a theoretical vector — mitigated by lockfile integrity hashes and by Fontsource being among the most widely deployed font distributions in existence. And self-hosting *removes* a security dependency rather than adding one: no third-party origin to be compromised, no external TLS to trust, no availability risk from someone else's CDN.
