# @fontsource-variable/ibm-plex-sans

- Status: under-review
- Date: 2026-08-04
- Decided by: project owner
- Version / commit pinned: 5.3.0

## 1. Problem

The [OpenIdle Design System](../../Frontend/src/lib/styles/openidle/index.css) gives IBM Plex Sans the prose job: item descriptions, tooltips, log lines, empty states — everything the `--type-body-*` tokens cover — and it is the document's default face via `--font-sans`. It is the highest-volume of the three typefaces by character count even though it is the least conspicuous. A webfont has to be delivered somehow: self-hosted from our own origin, fetched from a third-party CDN, or abandoned for whatever the system provides.

This package **replaced `@fontsource-variable/inter`**, which was the previous default face, inherited from shadcn-svelte's `vega` preset. Inter was uninstalled and its decision document removed on 2026-08-04 when the design system landed and `--font-body` was repointed here; nothing imported it any more.

**This typeface is a substitution, and the status above reflects that.** The design system's `tokens/fonts.css` carries the header *"SUBSTITUTED FONTS — no font binaries were provided with the source material"*, and its readme has a standing *"Font substitution — action needed"* item. IBM Plex Sans is the design system's nearest match to an intended feel, not a typeface OpenIdle chose or owns. It is adopted operationally, but the decision is provisional.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@fontsource-variable/ibm-plex-sans** (chosen) | 5.3.0, 1.5 MB unpacked, 36 woff2 files, **0 dependencies**, no install scripts | Self-hosted; **variable** — one file covers the whole 100–700 weight axis. `wght`, `wdth` and italic axes are separate entry points, so importing `wght.css` pulls only the weight axis. Sits in the same superfamily as the mono face, so prose and numbers share skeletons | Active; Fontsource packages the whole Google Fonts catalogue | **OFL-1.1** (font, by IBM); packaging MIT | High: delivers the specified face on our own origin, variable, at essentially no engineering cost |
| Keep `@fontsource-variable/inter` | 5.3.0, 1.86 MB unpacked | Already installed and already documented; variable; the shadcn `vega` preset's own face | Active | OFL-1.1 | **Low: contradicts the design system for no gain.** Inter is a fine face but shares nothing with the mono used for every number, so digits and prose would come from unrelated families |
| Google Fonts CDN | 0 bytes installed | The `@import` the design system ships verbatim | Google | OFL-1.1 | **Low: sends every visitor's IP and User-Agent to a third party on every page load.** A German regional court (LG München I, 2022) held that this violates the GDPR absent consent; this project is developed in Denmark and would serve EU players. Cache partitioning has eliminated the shared-cross-site-cache counter-argument |
| Build in-house (download the woff2, hand-write `@font-face`) | 0 bytes installed | Full control; exactly the files we choose | Us | OFL-1.1 | Medium: genuinely simple and the closest challenger — see section 4 |
| System font stack | 0 bytes | `system-ui, -apple-system, Segoe UI, …`. No download at all, no layout shift, instant render | n/a | n/a | Medium: free and fast, and the fallback if this is ever dropped, but the metrics vary per platform under a design system built on a fixed 4px grid and fixed chrome heights |

Why the others lost: keeping Inter is the "change nothing" option and loses because it would leave the body face unrelated to the numeric face in a product where numbers and prose sit adjacent constantly. The CDN is rejected on privacy grounds, not performance. The system stack is a legitimate zero-cost position and remains the fallback. Hand-rolling `@font-face` is weighed in section 4.

## 3. Decision & rationale

Adopt **@fontsource-variable/ibm-plex-sans 5.3.0**, self-hosted, importing the `wght` axis only.

**Self-hosting is the substantive half of this decision and it is a privacy decision before a performance one.** Loading fonts from `fonts.googleapis.com` transmits every visitor's IP address and `User-Agent` to Google on every page load, which the LG München I ruling treats as a GDPR violation without consent. Self-hosting removes the third-party origin entirely: no consent-banner implications, no data leaving our infrastructure, no dependence on another operator's uptime or TLS. Because browsers partition their HTTP caches per top-level site, the shared-cache argument for the CDN no longer exists — a visitor downloads the font from us either way.

