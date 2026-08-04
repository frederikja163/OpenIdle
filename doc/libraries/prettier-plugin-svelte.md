# prettier-plugin-svelte

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 4.1.1 (declared `^4.1.0`)

## 1. Problem

[Prettier](./prettier.md) formats TypeScript, CSS, HTML, and several other languages, but it does not understand `.svelte` files. A Svelte component mixes a `<script>` block, a `<style>` block, and markup containing template directives (`{#each}`, `{#if}`, `{@render}`) in one file. Prettier will not touch it without a plugin — which means the file type that makes up most of the client would be the one file type left unformatted.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **prettier-plugin-svelte 4** (chosen) | 4.1.1, 0 direct deps | The official Svelte formatter plugin; understands template syntax, runes, and snippets; formats script and style blocks by delegating to Prettier's own parsers | Active: 1.5M weekly downloads, latest 2026-06-15, maintained by the Svelte team | MIT | High: the only option, and it is first-party |
| Format `.svelte` manually | 0 bytes | No dependency | n/a | n/a | Low: leaves the majority of the codebase unformatted and inconsistent |
| Switch to a formatter with native Svelte support | n/a | Would remove the plugin layer | n/a | n/a | Low: no such formatter exists. Biome does not support Svelte yet |
| Build in-house | n/a | Exactly our style | Us | n/a | Low: requires a Svelte parser and printer. See build-vs-buy |

Why the others lost: there is no alternative. No formatter handles Svelte natively, and leaving components unformatted would make the formatter decision largely pointless — the files that most need consistent formatting are exactly the ones with mixed markup and logic.

## 3. Decision & rationale

Adopt. This is a mechanical consequence of choosing both [Svelte](./svelte.md) and [Prettier](./prettier.md); with those two settled, this plugin is not really a separate decision. It is registered in `prettier.config.js` along with an override forcing the `svelte` parser for `*.svelte` files.

Two things make it an easy adopt beyond necessity. It has **zero direct dependencies**, so it adds a package entry and nothing else. And it is maintained by the Svelte team rather than by a third party, which matters for a formatter: a formatter that lags a syntax change does not merely fail to format, it can mangle files. Runes and snippets in Svelte 5 were exactly that kind of change, and first-party maintenance is what keeps the plugin in step.

One operational detail worth recording, because it is easy to break: plugin order in `prettier.config.js` is significant. `prettier-plugin-svelte` must be listed before [prettier-plugin-tailwindcss](./prettier-plugin-tailwindcss.md), because the Tailwind plugin needs to wrap the Svelte parser rather than the other way round. The current configuration has this right. Reordering the array produces class sorting that silently stops working inside components.

### Pros

- Zero direct dependencies.
- First-party to the Svelte team, so it tracks syntax changes (runes, snippets) as they land.
- Delegates script and style block formatting to Prettier's own parsers, so components and `.ts` files format consistently.
- Handles the parts that are easy to get wrong: indentation of nested template blocks, attribute wrapping, and preserving whitespace significance in markup.
- MIT, 1.5M weekly downloads.

### Cons

- Formatting quality on complex markup is noticeably weaker than Prettier's on plain TypeScript — long attribute lists and deeply nested blocks sometimes come out worse than hand-formatted.
- Must track Svelte syntax; a Svelte major requires a coordinated plugin release, and until it arrives, formatting new syntax can be wrong rather than merely absent.
- Silent ordering dependency with the Tailwind plugin — a configuration footgun with no error message when it is wrong.
- Smallest user base among our formatting tools, so edge-case bugs surface slowly.
- A formatter that misparses can corrupt source, which is a higher-consequence failure than a linter being wrong.

## 4. Build-vs-buy

Not buildable, for the same reason as [eslint-plugin-svelte](./eslint-plugin-svelte.md): the work is a full-fidelity parser and printer for a mixed markup/script/style language, preserving comments and their attachment points, with a line-breaking algorithm for template syntax. That is months, and the failure mode of getting it subtly wrong is corrupted source files rather than a missing feature.

The zero-cost in-house alternative is simply not formatting `.svelte` files — accept inconsistency in components while formatting everything else. That was considered and rejected: components are where markup and logic interleave, which is precisely where consistent formatting earns its keep, and formatting only the minority of files would make [Prettier](./prettier.md) hard to justify at all.

## 5. Risk

### Undo risk — low

Two lines in `prettier.config.js` — one plugin entry, one parser override. Nothing else references it. Removing it leaves already-formatted files untouched and simply stops formatting components from that point on.

### Security risk — low

Development-only; never runs in production and ships nothing to the browser. Zero direct dependencies, no native binaries, no install or postinstall scripts. MIT, first-party to the Svelte team, no known CVEs.

The one contextual note: Prettier plugins were part of the attack surface in the July 2025 npm compromise that hit `eslint-plugin-prettier` (see [eslint-config-prettier](./eslint-config-prettier.md)). This plugin was not involved, but it is the same category — a small, widely-installed package executed with local privileges during `bun run format` and on editor save. With zero dependencies and no install hooks its own surface is minimal, and exact pinning in `bun.lock` covers the realistic risk.
