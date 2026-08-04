# @fontsource/ibm-plex-mono

- Status: under-review
- Date: 2026-08-04
- Decided by: project owner
- Version / commit pinned: 5.3.0

## 1. Problem

The [OpenIdle Design System](../../Frontend/src/lib/styles/openidle/index.css) reserves IBM Plex Mono for one job: **every number in the product**. Counts, timers, rates, levels, yields — `5s`, `+3 XP`, `×212`, `12.4/min`, `64/100`, `Lv 7`. The design system's own justification is mechanical rather than aesthetic, and it is the strongest of the three font arguments: this is an idle game whose counters and tick timers change every second, and in a proportional face the digits have unequal advances, so a number re-rendering once a second visibly jitters and reflows the elements beside it. A dense HUD of columns that twitch is the failure mode being designed out.

**This typeface is a substitution, and the status above reflects that.** The design system's `tokens/fonts.css` carries the header *"SUBSTITUTED FONTS — no font binaries were provided with the source material"*, and its readme has a standing *"Font substitution — action needed"* item. IBM Plex Mono is the design system's nearest match to an intended feel, not a typeface OpenIdle chose or owns. The *requirement* — non-jittering digits — survives any brand-font decision; the specific face does not.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@fontsource/ibm-plex-mono** (chosen) | 5.3.0, 2.0 MB unpacked, 70 woff2 files, **0 dependencies**, no install scripts | Self-hosted; fixed advance by construction, so digits cannot jitter. Same superfamily as the body face, so numbers and prose share letterform skeletons where they sit adjacent | Active; Fontsource packages the whole Google Fonts catalogue | **OFL-1.1** (font, by IBM); packaging MIT | High: solves the jitter problem outright and matches the body face |
| **`font-variant-numeric: tabular-nums` on the body face** | 0 bytes | IBM Plex Sans has tabular figures. `tnum` gives every digit an equal advance **without a second family** — the exact property the design system is buying a mono face to obtain. The token layer already ships a `.num` helper doing precisely this | n/a | n/a | **Medium-high: the strongest challenger.** It solves the stated mechanical problem for free. It loses on a narrower point — see below |
| Any other monospace webfont (JetBrains Mono, Roboto Mono, Source Code Pro) | comparable | Interchangeable for this purpose | Active | OFL-1.1 / Apache-2.0 | Medium: no worse, but none share a superfamily with the body face, which is this one's only differentiator |
| System monospace stack | 0 bytes | `ui-monospace, SFMono-Regular, Consolas, monospace`. No download; already the tail of `--font-mono` | n/a | n/a | Medium: free and non-jittering, but the face differs per platform, so a HUD built on fixed pixel chrome renders at different widths on macOS, Windows and Linux |
| Google Fonts CDN | 0 bytes installed | The `@import` the design system ships verbatim | Google | OFL-1.1 | **Low: sends every visitor's IP and User-Agent to a third party on every page load.** A German regional court (LG München I, 2022) held that this violates the GDPR absent consent; this project is developed in Denmark and would serve EU players |
| Build in-house (download the woff2, hand-write `@font-face`) | 0 bytes installed | Full control; exactly the files we choose | Us | OFL-1.1 | Medium: simple, and the closest challenger among the self-hosted options — see section 4 |

Why the others lost — and this one deserves stating plainly, because **`tabular-nums` on the body face nearly wins.** It fixes the jitter, costs zero bytes, and the project already has the helper class for it. Two things decide it. First, tabular figures equalise digit advances but not the advances of everything set alongside them — `12.4/min`, `×212`, `4h 18m`, `64/100` mix digits with letters and punctuation, and only a genuinely fixed-advance face makes those whole strings align in a column. Second, the mono face is a *visual* signal as much as a metric one: it marks a value as machine-read state rather than prose, which is why the design system says "numbers are content". The margin is real but it is not large, and if the font budget ever comes under pressure this is the first of the three to reconsider. The system monospace stack loses on cross-platform metric variance under fixed pixel chrome. The CDN is rejected on privacy grounds.

## 3. Decision & rationale

Adopt **@fontsource/ibm-plex-mono 5.3.0**, self-hosted, weights 400/500/600 only.

**Self-hosting is the substantive half of this decision and it is a privacy decision before a performance one.** Loading fonts from `fonts.googleapis.com` transmits every visitor's IP address and `User-Agent` to Google on every page load, which the LG München I ruling treats as a GDPR violation without consent. Self-hosting removes the third-party origin entirely: no consent-banner implications, no data leaving our infrastructure, no dependence on another operator's uptime or TLS. Cache partitioning has already eliminated the shared-cross-site-cache argument that used to favour the CDN.