Given self-hosting is settled, this package is simply the least-effort way to do it. Our setup **diverges deliberately from the design system as published**: [layout.css](../../Frontend/src/routes/layout.css) imports from Fontsource rather than mirroring the upstream `tokens/fonts.css` Google Fonts `@import`. That divergence is recorded in [index.css](../../Frontend/src/lib/styles/openidle/index.css).

Of the three faces this is the only one with a variable build, so it is also the cheapest: `wght.css` is a single file covering 100–700, rather than one download per weight. Importing the axis-specific entry point rather than `index.css` keeps the `wdth` axis — which nothing uses — out of the build.

The superfamily relationship with [@fontsource/ibm-plex-mono](./fontsource-ibm-plex-mono.md) is the reason to prefer this over simply keeping Inter. In a dense HUD where a sentence of prose sits directly beside a tabular count, prose and digits sharing letterform skeletons is the difference between a designed panel and two fonts in a box.

### Pros

- **No third-party origin at runtime** — no visitor data leaves our infrastructure, sidestepping the GDPR exposure of the Google Fonts CDN.
- **Variable**: the full weight axis in one file, rather than one download per weight — the only one of the three faces that gets this.
- Axis-specific entry points mean the unused `wdth` axis and italics never enter the build.
- Shares its skeleton with the mono face used for every number in the product.
- Subsets are split by `unicode-range`, so Cyrillic and Greek ranges are not fetched by Latin-only players.
- Zero dependencies and **no install or postinstall scripts**.
- No JavaScript reaches the browser; the package contributes CSS and binary font files only.

### Cons

- **The typeface is a substitution, not a brand decision** — see section 1.
- 1.5 MB unpacked for what is ultimately one or two woff2 files and some CSS.
- A `package.json` entry for an asset, which is arguably not what a package manager is for.
- Adds a webfont download to first paint; a system font stack would render instantly with no layout shift.
- Replacing Inter means the shadcn `vega` preset is now running on a face it was not drawn against. In practice this is unobservable — both are neutral grotesques at similar optical sizes — but it is a documented divergence from the preset.

## 4. Build-vs-buy

Doing this by hand is easy: download the variable woff2, drop it in `static/`, write two or three `@font-face` rules with `font-weight: 100 700`, `font-display: swap` and appropriate `unicode-range` values. Half an hour, zero dependencies, complete control, and a leaner result. By this project's rule of thumb — build what fits in hours — that is the indicated answer.

**Buying wins on the details that are easy to get slightly wrong and never notice.** Correct `unicode-range` subsetting is the main one: get it wrong and either every visitor downloads Cyrillic and Greek glyphs they will never render, or a player's name in an unexpected script silently falls back to a system font. Fontsource has already split those ranges correctly, and has separated the `wght` and `wdth` axes so we can take only what we use — a distinction easy to miss when hand-writing the rules. Font updates and the `font-display` and `size-adjust` defaults come along for the ride, and the whole thing stays under lockfile control rather than becoming a binary checked into `static/` with no version anyone can name.

The margin is small, and the manual route remains a sensible fallback if this dependency is ever unwanted.

## 5. Risk

### Undo risk — low

One `@import` line in [layout.css](../../Frontend/src/routes/layout.css), one `--font-body` declaration in its `@theme` block, and one `package.json` entry. Removing it degrades to `system-ui` — the interface stays entirely functional, only the typeface changes.

The risk is low **because** the font is referenced through a token. Nothing in the codebase names IBM Plex Sans except the `--font-body` declaration; components inherit it via `font-sans` or the `.oi-body-*` classes. Swapping the face when real brand fonts arrive is a one-line change — as this decision itself demonstrates, having just replaced Inter that way.

### Security risk — low

OFL-1.1 font data with MIT packaging, zero dependencies, **no install or postinstall scripts**, no native binaries, no known CVEs. No JavaScript reaches the browser, so there is no runtime code-execution surface of the kind that applies to [bits-ui](./bits-ui.md).

Two residual notes. Font files are parsed by the browser's font engine, historically a source of memory-safety bugs, so a malicious woff2 is a theoretical vector — mitigated by lockfile integrity hashes and by this being one of the most widely deployed font packages in existence. And self-hosting *removes* a security dependency rather than adding one: no third-party origin to be compromised, no external TLS to trust, no availability risk from someone else's CDN.
