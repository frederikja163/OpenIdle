# @fontsource-variable/inter

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 5.3.0

## 1. Problem

The `vega` preset chosen in [shadcn-svelte](./shadcn-svelte.md) specifies Inter as its interface typeface, and the generated token layer sets it as the body font. A webfont has to be delivered somehow: either self-hosted from our own origin, fetched from a third-party CDN, or abandoned in favour of whatever the user's system provides.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@fontsource-variable/inter** (chosen) | 5.3.0, 1.86 MB unpacked, **0 dependencies**. Only the imported variable woff2 subset ships | Self-hosted; a single `@import` pulls the `@font-face` declarations. Variable font — one file covers weights 100–900. Latin and extended subsets split so unused ranges are not fetched | Active; the Fontsource project packages the whole Google Fonts catalogue | **OFL-1.1** (the font); packaging MIT | High: keeps the font on our own origin at essentially no engineering cost |
| Google Fonts CDN | 0 bytes installed | A `<link>` tag; possible cross-site cache hit (largely eliminated by modern cache partitioning) | Google | OFL-1.1 | **Low: sends every visitor's IP to a third party — a GDPR exposure, see section 3** |
| Build in-house (download the woff2, hand-write `@font-face`) | 0 bytes installed | Full control; exactly the files we choose | Us | OFL-1.1 | Medium: genuinely simple and the closest challenger — see section 4 |
| System font stack | 0 bytes | `system-ui, -apple-system, Segoe UI, …`. No download at all, no layout shift, instant render | n/a | n/a | Medium: free and fast, but abandons the preset's typography and varies by platform |

Why the others lost: the Google Fonts CDN is rejected on privacy grounds set out below, not on performance. The system font stack is a legitimate zero-cost position and remains the fallback if the font is ever dropped, but it gives up the visual baseline that was one of the stated reasons for adopting shadcn-svelte at all. Hand-rolling `@font-face` is weighed in section 4.

## 3. Decision & rationale

Adopt **@fontsource-variable/inter 5.3.0**, entailed by the preset choice in [shadcn-svelte](./shadcn-svelte.md) — but the *self-hosting* half of this decision is independently argued and would stand on its own.

**Self-hosting is the substantive choice here, and it is a privacy decision before it is a performance one.** Loading fonts from `fonts.googleapis.com` transmits every visitor's IP address and `User-Agent` to Google on every page load. A German regional court (LG München I, 2022) held that doing so without consent violates the GDPR, and the reasoning applies across the EU; this project is developed in Denmark and would serve EU players. Self-hosting removes the third-party origin entirely — no consent banner implications, no data leaving our infrastructure, no reliance on another operator's uptime or TLS. The historical argument for the CDN, a shared cross-site cache, no longer holds: browsers partition their HTTP caches per top-level site, so a visitor downloads the font from us either way.

Given that self-hosting is settled, this package is simply the least-effort way to do it: one `@import` in [layout.css](../../Frontend/src/routes/layout.css), correct `@font-face` blocks with `unicode-range` subsetting already written, and a variable font so the entire 100–900 weight axis is one file rather than nine.

The installed footprint is misleading. The 1.86 MB unpacked covers every subset and both variable axes; the build emits only the woff2 files the imported subset actually references, and Vite fingerprints and serves them from our own origin.

### Pros

- **No third-party origin at runtime** — no visitor data leaves our infrastructure, sidestepping the GDPR exposure of the Google Fonts CDN.
- One `@import` replaces hand-written `@font-face` blocks, `unicode-range` subsetting and file management.
- Variable font: the full weight axis in a single file, rather than one download per weight.
- Subsets are split, so extended Latin, Cyrillic and Greek ranges are not fetched by Latin-only users.
- Zero dependencies; no JavaScript reaches the browser — the output is CSS plus font files.
- Updates arrive through the lockfile like any other package.

### Cons

- 1.86 MB unpacked for what is ultimately a couple of woff2 files and some CSS.
- A `package.json` entry for an asset, which is arguably not what a package manager is for.
- Adds a webfont download to first paint; a system font stack would render instantly with no layout shift.
- OFL-1.1 rather than MIT — permissive and standard for fonts, but a third licence family in the set.
- Tied to the preset: a different [shadcn-svelte](./shadcn-svelte.md) preset would want a different typeface.

## 4. Build-vs-buy

Doing this by hand is genuinely easy and the effort estimate is honest: download the Inter variable woff2, drop it in `static/`, write two or three `@font-face` rules with `font-weight: 100 900`, `font-display: swap` and appropriate `unicode-range` values. Half an hour, zero dependencies, complete control. By this project's rule of thumb — build what fits in hours — that is the indicated answer, and it would produce a marginally leaner result.

**Buying wins on the details that are easy to get slightly wrong and never notice.** Correct `unicode-range` subsetting is the main one: get it wrong and either every visitor downloads Cyrillic and Greek glyphs they will never render, or a player's name in an unexpected script silently falls back to a system font. Fontsource has already split those ranges correctly. Font updates and the `font-display` and `size-adjust` defaults come along for the ride, and the whole thing stays under lockfile control rather than becoming a binary checked into `static/` with no version anyone can name.

The margin is small, and the manual route remains a sensible fallback if this dependency is ever unwanted — the font licence permits it and nothing else depends on the package. It is recorded as adopted because the packaged version costs one line and removes a class of quiet mistakes.

## 5. Risk

### Undo risk — low

One `@import` line in [layout.css](../../Frontend/src/routes/layout.css) and one `package.json` entry. Removing it degrades to whatever the CSS font stack falls back to — the interface stays entirely functional, only the typeface changes. Replacing it with hand-written `@font-face` rules is the half-hour job described above.

### Security risk — low

OFL-1.1 font data with MIT packaging, zero dependencies, no install or postinstall scripts, no native binaries, no known CVEs. **No JavaScript reaches the browser** — the package contributes CSS and binary font files only, so there is no runtime code-execution surface of the kind that applies to [bits-ui](./bits-ui.md).

Two residual notes. Font files are parsed by the browser's font engine, historically a source of memory-safety bugs, so a malicious woff2 is a theoretical vector — mitigated by lockfile integrity hashes and by this being one of the most widely deployed font packages in existence. And self-hosting *removes* a security dependency rather than adding one: there is no third-party origin to be compromised, no external TLS to trust, and no availability risk from someone else's CDN.