Given self-hosting is settled, this package is the least-effort way to do it. Our setup **diverges deliberately from the design system as published**: [layout.css](../../Frontend/src/routes/layout.css) imports from Fontsource rather than mirroring the upstream `tokens/fonts.css` Google Fonts `@import`. That divergence is recorded in [index.css](../../Frontend/src/lib/styles/openidle/index.css).

IBM Plex Mono has **no variable build on Fontsource** — only static weights — so the import is per-weight. Only 400, 500 and 600 are imported: 500 and 600 are what the `--type-num-*` tokens reference, and 400 is kept as the default for any bare `font-mono` usage. Unused weights cost CSS bytes rather than bandwidth, since `@font-face` declarations only trigger a download when a rule actually matches.

The `.oi-num-*` classes and the `.num` helper both set `font-variant-numeric: tabular-nums` **in addition** to selecting this face. That is belt-and-braces on purpose: it keeps digits aligned during the fallback window before the webfont loads, which in an idle game is exactly when a counter is already ticking.

### Pros

- **No third-party origin at runtime** — no visitor data leaves our infrastructure, sidestepping the GDPR exposure of the Google Fonts CDN.
- Solves a real, observable defect: digits that jitter and reflow their neighbours once per second.
- Fixed advance applies to whole strings — `12.4/min`, `4h 18m` — not just to the digits within them.
- Shares its skeleton with [@fontsource-variable/ibm-plex-sans](./fontsource-variable-ibm-plex-sans.md), so numbers and adjacent prose read as one system.
- Subsets are split by `unicode-range`; Cyrillic and Greek ranges are not fetched by Latin-only players.
- Zero dependencies and **no install or postinstall scripts**.
- No JavaScript reaches the browser; the package contributes CSS and binary font files only.

### Cons

- **The typeface is a substitution, not a brand decision** — see section 1.
- **The problem is largely solvable for free** with `font-variant-numeric: tabular-nums` on the body face. This is the weakest of the three font decisions on cost-benefit, and it is recorded as such — see section 2.
- **2.0 MB unpacked across 70 woff2 files**, the largest of the three, for a face used only on numbers.
- **No variable build**: three weights means three `@import`s rather than one file covering the axis.
- A `package.json` entry for an asset, which is arguably not what a package manager is for.
- Adds a webfont to first paint on a surface where the fallback is visible precisely because the values are already animating.

## 4. Build-vs-buy

Doing this by hand is easy: download three woff2 files, drop them in `static/`, write three `@font-face` rules with `font-display: swap` and appropriate `unicode-range` values. Under an hour, zero dependencies, and considerably leaner than 2.0 MB in `node_modules`. By this project's rule of thumb — build what fits in hours — that is the indicated answer, and the case for buying is thinner here than for the other two faces because the installed footprint is the largest.

**Buying still wins, narrowly, on the details that are easy to get slightly wrong and never notice.** Correct `unicode-range` subsetting is the main one: get it wrong and either every visitor downloads Cyrillic and Greek glyphs they will never render, or text in an unexpected script silently falls back to a system font. Fontsource has already split those ranges across all three weights — thirty-odd `@font-face` blocks that would otherwise be hand-maintained. Font updates and `font-display` defaults come along for the ride, and the whole thing stays under lockfile control rather than becoming binaries checked into `static/` with no version anyone can name.

Consistency is the tiebreaker rather than effort: having two of the three faces come from Fontsource and one from `static/` would be worse than either uniform answer.

## 5. Risk

### Undo risk — low

Three `@import` lines in [layout.css](../../Frontend/src/routes/layout.css), one `--font-mono` declaration in its `@theme` block, and one `package.json` entry. Removing it degrades to `ui-monospace` and then the platform monospace — still fixed-advance, so **the jitter problem stays solved even with the package gone**. Only the specific face changes.

That is the notable property of this decision: its failure mode is graceful in a way the display face's is not. Nothing in the codebase names IBM Plex Mono except the `--font-mono` declaration; components use `.oi-num-*`, `.num` or `font-mono`.

### Security risk — low

OFL-1.1 font data with MIT packaging, zero dependencies, **no install or postinstall scripts**, no native binaries, no known CVEs. No JavaScript reaches the browser, so there is no runtime code-execution surface of the kind that applies to [bits-ui](./bits-ui.md).

Two residual notes. Font files are parsed by the browser's font engine, historically a source of memory-safety bugs, so a malicious woff2 is a theoretical vector — mitigated by lockfile integrity hashes and by Fontsource being among the most widely deployed font distributions in existence. And self-hosting *removes* a security dependency rather than adding one: no third-party origin to be compromised, no external TLS to trust, no availability risk from someone else's CDN.
